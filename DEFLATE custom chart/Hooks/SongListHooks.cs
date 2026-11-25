using System;
using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using DEFLATE_custom_chart.Core;
using DEFLATE_custom_chart.Core.Wrapper;

namespace DEFLATE_custom_chart.Hooks
{
    public static class SongListHooks
    {
        [HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.Start))]
        public static class MainTrackList_Start_Patch
        {
            [HarmonyPriority(Priority.Last)]
            public static void Postfix(MainTrackList __instance)
            {
                try
                {
                    // 곡 목록 씬으로 (재)진입 시 프리뷰 컨텍스트를 다시 활성화한다.
                    HwaAssetManager.EnterSongSelect();

                    if (__instance == null || __instance.tracks == null) return;

                    // 117+곡 전체 카탈로그 덤프는 씬 진입마다 ~1,000줄 I/O 스팸 및 117개 래퍼 할당 병목을 유발하므로
                    // 요약 + 주입된 커스텀 곡만 간결하게 기록한다.
                    int customCount = 0;
                    for (int i = 0; i < __instance.tracks.Length; i++)
                    {
                        var track = __instance.tracks[i];
                        if (!SongInjectorHooks.IsCustomBlock(track)) continue;

                        customCount++;
                        try
                        {
                            var wrapped = new CustomTrackWrapper(track);
                            ModLog.Verbose($"  [커스텀 트랙 #{i + 1}] '{wrapped.Title}' / {wrapped.Artist} (앨범: '{wrapped.AlbumName}', ID: {wrapped.UniqueID}, ★{wrapped.EZ_Star}/{wrapped.NM_Star}/{wrapped.HD_Star})");
                        }
                        catch (Exception trackEx)
                        {
                            MelonLogger.Warning($"[곡 목록 스캔] 커스텀 트랙 #{i + 1} 로깅 중 예외: {trackEx.Message}");
                        }
                    }

                    ModLog.Verbose($"[곡 목록 카탈로그] 총 {__instance.tracks.Length}곡 (커스텀 {customCount}곡)");

                    // config.txt SmokeTest=1이면 첫 커스텀 곡을 자동 선택·플레이해 파이프라인 전체를 점검한다.
                    SmokeTest.OnSongListReady(__instance, SongInjectorHooks.IsCustomBlock);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[곡 목록 스캔] MainTrackList.Start 로깅 중 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.GoToTrack))]
        public static class MainTrackList_GoToTrack_Patch
        {
            // 로그 전용 훅: 로그 레벨이 모자라면 패치하지 않는다.
            public static bool Prepare() => ModLog.Prepare(ModLog.DebugLevel);

            public static void Postfix(MainTrackList __instance, object[] __args)
            {
                try
                {
                    if (__instance == null || __instance.tracks == null) return;
                    int trackIndex = HookArgs.FirstInt(__args, -1);
                    if (trackIndex >= 0 && trackIndex < __instance.tracks.Length)
                    {
                        var track = __instance.tracks[trackIndex];
                        if (track != null)
                        {
                            string coverName = track.TrackCover != null ? track.TrackCover.name : "null";
                            ModLog.Debug($"[곡 목록 커서 이동] 인덱스: {trackIndex} | 제목: '{track.TrackTitle}' (ID: {track.uniqueID}) | 자켓: '{coverName}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[곡 목록 커서 이동] GoToTrack 로깅 중 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(TrackListBlockCtrl), nameof(TrackListBlockCtrl.SetSelected))]
        public static class TrackListBlockCtrl_SetSelected_Patch
        {
            public static void Postfix(TrackListBlockCtrl __instance, bool isSelected)
            {
                try
                {
                    if (__instance == null || !isSelected) return;

                    // 곡 목록에서 커서가 옮겨갈 때마다 hwa/ 커스텀 에셋(사본 전용) 활성 상태를 갱신
                    // → 목록 PV 프리뷰(BGA)에서도 사본에서만 커스텀 비디오/오디오가 적용되도록 함
                    string uniqueId = Il2CppReflectionHelper.GetValueResilient(__instance, () => __instance.UniqueID, "UniqueID", MemberFallbacks.TrackList.BlockCtrlUniqueId, string.Empty);
                    string titleText = __instance.UI_TrackTitle != null ? __instance.UI_TrackTitle.text : null;
                    bool isTargetTrack = HwaAssetManager.SetActiveTrack(uniqueId, titleText);
                    if (isTargetTrack)
                    {
                        HwaAssetManager.ApplyCustomCover(__instance.cover);
                    }

                    string coverName = __instance.cover != null && __instance.cover.sprite != null ? __instance.cover.sprite.name : "null";
                    ModLog.Debug($"[곡 블록 선택 감지] 인덱스: {__instance.IndexInTrackList} | 제목: '{titleText}' (ID: {uniqueId}) | UI 자켓: '{coverName}' | 커스텀 에셋 사본 여부: {isTargetTrack}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[곡 블록 선택 감지] SetSelected 처리 중 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(TrackListBlockCtrl), nameof(TrackListBlockCtrl.OnPointerClick))]
        public static class TrackListBlockCtrl_OnPointerClick_Patch
        {
            // 로그 전용 훅: 로그 레벨이 모자라면 패치하지 않는다.
            public static bool Prepare() => ModLog.Prepare(ModLog.DebugLevel);

            // 원본 인자(PointerEventData)는 쓰지 않으므로 선언하지 않는다(이름이 바뀌어도 패치 실패 없음).
            public static void Postfix(TrackListBlockCtrl __instance)
            {
                try
                {
                    if (__instance == null) return;
                    ModLog.Debug($"[곡 클릭 감지] 인덱스: {__instance.IndexInTrackList} | 제목: '{__instance.UI_TrackTitle?.text}' (ID: {__instance.UniqueID})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[곡 클릭 감지] OnPointerClick 처리 중 예외: {ex.Message}");
                }
            }
        }

        // 진단용: 1.2의 사용자 플레이 경로는 GotoSceneData를 거치지 않고 로딩 씬(LoadingGamePlay)으로 간다(2026-11-20 실기 확인).
        // 어느 씬 이름으로 어떤 경로를 타는지 다음 업데이트 때 바로 확인할 수 있게 씬 이름만 기록한다.
        [HarmonyPatch(typeof(MainTrackList), "GoToScene")]
        public static class MainTrackList_GoToScene_Patch
        {
            public static bool Prepare() => ModLog.Prepare(ModLog.Detail) && Il2CppReflectionHelper.HasPatchTarget(typeof(MainTrackList), "GoToScene");

            public static void Prefix(object[] __args)
            {
                try
                {
                    ModLog.Verbose($"[씬 전환 탐지] MainTrackList.GoToScene('{HookArgs.First<string>(__args)}') | 커스텀 곡 활성: {HwaAssetManager.IsTargetTrackActive}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[MainTrackList.GoToScene] Prefix 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.GotoSceneData))]
        public static class MainTrackList_GotoSceneData_Patch
        {
            // 원본이 정상 종료(=플레이 확정 및 씬 전환 시작)했을 때만 실행된다. 예외로 중단되면 곡 선택 컨텍스트를 유지한다.
            public static void Postfix()
            {
                HwaAssetManager.EnterGameplay("MainTrackList.GotoSceneData");
            }

            public static void Prefix(MainTrackList __instance)
            {
                try
                {
                    if (__instance == null) return;

                    string nowSongId = Il2CppReflectionHelper.GetValueResilient(__instance, () => __instance.NowSongID, "NowSongID", MemberFallbacks.TrackList.NowSongId, string.Empty);
                    string uiTitle = __instance.UI_TrackTitle != null ? __instance.UI_TrackTitle.text : null;
                    bool isTargetTrack = HwaAssetManager.SetActiveTrack(nowSongId, uiTitle);

                    if (isTargetTrack && __instance.gameData != null)
                    {
                        var meta = HwaAssetManager.CurrentMeta ?? new HwaAssetManager.HwaMetaInfo();
                        var customCover = HwaAssetManager.LoadCoverSprite();
                        int targetStar = meta.GetLevelForDifficulty((int)__instance.gameData.nowDifficulty);

                        HwaAssetManager.ApplyMetaToGameData(__instance.gameData, customCover, targetStar);

                        ModLog.Verbose($"[GameData 사전 주입] 제목: '{meta.Title}', 아티스트: '{meta.Artist}', 매퍼: '{meta.ChartAuthor}', BGA: '{meta.BgaAuthor}', 앨범: '{meta.Album}', 난이도: ★{targetStar}, 커버: '{(customCover != null ? customCover.name : "null")}' 주입 완료");
                    }

                    ModLog.Verbose("==================================================");
                    ModLog.Verbose($"[곡 플레이 선택 확정!] 사용자가 플레이를 시작합니다!");
                    ModLog.Verbose($"  - 선택된 인덱스: {__instance.NowIndex}");
                    ModLog.Verbose($"  - 선택된 곡 ID:  '{nowSongId}'");
                    ModLog.Verbose($"  - 커스텀 에셋 사본 여부: {isTargetTrack}");
                    ModLog.Verbose("==================================================");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[곡 플레이 선택 확정] GotoSceneData 처리 중 예외: {ex.Message}");
                }
            }
        }

        private static int GetTargetStarCount(DiffCtrl diffCtrl)
        {
            if (diffCtrl == null) return 8;
            var meta = HwaAssetManager.CurrentMeta ?? new HwaAssetManager.HwaMetaInfo();
            return meta.GetLevelForDifficulty((int)diffCtrl.myDifficulty);
        }

        private static void ApplyCustomDiffDisplay(DiffCtrl diffCtrl, bool logUpdate = false)
        {
            try
            {
                if (diffCtrl == null || !HwaAssetManager.IsTargetTrackActive) return;

                int targetStar = GetTargetStarCount(diffCtrl);

                if (diffCtrl.starsCtrl != null)
                {
                    HwaAssetManager.SafeGenerateStarImages(diffCtrl.starsCtrl, targetStar);
                }

                if (diffCtrl.Diff_Text != null)
                {
                    string levelStr = targetStar.ToString();
                    if (!string.Equals(diffCtrl.Diff_Text.text, levelStr, StringComparison.Ordinal))
                    {
                        diffCtrl.Diff_Text.text = levelStr;
                    }
                }

                if (diffCtrl.gameData != null)
                {
                    string levelStr = targetStar.ToString();
                    Il2CppReflectionHelper.SetValueResilient(diffCtrl.gameData, () => diffCtrl.gameData.NowTrackLevel = levelStr, "NowTrackLevel", MemberFallbacks.GameData.Level, levelStr);
                }

                if (logUpdate)
                {
                    ModLog.Debug($"[DiffCtrl UI 난이도 갱신] 난이도: {diffCtrl.myDifficulty} -> ★{targetStar}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DiffCtrl UI 난이도 갱신] 처리 중 예외: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(DiffCtrl), nameof(DiffCtrl.UpdateStarDisplay))]
        public static class DiffCtrl_UpdateStarDisplay_Patch
        {
            public static void Postfix(DiffCtrl __instance)
            {
                ApplyCustomDiffDisplay(__instance, logUpdate: false);
            }
        }

        [HarmonyPatch(typeof(DiffCtrl), nameof(DiffCtrl.SelectNextDifficulty))]
        public static class DiffCtrl_SelectNextDifficulty_Patch
        {
            public static void Postfix(DiffCtrl __instance)
            {
                try
                {
                    if (__instance == null) return;
                    ApplyCustomDiffDisplay(__instance, logUpdate: true);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[난이도 변경] SelectNextDifficulty 처리 중 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(DiffCtrl), nameof(DiffCtrl.SelectPreviousDifficulty))]
        public static class DiffCtrl_SelectPreviousDifficulty_Patch
        {
            public static void Postfix(DiffCtrl __instance)
            {
                try
                {
                    if (__instance == null) return;
                    ApplyCustomDiffDisplay(__instance, logUpdate: true);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[난이도 변경] SelectPreviousDifficulty 처리 중 예외: {ex.Message}");
                }
            }
        }
    }
}
