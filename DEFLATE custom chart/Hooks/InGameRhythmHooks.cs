using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using Il2CppSonicBloom.Koreo;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;
using DEFLATE_custom_chart.Core;
using DEFLATE_custom_chart.Core.Bms;

namespace DEFLATE_custom_chart.Hooks
{
    public static class InGameRhythmHooks
    {
        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.Start))]
        public static class RhythmGameController_Start_Patch
        {
            public static void Postfix(RhythmGameController __instance)
            {
                if (__instance == null) return;

                MelonLogger.Msg("==================================================");
                MelonLogger.Msg("[★ 핵심 훅: RhythmGameController.Start ★] 인게임 리듬게임 씬 진입!");
                bool isTargetTrack = false;
                if (__instance.gameData != null)
                {
                    MelonLogger.Msg($"  - 곡 ID:        {__instance.gameData.NowTrackID}");
                    MelonLogger.Msg($"  - 곡 제목:      {__instance.gameData.NowTrackTitle}");
                    MelonLogger.Msg($"  - 곡 아티스트:   {__instance.gameData.NowTrackAurthor}");
                    MelonLogger.Msg($"  - 타겟 차트 Key: {__instance.gameData.targetKorePath}");

                    // hwa/ 커스텀 png·bga·bgm 에셋을 사본(테스트 곡)에만 적용하기 위한 활성 트랙 갱신
                    isTargetTrack = HwaAssetManager.SetActiveTrack(__instance.gameData.NowTrackID, __instance.gameData.NowTrackTitle);
                    MelonLogger.Msg($"  - 커스텀 에셋 사본 여부: {isTargetTrack}");
                }
                if (DEFLATE_custom_chart.Core.ModConfig.Instance.AutoMode)
                {
                    __instance.autoMode = true;
                    MelonLogger.Msg("  [ModConfig] 오토 모드(AutoMode) 강제 활성화!");
                }

                MelonLogger.Msg($"  - 오토 모드:     {__instance.autoMode}");
                MelonLogger.Msg($"  - 노트 배속:     {__instance.noteSpeed}");

                if (isTargetTrack)
                {
                    // hwa/ Custom BGM (.ogg/.wav/.mp3) 사전 로드된 클립 0ms 즉시 할당 (원곡 소리 튐 차단)
                    if (__instance.audioCom != null)
                    {
                        if (HwaAssetManager.CustomBgmClip != null)
                        {
                            __instance.audioCom.clip = HwaAssetManager.CustomBgmClip;
                            MelonLogger.Msg($"  - [Start] Custom BGM 사전 클립 즉시 할당 완료: '{HwaAssetManager.CustomBgmClip.name}'");
                        }
                        else
                        {
                            __instance.audioCom.mute = true;
                            MelonCoroutines.Start(HwaAssetManager.LoadCustomBgmCoroutine(__instance.audioCom, true, (clip) => {
                                if (__instance != null && __instance.audioCom != null)
                                {
                                    __instance.audioCom.mute = false;
                                }
                            }));
                        }
                    }

                    // hwa/ Custom BGA (.mp4) 사전 주입 (사본 전용)
                    if (__instance.videoPlayer != null)
                    {
                        HwaAssetManager.ApplyCustomBga(__instance.videoPlayer, false);
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
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.ChangeDrumMode))]
        public static class RhythmGameController_ChangeDrumMode_Patch
        {
            public static void Postfix(RhythmGameController __instance)
            {
                if (__instance == null) return;
                MelonLogger.Msg($"[드럼 모드 전환] drumMode -> {__instance.drumMode} | lastDrumModeChangeSample={__instance.lastDrumModeChangeSample}");
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.ChangeDrumModeScore))]
        public static class RhythmGameController_ChangeDrumModeScore_Patch
        {
            public static void Postfix(RhythmGameController __instance)
            {
                if (__instance == null) return;
                MelonLogger.Msg($"[드럼 모드(점수) 전환] drumMode_Score -> {__instance.drumMode_Score}");
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "InitializeKoreographyTracks")]
        public static class RhythmGameController_InitializeKoreographyTracks_Patch
        {
            public static void Postfix(RhythmGameController __instance)
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
                    // 1. 드롭 심벌 전용 독립 배열 초기화
                    if (__instance.dropEventSamples != null) __instance.dropEventSamples.Clear();
                    if (__instance.processedDropSamples != null) __instance.processedDropSamples.Clear();
                    __instance.nextDropEventIdx = 0;

                    // 2. 드롭 레인으로 라우팅되는 BMS 노트(14/54 채널 또는 drop 키음)를 dropEventSamples에 주입
                    var bmsChart = HwaAssetManager.LoadedBmsChart;
                    if (bmsChart != null && bmsChart.Notes.Count > 0 && __instance.dropEventSamples != null)
                    {
                        foreach (var n in bmsChart.Notes)
                        {
                            if (BmsLaneMapper.ResolveNoteLane(n) == BmsLaneMapper.Drop)
                            {
                                __instance.dropEventSamples.Add(n.SamplePosition);
                            }
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
        }

        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.LoadKoreographyEvents))]
        public static class RhythmGameController_LoadKoreographyEvents_Patch
        {
            private const bool NoteManipulationEnabled = true;

            public static void Postfix(RhythmGameController __instance, string trackID, LaneController lane)
            {
                if (lane == null) return;

                if (__instance != null && __instance.gameData != null)
                {
                    HwaAssetManager.SetActiveTrack(__instance.gameData.NowTrackID, __instance.gameData.NowTrackTitle);
                }

                // 테스트 곡(사본) 외의 원본 곡들은 노트 배치를 원본 그대로 유지한다.
                if (!HwaAssetManager.IsTargetTrackActive) return;

                if (!NoteManipulationEnabled)
                {
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

                if (__instance == null || __instance.playingKoreo == null || __instance.playingKoreo.Tracks == null)
                {
                    lane.laneEvents?.Clear();
                    return;
                }

                var koreo = __instance.playingKoreo;
                int before = lane.laneEvents != null ? lane.laneEvents.Count : -1;
                var bmsChart = HwaAssetManager.LoadedBmsChart;

                if (bmsChart != null && bmsChart.Notes.Count > 0)
                {
                    // =========================================================
                    // 1. 사용자 지정 규격 BMS ➔ DEFLATE 레인 노트 변환 주입
                    // 레인 판정은 BmsLaneMapper가 담당 (채널 매핑만 사용, 키음 이름 추론 없음)
                    // =========================================================
                    lane.laneEvents?.Clear();

                    string targetLane = BmsLaneMapper.ResolveLaneId(trackID, lane.laneType.ToString());

                    if (targetLane != null)
                    {
                        int addedCount = 0;
                        int holdCount = 0;
                        foreach (var bmsNote in bmsChart.Notes)
                        {
                            if (!string.Equals(BmsLaneMapper.ResolveNoteLane(bmsNote), targetLane, StringComparison.OrdinalIgnoreCase)) continue;

                            int startSample = bmsNote.SamplePosition;
                            int endSample = bmsNote.IsLongNote ? bmsNote.LongNoteEndSamplePosition : startSample + (int)(koreo.SampleRate * 0.1f);

                            var newEvt = new KoreographyEvent();
                            newEvt.StartSample = startSample;
                            newEvt.EndSample = endSample;
                            lane.laneEvents.Add(newEvt);
                            addedCount++;
                            if (bmsNote.IsLongNote) holdCount++;
                        }
                        MelonLogger.Msg($"[★ HWA BMS 노트 주입 ★] 레인: '{trackID}'(lane {targetLane}) | {before}개 ➔ {addedCount}개 주입 완료 (홀드 {holdCount}개)");
                    }
                    else
                    {
                        MelonLogger.Msg($"[BMS 레인 비움] 레인: '{trackID}' | 지정 매핑 없음 ➔ 0개");
                    }
                }
                else
                {
                    // =========================================================
                    // 2. BMS 파일이 없을 경우: 테스트 롱노트 10연발 주입
                    // =========================================================
                    int globalFirstStart = int.MaxValue;
                    if (koreo.Tracks != null)
                    {
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
                    }

                    bool isTargetLane = string.Equals(trackID, "hihat_3", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(lane.laneType.ToString(), "HiHat_3", StringComparison.OrdinalIgnoreCase);

                    lane.laneEvents?.Clear();

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
                            lane.laneEvents.Add(newEvt);
                        }

                        MelonLogger.Msg($"[★ hihat_3 커스텀 롱노트 10연발 주입 ★] 레인: '{trackID}' | StartSample={globalFirstStart}, 1.5초 롱노트 | {before}개 -> {lane.laneEvents?.Count ?? 0}개");
                    }
                    else
                    {
                        MelonLogger.Msg($"[커스텀 테스트 주입] 레인: '{trackID}' | 비움 (0개)");
                    }
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
            foreach (var bar in bmsChart.BarLines)
            {
                if (bar.SamplePosition <= 0) continue; // 곡 시작 순간의 선은 이미 판정선 위라 보이지 않는다

                var evt = new KoreographyEvent();
                evt.StartSample = bar.SamplePosition;
                evt.EndSample = bar.SamplePosition + span;
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
                var events = BuildBmsLineBarEvents(lane, trackID, "LoadLineBarEvents");
                if (events == null) return;

                // 로드 직후라 아직 스폰된 라인바가 없으므로 리스트를 통째로 바꾸고 처음부터 소비하게 한다.
                // (기존 리스트를 Clear()하지 않는 건 그 리스트가 원본 Koreography 트랙과 공유될 가능성을 피하기 위함)
                lane.laneEvents = events;
                lane.pendingEventIdx = 0;
            }
        }

        // 재시작/재로드 경로에서 원본 beat 트랙으로 라인바가 되돌아가는 것을 막는다.
        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.ReplaceLineBarEventsFromTrack))]
        public static class RhythmGameController_ReplaceLineBarEventsFromTrack_Patch
        {
            public static void Postfix(RhythmGameController __instance, string trackID, LineBarController lane)
            {
                var events = BuildBmsLineBarEvents(lane, trackID, "ReplaceLineBarEventsFromTrack");
                if (events == null) return;

                int currentSample = __instance != null && __instance.audioCom != null ? __instance.audioCom.timeSamples : 0;
                lane.ReplaceEvents(events, currentSample);
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), nameof(RhythmGameController.ReplaceLineBarEventsFromTrackAtSample))]
        public static class RhythmGameController_ReplaceLineBarEventsFromTrackAtSample_Patch
        {
            public static void Postfix(string trackID, LineBarController lane, int currentSample)
            {
                var events = BuildBmsLineBarEvents(lane, trackID, "ReplaceLineBarEventsFromTrackAtSample");
                if (events == null) return;

                lane.ReplaceEvents(events, currentSample);
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "UpdateSongDurationScrollbar")]
        public static class RhythmGameController_UpdateSongDurationScrollbar_Patch
        {
            private static float lastLoggedTime = -5f;

            public static bool Prefix(RhythmGameController __instance, ref float currentTimeSeconds)
            {
                if (__instance == null) return true;

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

                return true;
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "TriggerAudioStartIfReady")]
        public static class RhythmGameController_TriggerAudioStartIfReady_Patch
        {
            public static void Prefix(RhythmGameController __instance)
            {
                if (__instance == null) return;
                MelonLogger.Msg($"[★ 핵심 훅: RhythmGameController.TriggerAudioStartIfReady ★] BGM 오디오 재생 트리거!");

                if (HwaAssetManager.IsTargetTrackActive && __instance.audioCom != null && !string.IsNullOrEmpty(HwaAssetManager.BgmFilePath))
                {
                    if (HwaAssetManager.CustomBgmClip != null)
                    {
                        __instance.audioCom.clip = HwaAssetManager.CustomBgmClip;
                        __instance.audioCom.mute = false;
                        MelonLogger.Msg($"  - [BGM 재생 준비] 사전 로드 클립 0ms 할당 완료: '{HwaAssetManager.CustomBgmClip.name}'");
                    }
                    else
                    {
                        __instance.audioCom.mute = true;
                        MelonCoroutines.Start(HwaAssetManager.LoadCustomBgmCoroutine(__instance.audioCom, true, (clip) => {
                            if (__instance != null && __instance.audioCom != null)
                            {
                                __instance.audioCom.mute = false;
                            }
                        }));
                    }
                }

                if (__instance.audioCom != null && __instance.audioCom.clip != null)
                {
                    var clip = __instance.audioCom.clip;
                    MelonLogger.Msg($"  - 현재 오디오 클립명: '{clip.name}' | 주파수: {clip.frequency}Hz | 길이: {clip.length:F2}초");
                }
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "LoadVideo")]
        public static class RhythmGameController_LoadVideo_Patch
        {
            public static bool Prefix(RhythmGameController __instance, ref string addresskey, VideoPlayer thisplayer)
            {
                MelonLogger.Msg($"[인게임 BGA 로드] BGA AddressKey: '{addresskey}'");

                if (HwaAssetManager.IsTargetTrackActive && thisplayer != null && HwaAssetManager.ApplyCustomBga(thisplayer, true))
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "PlayVideoWithOffset")]
        public static class RhythmGameController_PlayVideoWithOffset_Patch
        {
            public static bool Prefix(RhythmGameController __instance, VideoPlayer player, float offsetSeconds)
            {
                if (HwaAssetManager.IsTargetTrackActive && player != null)
                {
                    HwaAssetManager.ApplyCustomBga(player, false);
                }

                MelonLogger.Msg($"[인게임 BGA 동기화 재생] Offset: {offsetSeconds}초 | URL: '{player?.url}'");
                return true;
            }
        }

        [HarmonyPatch(typeof(RhythmGameController), "OnVideoStarted")]
        public static class RhythmGameController_OnVideoStarted_Patch
        {
            public static void Postfix(RhythmGameController __instance, VideoPlayer source)
            {
                if (source != null)
                {
                    MelonLogger.Msg($"[인게임 BGA 재생 시작] URL: '{source.url}'");
                }
            }
        }

        [HarmonyPatch(typeof(CoverArtController), nameof(CoverArtController.Initialize))]
        public static class CoverArtController_Initialize_Patch
        {
            public static void Postfix(CoverArtController __instance)
            {
                if (__instance == null || !HwaAssetManager.IsTargetTrackActive) return;
                HwaAssetManager.ApplyCustomCoverToHierarchy(__instance.gameObject);
            }
        }
    }
}
