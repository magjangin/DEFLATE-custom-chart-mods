using System.Text;
using DEFLATE_custom_chart.Core;
using DEFLATE_custom_chart.Core.Bms;
using Xunit;

namespace DEFLATE_custom_chart.Tests
{
    public class BmsLaneMapperTests
    {
        [Theory]
        [InlineData("16", "hihat.wav", BmsLaneMapper.HiHat1)]
        [InlineData("13", "hihat_4.wav", BmsLaneMapper.HiHat4)]
        [InlineData("16", "snare.wav", BmsLaneMapper.SnareLeft)]
        [InlineData("11", "Kick.ogg", BmsLaneMapper.KickLeft)]
        [InlineData("12", "kick_right.wav", BmsLaneMapper.KickRight)]
        [InlineData("13", "snare_right.wav", BmsLaneMapper.SnareRight)]
        [InlineData("12", "킥 03.wav", BmsLaneMapper.KickRight)]
        [InlineData("16", "스네어.ogg", BmsLaneMapper.SnareLeft)]
        [InlineData("11", "하이햇 홀드 시작.wav", BmsLaneMapper.HiHat2)]
        [InlineData("14", "anything.wav", BmsLaneMapper.Drop)]
        [InlineData("15", "hihat.wav", null)]
        [InlineData("01", "bgm.wav", null)]
        public void 채널과_키음으로_게임_레인을_정한다(string channel, string keysound, string expected)
        {
            Assert.Equal(expected, BmsLaneMapper.ResolveGameLane(channel, keysound));
        }

        [Fact]
        public void 키음_이름이_가리키는_레인과_열이_다르면_진단값을_돌려준다()
        {
            // kick_left.wav를 1열(16)에 찍으면 1열 킥·스네어 레인(snare_left)에 배치된다.
            Assert.Equal(BmsLaneMapper.SnareLeft, BmsLaneMapper.ResolveGameLane("16", "kick_left.wav"));
            Assert.Equal("kick_left", BmsLaneMapper.KeysoundLaneHintMismatch("kick_left.wav", BmsLaneMapper.SnareLeft));
            Assert.Null(BmsLaneMapper.KeysoundLaneHintMismatch("kick_left.wav", BmsLaneMapper.KickLeft));
        }

        [Theory]
        [InlineData("hihat_2", null, BmsLaneMapper.HiHat2)]
        [InlineData("", "Kick_Right", BmsLaneMapper.KickRight)]
        [InlineData("", "Snare_Left", BmsLaneMapper.SnareLeft)]
        [InlineData("drop", "HiHat_1", BmsLaneMapper.Drop)] // trackID가 열거형 이름보다 우선
        [InlineData("beat", "Shake", null)]
        public void trackID나_열거형_이름으로_레인을_판정한다(string trackId, string laneTypeName, string expected)
        {
            // 게임 열거형 타입에 의존하지 않고 이름만 본다(AGENTS.md §3-⑤ ⑥). object 오버로드는 ToString()을 쓴다.
            Assert.Equal(expected, BmsLaneMapper.ResolveLaneId(trackId, (object)laneTypeName));
        }
    }

    public class TextDecoderTests
    {
        static TextDecoderTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void UTF8과_BOM을_읽는다()
        {
            byte[] utf8 = Encoding.UTF8.GetBytes("#TITLE 홀드 테스트");
            Assert.Equal("#TITLE 홀드 테스트", TextDecoder.Decode(utf8, TextDecoder.Preference.Chart));

            byte[] bom = Encoding.UTF8.GetPreamble().Concat(utf8).ToArray();
            Assert.Equal("#TITLE 홀드 테스트", TextDecoder.Decode(bom, TextDecoder.Preference.Korean));
        }

        [Fact]
        public void ShiftJIS_BMS를_읽는다()
        {
            const string text = "#TITLE 曲名テスト\n#WAV01 ハイハット.wav";
            byte[] sjis = Encoding.GetEncoding(932).GetBytes(text);
            Assert.Equal(text, TextDecoder.Decode(sjis, TextDecoder.Preference.Chart));
            Assert.Equal("shift_jis", TextDecoder.LastEncodingName);
        }

        [Fact]
        public void CP949_info_txt를_읽는다()
        {
            const string text = "title=드림 모드\nartist=화영왕";
            byte[] cp949 = Encoding.GetEncoding(949).GetBytes(text);
            Assert.Equal(text, TextDecoder.Decode(cp949, TextDecoder.Preference.Korean));
        }

        [Fact]
        public void CP949_BMS를_반각_가타카나로_잘못_읽지_않는다()
        {
            // 한글 완성형 바이트(0xB0~0xC8 / 0xA1~0xFE)는 Shift-JIS에서 반각 가타카나로 '오류 없이' 해석된다.
            // 그대로 두면 "홀드 시작.wav" 같은 키음 이름이 깨져 홀드 판정이 조용히 빠진다.
            // 아래 문자열은 모든 바이트가 Shift-JIS로도 유효해(화영왕 → ﾈｭｿｵｿﾕ) 예외 없이 잘못 읽힌다(2026-11-24 확인).
            const string text = "#TITLE 화영왕\n#ARTIST 화영왕\n#WAV01 하이햇.wav\n#WAV02 시작.wav";
            byte[] cp949 = Encoding.GetEncoding(949).GetBytes(text);
            Assert.Equal(text, TextDecoder.Decode(cp949, TextDecoder.Preference.Chart));
        }

        [Fact]
        public void CP949로_저장된_BMS의_키음_홀드가_끝까지_동작한다()
        {
            const string bms = "#BPM 120\n#TITLE 화영왕\n#WAV01 홀드 시작.wav\n#WAV02 홀드 끝.wav\n#00016:0102";
            byte[] cp949 = Encoding.GetEncoding(949).GetBytes(bms);
            var chart = new BmsParser().Parse(TextDecoder.Decode(cp949, TextDecoder.Preference.Chart));
            Assert.True(Assert.Single(chart.Notes).IsLongNote);
        }

        [Fact]
        public void 반각_가타카나가_조금_섞인_일본어_BMS는_ShiftJIS로_유지한다()
        {
            const string text = "#TITLE ﾃｽﾄ 曲名テストです\n#ARTIST 作曲者さん\n#GENRE ジャンル";
            byte[] sjis = Encoding.GetEncoding(932).GetBytes(text);
            Assert.Equal(text, TextDecoder.Decode(sjis, TextDecoder.Preference.Chart));
        }
    }
}
