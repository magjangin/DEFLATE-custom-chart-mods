using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;

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
            private static string lastLogged = null;

            public static void Postfix(HUDControl __instance, bool forceUpdate)
            {
                if (__instance == null) return;

                string coverName = __instance.cachedCover != null ? __instance.cachedCover.name : "null";
                string key = $"{coverName}|{__instance.cachedTitle}";
                if (key == lastLogged) return;
                lastLogged = key;

                MelonLogger.Msg($"[HUD] Cover='{coverName}' | Title='{__instance.cachedTitle}'");
            }
        }
    }
}
