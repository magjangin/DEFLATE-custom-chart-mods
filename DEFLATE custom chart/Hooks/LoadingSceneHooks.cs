using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;

namespace DEFLATE_custom_chart.Hooks
{
    public static class LoadingSceneHooks
    {
        [HarmonyPatch(typeof(LoadingGamePlay), nameof(LoadingGamePlay.Start))]
        public static class LoadingGamePlay_Start_Patch
        {
            public static void Postfix(LoadingGamePlay __instance)
            {
                if (__instance == null || __instance.gameData == null) return;

                var gd = __instance.gameData;
                MelonLogger.Msg("==================================================");
                MelonLogger.Msg("[로딩 씬 진입] 곡 선택 정보 검증 완료!");
                MelonLogger.Msg($"  - 곡 제목:      '{gd.NowTrackTitle}'");
                MelonLogger.Msg($"  - 곡 아티스트:   '{gd.NowTrackAurthor}'");
                MelonLogger.Msg($"  - 곡 ID:        '{gd.NowTrackID}'");
                MelonLogger.Msg($"  - 타겟 차트 Key: '{gd.targetKorePath}'");
                MelonLogger.Msg($"  - 오디오 Key:    '{gd.audioClip_Key}'");
                MelonLogger.Msg($"  - BGA Key:      '{gd.pv_Key}'");
                MelonLogger.Msg("==================================================");
            }
        }

        [HarmonyPatch(typeof(LoadingManager), nameof(LoadingManager.LoadScene))]
        public static class LoadingManager_LoadScene_Patch
        {
            public static void Prefix(string sceneName)
            {
                MelonLogger.Msg($"[씬 전환 탐지] target scene -> '{sceneName}'");
            }
        }
    }
}
