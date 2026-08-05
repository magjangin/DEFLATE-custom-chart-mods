using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DEFLATE_custom_chart.Core.Bms
{
    /// <summary>
    /// BMS (.bms, .bme, .bml) 차트 파서 뼈대 클래스 (개선판)
    /// </summary>
    public class BmsParser
    {
        // 1마디 당 표준 틱 수 (예: 4박자 x 960 = 3840 틱)
        public const int TicksPerMeasure = 3840;

        /// <summary>
        /// 파일 경로로부터 BMS 데이터를 파싱합니다.
        /// </summary>
        public BmsChart ParseFile(string filePath, int targetSampleRate = 44100)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"BMS 파일을 찾을 수 없습니다: {filePath}");
            }

            string content = File.ReadAllText(filePath);
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
                    line = line.Trim();
                    if (!line.StartsWith("#WAV", StringComparison.OrdinalIgnoreCase)) continue;

                    // 예: #WAV01 sample.wav -> key: 01 (len 2)
                    // 예: #WAV1A2 sample.wav -> key: 1A2 (len 3)
                    int spaceIdx = line.IndexOfAny(new[] { ' ', '\t' });
                    string tagWithKey = spaceIdx > 0 ? line.Substring(0, spaceIdx) : line;

                    if (tagWithKey.Length > 4)
                    {
                        string key = tagWithKey.Substring(4);
                        if (key.Length == 3)
                        {
                            return 3; // 확장 3글자 ID 감지
                        }
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
            var chart = new BmsChart();
            int noteWidth = DetectNoteValueWidth(bmsContent);
            chart.Header.NoteValueWidth = noteWidth;

            var rawChannelData = new List<RawMeasureChannel>();
            var measureLengths = new Dictionary<int, float>();

            // 1차 스캔: 헤더 및 마디/채널 데이터 수집
            using (var reader = new StringReader(bmsContent))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || !line.StartsWith("#")) continue;

                    ParseLine(line, chart, rawChannelData, measureLengths, noteWidth);
                }
            }

            // 마디별 시작 틱 위치 계산 (마디 배율 #XXX02 적용)
            var measureStartTicks = CalculateMeasureStartTicks(measureLengths, rawChannelData);

            // BPM 변경 이벤트 목록 수집 및 틱 순서 정렬
            BuildBpmTimeline(chart, rawChannelData, measureStartTicks, noteWidth);

            // 노트 생성 및 틱/시간(Seconds)/샘플위치 계산
            GenerateNotes(chart, rawChannelData, measureStartTicks, noteWidth, targetSampleRate);

            // 롱노트(LN) Head-Tail 페어링 처리
            PairLongNotes(chart, targetSampleRate);

            // 최종 틱 순서 정렬
            chart.Notes = chart.Notes.OrderBy(n => n.Tick).ThenBy(n => n.Channel).ToList();

            return chart;
        }

        private void ParseLine(
            string line,
            BmsChart chart,
            List<RawMeasureChannel> rawChannelData,
            Dictionary<int, float> measureLengths,
            int noteWidth)
        {
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
            else if (line.StartsWith("#BPM ", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(ExtractHeaderValue(line, "#BPM"), NumberStyles.Any, CultureInfo.InvariantCulture, out float bpm))
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
            else if (line.StartsWith("#WAV", StringComparison.OrdinalIgnoreCase) && line.Length >= (4 + noteWidth))
            {
                string wavKey = line.Substring(4, noteWidth).ToUpperInvariant();
                string wavFile = line.Substring(4 + noteWidth).Trim();
                chart.Header.WavTable[wavKey] = wavFile;
            }
            else if (line.StartsWith("#BPM", StringComparison.OrdinalIgnoreCase) && line.Length >= (4 + noteWidth) && line[4] != ' ')
            {
                string bpmKey = line.Substring(4, noteWidth).ToUpperInvariant();
                string bpmStr = line.Substring(4 + noteWidth).Trim();
                if (float.TryParse(bpmStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float exBpm))
                {
                    chart.Header.BpmTable[bpmKey] = exBpm;
                }
            }
            // #XXXYY:ZZZZ 채널 데이터 처리 (예: #00111:01020304 -> line[0]='#', line[1..3]=Measure, line[4..5]=Channel, line[6]=':')
            else if (line.Length >= 7 && line[0] == '#' && line[6] == ':')
            {
                if (int.TryParse(line.Substring(1, 3), out int measure))
                {
                    string channel = line.Substring(4, 2);
                    string data = line.Substring(7).Trim();

                    // #XXX02: 마디 길이 변형 (배율 float 지정)
                    if (channel == "02")
                    {
                        if (float.TryParse(data, NumberStyles.Any, CultureInfo.InvariantCulture, out float lenMult))
                        {
                            measureLengths[measure] = lenMult;
                        }
                    }
                    else
                    {
                        rawChannelData.Add(new RawMeasureChannel
                        {
                            Measure = measure,
                            Channel = channel,
                            Data = data
                        });
                    }
                }
            }
        }

        private string ExtractHeaderValue(string line, string prefix)
        {
            if (line.Length <= prefix.Length) return string.Empty;
            return line.Substring(prefix.Length).Trim();
        }

        /// <summary>
        /// 변박자(#XXX02)를 고려하여 각 마디의 시작 Tick 위치를 구합니다.
        /// </summary>
        private Dictionary<int, double> CalculateMeasureStartTicks(Dictionary<int, float> measureLengths, List<RawMeasureChannel> rawChannels)
        {
            int maxMeasure = rawChannels.Count > 0 ? rawChannels.Max(r => r.Measure) : 0;
            if (measureLengths.Count > 0)
            {
                maxMeasure = Math.Max(maxMeasure, measureLengths.Keys.Max());
            }

            var startTicks = new Dictionary<int, double>();
            double currentTick = 0.0;

            for (int m = 0; m <= maxMeasure + 1; m++)
            {
                startTicks[m] = currentTick;
                float multiplier = measureLengths.ContainsKey(m) ? measureLengths[m] : 1.0f;
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

            // 0번 Tick에 초기 BPM 설정
            chart.BpmEvents.Add(new BmsBpmEvent
            {
                Tick = 0,
                Bpm = chart.Header.InitialBpm,
                TimeSeconds = 0.0
            });

            foreach (var raw in rawChannels)
            {
                if (raw.Channel != "03" && raw.Channel != "08") continue;
                if (string.IsNullOrEmpty(raw.Data) || raw.Data.Length % noteWidth != 0) continue;

                int objectCount = raw.Data.Length / noteWidth;
                double mStartTick = measureStartTicks.ContainsKey(raw.Measure) ? measureStartTicks[raw.Measure] : raw.Measure * TicksPerMeasure;
                double mLengthTicks = (measureStartTicks.ContainsKey(raw.Measure + 1) ? measureStartTicks[raw.Measure + 1] : (raw.Measure + 1) * TicksPerMeasure) - mStartTick;

                for (int i = 0; i < objectCount; i++)
                {
                    string val = raw.Data.Substring(i * noteWidth, noteWidth);
                    if (val == new string('0', noteWidth)) continue;

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
                        if (chart.Header.BpmTable.ContainsKey(key))
                        {
                            newBpm = chart.Header.BpmTable[key];
                        }
                    }

                    if (newBpm > 0)
                    {
                        chart.BpmEvents.Add(new BmsBpmEvent
                        {
                            Tick = eventTick,
                            Bpm = newBpm,
                            TimeSeconds = 0.0 // 아래 누적 연산에서 채움
                        });
                    }
                }
            }

            // Tick 순서대로 정렬 후 시간(TimeSeconds) 누적 계산
            chart.BpmEvents = chart.BpmEvents.OrderBy(e => e.Tick).ToList();

            double prevTick = 0.0;
            double prevTime = 0.0;
            float currentBpm = chart.Header.InitialBpm;

            foreach (var bpmEv in chart.BpmEvents)
            {
                double deltaTick = bpmEv.Tick - prevTick;
                // 공식: time = tick * 240 / (bpm * TicksPerMeasure)
                double deltaTime = deltaTick * 240.0 / (currentBpm * TicksPerMeasure);
                prevTime += deltaTime;
                bpmEv.TimeSeconds = prevTime;

                prevTick = bpmEv.Tick;
                currentBpm = bpmEv.Bpm;
            }
        }

        /// <summary>
        /// 주어진 Tick 위치에 대한 정확한 시간(초)을 타임라인을 기초하여 구합니다.
        /// 공식: time = prevTime + (tick - prevTick) * 240 / (bpm * TicksPerMeasure)
        /// </summary>
        private double CalculateTimeAtTick(double tick, List<BmsBpmEvent> bpmEvents)
        {
            if (bpmEvents == null || bpmEvents.Count == 0) return 0.0;

            BmsBpmEvent currentEvent = bpmEvents[0];

            for (int i = 0; i < bpmEvents.Count; i++)
            {
                if (bpmEvents[i].Tick <= tick)
                {
                    currentEvent = bpmEvents[i];
                }
                else
                {
                    break;
                }
            }

            double deltaTick = tick - currentEvent.Tick;
            double deltaTime = deltaTick * 240.0 / (currentEvent.Bpm * TicksPerMeasure);
            return currentEvent.TimeSeconds + deltaTime;
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
                // 노트 채널 여부 확인 (11~29: 일반/키버튼, 51~69: 롱노트)
                if (raw.Channel == "02" || raw.Channel == "03" || raw.Channel == "08") continue;
                if (string.IsNullOrEmpty(raw.Data) || raw.Data.Length % noteWidth != 0) continue;

                int objectCount = raw.Data.Length / noteWidth;
                double mStartTick = measureStartTicks.ContainsKey(raw.Measure) ? measureStartTicks[raw.Measure] : raw.Measure * TicksPerMeasure;
                double mLengthTicks = (measureStartTicks.ContainsKey(raw.Measure + 1) ? measureStartTicks[raw.Measure + 1] : (raw.Measure + 1) * TicksPerMeasure) - mStartTick;

                bool isLnChannel = false;
                if (int.TryParse(raw.Channel, out int chNum) && chNum >= 51 && chNum <= 69)
                {
                    isLnChannel = true;
                }

                for (int i = 0; i < objectCount; i++)
                {
                    string val = raw.Data.Substring(i * noteWidth, noteWidth).ToUpperInvariant();
                    if (val == new string('0', noteWidth)) continue;

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
                        IsLongNote = isLnChannel // 51~69 채널은 기본 LN 후보
                    });
                }
            }
        }

        /// <summary>
        /// 롱노트(LN 채널 51~69 및 #LNOBJ)의 Head-Tail 짝을 맞춰 롱노트 종단 정보를 갱신합니다.
        /// </summary>
        private void PairLongNotes(BmsChart chart, int sampleRate)
        {
            string lnObjKey = chart.Header.LnObj;

            // 1. #LNOBJ 처리: 지정된 LNOBJ 키의 노트를 바로 전 노트의 Tail로 병합
            if (!string.IsNullOrEmpty(lnObjKey))
            {
                var notesGroupedByChannel = chart.Notes.GroupBy(n => n.Channel);
                foreach (var group in notesGroupedByChannel)
                {
                    var orderedNotes = group.OrderBy(n => n.Tick).ToList();
                    for (int i = 0; i < orderedNotes.Count; i++)
                    {
                        if (orderedNotes[i].NoteValue == lnObjKey && i > 0)
                        {
                            var head = orderedNotes[i - 1];
                            var tail = orderedNotes[i];

                            head.IsLongNote = true;
                            head.LongNoteEndTick = tail.Tick;
                            head.LongNoteEndTimeSeconds = tail.TimeSeconds;
                            head.LongNoteEndSamplePosition = tail.SamplePosition;

                            // Tail 전용 LNOBJ 개체는 삭제 대상 처리
                            chart.Notes.Remove(tail);
                        }
                    }
                }
            }

            // 2. 51~69 LN 채널 짝맞추기 처리 (Head -> Tail)
            var lnNotesGrouped = chart.Notes.Where(n => n.IsLongNote && string.IsNullOrEmpty(n.LongNoteEndTick > 0 ? "paired" : null))
                                            .GroupBy(n => n.Channel);

            foreach (var group in lnNotesGrouped)
            {
                var ordered = group.OrderBy(n => n.Tick).ToList();
                for (int i = 0; i < ordered.Count - 1; i += 2)
                {
                    var head = ordered[i];
                    var tail = ordered[i + 1];

                    head.IsLongNote = true;
                    head.LongNoteEndTick = tail.Tick;
                    head.LongNoteEndTimeSeconds = tail.TimeSeconds;
                    head.LongNoteEndSamplePosition = tail.SamplePosition;

                    // Tail 노트는 Head에 통합되었으므로 리스트에서 제거
                    chart.Notes.Remove(tail);
                }
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
