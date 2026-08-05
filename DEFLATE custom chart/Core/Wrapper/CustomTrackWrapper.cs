using System;
using UnityEngine;
using Il2Cppdizzylab.castor;

namespace DEFLATE_custom_chart.Core.Wrapper
{
    /// <summary>
    /// Il2Cpp MainTrackListBlock / TrackData 객체를 감싸는 C# 전용 래퍼 클래스
    /// </summary>
    public class CustomTrackWrapper
    {
        public string UniqueID { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string AlbumName { get; set; } = string.Empty;
        public string AudioKey { get; set; } = string.Empty;
        public string VideoKey { get; set; } = string.Empty;

        // 난이도별 차트 키
        public string EZ_KoreKey { get; set; } = string.Empty;
        public string NM_KoreKey { get; set; } = string.Empty;
        public string HD_KoreKey { get; set; } = string.Empty;

        // 커버 이미지 에셋
        public Sprite CoverSprite { get; set; }

        // 원본 Il2Cpp 참조 (캐스팅용)
        public MainTrackListBlock NativeBlock { get; private set; }

        public CustomTrackWrapper() { }

        public CustomTrackWrapper(MainTrackListBlock block)
        {
            Wrap(block);
        }

        /// <summary>
        /// Il2Cpp MainTrackListBlock 객체를 래퍼로 캡슐화합니다.
        /// </summary>
        public void Wrap(MainTrackListBlock block)
        {
            if (block == null) return;

            NativeBlock = block;
            UniqueID = block.uniqueID ?? string.Empty;
            Title = block.TrackTitle ?? string.Empty;
            Artist = block.TrackAuthor ?? string.Empty;
            AlbumName = block.TrackAlbum ?? string.Empty;
            AudioKey = block.audioClip_Key ?? string.Empty;
            VideoKey = block.videoClip_Key ?? string.Empty;
            EZ_KoreKey = block.EZ_TrackKore_Key ?? string.Empty;
            NM_KoreKey = block.NM_TrackKore_Key ?? string.Empty;
            HD_KoreKey = block.HD_TrackKore_Key ?? string.Empty;
            CoverSprite = block.TrackCover;
        }

        /// <summary>
        /// 래퍼 데이터를 타겟 Il2Cpp MainTrackListBlock 객체로 다시 캐스팅 및 주입(Apply)합니다.
        /// </summary>
        public void ApplyTo(MainTrackListBlock block)
        {
            if (block == null) return;

            block.uniqueID = UniqueID;
            block.TrackTitle = Title;
            block.TrackAuthor = Artist;
            block.TrackAlbum = AlbumName;
            block.audioClip_Key = AudioKey;
            block.videoClip_Key = VideoKey;
            block.EZ_TrackKore_Key = EZ_KoreKey;
            block.NM_TrackKore_Key = NM_KoreKey;
            block.HD_TrackKore_Key = HD_KoreKey;
            if (CoverSprite != null)
            {
                block.TrackCover = CoverSprite;
            }
        }
    }
}
