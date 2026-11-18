using System;
using UnityEngine;
using MelonLoader;
using Il2Cppdizzylab.castor;
using DEFLATE_custom_chart.Core;

namespace DEFLATE_custom_chart.Core.Wrapper
{
    /// <summary>
    /// Il2Cpp MainTrackListBlock / TrackData 객체를 감싸는 C# 전용 내성 강화 래퍼 클래스
    /// </summary>
    public class CustomTrackWrapper
    {
        public string UniqueID { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string AlbumName { get; set; } = string.Empty;
        public string TrackLabel { get; set; } = string.Empty;
        public string PVAuthor { get; set; } = string.Empty;
        public string ChartMaker { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AudioKey { get; set; } = string.Empty;
        public string VideoKey { get; set; } = string.Empty;

        // 난이도별 차트 키
        public string EZ_KoreKey { get; set; } = string.Empty;
        public string NM_KoreKey { get; set; } = string.Empty;
        public string HD_KoreKey { get; set; } = string.Empty;

        // 난이도별 표기 레벨
        public int EZ_Star { get; set; }
        public int NM_Star { get; set; }
        public int HD_Star { get; set; }

        // 커버 이미지 에셋
        public Sprite CoverSprite { get; set; }

        // 원본 Il2Cpp 참조 (안전한 캐스팅 지원)
        public MainTrackListBlock NativeBlock { get; private set; }

        // 정적 캐시 폴백 배열 (매 호출 시 힙 할당 제거)
        private static readonly string[] UniqueIdFallbacks = { "UniqueID", "id", "m_UniqueID", "trackID", "_uniqueID" };
        private static readonly string[] TitleFallbacks = { "trackTitle", "Title", "title", "m_TrackTitle", "_trackTitle" };
        private static readonly string[] ArtistFallbacks = { "trackAuthor", "Artist", "artist", "Author", "author", "m_TrackAuthor" };
        private static readonly string[] AlbumFallbacks = { "trackAlbum", "Album", "album", "AlbumName", "m_TrackAlbum" };
        private static readonly string[] AudioKeyFallbacks = { "audioClipKey", "audioKey", "AudioKey", "m_audioClip_Key" };
        private static readonly string[] VideoKeyFallbacks = { "videoClipKey", "videoKey", "VideoKey", "m_videoClip_Key", "pv_Key" };
        private static readonly string[] EzKoreKeyFallbacks = { "ez_TrackKore_Key", "ezTrackKoreKey", "ezKoreKey", "EZ_KoreKey" };
        private static readonly string[] NmKoreKeyFallbacks = { "nm_TrackKore_Key", "nmTrackKoreKey", "nmKoreKey", "NM_KoreKey" };
        private static readonly string[] HdKoreKeyFallbacks = { "hd_TrackKore_Key", "hdTrackKoreKey", "hdKoreKey", "HD_KoreKey" };
        private static readonly string[] CoverFallbacks = { "trackCover", "Cover", "cover", "m_TrackCover", "CoverSprite" };
        private static readonly string[] EzStarFallbacks = { "ez_Star", "ezStar", "EasyStar", "m_EZ_Star" };
        private static readonly string[] NmStarFallbacks = { "nm_Star", "nmStar", "NormalStar", "m_NM_Star" };
        private static readonly string[] HdStarFallbacks = { "hd_Star", "hdStar", "HardStar", "m_HD_Star" };
        private static readonly string[] DescriptionFallbacks = { "description", "Desc", "desc", "m_Description" };
        private static readonly string[] PvAuthorFallbacks = { "trackPVAuthor", "PVAuthor", "pvAuthor", "BgaAuthor", "m_TrackPVAuthor" };
        private static readonly string[] ChartMakerFallbacks = { "trackScore", "ChartAuthor", "ChartMaker", "ScoreAuthor", "m_TrackScore" };
        private static readonly string[] TrackLabelFallbacks = { "trackLabel", "Label", "label", "m_TrackLabel" };

        public CustomTrackWrapper() { }

        public CustomTrackWrapper(object block)
        {
            Wrap(block);
        }

        private static T ReadBlockMember<T>(MainTrackListBlock nativeBlock, object targetObj, Func<MainTrackListBlock, T> directAccessor, string propertyName, string[] fallbacks, T defaultValue)
        {
            Func<T> directGetter = nativeBlock != null ? (Func<T>)(() => directAccessor(nativeBlock)) : null;
            return Il2CppReflectionHelper.GetValueResilient(targetObj, directGetter, propertyName, fallbacks, defaultValue);
        }

        private static void WriteBlockMember(MainTrackListBlock targetBlock, object targetObj, Action<MainTrackListBlock> directMutator, string propertyName, string[] fallbacks, object value)
        {
            Action directSetter = targetBlock != null ? (Action)(() => directMutator(targetBlock)) : null;
            Il2CppReflectionHelper.SetValueResilient(targetObj, directSetter, propertyName, fallbacks, value);
        }

        /// <summary>
        /// Il2Cpp 객체(MainTrackListBlock 등)를 안전하게 캐스트하고 백업 리플렉션을 포함하여 래퍼로 캡슐화합니다.
        /// SafeCast가 실패해 NativeBlock이 null이어도 directGetter를 null로 넘겨 백업 리플렉션 경로가 실제로 동작합니다.
        /// </summary>
        public void Wrap(object blockObj)
        {
            if (blockObj == null) return;

            // SafeCast로 타겟 타입 변환 시도 (실패해도 null 처리 후 백업 리플렉션 탐색)
            NativeBlock = Il2CppReflectionHelper.SafeCast<MainTrackListBlock>(blockObj);
            object targetObj = (object)NativeBlock ?? blockObj;

            UniqueID = ReadBlockMember(NativeBlock, targetObj, b => b.uniqueID, "uniqueID", UniqueIdFallbacks, string.Empty);
            Title = ReadBlockMember(NativeBlock, targetObj, b => b.TrackTitle, "TrackTitle", TitleFallbacks, string.Empty);
            Artist = ReadBlockMember(NativeBlock, targetObj, b => b.TrackAuthor, "TrackAuthor", ArtistFallbacks, string.Empty);
            AlbumName = ReadBlockMember(NativeBlock, targetObj, b => b.TrackAlbum, "TrackAlbum", AlbumFallbacks, string.Empty);
            AudioKey = ReadBlockMember(NativeBlock, targetObj, b => b.audioClip_Key, "audioClip_Key", AudioKeyFallbacks, string.Empty);
            VideoKey = ReadBlockMember(NativeBlock, targetObj, b => b.videoClip_Key, "videoClip_Key", VideoKeyFallbacks, string.Empty);
            EZ_KoreKey = ReadBlockMember(NativeBlock, targetObj, b => b.EZ_TrackKore_Key, "EZ_TrackKore_Key", EzKoreKeyFallbacks, string.Empty);
            NM_KoreKey = ReadBlockMember(NativeBlock, targetObj, b => b.NM_TrackKore_Key, "NM_TrackKore_Key", NmKoreKeyFallbacks, string.Empty);
            HD_KoreKey = ReadBlockMember(NativeBlock, targetObj, b => b.HD_TrackKore_Key, "HD_TrackKore_Key", HdKoreKeyFallbacks, string.Empty);
            CoverSprite = ReadBlockMember<Sprite>(NativeBlock, targetObj, b => b.TrackCover, "TrackCover", CoverFallbacks, null);
            EZ_Star = ReadBlockMember(NativeBlock, targetObj, b => b.EZ_Star, "EZ_Star", EzStarFallbacks, 0);
            NM_Star = ReadBlockMember(NativeBlock, targetObj, b => b.NM_Star, "NM_Star", NmStarFallbacks, 0);
            HD_Star = ReadBlockMember(NativeBlock, targetObj, b => b.HD_Star, "HD_Star", HdStarFallbacks, 0);
            Description = ReadBlockMember(NativeBlock, targetObj, b => b.Description, "Description", DescriptionFallbacks, string.Empty);
            PVAuthor = ReadBlockMember(NativeBlock, targetObj, b => b.TrackPVAuthor, "TrackPVAuthor", PvAuthorFallbacks, string.Empty);
            ChartMaker = ReadBlockMember(NativeBlock, targetObj, b => b.TrackScore, "TrackScore", ChartMakerFallbacks, string.Empty);
            TrackLabel = ReadBlockMember(NativeBlock, targetObj, b => b.TrackLabel, "TrackLabel", TrackLabelFallbacks, string.Empty);
        }

        /// <summary>
        /// 커스텀 곡의 info.txt 메타데이터와 자켓 스프라이트를 래퍼 속성에 한 번에 적용합니다.
        /// </summary>
        public void ApplySongMetadata(HwaAssetManager.HwaMetaInfo meta, Sprite customCover = null)
        {
            if (meta == null) return;

            Title = meta.Title;
            Artist = meta.Artist;
            AlbumName = meta.Album;
            TrackLabel = meta.Album;
            PVAuthor = meta.BgaAuthor;
            ChartMaker = meta.ChartAuthor;
            EZ_Star = meta.EasyLevel;
            NM_Star = meta.NormalLevel;
            HD_Star = meta.HardLevel;
            Description = $"BGA: {meta.BgaAuthor} | Chart: {meta.ChartAuthor} | Easy:{meta.EasyLevel} Normal:{meta.NormalLevel} Hard:{meta.HardLevel}";
            if (customCover != null)
            {
                CoverSprite = customCover;
            }
        }

        /// <summary>
        /// 래퍼 데이터를 타겟 Il2Cpp 객체로 안전하게 다시 캐스팅 및 주입(Apply)합니다.
        /// 게임 패치로 일부 프로퍼티가 수정되거나 SafeCast가 실패해도 directSetter를 null로 넘겨
        /// 백업 리플렉션으로 안전하게 주입하며, 예외가 격리되어 정상 속성들은 모두 유지됩니다.
        /// </summary>
        public void ApplyTo(object blockObj)
        {
            if (blockObj == null) return;

            var targetBlock = Il2CppReflectionHelper.SafeCast<MainTrackListBlock>(blockObj);
            object targetObj = (object)targetBlock ?? blockObj;

            WriteBlockMember(targetBlock, targetObj, b => b.uniqueID = UniqueID, "uniqueID", UniqueIdFallbacks, UniqueID);
            WriteBlockMember(targetBlock, targetObj, b => b.TrackTitle = Title, "TrackTitle", TitleFallbacks, Title);
            WriteBlockMember(targetBlock, targetObj, b => b.TrackAuthor = Artist, "TrackAuthor", ArtistFallbacks, Artist);
            WriteBlockMember(targetBlock, targetObj, b => b.TrackAlbum = AlbumName, "TrackAlbum", AlbumFallbacks, AlbumName);
            WriteBlockMember(targetBlock, targetObj, b => b.audioClip_Key = AudioKey, "audioClip_Key", AudioKeyFallbacks, AudioKey);
            WriteBlockMember(targetBlock, targetObj, b => b.videoClip_Key = VideoKey, "videoClip_Key", VideoKeyFallbacks, VideoKey);
            WriteBlockMember(targetBlock, targetObj, b => b.EZ_TrackKore_Key = EZ_KoreKey, "EZ_TrackKore_Key", EzKoreKeyFallbacks, EZ_KoreKey);
            WriteBlockMember(targetBlock, targetObj, b => b.NM_TrackKore_Key = NM_KoreKey, "NM_TrackKore_Key", NmKoreKeyFallbacks, NM_KoreKey);
            WriteBlockMember(targetBlock, targetObj, b => b.HD_TrackKore_Key = HD_KoreKey, "HD_TrackKore_Key", HdKoreKeyFallbacks, HD_KoreKey);

            if (CoverSprite != null)
            {
                WriteBlockMember(targetBlock, targetObj, b => b.TrackCover = CoverSprite, "TrackCover", CoverFallbacks, CoverSprite);
            }

            WriteBlockMember(targetBlock, targetObj, b => b.EZ_Star = EZ_Star, "EZ_Star", EzStarFallbacks, EZ_Star);
            WriteBlockMember(targetBlock, targetObj, b => b.NM_Star = NM_Star, "NM_Star", NmStarFallbacks, NM_Star);
            WriteBlockMember(targetBlock, targetObj, b => b.HD_Star = HD_Star, "HD_Star", HdStarFallbacks, HD_Star);
            WriteBlockMember(targetBlock, targetObj, b => b.Description = Description, "Description", DescriptionFallbacks, Description);
            WriteBlockMember(targetBlock, targetObj, b => b.TrackPVAuthor = PVAuthor, "TrackPVAuthor", PvAuthorFallbacks, PVAuthor);
            WriteBlockMember(targetBlock, targetObj, b => b.TrackScore = ChartMaker, "TrackScore", ChartMakerFallbacks, ChartMaker);
            WriteBlockMember(targetBlock, targetObj, b => b.TrackLabel = TrackLabel, "TrackLabel", TrackLabelFallbacks, TrackLabel);
        }
    }
}
