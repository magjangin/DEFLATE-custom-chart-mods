using System;
using MelonLoader;
using DEFLATE_custom_chart.Core;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using DEFLATE_custom_chart.Core.Wrapper;

namespace DEFLATE_custom_chart.Hooks
{
    // =============================================================================
    // Canvas_UI/HUD/BigHUD, controlled by HUDControl - the live gameplay HUD panel
    // (song cover, title, author, difficulty stars, PERFECT/GOOD/MISMATCH/MISS
    // counters, and the song progress slider/time). Distinct from Panel_Result
    // (Canvas_UI/playDataPanel/#RESULT_PANEL), which is the separate end-of-song
    // results screen that happens to reuse similar judge-label text.
    // =============================================================================
    public static class HUDHooks
    {
        [HarmonyPatch(typeof(HUDControl), nameof(HUDControl.RefreshSongMetaUI))]
        public static class HUDControl_RefreshSongMetaUI_Patch
        {
            // 로그 전용 훅: 로그 레벨이 모자라면 패치하지 않는다(호출마다 IL2CPP 문자열 두 개를 읽던 비용 제거).
            public static bool Prepare() => ModLog.Prepare(ModLog.Detail) && Il2CppReflectionHelper.HasPatchTarget(typeof(HUDControl), nameof(HUDControl.RefreshSongMetaUI));

            private static string _lastCoverName;
            private static string _lastTitle;

            // 원본 인자(bool forceUpdate)는 쓰지 않으므로 선언하지 않는다.
            public static void Postfix(HUDControl __instance)
            {
                try
                {
                    if (__instance == null) return;

                    string coverName = __instance.cachedCover != null ? __instance.cachedCover.name : "null";
                    string title = __instance.cachedTitle;
                    if (string.Equals(coverName, _lastCoverName, StringComparison.Ordinal) &&
                        string.Equals(title, _lastTitle, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _lastCoverName = coverName;
                    _lastTitle = title;
                    ModLog.Verbose($"[HUD] Cover='{coverName}' | Title='{title}'");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[HUDControl.RefreshSongMetaUI] Postfix 예외: {ex.Message}");
                }
            }
        }
    }
}
