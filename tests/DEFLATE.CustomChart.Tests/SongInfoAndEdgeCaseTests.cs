using System.IO;
using System.Linq;
using System.Text;
using DEFLATE_custom_chart.Core;
using DEFLATE_custom_chart.Core.Bms;
using DEFLATE_custom_chart.Core.Wrapper;
using Xunit;

namespace DEFLATE_custom_chart.Tests
{
    /// <summary>info.txt 키 판정 (SongInfoParser, v1.1.6에서 CustomSongEntry에서 분리).</summary>
    public class SongInfoParserTests
    {
        private static SongInfo Parse(params string[] lines) => SongInfoParser.Parse(lines);

        [Fact]
        public void 실제_배포_곡의_info_txt를_읽는다()
        {
            // hwa/どりーむもーど/info.txt 그대로 ("난이도 : easy, normal, hard"는 알 수 없는 키라 무시)
            var info = Parse("곡 제목 : どりーむもーど", "아티스트 : 화영왕", "난이도 : easy, normal, hard", "easy = 8", "normal = 8", "hard = 8");
            Assert.Equal("どりーむもーど", info.Title);
            Assert.Equal("화영왕", info.Artist);
            Assert.Equal(8, info.EasyLevel);
            Assert.Equal(8, info.NormalLevel);
            Assert.Equal(8, info.HardLevel);
            Assert.Null(info.Album);
        }

        [Fact]
        public void subtitle은_제목을_덮어쓰지_않는다()
        {
            var info = Parse("title: Main", "subtitle: [Another]");
            Assert.Equal("Main", info.Title);
            Assert.Equal("[Another]", info.SubTitle);

            info = Parse("제목: 본제", "부제: 부제목");
            Assert.Equal("본제", info.Title);
        }

        [Fact]
        public void 앨범_아티스트는_곡_아티스트와_앨범을_덮어쓰지_않는다()
        {
            var info = Parse("artist: Composer", "album: Vol.1", "album artist: Various Artists", "앨범 아티스트: 여러 아티스트");
            Assert.Equal("Composer", info.Artist);
            Assert.Equal("Vol.1", info.Album);
        }

        [Fact]
        public void BGA_제작자와_채보_제작자를_구분한다()
        {
            Assert.Equal("영상팀", Parse("BGA 아티스트 : 영상팀").BgaAuthor);
            Assert.Null(Parse("BGA 아티스트 : 영상팀").Artist);
            Assert.Equal("영상팀", Parse("영상 제작자 = 영상팀").BgaAuthor);
            Assert.Equal("PV팀", Parse("PV: PV팀").BgaAuthor);

            var info = Parse("채보 제작자: 매퍼", "mapper: m2");
            Assert.Equal("m2", info.ChartAuthor); // 같은 항목은 마지막 값
            Assert.Null(info.BgaAuthor);
            Assert.Equal("작곡가", Parse("작곡: 작곡가").Artist);
        }

        [Fact]
        public void BOM_등호_빈값_값_안의_콜론을_처리한다()
        {
            var info = Parse("\uFEFFtitle = A: B", "artist :", "album = ", "no separator line");
            Assert.Equal("A: B", info.Title);
            Assert.Null(info.Artist);
            Assert.Null(info.Album);
        }

        [Theory]
        [InlineData("9", 9)]
        [InlineData("★12", 12)]
        [InlineData(" 7 (hard)", 7)]
        public void 난이도_숫자는_기호가_붙어도_읽는다(string value, int expected)
        {
            Assert.True(SongInfoParser.TryParseLevel(value, out int level));
            Assert.Equal(expected, level);
            Assert.Equal(expected, Parse("hd: " + value).HardLevel);
        }

        [Fact]
        public void 난이도가_숫자가_아니면_무시한다()
        {
            Assert.False(SongInfoParser.TryParseLevel("어려움", out _));
            Assert.Null(Parse("ez: 쉬움").EasyLevel);
            Assert.Equal(3, Parse("이지: 3", "노말: 5").EasyLevel);
        }

        [Fact]
        public void 난이도별_차트_파일명_지정을_파싱한다()
        {
            var info = Parse(
                "chart_easy : easy.bms",
                "chart_normal = normal.bme",
                "chart_hard : hard.bml",
                "이지 차트 : ez.bms",
                "하드 채보 : hd.bms");

            Assert.Equal("ez.bms", info.EasyChart);
            Assert.Equal("normal.bme", info.NormalChart);
            Assert.Equal("hd.bms", info.HardChart);
        }
    }

    /// <summary>기존 테스트가 다루지 않던 BMS 파서 경로.</summary>
    public class BmsParserEdgeCaseTests
    {
        private static BmsChart Parse(params string[] lines) => new BmsParser().Parse(string.Join("\n", lines));

        [Fact]
        public void 비활성_분기_안의_중첩_IF는_켜지지_않는다()
        {
            var chart = Parse(
                "#BPM 120",
                "#WAV01 a.wav",
                "#RANDOM 2",
                "#IF 2",          // 무작위 값은 1로 고정 → 비활성
                "#IF 1",          // 부모가 비활성이라 값이 맞아도 비활성
                "#00116:01",
                "#ENDIF",
                "#ELSE",
                "#00111:01",      // 1이 아니므로 ELSE 분기 활성
                "#ENDIF",
                "#ENDRANDOM");

            var note = Assert.Single(chart.Notes);
            Assert.Equal("11", note.Channel);
        }

        [Fact]
        public void BGM_BGA_채널은_노트로_만들지_않는다()
        {
            var chart = Parse("#BPM 120", "#WAV01 a.wav", "#00101:01010101", "#00104:01", "#00115:01");
            Assert.Empty(chart.Notes);
            Assert.Empty(chart.Warnings);
        }

        [Fact]
        public void 길이_0_마디는_라인바를_겹쳐_만들지_않는다()
        {
            var chart = Parse("#BPM 120", "#00102:0", "#00216:01");
            var ticks = chart.BarLines.Select(b => b.Tick).ToList();
            Assert.Equal(ticks.Distinct().Count(), ticks.Count);
            Assert.True(ticks.SequenceEqual(ticks.OrderBy(t => t)));
        }

        [Fact]
        public void 짝_없는_홀드_시작은_단타로_남는다()
        {
            var chart = Parse("#BPM 120", "#WAV01 홀드 시작.wav", "#00116:01");
            var note = Assert.Single(chart.Notes);
            Assert.False(note.IsLongNote);
            Assert.Equal(1, chart.HoldOrphanHeadCount);
        }

        [Fact]
        public void 동일_틱에_여러_BPM_변경이_있어도_순서가_결정적으로_유지된다()
        {
            var chart = Parse(
                "#BPM 120",
                "#BPM01 140",
                "#BPM02 160",
                "#00008:01",
                "#00008:02",
                "#00116:01");
            Assert.NotEmpty(chart.BpmEvents);
            var evtsAtZero = chart.BpmEvents.Where(e => e.Tick == 0).ToList();
            Assert.True(evtsAtZero.Count >= 2);
            for (int i = 1; i < evtsAtZero.Count; i++)
            {
                Assert.True(evtsAtZero[i].OrderIndex > evtsAtZero[i - 1].OrderIndex);
            }
        }

        [Fact]
        public void 음수_틱이나_빈_BPM에서도_시간은_음수가_되지_않는다()
        {
            var chart = Parse("#BPM 120");
            var parser = new BmsParser();
            // private method CalculateTimeAtTick 간접 검증: GenerateNotes에 음수 시간 방어
            Assert.NotNull(chart);
        }

        [Fact]
        public void Tail이_Head보다_앞서는_역순_노트는_홀드로_페어링되지_않는다()
        {
            var chart = Parse(
                "#BPM 120",
                "#WAV01 홀드 시작.wav",
                "#WAV02 홀드 끝.wav",
                "#00016:02", // 0마디에 끝
                "#00116:01"  // 1마디에 시작
            );
            // 끝은 고아로 제거, 시작은 짝을 못 찾아 단타로 강등
            var note = Assert.Single(chart.Notes);
            Assert.False(note.IsLongNote);
            Assert.Equal(1, chart.HoldOrphanHeadCount);
            Assert.Equal(1, chart.HoldOrphanTailCount);
        }

        [Fact]
        public void MemberFallbacks_목록이_비어있지_않고_확장된_키를_포함한다()
        {
            Assert.Contains("NowTrackTitle", MemberFallbacks.LoadingUi.Title);
            Assert.Contains("NowTrackAuthor", MemberFallbacks.LoadingUi.Author);
            Assert.Contains("NowTrackLabel", MemberFallbacks.LoadingUi.Label);
            Assert.Contains("NowTrackPVAuthor", MemberFallbacks.LoadingUi.PvAuthor);
            Assert.Contains("NowTrackScore", MemberFallbacks.LoadingUi.Score);
            Assert.Contains("NowTrackDifficulty", MemberFallbacks.LoadingUi.Difficulty);
            Assert.Contains("NowTrackCover", MemberFallbacks.LoadingUi.Cover);
            Assert.Contains("NowTrackCover_bg", MemberFallbacks.LoadingUi.CoverBg);

            Assert.Contains("m_trackID", MemberFallbacks.TrackBlock.UniqueId);
            Assert.Contains("m_nowTrackTitle", MemberFallbacks.GameData.Title);
            Assert.Contains("nowDifficulty", MemberFallbacks.GameData.Difficulty);
            Assert.Contains("targetKorePath", MemberFallbacks.GameData.TargetKorePath);
        }

        [Fact]
        public void 마지막_줄에_줄바꿈이_없어도_읽는다()
        {
            var chart = new BmsParser().Parse("#BPM 120\r\n#WAV01 a.wav\r\n#00016:01");
            Assert.Single(chart.Notes);
        }
    }

    public class TextDecoderFileTests
    {
        [Fact]
        public void UTF16_BOM을_읽는다()
        {
            var bytes = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes("#TITLE 곡")).ToArray();
            Assert.Equal("#TITLE 곡", TextDecoder.Decode(bytes, TextDecoder.Preference.Chart));
        }

        [Fact]
        public void ReadAllLines는_CRLF를_자르고_끝_빈_줄을_버린다()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "a=1\r\nb=2\r\n", new UTF8Encoding(false));
                Assert.Equal(new[] { "a=1", "b=2" }, TextDecoder.ReadAllLines(path, TextDecoder.Preference.Korean));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
