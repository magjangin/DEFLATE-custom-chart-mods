using System;
using System.Collections;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>
    /// hwa/ 아래 폴더 하나 = 커스텀 곡 하나. 해당 폴더의 에셋 경로/메타데이터/로드된 리소스를 전부 들고 있습니다.
    /// (기존 HwaAssetManager의 단일 곡 static 상태를 곡 단위로 쪼갠 것)
    /// </summary>
    public class CustomSongEntry
    {
        /// <summary>이 곡의 에셋이 들어있는 폴더 경로.</summary>
        public string FolderPath { get; private set; }

        /// <summary>곡 폴더의 상위 앨범 폴더 이름. 앨범 폴더 없이 배치된 곡이면 null.</summary>
        public string AlbumFolderName { get; private set; }

        /// <summary>폴더 이름 (앨범 폴더 판별 및 기본 제목/로그용).</summary>
        public string FolderName { get; private set; }

        /// <summary>디렉터리 경로에서 DirectoryInfo 할당 없이 마지막 폴더 이름만 추출합니다.</summary>
        public static string GetDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);
            return string.IsNullOrEmpty(name) ? trimmed : name;
        }

        /// <summary>로컬 파일 경로를 특수문자(#, % 등)가 안전하게 인코딩된 file:// URI로 변환합니다.</summary>
        public static string BuildFileUri(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;
            try
            {
                return new Uri(Path.GetFullPath(filePath)).AbsoluteUri;
            }
            catch
            {
                return "file:///" + filePath.Replace('\\', '/');
            }
        }

        // 에셋 파일 경로
        public string BgmFilePath { get; private set; }
        public string BgaFilePath { get; private set; }
        public string BgaFileUrl { get; private set; }
        public string CoverFilePath { get; private set; }
        public string InfoFilePath { get; private set; }
        public string BmsFilePath { get; private set; }

        // 파싱/로드된 리소스
        public HwaAssetManager.HwaMetaInfo Meta { get; private set; } = new HwaAssetManager.HwaMetaInfo();
        public Bms.BmsChart Chart { get; private set; }
        public Sprite CoverSprite { get; private set; }
        public AudioClip BgmClip { get; private set; }

        /// <summary>이 곡이 주입된 곡 목록 사본(MainTrackListBlock)의 uniqueID.</summary>
        public string InjectedTrackID { get; set; }

        /// <summary>사본이 원본에서 복사해온 오디오/비디오 Addressables Key (프리뷰 오버라이드 이중 확인용).</summary>
        public string AudioKey { get; set; }
        public string VideoKey { get; set; }

        private bool _isBgmLoading;

        /// <summary>
        /// 스트리밍(<c>streamAudio=true</c>) 클립의 데이터를 들고 있는 요청. 클립이 살아 있는 동안 Dispose하지 않습니다.
        /// 예전에는 클립을 받자마자 요청을 Dispose해, 스트리밍 클립이 참조하는 다운로드 버퍼가 곡 도중 해제될 위험이 있었습니다
        /// (AGENTS.md §9 알려진 한계). 해제는 <see cref="ResetLoadedAssets"/>에서 클립과 함께 합니다.
        /// </summary>
        private UnityWebRequest _bgmRequest;
        public bool IsBgmLoading => _isBgmLoading;

        private static readonly char[] InfoSeparators = { ':', '=' };
        private static readonly string[] BgmKeywords = { "music", "bgm", "song", "track", "audio" };

        private CustomSongEntry(string folderPath, string albumFolderName)
        {
            FolderPath = folderPath;
            AlbumFolderName = albumFolderName;
            FolderName = GetDirectoryName(folderPath);
        }

        private static readonly string[] CoverKeywords = { "cover", "jacket", "thumbnail", "thumb", "커버", "자켓" };

        /// <summary>커버 후보 순위: 이름에 커버 키워드가 있는 PNG/JPG = 2, 그 외 PNG = 1, 그 외(JPG) = 0(후보 아님).</summary>
        private static int CoverRank(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            foreach (var kw in CoverKeywords)
            {
                if (name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            }
            return string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        private static bool HasBgmKeyword(string fileNameWithoutExt)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExt)) return false;
            foreach (var kw in BgmKeywords)
            {
                if (fileNameWithoutExt.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// 폴더를 스캔해 커스텀 곡 엔트리를 만듭니다. 곡 폴더로 볼 근거가 없으면 null을 돌려줍니다.
        ///
        /// 곡 폴더 판정 기준: info.txt / BMS 차트 / 커버 PNG / BGA MP4 중 하나라도 직접 들어있을 것.
        /// 오디오 파일만 잔뜩 있는 폴더는 곡이 아니라 키음(keysound) 보관함으로 보고 제외합니다
        /// (예: hwa/홀드 모음/ 처럼 .wav만 모아둔 폴더).
        /// </summary>
        public static CustomSongEntry TryCreate(string folderPath, string albumFolderName, bool isRootLegacy = false)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return null;

            var entry = new CustomSongEntry(folderPath, albumFolderName) { IsRootLegacyEntry = isRootLegacy };
            entry.ScanFiles();

            bool isSongFolder = entry.InfoFilePath != null || entry.BmsFilePath != null ||
                entry.CoverFilePath != null || entry.BgaFilePath != null;
            if (!isSongFolder) return null;

            entry.ParseInfoTxt();
            entry.ParseBmsChart();
            return entry;
        }

        /// <summary>폴더 안의 파일들을 단일 패스로 BGM/BGA/커버/info/BMS로 분류합니다 (하위 폴더는 보지 않습니다).</summary>
        public void ScanFiles()
        {
            BgmFilePath = null;
            BgaFilePath = null;
            BgaFileUrl = null;
            CoverFilePath = null;
            InfoFilePath = null;
            BmsFilePath = null;

            if (!Directory.Exists(FolderPath)) return;

            string[] files = Directory.GetFiles(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            // BGM 음원 파일 선별 (.ogg 최우선 채택 ➔ music/bgm 키워드 ➔ 최대 용량 오디오 순)
            // (.flac은 UnityWebRequestMultimedia가 읽지 못하므로 후보에서 제외)
            int coverRank = 0;
            string bestBgm = null;
            bool bestIsOgg = false, bestHasKeyword = false;
            long bestSize = -1;

            foreach (var f in files)
            {
                string ext = Path.GetExtension(f);
                if (string.IsNullOrEmpty(ext)) continue;

                bool isOgg = string.Equals(ext, ".ogg", StringComparison.OrdinalIgnoreCase);
                if (isOgg || string.Equals(ext, ".wav", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    // 이미 .ogg가 확정되었으면 비-.ogg 키음 파일(.wav 등)은 즉시 건너뛰어 FileInfo/문자열 할당을 피한다.
                    if (bestBgm != null && bestIsOgg && !isOgg) continue;

                    bool hasKeyword = HasBgmKeyword(Path.GetFileNameWithoutExtension(f));
                    if (bestBgm != null && isOgg == bestIsOgg && bestHasKeyword && !hasKeyword) continue;

                    bool better;
                    long size = -1;
                    if (bestBgm == null || isOgg != bestIsOgg || hasKeyword != bestHasKeyword)
                    {
                        better = bestBgm == null || (isOgg != bestIsOgg ? isOgg : hasKeyword);
                    }
                    else
                    {
                        if (bestSize < 0) bestSize = new FileInfo(bestBgm).Length;
                        size = new FileInfo(f).Length;
                        better = size > bestSize;
                    }

                    if (better)
                    {
                        bestBgm = f;
                        bestIsOgg = isOgg;
                        bestHasKeyword = hasKeyword;
                        bestSize = size;
                    }
                    continue;
                }

                if (BgaFilePath == null && string.Equals(ext, ".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    BgaFilePath = f;
                    BgaFileUrl = BuildFileUri(f);
                }
                else if (string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    // 커버 이름(cover/jacket/thumbnail/thumb)이 붙은 이미지를 먼저 쓰고, 없으면 처음 나온 PNG를 쓴다.
                    // (예전에는 정렬상 첫 PNG만 잡아, BMS 배경 이미지 등이 커버로 뽑힐 수 있었음)
                    int rank = CoverRank(f);
                    if (rank > 0 && (CoverFilePath == null || rank > coverRank))
                    {
                        CoverFilePath = f;
                        coverRank = rank;
                    }
                }
                else if (string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase) &&
                    (InfoFilePath == null || string.Equals(Path.GetFileName(f), "info.txt", StringComparison.OrdinalIgnoreCase)))
                {
                    // info.txt가 있으면 그것을, 없으면 처음 나온 .txt를 메타데이터 파일로 쓴다
                    // (정렬상 info.txt보다 앞서는 readme.txt 등이 먼저 잡히는 것 방지).
                    InfoFilePath = f;
                }
                else if (BmsFilePath == null &&
                    (string.Equals(ext, ".bms", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(ext, ".bme", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(ext, ".bml", StringComparison.OrdinalIgnoreCase)))
                {
                    BmsFilePath = f;
                }
            }
            BgmFilePath = bestBgm;
        }

        /// <summary>info.txt에 아티스트가 있었는지. 없으면 BMS #ARTIST로 채운다(<see cref="ParseBmsChart"/>).</summary>
        private bool _artistFromInfo;

        /// <summary>
        /// info.txt를 파싱합니다. 제목/앨범이 비어 있으면 폴더 이름 규칙으로 채웁니다.
        /// (제목 = 곡 폴더 이름, 앨범 = 상위 앨범 폴더 이름)
        /// </summary>
        public void ParseInfoTxt()
        {
            Meta = new HwaAssetManager.HwaMetaInfo();
            _artistFromInfo = false;

            bool titleFromFile = false;
            bool albumFromFile = false;

            if (!string.IsNullOrEmpty(InfoFilePath) && File.Exists(InfoFilePath))
            {
                try
                {
                    foreach (var line in TextDecoder.ReadAllLines(InfoFilePath, TextDecoder.Preference.Korean))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string[] parts = line.Split(InfoSeparators, 2);
                        if (parts.Length < 2) continue;

                        string key = parts[0].Trim().TrimStart('\uFEFF').ToLowerInvariant();
                        string val = parts[1].Trim();
                        if (string.IsNullOrEmpty(val)) continue;

                        // BGA 키를 아티스트/매퍼보다 먼저 본다. ("BGA 아티스트"가 아티스트로, "영상 제작자"가 매퍼로 들어가는 것 방지)
                        bool isBgaKey = key.Contains("bga") || key.Contains("pv") ||
                            ((key.Contains("영상") || key.Contains("video")) &&
                             (key.Contains("제작") || key.Contains("author") || key.Contains("maker")));

                        if (key.Contains("제목") || key.Contains("title")) { Meta.Title = val; titleFromFile = true; }
                        else if (isBgaKey) Meta.BgaAuthor = val;
                        else if (key.Contains("아티스트") || key.Contains("artist")) { Meta.Artist = val; _artistFromInfo = true; }
                        else if (key.Contains("앨범") || key.Contains("album")) { Meta.Album = val; albumFromFile = true; }
                        else if (key.Contains("매퍼") || key.Contains("mapper") || key.Contains("제작자") || key.Contains("maker") || key.Contains("charter")) Meta.ChartAuthor = val;
                        else if (key.Equals("easy", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int ez)) Meta.EasyLevel = ez;
                        else if (key.Equals("normal", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int nm)) Meta.NormalLevel = nm;
                        else if (key.Equals("hard", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int hd)) Meta.HardLevel = hd;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[커스텀 곡] info.txt 파싱 예외 ('{FolderName}'): {ex.Message}");
                }
            }

            // info.txt에 값이 없으면 폴더 구조에서 유추한다.
            if (!titleFromFile && !IsRootLegacyEntry) Meta.Title = FolderName;
            if (!albumFromFile && !string.IsNullOrEmpty(AlbumFolderName)) Meta.Album = AlbumFolderName;
        }

        /// <summary>hwa/ 루트에 파일을 직접 둔 기존(단일 곡) 방식 엔트리인지 여부.</summary>
        public bool IsRootLegacyEntry { get; set; }

        public void ParseBmsChart(int targetSampleRate = 44100)
        {
            Chart = null;
            if (string.IsNullOrEmpty(BmsFilePath) || !File.Exists(BmsFilePath)) return;

            try
            {
                var parser = new Bms.BmsParser();
                Chart = parser.ParseFile(BmsFilePath, targetSampleRate);
                MelonLogger.Msg($"  - [BMS 파싱] '{Meta.Title}' ➔ '{Chart.Header.Title}' | BPM {Chart.Header.InitialBpm} | 노트 {Chart.Notes.Count}개 ({Chart.DescribeLaneDistribution()})"
                    + (Chart.KeysoundLaneMismatchCount > 0 ? $" | 킥·스네어 키음 이름과 열이 다른 노트 {Chart.KeysoundLaneMismatchCount}개(열 기준 배치)" : ""));
                ModLog.Verbose($"    홀드 매칭: {Chart.HoldPairedCount}개 성사 | 짝 없는 시작 {Chart.HoldOrphanHeadCount}개(단타 강등) | 짝 없는 끝 {Chart.HoldOrphanTailCount}개(제거)");
                if (!_artistFromInfo && !string.IsNullOrWhiteSpace(Chart.Header.Artist))
                {
                    // info.txt에 아티스트가 없으면 기본값("화영왕") 대신 BMS #ARTIST를 쓴다.
                    Meta.Artist = Chart.Header.Artist.Trim();
                }
                foreach (var warning in Chart.Warnings)
                {
                    MelonLogger.Warning($"    [BMS 경고] '{FolderName}' {warning}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[커스텀 곡] BMS 파싱 예외 ('{FolderName}'): {ex.Message}");
            }
        }

        // =========================================================================
        // 에셋 로딩 (커버 PNG / BGM 오디오)
        // =========================================================================

        public Sprite LoadCoverSprite()
        {
            if (CoverSprite != null) return CoverSprite;
            if (string.IsNullOrEmpty(CoverFilePath) || !File.Exists(CoverFilePath)) return null;

            try
            {
                byte[] fileData = File.ReadAllBytes(CoverFilePath);
                // UI 전용 스프라이트라 밉맵이 필요 없다(밉맵을 만들면 메모리 +33%, 디코딩 시간도 늘어남).
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (ImageConversion.LoadImage(tex, fileData))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    CoverSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    CoverSprite.name = Path.GetFileNameWithoutExtension(CoverFilePath);
                    ModLog.Verbose($"[커스텀 곡] PNG 커버 로드: '{Meta.Title}' ({tex.width}x{tex.height})");
                }
                else
                {
                    UnityEngine.Object.Destroy(tex);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[커스텀 곡] PNG 커버 로드 오류 ('{FolderName}'): {ex.Message}");
            }

            return CoverSprite;
        }

        private static AudioType GetAudioType(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return AudioType.UNKNOWN;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".ogg":
                case ".oga":
                    return AudioType.OGGVORBIS;
                case ".wav":
                    return AudioType.WAV;
                case ".mp3":
                    return AudioType.MPEG;
                case ".aif":
                case ".aiff":
                    return AudioType.AIFF;
                default:
                    return AudioType.UNKNOWN;
            }
        }

        /// <param name="onFailed">BGM 파일이 없거나 로드에 실패해 <paramref name="onLoaded"/>가 불리지 않을 때 대신 호출됩니다.</param>
        public IEnumerator LoadBgmCoroutine(AudioSource targetAudioSource, bool forcePlay = true, Action<AudioClip> onLoaded = null, Action onFailed = null)
        {
            if (string.IsNullOrEmpty(BgmFilePath) || !File.Exists(BgmFilePath))
            {
                onFailed?.Invoke();
                yield break;
            }

            if (BgmClip != null)
            {
                ApplyLoadedBgm(targetAudioSource, forcePlay, onLoaded, fromCache: true);
                yield break;
            }

            // 이미 다른 곳에서 로딩 중이면 그 결과를 기다렸다가 동일하게 적용/콜백한다. (고주사율 환경 대응 10초 + 3000프레임 가드)
            if (_isBgmLoading)
            {
                float startTime = Time.realtimeSinceStartup;
                int waitFrames = 0;
                while (_isBgmLoading && (Time.realtimeSinceStartup - startTime) < 10.0f && waitFrames < 3000)
                {
                    waitFrames++;
                    yield return null;
                }
                if (BgmClip != null) ApplyLoadedBgm(targetAudioSource, forcePlay, onLoaded, fromCache: true);
                else onFailed?.Invoke();
                yield break;
            }

            _isBgmLoading = true;
            bool loaded = false;
            UnityWebRequest www = null;

            try
            {
                string uri = BuildFileUri(BgmFilePath);
                AudioType audioType = GetAudioType(BgmFilePath);
                www = UnityWebRequestMultimedia.GetAudioClip(uri, audioType);

                var dh = www.downloadHandler as DownloadHandlerAudioClip;
                if (dh != null)
                {
                    dh.streamAudio = true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[커스텀 곡] BGM UnityWebRequest 생성 실패 ('{Meta.Title}'): {ex.Message}");
                www?.Dispose();
                _isBgmLoading = false;
                onFailed?.Invoke();
                yield break;
            }

            yield return www.SendWebRequest();

            try
            {
                if (www.result == UnityWebRequest.Result.Success)
                {
                    BgmClip = DownloadHandlerAudioClip.GetContent(www);
                    if (BgmClip != null)
                    {
                        BgmClip.name = Path.GetFileNameWithoutExtension(BgmFilePath);
                        ApplyLoadedBgm(targetAudioSource, forcePlay, onLoaded, fromCache: false);
                        loaded = true;
                    }
                    else
                    {
                        MelonLogger.Error($"[커스텀 곡] BGM 클립 변환 실패 ('{Meta.Title}'): 요청은 성공했지만 AudioClip이 비어 있습니다.");
                    }
                }
                else
                {
                    MelonLogger.Error($"[커스텀 곡] BGM 비동기 로드 실패 ('{Meta.Title}'): {www.error}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[커스텀 곡] BGM 후처리 예외 ('{Meta.Title}'): {ex.Message}");
            }
            finally
            {
                // 성공하면 스트리밍 클립이 요청의 버퍼를 쓰므로 클립과 수명을 같이한다. 실패한 요청만 즉시 해제.
                if (loaded) _bgmRequest = www;
                else www?.Dispose();
                _isBgmLoading = false;
            }

            if (!loaded) onFailed?.Invoke();
        }

        private void ApplyLoadedBgm(AudioSource targetAudioSource, bool forcePlay, Action<AudioClip> onLoaded, bool fromCache)
        {
            if (targetAudioSource != null)
            {
                if (targetAudioSource.clip != BgmClip || !targetAudioSource.isPlaying)
                {
                    targetAudioSource.Stop();
                    targetAudioSource.clip = BgmClip;
                    targetAudioSource.time = 0f;
                    if (forcePlay) targetAudioSource.Play();
                }
            }

            string tag = targetAudioSource == null && !forcePlay
                ? (fromCache ? "Custom BGM 캐시 확인" : "Custom BGM 사전 로드 완료")
                : (fromCache ? "캐시된 Custom BGM 즉시 적용" : "★ Custom BGM 주입 및 재생 성공 ★");
            ModLog.Verbose($"[커스텀 곡] {tag}: '{Meta.Title}' ➔ '{BgmClip.name}' ({BgmClip.length:F2}초)");

            onLoaded?.Invoke(BgmClip);
        }

        public void ResetLoadedAssets()
        {
            if (CoverSprite != null)
            {
                if (CoverSprite.texture != null)
                {
                    UnityEngine.Object.Destroy(CoverSprite.texture);
                }
                UnityEngine.Object.Destroy(CoverSprite);
                CoverSprite = null;
            }

            if (BgmClip != null)
            {
                UnityEngine.Object.Destroy(BgmClip);
                BgmClip = null;
            }

            if (_bgmRequest != null)
            {
                try { _bgmRequest.Dispose(); } catch (Exception ex) { ModLog.Debug($"[커스텀 곡] BGM 요청 해제 생략 ({ex.GetType().Name})"); }
                _bgmRequest = null;
            }

            _isBgmLoading = false;
        }
    }
}
