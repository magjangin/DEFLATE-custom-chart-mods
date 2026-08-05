using System;
using System.Collections;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>
    /// 커스텀 에셋 접근용 파사드(Facade).
    ///
    /// 실제 곡 데이터는 <see cref="CustomSongLibrary"/>가 앨범/곡 폴더 단위(<see cref="CustomSongEntry"/>)로 들고 있고,
    /// 이 클래스는 "지금 선택된 곡(Active)"의 에셋을 각 훅에 그대로 노출해주는 얇은 레이어입니다.
    /// (곡이 하나뿐이던 시절의 훅 코드가 그대로 동작하도록 API 형태를 유지합니다.)
    /// </summary>
    public static class HwaAssetManager
    {
        public static string HwaDirectoryPath => CustomSongLibrary.HwaDirectoryPath;

        /// <summary>현재 선택된 커스텀 곡. 원본 곡을 고른 상태면 null.</summary>
        public static CustomSongEntry Active => CustomSongLibrary.Active;

        // =========================================================================
        // 현재 곡 에셋 (Active 위임)
        // =========================================================================

        public static string BgmFilePath => Active?.BgmFilePath;
        public static string BgaFilePath => Active?.BgaFilePath;
        public static string BgaFileUrl => Active?.BgaFileUrl;
        public static string CoverFilePath => Active?.CoverFilePath;
        public static string InfoFilePath => Active?.InfoFilePath;
        public static string BmsFilePath => Active?.BmsFilePath;
        public static Bms.BmsChart LoadedBmsChart => Active?.Chart;

        public static AudioClip CustomBgmClip => Active?.BgmClip;
        public static Sprite CustomCoverSprite => Active?.CoverSprite;

        /// <summary>현재 선택된 곡의 메타데이터. 커스텀 곡이 선택된 상태가 아니면 (다른 곡의 정보가 새어나가지 않도록) 기본값 객체를 돌려줍니다.</summary>
        public static HwaMetaInfo CurrentMeta => Active?.Meta ?? _defaultMeta;
        private static readonly HwaMetaInfo _defaultMeta = new HwaMetaInfo();

        // =========================================================================
        // 커스텀 에셋 주입 대상 트랙 식별
        // =========================================================================

        /// <summary>현재 선택된 커스텀 곡 사본의 고유 ID (없으면 null).</summary>
        public static string TargetTrackID => Active?.InjectedTrackID;

        /// <summary>사본이 원본에서 복사해온 오디오/비디오 Key. 곡 목록 프리뷰 오버라이드 이중 확인용.</summary>
        public static string TargetAudioKey => Active?.AudioKey;
        public static string TargetVideoKey => Active?.VideoKey;

        /// <summary>현재 선택/재생 중인 트랙이 커스텀 곡인지 여부. 원본 곡에서는 false여야 합니다.</summary>
        public static bool IsTargetTrackActive
        {
            get => Active != null;
            set { if (!value) CustomSongLibrary.SetActive(null, null); }
        }

        /// <summary>곡 목록(프리뷰) 씬에 있는지 여부. 로딩 씬 진입 시 false로 전환되어, 프리뷰 전용 BGM 오버라이드 훅이
        /// 로딩/인게임 씬의 실제 오디오 로드 요청을 잘못 가로채 원본 로직을 스킵시키는(크래시 원인) 것을 막습니다.</summary>
        public static bool IsInSongSelectContext { get; set; } = true;

        /// <summary>선택된 곡 ID/제목으로 커스텀 곡을 판정하고 Active를 갱신합니다.</summary>
        public static bool SetActiveTrack(string activeTrackID, string activeTitle = null)
        {
            return CustomSongLibrary.SetActive(activeTrackID, activeTitle) != null;
        }

        public static bool IsTargetAudioPreview(string audioKey)
        {
            var active = Active;
            return IsInSongSelectContext &&
                active != null &&
                !string.IsNullOrEmpty(active.AudioKey) &&
                string.Equals(active.AudioKey, audioKey, StringComparison.OrdinalIgnoreCase);
        }

        public class HwaMetaInfo
        {
            public string Title { get; set; } = "どりーむもーど";
            public string Artist { get; set; } = "화영왕";
            public string Album { get; set; } = "custom albums";
            public string BgaAuthor { get; set; } = "화영왕";
            public string ChartAuthor { get; set; } = "화영왕";
            public string ChartMaker => ChartAuthor;

            public int EasyLevel { get; set; } = 8;
            public int NormalLevel { get; set; } = 8;
            public int HardLevel { get; set; } = 8;
        }

        public static void Initialize()
        {
            CustomSongLibrary.Initialize();
        }

        /// <summary>hwa/ 폴더를 다시 스캔합니다 (곡 추가/삭제 후 재적용용).</summary>
        public static void Rescan()
        {
            CustomSongLibrary.Scan();
        }

        // =========================================================================
        // PNG 자켓 에셋 로딩
        // =========================================================================

        public static Sprite LoadCoverSprite()
        {
            return Active?.LoadCoverSprite();
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
        // BGA (.mp4) 에셋 적용
        // =========================================================================

        public static bool ApplyCustomBga(VideoPlayer videoPlayer, bool autoPlay = true)
        {
            string url = BgaFileUrl;
            if (videoPlayer == null || string.IsNullOrEmpty(url)) return false;

            try
            {
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = url;
                videoPlayer.Prepare();
                if (autoPlay)
                {
                    videoPlayer.Play();
                }
                MelonLogger.Msg($"[HwaAssetManager] ★ Custom BGA 주입 성공 ★ 오브젝트: '{videoPlayer.name}' | URL: '{url}'");
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
            var active = Active;
            if (active == null) yield break;

            // 중첩 코루틴 지원 여부에 기대지 않고 내부 이터레이터를 직접 굴려 yield를 그대로 전달한다.
            var inner = active.LoadBgmCoroutine(targetAudioSource, forcePlay, onLoaded);
            while (inner.MoveNext())
            {
                yield return inner.Current;
            }
        }

        public static void ResetCache()
        {
            foreach (var entry in CustomSongLibrary.Entries)
            {
                entry.ResetLoadedAssets();
            }
            IsInSongSelectContext = true;
        }
    }
}
