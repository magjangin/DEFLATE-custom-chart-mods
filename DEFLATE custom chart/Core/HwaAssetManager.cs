using System;
using System.Collections;
using Il2Cppdizzylab.castor;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using DEFLATE_custom_chart.Core.Wrapper;

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

        /// <summary>곡 목록(프리뷰) 씬에 있는지 여부. 곡 선택 전용 훅(FavControl 프리뷰, 빠른 스크롤 취소 등)이
        /// 인게임 씬의 실제 로드 요청에 끼어들지 않게 합니다. <see cref="EnterSongSelect"/> / <see cref="EnterGameplay"/>로만 전환하세요.
        /// (게임 1.2부터 곡 선택 → 인게임이 로딩 씬(LoadingGamePlay)을 거치지 않으므로, 로딩 씬 훅만으로 끄면 인게임에서도 true로 남습니다.)</summary>
        public static bool IsInSongSelectContext { get; set; } = true;

        /// <summary>플레이 확정 이후 인게임(결과 화면 포함) 컨텍스트인지 여부.</summary>
        public static bool IsInGameplayContext { get; private set; }

        /// <summary>곡 목록 씬 진입(MainTrackList.Start).</summary>
        public static void EnterSongSelect()
        {
            IsInSongSelectContext = true;
            IsInGameplayContext = false;
        }

        /// <summary>
        /// 곡 선택을 떠나 인게임으로 진입. 플레이 확정(GotoSceneData 정상 종료) / 로딩 씬 / RhythmGameController.Start 중
        /// 가장 먼저 도달한 지점에서 1회 전환되며, 이후 호출은 무시됩니다.
        /// </summary>
        public static void EnterGameplay(string reason)
        {
            if (IsInGameplayContext && !IsInSongSelectContext) return;
            IsInSongSelectContext = false;
            IsInGameplayContext = true;
            MelonLogger.Msg($"[HwaAssetManager] 컨텍스트 전환: 곡 선택 → 인게임 ({reason})");
        }

        /// <summary>선택된 곡 ID/제목으로 커스텀 곡을 판정하고 Active를 갱신합니다.</summary>
        public static bool SetActiveTrack(string activeTrackID, string activeTitle = null)
        {
            var active = CustomSongLibrary.SetActive(activeTrackID, activeTitle);
            if (active != null)
            {
                // 곡 선택 씬에서 커스텀 트랙이 선택되는 즉시 BGM 비동기 스트리밍 사전 로딩(Preload) 개시
                // 이미 로드되었거나 로딩 중이면 중복 코루틴을 시작하지 않는다.
                if (active.BgmClip == null && !active.IsBgmLoading && !string.IsNullOrEmpty(active.BgmFilePath))
                {
                    MelonCoroutines.Start(active.LoadBgmCoroutine(null, false));
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// TrackAssetManager의 오디오 로드 요청을 커스텀 BGM으로 바꿔야 하는지.
        /// 커스텀 곡이 활성이고, 요청 Key가 그 사본이 숙주 곡에서 물려받은 오디오 Key와 정확히 같을 때만 true.
        /// 곡 선택(프리뷰)과 인게임 모두 적용됩니다 — 1.2 인게임은 숙주 Key로 BGM을 다시 요청하므로 여기서 막지 않으면 숙주 곡이 섞입니다.
        /// 인접 곡 사전 로드처럼 다른 Key 요청은 절대 건드리지 않습니다.
        /// </summary>
        public static bool ShouldOverrideAudioKey(string audioKey)
        {
            var active = Active;
            return (IsInSongSelectContext || IsInGameplayContext) &&
                active != null &&
                !string.IsNullOrEmpty(active.AudioKey) &&
                string.Equals(active.AudioKey, audioKey, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// TrackAssetManager의 비디오 URL 요청을 커스텀 BGA로 바꿔야 하는지. 활성 커스텀 곡에 BGA가 있고,
        /// 요청 Key가 숙주 비디오 Key와 같을 때만 true (숙주 Key를 모르면 이전 동작대로 Key 검사를 생략).
        /// 1.2 인게임 BGA는 LoadVideo 훅이 아니라 이 경로로 로드되므로 인게임에서도 적용해야 합니다.
        /// </summary>
        public static bool ShouldOverrideVideoKey(string videoKey)
        {
            var active = Active;
            if (!(IsInSongSelectContext || IsInGameplayContext) || active == null || string.IsNullOrEmpty(active.BgaFileUrl)) return false;
            return string.IsNullOrEmpty(active.VideoKey) ||
                string.Equals(active.VideoKey, videoKey, StringComparison.OrdinalIgnoreCase);
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

            public int GetLevelForDifficulty(int difficultyIndex)
            {
                switch (difficultyIndex)
                {
                    case 0: return EasyLevel;
                    case 1: return NormalLevel;
                    case 2: return HardLevel;
                    default: return NormalLevel;
                }
            }
        }

        /// <summary>
        /// 활성 커스텀 곡 메타데이터와 커버를 GameData에 안전하게 주입합니다.
        /// 원본 오타 필드(NowTrackAurthor / NowTracLabel)가 향후 게임 패치에서 교정되더라도 폴백 리플렉션으로 동작합니다.
        /// </summary>
        public static void ApplyMetaToGameData(GameData gd, Sprite customCover = null, int? targetStar = null)
        {
            if (gd == null) return;
            var meta = CurrentMeta;

            Il2CppReflectionHelper.SetValueResilient(gd, () => gd.NowTrackTitle = meta.Title, "NowTrackTitle", MemberFallbacks.GameData.Title, meta.Title);
            Il2CppReflectionHelper.SetValueResilient(gd, () => gd.NowTrackAurthor = meta.Artist, "NowTrackAurthor", MemberFallbacks.GameData.Author, meta.Artist);
            Il2CppReflectionHelper.SetValueResilient(gd, () => gd.NowTrackPVAuthor = meta.BgaAuthor, "NowTrackPVAuthor", MemberFallbacks.GameData.PvAuthor, meta.BgaAuthor);
            Il2CppReflectionHelper.SetValueResilient(gd, () => gd.NowTrackScore = meta.ChartAuthor, "NowTrackScore", MemberFallbacks.GameData.Score, meta.ChartAuthor);
            Il2CppReflectionHelper.SetValueResilient(gd, () => gd.NowTracLabel = meta.Album, "NowTracLabel", MemberFallbacks.GameData.Label, meta.Album);

            if (targetStar.HasValue)
            {
                string levelStr = targetStar.Value.ToString();
                Il2CppReflectionHelper.SetValueResilient(gd, () => gd.NowTrackLevel = levelStr, "NowTrackLevel", MemberFallbacks.GameData.Level, levelStr);
            }

            if (customCover != null)
            {
                Il2CppReflectionHelper.SetValueResilient(gd, () => gd.NowTrackCover = customCover, "NowTrackCover", MemberFallbacks.GameData.Cover, customCover);
            }
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
                string name = img.name;
                if (string.IsNullOrEmpty(name)) continue;
                if (name.IndexOf("cover", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("track", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("jacket", StringComparison.OrdinalIgnoreCase) >= 0)
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
        // 난이도 별 표시 안전 래퍼 (Il2CppSystem.Nullable<Color> 언박싱 NRE 방어)
        // =========================================================================

        /// <summary>
        /// 게임 1.2의 <c>DiffStarsCtrl.GenerateStarImages(int, Il2CppSystem.Nullable&lt;Color&gt; = null)</c>은
        /// C# <c>null</c>을 넘기면 Il2CppInterop 언박싱(<c>Il2CppObjectBaseToPtrNotNull</c>)에서
        /// <see cref="NullReferenceException"/>이 발생합니다.
        /// 반드시 <c>HasValue == false</c>인 빈 <c>Il2CppSystem.Nullable&lt;Color&gt;</c> 인스턴스를 전달합니다.
        /// </summary>
        public static void SafeGenerateStarImages(DiffStarsCtrl starsCtrl, int starCount)
        {
            if (starsCtrl == null) return;
            starsCtrl.GenerateStarImages(starCount, new Il2CppSystem.Nullable<Color>());
        }

        // =========================================================================
        // BGA (.mp4) 에셋 적용 및 인게임 오디오 음소거
        // =========================================================================

        /// <summary>
        /// BGA용 VideoPlayer의 자체 오디오 트랙을 완전히 차단합니다.
        /// 인게임(<c>RhythmGameController</c>)이나 결과 화면에서 <c>audioCom</c> BGM과
        /// <c>video.mp4</c> 내부 오디오가 동시에 재생되는 이중 BGM 버그를 방지합니다.
        /// </summary>
        public static void MuteVideoPlayer(VideoPlayer videoPlayer)
        {
            if (videoPlayer == null) return;
            try
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                videoPlayer.SetDirectAudioMute(0, true);
            }
            catch { }
        }

        public static bool ApplyCustomBga(VideoPlayer videoPlayer, bool autoPlay = true, bool muteAudio = false)
        {
            string url = BgaFileUrl;
            if (videoPlayer == null || string.IsNullOrEmpty(url)) return false;

            try
            {
                if (muteAudio)
                {
                    MuteVideoPlayer(videoPlayer);
                }

                if (videoPlayer.source == VideoSource.Url &&
                    string.Equals(videoPlayer.url, url, StringComparison.Ordinal))
                {
                    if (autoPlay && !videoPlayer.isPlaying)
                    {
                        videoPlayer.Play();
                    }
                    return true;
                }

                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = url;
                videoPlayer.Prepare();
                if (autoPlay)
                {
                    videoPlayer.Play();
                }
                MelonLogger.Msg($"[HwaAssetManager] ★ Custom BGA 주입 성공 ★ 오브젝트: '{videoPlayer.name}' | URL: '{url}'{(muteAudio ? " (음소거)" : "")}");
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

        /// <param name="onFailed">선택된 커스텀 곡이 없거나 BGM 로드에 실패했을 때 호출됩니다.</param>
        public static IEnumerator LoadCustomBgmCoroutine(AudioSource targetAudioSource, bool forcePlay = true, Action<AudioClip> onLoaded = null, Action onFailed = null)
        {
            return LoadCustomBgmForEntryCoroutine(Active, targetAudioSource, forcePlay, requireStillActive: false, onLoaded, onFailed);
        }

        /// <summary>
        /// 특정 <paramref name="requestedEntry"/>의 BGM을 비동기 로드합니다.
        /// <paramref name="requireStillActive"/>가 true이면 로드 완료 시점에 커서가 이미 다른 곡으로 이동한 경우
        /// 재생/콜백을 취소하여 빠른 스크롤 중 이전 커스텀 곡 BGM이 겹쳐 들리는 경쟁 상태를 방지합니다.
        /// </summary>
        public static IEnumerator LoadCustomBgmForEntryCoroutine(
            CustomSongEntry requestedEntry,
            AudioSource targetAudioSource,
            bool forcePlay = true,
            bool requireStillActive = false,
            Action<AudioClip> onLoaded = null,
            Action onFailed = null)
        {
            if (requestedEntry == null)
            {
                onFailed?.Invoke();
                yield break;
            }

            if (requireStillActive)
            {
                // targetAudioSource를 직접 넘기지 않고 로드만 수행한 뒤, 여전히 활성 곡일 때만 적용한다.
                AudioClip loadedClip = null;
                bool failed = false;
                var inner = requestedEntry.LoadBgmCoroutine(null, false, c => loadedClip = c, () => failed = true);
                while (inner.MoveNext())
                {
                    yield return inner.Current;
                }

                if (!IsInSongSelectContext || !ReferenceEquals(Active, requestedEntry))
                {
                    yield break;
                }

                if (failed || loadedClip == null)
                {
                    onFailed?.Invoke();
                    yield break;
                }

                if (targetAudioSource != null)
                {
                    targetAudioSource.Stop();
                    targetAudioSource.clip = loadedClip;
                    targetAudioSource.time = 0f;
                    if (forcePlay) targetAudioSource.Play();
                }

                onLoaded?.Invoke(loadedClip);
                yield break;
            }

            // 중첩 코루틴 지원 여부에 기대지 않고 내부 이터레이터를 직접 굴려 yield를 그대로 전달한다.
            var direct = requestedEntry.LoadBgmCoroutine(targetAudioSource, forcePlay, onLoaded, onFailed);
            while (direct.MoveNext())
            {
                yield return direct.Current;
            }
        }

        public static void ResetCache()
        {
            foreach (var entry in CustomSongLibrary.Entries)
            {
                entry.ResetLoadedAssets();
            }
            EnterSongSelect();
        }
    }
}
