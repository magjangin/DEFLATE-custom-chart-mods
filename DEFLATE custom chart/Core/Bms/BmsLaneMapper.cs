using System;
using System.Collections.Generic;

namespace DEFLATE_custom_chart.Core.Bms
{
    /// <summary>
    /// BMS 노트 ➔ DEFLATE 인게임 레인(Koreography EventID) 라우팅 규칙.
    ///
    /// <b>채널이 곧 레인입니다.</b> 인식 채널은 다섯 개뿐입니다.
    ///   16 / 11 / 12 / 13 ➔ 플레이 레인 1~4 (드롭을 제외한 모든 노트)
    ///   14                ➔ 드롭 전용 레인
    ///
    /// DEFLATE는 같은 레인 4개를 <see cref="Il2Cppdizzylab.castor.DrumMode"/>(HiHat / KickSnare)로
    /// 갈아끼우는 구조라, 레인 1~4는 하이햇 모드에서 hihat_1~4, 킥스네어 모드에서 킥/스네어가 됩니다.
    /// 따라서 킥/스네어 전용 채널은 없고, 4개 채널에 전부 찍으면 됩니다.
    /// (키음 파일명은 홀드 시작/끝 판정에만 쓰이고, 레인 판정에는 관여하지 않습니다.)
    /// </summary>
    public static class BmsLaneMapper
    {
        /// <summary>플레이 레인 슬롯 ID. 하이햇 모드/킥스네어 모드가 공유하는 물리 레인입니다.</summary>
        public const string Lane1 = "lane_1";
        public const string Lane2 = "lane_2";
        public const string Lane3 = "lane_3";
        public const string Lane4 = "lane_4";
        public const string Drop = "drop";

        /// <summary>BMS 채널 ➔ 레인 슬롯. 여기 없는 채널의 노트는 무시됩니다.</summary>
        private static readonly Dictionary<string, string> ChannelToLane = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "16", Lane1 },
            { "11", Lane2 },
            { "12", Lane3 },
            { "13", Lane4 },
            { "14", Drop },
        };

        /// <summary>
        /// 인게임 레인 이름 ➔ 레인 슬롯. 하이햇 레인과 킥/스네어 레인이 같은 슬롯을 가리킵니다.
        /// 순서 주의: 긴 이름이 먼저 와야 오검출이 없습니다.
        /// </summary>
        private static readonly (string Keyword, string Lane)[] LaneNameKeywords =
        {
            ("hihat_1", Lane1), ("kick_left", Lane1),
            ("hihat_2", Lane2), ("kick_right", Lane2),
            ("hihat_3", Lane3), ("snare_left", Lane3),
            ("hihat_4", Lane4), ("snare_right", Lane4),
            ("drop", Drop),
        };

        /// <summary>BMS 노트가 어느 레인으로 가야 하는지 판정합니다. 채널이 표에 없으면 null.</summary>
        public static string ResolveNoteLane(BmsNote note)
        {
            if (note == null || string.IsNullOrEmpty(note.Channel)) return null;

            return ChannelToLane.TryGetValue(note.Channel, out string lane) ? lane : null;
        }

        /// <summary>
        /// 인게임 훅이 받은 Koreography trackID / LaneController.laneType 이름으로 레인 슬롯을 판정합니다.
        /// (예: trackID "hihat_2" 또는 laneType "Kick_Right" ➔ lane_2)
        /// </summary>
        public static string ResolveLaneId(string trackID, string laneTypeName)
        {
            string tid = trackID != null ? trackID.ToLowerInvariant() : string.Empty;
            string ltype = laneTypeName != null ? laneTypeName.ToLowerInvariant() : string.Empty;

            foreach (var (keyword, lane) in LaneNameKeywords)
            {
                if (tid.Contains(keyword) || ltype.Contains(keyword)) return lane;
            }
            return null;
        }
    }
}
