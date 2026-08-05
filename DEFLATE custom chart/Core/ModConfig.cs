using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;

namespace DEFLATE_custom_chart.Core
{
    public class ModConfig
    {
        public bool AutoMode { get; set; } = false;
        public bool StealthAllPerfect { get; set; } = false;

        private static string ConfigFilePath =>
            Path.Combine(MelonEnvironment.GameRootDirectory, "savecustomkey", "mod_config.txt");

        public static ModConfig Instance { get; private set; } = new ModConfig();

        public static void Initialize()
        {
            try
            {
                string dirPath = Path.Combine(MelonEnvironment.GameRootDirectory, "savecustomkey");
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                    MelonLogger.Msg($"[ModConfig] Directory created: {dirPath}");
                }

                if (File.Exists(ConfigFilePath))
                {
                    LoadFromTxt();
                    MelonLogger.Msg($"[ModConfig] Config loaded from txt: AutoMode={Instance.AutoMode}, StealthAllPerfect={Instance.StealthAllPerfect}");
                    return;
                }

                // 파일이 없으면 기본값으로 txt 작성
                Instance = new ModConfig();
                Save();
                MelonLogger.Msg($"[ModConfig] Default config created at: {ConfigFilePath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ModConfig] Failed to initialize config: {ex.Message}");
            }
        }

        private static void LoadFromTxt()
        {
            var config = new ModConfig();
            string[] lines = File.ReadAllLines(ConfigFilePath);
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || !trimmed.Contains("=")) continue;

                string[] parts = trimmed.Split(new[] { '=' }, 2);
                string key = parts[0].Trim();
                string val = parts[1].Trim();

                if (key.Equals("AutoMode", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool auto)) config.AutoMode = auto;
                }
                else if (key.Equals("StealthAllPerfect", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool stealth)) config.StealthAllPerfect = stealth;
                }
            }
            Instance = config;
        }

        private static bool TryParseFlexibleBool(string input, out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string normalized = input.Trim().ToLowerInvariant();

            // True 의미를 갖는 단어들
            if (normalized == "true" || normalized == "트루" || normalized == "참" ||
                normalized == "켜기" || normalized == "활성화" || normalized == "on" ||
                normalized == "1" || normalized == "enable" || normalized == "enabled" ||
                normalized == "y" || normalized == "yes")
            {
                result = true;
                return true;
            }

            // False 의미를 갖는 단어들
            if (normalized == "false" || normalized == "폴스" || normalized == "거짓" ||
                normalized == "끄기" || normalized == "비활성화" || normalized == "off" ||
                normalized == "0" || normalized == "disable" || normalized == "disabled" ||
                normalized == "n" || normalized == "no")
            {
                result = false;
                return true;
            }

            return bool.TryParse(input, out result);
        }

        public static void Save()
        {
            try
            {
                var lines = new List<string>
                {
                    "# DEFLATE Custom Chart Mod Configuration",
                    $"AutoMode={Instance.AutoMode}",
                    $"StealthAllPerfect={Instance.StealthAllPerfect}"
                };
                File.WriteAllLines(ConfigFilePath, lines);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ModConfig] Failed to save config: {ex.Message}");
            }
        }
    }
}
