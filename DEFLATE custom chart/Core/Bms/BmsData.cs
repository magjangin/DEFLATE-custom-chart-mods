using System;
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

        /// <summary>
        /// 확장 STOP 정의 (#STOPxx) - 192분음표 기준 단위 정지 시간
        /// </summary>
        public Dictionary<string, double> StopTable { get; } = new Dictionary<string, double>();
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

        /// <summary>파싱 시점에 미리 확정된 DEFLATE 레인 슬롯 ID (hihat_1~4 / kick·snare / drop).</summary>
        public string ResolvedLane { get; set; } = string.Empty;

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
        public int OrderIndex { get; set; }
        public double Tick { get; set; }
        public float Bpm { get; set; }
        public double TimeSeconds { get; set; }
        /// <summary>정지(STOP) 이벤트와 동일 틱에 발생했을 때, 정지 시간 가산 직전의 시점(초)</summary>
        public double? PreStopTimeSeconds { get; set; }
    }

    /// <summary>
    /// 정지(STOP) 이벤트 정보 (09 채널 및 #STOPxx)
    /// </summary>
    public class BmsStopEvent
    {
        public int OrderIndex { get; set; }
        public double Tick { get; set; }
        /// <summary>192분음표 기준 단위 정지 수치 (192 = 1마디 = 4박)</summary>
        public double StopValue { get; set; }
        /// <summary>실제 계산된 정지 시간(초)</summary>
        public double DurationSeconds { get; set; }
    }

    /// <summary>
    /// 라인바(마디선/박자선) 한 줄. BMS 마디 경계와 4분음표 박자 위치에서 만들어집니다.
    /// </summary>
    public class BmsBarLine
    {
        public double Tick { get; set; }
        public double TimeSeconds { get; set; }
        public int SamplePosition { get; set; }

        /// <summary>마디 시작선이면 true, 마디 안의 박자선이면 false.</summary>
        public bool IsMeasureStart { get; set; }
    }

    /// <summary>
    /// 파싱이 완료된 BMS 차트 데이터
    /// </summary>
    public class BmsChart
    {
        public int SampleRate { get; set; } = 44100;
        public BmsHeader Header { get; set; } = new BmsHeader();
        public List<BmsBpmEvent> BpmEvents { get; set; } = new List<BmsBpmEvent>();
        public List<BmsStopEvent> StopEvents { get; set; } = new List<BmsStopEvent>();
        public List<BmsNote> Notes { get; set; } = new List<BmsNote>();

        /// <summary>인게임 라인바로 주입할 마디선/박자선 (틱 순).</summary>
        public List<BmsBarLine> BarLines { get; set; } = new List<BmsBarLine>();

        /// <summary>키음 이름 기반으로 짝이 맞아 홀드가 된 노트 수.</summary>
        public int HoldPairedCount { get; set; }

        /// <summary>"홀드 시작"인데 짝이 없어 단타로 강등된 노트 수.</summary>
        public int HoldOrphanHeadCount { get; set; }

        /// <summary>"홀드 끝"인데 짝이 없어 제거된 노트 수.</summary>
        public int HoldOrphanTailCount { get; set; }

        /// <summary>파싱 중 건너뛴 줄 등 차트 제작자가 알아야 할 경고 (로그 출력용).</summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>키음 이름이 가리키는 레인(예: hihat_3.wav)과 채널(열)로 배치된 레인이 다른 노트 수. 배치는 항상 채널 기준.</summary>
        public int KeysoundLaneMismatchCount { get; set; }

        /// <summary>게임 레인별 노트 수 요약 ("하이햇 a/b/c/d · 킥·스네어 e/f/g/h(1~4열) · 드롭 i").</summary>
        public string DescribeLaneDistribution()
        {
            string Count(string lane) => GetNotesForLane(lane).Count.ToString();
            return $"하이햇 {Count(BmsLaneMapper.HiHat1)}/{Count(BmsLaneMapper.HiHat2)}/{Count(BmsLaneMapper.HiHat3)}/{Count(BmsLaneMapper.HiHat4)}"
                + $" · 킥·스네어 {Count(BmsLaneMapper.SnareLeft)}/{Count(BmsLaneMapper.KickLeft)}/{Count(BmsLaneMapper.KickRight)}/{Count(BmsLaneMapper.SnareRight)}(1~4열)"
                + $" · 드롭 {Count(BmsLaneMapper.Drop)}";
        }

        private readonly Dictionary<string, List<BmsNote>> _notesByLane =
            new Dictionary<string, List<BmsNote>>(StringComparer.OrdinalIgnoreCase);

        private static readonly BmsNote[] EmptyNotes = Array.Empty<BmsNote>();

        /// <summary>
        /// 인게임 게임 레인별(hihat_1~4, kick/snare, drop) 노트 리스트를 O(1)로 조회합니다.
        /// (9개 레인 + 드롭 초기화 시 전체 노트를 매번 순회하는 O(10N) 병목 제거)
        /// </summary>
        public IReadOnlyList<BmsNote> GetNotesForLane(string laneId)
        {
            if (string.IsNullOrEmpty(laneId)) return EmptyNotes;

            if (_notesByLane.Count == 0 && Notes.Count > 0)
            {
                RebuildLaneIndex();
            }

            return _notesByLane.TryGetValue(laneId, out var laneNotes) ? laneNotes : EmptyNotes;
        }

        /// <summary>노트 목록을 레인 슬롯 기준으로 재인덱싱합니다.</summary>
        public void RebuildLaneIndex()
        {
            _notesByLane.Clear();
            foreach (var note in Notes)
            {
                if (note == null) continue;
                string lane = BmsLaneMapper.ResolveNoteLane(note);
                if (string.IsNullOrEmpty(lane)) continue;

                if (!_notesByLane.TryGetValue(lane, out var list))
                {
                    list = new List<BmsNote>();
                    _notesByLane[lane] = list;
                }
                list.Add(note);
            }
        }
    }
}
