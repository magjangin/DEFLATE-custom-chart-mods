using System;
using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using UnityEngine;
using UnityEngine.Video;
using DEFLATE_custom_chart.Core;
using DEFLATE_custom_chart.Core.Wrapper;

namespace DEFLATE_custom_chart.Hooks
{
    public static class AssetManagerHooks
    {
        [HarmonyPatch(typeof(TrackAssetManager), nameof(TrackAssetManager.LoadAudioClip))]
        public static class TrackAssetManager_LoadAudioClip_Patch
        {
            // 원본 시그니처의 콜백은 Il2CppInterop이 만든 Il2CppSystem.Action 계열이다.
            // (System.Action으로 받으면 타입이 맞지 않는 객체를 델리게이트로 호출하게 된다)
            // 파라미터 이름 비의존(__args 타입 바인딩): key는 읽기만 하므로 이름 대신 타입으로 고른다(HookArgs 참고).
            public static bool Prefix(object[] __args)
            {
                try
                {
                    string key = HookArgs.First<string>(__args);
                    var onSuccess = HookArgs.First<Il2CppSystem.Action<AudioClip>>(__args);
                    var onFail = HookArgs.First<Il2CppSystem.Action>(__args);
                    // 곡 목록 프리뷰에서 커스텀 사본이 선택된 상태로 자신의 오디오 Key가 요청된 경우에만
                    // hwa/ 커스텀 BGM으로 오버라이드한다 (원본 WindShifter는 같은 Key를 써도 건드리지 않음).
                    // 인접 곡 사전 로드(PreloadNearbyTracks) 로그 스팸을 피하기 위해 커스텀 오버라이드 시에만 기록한다.
                    if (HwaAssetManager.ShouldOverrideAudioKey(key) && !string.IsNullOrEmpty(HwaAssetManager.BgmFilePath))
                    {
                        bool songSelect = HwaAssetManager.IsInSongSelectContext;
                        string where = songSelect ? "곡 목록 프리뷰" : "인게임";
                        var requestedEntry = HwaAssetManager.Active;
                        if (requestedEntry != null && requestedEntry.BgmClip != null)
                        {
                            ModLog.Verbose($"[HwaAssetManager] ★ {where} Custom BGM 즉시 오버라이드(캐시) ★ Key: '{key}'");
                            onSuccess?.Invoke(requestedEntry.BgmClip);
                            return false;
                        }

                        ModLog.Verbose($"[HwaAssetManager] ★ {where} Custom BGM 오버라이드 ★ Key: '{key}'");
                        // 원본 로드를 건너뛰므로, 실패하면 onFail을 불러 게임이 계속 기다리지 않게 한다.
                        // 곡 선택에서만 '빠른 스크롤로 커서가 다른 곡으로 넘어갔다면 지연 콜백 취소'를 적용한다.
                        // (인게임에서 콜백을 취소하면 게임이 오디오를 영원히 기다리므로 반드시 성공/실패 중 하나를 호출)
                        MelonCoroutines.Start(HwaAssetManager.LoadCustomBgmForEntryCoroutine(
                            requestedEntry,
                            null,
                            false,
                            requireStillActive: songSelect,
                            clip => onSuccess?.Invoke(clip),
                            () => onFail?.Invoke()));
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[TrackAssetManager.LoadAudioClip] Prefix 처리 중 예외: {ex.Message}");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(TrackAssetManager), nameof(TrackAssetManager.LoadVideoURL))]
        public static class TrackAssetManager_LoadVideoURL_Patch
        {
            public static bool Prefix(object[] __args)
            {
                try
                {
                    string key = HookArgs.First<string>(__args);
                    var onSuccess = HookArgs.First<Il2CppSystem.Action<string>>(__args);
                    if (HwaAssetManager.ShouldOverrideVideoKey(key))
                    {
                        string where = HwaAssetManager.IsInSongSelectContext ? "곡 목록 프리뷰" : "인게임";
                        ModLog.Verbose($"[HwaAssetManager] ★ {where} Custom BGA URL 오버라이드 ★ Key: '{key}'");
                        onSuccess?.Invoke(HwaAssetManager.BgaFileUrl);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[TrackAssetManager.LoadVideoURL] Prefix 처리 중 예외: {ex.Message}");
                }
                return true;
            }
        }

        // =========================================================================
        // 곡 선택 씬(FavControl) 프리뷰 오디오 직접 인터셉터
        // TrackAssetManager.audioCache에 인접 곡 사전 로드로 숙주 곡 OGG가 캐시되어 있어도
        // 커스텀 곡 선택 중에는 절대로 숙주 곡 BGM이 재생되지 않도록 차단한다.
        // =========================================================================

        [HarmonyPatch(typeof(FavControl), "SafeLoadAndPlayAudio")]
        public static class FavControl_SafeLoadAndPlayAudio_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(FavControl), "SafeLoadAndPlayAudio");

            // 파라미터 이름 비의존: (string addressKey, AudioSource audioSource) 중 AudioSource만 쓴다.
            public static bool Prefix(object[] __args)
            {
                try
                {
                    if (!HwaAssetManager.IsInSongSelectContext || !HwaAssetManager.IsTargetTrackActive) return true;
                    var audioSource = HookArgs.First<AudioSource>(__args);
                    if (audioSource == null || string.IsNullOrEmpty(HwaAssetManager.BgmFilePath)) return true;

                    var requestedEntry = HwaAssetManager.Active;
                    if (requestedEntry == null) return true;

                    if (requestedEntry.BgmClip != null)
                    {
                        if (audioSource.clip != requestedEntry.BgmClip || !audioSource.isPlaying)
                        {
                            audioSource.Stop();
                            audioSource.clip = requestedEntry.BgmClip;
                            audioSource.time = 0f;
                            audioSource.Play();
                        }
                        return false;
                    }

                    // 로드 중 숙주 곡 잔여 오디오 차단
                    audioSource.Stop();
                    MelonCoroutines.Start(HwaAssetManager.LoadCustomBgmForEntryCoroutine(
                        requestedEntry,
                        audioSource,
                        forcePlay: true,
                        requireStillActive: true));
                    return false;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[FavControl.SafeLoadAndPlayAudio] Prefix 처리 중 예외: {ex.Message}");
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(FavControl), "PlayAudioWhenReady")]
        public static class FavControl_PlayAudioWhenReady_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(FavControl), "PlayAudioWhenReady");

            public static bool Prefix(AudioSource audioSource, ref AudioClip clip)
            {
                try
                {
                    if (!HwaAssetManager.IsInSongSelectContext || !HwaAssetManager.IsTargetTrackActive) return true;
                    if (string.IsNullOrEmpty(HwaAssetManager.BgmFilePath)) return true;

                    var customClip = HwaAssetManager.CustomBgmClip;
                    if (customClip != null)
                    {
                        clip = customClip;
                        return true;
                    }

                    // 커스텀 BGM이 아직 로딩 중이면 숙주 곡 클립 재생을 막고 완료 후 재생한다.
                    if (audioSource != null)
                    {
                        audioSource.Stop();
                        MelonCoroutines.Start(HwaAssetManager.LoadCustomBgmForEntryCoroutine(
                            HwaAssetManager.Active,
                            audioSource,
                            forcePlay: true,
                            requireStillActive: true));
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[FavControl.PlayAudioWhenReady] Prefix 처리 중 예외: {ex.Message}");
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Bank_PV_Ctrl), nameof(Bank_PV_Ctrl.PlayPVVideo))]
        public static class Bank_PV_Ctrl_PlayPVVideo_Patch
        {
            // pvKey는 로그에만 쓰므로 이름 대신 __args로 읽는다(ref 선언 불필요).
            public static bool Prefix(Bank_PV_Ctrl __instance, object[] __args)
            {
                try
                {
                    ModLog.Verbose($"[PV 메뉴 BGA 재생] pvKey: '{HookArgs.First<string>(__args)}'");

                    if (HwaAssetManager.IsTargetTrackActive && __instance != null && __instance.videoPlayer != null)
                    {
                        if (HwaAssetManager.ApplyCustomBga(__instance.videoPlayer, true))
                        {
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Bank_PV_Ctrl.PlayPVVideo] Prefix 처리 중 예외: {ex.Message}");
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Bank_PV_Ctrl), "LoadVideoAndAudio")]
        public static class Bank_PV_Ctrl_LoadVideoAndAudio_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(Bank_PV_Ctrl), "LoadVideoAndAudio");

            // (string videoKey, string audioKey, MainTrackListBlock track) — 키는 로그에만 쓰므로 __args로 읽는다.
            public static bool Prefix(Bank_PV_Ctrl __instance, object[] __args)
            {
                try
                {
                    ModLog.Verbose($"[PV 메뉴 미디어 로드] 인자: {HookArgs.DescribeStrings(__args)}");

                    if (HwaAssetManager.IsTargetTrackActive && __instance != null && __instance.videoPlayer != null)
                    {
                        HwaAssetManager.ApplyCustomBga(__instance.videoPlayer, false);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Bank_PV_Ctrl.LoadVideoAndAudio] Prefix 처리 중 예외: {ex.Message}");
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
            // (string pvKey, VideoPlayer videoPlayer, float pvOffset, bool autoPlay) — 같은 타입 인자가 하나씩이라 타입으로 고른다.
            public static bool Prefix(object[] __args)
            {
                try
                {
                    var videoPlayer = HookArgs.First<VideoPlayer>(__args);
                    bool autoPlay = HookArgs.FirstValue(__args, true);
                    ModLog.Verbose($"[★ SimpleRandomVideo.LoadVideoSmart 호출 ★] pvKey: '{HookArgs.First<string>(__args)}' | videoPlayer: '{(videoPlayer != null ? videoPlayer.name : "null")}'");

                    if (HwaAssetManager.IsTargetTrackActive && videoPlayer != null && HwaAssetManager.ApplyCustomBga(videoPlayer, autoPlay))
                    {
                        return false; // 원본 Addressable 비디오 로드 스킵
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SimpleRandomVideo.LoadVideoSmart] Prefix 처리 중 예외: {ex.Message}");
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SimpleRandomVideo), nameof(SimpleRandomVideo.LoadVideo))]
        public static class SimpleRandomVideo_LoadVideo_Patch
        {
            public static bool Prefix(object[] __args)
            {
                try
                {
                    var videoPlayer = HookArgs.First<VideoPlayer>(__args);
                    bool autoPlay = HookArgs.FirstValue(__args, true);
                    ModLog.Verbose($"[★ SimpleRandomVideo.LoadVideo 호출 ★] addressKey: '{HookArgs.First<string>(__args)}' | videoPlayer: '{(videoPlayer != null ? videoPlayer.name : "null")}'");

                    if (HwaAssetManager.IsTargetTrackActive && videoPlayer != null && HwaAssetManager.ApplyCustomBga(videoPlayer, autoPlay))
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SimpleRandomVideo.LoadVideo] Prefix 처리 중 예외: {ex.Message}");
                }
                return true;
            }
        }

        private static void EnforceCustomBgaUrl(VideoPlayer player, string caller)
        {
            try
            {
                if (player == null) return;
                if (!HwaAssetManager.IsTargetTrackActive) return;

                // 인게임/결과 화면에서는 audioCom이 BGM을 담당하므로 VideoPlayer 자체 오디오를 항상 음소거한다.
                if (!HwaAssetManager.IsInSongSelectContext)
                {
                    HwaAssetManager.MuteVideoPlayer(player);
                }

                string bgaUrl = HwaAssetManager.BgaFileUrl;
                if (string.IsNullOrEmpty(bgaUrl)) return;

                if (player.source != VideoSource.Url || !string.Equals(player.url, bgaUrl, StringComparison.Ordinal))
                {
                    player.source = VideoSource.Url;
                    player.url = bgaUrl;
                    ModLog.Verbose($"[★ Universal {caller} Custom BGA 강제 적용 ★] 오브젝트: '{player.gameObject.name}' | URL: '{bgaUrl}'");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{caller}] Prefix 처리 중 예외: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(VideoPlayer), nameof(VideoPlayer.Play))]
        public static class VideoPlayer_Play_Patch
        {
            public static void Prefix(VideoPlayer __instance)
            {
                EnforceCustomBgaUrl(__instance, "VideoPlayer.Play");
            }
        }

        [HarmonyPatch(typeof(VideoPlayer), nameof(VideoPlayer.Prepare))]
        public static class VideoPlayer_Prepare_Patch
        {
            public static void Prefix(VideoPlayer __instance)
            {
                EnforceCustomBgaUrl(__instance, "VideoPlayer.Prepare");
            }
        }

        // =========================================================================
        // 곡 선택 씬 TrackList 데이터 모델 (MainTrackList.tracks) PNG 커버 핀포인트 주입
        // =========================================================================

        [HarmonyPatch(typeof(MainTrackList), nameof(MainTrackList.Start))]
        public static class MainTrackList_Start_Patch
        {
            // SongInjectorHooks(Priority.High) 실행 후, SongListHooks(Priority.Last) 로깅 전에 실행한다.
            [HarmonyPriority(Priority.Normal)]
            public static void Postfix(MainTrackList __instance)
            {
                try
                {
                    if (__instance == null || __instance.tracks == null) return;
                    ModLog.Verbose($"[★ MainTrackList.Start 감지 ★] 총 곡 수: {__instance.tracks.Count}개");

                    // 주입된 커스텀 곡 사본 블록에만 각자의 PNG 커버 적용 (원본 곡 116+개는 이름 체크로 즉시 건너뜀)
                    if (!CustomSongLibrary.HasSongs) return;

                    foreach (var trackBlock in __instance.tracks)
                    {
                        if (!SongInjectorHooks.IsCustomBlock(trackBlock)) continue;

                        string uniqueId = Il2CppReflectionHelper.GetValueResilient(trackBlock, () => trackBlock.uniqueID, "uniqueID", MemberFallbacks.TrackBlock.UniqueId, string.Empty);
                        var entry = CustomSongLibrary.FindByTrackID(uniqueId);
                        if (entry == null) continue;

                        var customCover = entry.LoadCoverSprite();
                        if (customCover == null) continue;

                        var currentCover = Il2CppReflectionHelper.GetValueResilient<Sprite>(trackBlock, () => trackBlock.TrackCover, "TrackCover", MemberFallbacks.TrackBlock.Cover, null);
                        if (currentCover == customCover) continue;

                        Il2CppReflectionHelper.SetValueResilient(trackBlock, () => trackBlock.TrackCover = customCover, "TrackCover", MemberFallbacks.TrackBlock.Cover, customCover);
                        string title = Il2CppReflectionHelper.GetValueResilient(trackBlock, () => trackBlock.TrackTitle, "TrackTitle", MemberFallbacks.TrackBlock.Title, entry.Meta.Title);
                        ModLog.Verbose($"  [★ MainTrackListBlock 데이터 커버 교체 성공 (사본 전용) ★] 앨범: '{entry.Meta.Album}' | 제목: '{title}' | 커버: '{customCover.name}'");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[MainTrackList.Start] 커버 적용 중 예외: {ex.Message}");
                }
            }
        }

        // =========================================================================
        // 결과 씬 (Panel_Result) BGA & PNG Cover & 파형/재생 미리듣기 BGM 주입
        // =========================================================================

        // Panel_Result의 파형/재생 미리듣기 위젯이 사용할 오디오 클립을 강제로 커스텀 BGM으로 오버라이드한다.
        // (내부적으로 태그 기반 AudioSource 자동탐색(TryAutoFindBgmSource)에 의존하므로, 명시적으로 고정해 신뢰성을 확보)
        [HarmonyPatch(typeof(Panel_Result), nameof(Panel_Result.GetPreviewClip))]
        public static class Panel_Result_GetPreviewClip_Patch
        {
            public static bool Prefix(Panel_Result __instance, ref AudioClip __result)
            {
                try
                {
                    if (__instance == null || !HwaAssetManager.IsTargetTrackActive) return true;
                    if (HwaAssetManager.CustomBgmClip == null) return true;

                    __result = HwaAssetManager.CustomBgmClip;
                    ModLog.Verbose($"[HwaAssetManager] ★ 결과 화면 파형/재생 미리듣기 Custom BGM 오버라이드 ★: '{__result.name}' ({__result.length:F2}초)");
                    return false;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Panel_Result.GetPreviewClip] Prefix 처리 중 예외: {ex.Message}");
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Panel_Result), nameof(Panel_Result.Awake))]
        public static class Panel_Result_Awake_Patch
        {
            public static void Postfix(Panel_Result __instance)
            {
                try
                {
                    if (__instance == null) return;
                    ModLog.Verbose("[★ Panel_Result.Awake 감지 ★] 결과 씬 진입!");

                    if (!HwaAssetManager.IsTargetTrackActive) return;

                    // 자켓 PNG 주입 (사본 전용)
                    HwaAssetManager.ApplyCustomCoverToHierarchy(__instance.gameObject);

                    // BGA 주입 (사본 전용, 결과 화면 파형 미리듣기 BGM과 겹치지 않도록 음소거)
                    var players = __instance.GetComponentsInChildren<VideoPlayer>(true);
                    if (players != null)
                    {
                        foreach (var vp in players)
                        {
                            HwaAssetManager.ApplyCustomBga(vp, true, muteAudio: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Panel_Result.Awake] Postfix 처리 중 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(Panel_Result), nameof(Panel_Result.ApplyText))]
        public static class Panel_Result_ApplyText_Patch
        {
            public static void Postfix(Panel_Result __instance)
            {
                try
                {
                    if (__instance == null) return;
                    ModLog.Verbose("[★ Panel_Result.ApplyText 감지 ★] 점수 표출 시작!");

                    if (!HwaAssetManager.IsTargetTrackActive) return;

                    var players = __instance.GetComponentsInChildren<VideoPlayer>(true);
                    if (players == null || players.Length == 0)
                    {
                        players = UnityEngine.Object.FindObjectsOfType<VideoPlayer>();
                    }
                    if (players != null)
                    {
                        foreach (var vp in players)
                        {
                            HwaAssetManager.ApplyCustomBga(vp, true, muteAudio: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Panel_Result.ApplyText] Postfix 처리 중 예외: {ex.Message}");
                }
            }
        }
    }
}
