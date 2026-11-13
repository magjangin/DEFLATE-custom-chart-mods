using System;
using MelonLoader;
using DEFLATE_custom_chart.Core;

[assembly: MelonInfo(typeof(DeflateMod), "DEFLATE Custom Chart & Song Logger Mod", "1.0.0", "Hwa-young-wang")]
[assembly: MelonGame("dizzylab", "DEFLATE")]

namespace DEFLATE_custom_chart.Core
{
    public class DeflateMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            try
            {
                MelonLogger.Msg("==================================================");
                MelonLogger.Msg("  DEFLATE Custom Chart Mod v1.0.0 Loaded!");
                MelonLogger.Msg("  - Modular Architecture & Selection Interceptor");
                MelonLogger.Msg("==================================================");

                // savecustomkey/config.txt 설정 로드 및 자동 생성 (디렉터리 생성 포함)
                ModConfig.Initialize();

                // hwa/ 에셋 매니저 초기화 및 3종 에셋(BGM, BGA, Cover) 스캔 (디렉터리 생성 포함)
                HwaAssetManager.Initialize();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DeflateMod] 초기화 중 치명적 예외 발생: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
