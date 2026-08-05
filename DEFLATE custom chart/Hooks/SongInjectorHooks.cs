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

                // 이미 이번 tracks 배열에 사본("테스트 곡")이 존재하면 중복 생성하지 않고 상태만 재동기화한다.
                foreach (var existing in __instance.tracks)
                {
                    if (existing == null) continue;
                    var existingWrapper = new CustomTrackWrapper(existing);
                    if (existingWrapper.Title == "테스트 곡")
                    {
                        HwaAssetManager.TargetTrackID = existingWrapper.UniqueID;
                        HwaAssetManager.TargetAudioKey = existingWrapper.AudioKey;
                        HwaAssetManager.TargetVideoKey = existingWrapper.VideoKey;
                        MelonLogger.Msg($"[곡 목록 주입] 기존 사본을 재사용합니다 (ID: {existingWrapper.UniqueID}). 재생성을 건너뜁니다.");
                        return;
                    }
                }

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

                // 1) 원본 GameObject 복제
                var cloneGO = UnityEngine.Object.Instantiate(sourceBlock.gameObject, sourceBlock.transform.parent);
                cloneGO.name = sourceBlock.gameObject.name + "_Copy";
                
                var cloneBlock = Il2CppReflectionHelper.SafeCast<MainTrackListBlock>(cloneGO.GetComponent<MainTrackListBlock>());
                if (cloneBlock == null)
                {
                    MelonLogger.Error("[곡 목록 주입] 복제된 오브젝트에서 MainTrackListBlock 컴포넌트를 찾거나 캐스팅할 수 없습니다.");
                    UnityEngine.Object.Destroy(cloneGO);
                    return;
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

                // 사본 래퍼 갱신 및 제목 설정 ("테스트 곡")
                var cloneWrapper = new CustomTrackWrapper(cloneBlock);
                cloneWrapper.Title = "테스트 곡";
                cloneWrapper.ApplyTo(cloneBlock);

                // 4) tracks 배열 확장 주입
                var oldTracks = __instance.tracks;
                var newTracks = new Il2CppReferenceArray<MainTrackListBlock>(oldTracks.Length + 1);
                for (int i = 0; i < oldTracks.Length; i++)
                {
                    newTracks[i] = oldTracks[i];
                }
                newTracks[oldTracks.Length] = cloneBlock;
                __instance.tracks = newTracks;

                // 5) 커스텀 에셋 타겟 정보 등록
                HwaAssetManager.TargetTrackID = cloneWrapper.UniqueID;
                HwaAssetManager.TargetAudioKey = cloneWrapper.AudioKey;
                HwaAssetManager.TargetVideoKey = cloneWrapper.VideoKey;
                HwaAssetManager.IsTargetTrackActive = false;

                MelonLogger.Msg($"[곡 목록 주입 완료] 원본 '{sourceWrapper.Title}' (ID: {sourceWrapper.UniqueID}) → 사본 '{cloneWrapper.Title}' (ID: {cloneWrapper.UniqueID}) | 총 {__instance.tracks.Length}곡 | 커스텀 에셋 타겟 등록 완료");
            }
        }
    }
}
