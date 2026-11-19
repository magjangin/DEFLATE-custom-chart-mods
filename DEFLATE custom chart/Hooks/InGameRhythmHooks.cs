using System;
using System.Collections.Generic;
using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using Il2CppSonicBloom.Koreo;
using UnityEngine;
using UnityEngine.Video;
using DEFLATE_custom_chart.Core;
using DEFLATE_custom_chart.Core.Bms;
using DEFLATE_custom_chart.Core.Wrapper;

namespace DEFLATE_custom_chart.Hooks
{
    public static class InGameRhythmHooks
    {
        private static int ResolveSamplePosition(double timeSeconds, int precomputedSample, int chartSampleRate, int targetSampleRate)
        {
            if (targetSampleRate > 0 && chartSampleRate > 0 && targetSampleRate != chartSampleRate)
            {
                return (int)Math.Round(timeSeconds * targetSampleRate);
            }
            return precomputedSample;
        }

        private static void ApplyCustomClipToKoreography(Koreography koreo)
        {
            if (koreo == null || !HwaAssetManager.IsTargetTrackActive) return;
            var customClip = HwaAssetManager.CustomBgmClip;
            if (customClip == null) return;

            Il2CppReflectionHelper.SetValueResilient(
                koreo,
                () => koreo.SourceClip = customClip,
                "SourceClip",
                MemberFallbacks.Koreography.SourceClip,
                customClip);
        }

        private static void EnsureInGameCustomBgm(RhythmGameController instance, string contextTag)
        {
            if (instance == null || instance.audioCom == null) return;
            if (!HwaAssetManager.IsTargetTrackActive || string.IsNullOrEmpty(HwaAssetManager.BgmFilePath)) return;

            var customClip = HwaAssetManager.CustomBgmClip;
            if (customClip != null)
            {
                if (instance.audioCom.clip == customClip && !instance.audioCom.mute)
                {
                    return;
                }

                // 숙주 곡 오디오가 재생 중이면 즉시 정지시킨 뒤 커스텀 클립으로 교체한다.
                if (instance.audioCom.isPlaying && instance.audioCom.clip != customClip)
                {
                    instance.audioCom.Stop();
                }

                instance.audioCom.clip = customClip;
                instance.audioCom.mute = false;
                MelonLogger.Msg($"  - [{contextTag}] Custom BGM 사전 클립 0ms 즉시 할당 완료: '{customClip.name}'");
                return;
            }

            // 커스텀 BGM이 아직 로딩 중이면 숙주 곡 클립이 재생되지 않도록 정지·비움·음소거한다.
            if (instance.audioCom.isPlaying)
            {
                instance.audioCom.Stop();
            }
            instance.audioCom.clip = null;
            instance.audioCom.mute = true;
            MelonCoroutines.Start(HwaAssetManager.LoadCustomBgmCoroutine(
                instance.audioCom,
                true,
                _ =>
                {
                    if (instance != null && instance.audioCom != null)
                    {
                        instance.audioCom.mute = false;
                        ApplyCustomClipToKoreography(instance.playingKoreo);
                    }
                },
                () =>
                {
                    if (instance != null && instance.audioCom != null)
                    {
                        instance.audioCom.mute = false;
                    }
                    MelonLogger.Warning($"  - [{contextTag}] Custom BGM 비동기 로드 실패로 audioCom 음소거를 해제합니다.");
                }));
        }

        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.Start))]
        public static class RhythmGameController_Start_Patch
        {
            // 최후 안전망: 어떤 경로로 인게임에 들어오든 곡 선택 전용 훅이 인게임 요청에 끼어들지 않게 한다.
            public static void Prefix()
            {
                HwaAssetManager.EnterGameplay("RhythmGameController.Start");
            }

            public static void Postfix(RhythmGameController __instance)
            {
                try
                {
                    if (__instance == null) return;
                    RhythmGameController_UpdateSongDurationScrollbar_Patch.ResetTimer();
                    NoteHooks.NoteObject_UpdateNotePosition_Patch.ResetFrameCache();

                    MelonLogger.Msg("==================================================");
                    MelonLogger.Msg("[★ 핵심 훅: RhythmGameController.Start ★] 인게임 리듬게임 씬 진입!");
                    bool isTargetTrack = false;
                    if (__instance.gameData != null)
                    {
                        var gd = __instance.gameData;
                        string nowTrackId = Il2CppReflectionHelper.GetValueResilient(gd, () => gd.NowTrackID, "NowTrackID", MemberFallbacks.GameData.TrackId, string.Empty);
                        string nowTrackTitle = Il2CppReflectionHelper.GetValueResilient(gd, () => gd.NowTrackTitle, "NowTrackTitle", MemberFallbacks.GameData.Title, string.Empty);
                        string nowTrackAuthor = Il2CppReflectionHelper.GetValueResilient(gd, () => gd.NowTrackAurthor, "NowTrackAurthor", MemberFallbacks.GameData.Author, string.Empty);

                        MelonLogger.Msg($"  - 곡 ID:        {nowTrackId}");
                        MelonLogger.Msg($"  - 곡 제목:      {nowTrackTitle}");
                        MelonLogger.Msg($"  - 곡 아티스트:   {nowTrackAuthor}");
                        MelonLogger.Msg($"  - 타겟 차트 Key: {gd.targetKorePath}");

                        // hwa/ 커스텀 png·bga·bgm 에셋을 사본(테스트 곡)에만 적용하기 위한 활성 트랙 갱신
                        isTargetTrack = HwaAssetManager.SetActiveTrack(nowTrackId, nowTrackTitle);
                        MelonLogger.Msg($"  - 커스텀 에셋 사본 여부: {isTargetTrack}");
                    }
                    if (DEFLATE_custom_chart.Core.ModConfig.Instance.AutoMode || SmokeTest.IsRunning)
                    {
                        __instance.autoMode = true;
                        MelonLogger.Msg("  [ModConfig] 오토 모드(AutoMode) 강제 활성화!");
                    }

                    MelonLogger.Msg($"  - 오토 모드:     {__instance.autoMode}");
                    MelonLogger.Msg($"  - 노트 배속:     {__instance.noteSpeed}");

                    if (isTargetTrack)
                    {
                        // hwa/ Custom BGM (.ogg/.wav/.mp3) 사전 로드된 클립 0ms 즉시 할당 (원곡 소리 튐 차단)
                        EnsureInGameCustomBgm(__instance, "Start");

                        // hwa/ Custom BGA (.mp4) 사전 주입 (사본 전용, 인게임에서는 audioCom과 겹치지 않도록 음소거)
                        if (__instance.videoPlayer != null)
                        {
                            HwaAssetManager.ApplyCustomBga(__instance.videoPlayer, false, muteAudio: true);
                        }
                        if (__instance.videoPlayer_playdata != null)
                        {
                            HwaAssetManager.MuteVideoPlayer(__instance.videoPlayer_playdata);
                        }

                        // hwa/ Custom PNG 커버 자켓 사전 주입 (사본 전용)
                        if (__instance.coverArtController != null)
                        {
                            var img = __instance.coverArtController.GetComponentInChildren<UnityEngine.UI.Image>(true);
                            if (img != null)
                            {
                                HwaAssetManager.ApplyCustomCover(img);
                            }
                        }
                    }

                    MelonLogger.Msg("--------------------------------------------------");
                    MelonLogger.Msg($"  - 판정 윈도우(ms):     {__instance.hitWindowRangeInMS}");
                    MelonLogger.Msg($"  - 판정 윈도우 Hi(ms):  {__instance.hitWindowsRangeInMS_Hi}");
                    MelonLogger.Msg($"  - 판정 윈도우(샘플):    {__instance.hitWindowRangeInSamples}");
                    MelonLogger.Msg($"  - 판정 윈도우 Hi(샘플): {__instance.hitWindowRangeInSamples_Hi}");
                    MelonLogger.Msg($"  - HitWindowSampleWidth:    {__instance.HitWindowSampleWidth}");
                    MelonLogger.Msg($"  - HitWindowSampleWidth_hi: {__instance.HitWindowSampleWidth_hi}");
                    MelonLogger.Msg($"  - WindowSizeInUnits:       {__instance.WindowSizeInUnits}");
                    MelonLogger.Msg("==================================================");

                    SmokeTest.OnInGameStart(__instance);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[RhythmGameController.Start] Postfix 처리 중 예외: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.ChangeDrumMode))]
        public static class RhythmGameController_ChangeDrumMode_Patch
        {
            public static void Postfix(RhythmGameController __instance)
            {
                try
                {
                    if (__instance == null) return;
                    MelonLogger.Msg($"[드럼 모드 전환] drumMode -> {__instance.drumMode} | lastDrumModeChangeSample={__instance.lastDrumModeChangeSample}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[ChangeDrumMode] Postfix 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.ChangeDrumModeScore))]
        public static class RhythmGameController_ChangeDrumModeScore_Patch
        {
            public static void Postfix(RhythmGameController __instance)
            {
                try
                {
                    if (__instance == null) return;
                    MelonLogger.Msg($"[드럼 모드(점수) 전환] drumMode_Score -> {__instance.drumMode_Score}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[ChangeDrumModeScore] Postfix 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "InitializeKoreographyTracks")]
        public static class RhythmGameController_InitializeKoreographyTracks_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(RhythmGameController), "InitializeKoreographyTracks");

            public static void Postfix(RhythmGameController __instance)
            {
                try
                {
                    if (__instance == null || __instance.playingKoreo == null) return;

                    var koreo = __instance.playingKoreo;
                    int totalEvents = 0;
                    if (koreo.Tracks != null)
                    {
                        for (int i = 0; i < koreo.Tracks.Count; i++)
                        {
                            var trk = koreo.Tracks[i];
                            if (trk != null && trk.mEventList != null)
                            {
                                totalEvents += trk.mEventList.Count;
                            }
                        }
                    }

                    if (HwaAssetManager.IsTargetTrackActive)
                    {
                        // 0. 숙주 Koreography의 SourceClip 및 audioCom을 커스텀 BGM으로 고정 (숙주 BGM 재바인딩 차단)
                        ApplyCustomClipToKoreography(koreo);
                        EnsureInGameCustomBgm(__instance, "InitializeKoreographyTracks");

                        // 1. 드롭 심벌 전용 독립 배열 초기화
                        if (__instance.dropEventSamples != null) __instance.dropEventSamples.Clear();
                        if (__instance.processedDropSamples != null) __instance.processedDropSamples.Clear();
                        __instance.nextDropEventIdx = 0;

                        // 2. 드롭 레인으로 라우팅되는 BMS 노트(14번 채널)를 O(1) 레인 인덱스로 dropEventSamples에 주입
                        var bmsChart = HwaAssetManager.LoadedBmsChart;
                        if (bmsChart != null && bmsChart.Notes.Count > 0 && __instance.dropEventSamples != null)
                        {
                            var dropNotes = bmsChart.GetNotesForLane(BmsLaneMapper.Drop);
                            int targetSampleRate = koreo.SampleRate > 0 ? koreo.SampleRate : bmsChart.SampleRate;
                            for (int i = 0; i < dropNotes.Count; i++)
                            {
                                var n = dropNotes[i];
                                __instance.dropEventSamples.Add(ResolveSamplePosition(n.TimeSeconds, n.SamplePosition, bmsChart.SampleRate, targetSampleRate));
                            }
                            MelonLogger.Msg($"  - [BMS 14번 채널] 커스텀 드롭(Drop) 노트 샘플 {__instance.dropEventSamples.Count}개 dropEventSamples 주입 완료!");
                        }

                        // 3. Koreography 원본 차트 트랙 중 hihat_3 이외 트랙의 mEventList 클리어
                        if (koreo.Tracks != null && bmsChart == null)
                        {
                            for (int i = 0; i < koreo.Tracks.Count; i++)
                            {
                                var trk = koreo.Tracks[i];
                                if (trk == null || trk.mEventList == null) continue;
                                bool isHiHat3 = string.Equals(trk.EventID, "hihat_3", StringComparison.OrdinalIgnoreCase);
                                if (!isHiHat3)
                                {
                                    trk.mEventList.Clear();
                                }
                            }
                        }

                        MelonLogger.Msg("[★ 잔여 dropEventSamples 및 원본 차트 에셋 잔여 노트 완전 정리 완료 ★]");
                    }

                    MelonLogger.Msg("----------------------------------------------------------------------------------------------------");
                    MelonLogger.Msg($"[★ 핵심 훅: RhythmGameController.InitializeKoreographyTracks ★] 차트(Koreography) 로드 완료!");
                    MelonLogger.Msg($"  - 차트 이름: '{koreo.name}' | 오디오 샘플레이트: {koreo.SampleRate}Hz | 트랙 수: {koreo.Tracks?.Count ?? 0} | 총 노트 이벤트 수: {totalEvents}개");
                    MelonLogger.Msg("----------------------------------------------------------------------------------------------------");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[InitializeKoreographyTracks] Postfix 예외: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// <c>LaneController.laneType</c>를 열거형 <b>타입</b>에 컴파일 타임 의존 없이 박싱된 값으로 읽습니다.
        /// 게임 1.2에서 <c>LaneType</c>이 최상위 → <c>LaneController.LaneType</c> 중첩으로 옮겨져, 타입을 직접 참조하면
        /// 이 메서드를 호출하는 훅 전체가 JIT 단계에서 TypeLoadException으로 무력화되기 때문입니다.
        /// 값의 ToString()(예: "HiHat_3")으로 <see cref="BmsLaneMapper"/>가 레인을 판정합니다.
        /// </summary>
        private static object ReadLaneTypeValue(LaneController lane)
        {
            return Il2CppReflectionHelper.GetValueResilient<object>(lane, null, "laneType", MemberFallbacks.Lane.LaneType, null);
        }

        private static Il2CppSystem.Collections.Generic.List<KoreographyEvent> BuildBmsLaneEvents(
            RhythmGameController instance,
            string trackID,
            LaneController lane,
            string source)
        {
            if (lane == null) return null;

            if (HwaAssetManager.Active == null && instance != null && instance.gameData != null)
            {
                var gd = instance.gameData;
                string nowTrackId = Il2CppReflectionHelper.GetValueResilient(gd, () => gd.NowTrackID, "NowTrackID", MemberFallbacks.GameData.TrackId, string.Empty);
                string nowTrackTitle = Il2CppReflectionHelper.GetValueResilient(gd, () => gd.NowTrackTitle, "NowTrackTitle", MemberFallbacks.GameData.Title, string.Empty);
                HwaAssetManager.SetActiveTrack(nowTrackId, nowTrackTitle);
            }

            if (!HwaAssetManager.IsTargetTrackActive) return null;

            var events = new Il2CppSystem.Collections.Generic.List<KoreographyEvent>();
            if (instance == null || instance.playingKoreo == null || instance.playingKoreo.Tracks == null)
            {
                return events;
            }

            var koreo = instance.playingKoreo;
            int before = lane.laneEvents != null ? lane.laneEvents.Count : -1;
            var bmsChart = HwaAssetManager.LoadedBmsChart;

            if (bmsChart != null && bmsChart.Notes.Count > 0)
            {
                // =========================================================
                // 1. 사용자 지정 규격 BMS ➔ DEFLATE 레인 노트 변환 주입 (O(1) 레인 인덱스 조회)
                // =========================================================
                string targetLane = BmsLaneMapper.ResolveLaneId(trackID, ReadLaneTypeValue(lane));

                if (targetLane != null)
                {
                    var laneNotes = bmsChart.GetNotesForLane(targetLane);
                    int holdCount = 0;
                    int targetSampleRate = koreo.SampleRate > 0 ? koreo.SampleRate : bmsChart.SampleRate;
                    int tapDurationSamples = (int)(targetSampleRate * 0.1f);

                    for (int i = 0; i < laneNotes.Count; i++)
                    {
                        var bmsNote = laneNotes[i];
                        int startSample = ResolveSamplePosition(bmsNote.TimeSeconds, bmsNote.SamplePosition, bmsChart.SampleRate, targetSampleRate);
                        int endSample = bmsNote.IsLongNote
                            ? ResolveSamplePosition(bmsNote.LongNoteEndTimeSeconds, bmsNote.LongNoteEndSamplePosition, bmsChart.SampleRate, targetSampleRate)
                            : startSample + tapDurationSamples;

                        var newEvt = new KoreographyEvent();
                        newEvt.StartSample = startSample;
                        newEvt.EndSample = endSample;
                        events.Add(newEvt);
                        if (bmsNote.IsLongNote) holdCount++;
                    }

                    MelonLogger.Msg($"[★ HWA BMS 노트 주입 ★] ({source}) 레인: '{trackID}'(lane {targetLane}) | {before}개 ➔ {events.Count}개 주입 완료 (홀드 {holdCount}개)");
                }
                else
                {
                    MelonLogger.Msg($"[BMS 레인 비움] ({source}) 레인: '{trackID}' | 지정 매핑 없음 ➔ 0개");
                }

                return events;
            }

            // =========================================================
            // 2. BMS 파일이 없을 경우: 테스트 롱노트 10연발 주입
            // =========================================================
            int globalFirstStart = int.MaxValue;
            for (int i = 0; i < koreo.Tracks.Count; i++)
            {
                var trk = koreo.Tracks[i];
                if (trk == null || trk.EventID == "beat" || trk.mEventList == null) continue;
                for (int j = 0; j < trk.mEventList.Count; j++)
                {
                    var ev = trk.mEventList[j];
                    if (ev != null && ev.StartSample > 0 && ev.StartSample < globalFirstStart)
                    {
                        globalFirstStart = ev.StartSample;
                    }
                }
            }

            bool isTargetLane = string.Equals(trackID, "hihat_3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ReadLaneTypeValue(lane)?.ToString(), "HiHat_3", StringComparison.OrdinalIgnoreCase);

            if (isTargetLane && globalFirstStart != int.MaxValue)
            {
                int longNoteDuration = (int)(koreo.SampleRate * 1.5f);
                int intervalSamples = koreo.SampleRate * 5;

                for (int copy = 0; copy < 10; copy++)
                {
                    int start = globalFirstStart + intervalSamples * copy;
                    var newEvt = new KoreographyEvent();
                    newEvt.StartSample = start;
                    newEvt.EndSample = start + longNoteDuration;
                    events.Add(newEvt);
                }

                MelonLogger.Msg($"[★ hihat_3 커스텀 롱노트 10연발 주입 ★] ({source}) 레인: '{trackID}' | StartSample={globalFirstStart}, 1.5초 롱노트 | {before}개 -> {events.Count}개");
            }
            else
            {
                MelonLogger.Msg($"[커스텀 테스트 주입] ({source}) 레인: '{trackID}' | 비움 (0개)");
            }

            return events;
        }

        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.LoadKoreographyEvents))]
        public static class RhythmGameController_LoadKoreographyEvents_Patch
        {
            // 디버그 스위치: false면 노트 주입 대신 원본 레인 노트(롱노트)만 로깅한다. readonly로 둬 CS0162 경고를 피한다.
            private static readonly bool NoteManipulationEnabled = true;

            public static void Postfix(RhythmGameController __instance, string trackID, LaneController lane)
            {
                try
                {
                    if (lane == null) return;

                    if (!NoteManipulationEnabled)
                    {
                        if (HwaAssetManager.Active == null && __instance != null && __instance.gameData != null)
                        {
                            var gd = __instance.gameData;
                            string nowTrackId = Il2CppReflectionHelper.GetValueResilient(gd, () => gd.NowTrackID, "NowTrackID", MemberFallbacks.GameData.TrackId, string.Empty);
                            string nowTrackTitle = Il2CppReflectionHelper.GetValueResilient(gd, () => gd.NowTrackTitle, "NowTrackTitle", MemberFallbacks.GameData.Title, string.Empty);
                            HwaAssetManager.SetActiveTrack(nowTrackId, nowTrackTitle);
                        }
                        if (!HwaAssetManager.IsTargetTrackActive) return;

                        int count = lane.laneEvents != null ? lane.laneEvents.Count : -1;
                        MelonLogger.Msg($"[레인 노트 로드] trackID='{trackID}' | 노트 수: {count}개");

                        const int LongNoteDurationThreshold = 10000;
                        if (lane.laneEvents != null)
                        {
                            for (int i = 0; i < lane.laneEvents.Count; i++)
                            {
                                var ev = lane.laneEvents[i];
                                if (ev == null) continue;
                                int duration = ev.EndSample - ev.StartSample;
                                if (duration >= LongNoteDurationThreshold)
                                {
                                    MelonLogger.Msg($"  [롱노트] trackID='{trackID}' [{i}] StartSample={ev.StartSample} EndSample={ev.EndSample} duration={duration}");
                                }
                            }
                        }

                        return;
                    }

                    var events = BuildBmsLaneEvents(__instance, trackID, lane, "LoadKoreographyEvents");
                    if (events == null) return;

                    lane.laneEvents = events;
                    lane.pendingEventIdx = 0;
                    SmokeTest.RecordLaneInjection(events.Count);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[LoadKoreographyEvents] Postfix 처리 중 예외 (trackID='{trackID}'): {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        // 재시작/배속/드럼 모드 전환 등 레인 이벤트 재로드 경로에서 원본 차트로 되돌아가는 것을 방지한다.
        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.ReplaceLaneEventsFromTrack))]
        public static class RhythmGameController_ReplaceLaneEventsFromTrack_Patch
        {
            public static void Postfix(RhythmGameController __instance, string trackID, LaneController lane)
            {
                try
                {
                    var events = BuildBmsLaneEvents(__instance, trackID, lane, "ReplaceLaneEventsFromTrack");
                    if (events == null) return;

                    int currentSample = __instance != null && __instance.audioCom != null ? __instance.audioCom.timeSamples : 0;
                    lane.ReplaceEvents(events, currentSample);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[ReplaceLaneEventsFromTrack] Postfix 처리 중 예외 (trackID='{trackID}'): {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.ReplaceLaneEventsFromTrackAtSample))]
        public static class RhythmGameController_ReplaceLaneEventsFromTrackAtSample_Patch
        {
            public static void Postfix(RhythmGameController __instance, string trackID, LaneController lane, int currentSample)
            {
                try
                {
                    var events = BuildBmsLaneEvents(__instance, trackID, lane, "ReplaceLaneEventsFromTrackAtSample");
                    if (events == null) return;

                    lane.ReplaceEvents(events, currentSample);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[ReplaceLaneEventsFromTrackAtSample] Postfix 처리 중 예외 (trackID='{trackID}'): {ex.Message}");
                }
            }
        }

        // =========================================================================
        // 라인바(마디선) — BMS 마디/박자 위치로 교체
        // =========================================================================

        /// <summary>
        /// 커스텀 BMS 곡이면 라인바 이벤트를 BMS 마디선/박자선 위치로 만든 새 리스트를 돌려줍니다. 교체 대상이 아니면 null.
        ///
        /// 라인바는 원래 Koreography의 beat 트랙(= 복제 원본 곡의 박자)을 따라가서, 템포가 다른 커스텀 곡에서는 노트와 어긋납니다.
        /// 원본 이벤트의 페이로드가 마디선/박자선 구분에 쓰일 수 있으므로, 원본에서 가장 드문 페이로드를 마디 시작선에,
        /// 가장 흔한 페이로드를 박자선에 그대로 재사용합니다 (원본 분포는 로그로 남김).
        /// </summary>
        private static Il2CppSystem.Collections.Generic.List<KoreographyEvent> BuildBmsLineBarEvents(LineBarController lane, string trackID, string source)
        {
            try
            {
                if (lane == null || !HwaAssetManager.IsTargetTrackActive) return null;

                var bmsChart = HwaAssetManager.LoadedBmsChart;
                if (bmsChart == null || bmsChart.Notes.Count == 0 || bmsChart.BarLines.Count == 0) return null;

            // 1. 원본 라인바 이벤트 관찰: 페이로드 종류별 개수 / 대표 페이로드 / 이벤트 길이 / 평균 간격
            var original = lane.laneEvents;
            int originalCount = original != null ? original.Count : 0;
            var payloadCounts = new Dictionary<string, int>();
            var payloadSamples = new Dictionary<string, IPayload>();
            int span = 0;
            int firstStart = -1, lastStart = -1;

            for (int i = 0; i < originalCount; i++)
            {
                var ev = original[i];
                if (ev == null) continue;

                if (firstStart < 0)
                {
                    firstStart = ev.StartSample;
                    span = Math.Max(0, ev.EndSample - ev.StartSample);
                }
                lastStart = ev.StartSample;

                var payload = ev.Payload;
                string key = DescribePayload(payload);
                payloadCounts[key] = payloadCounts.TryGetValue(key, out int c) ? c + 1 : 1;
                if (!payloadSamples.ContainsKey(key)) payloadSamples[key] = payload;
            }

            string measureKey = null, beatKey = null;
            foreach (var kv in payloadCounts)
            {
                if (measureKey == null || kv.Value < payloadCounts[measureKey]) measureKey = kv.Key;
                if (beatKey == null || kv.Value > payloadCounts[beatKey]) beatKey = kv.Key;
            }
            IPayload measurePayload = measureKey != null ? payloadSamples[measureKey] : null;
            IPayload beatPayload = beatKey != null ? payloadSamples[beatKey] : null;

            // 2. BMS 마디선/박자선 ➔ KoreographyEvent
            var events = new Il2CppSystem.Collections.Generic.List<KoreographyEvent>();
            int measureLines = 0;
            int targetSampleRate = lane.gameController != null && lane.gameController.playingKoreo != null && lane.gameController.playingKoreo.SampleRate > 0
                ? lane.gameController.playingKoreo.SampleRate
                : bmsChart.SampleRate;
            foreach (var bar in bmsChart.BarLines)
            {
                int samplePos = ResolveSamplePosition(bar.TimeSeconds, bar.SamplePosition, bmsChart.SampleRate, targetSampleRate);
                if (samplePos <= 0) continue; // 곡 시작 순간의 선은 이미 판정선 위라 보이지 않는다

                var evt = new KoreographyEvent();
                evt.StartSample = samplePos;
                evt.EndSample = samplePos + span;
                var payload = bar.IsMeasureStart ? measurePayload : beatPayload;
                if (payload != null) evt.Payload = payload;
                events.Add(evt);
                if (bar.IsMeasureStart) measureLines++;
            }

            var dist = new List<string>();
            foreach (var kv in payloadCounts) dist.Add($"{kv.Key}={kv.Value}");
            int avgInterval = originalCount > 1 ? (lastStart - firstStart) / (originalCount - 1) : 0;

            MelonLogger.Msg($"[★ HWA BMS 라인바 주입 ★] ({source}) trackID='{trackID}' | 원본 {originalCount}개 ➔ BMS {events.Count}개 (마디선 {measureLines} / 박자선 {events.Count - measureLines})");
            MelonLogger.Msg($"  - 원본 페이로드 분포: [{string.Join(", ", dist)}] | 마디선←'{measureKey}' 박자선←'{beatKey}' | 이벤트 길이 {span}샘플 | 원본 평균 간격 {avgInterval}샘플");
            return events;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BuildBmsLineBarEvents] 라인바 주입 중 예외 ({source}, trackID='{trackID}'): {ex.Message}");
                return null;
            }
        }

        private static string DescribePayload(IPayload payload)
        {
            if (payload == null) return "없음";
            var text = payload.TryCast<TextPayload>();
            if (text != null) return $"Text:{text.TextVal}";
            var num = payload.TryCast<IntPayload>();
            if (num != null) return $"Int:{num.mIntVal}";
            var real = payload.TryCast<FloatPayload>();
            if (real != null) return $"Float:{real.mFloatVal}";
            return "기타";
        }

        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.LoadLineBarEvents))]
        public static class RhythmGameController_LoadLineBarEvents_Patch
        {
            public static void Postfix(string trackID, LineBarController lane)
            {
                try
                {
                    var events = BuildBmsLineBarEvents(lane, trackID, "LoadLineBarEvents");
                    if (events == null) return;

                    // 로드 직후라 아직 스폰된 라인바가 없으므로 리스트를 통째로 바꾸고 처음부터 소비하게 한다.
                    // (기존 리스트를 Clear()하지 않는 건 그 리스트가 원본 Koreography 트랙과 공유될 가능성을 피하기 위함)
                    lane.laneEvents = events;
                    lane.pendingEventIdx = 0;
                    SmokeTest.RecordLineBarInjection(events.Count);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[LoadLineBarEvents] Postfix 처리 중 예외 (trackID='{trackID}'): {ex.Message}");
                }
            }
        }

        // 재시작/재로드 경로에서 원본 beat 트랙으로 라인바가 되돌아가는 것을 막는다.
        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.ReplaceLineBarEventsFromTrack))]
        public static class RhythmGameController_ReplaceLineBarEventsFromTrack_Patch
        {
            public static void Postfix(RhythmGameController __instance, string trackID, LineBarController lane)
            {
                try
                {
                    var events = BuildBmsLineBarEvents(lane, trackID, "ReplaceLineBarEventsFromTrack");
                    if (events == null) return;

                    int currentSample = __instance != null && __instance.audioCom != null ? __instance.audioCom.timeSamples : 0;
                    lane.ReplaceEvents(events, currentSample);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[ReplaceLineBarEventsFromTrack] Postfix 처리 중 예외 (trackID='{trackID}'): {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.ReplaceLineBarEventsFromTrackAtSample))]
        public static class RhythmGameController_ReplaceLineBarEventsFromTrackAtSample_Patch
        {
            public static void Postfix(string trackID, LineBarController lane, int currentSample)
            {
                try
                {
                    var events = BuildBmsLineBarEvents(lane, trackID, "ReplaceLineBarEventsFromTrackAtSample");
                    if (events == null) return;

                    lane.ReplaceEvents(events, currentSample);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[ReplaceLineBarEventsFromTrackAtSample] Postfix 처리 중 예외 (trackID='{trackID}'): {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "UpdateSongDurationScrollbar")]
        public static class RhythmGameController_UpdateSongDurationScrollbar_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(RhythmGameController), "UpdateSongDurationScrollbar");

            private static float lastLoggedTime = -10f;

            public static void ResetTimer() => lastLoggedTime = -10f;

            public static bool Prefix(RhythmGameController __instance, ref float currentTimeSeconds)
            {
                try
                {
                    if (__instance == null) return true;

                    if (currentTimeSeconds < lastLoggedTime)
                    {
                        lastLoggedTime = -10f;
                    }

                    if (currentTimeSeconds - lastLoggedTime >= 10f)
                    {
                        lastLoggedTime = currentTimeSeconds;
                        float totalLength = __instance.audioCom != null && __instance.audioCom.clip != null ? __instance.audioCom.clip.length : 0f;
                        float progressPercent = totalLength > 0f ? (currentTimeSeconds / totalLength) * 100f : 0f;

                        MelonLogger.Msg($"[재생 진행률 업데이트] 현재 시간: {currentTimeSeconds:F2}초 / 전체: {totalLength:F2}초 ({progressPercent:F1}%)");

                        int activeNotesCount = __instance.activeNotes != null ? __instance.activeNotes.Count : -1;
                        int activeLineBarsCount = __instance.activeLineBars != null ? __instance.activeLineBars.Count : -1;
                        MelonLogger.Msg($"  - 활성 노트(activeNotes): {activeNotesCount}개 | 활성 라인바(activeLineBars): {activeLineBarsCount}개");
                        MelonLogger.Msg($"  - 드럼 모드: {__instance.drumMode} | 점수용 드럼 모드: {__instance.drumMode_Score}");

                        int kickL = __instance.kickNoteObjectPool_left != null ? __instance.kickNoteObjectPool_left.Count : -1;
                        int kickR = __instance.kickNoteObjectPool_right != null ? __instance.kickNoteObjectPool_right.Count : -1;
                        int snareL = __instance.snareNoteObjectPool_left != null ? __instance.snareNoteObjectPool_left.Count : -1;
                        int snareR = __instance.snareNoteObjectPool_right != null ? __instance.snareNoteObjectPool_right.Count : -1;
                        int hh1 = __instance.hihatNoteObjectPool_1 != null ? __instance.hihatNoteObjectPool_1.Count : -1;
                        int hh2 = __instance.hihatNoteObjectPool_2 != null ? __instance.hihatNoteObjectPool_2.Count : -1;
                        int hh3 = __instance.hihatNoteObjectPool_3 != null ? __instance.hihatNoteObjectPool_3.Count : -1;
                        int hh4 = __instance.hihatNoteObjectPool_4 != null ? __instance.hihatNoteObjectPool_4.Count : -1;
                        int drop = __instance.dropNoteObjectPool != null ? __instance.dropNoteObjectPool.Count : -1;
                        int shake = __instance.shakeNoteObjectPool != null ? __instance.shakeNoteObjectPool.Count : -1;
                        int lineBarPool = __instance.lineBarObjectPool != null ? __instance.lineBarObjectPool.Count : -1;
                        MelonLogger.Msg($"  - 풀 여유분(대기 중): kickL={kickL} kickR={kickR} snareL={snareL} snareR={snareR} hh1={hh1} hh2={hh2} hh3={hh3} hh4={hh4} drop={drop} shake={shake} lineBar={lineBarPool}");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[UpdateSongDurationScrollbar] Prefix 예외: {ex.Message}");
                }

                return true;
            }
        }

        // =========================================================================
        // 인게임 BGM 바인딩 가드: 게임 1.2의 비동기 Koreography 바인딩 경로가
        // audioCom.clip을 숙주 곡(WindShifter)의 SourceClip으로 덮어쓰는 것을 원천 차단한다.
        // =========================================================================

        [HarmonyPatch(typeof(RhythmGameController), "EnsureAudioSourceBound")]
        public static class RhythmGameController_EnsureAudioSourceBound_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(RhythmGameController), "EnsureAudioSourceBound");

            public static void Prefix(RhythmGameController __instance, Koreography kore)
            {
                try
                {
                    if (!HwaAssetManager.IsTargetTrackActive) return;
                    ApplyCustomClipToKoreography(kore);
                    EnsureInGameCustomBgm(__instance, "EnsureAudioSourceBound.Pre");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[EnsureAudioSourceBound] Prefix 예외: {ex.Message}");
                }
            }

            public static void Postfix(RhythmGameController __instance)
            {
                try
                {
                    if (!HwaAssetManager.IsTargetTrackActive) return;
                    EnsureInGameCustomBgm(__instance, "EnsureAudioSourceBound.Post");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[EnsureAudioSourceBound] Postfix 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "BindAudioClipWhenReady")]
        public static class RhythmGameController_BindAudioClipWhenReady_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(RhythmGameController), "BindAudioClipWhenReady");

            public static void Prefix(RhythmGameController __instance, ref AudioClip clip)
            {
                try
                {
                    if (!HwaAssetManager.IsTargetTrackActive) return;
                    if (HwaAssetManager.CustomBgmClip != null)
                    {
                        clip = HwaAssetManager.CustomBgmClip;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BindAudioClipWhenReady] Prefix 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "StartAudioWhenReady")]
        public static class RhythmGameController_StartAudioWhenReady_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(RhythmGameController), "StartAudioWhenReady");

            public static void Prefix(RhythmGameController __instance)
            {
                try
                {
                    if (!HwaAssetManager.IsTargetTrackActive) return;
                    EnsureInGameCustomBgm(__instance, "StartAudioWhenReady");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[StartAudioWhenReady] Prefix 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "TriggerAudioStartIfReady")]
        public static class RhythmGameController_TriggerAudioStartIfReady_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(RhythmGameController), "TriggerAudioStartIfReady");

            public static void Prefix(RhythmGameController __instance)
            {
                try
                {
                    if (__instance == null) return;
                    MelonLogger.Msg($"[★ 핵심 훅: RhythmGameController.TriggerAudioStartIfReady ★] BGM 오디오 재생 트리거!");

                    EnsureInGameCustomBgm(__instance, "TriggerAudioStartIfReady");

                    if (__instance.audioCom != null && __instance.audioCom.clip != null)
                    {
                        var clip = __instance.audioCom.clip;
                        MelonLogger.Msg($"  - 현재 오디오 클립명: '{clip.name}' | 주파수: {clip.frequency}Hz | 길이: {clip.length:F2}초");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[TriggerAudioStartIfReady] Prefix 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "LoadVideo")]
        public static class RhythmGameController_LoadVideo_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(RhythmGameController), "LoadVideo");

            public static bool Prefix(RhythmGameController __instance, ref string addresskey, VideoPlayer thisplayer)
            {
                try
                {
                    MelonLogger.Msg($"[인게임 BGA 로드] BGA AddressKey: '{addresskey}'");

                    // 인게임 BGA VideoPlayer는 audioCom과 이중 재생되지 않도록 반드시 음소거한다.
                    if (HwaAssetManager.IsTargetTrackActive && thisplayer != null && HwaAssetManager.ApplyCustomBga(thisplayer, true, muteAudio: true))
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[LoadVideo] Prefix 예외: {ex.Message}");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "PlayVideoWithOffset")]
        public static class RhythmGameController_PlayVideoWithOffset_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(RhythmGameController), "PlayVideoWithOffset");

            public static bool Prefix(RhythmGameController __instance, VideoPlayer player, float offsetSeconds)
            {
                try
                {
                    if (HwaAssetManager.IsTargetTrackActive && player != null)
                    {
                        HwaAssetManager.ApplyCustomBga(player, false, muteAudio: true);
                    }

                    MelonLogger.Msg($"[인게임 BGA 동기화 재생] Offset: {offsetSeconds}초 | URL: '{player?.url}'");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PlayVideoWithOffset] Prefix 예외: {ex.Message}");
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "OnVideoStarted")]
        public static class RhythmGameController_OnVideoStarted_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(RhythmGameController), "OnVideoStarted");

            public static void Postfix(RhythmGameController __instance, VideoPlayer source)
            {
                try
                {
                    if (source != null)
                    {
                        MelonLogger.Msg($"[인게임 BGA 재생 시작] URL: '{source.url}'");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[OnVideoStarted] Postfix 예외: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(CoverArtController), nameof(CoverArtController.Initialize))]
        public static class CoverArtController_Initialize_Patch
        {
            public static void Postfix(CoverArtController __instance)
            {
                try
                {
                    if (__instance == null || !HwaAssetManager.IsTargetTrackActive) return;
                    HwaAssetManager.ApplyCustomCoverToHierarchy(__instance.gameObject);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CoverArtController.Initialize] Postfix 예외: {ex.Message}");
                }
            }
        }
    }
}
