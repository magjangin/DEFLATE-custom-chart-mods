using System;
using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using UnityEngine.UI;
using DEFLATE_custom_chart.Core;
using DEFLATE_custom_chart.Core.Wrapper;

namespace DEFLATE_custom_chart.Hooks
{
    public static class LoadingSceneHooks
    {
        private static void ApplyLoadingUiMetadata(LoadingGamePlay instance)
        {
            try
            {
                if (instance == null || instance.gameData == null) return;
                if (!HwaAssetManager.IsTargetTrackActive) return;

                var meta = HwaAssetManager.CurrentMeta ?? new HwaAssetManager.HwaMetaInfo();
                var gd = instance.gameData;

                var customCover = HwaAssetManager.LoadCoverSprite();
                int targetStar = meta.GetLevelForDifficulty((int)gd.nowDifficulty);

                HwaAssetManager.ApplyMetaToGameData(gd, customCover, targetStar);

                if (instance.NowTrackTitle != null) instance.NowTrackTitle.text = meta.Title;

                var authorText = Il2CppReflectionHelper.GetValueResilient<Text>(
                    instance,
                    () => instance.NowTrackAurthor,
                    "NowTrackAurthor",
                    new[] { "NowTrackAuthor", "nowTrackAuthor", "TrackAuthor" },
                    null);
                if (authorText != null) authorText.text = meta.Artist;

                if (instance.NowTrackPVAuthor != null) instance.NowTrackPVAuthor.text = meta.BgaAuthor;

                var labelText = Il2CppReflectionHelper.GetValueResilient<Text>(
                    instance,
                    () => instance.NowTracLabel,
                    "NowTracLabel",
                    new[] { "NowTrackLabel", "nowTrackLabel", "TrackLabel" },
                    null);
                if (labelText != null) labelText.text = meta.Album;

                if (instance.NowTrackScore != null) instance.NowTrackScore.text = meta.ChartAuthor;
                if (instance.NowTrackDifficulty != null) instance.NowTrackDifficulty.text = targetStar.ToString();
                if (instance.diffStarsCtrl != null) instance.diffStarsCtrl.GenerateStarImages(targetStar);

                if (customCover != null)
                {
                    if (instance.NowTrackCover != null) instance.NowTrackCover.sprite = customCover;
                    if (instance.NowTrackCover_bg != null) instance.NowTrackCover_bg.sprite = customCover;
                }

                MelonLogger.Msg($"[로딩 씬 커스텀 UI 주입] 제목: '{meta.Title}' | 아티스트: '{meta.Artist}' | 매퍼: '{meta.ChartAuthor}' | BGA: '{meta.BgaAuthor}' | 난이도: {gd.nowDifficulty}(★{targetStar}) | 앨범: '{meta.Album}'");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[로딩 씬 UI 주입] ApplyLoadingUiMetadata 처리 중 예외: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(LoadingGamePlay), nameof(LoadingGamePlay.Start))]
        public static class LoadingGamePlay_Start_Patch
        {
            public static void Postfix(LoadingGamePlay __instance)
            {
                try
                {
                    // 곡 목록 프리뷰 전용 BGM 오버라이드 훅(TrackAssetManager.LoadAudioClip)이
                    // 로딩 씬의 실제 게임플레이 오디오 로드 요청까지 잘못 가로채 원본 로직을 스킵시키던
                    // 크래시를 막기 위해, 로딩 씬 진입 시점에 프리뷰 컨텍스트를 명시적으로 종료한다.
                    HwaAssetManager.IsInSongSelectContext = false;

                    ApplyLoadingUiMetadata(__instance);

                    if (HwaAssetManager.IsTargetTrackActive && !string.IsNullOrEmpty(HwaAssetManager.BgmFilePath))
                    {
                        MelonLogger.Msg("[로딩 씬] 커스텀 BGM 사전 로딩(Preload) 시작!");
                        MelonCoroutines.Start(HwaAssetManager.LoadCustomBgmCoroutine(null, false));
                    }

                    if (__instance == null || __instance.gameData == null) return;
                    var gd = __instance.gameData;

                    var meta = HwaAssetManager.CurrentMeta ?? new HwaAssetManager.HwaMetaInfo();
                    string trackAuthor = Il2CppReflectionHelper.GetValueResilient(
                        gd,
                        () => gd.NowTrackAurthor,
                        "NowTrackAurthor",
                        new[] { "NowTrackAuthor", "nowTrackAuthor" },
                        string.Empty);

                    MelonLogger.Msg("==================================================");
                    MelonLogger.Msg($"[로딩 씬 진입 감지] TargetActive: {HwaAssetManager.IsTargetTrackActive}");
                    MelonLogger.Msg($"  - 곡 제목:      '{gd.NowTrackTitle}'");
                    MelonLogger.Msg($"  - 곡 아티스트:   '{trackAuthor}'");
                    MelonLogger.Msg($"  - 채보 매퍼:    '{meta.ChartAuthor}'");
                    MelonLogger.Msg($"  - PV/BGA 제작자: '{meta.BgaAuthor}'");
                    MelonLogger.Msg($"  - 곡 ID:        '{gd.NowTrackID}'");
                    MelonLogger.Msg("==================================================");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[LoadingGamePlay.Start] Postfix 처리 중 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(LoadingGamePlay), nameof(LoadingGamePlay.InitializeAndPlayAnimations))]
        public static class LoadingGamePlay_InitializeAndPlayAnimations_Patch
        {
            public static void Postfix(LoadingGamePlay __instance)
            {
                ApplyLoadingUiMetadata(__instance);
            }
        }

        [HarmonyPatch(typeof(LoadingGamePlay), nameof(LoadingGamePlay.HandleEmptyFields))]
        public static class LoadingGamePlay_HandleEmptyFields_Patch
        {
            public static void Postfix(LoadingGamePlay __instance)
            {
                ApplyLoadingUiMetadata(__instance);
            }
        }

        [HarmonyPatch(typeof(LoadingManager), nameof(LoadingManager.LoadScene))]
        public static class LoadingManager_LoadScene_Patch
        {
            public static void Prefix(string sceneName)
            {
                try
                {
                    MelonLogger.Msg($"[씬 전환 탐지] target scene -> '{sceneName}'");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[LoadingManager.LoadScene] Prefix 처리 중 예외: {ex.Message}");
                }
            }
        }
    }
}
