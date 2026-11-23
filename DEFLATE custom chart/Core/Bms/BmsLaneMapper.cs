using System;
using System.Collections.Generic;
using System.IO;

namespace DEFLATE_custom_chart.Core.Bms
{
    /// <summary>
    /// BMS 노트 ➔ DEFLATE 인게임 레인(Koreography trackID = LaneController) 라우팅 규칙.
    ///
    /// <b>노트 하나는 정확히 한 레인에만 들어갑니다.</b>
    /// <list type="bullet">
    /// <item><b>열(1~4)</b>은 채널이 정합니다: <c>16</c>=1열, <c>11</c>=2열, <c>12</c>=3열, <c>13</c>=4열, <c>14</c>=드롭.</item>
    /// <item><b>하이햇 / 킥·스네어</b>는 그 노트의 키음(#WAV) 파일명이 정합니다:
    ///   이름에 <c>kick</c> 또는 <c>snare</c>가 있으면 킥·스네어, 그 외는 전부 하이햇.</item>
    /// </list>
    ///
    /// <b>화면 열 ↔ 게임 레인 (2026-11-20 실기 측정: LaneController.thisPad = KEY_1~4, transform.x)</b>
    /// <code>
    ///   1열(KEY_1): hihat_1 | snare_left
    ///   2열(KEY_2): hihat_2 | kick_left
    ///   3열(KEY_3): hihat_3 | kick_right
    ///   4열(KEY_4): hihat_4 | snare_right
    /// </code>
    ///
    /// <b>예전 규칙이 틀렸던 이유:</b> 이전에는 "레인 4개를 하이햇/킥·스네어 모드가 공유한다"고 보고 노트 하나를
    /// hihat_N과 킥·스네어 레인 양쪽에 복제했습니다. 실측해 보니 ① 킥/스네어의 열 배치가 달라(kick_left는 1열이 아니라 2열)
    /// 노트가 서로 다른 두 열에 나왔고, ② 원곡 차트에서 킥·스네어와 하이햇 노트가 같은 시각에 나오는 경우가 0건이라
    /// 두 세트는 같은 노트의 두 표현이 아니라 별개 노트였습니다. 결과적으로 모든 노트가 두 번 생성·판정됐습니다(AllNoteCount 2배).
    /// </summary>
    public static class BmsLaneMapper
    {
        public const string HiHat1 = "hihat_1";
        public const string HiHat2 = "hihat_2";
        public const string HiHat3 = "hihat_3";
        public const string HiHat4 = "hihat_4";
        public const string KickLeft = "kick_left";
        public const string KickRight = "kick_right";
        public const string SnareLeft = "snare_left";
        public const string SnareRight = "snare_right";
        public const string Drop = "drop";

        /// <summary>열(1~4)별 하이햇 레인.</summary>
        public static readonly string[] HiHatByColumn = { HiHat1, HiHat2, HiHat3, HiHat4 };

        /// <summary>열(1~4)별 킥·스네어 레인 (실측 배치).</summary>
        public static readonly string[] KickSnareByColumn = { SnareLeft, KickLeft, KickRight, SnareRight };

        /// <summary>BMS 노트를 받을 수 있는 게임 레인 전체 (trackID/laneType 이름 판정 순서).</summary>
        public static readonly string[] AllGameLanes =
        {
            HiHat1, HiHat2, HiHat3, HiHat4, KickLeft, KickRight, SnareLeft, SnareRight, Drop,
        };

        /// <summary>BMS 채널 ➔ 화면 열(1~4). 드롭(14)은 0. 여기 없는 채널의 노트는 무시됩니다.</summary>
        private static readonly Dictionary<string, int> ChannelToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "16", 1 },
            { "11", 2 },
            { "12", 3 },
            { "13", 4 },
            { "14", 0 },
        };

        /// <summary>이 채널이 레인 노트(16/11/12/13/14) 채널인지 판정합니다. 파서가 노트를 생성할지 걸러낼 때 씁니다.</summary>
        public static bool IsNoteChannel(string channel)
        {
            return !string.IsNullOrEmpty(channel) && ChannelToColumn.ContainsKey(channel);
        }

        /// <summary>킥·스네어 키음 이름 키워드. 홀드 키음("홀드 시작")처럼 한글 이름도 받는다(v1.1.3~).</summary>
        private static readonly string[] KickSnareKeywords = { "kick", "snare", "킥", "스네어" };

        /// <summary>키음 파일명이 킥·스네어 계열인지 (이름에 kick/snare/킥/스네어 포함, 대소문자 무시).</summary>
        public static bool IsKickSnareKeysound(string keysoundFile)
        {
            if (string.IsNullOrEmpty(keysoundFile)) return false;
            string name = Path.GetFileNameWithoutExtension(keysoundFile);
            foreach (var kw in KickSnareKeywords)
            {
                if (name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>채널 + 키음 파일명 ➔ 게임 레인 ID. 노트 채널이 아니면 null.</summary>
        public static string ResolveGameLane(string channel, string keysoundFile)
        {
            if (string.IsNullOrEmpty(channel) || !ChannelToColumn.TryGetValue(channel, out int column)) return null;
            if (column == 0) return Drop;
            return IsKickSnareKeysound(keysoundFile) ? KickSnareByColumn[column - 1] : HiHatByColumn[column - 1];
        }

        /// <summary>게임 레인 ID ➔ 화면 열(1~4). 드롭/미지정은 0.</summary>
        public static int ColumnOf(string gameLane)
        {
            for (int i = 0; i < 4; i++)
            {
                if (string.Equals(HiHatByColumn[i], gameLane, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(KickSnareByColumn[i], gameLane, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1;
                }
            }
            return 0;
        }

        /// <summary>
        /// 키음 파일명이 특정 킥·스네어 레인 이름(예: "kick_left")을 담고 있는데 채널(열)로 배치된 레인과 다르면 그 레인 이름을 돌려줍니다.
        /// (예: kick_left.wav를 1열(16)에 찍으면 1열의 킥·스네어 레인인 snare_left로 배치됨)
        /// 하이햇 번호(hihat_1~4.wav)는 공통 하이햇 소리로 쓰이는 경우가 많아 비교하지 않습니다. 배치는 언제나 채널(열) 기준이며, 진단용입니다.
        /// </summary>
        public static string KeysoundLaneHintMismatch(string keysoundFile, string assignedLane)
        {
            if (string.IsNullOrEmpty(keysoundFile) || string.IsNullOrEmpty(assignedLane)) return null;
            string name = Path.GetFileNameWithoutExtension(keysoundFile);
            foreach (var lane in KickSnareByColumn)
            {
                if (name.IndexOf(lane, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return string.Equals(lane, assignedLane, StringComparison.OrdinalIgnoreCase) ? null : lane;
                }
            }
            return null;
        }

        /// <summary>BMS 노트가 들어갈 게임 레인. 파서가 키음까지 보고 정한 값(ResolvedLane)을 우선합니다.</summary>
        public static string ResolveNoteLane(BmsNote note)
        {
            if (note == null) return null;
            if (!string.IsNullOrEmpty(note.ResolvedLane)) return note.ResolvedLane;

            string lane = ResolveGameLane(note.Channel, null);
            if (lane != null) note.ResolvedLane = lane;
            return lane;
        }

        /// <summary>
        /// 인게임 훅이 받은 Koreography trackID와 <c>LaneController.laneType</c> 값(박싱된 열거형)으로 게임 레인 ID를 판정합니다.
        ///
        /// 열거형 <b>타입</b>에 컴파일 타임 의존을 두지 않는 이유: 게임 1.2(Build 26402105)에서 최상위 <c>LaneType</c>이
        /// <c>LaneController.LaneType</c> 중첩 열거형으로 옮겨지고 값이 다른 <c>CommingNote.LaneType</c>이 새로 생겨,
        /// 열거형 타입을 직접 참조하던 이전 빌드는 런타임 TypeLoadException으로 BMS 노트 주입 전체가 조용히 무력화됐습니다.
        /// 멤버 이름("HiHat_3", "Kick_Left" 등)은 선언 위치가 바뀌어도 유지되므로 이름 기반으로만 판정합니다.
        /// </summary>
        public static string ResolveLaneId(string trackID, object laneTypeValue)
        {
            // trackID 판정이 열거형 이름보다 우선한다.
            return ResolveLaneId(trackID, (string)null) ?? ResolveLaneId(null, laneTypeValue?.ToString());
        }

        /// <summary>trackID("hihat_2") 또는 laneType 이름("Kick_Right")을 게임 레인 ID("hihat_2"/"kick_right")로 정규화합니다.</summary>
        public static string ResolveLaneId(string trackID, string laneTypeName)
        {
            foreach (var lane in AllGameLanes)
            {
                if ((trackID != null && trackID.IndexOf(lane, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (laneTypeName != null && laneTypeName.IndexOf(lane, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return lane;
                }
            }
            return null;
        }
    }
}
