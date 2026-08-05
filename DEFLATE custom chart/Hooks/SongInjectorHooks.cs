using System;
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
    /// 곡 목록에서 특정 트랙(WindShifter)을 찾아 CustomTrackWrapper로 캐스트/복사한 뒤
    /// 새 MainTrackListBlock 인스턴스로 재주입하여 곡 리스트에 사본을 추가합니다.
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

                // 이미 이번 tracks 배열에 사본("테스트 곡")이 존재하면 중복 생성하지 않고 상태만 재동기화한다.
                // (MainTrackList.Start가 씬 재진입 등으로 여러 번 호출되는 경우를 대비)
                foreach (var existing in __instance.tracks)
                {
                    if (existing != null && existing.TrackTitle == "테스트 곡")
                    {
                        HwaAssetManager.TargetTrackID = existing.uniqueID;
                        HwaAssetManager.TargetAudioKey = existing.audioClip_Key;
                        HwaAssetManager.TargetVideoKey = existing.videoClip_Key;
                        MelonLogger.Msg($"[곡 목록 주입] 기존 사본을 재사용합니다 (ID: {existing.uniqueID}). 재생성을 건너뜁니다.");
                        return;
                    }
                }

                MainTrackListBlock source = null;
                foreach (var track in __instance.tracks)
                {
                    if (track == null) continue;

                    bool titleMatch = !string.IsNullOrEmpty(track.TrackTitle) &&
                        track.TrackTitle.IndexOf(TargetKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool idMatch = !string.IsNullOrEmpty(track.uniqueID) &&
                        track.uniqueID.IndexOf(TargetKeyword, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (titleMatch || idMatch)
                    {
                        source = track;
                        break;
                    }
                }

                if (source == null)
                {
                    MelonLogger.Warning($"[곡 목록 주입] '{TargetKeyword}' 트랙을 찾지 못했습니다.");
                    return;
                }

                // 1) Il2Cpp 원본 트랙을 C# 래퍼로 캐스트/캡슐화
                var wrapper = new CustomTrackWrapper(source);

                // 2) 원본 GameObject를 복제하여 새 네이티브 MainTrackListBlock 인스턴스 생성
                var cloneGO = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
                cloneGO.name = source.gameObject.name + "_Copy";
                var clone = cloneGO.GetComponent<MainTrackListBlock>();
                if (clone == null)
                {
                    MelonLogger.Error("[곡 목록 주입] 복제된 오브젝트에서 MainTrackListBlock 컴포넌트를 찾을 수 없습니다.");
                    UnityEngine.Object.Destroy(cloneGO);
                    return;
                }

                // 3) 래퍼에 담아둔 원본 데이터를 새 인스턴스로 재적용(Apply) 후 고유 ID 재발급
                wrapper.ApplyTo(clone);
                clone.RegenerateID();

                // 사본은 "테스트 곡"이라는 이름으로 곡 목록에 표시 (원본과 시각적으로 구분)
                clone.TrackTitle = "테스트 곡";

                // 4) tracks 배열을 1칸 확장하여 복제본을 곡 리스트 끝에 주입
                var oldTracks = __instance.tracks;
                var newTracks = new Il2CppReferenceArray<MainTrackListBlock>(oldTracks.Length + 1);
                for (int i = 0; i < oldTracks.Length; i++)
                {
                    newTracks[i] = oldTracks[i];
                }
                newTracks[oldTracks.Length] = clone;
                __instance.tracks = newTracks;

                // 5) hwa/ 커스텀 png·bga·bgm 에셋이 이 사본에만 적용되도록 타겟 정보 등록 (원본 WindShifter에는 주입되지 않음)
                HwaAssetManager.TargetTrackID = clone.uniqueID;
                HwaAssetManager.TargetAudioKey = clone.audioClip_Key;
                HwaAssetManager.TargetVideoKey = clone.videoClip_Key;
                HwaAssetManager.IsTargetTrackActive = false;

                MelonLogger.Msg($"[곡 목록 주입 완료] 원본 '{source.TrackTitle}' (ID: {source.uniqueID}) → 사본 '{clone.TrackTitle}' (ID: {clone.uniqueID}) | 총 {__instance.tracks.Length}곡 | 커스텀 에셋 타겟 등록 완료");
            }
        }
    }
}
