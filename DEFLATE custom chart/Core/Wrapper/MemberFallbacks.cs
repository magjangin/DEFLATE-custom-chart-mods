namespace DEFLATE_custom_chart.Core.Wrapper
{
    /// <summary>
    /// 게임 업데이트로 인한 Il2Cpp 필드/프로퍼티/메서드 이름 변경 시
    /// 모든 훅과 래퍼가 공유하는 백업 리플렉션 후보군 단일 정의(Single Source of Truth).
    /// 패치로 이름이 바뀌면 이 파일만 갱신하면 전체 모드에 즉시 반영됩니다.
    /// </summary>
    public static class MemberFallbacks
    {
        public static class TrackBlock
        {
            public static readonly string[] UniqueId = { "UniqueID", "id", "m_UniqueID", "trackID", "_uniqueID", "track_id", "m_trackID" };
            public static readonly string[] Title = { "trackTitle", "Title", "title", "m_TrackTitle", "_trackTitle", "m_title", "songTitle", "SongTitle" };
            public static readonly string[] Artist = { "trackAuthor", "Artist", "artist", "Author", "author", "m_TrackAuthor", "m_artist", "songAuthor", "SongAuthor" };
            public static readonly string[] Album = { "trackAlbum", "Album", "album", "AlbumName", "m_TrackAlbum", "m_album" };
            public static readonly string[] DiscId = { "discID", "DiscId", "discId", "m_DiscID" };
            public static readonly string[] AudioKey = { "audioClipKey", "audioKey", "AudioKey", "m_audioClip_Key", "m_audioKey" };
            public static readonly string[] VideoKey = { "videoClipKey", "videoKey", "VideoKey", "m_videoClip_Key", "pv_Key", "m_videoKey" };
            public static readonly string[] EzKoreKey = { "ez_TrackKore_Key", "ezTrackKoreKey", "ezKoreKey", "EZ_KoreKey" };
            public static readonly string[] NmKoreKey = { "nm_TrackKore_Key", "nmTrackKoreKey", "nmKoreKey", "NM_KoreKey" };
            public static readonly string[] HdKoreKey = { "hd_TrackKore_Key", "hdTrackKoreKey", "hdKoreKey", "HD_KoreKey" };
            public static readonly string[] Cover = { "trackCover", "Cover", "cover", "m_TrackCover", "CoverSprite", "m_cover", "jacket", "Jacket" };
            public static readonly string[] EzStar = { "ez_Star", "ezStar", "EasyStar", "m_EZ_Star" };
            public static readonly string[] NmStar = { "nm_Star", "nmStar", "NormalStar", "m_NM_Star" };
            public static readonly string[] HdStar = { "hd_Star", "hdStar", "HardStar", "m_HD_Star" };
            public static readonly string[] Description = { "description", "Desc", "desc", "m_Description" };
            public static readonly string[] PvAuthor = { "trackPVAuthor", "PVAuthor", "pvAuthor", "BgaAuthor", "m_TrackPVAuthor", "bgaAuthor" };
            public static readonly string[] ChartMaker = { "trackScore", "ChartAuthor", "ChartMaker", "ScoreAuthor", "m_TrackScore", "chartMaker" };
            public static readonly string[] TrackLabel = { "trackLabel", "Label", "label", "m_TrackLabel" };
            public static readonly string[] RegenerateIdMethod = { "GenerateID", "RefreshID", "ResetID", "RegenerateId" };
        }

        public static class GameData
        {
            public static readonly string[] TrackId = { "nowTrackID", "TrackID", "uniqueID", "m_nowTrackID", "currentTrackId" };
            public static readonly string[] Title = { "trackTitle", "Title", "nowTrackTitle", "m_nowTrackTitle" };
            public static readonly string[] Author = { "NowTrackAuthor", "TrackAuthor", "nowTrackAuthor", "nowTrackArtist", "NowTrackArtist" };
            public static readonly string[] PvAuthor = { "NowTrackPvAuthor", "TrackPVAuthor", "nowTrackPVAuthor", "nowTrackBgaAuthor", "NowTrackBgaAuthor" };
            public static readonly string[] Score = { "TrackScore", "nowTrackScore", "ChartAuthor", "ChartMaker", "nowTrackChartMaker" };
            public static readonly string[] Label = { "NowTrackLabel", "TrackLabel", "TrackAlbum", "nowTrackLabel", "nowTrackAlbum", "NowTrackAlbum" };
            public static readonly string[] Level = { "TrackLevel", "nowTrackLevel", "m_nowTrackLevel" };
            public static readonly string[] Cover = { "TrackCover", "nowTrackCover", "m_nowTrackCover" };
        }

        public static class LoadingUi
        {
            public static readonly string[] Title = { "NowTrackTitle", "nowTrackTitle", "TrackTitle" };
            public static readonly string[] Author = { "NowTrackAuthor", "nowTrackAuthor", "TrackAuthor", "NowTrackArtist", "nowTrackArtist" };
            public static readonly string[] Label = { "NowTrackLabel", "nowTrackLabel", "TrackLabel", "NowTrackAlbum", "nowTrackAlbum" };
            public static readonly string[] PvAuthor = { "NowTrackPVAuthor", "nowTrackPVAuthor", "TrackPVAuthor", "NowTrackBgaAuthor" };
            public static readonly string[] Score = { "NowTrackScore", "nowTrackScore", "TrackScore", "NowTrackChartMaker" };
            public static readonly string[] Difficulty = { "NowTrackDifficulty", "nowTrackDifficulty", "TrackDifficulty" };
            public static readonly string[] Cover = { "NowTrackCover", "nowTrackCover", "TrackCover" };
            public static readonly string[] CoverBg = { "NowTrackCover_bg", "nowTrackCover_bg", "TrackCover_bg" };
        }

        public static class Lane
        {
            /// <summary>LaneController의 레인 종류 열거형 멤버. 열거형 타입 선언 위치가 바뀌어도 값은 이 이름으로 읽는다.</summary>
            public static readonly string[] LaneType = { "LaneType", "m_laneType", "_laneType", "type" };
        }

        public static class TrackList
        {
            public static readonly string[] NowSongId = { "nowSongID", "NowTrackID", "CurrentSongID", "m_nowSongID" };
            public static readonly string[] BlockCtrlUniqueId = { "uniqueID", "m_UniqueID", "trackID", "_uniqueID" };
        }

        public static class Koreography
        {
            public static readonly string[] SourceClip = { "mSourceClip", "sourceClip", "audioClip", "_sourceClip" };
        }
    }
}
