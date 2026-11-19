using System;
using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using UnityEngine.EventSystems;
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
                    HwaAssetManager.IsInSongSelectContext = true;

                    if (__instance == null || __instance.tracks == null) return;

                    // 117+곡 전체 카탈로그 덤프는 씬 진입마다 ~1,000줄 I/O 스팸 및 117개 래퍼 할당 병목을 유발하므로
                    // 요약 + 주입된 커스텀 곡만 간결하게 기록한다.
                    int customCount = 0;
                    for (int i = 0; i < __instance.tracks.Length; i++)
                    {
                        var track = __instance.tracks[i];
                        if (track == null || track.gameObject == null) continue;
                        if (!track.gameObject.name.Contains("_Custom_")) continue;

                        customCount++;
                        try
                        {
                            var wrapped = new CustomTrackWrapper(track);
                            MelonLogger.Msg($"  [커스텀 트랙 #{i + 1}] '{wrapped.Title}' / {wrapped.Artist} (앨범: '{wrapped.AlbumName}', ID: {wrapped.UniqueID}, ★{wrapped.EZ_Star}/{wrapped.NM_Star}/{wrapped.HD_Star})");
                        }
                        catch (Exception trackEx)
                        {
                            MelonLogger.Warning($"[곡 목록 스캔] 커스텀 트랙 #{i + 1} 로깅 중 예외: {trackEx.Message}");
                        }
                    }

                    MelonLogger.Msg($"[곡 목록 카탈로그] 총 {__instance.tracks.Length}곡 (커스텀 {customCount}곡)");
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
            public static void Postfix(MainTrackList __instance, int trackIndex)
            {
                try
                {
                    if (__instance == null || __instance.tracks == null) return;
                    if (trackIndex >= 0 && trackIndex < __instance.tracks.Length)
                    {
                        var track = __instance.tracks[trackIndex];
                        if (track != null)
                        {
                            string coverName = track.TrackCover != null ? track.TrackCover.name : "null";
                            MelonLogger.Msg($"[곡 목록 커서 이동] 인덱스: {trackIndex} | 제목: '{track.TrackTitle}' (ID: {track.uniqueID}) | 자켓: '{coverName}'");
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
                    MelonLogger.Msg($"[곡 블록 선택 감지] 인덱스: {__instance.IndexInTrackList} | 제목: '{titleText}' (ID: {uniqueId}) | UI 자켓: '{coverName}' | 커스텀 에셋 사본 여부: {isTargetTrack}");
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
            public static void Postfix(TrackListBlockCtrl __instance, PointerEventData eventData)
            {
                try
                {
                    if (__instance == null) return;
                    MelonLogger.Msg($"[곡 클릭 감지] 인덱스: {__instance.IndexInTrackList} | 제목: '{__instance.UI_TrackTitle?.text}' (ID: {__instance.UniqueID})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[곡 클릭 감지] OnPointerClick 처리 중 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.GotoSceneData))]
        public static class MainTrackList_GotoSceneData_Patch
        {
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

                        MelonLogger.Msg($"[GameData 사전 주입] 제목: '{meta.Title}', 아티스트: '{meta.Artist}', 매퍼: '{meta.ChartAuthor}', BGA: '{meta.BgaAuthor}', 앨범: '{meta.Album}', 난이도: ★{targetStar}, 커버: '{(customCover != null ? customCover.name : "null")}' 주입 완료");
                    }

                    MelonLogger.Msg("==================================================");
                    MelonLogger.Msg($"[곡 플레이 선택 확정!] 사용자가 플레이를 시작합니다!");
                    MelonLogger.Msg($"  - 선택된 인덱스: {__instance.NowIndex}");
                    MelonLogger.Msg($"  - 선택된 곡 ID:  '{nowSongId}'");
                    MelonLogger.Msg($"  - 커스텀 에셋 사본 여부: {isTargetTrack}");
                    MelonLogger.Msg("==================================================");
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
                    MelonLogger.Msg($"[DiffCtrl UI 난이도 갱신] 난이도: {diffCtrl.myDifficulty} -> ★{targetStar}");
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
