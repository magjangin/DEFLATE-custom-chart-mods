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
        public string FolderName => string.IsNullOrEmpty(FolderPath) ? string.Empty : new DirectoryInfo(FolderPath).Name;

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

        private CustomSongEntry(string folderPath, string albumFolderName)
        {
            FolderPath = folderPath;
            AlbumFolderName = albumFolderName;
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

        /// <summary>폴더 안의 파일들을 BGM/BGA/커버/info/BMS로 분류합니다 (하위 폴더는 보지 않습니다).</summary>
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

            // BGM 음원 파일 선별 (.ogg 최우선 채택 ➔ music/bgm 키워드 ➔ 최대 용량 오디오 순)
            string bestBgm = null;
            long maxBgmSize = 0;

            foreach (var f in files)
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                string name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();

                if (ext == ".wav" || ext == ".mp3" || ext == ".ogg" || ext == ".flac")
                {
                    // 1. .ogg 확장자 파일 최우선 채택 (BMS용 단품 .wav 키음들과 명확히 분리)
                    if (ext == ".ogg")
                    {
                        bestBgm = f;
                        break;
                    }

                    // 2. music, bgm, song, track 이름 포함 시 2순위 BGM으로 채택
                    if (name.Contains("music") || name.Contains("bgm") || name.Contains("song") || name.Contains("track") || name.Contains("audio"))
                    {
                        bestBgm = f;
                        break;
                    }

                    // 3. 파일 용량이 가장 큰 오디오를 BGM 후보로 추적
                    var fi = new FileInfo(f);
                    if (fi.Length > maxBgmSize)
                    {
                        maxBgmSize = fi.Length;
                        bestBgm = f;
                    }
                }
            }
            BgmFilePath = bestBgm;

            foreach (var f in files)
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                string name = Path.GetFileName(f).ToLowerInvariant();

                if (BgaFilePath == null && ext == ".mp4")
                {
                    BgaFilePath = f;
                    BgaFileUrl = "file:///" + f.Replace('\\', '/');
                }
                else if (CoverFilePath == null && ext == ".png")
                {
                    CoverFilePath = f;
                }
                else if (InfoFilePath == null && (name == "info.txt" || ext == ".txt"))
                {
                    InfoFilePath = f;
                }
                else if (BmsFilePath == null && (ext == ".bms" || ext == ".bme" || ext == ".bml"))
                {
                    BmsFilePath = f;
                }
            }
        }

        /// <summary>
        /// info.txt를 파싱합니다. 제목/앨범이 비어 있으면 폴더 이름 규칙으로 채웁니다.
        /// (제목 = 곡 폴더 이름, 앨범 = 상위 앨범 폴더 이름)
        /// </summary>
        public void ParseInfoTxt()
        {
            Meta = new HwaAssetManager.HwaMetaInfo();

            bool titleFromFile = false;
            bool albumFromFile = false;

            if (!string.IsNullOrEmpty(InfoFilePath) && File.Exists(InfoFilePath))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(InfoFilePath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string[] parts = line.Split(new char[] { ':', '=' }, 2);
                        if (parts.Length < 2) continue;

                        string key = parts[0].Trim().ToLowerInvariant();
                        string val = parts[1].Trim();
                        if (string.IsNullOrEmpty(val)) continue;

                        if (key.Contains("제목") || key.Contains("title")) { Meta.Title = val; titleFromFile = true; }
                        else if (key.Contains("아티스트") || key.Contains("artist")) Meta.Artist = val;
                        else if (key.Contains("앨범") || key.Contains("album")) { Meta.Album = val; albumFromFile = true; }
                        else if (key.Contains("bga") || key.Contains("pv")) Meta.BgaAuthor = val;
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
                MelonLogger.Msg($"  - [BMS 파싱] '{Meta.Title}' ➔ '{Chart.Header.Title}' | BPM {Chart.Header.InitialBpm} | 노트 {Chart.Notes.Count}개");
                MelonLogger.Msg($"    홀드 매칭: {Chart.HoldPairedCount}개 성사 | 짝 없는 시작 {Chart.HoldOrphanHeadCount}개(단타 강등) | 짝 없는 끝 {Chart.HoldOrphanTailCount}개(제거)");
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
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (ImageConversion.LoadImage(tex, fileData))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    CoverSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    CoverSprite.name = Path.GetFileNameWithoutExtension(CoverFilePath);
                    MelonLogger.Msg($"[커스텀 곡] PNG 커버 로드: '{Meta.Title}' ({tex.width}x{tex.height})");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[커스텀 곡] PNG 커버 로드 오류 ('{FolderName}'): {ex.Message}");
            }

            return CoverSprite;
        }

        public IEnumerator LoadBgmCoroutine(AudioSource targetAudioSource, bool forcePlay = true, Action<AudioClip> onLoaded = null)
        {
            if (string.IsNullOrEmpty(BgmFilePath) || !File.Exists(BgmFilePath)) yield break;

            if (BgmClip != null)
            {
                ApplyLoadedBgm(targetAudioSource, forcePlay, onLoaded, fromCache: true);
                yield break;
            }

            // 이미 다른 곳에서 로딩 중이면 그 결과를 기다렸다가 동일하게 적용/콜백한다.
            if (_isBgmLoading)
            {
                while (_isBgmLoading) yield return null;
                if (BgmClip != null) ApplyLoadedBgm(targetAudioSource, forcePlay, onLoaded, fromCache: true);
                yield break;
            }

            _isBgmLoading = true;

            string uri = "file:///" + BgmFilePath.Replace('\\', '/');
            UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                BgmClip = DownloadHandlerAudioClip.GetContent(www);
                if (BgmClip != null)
                {
                    BgmClip.name = Path.GetFileNameWithoutExtension(BgmFilePath);
                    ApplyLoadedBgm(targetAudioSource, forcePlay, onLoaded, fromCache: false);
                }
            }
            else
            {
                MelonLogger.Error($"[커스텀 곡] BGM 비동기 로드 실패 ('{Meta.Title}'): {www.error}");
            }

            www.Dispose();
            _isBgmLoading = false;
        }

        private void ApplyLoadedBgm(AudioSource targetAudioSource, bool forcePlay, Action<AudioClip> onLoaded, bool fromCache)
        {
            if (targetAudioSource != null)
            {
                targetAudioSource.Stop();
                targetAudioSource.clip = BgmClip;
                targetAudioSource.time = 0f;
                if (forcePlay) targetAudioSource.Play();
            }

            string tag = fromCache ? "캐시된 Custom BGM 즉시 적용" : "★ Custom BGM 주입 및 재생 성공 ★";
            MelonLogger.Msg($"[커스텀 곡] {tag}: '{Meta.Title}' ➔ '{BgmClip.name}' ({BgmClip.length:F2}초)");

            onLoaded?.Invoke(BgmClip);
        }

        /// <summary>주어진 곡 ID / 제목이 이 엔트리에 해당하는지 판정합니다.</summary>
        public bool Matches(string trackID, string title)
        {
            if (!string.IsNullOrEmpty(InjectedTrackID) && !string.IsNullOrEmpty(trackID) &&
                string.Equals(InjectedTrackID, trackID, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrEmpty(Meta?.Title) && !string.IsNullOrEmpty(title) &&
                string.Equals(Meta.Title, title, StringComparison.OrdinalIgnoreCase);
        }

        public void ResetLoadedAssets()
        {
            CoverSprite = null;
            BgmClip = null;
            _isBgmLoading = false;
        }
    }
}
