using System;
using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;
using UnityEngine;
using DEFLATE_custom_chart.Core;

namespace DEFLATE_custom_chart.Hooks
{
    public static class NoteHooks
    {
        [HarmonyPatch(typeof(RhythmGameController), "RecalculateAllNoteCount")]
        public static class RhythmGameController_RecalculateAllNoteCount_Patch
        {
            public static void Postfix(RhythmGameController __instance)
            {
                if (__instance == null) return;
                MelonLogger.Msg($"[노트 수 재계산 완료] 전체 노트 수(AllNoteCount): {__instance.AllNoteCount}");
            }
        }

        // =========================================================================
        // [랭킹/스코어 저장 차단] BlockSave 훅
        // =========================================================================
        [HarmonyPatch(typeof(GameData), nameof(GameData.SaveGameData))]
        public static class GameData_SaveGameData_Patch
        {
            public static bool Prefix()
            {
                if (ModConfig.Instance.BlockSave)
                {
                    MelonLogger.Msg("[ModConfig] BlockSave=1: 게임 스코어 및 랭킹 데이터 저장을 차단했습니다.");
                    return false; // 세이브 스킵
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
            public static void Postfix(NoteObject __instance)
            {
                if (__instance == null || !ModConfig.Instance.NoteSpeedChaos) return;

                float minSpeed = ModConfig.Instance.NoteSpeedChaosMin;
                float maxSpeed = ModConfig.Instance.NoteSpeedChaosMax;

                float speedMultiplier = 1.0f;

                if (ModConfig.Instance.NoteSpeedChaosPerLane)
                {
                    // 레인 단위 카오스 배속 (같은 레인 노트끼리는 동일 속도 유지)
                    int laneSeed = (int)__instance.noteType;
                    var rand = new System.Random(laneSeed + 100);
                    speedMultiplier = (float)(rand.NextDouble() * (maxSpeed - minSpeed) + minSpeed);
                }
                else
                {
                    // 노트 완전 카오스 배속
                    var rand = new System.Random(__instance.Note_Index + (int)__instance.noteType * 1000);
                    speedMultiplier = (float)(rand.NextDouble() * (maxSpeed - minSpeed) + minSpeed);
                }

                if (speedMultiplier <= 0) speedMultiplier = 1.0f;

                float hitSample = __instance.moveEndSample;
                float originalDuration = hitSample - __instance.moveStartSample;
                if (originalDuration > 0)
                {
                    float newDuration = originalDuration / speedMultiplier;
                    __instance.moveStartSample = hitSample - newDuration;
                }
            }
        }

        // =========================================================================
        // 노트 눈송이 좌우 흔들림 (NoteSway) 훅
        // =========================================================================
        [HarmonyPatch(typeof(NoteObject), "UpdateNotePosition")]
        public static class NoteObject_UpdateNotePosition_Patch
        {
            public static void Postfix(NoteObject __instance)
            {
                if (__instance == null || __instance.isProcessed || !ModConfig.Instance.NoteSway) return;
                if (__instance.gameController == null || __instance.gameController.audioCom == null) return;

                float startSample = __instance.moveStartSample;
                float endSample = __instance.moveEndSample;
                float totalSample = endSample - startSample;

                if (totalSample <= 0) return;

                float currentSample = (float)__instance.gameController.audioCom.timeSamples;
                float progress = Mathf.Clamp01((currentSample - startSample) / totalSample);

                float timeRemaining = 0.0f;
                int sampleRate = __instance.gameController.playingKoreo != null ? __instance.gameController.playingKoreo.SampleRate : 44100;
                if (sampleRate > 0)
                {
                    timeRemaining = (endSample - currentSample) / (float)sampleRate;
                }

                // Damping Factor (판정선 도달 시 흔들림 잦아듦)
                float dampingFactor = 1.0f;
                if (ModConfig.Instance.NoteSwayDamping)
                {
                    float dampingTime = ModConfig.Instance.NoteSwayDampingTime;
                    if (dampingTime > 0 && timeRemaining >= 0 && timeRemaining <= dampingTime)
                    {
                        dampingFactor = Mathf.Clamp01(timeRemaining / dampingTime);
                    }
                }

                float amplitude = ModConfig.Instance.NoteSwayAmplitude;
                float speed = ModConfig.Instance.NoteSwaySpeed;

                // Sine Wave 흔들림 오프셋 계산
                float swayOffset = Mathf.Sin(progress * Mathf.PI * 2.0f * speed * 5.0f) * amplitude * dampingFactor;

                Vector3 pos = __instance.transform.localPosition;
                pos.x += swayOffset;
                __instance.transform.localPosition = pos;
            }
        }
    }
}
