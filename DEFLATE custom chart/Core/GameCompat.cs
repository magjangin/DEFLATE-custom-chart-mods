using System;
using System.IO;
using System.Text.RegularExpressions;
using MelonLoader;
using MelonLoader.Utils;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>
    /// 시작 시 현재 게임 빌드 지문(게임 버전 / Unity 버전 / Steam Build ID)을 로그에 남기고,
    /// 이 모드 소스가 마지막으로 시그니처 대조·빌드된 기준 빌드와 다르면 경고합니다.
    ///
    /// 목적은 차단이 아니라 <b>진단</b>입니다. Steam 자동 업데이트로 게임이 바뀌면 훅/필드 이름 변경이
    /// 조용히 기능을 무력화할 수 있으므로(예: 1.2의 LaneType 중첩 이동), 로그 첫머리에서 바로 알아차리게 합니다.
    /// 기준값을 갱신할 때는 AGENTS.md §1의 버전 표도 함께 갱신합니다.
    /// </summary>
    public static class GameCompat
    {
        /// <summary>Steam App ID (steamapps/appmanifest_&lt;id&gt;.acf).</summary>
        public const string SteamAppId = "3921360";

        // ── 기준 빌드 (시그니처 재덤프 + 컴파일 + 훅 대상 시그니처 대조 완료 시점) ──
        public const string ReferenceGameVersion = "1.2";
        public const string ReferenceUnityVersion = "2022.3.62f2";
        public const string ReferenceSteamBuildId = "26402105";

        public static string CurrentGameVersion { get; private set; } = "?";
        public static string CurrentUnityVersion { get; private set; } = "?";
        public static string CurrentSteamBuildId { get; private set; } = "?";

        /// <summary>현재 빌드가 기준 빌드와 일치하는지 (알 수 없는 항목은 비교에서 제외).</summary>
        public static bool MatchesReference { get; private set; } = true;

        public static void LogFingerprint()
        {
            try
            {
                CurrentGameVersion = SafeRead(() => UnityEngine.Application.version);
                CurrentUnityVersion = SafeRead(() => UnityEngine.Application.unityVersion);
                string acfText = ReadSteamManifest();
                CurrentSteamBuildId = ReadAcfValue(acfText, "buildid") ?? "?";

                bool gameOk = IsUnknownOrEqual(CurrentGameVersion, ReferenceGameVersion);
                bool unityOk = IsUnknownOrEqual(CurrentUnityVersion, ReferenceUnityVersion);
                bool buildOk = IsUnknownOrEqual(CurrentSteamBuildId, ReferenceSteamBuildId);
                MatchesReference = gameOk && unityOk && buildOk;

                string line = $"[GameCompat] 게임 {CurrentGameVersion} / Unity {CurrentUnityVersion} / Steam Build {CurrentSteamBuildId}"
                    + $" (기준: {ReferenceGameVersion} / {ReferenceUnityVersion} / {ReferenceSteamBuildId})";

                if (MatchesReference)
                {
                    MelonLogger.Msg(line + " → 일치");
                }
                else
                {
                    MelonLogger.Warning(line + " → 불일치!");
                    MelonLogger.Warning("[GameCompat] 게임이 업데이트되었습니다. [SafePatcher] 요약과 [HarmonyGuard]/[GetValueResilient] 경고를 확인하고, "
                        + "게임 1회 실행 → SignatureDumper 재덤프 → 재빌드 순서(AGENTS.md §6)로 대응하세요.");
                }

                WarnSteamUpdateState(acfText);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameCompat] 빌드 지문 확인 중 예외(무시하고 계속): {ex.Message}");
            }
        }

        private static string SafeRead(Func<string> reader)
        {
            try
            {
                string value = reader();
                return string.IsNullOrWhiteSpace(value) ? "?" : value.Trim();
            }
            catch
            {
                return "?";
            }
        }

        private static bool IsUnknownOrEqual(string current, string reference)
        {
            return current == "?" || string.Equals(current, reference, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Steam 업데이트 대기·자동 업데이트 설정을 경고합니다 (1.2 자동 업데이트로 모드가 깨진 사고 대비, AGENTS.md §1).
        /// <c>TargetBuildID</c>가 0/빈 값이면 "대기 없음"으로 봅니다(거짓 경고 방지). <c>StateFlags</c>의 UpdateRequired(2) 비트도 봅니다.
        /// </summary>
        private static void WarnSteamUpdateState(string acfText)
        {
            if (string.IsNullOrEmpty(acfText)) return; // Steam 밖 사본(DepotDownloader 등)은 조용히 넘어간다

            string target = ReadAcfValue(acfText, "TargetBuildID");
            bool targetPending = !string.IsNullOrEmpty(target) && target != "0" && target != CurrentSteamBuildId;
            bool flagPending = long.TryParse(ReadAcfValue(acfText, "StateFlags"), out long flags) && (flags & 2) != 0;
            if (targetPending || flagPending)
            {
                MelonLogger.Warning($"[GameCompat] Steam 업데이트가 대기 중입니다 (TargetBuildID={target ?? "?"}). "
                    + "다음 Steam 실행 때 게임이 바뀌어 훅이 깨질 수 있습니다.");
            }

            string autoUpdate = ReadAcfValue(acfText, "AutoUpdateBehavior");
            if (autoUpdate == "0")
            {
                ModLog.Verbose("[GameCompat] Steam 자동 업데이트가 켜져 있습니다(AutoUpdateBehavior=0). "
                    + "게임이 바뀌면 이 로그 첫머리의 '불일치!' 경고로 알 수 있습니다.");
            }
        }

        /// <summary>&lt;GameDir&gt;/../../appmanifest_&lt;appid&gt;.acf 전체 텍스트. Steam 외 설치/권한 문제 시 null.</summary>
        private static string ReadSteamManifest()
        {
            try
            {
                var gameDir = new DirectoryInfo(MelonEnvironment.GameRootDirectory);
                var steamApps = gameDir.Parent?.Parent; // steamapps/common/DEFLATE → steamapps
                if (steamApps == null) return null;

                string acf = Path.Combine(steamApps.FullName, $"appmanifest_{SteamAppId}.acf");
                return File.Exists(acf) ? File.ReadAllText(acf) : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>ACF(KeyValues) 텍스트에서 <c>"key"  "value"</c> 한 쌍의 값을 읽습니다. 없으면 null.</summary>
        private static string ReadAcfValue(string acfText, string key)
        {
            if (string.IsNullOrEmpty(acfText)) return null;
            var match = Regex.Match(acfText, "\"" + Regex.Escape(key) + "\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
