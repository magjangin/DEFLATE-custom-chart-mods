using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>
    /// hwa/ 폴더 전체를 훑어 커스텀 곡 카탈로그를 구성하고, 현재 선택된 곡(Active)을 관리합니다.
    ///
    /// 지원하는 폴더 배치 (깊이는 자동 판별):
    ///   hwa/bgm.ogg, info.txt ...        ➔ 기존 단일 곡 방식 (루트 직접 배치)
    ///   hwa/(곡 폴더)/...                 ➔ 앨범 없는 단독 곡
    ///   hwa/(앨범 폴더)/(곡 폴더)/...      ➔ 앨범별 폴더 (앨범명 = 폴더 이름, info.txt의 album이 있으면 그쪽 우선)
    /// </summary>
    public static class CustomSongLibrary
    {
        public static string HwaDirectoryPath => Path.Combine(MelonEnvironment.GameRootDirectory, "hwa");

        /// <summary>스캔된 전체 커스텀 곡 (앨범 ➔ 곡 순으로 정렬된 주입 순서).</summary>
        public static List<CustomSongEntry> Entries { get; } = new List<CustomSongEntry>();

        /// <summary>현재 선택/재생 중인 커스텀 곡. 커스텀 곡이 아닌 원본 곡을 고르면 null.</summary>
        public static CustomSongEntry Active { get; private set; }

        /// <summary>Active가 null일 때 각종 훅이 기본값으로 쓸 폴백 곡 (첫 번째 엔트리).</summary>
        public static CustomSongEntry Fallback => Entries.Count > 0 ? Entries[0] : null;

        public static bool HasSongs => Entries.Count > 0;

        public static void Initialize()
        {
            if (!Directory.Exists(HwaDirectoryPath))
            {
                try
                {
                    Directory.CreateDirectory(HwaDirectoryPath);
                    MelonLogger.Msg($"[커스텀 곡 라이브러리] hwa/ 폴더 생성: '{HwaDirectoryPath}'");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[커스텀 곡 라이브러리] hwa/ 폴더 생성 실패: {ex.Message}");
                    return;
                }
            }

            Scan();
        }

        public static void Scan()
        {
            Entries.Clear();
            Active = null;

            if (!Directory.Exists(HwaDirectoryPath)) return;

            // 1) hwa/ 루트에 직접 놓인 에셋 = 기존 방식의 단일 곡
            var rootEntry = CustomSongEntry.TryCreate(HwaDirectoryPath, null, isRootLegacy: true);
            if (rootEntry != null) Entries.Add(rootEntry);

            // 2) hwa/ 하위 폴더 순회 (앨범 폴더 / 단독 곡 폴더 자동 판별)
            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(HwaDirectoryPath);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[커스텀 곡 라이브러리] hwa/ 하위 폴더 조회 실패: {ex.Message}");
                return;
            }

            Array.Sort(subDirs, StringComparer.OrdinalIgnoreCase);

            foreach (var dir in subDirs)
            {
                // 폴더 안에 에셋이 직접 있으면 곡 폴더로 본다 (앨범 없음).
                var direct = CustomSongEntry.TryCreate(dir, null);
                if (direct != null)
                {
                    Entries.Add(direct);
                    continue;
                }

                // 에셋이 없고 하위 폴더만 있으면 앨범 폴더로 본다.
                string albumName = new DirectoryInfo(dir).Name;
                string[] songDirs;
                try
                {
                    songDirs = Directory.GetDirectories(dir);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[커스텀 곡 라이브러리] 앨범 폴더 '{albumName}' 조회 실패: {ex.Message}");
                    continue;
                }

                if (songDirs.Length == 0)
                {
                    // info.txt/차트/커버/BGA가 없는 폴더 (예: 키음 .wav 보관함) — 곡도 앨범도 아니므로 무시한다.
                    MelonLogger.Msg($"[커스텀 곡 라이브러리] 곡 폴더가 아니라 건너뜀: '{albumName}/'");
                    continue;
                }

                Array.Sort(songDirs, StringComparer.OrdinalIgnoreCase);

                int addedInAlbum = 0;
                foreach (var songDir in songDirs)
                {
                    var song = CustomSongEntry.TryCreate(songDir, albumName);
                    if (song != null) { Entries.Add(song); addedInAlbum++; }
                    else MelonLogger.Msg($"[커스텀 곡 라이브러리] 곡 에셋(info.txt/BMS/PNG/MP4)이 없어 건너뜀: '{albumName}/{new DirectoryInfo(songDir).Name}'");
                }

                if (addedInAlbum == 0)
                {
                    MelonLogger.Warning($"[커스텀 곡 라이브러리] 앨범 폴더 '{albumName}'에서 곡을 하나도 찾지 못했습니다.");
                }
            }

            LogCatalog();
        }

        private static void LogCatalog()
        {
            MelonLogger.Msg("==================================================");
            MelonLogger.Msg($"[커스텀 곡 라이브러리] hwa/ 스캔 완료 — 총 {Entries.Count}곡");

            foreach (var group in Entries.GroupBy(e => string.IsNullOrEmpty(e.Meta.Album) ? "(앨범 없음)" : e.Meta.Album))
            {
                MelonLogger.Msg($"  ▣ 앨범 '{group.Key}' ({group.Count()}곡)");
                foreach (var e in group)
                {
                    MelonLogger.Msg($"    - '{e.Meta.Title}' / {e.Meta.Artist} | 매퍼: {e.Meta.ChartAuthor} | ★{e.Meta.EasyLevel}/{e.Meta.NormalLevel}/{e.Meta.HardLevel}");
                    MelonLogger.Msg($"      BGM: {NameOr(e.BgmFilePath)} | BGA: {NameOr(e.BgaFilePath)} | 커버: {NameOr(e.CoverFilePath)} | BMS: {NameOr(e.BmsFilePath)}");
                }
            }
            MelonLogger.Msg("==================================================");
        }

        private static string NameOr(string path) => string.IsNullOrEmpty(path) ? "없음" : Path.GetFileName(path);

        // =========================================================================
        // 활성 곡 판정
        // =========================================================================

        /// <summary>곡 ID / 제목으로 커스텀 곡을 찾아 Active로 지정합니다. 커스텀 곡이 아니면 Active를 비웁니다.</summary>
        public static CustomSongEntry SetActive(string trackID, string title)
        {
            foreach (var entry in Entries)
            {
                if (entry.Matches(trackID, title))
                {
                    Active = entry;
                    return entry;
                }
            }

            Active = null;
            return null;
        }

        public static CustomSongEntry FindByTrackID(string trackID)
        {
            if (string.IsNullOrEmpty(trackID)) return null;
            foreach (var entry in Entries)
            {
                if (!string.IsNullOrEmpty(entry.InjectedTrackID) &&
                    string.Equals(entry.InjectedTrackID, trackID, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }

        /// <summary>제목 + 앨범 조합으로 엔트리를 찾습니다 (곡 목록 사본 중복 주입 방지용).</summary>
        public static CustomSongEntry FindByTitleAndAlbum(string title, string album)
        {
            if (string.IsNullOrEmpty(title)) return null;
            foreach (var entry in Entries)
            {
                if (!string.Equals(entry.Meta.Title, title, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrEmpty(album) || string.Equals(entry.Meta.Album, album, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }

        public static void ClearInjectionState()
        {
            foreach (var entry in Entries)
            {
                entry.InjectedTrackID = null;
                entry.AudioKey = null;
                entry.VideoKey = null;
            }
            Active = null;
        }
    }
}
