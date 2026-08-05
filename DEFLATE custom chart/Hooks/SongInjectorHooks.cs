using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppdizzylab.castor;
using DEFLATE_custom_chart.Core;
using DEFLATE_custom_chart.Core.Wrapper;

namespace DEFLATE_custom_chart.Hooks
{
    /// <summary>
    /// 곡 목록에서 원본 트랙(WindShifter)을 찾아 CustomTrackWrapper로 캐스트/복사한 뒤,
    /// hwa/ 라이브러리에 스캔된 커스텀 곡(앨범/곡 폴더) 수만큼 새 MainTrackListBlock 사본을 만들어 곡 리스트에 주입합니다.
    /// 패치/업데이트 시 프로퍼티/메서드 변형에 대비하여 래퍼와 내성 리플렉션을 통해 주입을 수행합니다.
    /// </summary>
    public static class SongInjectorHooks
    {
        private const string TargetKeyword = "WindShifter";

        [HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.Start))]
        public static class MainTrackList_Start_InjectCopy_Patch
        {
            public static void Postfix(MainTrackList __instance)
            {
                if (__instance == null || __instance.tracks == null || __instance.tracks.Length == 0) return;

                if (!CustomSongLibrary.HasSongs)
                {
                    MelonLogger.Warning("[곡 목록 주입] hwa/ 폴더에서 스캔된 커스텀 곡이 없어 주입을 건너뜁니다.");
                    return;
                }

                // 1) 이미 이번 tracks 배열에 사본이 있는 곡은 재생성하지 않고 상태만 재동기화한다.
                var pending = new List<CustomSongEntry>(CustomSongLibrary.Entries);

                foreach (var existing in __instance.tracks)
                {
                    if (existing == null) continue;

                    var existingWrapper = new CustomTrackWrapper(existing);
                    var matched = CustomSongLibrary.FindByTitleAndAlbum(existingWrapper.Title, existingWrapper.AlbumName);
                    if (matched == null || !pending.Contains(matched)) continue;

                    matched.InjectedTrackID = existingWrapper.UniqueID;
                    matched.AudioKey = existingWrapper.AudioKey;
                    matched.VideoKey = existingWrapper.VideoKey;
                    pending.Remove(matched);

                    MelonLogger.Msg($"[곡 목록 주입] 기존 사본 재사용: '{matched.Meta.Title}' (앨범: '{matched.Meta.Album}', ID: {existingWrapper.UniqueID})");
                }

                if (pending.Count == 0)
                {
                    MelonLogger.Msg($"[곡 목록 주입] 커스텀 곡 {CustomSongLibrary.Entries.Count}곡이 모두 이미 주입되어 있습니다.");
                    return;
                }

                // 2) 복제 원본이 될 트랙 탐색
                MainTrackListBlock sourceBlock = null;
                CustomTrackWrapper sourceWrapper = null;

                foreach (var track in __instance.tracks)
                {
                    if (track == null) continue;

                    var tempWrapper = new CustomTrackWrapper(track);
                    bool titleMatch = !string.IsNullOrEmpty(tempWrapper.Title) &&
                        tempWrapper.Title.IndexOf(TargetKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool idMatch = !string.IsNullOrEmpty(tempWrapper.UniqueID) &&
                        tempWrapper.UniqueID.IndexOf(TargetKeyword, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (titleMatch || idMatch)
                    {
                        sourceBlock = track;
                        sourceWrapper = tempWrapper;
                        break;
                    }
                }

                if (sourceBlock == null || sourceWrapper == null)
                {
                    MelonLogger.Warning($"[곡 목록 주입] '{TargetKeyword}' 트랙을 찾지 못했습니다.");
                    return;
                }

                // 3) 남은 커스텀 곡마다 사본 생성
                var createdBlocks = new List<MainTrackListBlock>();

                foreach (var entry in pending)
                {
                    var cloneBlock = CreateTrackBlock(sourceBlock, sourceWrapper, entry);
                    if (cloneBlock != null) createdBlocks.Add(cloneBlock);
                }

                if (createdBlocks.Count == 0)
                {
                    MelonLogger.Error("[곡 목록 주입] 사본을 하나도 만들지 못했습니다.");
                    return;
                }

                // 4) tracks 배열 확장 주입 (한 번에)
                var oldTracks = __instance.tracks;
                var newTracks = new Il2CppReferenceArray<MainTrackListBlock>(oldTracks.Length + createdBlocks.Count);
                for (int i = 0; i < oldTracks.Length; i++)
                {
                    newTracks[i] = oldTracks[i];
                }
                for (int i = 0; i < createdBlocks.Count; i++)
                {
                    newTracks[oldTracks.Length + i] = createdBlocks[i];
                }
                __instance.tracks = newTracks;

                // 5) 곡 목록 진입 시점에는 아직 아무 커스텀 곡도 선택되지 않은 상태로 둔다.
                HwaAssetManager.IsTargetTrackActive = false;

                MelonLogger.Msg($"[곡 목록 주입 완료] 원본 '{sourceWrapper.Title}' (ID: {sourceWrapper.UniqueID}) 기반으로 커스텀 곡 {createdBlocks.Count}곡 주입 | 총 {__instance.tracks.Length}곡");
            }
        }

        /// <summary>원본 블록을 복제해 커스텀 곡 하나의 메타데이터/커버를 입힌 새 MainTrackListBlock을 만듭니다.</summary>
        private static MainTrackListBlock CreateTrackBlock(MainTrackListBlock sourceBlock, CustomTrackWrapper sourceWrapper, CustomSongEntry entry)
        {
            try
            {
                // 1) 원본 GameObject 복제
                var cloneGO = UnityEngine.Object.Instantiate(sourceBlock.gameObject, sourceBlock.transform.parent);
                cloneGO.name = $"{sourceBlock.gameObject.name}_Custom_{entry.FolderName}";

                var cloneBlock = Il2CppReflectionHelper.SafeCast<MainTrackListBlock>(cloneGO.GetComponent<MainTrackListBlock>());
                if (cloneBlock == null)
                {
                    MelonLogger.Error($"[곡 목록 주입] '{entry.Meta.Title}' 사본에서 MainTrackListBlock 컴포넌트를 찾거나 캐스팅할 수 없습니다.");
                    UnityEngine.Object.Destroy(cloneGO);
                    return null;
                }

                // 2) 내성 래퍼를 통한 원본 데이터 재적용 (ApplyTo)
                sourceWrapper.ApplyTo(cloneBlock);

                // 3) RegenerateID 호출 시도 (메서드명 변경 시 안전 가드)
                try
                {
                    cloneBlock.RegenerateID();
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[곡 목록 주입] RegenerateID() 직접 호출 실패 ({ex.Message}), 리플렉션 호출을 시도합니다.");
                    var method = cloneBlock.GetType().GetMethod("RegenerateID", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method?.Invoke(cloneBlock, null);
                }

                // 4) 사본 래퍼 갱신 및 커스텀 info.txt 메타데이터 / PNG 커버 자켓 주입
                var cloneWrapper = new CustomTrackWrapper(cloneBlock);
                var meta = entry.Meta;
                var customCover = entry.LoadCoverSprite();

                cloneWrapper.Title = meta.Title;
                cloneWrapper.Artist = meta.Artist;
                cloneWrapper.AlbumName = meta.Album;
                if (customCover != null) cloneWrapper.CoverSprite = customCover;
                cloneWrapper.ApplyTo(cloneBlock);

                // MainTrackListBlock 직접 속성에도 확실히 반영
                cloneBlock.TrackTitle = meta.Title;
                cloneBlock.TrackAuthor = meta.Artist;
                cloneBlock.TrackAlbum = meta.Album;
                if (customCover != null) cloneBlock.TrackCover = customCover;
                cloneBlock.EZ_Star = meta.EasyLevel;
                cloneBlock.NM_Star = meta.NormalLevel;
                cloneBlock.HD_Star = meta.HardLevel;
                cloneBlock.Description = $"BGA: {meta.BgaAuthor} | Chart: {meta.ChartMaker} | Easy:{meta.EasyLevel} Normal:{meta.NormalLevel} Hard:{meta.HardLevel}";

                // 5) 커스텀 에셋 타겟 정보 등록
                entry.InjectedTrackID = cloneWrapper.UniqueID;
                entry.AudioKey = cloneWrapper.AudioKey;
                entry.VideoKey = cloneWrapper.VideoKey;

                MelonLogger.Msg($"  [사본 생성] 앨범 '{meta.Album}' / '{meta.Title}' (ID: {cloneWrapper.UniqueID}) ← 폴더 '{entry.FolderName}'");
                return cloneBlock;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[곡 목록 주입] '{entry.Meta.Title}' 사본 생성 중 예외: {ex.Message}");
                return null;
            }
        }
    }
}
