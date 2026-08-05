using System;
using System.Text;
using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using UnityEngine.EventSystems;
using DEFLATE_custom_chart.Core;

namespace DEFLATE_custom_chart.Hooks
{
    public static class SongListHooks
    {
        [HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.Start))]
        public static class MainTrackList_Start_Patch
        {
            public static void Postfix(MainTrackList __instance)
            {
                // 곡 목록 씬으로 (재)진입 시 프리뷰 컨텍스트를 다시 활성화한다.
                HwaAssetManager.IsInSongSelectContext = true;

                if (__instance == null || __instance.tracks == null) return;

                MelonLogger.Msg("--------------------------------------------------");
                MelonLogger.Msg($"[곡 목록 카탈로그 스캔 완료] 전체 수록곡: 총 {__instance.tracks.Length}곡");
                MelonLogger.Msg("--------------------------------------------------");

                for (int i = 0; i < __instance.tracks.Length; i++)
                {
                    var track = __instance.tracks[i];
                    if (track == null) continue;

                    string coverInfoStr = "없음";
                    if (track.TrackCover != null)
                    {
                        var sprite = track.TrackCover;
                        string texInfo = sprite.texture != null ? $"{sprite.texture.width}x{sprite.texture.height}" : "NoTexture";
                        coverInfoStr = $"'{sprite.name}' ({texInfo})";
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine($"[트랙 #{i + 1}] ID: {track.uniqueID} | 제목: {track.TrackTitle} | 아티스트: {track.TrackAuthor}");
                    sb.AppendLine($"  - 앨범: {track.TrackAlbum} | 디스크: {track.DiscID}");
                    sb.AppendLine($"  - 자켓 이미지: {coverInfoStr}");
                    sb.AppendLine($"  - 오디오 Key: {track.audioClip_Key}");
                    sb.AppendLine($"  - 비디오 Key: {track.videoClip_Key}");
                    sb.AppendLine($"  - Easy 차트 Key: {track.EZ_TrackKore_Key} (★{track.EZ_Star})");
                    sb.AppendLine($"  - Normal 차트 Key: {track.NM_TrackKore_Key} (★{track.NM_Star})");
                    sb.AppendLine($"  - Hard 차트 Key: {track.HD_TrackKore_Key} (★{track.HD_Star})");

                    MelonLogger.Msg(sb.ToString().TrimEnd());
                }
                MelonLogger.Msg("--------------------------------------------------");
            }
        }

        [HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.GoToTrack))]
        public static class MainTrackList_GoToTrack_Patch
        {
            public static void Postfix(MainTrackList __instance, int trackIndex)
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
        }

        [HarmonyPatch(typeof(TrackListBlockCtrl), nameof(TrackListBlockCtrl.SetSelected))]
        public static class TrackListBlockCtrl_SetSelected_Patch
        {
            public static void Postfix(TrackListBlockCtrl __instance, bool isSelected)
            {
                if (__instance == null || !isSelected) return;

                // 곡 목록에서 커서가 옮겨갈 때마다 hwa/ 커스텀 에셋(사본 전용) 활성 상태를 갱신
                // → 목록 PV 프리뷰(BGA)에서도 사본에서만 커스텀 비디오/오디오가 적용되도록 함
                bool isTargetTrack = HwaAssetManager.SetActiveTrack(__instance.UniqueID);
                if (isTargetTrack)
                {
                    HwaAssetManager.ApplyCustomCover(__instance.cover);
                }

                string coverName = __instance.cover != null && __instance.cover.sprite != null ? __instance.cover.sprite.name : "null";
                MelonLogger.Msg($"[곡 블록 선택 감지] 인덱스: {__instance.IndexInTrackList} | 제목: '{__instance.UI_TrackTitle?.text}' (ID: {__instance.UniqueID}) | UI 자켓: '{coverName}' | 커스텀 에셋 사본 여부: {isTargetTrack}");
            }
        }

        [HarmonyPatch(typeof(TrackListBlockCtrl), nameof(TrackListBlockCtrl.OnPointerClick))]
        public static class TrackListBlockCtrl_OnPointerClick_Patch
        {
            public static void Postfix(TrackListBlockCtrl __instance, PointerEventData eventData)
            {
                if (__instance == null) return;
                MelonLogger.Msg($"[곡 클릭 감지] 인덱스: {__instance.IndexInTrackList} | 제목: '{__instance.UI_TrackTitle?.text}' (ID: {__instance.UniqueID})");
            }
        }

        [HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.GotoSceneData))]
        public static class MainTrackList_GotoSceneData_Patch
        {
            public static void Prefix(MainTrackList __instance)
            {
                if (__instance == null) return;

                bool isTargetTrack = HwaAssetManager.SetActiveTrack(__instance.NowSongID);

                if (isTargetTrack && __instance.gameData != null)
                {
                    var meta = HwaAssetManager.CurrentMeta ?? new HwaAssetManager.HwaMetaInfo();
                    var customCover = HwaAssetManager.LoadCoverSprite();

                    __instance.gameData.NowTrackTitle = meta.Title;
                    __instance.gameData.NowTrackAurthor = meta.Artist;
                    __instance.gameData.NowTrackPVAuthor = meta.BgaAuthor;
                    __instance.gameData.NowTracLabel = meta.Album;

                    if (customCover != null)
                    {
                        __instance.gameData.NowTrackCover = customCover;
                    }

                    MelonLogger.Msg($"[GameData 사전 주입] 제목: '{meta.Title}', 아티스트: '{meta.Artist}', BGA: '{meta.BgaAuthor}', 앨범: '{meta.Album}', 커버: '{(customCover != null ? customCover.name : "null")}' 주입 완료");
                }

                MelonLogger.Msg("==================================================");
                MelonLogger.Msg($"[곡 플레이 선택 확정!] 사용자가 플레이를 시작합니다!");
                MelonLogger.Msg($"  - 선택된 인덱스: {__instance.NowIndex}");
                MelonLogger.Msg($"  - 선택된 곡 ID:  '{__instance.NowSongID}'");
                MelonLogger.Msg($"  - 커스텀 에셋 사본 여부: {isTargetTrack}");
                MelonLogger.Msg("==================================================");
            }
        }

        private static int GetTargetStarCount(DiffCtrl diffCtrl)
        {
            if (diffCtrl == null) return 8;
            var meta = HwaAssetManager.CurrentMeta ?? new HwaAssetManager.HwaMetaInfo();
            int diffVal = (int)diffCtrl.myDifficulty;
            if (diffVal == 0) return meta.EasyLevel;
            if (diffVal == 1) return meta.NormalLevel;
            if (diffVal == 2) return meta.HardLevel;
            return meta.NormalLevel;
        }

        private static void ApplyCustomDiffDisplay(DiffCtrl diffCtrl)
        {
            if (diffCtrl == null || !HwaAssetManager.IsTargetTrackActive) return;

            int targetStar = GetTargetStarCount(diffCtrl);

            if (diffCtrl.starsCtrl != null)
            {
                diffCtrl.starsCtrl.GenerateStarImages(targetStar);
            }

            if (diffCtrl.Diff_Text != null)
            {
                diffCtrl.Diff_Text.text = targetStar.ToString();
            }

            if (diffCtrl.gameData != null)
            {
                diffCtrl.gameData.NowTrackLevel = targetStar.ToString();
            }

            MelonLogger.Msg($"[DiffCtrl UI 난이도 갱신] 난이도: {diffCtrl.myDifficulty} -> ★{targetStar}");
        }

        [HarmonyPatch(typeof(DiffCtrl), nameof(DiffCtrl.UpdateStarDisplay))]
        public static class DiffCtrl_UpdateStarDisplay_Patch
        {
            public static void Postfix(DiffCtrl __instance)
            {
                ApplyCustomDiffDisplay(__instance);
            }
        }

        [HarmonyPatch(typeof(DiffCtrl), nameof(DiffCtrl.SelectNextDifficulty))]
        public static class DiffCtrl_SelectNextDifficulty_Patch
        {
            public static void Postfix(DiffCtrl __instance)
            {
                if (__instance == null) return;
                ApplyCustomDiffDisplay(__instance);
                MelonLogger.Msg($"[난이도 변경] 선택된 난이도: {__instance.myDifficulty}");
            }
        }

        [HarmonyPatch(typeof(DiffCtrl), nameof(DiffCtrl.SelectPreviousDifficulty))]
        public static class DiffCtrl_SelectPreviousDifficulty_Patch
        {
            public static void Postfix(DiffCtrl __instance)
            {
                if (__instance == null) return;
                ApplyCustomDiffDisplay(__instance);
                MelonLogger.Msg($"[난이도 변경] 선택된 난이도: {__instance.myDifficulty}");
            }
        }
    }
}
