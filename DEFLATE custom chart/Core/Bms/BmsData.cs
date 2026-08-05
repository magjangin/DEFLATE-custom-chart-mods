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
        /// 노트 ID 글자 수 (기본 2: 00~ZZ, 확장 3: 000~ZZZ)
        /// </summary>
        public int NoteValueWidth { get; set; } = 2;

        /// <summary>
        /// 롱노트 타입 (#LNTYPE 1 등)
        /// </summary>
        public int LnType { get; set; } = 1;

        /// <summary>
        /// 롱노트 종단 오브젝트 ID (#LNOBJ xx)
        /// </summary>
        public string LnObj { get; set; } = string.Empty;

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

        /// <summary>이 노트가 참조하는 #WAV 키음 파일명 (홀드 시작/끝 판정에 사용).</summary>
        public string KeysoundFile { get; set; } = string.Empty;

        /// <summary>키음 이름이 "홀드 시작"류인 노트 (홀드 Head 후보).</summary>
        public bool IsHoldStart { get; set; }

        /// <summary>키음 이름이 "홀드 끝"류인 노트 (홀드 Tail 후보). 매칭 후 차트에서 제거됩니다.</summary>
        public bool IsHoldEnd { get; set; }
    }

    /// <summary>
    /// 변속(BPM 변경) 이벤트 정보
    /// </summary>
    public class BmsBpmEvent
    {
        public double Tick { get; set; }
        public float Bpm { get; set; }
        public double TimeSeconds { get; set; }
    }

    /// <summary>
    /// 파싱이 완료된 BMS 차트 데이터
    /// </summary>
    public class BmsChart
    {
        public BmsHeader Header { get; set; } = new BmsHeader();
        public List<BmsBpmEvent> BpmEvents { get; set; } = new List<BmsBpmEvent>();
        public List<BmsNote> Notes { get; set; } = new List<BmsNote>();

        /// <summary>키음 이름 기반으로 짝이 맞아 홀드가 된 노트 수.</summary>
        public int HoldPairedCount { get; set; }

        /// <summary>"홀드 시작"인데 짝이 없어 단타로 강등된 노트 수.</summary>
        public int HoldOrphanHeadCount { get; set; }

        /// <summary>"홀드 끝"인데 짝이 없어 제거된 노트 수.</summary>
        public int HoldOrphanTailCount { get; set; }
    }
}
