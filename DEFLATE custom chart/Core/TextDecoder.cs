using System;
using System.IO;
using System.Text;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>
    /// 텍스트 파일(BMS 차트, info.txt, config.txt)을 인코딩 자동 판별로 읽습니다.
    /// <para>
    /// 순서: BOM(UTF-8/UTF-16) → 엄격 UTF-8 → 우선 코드페이지 → 나머지 코드페이지 → Latin-1.
    /// BMS는 일본어권 배포가 많아 Shift-JIS(932)를, info.txt/config.txt는 메모장 ANSI 저장이 많아 CP949를 먼저 봅니다.
    /// 예전에는 <see cref="File.ReadAllText(string)"/>(UTF-8 고정)만 써서 Shift-JIS BMS의 제목·<c>#WAV</c> 키음 파일명이 깨졌고,
    /// 키음 이름으로 정하는 홀드/킥·스네어 판정까지 틀어질 수 있었습니다(AGENTS.md §9 알려진 한계).
    /// </para>
    /// 순수 .NET 코드라 게임 타입을 참조하지 않습니다(업데이트 영향 없음).
    /// </summary>
    public static class TextDecoder
    {
        public enum Preference
        {
            /// <summary>BMS 차트: Shift-JIS → CP949 → GB18030.</summary>
            Chart,
            /// <summary>info.txt / config.txt: CP949 → Shift-JIS → GB18030.</summary>
            Korean,
        }

        private static readonly int[] ChartCodePages = { 932, 949, 54936 };
        private static readonly int[] KoreanCodePages = { 949, 932, 54936 };

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static bool _providerRegistered;

        /// <summary>마지막 호출에서 고른 인코딩 이름 (로그용).</summary>
        public static string LastEncodingName { get; private set; } = "utf-8";

        public static string ReadAllText(string path, Preference preference)
        {
            byte[] bytes = File.ReadAllBytes(path);
            return Decode(bytes, preference);
        }

        public static string[] ReadAllLines(string path, Preference preference)
        {
            string text = ReadAllText(path, preference);
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > 0 && lines[i][lines[i].Length - 1] == '\r')
                    lines[i] = lines[i].Substring(0, lines[i].Length - 1);
            }
            // File.ReadAllLines와 같게: 파일 끝 줄바꿈 뒤의 빈 줄은 버린다.
            if (lines.Length > 0 && lines[lines.Length - 1].Length == 0)
                Array.Resize(ref lines, lines.Length - 1);
            return lines;
        }

        public static string Decode(byte[] bytes, Preference preference)
        {
            if (bytes == null || bytes.Length == 0)
            {
                LastEncodingName = "utf-8";
                return string.Empty;
            }

            // BOM
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                LastEncodingName = "utf-8 (BOM)";
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                LastEncodingName = "utf-16le (BOM)";
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                LastEncodingName = "utf-16be (BOM)";
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            // 엄격 UTF-8 (ASCII 전용 파일도 여기서 끝난다)
            try
            {
                string utf8 = StrictUtf8.GetString(bytes);
                LastEncodingName = "utf-8";
                return utf8;
            }
            catch (DecoderFallbackException)
            {
                // 다음 후보로
            }

            EnsureCodePagesProvider();
            int[] candidates = preference == Preference.Chart ? ChartCodePages : KoreanCodePages;
            foreach (int codePage in candidates)
            {
                try
                {
                    var enc = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                    string text = enc.GetString(bytes);

                    // CP949 한글 완성형(0xB0~0xC8 / 0xA1~0xDF)은 Shift-JIS에서 반각 가타카나 두 글자로 '오류 없이' 읽힌다
                    // (완성형 2350자 중 약 2/3). 그대로 받으면 한국어 BMS의 제목·키음 이름("홀드 시작.wav" 등)이 깨져
                    // 홀드·킥/스네어 판정이 조용히 빠진다. 비ASCII 글자 대부분이 반각 가타카나면 CP949로 다시 읽어 본다.
                    if (codePage == 932 && LooksLikeMisdecodedKorean(text) && TryDecode(949, bytes, out string korean))
                    {
                        LastEncodingName = Encoding.GetEncoding(949).WebName;
                        return korean;
                    }

                    LastEncodingName = enc.WebName;
                    return text;
                }
                catch (DecoderFallbackException)
                {
                    // 이 코드페이지로 해석할 수 없는 바이트 → 다음 후보
                }
                catch (Exception)
                {
                    // 코드페이지 미지원(플랫폼) → 다음 후보
                }
            }

            // 최후: Latin-1은 모든 바이트를 문자로 대응시키므로 실패하지 않는다(ASCII 부분은 그대로 보존).
            LastEncodingName = "iso-8859-1 (fallback)";
            return Encoding.Latin1.GetString(bytes);
        }

        /// <summary>
        /// Shift-JIS로 읽은 결과가 '한국어를 잘못 읽은 것'처럼 보이는지. 실제 일본어 BMS는 비ASCII의 대부분이
        /// 히라가나·가타카나(전각)·한자이고 반각 가타카나는 드물다. 비ASCII가 4글자 이상이고 그 절반 넘게 반각 가타카나면 의심한다.
        /// </summary>
        private const char HalfWidthKanaFirst = (char)0xFF61; // ｡
        private const char HalfWidthKanaLast = (char)0xFF9F;  // ﾟ

        internal static bool LooksLikeMisdecodedKorean(string text)
        {
            int nonAscii = 0, halfWidthKana = 0;
            foreach (char c in text)
            {
                if (c < 0x80) continue;
                nonAscii++;
                if (c >= HalfWidthKanaFirst && c <= HalfWidthKanaLast) halfWidthKana++;
            }
            return nonAscii >= 4 && halfWidthKana * 2 > nonAscii;
        }

        private static bool TryDecode(int codePage, byte[] bytes, out string text)
        {
            try
            {
                var enc = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                text = enc.GetString(bytes);
                return true;
            }
            catch (Exception)
            {
                text = null;
                return false;
            }
        }

        private static void EnsureCodePagesProvider()
        {
            if (_providerRegistered) return;
            try
            {
                // .NET 6 공유 프레임워크에 포함된 System.Text.Encoding.CodePages 제공자 (932/949/54936 사용에 필요)
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch (Exception)
            {
                // 등록 실패 시 GetEncoding이 예외를 내고 Latin-1 폴백으로 내려간다.
            }
            _providerRegistered = true;
        }
    }
}
