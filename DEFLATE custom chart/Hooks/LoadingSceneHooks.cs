using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using DEFLATE_custom_chart.Core;

namespace DEFLATE_custom_chart.Hooks
{
    public static class LoadingSceneHooks
    {
        [HarmonyPatch(typeof(LoadingGamePlay), nameof(LoadingGamePlay.Start))]
        public static class LoadingGamePlay_Start_Patch
        {
            public static void Postfix(LoadingGamePlay __instance)
            {
                // 곡 목록 프리뷰 전용 BGM 오버라이드 훅(TrackAssetManager.LoadAudioClip)이
                // 로딩 씬의 실제 게임플레이 오디오 로드 요청까지 잘못 가로채 원본 로직을 스킵시키던
                // 크래시를 막기 위해, 로딩 씬 진입 시점에 프리뷰 컨텍스트를 명시적으로 종료한다.
                HwaAssetManager.IsInSongSelectContext = false;

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
