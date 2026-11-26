using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DEFLATE_custom_chart.Core.Bms
{
    /// <summary>
    /// BMS (.bms, .bme, .bml) 차트 파서 뼈대 클래스 (개선판)
    /// </summary>
    public class BmsParser
    {
        // 1마디 당 표준 틱 수 (예: 4박자 x 960 = 3840 틱)
        public const int TicksPerMeasure = 3840;

        // 4분음표 1박 틱 수 (라인바 박자선 간격)
        public const int TicksPerBeat = TicksPerMeasure / 4;

        private static readonly char[] WhitespaceChars = { ' ', '\t' };

        private static bool IsAllZeros(string data, int start, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (data[start + i] != '0') return false;
            }
            return true;
        }

        // 지원하지 않는 BMS 기능을 만났는지 (파싱 끝에 경고 한 줄씩). 파서 인스턴스는 파일마다 새로 만든다.
        private bool _sawStop;
        private bool _sawLnChannel;

        private static bool IsAllZerosOrEmpty(string line, int start)
        {
            for (int i = start; i < line.Length; i++)
            {
                char c = line[i];
                if (c != '0' && !char.IsWhiteSpace(c)) return false;
            }
            return true;
        }

        private static bool TryParse3Digits(string line, int startIndex, out int value)
        {
            value = 0;
            if (line == null || startIndex + 3 > line.Length) return false;
            int d0 = line[startIndex] - '0';
            int d1 = line[startIndex + 1] - '0';
            int d2 = line[startIndex + 2] - '0';
            if ((uint)d0 > 9 || (uint)d1 > 9 || (uint)d2 > 9) return false;
            value = d0 * 100 + d1 * 10 + d2;
            return true;
        }

        /// <summary>
        /// 파일 경로로부터 BMS 데이터를 파싱합니다.
        /// </summary>
        public BmsChart ParseFile(string filePath, int targetSampleRate = 44100)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"BMS 파일을 찾을 수 없습니다: {filePath}");
            }

            // UTF-8 고정으로 읽으면 Shift-JIS/CP949 BMS의 제목·#WAV 키음 이름이 깨진다 → 인코딩 자동 판별.
            string content = TextDecoder.ReadAllText(filePath, TextDecoder.Preference.Chart);
            if (!TextDecoder.LastEncodingName.StartsWith("utf-", StringComparison.Ordinal))
            {
                ModLog.Verbose($"[BMS 파싱] {Path.GetFileName(filePath)} 인코딩: {TextDecoder.LastEncodingName}");
            }
            return Parse(content, targetSampleRate);
        }

        /// <summary>
        /// BMS 파일 전체 내용을 사전 스캔하여 노트 Value 폭(2글자 vs 3글자 확장)을 자동 감지합니다.
        /// </summary>
        public int DetectNoteValueWidth(string bmsContent)
        {
            using (var reader = new StringReader(bmsContent))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int start = 0;
                    while (start < line.Length && char.IsWhiteSpace(line[start])) start++;
                    if (line.Length - start < 5 || line[start] != '#') continue;
                    if (string.Compare(line, start, "#WAV", 0, 4, StringComparison.OrdinalIgnoreCase) != 0) continue;

                    // 예: #WAV01 sample.wav -> key: 01 (len 2)
                    // 예: #WAV1A2 sample.wav -> key: 1A2 (len 3)
                    int keyStart = start + 4;
                    int spaceIdx = line.IndexOfAny(WhitespaceChars, keyStart);
                    int keyEnd = spaceIdx > 0 ? spaceIdx : line.Length;
                    if (keyEnd - keyStart == 3)
                    {
                        return 3; // 확장 3글자 ID 감지
                    }
                }
            }

            return 2; // 기본 2글자 ID
        }

        /// <summary>
        /// BMS 문자열 내용을 파싱하여 BmsChart 객체로 반환합니다.
        /// </summary>
        public BmsChart Parse(string bmsContent, int targetSampleRate = 44100)
        {
            var chart = new BmsChart { SampleRate = targetSampleRate };
            int noteWidth = DetectNoteValueWidth(bmsContent);
            chart.Header.NoteValueWidth = noteWidth;

            var rawChannelData = new List<RawMeasureChannel>();
            var measureLengths = new Dictionary<int, float>();
            int maxMeasure = 0;

            // 1차 스캔: 헤더 및 마디/채널 데이터 수집
            var branches = new BranchState();
            using (var reader = new StringReader(bmsContent))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line[0] != '#') continue;

                    // #RANDOM/#IF 분기: 예전에는 모든 분기의 노트를 한꺼번에 읽어 노트가 겹쳐 나왔다. 1번 분기로 고정한다.
                    if (branches.TryHandleControlLine(line)) continue;
                    if (!branches.IsActive) continue;

                    ParseLine(line, chart, rawChannelData, measureLengths, noteWidth, ref maxMeasure);
                }
            }

            if (branches.SawRandom)
            {
                chart.Warnings.Add("#RANDOM 분기가 있어 1번 분기(#IF 1)만 읽었습니다 (무작위 선택은 지원하지 않음)");
            }
            if (_sawStop)
            {
                chart.Warnings.Add("STOP(#STOPxx / 09 채널)은 지원하지 않습니다. STOP 이후 노트가 그 길이만큼 일찍 나옵니다");
            }
            if (_sawLnChannel)
            {
                chart.Warnings.Add("LN 채널(5x/6x, #LNTYPE)은 지원하지 않아 무시했습니다. 홀드는 키음 이름(홀드 시작/홀드 끝) 또는 #LNOBJ로 지정하세요");
            }

            // 마디별 시작 틱 위치 계산 (마디 배율 #XXX02 적용)
            var measureStartTicks = CalculateMeasureStartTicks(measureLengths, maxMeasure);

            // BPM 변경 이벤트 목록 수집 및 틱 순서 정렬
            BuildBpmTimeline(chart, rawChannelData, measureStartTicks, noteWidth);

            // 마디선 / 박자선 위치 계산 (인게임 라인바 주입용)
            GenerateBarLines(chart, measureStartTicks, targetSampleRate);

            // 노트 생성 및 틱/시간(Seconds)/샘플위치 계산
            GenerateNotes(chart, rawChannelData, measureStartTicks, noteWidth, targetSampleRate);

            // 이후 페어링 및 출력에서 정렬 재사용이 가능하도록 (Tick, Channel) 순으로 1회 인플레이스 정렬
            chart.Notes.Sort(CompareNotesByTickThenChannel);

            // 롱노트(LN) Head-Tail 페어링 처리
            PairLongNotes(chart);

            // 키음 이름("홀드 시작"/"홀드 끝") 기반 홀드 페어링 (GRC2 / sxtg2와 동일한 매칭 시맨틱)
            PairHoldNotesByKeysound(chart);

            // 짝을 못 찾은 홀드 Head는 단타로 강등 (EndSample이 StartSample보다 앞서는 유령 노트 방지)
            SanitizeUnpairedLongNotes(chart);

            // 인게임 O(1) 조회용 레인별 노트 인덱스 구축
            chart.RebuildLaneIndex();

            return chart;
        }

        private static int CompareNotesByTickThenChannel(BmsNote a, BmsNote b)
        {
            int tickCmp = a.Tick.CompareTo(b.Tick);
            return tickCmp != 0 ? tickCmp : string.CompareOrdinal(a.Channel, b.Channel);
        }

        private void ParseLine(
            string line,
            BmsChart chart,
            List<RawMeasureChannel> rawChannelData,
            Dictionary<int, float> measureLengths,
            int noteWidth,
            ref int maxMeasure)
        {
            // BMS 본문의 대다수를 차지하는 #XXXYY:ZZZZ 채널 라인을 헤더 StartsWith 체인보다 먼저 판별한다.
            if (line.Length >= 7 && line[6] == ':' && TryParse3Digits(line, 1, out int measure))
            {
                if (measure > maxMeasure) maxMeasure = measure;
                string channel = line.Substring(4, 2);

                // #XXX02: 마디 길이 변형 (배율 float 지정)
                if (channel == "02")
                {
                    string data = line.Substring(7).Trim();
                    if (float.TryParse(data, NumberStyles.Any, CultureInfo.InvariantCulture, out float lenMult) && lenMult >= 0f)
                    {
                        measureLengths[measure] = lenMult;
                    }
                }
                else if (channel == "09")
                {
                    if (!IsAllZerosOrEmpty(line, 7)) _sawStop = true;
                }
                else if ((channel[0] == '5' || channel[0] == '6') && !IsAllZerosOrEmpty(line, 7))
                {
                    _sawLnChannel = true;
                }
                else if (channel == "03" || channel == "08" || BmsLaneMapper.IsNoteChannel(channel))
                {
                    // 사용하지 않는 BGM(01)/BGA(04) 등 수백 줄의 채널 데이터 객체 할당을 생략한다.
                    rawChannelData.Add(new RawMeasureChannel
                    {
                        Measure = measure,
                        Channel = channel,
                        Data = line.Substring(7).Trim()
                    });
                }
                return;
            }

            // 헤더 처리
            if (line.StartsWith("#TITLE", StringComparison.OrdinalIgnoreCase))
            {
                chart.Header.Title = ExtractHeaderValue(line, "#TITLE");
            }
            else if (line.StartsWith("#SUBTITLE", StringComparison.OrdinalIgnoreCase))
            {
                chart.Header.SubTitle = ExtractHeaderValue(line, "#SUBTITLE");
            }
            else if (line.StartsWith("#ARTIST", StringComparison.OrdinalIgnoreCase))
            {
                chart.Header.Artist = ExtractHeaderValue(line, "#ARTIST");
            }
            else if (line.StartsWith("#GENRE", StringComparison.OrdinalIgnoreCase))
            {
                chart.Header.Genre = ExtractHeaderValue(line, "#GENRE");
            }
            else if (line.StartsWith("#BPM", StringComparison.OrdinalIgnoreCase) && line.Length > 4 && char.IsWhiteSpace(line[4]))
            {
                // "#BPM 150" / "#BPM\t150" (공백·탭 모두) ➔ 초기 BPM
                if (float.TryParse(ExtractHeaderValue(line, "#BPM"), NumberStyles.Any, CultureInfo.InvariantCulture, out float bpm) && bpm > 0f)
                {
                    chart.Header.InitialBpm = bpm;
                }
            }
            else if (line.StartsWith("#PLAYLEVEL", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(ExtractHeaderValue(line, "#PLAYLEVEL"), out int level))
                {
                    chart.Header.PlayLevel = level;
                }
            }
            else if (line.StartsWith("#TOTAL", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(ExtractHeaderValue(line, "#TOTAL"), NumberStyles.Any, CultureInfo.InvariantCulture, out float total))
                {
                    chart.Header.Total = total;
                }
            }
            else if (line.StartsWith("#LNTYPE", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(ExtractHeaderValue(line, "#LNTYPE"), out int lntype))
                {
                    chart.Header.LnType = lntype;
                }
            }
            else if (line.StartsWith("#LNOBJ", StringComparison.OrdinalIgnoreCase))
            {
                chart.Header.LnObj = ExtractHeaderValue(line, "#LNOBJ").Trim().ToUpperInvariant();
            }
            else if (line.StartsWith("#STOP", StringComparison.OrdinalIgnoreCase))
            {
                _sawStop = true;
            }
            else if (line.StartsWith("#WAV", StringComparison.OrdinalIgnoreCase) && line.Length >= (4 + noteWidth))
            {
                string wavKey = line.Substring(4, noteWidth).ToUpperInvariant();
                string wavFile = line.Substring(4 + noteWidth).Trim();
                chart.Header.WavTable[wavKey] = wavFile;
            }
            else if (line.StartsWith("#BPM", StringComparison.OrdinalIgnoreCase) && line.Length >= (4 + noteWidth) && !char.IsWhiteSpace(line[4]))
            {
                // "#BPMxx 150" ➔ 확장 BPM 테이블 (08 채널용)
                string bpmKey = line.Substring(4, noteWidth).ToUpperInvariant();
                string bpmStr = line.Substring(4 + noteWidth).Trim();
                if (float.TryParse(bpmStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float exBpm))
                {
                    chart.Header.BpmTable[bpmKey] = exBpm;
                }
            }
        }

        private string ExtractHeaderValue(string line, string prefix)
        {
            if (line.Length <= prefix.Length) return string.Empty;
            return line.Substring(prefix.Length).Trim();
        }

        /// <summary>
        /// 데이터 길이가 오브젝트 폭의 배수가 아니면 오브젝트 위치를 나눌 수 없으므로 그 줄 전체를 건너뛴다.
        /// 조용히 노트가 사라지지 않도록 경고를 남긴다.
        /// </summary>
        private void WarnMisalignedData(BmsChart chart, RawMeasureChannel raw, int noteWidth)
        {
            chart.Warnings.Add($"#{raw.Measure:D3}{raw.Channel}: 데이터 길이 {raw.Data.Length}가 오브젝트 폭 {noteWidth}의 배수가 아니라 이 줄을 건너뜀");
        }

        /// <summary>
        /// 변박자(#XXX02)를 고려하여 각 마디의 시작 Tick 위치를 구합니다.
        /// </summary>
        private Dictionary<int, double> CalculateMeasureStartTicks(Dictionary<int, float> measureLengths, int maxMeasure)
        {
            var startTicks = new Dictionary<int, double>();
            double currentTick = 0.0;

            for (int m = 0; m <= maxMeasure + 1; m++)
            {
                startTicks[m] = currentTick;
                float multiplier = measureLengths.TryGetValue(m, out float lenMult) ? lenMult : 1.0f;
                currentTick += TicksPerMeasure * multiplier;
            }

            return startTicks;
        }

        /// <summary>
        /// 초기 BPM 및 03/08 채널의 BPM 변경 이벤트를 타임라인 틱 순으로 구성합니다.
        /// </summary>
        private void BuildBpmTimeline(
            BmsChart chart,
            List<RawMeasureChannel> rawChannels,
            Dictionary<int, double> measureStartTicks,
            int noteWidth)
        {
            chart.BpmEvents.Clear();
            int order = 0;

            // 0번 Tick에 초기 BPM 설정
            chart.BpmEvents.Add(new BmsBpmEvent
            {
                OrderIndex = order++,
                Tick = 0,
                Bpm = chart.Header.InitialBpm,
                TimeSeconds = 0.0
            });

            foreach (var raw in rawChannels)
            {
                if (raw.Channel != "03" && raw.Channel != "08") continue;
                if (string.IsNullOrEmpty(raw.Data)) continue;

                // 03 채널 값은 오브젝트 ID가 아니라 BPM 자체(16진 00~FF)라서 3자리 확장 ID 파일에서도 항상 2자리다.
                // 예전에는 ID 폭(3)으로 나눠 "길이 4가 폭 3의 배수가 아님"으로 줄 전체를 버려 변속이 사라졌다.
                int width = raw.Channel == "03" ? 2 : noteWidth;
                if (raw.Data.Length % width != 0)
                {
                    WarnMisalignedData(chart, raw, width);
                    continue;
                }

                int objectCount = raw.Data.Length / width;
                double mStartTick = measureStartTicks.TryGetValue(raw.Measure, out double bms) ? bms : raw.Measure * TicksPerMeasure;
                double mLengthTicks = (measureStartTicks.TryGetValue(raw.Measure + 1, out double bme) ? bme : (raw.Measure + 1) * TicksPerMeasure) - mStartTick;

                for (int i = 0; i < objectCount; i++)
                {
                    int startIdx = i * width;
                    if (IsAllZeros(raw.Data, startIdx, width)) continue;

                    string val = raw.Data.Substring(startIdx, width);

                    double eventTick = mStartTick + ((double)i / objectCount) * mLengthTicks;
                    float newBpm = 0.0f;

                    if (raw.Channel == "03")
                    {
                        // 03 채널: 16진수 직접 지정 BPM
                        if (byte.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte hexBpm))
                        {
                            newBpm = hexBpm;
                        }
                    }
                    else if (raw.Channel == "08")
                    {
                        // 08 채널: #BPMxx 확장 테이블의 지정 BPM
                        string key = val.ToUpperInvariant();
                        if (chart.Header.BpmTable.TryGetValue(key, out float tableBpm))
                        {
                            newBpm = tableBpm;
                        }
                    }

                    if (newBpm > 0)
                    {
                        chart.BpmEvents.Add(new BmsBpmEvent
                        {
                            OrderIndex = order++,
                            Tick = eventTick,
                            Bpm = newBpm,
                            TimeSeconds = 0.0 // 아래 누적 연산에서 채움
                        });
                    }
                }
            }

            // Tick 순서대로 정렬 후 시간(TimeSeconds) 누적 계산 (Tick 일치 시 출현 순서 OrderIndex 유지)
            chart.BpmEvents.Sort(CompareBpmEventsByTick);

            double prevTick = 0.0;
            double prevTime = 0.0;
            float currentBpm = chart.Header.InitialBpm > 0f ? chart.Header.InitialBpm : 130.0f;

            foreach (var bpmEv in chart.BpmEvents)
            {
                double deltaTick = bpmEv.Tick - prevTick;
                // 공식: time = tick * 240 / (bpm * TicksPerMeasure)
                double safeBpm = currentBpm > 0f ? currentBpm : 130.0;
                double deltaTime = deltaTick * 240.0 / (safeBpm * TicksPerMeasure);
                prevTime += deltaTime;
                bpmEv.TimeSeconds = prevTime;

                prevTick = bpmEv.Tick;
                if (bpmEv.Bpm > 0f) currentBpm = bpmEv.Bpm;
            }
        }

        private static int CompareBpmEventsByTick(BmsBpmEvent a, BmsBpmEvent b)
        {
            int cmp = a.Tick.CompareTo(b.Tick);
            return cmp != 0 ? cmp : a.OrderIndex.CompareTo(b.OrderIndex);
        }

        /// <summary>
        /// 주어진 Tick 위치에 대한 정확한 시간(초)을 타임라인을 기초하여 이진 탐색으로 구합니다.
        /// 공식: time = prevTime + (tick - prevTick) * 240 / (bpm * TicksPerMeasure)
        /// </summary>
        private double CalculateTimeAtTick(double tick, List<BmsBpmEvent> bpmEvents)
        {
            if (bpmEvents == null || bpmEvents.Count == 0) return 0.0;
            if (tick <= 0.0) return 0.0;

            int lo = 0;
            int hi = bpmEvents.Count - 1;
            int bestIdx = 0;

            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (bpmEvents[mid].Tick <= tick)
                {
                    bestIdx = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            BmsBpmEvent currentEvent = bpmEvents[bestIdx];
            double safeBpm = currentEvent.Bpm > 0f ? currentEvent.Bpm : 130.0;
            double deltaTick = tick - currentEvent.Tick;
            double deltaTime = deltaTick * 240.0 / (safeBpm * TicksPerMeasure);
            return Math.Max(0.0, currentEvent.TimeSeconds + deltaTime);
        }

        /// <summary>
        /// 인게임 라인바로 쓸 마디선/박자선을 만듭니다.
        /// 마디 시작마다 마디선 하나, 그 마디 안에서는 4분음표(960틱)마다 박자선 하나.
        /// 변박자(#XXX02) 마디는 길이만큼만 박자선이 들어가고, 마지막 마디 뒤에 곡을 닫는 마디선이 하나 더 붙습니다.
        /// </summary>
        private void GenerateBarLines(BmsChart chart, Dictionary<int, double> measureStartTicks, int sampleRate)
        {
            chart.BarLines.Clear();
            if (measureStartTicks.Count == 0) return;

            int lastMeasure = measureStartTicks.Count - 1;
            for (int m = 0; m <= lastMeasure; m++)
            {
                double measureStart = measureStartTicks[m];
                AddBarLine(chart, measureStart, true, sampleRate);
                if (m == lastMeasure) break; // 마지막 키는 곡을 닫는 마디선 자리

                double measureEnd = measureStartTicks[m + 1];
                for (double tick = measureStart + TicksPerBeat; tick < measureEnd - 0.5; tick += TicksPerBeat)
                {
                    AddBarLine(chart, tick, false, sampleRate);
                }
            }
        }

        private void AddBarLine(BmsChart chart, double tick, bool isMeasureStart, int sampleRate)
        {
            // 길이 0 마디(#XXX02:0 등)로 같은 자리에 선이 겹치면 하나만 남긴다.
            if (chart.BarLines.Count > 0 && chart.BarLines[chart.BarLines.Count - 1].Tick >= tick) return;

            double timeSec = CalculateTimeAtTick(tick, chart.BpmEvents);
            chart.BarLines.Add(new BmsBarLine
            {
                Tick = tick,
                TimeSeconds = timeSec,
                SamplePosition = (int)(timeSec * sampleRate),
                IsMeasureStart = isMeasureStart
            });
        }

        /// <summary>
        /// 일반 노트 및 롱노트를 파싱하여 생성합니다.
        /// </summary>
        private void GenerateNotes(
            BmsChart chart,
            List<RawMeasureChannel> rawChannels,
            Dictionary<int, double> measureStartTicks,
            int noteWidth,
            int sampleRate)
        {
            chart.Notes.Clear();

            foreach (var raw in rawChannels)
            {
                // 노트 채널 여부 확인: BmsLaneMapper가 아는 레인 채널(16/11/12/13/14)만 노트로 취급.
                if (!BmsLaneMapper.IsNoteChannel(raw.Channel)) continue;
                if (string.IsNullOrEmpty(raw.Data)) continue;
                if (raw.Data.Length % noteWidth != 0)
                {
                    WarnMisalignedData(chart, raw, noteWidth);
                    continue;
                }

                int objectCount = raw.Data.Length / noteWidth;
                double mStartTick = measureStartTicks.TryGetValue(raw.Measure, out double ms) ? ms : raw.Measure * TicksPerMeasure;
                double mEndTick = measureStartTicks.TryGetValue(raw.Measure + 1, out double me) ? me : (raw.Measure + 1) * TicksPerMeasure;
                double mLengthTicks = mEndTick - mStartTick;

                for (int i = 0; i < objectCount; i++)
                {
                    int startIdx = i * noteWidth;
                    if (IsAllZeros(raw.Data, startIdx, noteWidth)) continue;

                    string val = raw.Data.Substring(startIdx, noteWidth).ToUpperInvariant();
                    // 열은 채널, 하이햇/킥·스네어는 키음 파일명으로 정해 노트 하나를 정확히 한 게임 레인에 배치한다.
                    chart.Header.WavTable.TryGetValue(val, out string keysoundFile);
                    string resolvedLane = BmsLaneMapper.ResolveGameLane(raw.Channel, keysoundFile);
                    if (resolvedLane == null) continue;
                    string hint = BmsLaneMapper.KeysoundLaneHintMismatch(keysoundFile, resolvedLane);
                    if (hint != null) chart.KeysoundLaneMismatchCount++;

                    double noteTick = mStartTick + ((double)i / objectCount) * mLengthTicks;
                    double timeSec = CalculateTimeAtTick(noteTick, chart.BpmEvents);
                    int samplePos = (int)(timeSec * sampleRate);

                    chart.Notes.Add(new BmsNote
                    {
                        Measure = raw.Measure,
                        Tick = noteTick,
                        TimeSeconds = timeSec,
                        SamplePosition = samplePos,
                        Channel = raw.Channel,
                        NoteValue = val,
                        ResolvedLane = resolvedLane
                        // IsLongNote는 여기서 정하지 않는다. 아래 #LNOBJ 처리와
                        // 키음("홀드 시작"/"홀드 끝") 매칭 단계에서 확정된다.
                    });
                }
            }
        }

        private static Dictionary<string, List<BmsNote>> GroupSortedNotesByChannel(List<BmsNote> sortedNotes)
        {
            var grouped = new Dictionary<string, List<BmsNote>>(StringComparer.OrdinalIgnoreCase);
            foreach (var note in sortedNotes)
            {
                if (!grouped.TryGetValue(note.Channel, out var list))
                {
                    list = new List<BmsNote>();
                    grouped[note.Channel] = list;
                }
                list.Add(note);
            }
            return grouped;
        }

        /// <summary>
        /// #LNOBJ로 지정된 롱노트 종단 오브젝트의 Head-Tail 짝을 맞춰 롱노트 종단 정보를 갱신합니다.
        /// (호출 시점에 chart.Notes는 이미 Tick 순으로 정렬되어 있어야 합니다.)
        /// </summary>
        private void PairLongNotes(BmsChart chart)
        {
            string lnObjKey = chart.Header.LnObj;
            if (string.IsNullOrEmpty(lnObjKey) || chart.Notes.Count == 0) return;

            var toRemove = new HashSet<BmsNote>();
            var notesByChannel = GroupSortedNotesByChannel(chart.Notes);

            foreach (var orderedNotes in notesByChannel.Values)
            {
                for (int i = 1; i < orderedNotes.Count; i++)
                {
                    if (orderedNotes[i].NoteValue != lnObjKey) continue;

                    var head = orderedNotes[i - 1];
                    var tail = orderedNotes[i];

                    head.IsLongNote = true;
                    head.LongNoteEndTick = tail.Tick;
                    head.LongNoteEndTimeSeconds = tail.TimeSeconds;
                    head.LongNoteEndSamplePosition = tail.SamplePosition;

                    // Tail 전용 LNOBJ 개체는 일괄 삭제 대상에 추가
                    toRemove.Add(tail);
                }
            }

            if (toRemove.Count > 0)
            {
                chart.Notes.RemoveAll(n => toRemove.Contains(n));
            }
        }

        // =====================================================================
        // 키음 이름 기반 홀드 시작/끝 매칭
        // =====================================================================

        private static readonly string[] HoldStartKeywords = { "홀드 시작", "홀드시작", "hold start", "holdstart", "hold_start", "ln start", "lnstart" };
        private static readonly string[] HoldEndKeywords = { "홀드 끝", "홀드끝", "hold end", "holdend", "hold_end", "ln end", "lnend" };

        private static bool ContainsAny(string text, string[] keywords)
        {
            foreach (var kw in keywords)
            {
                if (text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// #WAV 키음 파일명이 "홀드 시작" / "홀드 끝"인 노트를 Head / Tail로 보고 짝을 맞춥니다.
        /// (호출 시점에 chart.Notes는 이미 Tick 순으로 정렬되어 있어야 합니다.)
        ///
        /// 매칭 규칙 (GRC2 <c>HoldNoteProcessor.MatchHoldNotes</c> / sxtg2 <c>CalculateHoldNoteLengths</c>와 동일):
        ///   - 레인(= BMS 채널)이 같고, Head보다 뒤에 있는 Tail 중 **가장 가까운 것** 하나와 짝
        ///   - 한 번 소비된 Tail은 다른 Head가 다시 가져가지 못함
        ///   - 짝이 맞은 Tail은 차트에서 제거 (안 그러면 홀드 끝 지점에 유령 단타가 하나 더 생김)
        ///   - 짝을 못 찾은 Head는 단타로 강등, 고아 Tail은 제거
        /// </summary>
        private void PairHoldNotesByKeysound(BmsChart chart)
        {
            var wavTable = chart.Header.WavTable;
            if (wavTable.Count == 0 || chart.Notes.Count == 0) return;

            // 1. 키음 파일명으로 Head / Tail 분류
            int startCount = 0, endCount = 0;
            foreach (var note in chart.Notes)
            {
                if (string.IsNullOrEmpty(note.NoteValue)) continue;
                if (!wavTable.TryGetValue(note.NoteValue, out string wavFile) || string.IsNullOrEmpty(wavFile)) continue;

                note.KeysoundFile = wavFile;

                if (ContainsAny(wavFile, HoldEndKeywords)) { note.IsHoldEnd = true; endCount++; }
                else if (ContainsAny(wavFile, HoldStartKeywords)) { note.IsHoldStart = true; startCount++; }
            }

            if (startCount == 0 && endCount == 0) return;

            // 2. 레인(채널)별로 Head ➔ 가장 가까운 Tail 매칭 (chart.Notes는 이미 Tick 순)
            var consumedTails = new HashSet<BmsNote>();
            var toRemove = new HashSet<BmsNote>();
            int paired = 0, orphanHeads = 0, orphanTails = 0;

            foreach (var ordered in GroupSortedNotesByChannel(chart.Notes).Values)
            {
                int tailSearchIdx = 0;

                for (int i = 0; i < ordered.Count; i++)
                {
                    var head = ordered[i];
                    if (!head.IsHoldStart) continue;

                    // 이미 LN 채널 / #LNOBJ 로 짝이 맞은 노트는 그대로 둔다.
                    if (head.LongNoteEndSamplePosition > head.SamplePosition) continue;

                    if (tailSearchIdx <= i) tailSearchIdx = i + 1;

                    BmsNote tail = null;
                    while (tailSearchIdx < ordered.Count)
                    {
                        var candidate = ordered[tailSearchIdx];
                        if (!candidate.IsHoldEnd || consumedTails.Contains(candidate) || candidate.Tick <= head.Tick)
                        {
                            tailSearchIdx++;
                            continue;
                        }
                        tail = candidate; // ordered 정렬이므로 첫 후보가 가장 가까운 Tail
                        tailSearchIdx++;
                        break;
                    }

                    if (tail == null)
                    {
                        head.IsLongNote = false;
                        orphanHeads++;
                        continue;
                    }

                    consumedTails.Add(tail);
                    toRemove.Add(tail);

                    head.IsLongNote = true;
                    head.LongNoteEndTick = tail.Tick;
                    head.LongNoteEndTimeSeconds = tail.TimeSeconds;
                    head.LongNoteEndSamplePosition = tail.SamplePosition;
                    paired++;
                }

                // 짝 없는 Tail은 남겨두면 단타로 스폰되므로 제거한다.
                foreach (var note in ordered)
                {
                    if (note.IsHoldEnd && !consumedTails.Contains(note))
                    {
                        toRemove.Add(note);
                        orphanTails++;
                    }
                }
            }

            if (toRemove.Count > 0)
            {
                chart.Notes.RemoveAll(n => toRemove.Contains(n));
            }

            chart.HoldPairedCount = paired;
            chart.HoldOrphanHeadCount = orphanHeads;
            chart.HoldOrphanTailCount = orphanTails;
        }

        /// <summary>
        /// 짝을 못 찾아 종단 정보가 없는 롱노트(LN 채널 홀수 개 등)를 단타로 강등시킵니다.
        /// (EndSample이 0으로 남아 StartSample보다 앞서는 노트가 주입되는 것을 방지)
        /// </summary>
        private void SanitizeUnpairedLongNotes(BmsChart chart)
        {
            foreach (var note in chart.Notes)
            {
                if (!note.IsLongNote) continue;
                if (note.LongNoteEndSamplePosition > note.SamplePosition) continue;

                note.IsLongNote = false;
                note.LongNoteEndTick = 0;
                note.LongNoteEndTimeSeconds = 0;
                note.LongNoteEndSamplePosition = 0;
            }
        }

        /// <summary>
        /// #RANDOM / #SETRANDOM / #IF / #ELSEIF / #ELSE / #ENDIF 처리. 무작위 값은 항상 1로 보고(#SETRANDOM이면 그 값),
        /// 조건이 맞는 분기의 줄만 남깁니다. 중첩 분기도 부모 분기가 활성일 때만 활성입니다.
        /// </summary>
        internal sealed class BranchState
        {
            private sealed class Frame
            {
                public bool ParentActive;
                public bool Active;
                public bool Taken;
            }

            private readonly Stack<Frame> _frames = new Stack<Frame>();
            private readonly Stack<int> _randomValues = new Stack<int>();

            public bool SawRandom { get; private set; }

            public bool IsActive => _frames.Count == 0 || _frames.Peek().Active;

            private int CurrentRandom => _randomValues.Count > 0 ? _randomValues.Peek() : 1;

            public bool TryHandleControlLine(string line)
            {
                // 채널 줄(#00111:...)과 대부분의 헤더는 첫 글자로 바로 거른다(줄마다 문자열 할당 방지).
                if (line.Length < 3) return false;
                char c = char.ToUpperInvariant(line[1]);
                if (c != 'R' && c != 'S' && c != 'I' && c != 'E') return false;

                string keyword = ReadKeyword(line, out string arg);
                switch (keyword)
                {
                    case "#RANDOM":
                    case "#SETRANDOM":
                        if (IsActive)
                        {
                            int value = 1;
                            if (keyword == "#SETRANDOM" && int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fixedValue)) value = fixedValue;
                            if (keyword == "#RANDOM") SawRandom = true;
                            _randomValues.Push(value);
                        }
                        return true;
                    case "#ENDRANDOM":
                        if (IsActive && _randomValues.Count > 0) _randomValues.Pop();
                        return true;
                    case "#IF":
                    {
                        bool parentActive = IsActive;
                        bool match = parentActive && ParseIntOr(arg, int.MinValue) == CurrentRandom;
                        _frames.Push(new Frame { ParentActive = parentActive, Active = match, Taken = match });
                        return true;
                    }
                    case "#ELSEIF":
                    {
                        if (_frames.Count == 0) return true;
                        var frame = _frames.Peek();
                        bool match = frame.ParentActive && !frame.Taken && ParseIntOr(arg, int.MinValue) == CurrentRandom;
                        frame.Active = match;
                        frame.Taken |= match;
                        return true;
                    }
                    case "#ELSE":
                    {
                        if (_frames.Count == 0) return true;
                        var frame = _frames.Peek();
                        frame.Active = frame.ParentActive && !frame.Taken;
                        frame.Taken = true;
                        return true;
                    }
                    case "#ENDIF":
                    case "#END":
                        if (_frames.Count > 0) _frames.Pop();
                        return true;
                    default:
                        return false;
                }
            }

            private static int ParseIntOr(string text, int fallback)
            {
                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
            }

            /// <summary>줄의 첫 단어(대문자)와 나머지를 돌려줍니다. 채널 줄(#00111:...)은 콜론 앞까지가 단어라 제어문과 겹치지 않습니다.</summary>
            private static string ReadKeyword(string line, out string arg)
            {
                int end = 0;
                while (end < line.Length && !char.IsWhiteSpace(line[end])) end++;
                arg = end < line.Length ? line.Substring(end).Trim() : string.Empty;
                return line.Substring(0, end).ToUpperInvariant();
            }
        }

        private class RawMeasureChannel
        {
            public int Measure { get; set; }
            public string Channel { get; set; } = string.Empty;
            public string Data { get; set; } = string.Empty;
        }
    }
}
