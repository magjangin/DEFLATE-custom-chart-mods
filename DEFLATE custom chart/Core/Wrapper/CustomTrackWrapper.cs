using System;
using UnityEngine;
using MelonLoader;
using Il2Cppdizzylab.castor;

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
        public string AudioKey { get; set; } = string.Empty;
        public string VideoKey { get; set; } = string.Empty;

        // 난이도별 차트 키
        public string EZ_KoreKey { get; set; } = string.Empty;
        public string NM_KoreKey { get; set; } = string.Empty;
        public string HD_KoreKey { get; set; } = string.Empty;

        // 커버 이미지 에셋
        public Sprite CoverSprite { get; set; }

        // 원본 Il2Cpp 참조 (안전한 캐스팅 지원)
        public MainTrackListBlock NativeBlock { get; private set; }

        public CustomTrackWrapper() { }

        public CustomTrackWrapper(object block)
        {
            Wrap(block);
        }

        /// <summary>
        /// Il2Cpp 객체(MainTrackListBlock 등)를 안전하게 캐스트하고 백업 리플렉션을 포함하여 래퍼로 캡슐화합니다.
        /// </summary>
        public void Wrap(object blockObj)
        {
            if (blockObj == null) return;

            // SafeCast로 타겟 타입 변환 시도 (실패해도 null 처리 후 백업 리플렉션 탐색)
            NativeBlock = Il2CppReflectionHelper.SafeCast<MainTrackListBlock>(blockObj);
            object targetObj = (object)NativeBlock ?? blockObj;

            // 1. UniqueID
            UniqueID = Il2CppReflectionHelper.GetValueResilient(
                targetObj,
                () => NativeBlock?.uniqueID,
                "uniqueID",
                new[] { "UniqueID", "id", "m_UniqueID", "trackID", "_uniqueID" },
                string.Empty);

            // 2. Title
            Title = Il2CppReflectionHelper.GetValueResilient(
                targetObj,
                () => NativeBlock?.TrackTitle,
                "TrackTitle",
                new[] { "trackTitle", "Title", "title", "m_TrackTitle", "_trackTitle" },
                string.Empty);

            // 3. Artist / Author
            Artist = Il2CppReflectionHelper.GetValueResilient(
                targetObj,
                () => NativeBlock?.TrackAuthor,
                "TrackAuthor",
                new[] { "trackAuthor", "Artist", "artist", "Author", "author", "m_TrackAuthor" },
                string.Empty);

            // 4. AlbumName
            AlbumName = Il2CppReflectionHelper.GetValueResilient(
                targetObj,
                () => NativeBlock?.TrackAlbum,
                "TrackAlbum",
                new[] { "trackAlbum", "Album", "album", "AlbumName", "m_TrackAlbum" },
                string.Empty);

            // 5. AudioKey
            AudioKey = Il2CppReflectionHelper.GetValueResilient(
                targetObj,
                () => NativeBlock?.audioClip_Key,
                "audioClip_Key",
                new[] { "audioClipKey", "audioKey", "AudioKey", "m_audioClip_Key" },
                string.Empty);

            // 6. VideoKey
            VideoKey = Il2CppReflectionHelper.GetValueResilient(
                targetObj,
                () => NativeBlock?.videoClip_Key,
                "videoClip_Key",
                new[] { "videoClipKey", "videoKey", "VideoKey", "m_videoClip_Key" },
                string.Empty);

            // 7. EZ Kore Key
            EZ_KoreKey = Il2CppReflectionHelper.GetValueResilient(
                targetObj,
                () => NativeBlock?.EZ_TrackKore_Key,
                "EZ_TrackKore_Key",
                new[] { "ez_TrackKore_Key", "ezTrackKoreKey", "ezKoreKey", "EZ_KoreKey" },
                string.Empty);

            // 8. NM Kore Key
            NM_KoreKey = Il2CppReflectionHelper.GetValueResilient(
                targetObj,
                () => NativeBlock?.NM_TrackKore_Key,
                "NM_TrackKore_Key",
                new[] { "nm_TrackKore_Key", "nmTrackKoreKey", "nmKoreKey", "NM_KoreKey" },
                string.Empty);

            // 9. HD Kore Key
            HD_KoreKey = Il2CppReflectionHelper.GetValueResilient(
                targetObj,
                () => NativeBlock?.HD_TrackKore_Key,
                "HD_TrackKore_Key",
                new[] { "hd_TrackKore_Key", "hdTrackKoreKey", "hdKoreKey", "HD_KoreKey" },
                string.Empty);

            // 10. CoverSprite
            CoverSprite = Il2CppReflectionHelper.GetValueResilient<Sprite>(
                targetObj,
                () => NativeBlock?.TrackCover,
                "TrackCover",
                new[] { "trackCover", "Cover", "cover", "m_TrackCover", "CoverSprite" },
                null);
        }

        /// <summary>
        /// 래퍼 데이터를 타겟 Il2Cpp 객체로 안전하게 다시 캐스팅 및 주입(Apply)합니다.
        /// 게임 패치로 일부 프로퍼티가 수정되어도 예외가 격리되어 정상 속성들은 모두 유지됩니다.
        /// </summary>
        public void ApplyTo(object blockObj)
        {
            if (blockObj == null) return;

            var targetBlock = Il2CppReflectionHelper.SafeCast<MainTrackListBlock>(blockObj);
            object targetObj = (object)targetBlock ?? blockObj;

            // 1. UniqueID
            Il2CppReflectionHelper.SetValueResilient(
                targetObj,
                () => { if (targetBlock != null) targetBlock.uniqueID = UniqueID; },
                "uniqueID",
                new[] { "UniqueID", "id", "m_UniqueID", "trackID", "_uniqueID" },
                UniqueID);

            // 2. Title
            Il2CppReflectionHelper.SetValueResilient(
                targetObj,
                () => { if (targetBlock != null) targetBlock.TrackTitle = Title; },
                "TrackTitle",
                new[] { "trackTitle", "Title", "title", "m_TrackTitle", "_trackTitle" },
                Title);

            // 3. Artist
            Il2CppReflectionHelper.SetValueResilient(
                targetObj,
                () => { if (targetBlock != null) targetBlock.TrackAuthor = Artist; },
                "TrackAuthor",
                new[] { "trackAuthor", "Artist", "artist", "Author", "author", "m_TrackAuthor" },
                Artist);

            // 4. AlbumName
            Il2CppReflectionHelper.SetValueResilient(
                targetObj,
                () => { if (targetBlock != null) targetBlock.TrackAlbum = AlbumName; },
                "TrackAlbum",
                new[] { "trackAlbum", "Album", "album", "AlbumName", "m_TrackAlbum" },
                AlbumName);

            // 5. AudioKey
            Il2CppReflectionHelper.SetValueResilient(
                targetObj,
                () => { if (targetBlock != null) targetBlock.audioClip_Key = AudioKey; },
                "audioClip_Key",
                new[] { "audioClipKey", "audioKey", "AudioKey", "m_audioClip_Key" },
                AudioKey);

            // 6. VideoKey
            Il2CppReflectionHelper.SetValueResilient(
                targetObj,
                () => { if (targetBlock != null) targetBlock.videoClip_Key = VideoKey; },
                "videoClip_Key",
                new[] { "videoClipKey", "videoKey", "VideoKey", "m_videoClip_Key" },
                VideoKey);

            // 7. EZ Kore Key
            Il2CppReflectionHelper.SetValueResilient(
                targetObj,
                () => { if (targetBlock != null) targetBlock.EZ_TrackKore_Key = EZ_KoreKey; },
                "EZ_TrackKore_Key",
                new[] { "ez_TrackKore_Key", "ezTrackKoreKey", "ezKoreKey", "EZ_KoreKey" },
                EZ_KoreKey);

            // 8. NM Kore Key
            Il2CppReflectionHelper.SetValueResilient(
                targetObj,
                () => { if (targetBlock != null) targetBlock.NM_TrackKore_Key = NM_KoreKey; },
                "NM_TrackKore_Key",
                new[] { "nm_TrackKore_Key", "nmTrackKoreKey", "nmKoreKey", "NM_KoreKey" },
                NM_KoreKey);

            // 9. HD Kore Key
            Il2CppReflectionHelper.SetValueResilient(
                targetObj,
                () => { if (targetBlock != null) targetBlock.HD_TrackKore_Key = HD_KoreKey; },
                "HD_TrackKore_Key",
                new[] { "hd_TrackKore_Key", "hdTrackKoreKey", "hdKoreKey", "HD_KoreKey" },
                HD_KoreKey);

            // 10. CoverSprite
            if (CoverSprite != null)
            {
                Il2CppReflectionHelper.SetValueResilient(
                    targetObj,
                    () => { if (targetBlock != null) targetBlock.TrackCover = CoverSprite; },
                    "TrackCover",
                    new[] { "trackCover", "Cover", "cover", "m_TrackCover", "CoverSprite" },
                    CoverSprite);
            }
        }
    }
}
