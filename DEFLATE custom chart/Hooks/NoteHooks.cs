using System;
using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using UnityEngine;
using DEFLATE_custom_chart.Core;
using DEFLATE_custom_chart.Core.Wrapper;

namespace DEFLATE_custom_chart.Hooks
{
    public static class NoteHooks
    {
        [HarmonyPatch(typeof(RhythmGameController), "RecalculateAllNoteCount")]
        public static class RhythmGameController_RecalculateAllNoteCount_Patch
        {
            public static bool Prepare() => ModLog.Prepare(ModLog.Detail) && Il2CppReflectionHelper.HasPatchTarget(typeof(RhythmGameController), "RecalculateAllNoteCount");

            public static void Postfix(RhythmGameController __instance)
            {
                try
                {
                    if (__instance == null) return;
                    ModLog.Verbose($"[노트 수 재계산 완료] 전체 노트 수(AllNoteCount): {__instance.AllNoteCount}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[RecalculateAllNoteCount] Postfix 예외: {ex.Message}");
                }
            }
        }

        // =========================================================================
        // [랭킹/스코어 저장 차단] BlockSave 훅
        // =========================================================================
        [HarmonyPatch(typeof(GameData), nameof(GameData.SaveGameData))]
        public static class GameData_SaveGameData_Patch
        {
            private static bool _loggedOnce;

            public static bool Prepare() => ModConfig.Instance.BlockSave;

            public static bool Prefix()
            {
                try
                {
                    if (ModConfig.Instance.BlockSave)
                    {
                        if (!_loggedOnce)
                        {
                            _loggedOnce = true;
                            MelonLogger.Msg("[ModConfig] BlockSave=1: 게임 스코어 및 랭킹 데이터 저장을 차단합니다 (이후 동일 로그 생략).");
                        }
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[GameData.SaveGameData] Prefix 예외: {ex.Message}");
                }
                return true;
            }
        }

        // =========================================================================
        // [챌린지] 노트 카오스 배속 (NoteSpeedChaos) 훅
        // =========================================================================
        [HarmonyPatch(typeof(NoteObject), nameof(NoteObject.Initialize))]
        public static class NoteObject_Initialize_Patch
        {
            public static bool Prepare() => ModConfig.Instance.NoteSpeedChaos;
            private static float DeterministicFloat01(int seed)
            {
                unchecked
                {
                    uint z = (uint)seed + 0x9E3779B9u;
                    z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                    z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                    z ^= z >> 16;
                    return (z & 0x00FFFFFFu) / 16777216.0f;
                }
            }

            public static void Postfix(NoteObject __instance)
            {
                try
                {
                    var cfg = ModConfig.Instance;
                    if (!cfg.NoteSpeedChaos || __instance == null) return;

                    float minSpeed = Mathf.Max(0.1f, cfg.NoteSpeedChaosMin);
                    float maxSpeed = Mathf.Max(minSpeed, cfg.NoteSpeedChaosMax);

                    int seed = cfg.NoteSpeedChaosPerLane
                        ? ((int)__instance.noteType + 100)
                        : (__instance.Note_Index + (int)__instance.noteType * 1000);

                    float speedMultiplier = DeterministicFloat01(seed) * (maxSpeed - minSpeed) + minSpeed;
                    if (speedMultiplier <= 0f) speedMultiplier = 1.0f;

                    float hitSample = __instance.moveEndSample;
                    float originalDuration = hitSample - __instance.moveStartSample;
                    if (originalDuration > 0f)
                    {
                        float newDuration = originalDuration / speedMultiplier;
                        __instance.moveStartSample = hitSample - newDuration;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[NoteObject.Initialize] Postfix 예외: {ex.Message}");
                }
            }
        }

        // =========================================================================
        // 노트 눈송이 좌우 흔들림 (NoteSway) 훅
        // =========================================================================
        [HarmonyPatch(typeof(NoteObject), "UpdateNotePosition")]
        public static class NoteObject_UpdateNotePosition_Patch
        {
            public static bool Prepare() => ModConfig.Instance.NoteSway && Il2CppReflectionHelper.HasPatchTarget(typeof(NoteObject), "UpdateNotePosition");

            private static int _cachedFrame = -1;
            private static float _cachedSample;
            private static int _cachedSampleRate = 44100;

            // 서킷 브레이커: 매 프레임·노트마다 호출되는 핫패스라 게임 업데이트로 필드가 깨지면
            // 예외 비용이 프레임 드랍으로 직결된다. 연속 실패가 임계치를 넘으면 이번 곡 동안 흔들림을 끈다.
            private const int MaxConsecutiveFailures = 32;
            private static int _consecutiveFailures;
            private static bool _tripped;

            public static void ResetFrameCache()
            {
                _cachedFrame = -1;
                _cachedSample = 0f;
                _cachedSampleRate = 44100;
                _consecutiveFailures = 0;
                _tripped = false;
            }

            public static void Postfix(NoteObject __instance)
            {
                try
                {
                    var cfg = ModConfig.Instance;
                    if (_tripped || !cfg.NoteSway || __instance == null || __instance.isProcessed) return;

                    int frame = Time.frameCount;
                    if (_cachedFrame != frame)
                    {
                        var gc = __instance.gameController;
                        if (gc == null || gc.audioCom == null) return;

                        _cachedFrame = frame;
                        _cachedSample = (float)gc.audioCom.timeSamples;
                        // 게임 시계(timeSamples)는 재생 중인 클립 단위이므로 클립 주파수를 우선한다(48kHz 커스텀 음악에서 감쇠 시간 오차 방지).
                        var playingClip = gc.audioCom.clip;
                        _cachedSampleRate = playingClip != null && playingClip.frequency > 0
                            ? playingClip.frequency
                            : (gc.playingKoreo != null && gc.playingKoreo.SampleRate > 0 ? gc.playingKoreo.SampleRate : 44100);
                    }

                    float startSample = __instance.moveStartSample;
                    float endSample = __instance.moveEndSample;
                    float totalSample = endSample - startSample;

                    if (totalSample <= 0f) return;

                    float currentSample = _cachedSample;
                    float progress = Mathf.Clamp01((currentSample - startSample) / totalSample);

                    float dampingFactor = 1.0f;
                    if (cfg.NoteSwayDamping)
                    {
                        float dampingTime = cfg.NoteSwayDampingTime;
                        if (dampingTime > 0f && _cachedSampleRate > 0)
                        {
                            float timeRemaining = (endSample - currentSample) / (float)_cachedSampleRate;
                            if (timeRemaining >= 0f && timeRemaining <= dampingTime)
                            {
                                dampingFactor = Mathf.Clamp01(timeRemaining / dampingTime);
                            }
                        }
                    }

                    float swayOffset = Mathf.Sin(progress * cfg.NoteSwayAngularFreq)
                        * cfg.NoteSwayAmplitude
                        * dampingFactor;

                    Vector3 pos = __instance.transform.localPosition;
                    pos.x += swayOffset;
                    __instance.transform.localPosition = pos;
                    _consecutiveFailures = 0;
                }
                catch (Exception ex)
                {
                    // 매 프레임 다량 호출되므로 첫 실패만 기록하고, 연속 실패가 누적되면 이번 곡 동안 차단한다.
                    if (_consecutiveFailures++ == 0)
                    {
                        MelonLogger.Warning($"[NoteSway] UpdateNotePosition Postfix 예외 (이후 동일 예외는 생략): {ex.GetType().Name}: {ex.Message}");
                    }
                    if (_consecutiveFailures >= MaxConsecutiveFailures && !_tripped)
                    {
                        _tripped = true;
                        MelonLogger.Warning($"[NoteSway] 연속 {MaxConsecutiveFailures}회 실패로 이번 곡 동안 노트 흔들림을 비활성화합니다 (다음 곡 시작 시 재시도).");
                    }
                }
            }
        }
    }
}
