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
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(HUDControl), nameof(HUDControl.RefreshSongMetaUI));

            private static string _lastCoverName;
            private static string _lastTitle;

            public static void Postfix(HUDControl __instance, bool forceUpdate)
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
