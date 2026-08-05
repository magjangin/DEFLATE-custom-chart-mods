using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DEFLATE_custom_chart.Core.Bms
{
    /// <summary>
    /// BMS (.bms, .bme, .bml) 차트 파서 뼈대 클래스
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
        /// BMS 문자열 내용을 파싱하여 BmsChart 객체로 반환합니다.
        /// </summary>
        public BmsChart Parse(string bmsContent, int targetSampleRate = 44100)
        {
            var chart = new BmsChart();
            var rawChannelData = new List<RawMeasureChannel>();

            using (var reader = new StringReader(bmsContent))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || !line.StartsWith("#")) continue;

                    ParseLine(line, chart, rawChannelData);
                }
            }

            // 마디 및 노트 틱/시간 계산
            CalculateNoteTimings(chart, rawChannelData, targetSampleRate);

            return chart;
        }

        private void ParseLine(string line, BmsChart chart, List<RawMeasureChannel> rawChannelData)
        {
            // #HEADER 명령어 처리
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
            else if (line.StartsWith("#WAV", StringComparison.OrdinalIgnoreCase) && line.Length >= 7)
            {
                string wavKey = line.Substring(4, 2);
                string wavFile = line.Substring(7).Trim();
                chart.Header.WavTable[wavKey] = wavFile;
            }
            else if (line.StartsWith("#BPM", StringComparison.OrdinalIgnoreCase) && line.Length >= 7 && line[4] != ' ')
            {
                string bpmKey = line.Substring(4, 2);
                string bpmStr = line.Substring(7).Trim();
                if (float.TryParse(bpmStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float exBpm))
                {
                    chart.Header.BpmTable[bpmKey] = exBpm;
                }
            }
            // #XXXYY:ZZZZ 채널 데이터 처리 (예: #00111:01020304)
            else if (line.Length >= 7 && line[0] == '#' && line[4] == ':')
            {
                if (int.TryParse(line.Substring(1, 3), out int measure))
                {
                    string channel = line.Substring(4, 2);
                    string data = line.Substring(7).Trim();

                    rawChannelData.Add(new RawMeasureChannel
                    {
                        Measure = measure,
                        Channel = channel,
                        Data = data
                    });
                }
            }
        }

        private string ExtractHeaderValue(string line, string prefix)
        {
            if (line.Length <= prefix.Length) return string.Empty;
            return line.Substring(prefix.Length).Trim();
        }

        /// <summary>
        /// 파싱된 원시 마디/채널 데이터를 기반으로 각 노트의 Tick 및 Time(Seconds), SamplePosition을 계산합니다.
        /// 시간 계산 공식: time = tick * 240 / bpm (1 마디 틱 기준 단위 환산)
        /// </summary>
        private void CalculateNoteTimings(BmsChart chart, List<RawMeasureChannel> rawChannels, int sampleRate)
        {
            double currentBpm = chart.Header.InitialBpm;

            // TODO: 마디별 BPM 변경 및 박자 수(Measure Length) 변경 대응 확대
            foreach (var raw in rawChannels)
            {
                if (string.IsNullOrEmpty(raw.Data) || raw.Data.Length % 2 != 0) continue;

                int objectCount = raw.Data.Length / 2;
                double measureTickStart = raw.Measure * TicksPerMeasure;

                for (int i = 0; i < objectCount; i++)
                {
                    string val = raw.Data.Substring(i * 2, 2);
                    if (val == "00") continue; // 빈 노주는 패스

                    double noteTick = measureTickStart + (double)i / objectCount * TicksPerMeasure;
                    
                    // BMS 시간 계산 공식: time = tick * (240 / (bpm * TicksPerMeasure))
                    // 즉, time = tick * 240 / bpm (틱 단위 정규화 기준)
                    double timeSeconds = (noteTick / TicksPerMeasure) * (240.0 / currentBpm);
                    int samplePos = (int)(timeSeconds * sampleRate);

                    chart.Notes.Add(new BmsNote
                    {
                        Measure = raw.Measure,
                        Tick = noteTick,
                        TimeSeconds = timeSeconds,
                        SamplePosition = samplePos,
                        Channel = raw.Channel,
                        NoteValue = val
                    });
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
