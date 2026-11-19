using System;
using MelonLoader;
using DEFLATE_custom_chart.Core;

[assembly: MelonInfo(typeof(DeflateMod), "DEFLATE Custom Chart & Song Logger Mod", "1.0.0", "Hwa-young-wang")]
[assembly: MelonGame("dizzylab", "DEFLATE")]
// MelonLoader의 자동 PatchAll()은 패치 클래스 하나만 실패해도 나머지 훅 적용이 전부 중단되므로 끄고,
// OnInitializeMelon에서 SafePatcher로 패치 클래스 단위 격리 적용한다.
[assembly: HarmonyDontPatchAll]

namespace DEFLATE_custom_chart.Core
{
    public class DeflateMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            try
            {
                MelonLogger.Msg("DEFLATE Custom Chart Mod v1.0.0 로드 (로그 상세도는 config.txt의 LogLevel: 0 요약 / 1 상세 / 2 디버그)");

                // 게임 버전/Unity/Steam Build ID 지문 기록 및 기준 빌드와 불일치 시 경고
                GameCompat.LogFingerprint();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DeflateMod] 시작 배너/지문 기록 중 예외: {ex.Message}");
            }

            // 설정을 먼저 로드해야 SafePatcher가 NoteSway/NoteSpeedChaos/BlockSave 등 비활성 핫패스 훅을 Prepare()에서 건너뛸 수 있다.
            try
            {
                ModConfig.Initialize();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DeflateMod] 설정 초기화 실패 (기본값으로 계속 진행): {ex.Message}");
            }

            // 훅 적용은 설정/에셋 초기화 실패와 무관하게 반드시 시도한다.
            try
            {
                SafePatcher.PatchAll(HarmonyInstance, MelonAssembly.Assembly);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DeflateMod] 훅 적용 중 치명적 예외: {ex.Message}\n{ex.StackTrace}");
            }

            try
            {
                // hwa/ 에셋 매니저 초기화 및 3종 에셋(BGM, BGA, Cover) 스캔 (디렉터리 생성 포함)
                HwaAssetManager.Initialize();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DeflateMod] 에셋 초기화 중 치명적 예외 발생: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
