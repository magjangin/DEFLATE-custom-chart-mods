using System.Linq;
using DEFLATE_custom_chart.Core.Bms;
using Xunit;

namespace DEFLATE_custom_chart.Tests
{
    /// <summary>
    /// BMS 파서 회귀 테스트. 시간 계산은 AGENTS.md §3-④ 공식(time = prevTime + Δtick × 240 / (bpm × 3840))을 따른다.
    /// 120 BPM이면 한 마디(3840틱) = 2초, 60 BPM이면 4초.
    /// </summary>
    public class BmsParserTests
    {
        private const double Eps = 1e-6;

        // 채널 줄은 #MMMCC:데이터 — 앞 3자리가 마디, 뒤 2자리가 채널 (#00116 = 1마디 16채널).
        private static BmsChart Parse(params string[] lines) => new BmsParser().Parse(string.Join("\n", lines));

        [Fact]
        public void 채널은_열_키음은_하이햇_킥스네어를_정하고_노트는_한_레인에만_들어간다()
        {
            var chart = Parse(
                "#BPM 120",
                "#WAV01 hihat.wav",
                "#WAV02 kick.wav",
                "#00016:01000000",
                "#00011:0002",
                "#00014:01");

            Assert.Equal(3, chart.Notes.Count);

            var hihat = chart.Notes.Single(n => n.Channel == "16");
            Assert.Equal(BmsLaneMapper.HiHat1, hihat.ResolvedLane);
            Assert.Equal(0.0, hihat.TimeSeconds, 6);

            var kick = chart.Notes.Single(n => n.Channel == "11");
            Assert.Equal(BmsLaneMapper.KickLeft, kick.ResolvedLane); // 2열 킥·스네어 = kick_left (실측 배치)
            Assert.Equal(1.0, kick.TimeSeconds, 6);                    // 0마디 중간 = 1초

            var drop = chart.Notes.Single(n => n.Channel == "14");
            Assert.Equal(BmsLaneMapper.Drop, drop.ResolvedLane);

            Assert.Single(chart.GetNotesForLane(BmsLaneMapper.HiHat1));
            Assert.Single(chart.GetNotesForLane(BmsLaneMapper.KickLeft));
            Assert.Empty(chart.GetNotesForLane(BmsLaneMapper.HiHat2));
        }

        [Fact]
        public void 샘플_위치는_대상_샘플레이트로_계산된다()
        {
            var chart = new BmsParser().Parse("#BPM 120\n#00016:00000001", 48000);
            var note = Assert.Single(chart.Notes);
            Assert.Equal(1.5, note.TimeSeconds, 6);
            Assert.Equal(72000, note.SamplePosition);
        }

        [Fact]
        public void 채널03_16진_BPM_변경을_반영한다()
        {
            // 60 BPM으로 반 마디(2초) 뒤 0x78 = 120 BPM → 다음 반 마디는 1초
            var chart = Parse("#BPM 60", "#00003:0078", "#00116:01");
            Assert.Equal(3.0, chart.Notes.Single(n => n.Measure == 1).TimeSeconds, 6);
        }

        [Fact]
        public void 채널08_확장_BPM_테이블을_반영한다()
        {
            var chart = Parse("#BPM 60", "#BPM01 120", "#00008:0001", "#00116:01");
            Assert.Equal(3.0, chart.Notes.Single().TimeSeconds, 6);
        }

        [Fact]
        public void 확장_3자리_ID_파일에서도_채널03은_2자리_16진수로_읽는다()
        {
            var chart = Parse(
                "#BPM 60",
                "#WAV001 a.wav",
                "#00003:0078",
                "#00116:001");

            Assert.Equal(3, chart.Header.NoteValueWidth);
            Assert.DoesNotContain(chart.Warnings, w => w.Contains("03"));
            Assert.Equal(3.0, chart.Notes.Single().TimeSeconds, 6);
        }

        [Fact]
        public void 마디_길이_배율을_반영한다()
        {
            var chart = Parse("#BPM 120", "#00002:0.5", "#00116:01");
            Assert.Equal(1.0, chart.Notes.Single().TimeSeconds, 6); // 0마디가 반 마디(1초)
        }

        [Fact]
        public void RANDOM은_1번_분기만_읽는다()
        {
            var chart = Parse(
                "#BPM 120",
                "#RANDOM 2",
                "#IF 1",
                "#00116:01",
                "#ENDIF",
                "#IF 2",
                "#00111:01",
                "#ENDIF",
                "#ENDRANDOM");

            Assert.Equal("16", chart.Notes.Single().Channel);
            Assert.Contains(chart.Warnings, w => w.Contains("#RANDOM"));
        }

        [Fact]
        public void SETRANDOM은_지정한_분기를_읽고_경고하지_않는다()
        {
            var chart = Parse(
                "#BPM 120",
                "#SETRANDOM 2",
                "#IF 1",
                "#00116:01",
                "#ELSE",
                "#00111:01",
                "#ENDIF");

            Assert.Equal("11", chart.Notes.Single().Channel);
            Assert.DoesNotContain(chart.Warnings, w => w.Contains("#RANDOM"));
        }

        [Fact]
        public void 키음_이름_홀드_시작_끝으로_홀드를_만든다()
        {
            var chart = Parse(
                "#BPM 120",
                "#WAV01 홀드 시작.wav",
                "#WAV02 홀드 끝.wav",
                "#00016:0102",
                "#00011:02"); // 짝 없는 끝(고아) → 제거

            var head = Assert.Single(chart.Notes);
            Assert.True(head.IsLongNote);
            Assert.Equal(1.0, head.LongNoteEndTimeSeconds, 6);
            Assert.Equal(1, chart.HoldPairedCount);
            Assert.Equal(1, chart.HoldOrphanTailCount);
        }

        [Fact]
        public void LNOBJ로_홀드를_만든다()
        {
            var chart = Parse("#BPM 120", "#LNOBJ ZZ", "#00016:01ZZ");
            var head = Assert.Single(chart.Notes);
            Assert.True(head.IsLongNote);
            Assert.Equal(1.0, head.LongNoteEndTimeSeconds, 6);
        }

        [Fact]
        public void 데이터_길이가_ID_폭의_배수가_아니면_그_줄을_건너뛰고_경고한다()
        {
            var chart = Parse("#BPM 120", "#00016:010");
            Assert.Empty(chart.Notes);
            Assert.Contains(chart.Warnings, w => w.Contains("#00016"));
        }

        [Fact]
        public void 지원하지_않는_STOP과_LN채널은_경고한다()
        {
            var chart = Parse("#BPM 120", "#STOP01 192", "#00109:01", "#00151:01");
            Assert.Contains(chart.Warnings, w => w.Contains("STOP"));
            Assert.Contains(chart.Warnings, w => w.Contains("LN"));
        }

        [Fact]
        public void 라인바는_마디선과_4분음표_박자선이고_마지막에_닫는_마디선이_붙는다()
        {
            var chart = Parse("#BPM 120", "#00116:01");
            // 마디 0·1 각각 마디선 1 + 박자선 3, 닫는 마디선 1 = 9
            Assert.Equal(9, chart.BarLines.Count);
            Assert.Equal(3, chart.BarLines.Count(b => b.IsMeasureStart));
            Assert.Equal(4.0, chart.BarLines.Last().TimeSeconds, 6);
            Assert.True(chart.BarLines.Zip(chart.BarLines.Skip(1)).All(p => p.First.Tick < p.Second.Tick));
        }
    }
}
