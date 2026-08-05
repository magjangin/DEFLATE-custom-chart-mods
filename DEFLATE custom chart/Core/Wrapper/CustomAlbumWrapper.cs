using System.Collections.Generic;
using UnityEngine;

namespace DEFLATE_custom_chart.Core.Wrapper
{
    /// <summary>
    /// 커스텀 앨범(Pack/Category) 단위 래퍼 클래스
    /// </summary>
    public class CustomAlbumWrapper
    {
        public string AlbumID { get; set; } = string.Empty;
        public string AlbumTitle { get; set; } = string.Empty;
        public Sprite AlbumCover { get; set; }
        public List<CustomTrackWrapper> Tracks { get; } = new List<CustomTrackWrapper>();

        public CustomAlbumWrapper() { }

        public CustomAlbumWrapper(string albumId, string albumTitle)
        {
            AlbumID = albumId;
            AlbumTitle = albumTitle;
        }

        public void AddTrack(CustomTrackWrapper track)
        {
            if (track != null && !Tracks.Contains(track))
            {
                track.AlbumName = AlbumTitle;
                Tracks.Add(track);
            }
        }
    }

    /// <summary>
    /// 등록된 커스텀 앨범 카탈로그를 중앙에서 관리하는 래퍼 매니저
    /// </summary>
    public static class CustomAlbumManager
    {
        public static List<CustomAlbumWrapper> RegisteredAlbums { get; } = new List<CustomAlbumWrapper>();

        public static void RegisterAlbum(CustomAlbumWrapper album)
        {
            if (album != null && !RegisteredAlbums.Contains(album))
            {
                RegisteredAlbums.Add(album);
            }
        }

        public static CustomTrackWrapper FindTrack(string trackId)
        {
            foreach (var album in RegisteredAlbums)
            {
                foreach (var track in album.Tracks)
                {
                    if (track.UniqueID.Equals(trackId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return track;
                    }
                }
            }
            return null;
        }
    }
}
