using System;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using UnityEngine;
using UnityEngine.Video;
using DEFLATE_custom_chart.Core;

namespace DEFLATE_custom_chart.Hooks
{
    public static class AssetManagerHooks
    {
        [HarmonyPatch(typeof(TrackAssetManager), nameof(TrackAssetManager.LoadAudioClip))]
        public static class TrackAssetManager_LoadAudioClip_Patch
        {
            public static bool Prefix(ref string key, Action<AudioClip> onSuccess, Action onFail)
            {
                MelonLogger.Msg($"[★ 핵심 훅: TrackAssetManager.LoadAudioClip ★] BGM 오디오 클립 로드 요청! Key: '{key}'");

                // 곡 목록 프리뷰에서 사본("테스트 곡")이 선택된 상태로 자신의 오디오 Key가 요청된 경우에만
                // hwa/ 커스텀 BGM으로 오버라이드한다 (원본 WindShifter는 같은 Key를 써도 건드리지 않음).
                if (HwaAssetManager.IsTargetAudioPreview(key) && !string.IsNullOrEmpty(HwaAssetManager.BgmFilePath))
                {
                    MelonLogger.Msg($"[HwaAssetManager] ★ 곡 목록 프리뷰 Custom BGM 오버라이드 ★ Key: '{key}'");
                    MelonCoroutines.Start(HwaAssetManager.LoadCustomBgmCoroutine(null, false, onSuccess));
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(TrackAssetManager), nameof(TrackAssetManager.LoadVideoClip))]
        public static class TrackAssetManager_LoadVideoClip_Patch
        {
            public static bool Prefix(ref string key)
            {
                MelonLogger.Msg($"[에셋 매니저 BGA] VideoClip 로드 요청 Key: '{key}'");
                return true;
            }
        }

        [HarmonyPatch(typeof(TrackAssetManager), nameof(TrackAssetManager.LoadVideoURL))]
        public static class TrackAssetManager_LoadVideoURL_Patch
        {
            public static bool Prefix(ref string key)
            {
                MelonLogger.Msg($"[에셋 매니저 BGA] VideoURL 로드 요청 Key: '{key}'");
                return true;
            }
        }

        [HarmonyPatch(typeof(Bank_PV_Ctrl), nameof(Bank_PV_Ctrl.PlayPVVideo))]
        public static class Bank_PV_Ctrl_PlayPVVideo_Patch
        {
            public static bool Prefix(Bank_PV_Ctrl __instance, ref string pvKey)
            {
                MelonLogger.Msg($"[PV 메뉴 BGA 재생] pvKey: '{pvKey}'");

                if (HwaAssetManager.IsTargetTrackActive && __instance != null && __instance.videoPlayer != null)
                {
                    if (HwaAssetManager.ApplyCustomBga(__instance.videoPlayer, true))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Bank_PV_Ctrl), "LoadVideoAndAudio")]
        public static class Bank_PV_Ctrl_LoadVideoAndAudio_Patch
        {
            public static bool Prefix(Bank_PV_Ctrl __instance, ref string videoKey, ref string audioKey)
            {
                MelonLogger.Msg($"[PV 메뉴 미디어 로드] videoKey: '{videoKey}', audioKey: '{audioKey}'");

                if (HwaAssetManager.IsTargetTrackActive && __instance != null && __instance.videoPlayer != null)
                {
                    HwaAssetManager.ApplyCustomBga(__instance.videoPlayer, false);
                }
                return true;
            }
        }

        // =========================================================================
        // 전역 BGA (곡 선택, 결과 씬, 인게임 등) VideoPlayer 인터셉터
        // =========================================================================

        [HarmonyPatch(typeof(SimpleRandomVideo), nameof(SimpleRandomVideo.LoadVideoSmart))]
        public static class SimpleRandomVideo_LoadVideoSmart_Patch
        {
            public static bool Prefix(string pvKey, VideoPlayer videoPlayer, float pvOffset, bool autoPlay)
            {
                MelonLogger.Msg($"[★ SimpleRandomVideo.LoadVideoSmart 호출 ★] pvKey: '{pvKey}' | videoPlayer: '{(videoPlayer != null ? videoPlayer.name : "null")}'");

                if (HwaAssetManager.IsTargetTrackActive && videoPlayer != null && HwaAssetManager.ApplyCustomBga(videoPlayer, autoPlay))
                {
                    return false; // 원본 Addressable 비디오 로드 스킵
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SimpleRandomVideo), nameof(SimpleRandomVideo.LoadVideo))]
        public static class SimpleRandomVideo_LoadVideo_Patch
        {
            public static bool Prefix(string addressKey, VideoPlayer videoPlayer, float pvOffset, bool autoPlay)
            {
                MelonLogger.Msg($"[★ SimpleRandomVideo.LoadVideo 호출 ★] addressKey: '{addressKey}' | videoPlayer: '{(videoPlayer != null ? videoPlayer.name : "null")}'");

                if (HwaAssetManager.IsTargetTrackActive && videoPlayer != null && HwaAssetManager.ApplyCustomBga(videoPlayer, autoPlay))
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(VideoPlayer), nameof(VideoPlayer.Play))]
        public static class VideoPlayer_Play_Patch
        {
            public static void Prefix(VideoPlayer __instance)
            {
                if (__instance == null) return;
                MelonLogger.Msg($"[★ VideoPlayer.Play 감지 ★] 오브젝트: '{__instance.gameObject.name}' | 현재 source: {__instance.source} | url: '{__instance.url}'");

                if (HwaAssetManager.IsTargetTrackActive && !string.IsNullOrEmpty(HwaAssetManager.BgaFileUrl))
                {
                    if (__instance.source != VideoSource.Url || __instance.url != HwaAssetManager.BgaFileUrl)
                    {
                        __instance.source = VideoSource.Url;
                        __instance.url = HwaAssetManager.BgaFileUrl;
                        MelonLogger.Msg($"[★ Universal VideoPlayer.Play Custom BGA 강제 적용 ★] URL: '{HwaAssetManager.BgaFileUrl}'");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(VideoPlayer), nameof(VideoPlayer.Prepare))]
        public static class VideoPlayer_Prepare_Patch
        {
            public static void Prefix(VideoPlayer __instance)
            {
                if (__instance == null) return;
                MelonLogger.Msg($"[★ VideoPlayer.Prepare 감지 ★] 오브젝트: '{__instance.gameObject.name}'");

                if (HwaAssetManager.IsTargetTrackActive && !string.IsNullOrEmpty(HwaAssetManager.BgaFileUrl))
                {
                    if (__instance.source != VideoSource.Url || __instance.url != HwaAssetManager.BgaFileUrl)
                    {
                        __instance.source = VideoSource.Url;
                        __instance.url = HwaAssetManager.BgaFileUrl;
                        MelonLogger.Msg($"[★ Universal VideoPlayer.Prepare Custom BGA 강제 적용 ★] URL: '{HwaAssetManager.BgaFileUrl}'");
                    }
                }
            }
        }

        // =========================================================================
        // 곡 선택 씬 TrackList 데이터 모델 (MainTrackList.tracks) PNG 커버 핀포인트 주입
        // =========================================================================

        [HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.Start))]
        public static class MainTrackList_Start_Patch
        {
            // SongInjectorHooks의 MainTrackList.Start 패치(사본 생성 및 TargetTrackID 등록)가
            // 먼저 실행되도록 우선순위를 낮춰서 실행 순서를 보장한다.
            [HarmonyPriority(Priority.Last)]
            public static void Postfix(MainTrackList __instance)
            {
                if (__instance == null || __instance.tracks == null) return;
                MelonLogger.Msg($"[★ MainTrackList.Start 감지 ★] 총 곡 수: {__instance.tracks.Count}개");

                var customCover = HwaAssetManager.LoadCoverSprite();
                if (customCover == null) return;

                // 주입된 사본(테스트 곡) 블록에만 커스텀 PNG 커버 적용 (원본 WindShifter는 건드리지 않음)
                if (string.IsNullOrEmpty(HwaAssetManager.TargetTrackID)) return;

                foreach (var trackBlock in __instance.tracks)
                {
                    if (trackBlock == null) continue;
                    if (string.Equals(trackBlock.uniqueID, HwaAssetManager.TargetTrackID, StringComparison.OrdinalIgnoreCase))
                    {
                        trackBlock.TrackCover = customCover;
                        MelonLogger.Msg($"  [★ MainTrackListBlock 데이터 커버 교체 성공 (사본 전용) ★] 제목: '{trackBlock.TrackTitle}' | 커버: '{customCover.name}'");
                        break;
                    }
                }
            }
        }

        // =========================================================================
        // 결과 씬 (Panel_Result) BGA & PNG Cover 주입
        // =========================================================================

        [HarmonyPatch(typeof(Panel_Result), nameof(Panel_Result.Awake))]
        public static class Panel_Result_Awake_Patch
        {
            public static void Postfix(Panel_Result __instance)
            {
                if (__instance == null) return;
                MelonLogger.Msg("[★ Panel_Result.Awake 감지 ★] 결과 씬 진입!");

                if (!HwaAssetManager.IsTargetTrackActive) return;

                // 자켓 PNG 주입 (사본 전용)
                HwaAssetManager.ApplyCustomCoverToHierarchy(__instance.gameObject);

                // BGA 주입 (사본 전용)
                var players = __instance.GetComponentsInChildren<VideoPlayer>(true);
                if (players != null)
                {
                    foreach (var vp in players)
                    {
                        HwaAssetManager.ApplyCustomBga(vp, true);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Panel_Result), nameof(Panel_Result.ApplyText))]
        public static class Panel_Result_ApplyText_Patch
        {
            public static void Postfix(Panel_Result __instance)
            {
                if (__instance == null) return;
                MelonLogger.Msg("[★ Panel_Result.ApplyText 감지 ★] 점수 표출 시작!");

                if (!HwaAssetManager.IsTargetTrackActive) return;

                var players = UnityEngine.Object.FindObjectsOfType<VideoPlayer>();
                if (players != null)
                {
                    foreach (var vp in players)
                    {
                        HwaAssetManager.ApplyCustomBga(vp, true);
                    }
                }
            }
        }
    }
}
