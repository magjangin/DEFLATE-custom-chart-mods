using System.Collections.Generic;

namespace DEFLATE_custom_chart.Core.Bms
{
    /// <summary>
    /// BMS 차트의 메타데이터 헤더 정보
    /// </summary>
    public class BmsHeader
    {
        public string Title { get; set; } = string.Empty;
        public string SubTitle { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public float InitialBpm { get; set; } = 130.0f;
        public int PlayLevel { get; set; } = 1;
        public int Player { get; set; } = 1;
        public float Total { get; set; } = 100.0f;

        /// <summary>
        /// WAV 정의 (#WAVxx)
        /// </summary>
        public Dictionary<string, string> WavTable { get; } = new Dictionary<string, string>();

        /// <summary>
        /// 확장 BPM 정의 (#BPMxx)
        /// </summary>
        public Dictionary<string, float> BpmTable { get; } = new Dictionary<string, float>();
    }

    /// <summary>
    /// BMS 개별 노트 이벤트
    /// </summary>
    public class BmsNote
    {
        public int Measure { get; set; }
        public double Tick { get; set; }
        public double TimeSeconds { get; set; }
        public int SamplePosition { get; set; }

        public string Channel { get; set; } = string.Empty;
        public string NoteValue { get; set; } = string.Empty;

        public bool IsLongNote { get; set; }
        public double LongNoteEndTick { get; set; }
        public double LongNoteEndTimeSeconds { get; set; }
        public int LongNoteEndSamplePosition { get; set; }
    }

    /// <summary>
    /// 파싱이 완료된 BMS 차트 데이터
    /// </summary>
    public class BmsChart
    {
        public BmsHeader Header { get; set; } = new BmsHeader();
        public List<BmsNote> Notes { get; set; } = new List<BmsNote>();
    }
}
