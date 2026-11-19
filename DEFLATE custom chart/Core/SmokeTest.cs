using System;
using System.Collections;
using Il2Cppdizzylab.castor;
using MelonLoader;
using UnityEngine;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>
    /// 무인 스모크 테스트 (config.txt <c>SmokeTest=1</c>).
    ///
    /// 게임 업데이트 직후 "곡 목록 주입 → 커스텀 곡 선택 → 로딩 → 인게임 BMS 노트/BGM/BGA 주입"까지의
    /// 핵심 파이프라인을 사람 손 없이 한 번에 돌려보고, 결과를 <c>[SmokeTest] PASS/FAIL</c> 한 줄로 로그에 남깁니다.
    /// 게임 실행 1회당 1번만 동작하며(결과 화면 후 곡 목록 복귀 시 재실행 방지), 테스트 곡은 오토플레이로 진행됩니다.
    ///
    /// 실행: config.txt에 <c>SmokeTest=1</c> → <c>Start-Process "steam://rungameid/3921360"</c> →
    /// <c>Latest.log</c>에서 <c>[SmokeTest]</c> 검색. 끝나면 <c>SmokeTest=0</c>으로 되돌릴 것.
    /// </summary>
    public static class SmokeTest
    {
        private const float SelectDelaySeconds = 3f;
        private const float ConfirmDelaySeconds = 2f;
        private const float StepDelaySeconds = 0.3f;
        private const float KoreographyWaitSeconds = 10f;
        private const int MaxNavigationSteps = 200;
        private const float InGameProbeSeconds = 12f;

        // 게임은 시작 직후 MainTrackList를 두 번 만든다(인트로 → 곡 목록 재로드). 새 목록이 오면 세대를 올려
        // 이전 목록에서 돌던 실행을 조용히 폐기하고 새 목록에서 다시 시작한다. 플레이 확정 후에는 다시 돌지 않는다.
        private static int _generation;
        private static bool _playConfirmed;
        private static bool _inGameProbeStarted;
        private static int _injectedLaneEvents;
        private static int _injectedLanes;
        private static int _injectedLineBars;

        public static bool Enabled => ModConfig.Instance.SmokeTest;

        /// <summary>스모크 테스트가 이번 실행에서 커스텀 곡 플레이를 시작시켰는지 (오토플레이 강제 판단용).</summary>
        public static bool IsRunning => Enabled && _playConfirmed;

        /// <summary>곡 목록 준비 완료 시 호출. 첫 커스텀 곡을 선택하고 플레이를 시작합니다.</summary>
        public static void OnSongListReady(MainTrackList list, Func<MainTrackListBlock, bool> isCustomBlock)
        {
            if (!Enabled || _playConfirmed || list == null || isCustomBlock == null) return;
            int generation = ++_generation;
            MelonCoroutines.Start(SelectAndPlay(list, isCustomBlock, generation));
        }

        public static void RecordLaneInjection(int eventCount)
        {
            if (!Enabled) return;
            _injectedLanes++;
            _injectedLaneEvents += eventCount;
        }

        public static void RecordLineBarInjection(int eventCount)
        {
            if (!Enabled) return;
            _injectedLineBars += eventCount;
        }

        /// <summary>인게임 진입 시 호출. 일정 시간 뒤 BGM/BGA/노트 주입 상태를 점검해 PASS/FAIL을 기록합니다.</summary>
        public static void OnInGameStart(RhythmGameController controller)
        {
            if (!IsRunning || _inGameProbeStarted || controller == null) return;
            _inGameProbeStarted = true;
            MelonCoroutines.Start(ProbeInGame(controller));
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        /// <summary>더 새 곡 목록이 생겼거나 현재 목록이 파괴되었으면 이 실행을 조용히 종료해야 합니다.</summary>
        private static bool Superseded(int generation, MainTrackList list)
        {
            if (generation != _generation) return true;
            bool gone;
            try { gone = list == null || list.tracks == null; } catch { gone = true; }
            if (gone) MelonLogger.Msg("[SmokeTest] 곡 목록이 교체되었습니다. 새 목록이 준비되면 다시 시작합니다.");
            return gone;
        }

        private static IEnumerator SelectAndPlay(MainTrackList list, Func<MainTrackListBlock, bool> isCustomBlock, int generation)
        {
            MelonLogger.Msg($"[SmokeTest] 시작(#{generation}): 곡 목록에서 첫 커스텀 곡을 찾습니다.");
            var wait = WaitRealtime(SelectDelaySeconds);
            while (wait.MoveNext()) yield return wait.Current;
            if (Superseded(generation, list)) yield break;

            int index = -1;
            try
            {
                for (int i = 0; i < list.tracks.Length; i++)
                {
                    var block = list.tracks[i];
                    if (block != null && isCustomBlock(block)) { index = i; break; }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmokeTest] FAIL: 커스텀 곡 탐색 중 예외: {ex.Message}");
                yield break;
            }

            if (index < 0)
            {
                MelonLogger.Warning("[SmokeTest] FAIL: 곡 목록에 주입된 커스텀 곡이 없습니다 (곡 목록 주입 단계 실패).");
                yield break;
            }

            // 사용자 조작과 같은 경로(Next/Last → ChangeTrack → Koreography 비동기 로드)로 이동한다.
            // GoToTrack만 호출하면 currentKoreHandle이 채워지지 않아 GotoSceneData가 네이티브 NRE를 던진다(2026-11-20 확인).
            int steps = 0;
            while (steps < MaxNavigationSteps)
            {
                int now;
                try
                {
                    now = list.NowIndex;
                    if (now == index) break;

                    int count = list.tracks.Length;
                    int forward = ((index - now) % count + count) % count;
                    if (forward <= count - forward) list.Next(); else list.Last();
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmokeTest] FAIL: 곡 이동(Next/Last) 예외: {ex.Message}");
                    yield break;
                }
                steps++;
                var stepWait = WaitRealtime(StepDelaySeconds);
                while (stepWait.MoveNext()) yield return stepWait.Current;
                if (Superseded(generation, list)) yield break;
            }

            if (list == null || list.NowIndex != index)
            {
                MelonLogger.Warning($"[SmokeTest] FAIL: {steps}회 이동 후에도 커스텀 곡(인덱스 {index})에 도달하지 못했습니다.");
                yield break;
            }
            MelonLogger.Msg($"[SmokeTest] 커스텀 곡 선택: 인덱스 {index} ({steps}회 이동) | 커스텀 곡 활성: {HwaAssetManager.IsTargetTrackActive}");

            // 선택 곡 Koreography 핸들이 준비될 때까지 대기
            float deadline = Time.realtimeSinceStartup + KoreographyWaitSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool ready = false;
                try { ready = list != null && list.hasKoreHandle; } catch { }
                if (ready) break;
                yield return null;
            }

            var confirmWait = WaitRealtime(ConfirmDelaySeconds);
            while (confirmWait.MoveNext()) yield return confirmWait.Current;
            if (Superseded(generation, list)) yield break;

            try
            {
                _playConfirmed = true;
                MelonLogger.Msg($"[SmokeTest] 플레이 확정(GotoSceneData) 호출 | Koreography 핸들 준비: {list.hasKoreHandle} | 커스텀 곡 활성: {HwaAssetManager.IsTargetTrackActive}");
                list.GotoSceneData();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmokeTest] FAIL: GotoSceneData 예외: {ex.Message}");
            }
        }

        private static IEnumerator ProbeInGame(RhythmGameController controller)
        {
            var wait = WaitRealtime(InGameProbeSeconds);
            while (wait.MoveNext()) yield return wait.Current;

            try
            {
                if (controller == null)
                {
                    MelonLogger.Warning("[SmokeTest] FAIL: 점검 전에 인게임 컨트롤러가 사라졌습니다.");
                    yield break;
                }

                var audio = controller.audioCom;
                var customClip = HwaAssetManager.CustomBgmClip;
                bool customActive = HwaAssetManager.IsTargetTrackActive;
                bool bgmIsCustom = audio != null && customClip != null && audio.clip == customClip;
                bool bgmPlaying = audio != null && audio.isPlaying && !audio.mute;
                bool hasChart = HwaAssetManager.LoadedBmsChart != null;
                bool notesOk = !hasChart || _injectedLaneEvents > 0;
                string videoUrl = controller.videoPlayer != null ? controller.videoPlayer.url : null;
                bool bgaOk = string.IsNullOrEmpty(HwaAssetManager.BgaFileUrl)
                    || string.Equals(videoUrl, HwaAssetManager.BgaFileUrl, StringComparison.Ordinal);
                int activeNotes = controller.activeNotes != null ? controller.activeNotes.Count : -1;

                bool contextOk = HwaAssetManager.IsInGameplayContext && !HwaAssetManager.IsInSongSelectContext;
                string videoAudio = controller.videoPlayer != null ? controller.videoPlayer.audioOutputMode.ToString() : "없음";

                bool pass = customActive && bgmIsCustom && bgmPlaying && notesOk && bgaOk && contextOk;

                string detail = $"커스텀 곡 활성={customActive} | BGM 커스텀={bgmIsCustom}('{audio?.clip?.name}') 재생중={bgmPlaying} t={(audio != null ? audio.time : -1f):F1}s"
                    + $" | BMS 노트 주입 {_injectedLaneEvents}개/{_injectedLanes}레인 · 라인바 {_injectedLineBars}개 | 활성 노트 {activeNotes}"
                    + $" | BGA={(bgaOk ? "OK" : $"불일치('{videoUrl}')")} 영상오디오={videoAudio}"
                    + $" | 컨텍스트={(contextOk ? "인게임" : "곡선택 잔류!")}";

                if (pass) MelonLogger.Msg($"[SmokeTest] PASS — {detail}");
                else MelonLogger.Warning($"[SmokeTest] FAIL — {detail}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmokeTest] FAIL: 인게임 점검 중 예외: {ex.Message}");
            }
        }
    }
}
