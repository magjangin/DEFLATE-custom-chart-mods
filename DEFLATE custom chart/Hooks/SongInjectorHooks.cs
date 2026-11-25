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

        /// <summary>사본 GameObject 이름에 붙는 표식. 원곡 블록과 사본 블록을 구분하는 데 쓴다.</summary>
        internal const string CloneNameMarker = "_Custom_";

        /// <summary>이 모드가 복제해 주입한 커스텀 곡 블록인지 (GameObject 이름 표식 기준).</summary>
        internal static bool IsCustomBlock(MainTrackListBlock block)
        {
            return block != null && block.gameObject != null && block.gameObject.name.Contains(CloneNameMarker);
        }

        [HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.Start))]
        public static class MainTrackList_Start_InjectCopy_Patch
        {
            [HarmonyPriority(Priority.High)]
            public static void Postfix(MainTrackList __instance)
            {
                try
                {
                    HwaAssetManager.EnterSongSelect();
                    InGameRhythmHooks.RestoreHostKoreography();

                    if (__instance == null || __instance.tracks == null || __instance.tracks.Length == 0) return;

                    if (!CustomSongLibrary.HasSongs)
                    {
                        MelonLogger.Warning("[곡 목록 주입] hwa/ 폴더에서 스캔된 커스텀 곡이 없어 주입을 건너뜁니다.");
                        return;
                    }

                    // 0) 원곡과 제목이 같은 커스텀 곡을 찾아 제목 폴백 판정에서 뺀다(원곡에 커스텀 에셋이 붙는 것 방지).
                    RefreshOriginalTitleCollisions(__instance.tracks);

                    // 1) 이미 이번 tracks 배열에 사본이 있는 곡은 재생성하지 않고 상태만 재동기화한다.
                    var pending = new List<CustomSongEntry>(CustomSongLibrary.Entries);

                    foreach (var existing in __instance.tracks)
                    {
                        if (existing == null) continue;

                        // 이 모드가 만든 사본만 재사용 대상이다. 원곡은 제목+앨범이 커스텀 곡과 같아도 건너뛴다.
                        if (!existing.gameObject.name.Contains(CloneNameMarker)) continue;

                        var existingWrapper = new CustomTrackWrapper(existing);
                        var matched = CustomSongLibrary.FindByTitleAndAlbum(existingWrapper.Title, existingWrapper.AlbumName);
                        if (matched == null || !pending.Contains(matched)) continue;

                        CustomSongLibrary.RegisterInjectedTrack(matched, existingWrapper.UniqueID, existingWrapper.AudioKey, existingWrapper.VideoKey);
                        pending.Remove(matched);

                        ModLog.Verbose($"[곡 목록 주입] 기존 사본 재사용: '{matched.Meta.Title}' (앨범: '{matched.Meta.Album}', ID: {existingWrapper.UniqueID})");
                    }

                    if (pending.Count == 0)
                    {
                        ModLog.Verbose($"[곡 목록 주입] 커스텀 곡 {CustomSongLibrary.Entries.Count}곡이 모두 이미 주입되어 있습니다.");
                        return;
                    }

                    // 2) 복제 원본이 될 트랙 탐색 (1순위: WindShifter, 2순위: 첫 번째 유효 원곡 블록 폴백)
                    //    탐색 중에는 판정에 필요한 멤버(제목·ID·차트 키)만 읽고, 고른 블록 하나만 전체 래핑한다.
                    //    (예전에는 원곡마다 CustomTrackWrapper로 멤버 18개를 읽어, 곡 목록이 만들어질 때마다 수백~수천 번의 IL2CPP 읽기가 생겼다)
                    MainTrackListBlock sourceBlock = null;
                    MainTrackListBlock firstValidBlock = null;

                    foreach (var track in __instance.tracks)
                    {
                        if (track == null || track.gameObject == null) continue;
                        if (track.gameObject.name.Contains(CloneNameMarker)) continue;

                        var block = track;
                        if (firstValidBlock == null)
                        {
                            string nmKey = Il2CppReflectionHelper.GetValueResilient(block, () => block.NM_TrackKore_Key, "NM_TrackKore_Key", MemberFallbacks.TrackBlock.NmKoreKey, string.Empty);
                            if (!string.IsNullOrEmpty(nmKey)) firstValidBlock = track;
                        }

                        string title = Il2CppReflectionHelper.GetValueResilient(block, () => block.TrackTitle, "TrackTitle", MemberFallbacks.TrackBlock.Title, string.Empty);
                        string uniqueId = Il2CppReflectionHelper.GetValueResilient(block, () => block.uniqueID, "uniqueID", MemberFallbacks.TrackBlock.UniqueId, string.Empty);
                        bool titleMatch = !string.IsNullOrEmpty(title) && title.IndexOf(TargetKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
                        bool idMatch = !string.IsNullOrEmpty(uniqueId) && uniqueId.IndexOf(TargetKeyword, StringComparison.OrdinalIgnoreCase) >= 0;

                        if (titleMatch || idMatch)
                        {
                            sourceBlock = track;
                            break;
                        }
                    }

                    bool usedFallback = false;
                    if (sourceBlock == null && firstValidBlock != null)
                    {
                        sourceBlock = firstValidBlock;
                        usedFallback = true;
                    }

                    CustomTrackWrapper sourceWrapper = sourceBlock != null ? new CustomTrackWrapper(sourceBlock) : null;
                    if (usedFallback && sourceWrapper != null)
                    {
                        MelonLogger.Warning($"[곡 목록 주입] '{TargetKeyword}' 트랙을 찾지 못해 첫 번째 유효 원곡 '{sourceWrapper.Title}'(ID: {sourceWrapper.UniqueID})을 복제 템플릿으로 폴백 사용합니다.");
                    }

                    if (sourceBlock == null || sourceWrapper == null)
                    {
                        MelonLogger.Warning("[곡 목록 주입] 복제 템플릿으로 사용할 수 있는 원본 트랙을 찾지 못했습니다.");
                        return;
                    }

                    // 3) 남은 커스텀 곡마다 사본 생성 (커버 PNG 디코딩 포함 — 곡이 많을 때 지연의 대부분)
                    var createdBlocks = new List<MainTrackListBlock>();
                    var injectWatch = System.Diagnostics.Stopwatch.StartNew();

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

                    MelonLogger.Msg($"[곡 목록 주입 완료] 원본 '{sourceWrapper.Title}' (ID: {sourceWrapper.UniqueID}) 기반으로 커스텀 곡 {createdBlocks.Count}곡 주입 | 총 {__instance.tracks.Length}곡 ({injectWatch.ElapsedMilliseconds}ms)");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[곡 목록 주입] MainTrackList.Start 주입 처리 중 예외: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        // 같은 충돌 경고를 곡 목록이 만들어질 때마다(시작 직후 2회 등) 반복하지 않는다.
        private static readonly HashSet<string> _warnedTitleCollisions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 원곡 블록 제목 중 커스텀 곡 제목과 같은 것을 모아 <see cref="CustomSongLibrary.SetOriginalTitleCollisions"/>에 넘깁니다.
        /// 곡 목록 생성 시 1회, 원곡 수(약 116)만큼 제목 문자열을 읽습니다.
        /// </summary>
        private static void RefreshOriginalTitleCollisions(Il2CppReferenceArray<MainTrackListBlock> tracks)
        {
            var collisions = new List<string>();
            try
            {
                foreach (var track in tracks)
                {
                    if (track == null || IsCustomBlock(track)) continue;
                    var block = track;
                    string title = Il2CppReflectionHelper.GetValueResilient(block, () => block.TrackTitle, "TrackTitle", MemberFallbacks.TrackBlock.Title, string.Empty);
                    if (!CustomSongLibrary.HasCustomTitle(title)) continue;

                    collisions.Add(title);
                    if (_warnedTitleCollisions.Add(title))
                    {
                        MelonLogger.Warning($"[곡 목록 주입] 커스텀 곡 '{title}'이 원곡 제목과 같습니다. 이 곡은 제목이 아니라 ID로만 판정합니다"
                            + "(원곡을 골랐을 때 커스텀 BGM·차트가 붙지 않게). 구분하기 쉽게 info.txt의 title을 바꾸는 것을 권장합니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[곡 목록 주입] 원곡 제목 충돌 검사 중 예외(무시하고 계속): {ex.Message}");
            }
            CustomSongLibrary.SetOriginalTitleCollisions(collisions);
        }

        /// <summary>원본 블록을 복제해 커스텀 곡 하나의 메타데이터/커버를 입힌 새 MainTrackListBlock을 만듭니다.</summary>
        private static MainTrackListBlock CreateTrackBlock(MainTrackListBlock sourceBlock, CustomTrackWrapper sourceWrapper, CustomSongEntry entry)
        {
            GameObject cloneGO = null;
            try
            {
                // 1) 원본 GameObject 복제
                cloneGO = UnityEngine.Object.Instantiate(sourceBlock.gameObject, sourceBlock.transform.parent);
                cloneGO.name = $"{sourceBlock.gameObject.name}{CloneNameMarker}{entry.FolderName}";

                var cloneBlock = Il2CppReflectionHelper.SafeCast<MainTrackListBlock>(cloneGO.GetComponent<MainTrackListBlock>());
                if (cloneBlock == null)
                {
                    MelonLogger.Error($"[곡 목록 주입] '{entry.Meta.Title}' 사본에서 MainTrackListBlock 컴포넌트를 찾거나 캐스팅할 수 없습니다.");
                    UnityEngine.Object.Destroy(cloneGO);
                    return null;
                }

                // 2) 내성 래퍼를 통한 원본 데이터 재적용 (ApplyTo)
                sourceWrapper.ApplyTo(cloneBlock);

                // 3) RegenerateID 호출 시도 (메서드명 변경 시 안전 가드 + 상속 체인 백업 리플렉션)
                Il2CppReflectionHelper.InvokeMethodResilient(
                    cloneBlock,
                    () => cloneBlock.RegenerateID(),
                    "RegenerateID",
                    MemberFallbacks.TrackBlock.RegenerateIdMethod);

                // 4) 사본 래퍼 갱신 및 커스텀 info.txt 메타데이터 / PNG 커버 자켓 주입
                var cloneWrapper = new CustomTrackWrapper(cloneBlock);

                // ID 재생성에 실패해 원본과 같은 ID가 남으면, 고유 Fallback ID를 직접 생성하여 부여 (업데이트 내성 강화)
                if (string.IsNullOrEmpty(cloneWrapper.UniqueID) ||
                    string.Equals(cloneWrapper.UniqueID, sourceWrapper.UniqueID, StringComparison.OrdinalIgnoreCase))
                {
                    string fallbackId = "CUSTOM_" + Guid.NewGuid().ToString("N");
                    MelonLogger.Warning($"[곡 목록 주입] RegenerateID 미작동 감지. 폴백 고유 ID를 직접 할당합니다: '{fallbackId}'");
                    cloneWrapper.UniqueID = fallbackId;
                    Il2CppReflectionHelper.SetValueResilient(
                        cloneBlock,
                        () => cloneBlock.uniqueID = fallbackId,
                        "uniqueID",
                        MemberFallbacks.TrackBlock.UniqueId,
                        fallbackId);
                    cloneWrapper.UniqueID = Il2CppReflectionHelper.GetValueResilient(
                        cloneBlock,
                        () => cloneBlock.uniqueID,
                        "uniqueID",
                        MemberFallbacks.TrackBlock.UniqueId,
                        fallbackId);
                }

                if (string.IsNullOrEmpty(cloneWrapper.UniqueID) ||
                    string.Equals(cloneWrapper.UniqueID, sourceWrapper.UniqueID, StringComparison.OrdinalIgnoreCase))
                {
                    MelonLogger.Error($"[곡 목록 주입] '{entry.Meta.Title}' 사본의 ID 확보 실패로 주입을 취소합니다.");
                    UnityEngine.Object.Destroy(cloneGO);
                    return null;
                }

                var meta = entry.Meta;
                var customCover = entry.LoadCoverSprite();

                cloneWrapper.ApplySongMetadata(meta, customCover);
                // 숙주 곡의 온라인 코멘트 조회(DiscID) 차단
                cloneWrapper.DiscID = string.Empty;
                // 커스텀 BGA(.mp4)가 없는 곡은 숙주 곡의 PV 비디오+오디오가 재생되지 않도록 VideoKey를 비운다.
                if (string.IsNullOrEmpty(entry.BgaFilePath))
                {
                    cloneWrapper.VideoKey = string.Empty;
                }
                cloneWrapper.ApplyTo(cloneBlock);

                // 5) 커스텀 에셋 타겟 정보 및 O(1) 조회 인덱스 등록
                CustomSongLibrary.RegisterInjectedTrack(entry, cloneWrapper.UniqueID, cloneWrapper.AudioKey, cloneWrapper.VideoKey);

                ModLog.Verbose($"  [사본 생성] 앨범 '{meta.Album}' / '{meta.Title}' (ID: {cloneWrapper.UniqueID}) ← 폴더 '{entry.FolderName}'");
                return cloneBlock;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[곡 목록 주입] '{entry.Meta.Title}' 사본 생성 중 예외: {ex.Message}");
                // 반쯤 만들어진 사본이 곡 목록 UI에 남지 않도록 지운다.
                if (cloneGO != null) UnityEngine.Object.Destroy(cloneGO);
                return null;
            }
        }
    }
}
