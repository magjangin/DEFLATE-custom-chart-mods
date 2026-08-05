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
                    isTargetTrack = HwaAssetManager.SetActiveTrack(__instance.gameData.NowTrackID);
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

                // 실시간 5구간 판정바 생성 및 초기화
                JudgmentBarController.EnsureInstance(__instance.transform);
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
                int globalFirstStart = int.MaxValue;

                var tracks = koreo.Tracks;
                for (int i = 0; i < tracks.Count; i++)
                {
                    var trk = tracks[i];
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

                int before = lane.laneEvents != null ? lane.laneEvents.Count : -1;

                bool isTargetLane = string.Equals(trackID, "hihat_3", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(lane.laneType.ToString(), "HiHat_3", StringComparison.OrdinalIgnoreCase);

                if (isTargetLane && globalFirstStart != int.MaxValue)
                {
                    int longNoteDuration = (int)(koreo.SampleRate * 1.5f);
                    int intervalSamples = koreo.SampleRate * 5;

                    lane.laneEvents?.Clear();
                    for (int copy = 0; copy < 3; copy++)
                    {
                        int start = globalFirstStart + intervalSamples * copy;
                        var newEvt = new KoreographyEvent();
                        newEvt.StartSample = start;
                        newEvt.EndSample = start + longNoteDuration;
                        lane.laneEvents.Add(newEvt);
                    }

                    MelonLogger.Msg($"[★ hihat_3 롱노트 3연발 주입 ★] trackID='{trackID}' (LaneType={lane.laneType}) | StartSample={globalFirstStart}, 롱노트 길이={longNoteDuration}샘플(1.5초), 간격={intervalSamples}샘플(5초) | {before}개 -> {lane.laneEvents?.Count ?? -1}개");
                }
                else
                {
                    lane.laneEvents?.Clear();
                    MelonLogger.Msg($"[노트 주입] trackID='{trackID}' (LaneType={lane.laneType}) | 대상 레인 아님, 비움 | {before}개 -> 0개");
                }
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
                    MelonCoroutines.Start(HwaAssetManager.LoadCustomBgmCoroutine(__instance.audioCom, true));
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
