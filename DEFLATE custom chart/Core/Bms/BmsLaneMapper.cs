using System;
using System.Collections.Generic;

namespace DEFLATE_custom_chart.Core.Bms
{
    /// <summary>
    /// BMS 노트 ➔ DEFLATE 인게임 레인(Koreography EventID) 라우팅 규칙.
    ///
    /// 판정 순서:
    ///   1. 노트가 찍힌 <b>채널</b>이 아래 매핑 표에 있으면 그 레인
    ///   2. 없으면 노트의 <b>#WAV 키음 파일명</b>에서 레인 이름을 추론
    ///      (예: 'kick_left.wav', 'snare_right 홀드 시작.wav' ➔ kick_right/snare_right 레인)
    ///
    /// 덕분에 하이햇/드롭은 기존 채널 규격 그대로 동작하고, 킥/스네어는 키음 이름만 맞으면
    /// 어느 빈 채널에 찍어도 해당 레인으로 들어갑니다.
    /// </summary>
    public static class BmsLaneMapper
    {
        public const string Hihat1 = "hihat_1";
        public const string Hihat2 = "hihat_2";
        public const string Hihat3 = "hihat_3";
        public const string Hihat4 = "hihat_4";
        public const string KickLeft = "kick_left";
        public const string KickRight = "kick_right";
        public const string SnareLeft = "snare_left";
        public const string SnareRight = "snare_right";
        public const string Drop = "drop";

        /// <summary>BMS 채널 ➔ 레인. 5x번대는 같은 레인의 롱노트(LN) 채널입니다.</summary>
        private static readonly Dictionary<string, string> ChannelToLane = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 하이햇 / 드롭 (기존 규격)
            { "16", Hihat1 }, { "56", Hihat1 },
            { "11", Hihat2 }, { "51", Hihat2 },
            { "12", Hihat3 }, { "52", Hihat3 },
            { "13", Hihat4 }, { "53", Hihat4 },
            { "14", Drop },   { "54", Drop },

            // 킥 / 스네어 (P1 잔여 채널 + LN 채널)
            { "15", KickLeft },   { "55", KickLeft },
            { "18", KickRight },  { "58", KickRight },
            { "19", SnareLeft },  { "59", SnareLeft },
            { "17", SnareRight }, { "57", SnareRight },

            // 킥 / 스네어 대체 표기 (P2 사이드. 21번은 BGM 표기로 쓰이는 경우가 있어 비워둡니다)
            { "22", KickLeft },
            { "23", KickRight },
            { "24", SnareLeft },
            { "25", SnareRight },
        };

        /// <summary>키음 파일명에서 레인을 추론할 때 쓰는 키워드. 긴 이름이 먼저 와야 오검출이 없습니다.</summary>
        private static readonly (string Keyword, string Lane)[] KeysoundLaneKeywords =
        {
            ("hihat_1", Hihat1),
            ("hihat_2", Hihat2),
            ("hihat_3", Hihat3),
            ("hihat_4", Hihat4),
            ("kick_left", KickLeft),
            ("kick_right", KickRight),
            ("snare_left", SnareLeft),
            ("snare_right", SnareRight),
            ("drop", Drop),
        };

        /// <summary>BMS 노트가 어느 레인으로 가야 하는지 판정합니다. 해당 없으면 null.</summary>
        public static string ResolveNoteLane(BmsNote note)
        {
            if (note == null) return null;

            if (!string.IsNullOrEmpty(note.Channel) && ChannelToLane.TryGetValue(note.Channel, out string byChannel))
            {
                return byChannel;
            }

            return ResolveLaneFromKeysound(note.KeysoundFile);
        }

        public static string ResolveLaneFromKeysound(string keysoundFile)
        {
            if (string.IsNullOrEmpty(keysoundFile)) return null;

            foreach (var (keyword, lane) in KeysoundLaneKeywords)
            {
                if (keysoundFile.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return lane;
            }
            return null;
        }

        /// <summary>
        /// 인게임 훅이 받은 Koreography trackID / LaneController.laneType 이름으로 레인을 판정합니다.
        /// (예: trackID "snare_left", laneType "Snare_Left" ➔ snare_left)
        /// </summary>
        public static string ResolveLaneId(string trackID, string laneTypeName)
        {
            string tid = trackID != null ? trackID.ToLowerInvariant() : string.Empty;
            string ltype = laneTypeName != null ? laneTypeName.ToLowerInvariant() : string.Empty;

            foreach (var (keyword, lane) in KeysoundLaneKeywords)
            {
                if (tid.Contains(keyword) || ltype.Contains(keyword)) return lane;
            }
            return null;
        }
    }
}
