using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using DEFLATE_custom_chart.Core;

[assembly: MelonInfo(typeof(DeflateMod), "DEFLATE Custom Chart & Song Logger Mod", "2.0.0", "Hwa-young-wang")]
[assembly: MelonGame("dizzylab", "DEFLATE")]

namespace DEFLATE_custom_chart.Core
{
    public class DeflateMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("==================================================");
            MelonLogger.Msg("  DEFLATE Custom Chart Mod v2.0.0 Loaded!");
            MelonLogger.Msg("  - Modular Architecture & Selection Interceptor");
            MelonLogger.Msg("==================================================");

            string hwaPath = Path.Combine(MelonEnvironment.GameRootDirectory, "hwa");
            if (!Directory.Exists(hwaPath))
            {
                Directory.CreateDirectory(hwaPath);
                MelonLogger.Msg($"[DeflateMod] 'hwa' folder created at: {hwaPath}");
            }

            string saveCustomKeyPath = Path.Combine(MelonEnvironment.GameRootDirectory, "savecustomkey");
            if (!Directory.Exists(saveCustomKeyPath))
            {
                Directory.CreateDirectory(saveCustomKeyPath);
                MelonLogger.Msg($"[DeflateMod] 'savecustomkey' folder created at: {saveCustomKeyPath}");
            }

            // savecustomkey/mod_config.txt 설정 로드 및 자동 생성
            ModConfig.Initialize();

            // hwa/ 에셋 매니저 초기화 및 3종 에셋(BGM, BGA, Cover) 스캔
            HwaAssetManager.Initialize();

            // Il2Cpp 커스텀 컴포넌트 타입 등록
            JudgmentBarController.RegisterType();
        }
    }
}
