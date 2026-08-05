using System;
using System.Collections;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DEFLATE_custom_chart.Core
{
    public static class HwaAssetManager
    {
        public static string HwaDirectoryPath => Path.Combine(MelonEnvironment.GameRootDirectory, "hwa");

        // 캐시 변수
        public static string BgmFilePath { get; private set; }
        public static string BgaFilePath { get; private set; }
        public static string BgaFileUrl { get; private set; }
        public static string CoverFilePath { get; private set; }

        public static AudioClip CustomBgmClip { get; private set; }
        public static Sprite CustomCoverSprite { get; private set; }

        private static bool _isBgmPreloading = false;
        private static bool _isInitialized = false;

        // =========================================================================
        // 커스텀 에셋 주입 대상 트랙 식별 (사본 전용 주입을 위한 상태)
        // =========================================================================

        /// <summary>주입된 사본(테스트 곡)의 고유 ID. 이 ID를 가진 트랙에만 hwa/ 커스텀 에셋을 적용합니다.</summary>
        public static string TargetTrackID { get; set; }

        /// <summary>현재 선택/재생 중인 트랙이 TargetTrackID와 일치하는지 여부. 원본 곡에는 false를 유지시켜야 합니다.</summary>
        public static bool IsTargetTrackActive { get; set; }

        /// <summary>사본(테스트 곡)이 원본에서 그대로 복사해온 오디오/비디오 리소스 Key. 곡 목록 프리뷰에서 정확히 이 Key로 온 요청만 오버라이드하기 위한 이중 확인용입니다.</summary>
        public static string TargetAudioKey { get; set; }
        public static string TargetVideoKey { get; set; }

        /// <summary>곡 목록(프리뷰) 씬에 있는지 여부. 로딩 씬 진입 시 false로 전환되어, 프리뷰 전용 BGM 오버라이드 훅이
        /// 로딩/인게임 씬의 실제 오디오 로드 요청을 잘못 가로채 원본 로직을 스킵시키는(크래시 원인) 것을 막습니다.</summary>
        public static bool IsInSongSelectContext { get; set; } = true;

        /// <summary>주어진 trackId가 주입 대상(사본)인지 판정하고 IsTargetTrackActive 상태를 갱신합니다.</summary>
        public static bool SetActiveTrack(string trackId)
        {
            IsTargetTrackActive = !string.IsNullOrEmpty(TargetTrackID) &&
                !string.IsNullOrEmpty(trackId) &&
                string.Equals(TargetTrackID, trackId, StringComparison.OrdinalIgnoreCase);
            return IsTargetTrackActive;
        }

        /// <summary>곡 목록에서 오디오 프리뷰로 요청된 key가 사본 자신의 오디오 key와 일치하는지 (선택 상태까지 함께) 판정합니다.</summary>
        public static bool IsTargetAudioPreview(string audioKey)
        {
            return IsInSongSelectContext &&
                IsTargetTrackActive &&
                !string.IsNullOrEmpty(TargetAudioKey) &&
                string.Equals(TargetAudioKey, audioKey, StringComparison.OrdinalIgnoreCase);
        }

        public static void Initialize()
        {
            if (!Directory.Exists(HwaDirectoryPath))
            {
                try
                {
                    Directory.CreateDirectory(HwaDirectoryPath);
                    MelonLogger.Msg($"[HwaAssetManager] hwa/ 폴더 생성: '{HwaDirectoryPath}'");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[HwaAssetManager] hwa/ 폴더 생성 실패: {ex.Message}");
                    return;
                }
            }

            ScanHwaDirectory();
            LoadCoverSprite();
            _isInitialized = true;
        }

        public static void ScanHwaDirectory()
        {
            if (!Directory.Exists(HwaDirectoryPath)) return;

            string[] files = Directory.GetFiles(HwaDirectoryPath, "*.*");

            BgmFilePath = null;
            BgaFilePath = null;
            BgaFileUrl = null;
            CoverFilePath = null;

            foreach (var f in files)
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (BgmFilePath == null && (ext == ".wav" || ext == ".mp3" || ext == ".ogg"))
                {
                    BgmFilePath = f;
                }
                else if (BgaFilePath == null && ext == ".mp4")
                {
                    BgaFilePath = f;
                    BgaFileUrl = "file:///" + f.Replace('\\', '/');
                }
                else if (CoverFilePath == null && ext == ".png")
                {
                    CoverFilePath = f;
                }
            }

            MelonLogger.Msg($"[HwaAssetManager] hwa 폴더 에셋 감지 완료:");
            MelonLogger.Msg($"  - BGM 파일:   {(BgmFilePath != null ? Path.GetFileName(BgmFilePath) : "없음")}");
            MelonLogger.Msg($"  - BGA 비디오: {(BgaFilePath != null ? Path.GetFileName(BgaFilePath) : "없음")}");
            MelonLogger.Msg($"  - PNG 자켓:   {(CoverFilePath != null ? Path.GetFileName(CoverFilePath) : "없음")}");
        }

        // =========================================================================
        // PNG 자켓 에셋 로딩
        // =========================================================================

        public static Sprite LoadCoverSprite()
        {
            if (CustomCoverSprite != null) return CustomCoverSprite;
            if (string.IsNullOrEmpty(CoverFilePath) || !File.Exists(CoverFilePath)) return null;

            try
            {
                byte[] fileData = File.ReadAllBytes(CoverFilePath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (ImageConversion.LoadImage(tex, fileData))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    CustomCoverSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    CustomCoverSprite.name = Path.GetFileNameWithoutExtension(CoverFilePath);
                    MelonLogger.Msg($"[HwaAssetManager] ★ Custom PNG Cover 로드 성공 (고품질 필터링 적용) ★ ({tex.width}x{tex.height})");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HwaAssetManager] Custom PNG Cover 로드 오류: {ex.Message}");
            }

            return CustomCoverSprite;
        }

        public static bool ApplyCustomCover(Image targetImage)
        {
            var sprite = LoadCoverSprite();
            if (sprite != null && targetImage != null)
            {
                if (targetImage.sprite == sprite) return true; // 중복 재할당 및 깜빡임 방지
                targetImage.sprite = sprite;
                return true;
            }
            return false;
        }

        public static void ApplyCustomCoverToHierarchy(GameObject root)
        {
            if (root == null) return;
            var sprite = LoadCoverSprite();
            if (sprite == null) return;

            var images = root.GetComponentsInChildren<Image>(true);
            if (images == null) return;

            foreach (var img in images)
            {
                if (img == null) continue;
                string lowerName = img.name.ToLower();
                if (lowerName.Contains("cover") || lowerName.Contains("track") || lowerName.Contains("jacket"))
                {
                    if (img.sprite != sprite)
                    {
                        img.sprite = sprite;
                        MelonLogger.Msg($"[HwaAssetManager] UI 자켓 PNG 적용: 오브젝트 '{img.name}'");
                    }
                }
            }
        }

        // =========================================================================
        // 커스텀 테스트 곡 래퍼 (Custom Test Track Wrapper) 인젝터
        // =========================================================================

        public static bool InjectCustomTestTrackWrapper(Il2Cppdizzylab.castor.MainTrackListBlock trackBlock)
        {
            if (trackBlock == null) return false;

            try
            {
                MelonLogger.Msg($"[HwaAssetManager] ★ 테스트 곡 래퍼(Test Track Wrapper) 주입 시작 ★");
                MelonLogger.Msg($"  - 원본 곡 ID:     '{trackBlock.uniqueID}'");
                MelonLogger.Msg($"  - 원본 곡 제목:   '{trackBlock.TrackTitle}'");

                // 1. 곡 설명 및 메타데이터 주입
                trackBlock.TrackTitle = "[HWA CUSTOM] Test Track";
                trackBlock.TrackAuthor = "Antigravity & Hwa";
                trackBlock.TrackAlbum = "DEFLATE Modding Archive";
                trackBlock.Description = "Custom BGM, BGA, and Cover Art Test Track.";

                // 2. PNG 커버 자켓 주입
                var customCover = LoadCoverSprite();
                if (customCover != null)
                {
                    trackBlock.TrackCover = customCover;
                    MelonLogger.Msg($"  - [PNG 커버 주입] '{customCover.name}' ({customCover.texture.width}x{customCover.texture.height})");
                }

                MelonLogger.Msg($"[HwaAssetManager] ★ 테스트 곡 래퍼 데이터 바인딩 완수! ★");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HwaAssetManager] 테스트 곡 래퍼 주입 중 예외 발생: {ex.Message}");
                return false;
            }
        }

        // =========================================================================
        // BGA (.mp4) 에셋 로딩 및 적용
        // =========================================================================

        public static bool ApplyCustomBga(VideoPlayer videoPlayer, bool autoPlay = true)
        {
            if (videoPlayer == null || string.IsNullOrEmpty(BgaFileUrl)) return false;

            try
            {
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = BgaFileUrl;
                videoPlayer.Prepare();
                if (autoPlay)
                {
                    videoPlayer.Play();
                }
                MelonLogger.Msg($"[HwaAssetManager] ★ Custom BGA 주입 성공 ★ 오브젝트: '{videoPlayer.name}' | URL: '{BgaFileUrl}'");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HwaAssetManager] Custom BGA 주입 오류: {ex.Message}");
                return false;
            }
        }

        // =========================================================================
        // BGM (.wav, .mp3, .ogg) 에셋 코루틴 로딩 및 적용
        // =========================================================================

        public static IEnumerator LoadCustomBgmCoroutine(AudioSource targetAudioSource, bool forcePlay = true, Action<AudioClip> onLoaded = null)
        {
            if (string.IsNullOrEmpty(BgmFilePath) || !File.Exists(BgmFilePath)) yield break;

            if (CustomBgmClip != null)
            {
                ApplyLoadedBgm(targetAudioSource, forcePlay, onLoaded, fromCache: true);
                yield break;
            }

            // 이미 다른 곳에서 로딩 중이면 그 결과를 기다렸다가 동일하게 적용/콜백한다.
            if (_isBgmPreloading)
            {
                while (_isBgmPreloading) yield return null;
                if (CustomBgmClip != null)
                {
                    ApplyLoadedBgm(targetAudioSource, forcePlay, onLoaded, fromCache: true);
                }
                yield break;
            }

            _isBgmPreloading = true;

            string uri = "file:///" + BgmFilePath.Replace('\\', '/');
            UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                CustomBgmClip = DownloadHandlerAudioClip.GetContent(www);
                if (CustomBgmClip != null)
                {
                    ApplyLoadedBgm(targetAudioSource, forcePlay, onLoaded, fromCache: false);
                }
            }
            else
            {
                MelonLogger.Error($"[HwaAssetManager] Custom BGM 비동기 로드 실패: {www.error}");
            }

            www.Dispose();
            _isBgmPreloading = false;
        }

        private static void ApplyLoadedBgm(AudioSource targetAudioSource, bool forcePlay, Action<AudioClip> onLoaded, bool fromCache)
        {
            if (targetAudioSource != null)
            {
                targetAudioSource.Stop();
                targetAudioSource.clip = CustomBgmClip;
                targetAudioSource.time = 0f;
                if (forcePlay) targetAudioSource.Play();
            }

            string tag = fromCache ? "캐시된 Custom BGM 즉시 적용" : "★ Custom BGM 주입 및 재생 성공 ★";
            MelonLogger.Msg($"[HwaAssetManager] {tag}: '{CustomBgmClip.name}' ({CustomBgmClip.length:F2}초)");

            onLoaded?.Invoke(CustomBgmClip);
        }

        public static void ResetCache()
        {
            CustomBgmClip = null;
            CustomCoverSprite = null;
            _isBgmPreloading = false;
            _isInitialized = false;
        }
    }
}
