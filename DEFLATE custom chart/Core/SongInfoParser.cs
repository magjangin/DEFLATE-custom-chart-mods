using System;
using System.Collections.Generic;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>info.txt 한 파일에서 읽은 값. 파일에 없던 항목은 null(난이도는 null 가능 int).</summary>
    public sealed class SongInfo
    {
        public string Title;
        public string SubTitle;
        public string Artist;
        public string Album;
        public string BgaAuthor;
        public string ChartAuthor;
        public int? EasyLevel;
        public int? NormalLevel;
        public int? HardLevel;
        public string EasyChart;
        public string NormalChart;
        public string HardChart;
    }

    /// <summary>
    /// info.txt 파서 (<c>키 : 값</c> 또는 <c>키 = 값</c>, 한 줄에 하나). 게임 타입을 참조하지 않아 단위 테스트에 링크됩니다.
    ///
    /// 키는 대소문자 무시·부분 일치로 판정하되, <b>더 구체적인 키를 먼저</b> 봅니다:
    /// <list type="number">
    /// <item><c>subtitle</c>/<c>부제</c> — 예전에는 "title"을 포함해 <b>제목을 부제로 덮어썼음</b>(v1.1.6 수정).</item>
    /// <item><c>album artist</c>/<c>앨범 아티스트</c> — 예전에는 "artist"를 포함해 <b>곡 아티스트를 덮어썼음</b>(무시).</item>
    /// <item>BGA·PV·영상 제작자 — "BGA 아티스트"가 아티스트로, "영상 제작자"가 매퍼로 들어가지 않게 아티스트·매퍼보다 먼저.</item>
    /// <item>제목 / 아티스트·작곡 / 앨범 / 매퍼·채보·제작자 / 난이도(easy·ez·이지, normal·nm·노멀·노말, hard·hd·하드).</item>
    /// </list>
    /// 같은 항목이 여러 번 나오면 마지막 값을 씁니다. 값이 빈 줄과 알 수 없는 키(예: <c>난이도 : easy, normal, hard</c>)는 무시합니다.
    /// </summary>
    public static class SongInfoParser
    {
        private static readonly char[] Separators = { ':', '=' };

        private static readonly HashSet<string> EasyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "easy", "ez", "이지" };
        private static readonly HashSet<string> NormalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "normal", "nm", "노멀", "노말" };
        private static readonly HashSet<string> HardKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "hard", "hd", "하드" };

        public static SongInfo Parse(IEnumerable<string> lines)
        {
            var info = new SongInfo();
            if (lines == null) return info;

            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                string[] parts = raw.Split(Separators, 2);
                if (parts.Length < 2) continue;

                string key = parts[0].Trim().TrimStart('﻿').Trim().ToLowerInvariant();
                string val = parts[1].Trim();
                if (key.Length == 0 || val.Length == 0) continue;

                Apply(info, key, val);
            }
            return info;
        }

        private static bool Has(string key, string word) => key.IndexOf(word, StringComparison.Ordinal) >= 0;

        private static void Apply(SongInfo info, string key, string val)
        {
            if (Has(key, "subtitle") || Has(key, "sub title") || Has(key, "부제"))
            {
                info.SubTitle = val;
                return;
            }

            if ((Has(key, "album") || Has(key, "앨범")) && (Has(key, "artist") || Has(key, "아티스트")))
            {
                return; // 앨범 아티스트(Various Artists 등)는 곡 아티스트가 아니다.
            }

            bool isBgaKey = Has(key, "bga") || Has(key, "pv") ||
                ((Has(key, "영상") || Has(key, "video") || Has(key, "movie")) &&
                 (Has(key, "제작") || Has(key, "author") || Has(key, "maker") || Has(key, "artist") || Has(key, "아티스트")));

            bool isChartKeyword = Has(key, "chart") || Has(key, "bms") || Has(key, "차트") || Has(key, "채보");
            bool isEasyChart = isChartKeyword && (Has(key, "easy") || Has(key, "ez") || Has(key, "이지") || Has(key, "쉬움"));
            bool isNormalChart = isChartKeyword && (Has(key, "normal") || Has(key, "nm") || Has(key, "노멀") || Has(key, "노말") || Has(key, "보통"));
            bool isHardChart = isChartKeyword && (Has(key, "hard") || Has(key, "hd") || Has(key, "하드") || Has(key, "어려움"));

            if (isEasyChart) info.EasyChart = val;
            else if (isNormalChart) info.NormalChart = val;
            else if (isHardChart) info.HardChart = val;
            else if (Has(key, "제목") || Has(key, "title")) info.Title = val;
            else if (isBgaKey) info.BgaAuthor = val;
            else if (Has(key, "아티스트") || Has(key, "artist") || Has(key, "작곡") || Has(key, "composer")) info.Artist = val;
            else if (Has(key, "앨범") || Has(key, "album")) info.Album = val;
            else if (Has(key, "매퍼") || Has(key, "mapper") || Has(key, "채보") || Has(key, "charter") || Has(key, "제작자") || Has(key, "maker")) info.ChartAuthor = val;
            else if (EasyKeys.Contains(key)) { if (TryParseLevel(val, out int v)) info.EasyLevel = v; }
            else if (NormalKeys.Contains(key)) { if (TryParseLevel(val, out int v)) info.NormalLevel = v; }
            else if (HardKeys.Contains(key)) { if (TryParseLevel(val, out int v)) info.HardLevel = v; }
        }

        /// <summary>"8", "★8", "8 (★)" 처럼 숫자 앞뒤에 기호가 붙어도 첫 정수를 읽습니다.</summary>
        internal static bool TryParseLevel(string val, out int level)
        {
            level = 0;
            int i = 0;
            while (i < val.Length && !char.IsDigit(val[i])) i++;
            int start = i;
            while (i < val.Length && char.IsDigit(val[i])) i++;
            return i > start && int.TryParse(val.Substring(start, i - start), out level);
        }
    }
}
