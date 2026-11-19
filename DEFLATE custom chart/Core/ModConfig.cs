using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;

namespace DEFLATE_custom_chart.Core
{
    public class ModConfig
    {
        // 1. 기본 설정
        public bool AutoPlay { get; set; } = false;
        public bool AutoMode => AutoPlay; // 하위 호환용

        public bool BlockSave { get; set; } = true;

        // 3. 눈송이 흔들림 노트 연출 (NoteSway)
        public bool NoteSway { get; set; } = false;
        public float NoteSwayAmplitude { get; set; } = 20.0f;
        public float NoteSwaySpeed { get; set; } = 0.8f;
        public bool NoteSwayDamping { get; set; } = true;
        public float NoteSwayDampingTime { get; set; } = 0.4f;

        /// <summary>매 프레임 계산 병목 제거를 위해 사전 계산된 각주파수 (NoteSwaySpeed × 10π).</summary>
        public float NoteSwayAngularFreq { get; private set; }

        // 4. 카오스 노트 스피드 (NoteSpeedChaos)
        public bool NoteSpeedChaos { get; set; } = false;
        public float NoteSpeedChaosMin { get; set; } = 0.6f;
        public float NoteSpeedChaosMax { get; set; } = 1.8f;
        public bool NoteSpeedChaosPerLane { get; set; } = true;

        // 5. 무인 스모크 테스트 (게임 업데이트 후 검증용, 평소에는 0)
        public bool SmokeTest { get; set; } = false;

        // 6. 로그 상세도 (0 = 요약, 1 = 상세, 2 = 디버그) — ModLog 참고
        public int LogLevel { get; set; } = 0;

        public ModConfig()
        {
            UpdateDerivedValues();
        }

        private void UpdateDerivedValues()
        {
            NoteSwayAmplitude = SanitizeFloat(NoteSwayAmplitude, 0f, 10000f, 20.0f);
            NoteSwaySpeed = SanitizeFloat(NoteSwaySpeed, 0f, 1000f, 0.8f);
            NoteSwayDampingTime = SanitizeFloat(NoteSwayDampingTime, 0f, 60f, 0.4f);
            NoteSpeedChaosMin = SanitizeFloat(NoteSpeedChaosMin, 0.1f, 20f, 0.6f);
            NoteSpeedChaosMax = SanitizeFloat(NoteSpeedChaosMax, NoteSpeedChaosMin, 20f, 1.8f);
            if (NoteSpeedChaosMax < NoteSpeedChaosMin) NoteSpeedChaosMax = NoteSpeedChaosMin;

            NoteSwayAngularFreq = NoteSwaySpeed * 10.0f * (float)Math.PI;
        }

        private static float SanitizeFloat(float value, float min, float max, float fallback)
        {
            if (!float.IsFinite(value)) return fallback;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static string ConfigFilePath =>
            Path.Combine(MelonEnvironment.GameRootDirectory, "savecustomkey", "config.txt");

        public static ModConfig Instance { get; private set; } = new ModConfig();

        public static void Initialize()
        {
            try
            {
                string dirPath = Path.Combine(MelonEnvironment.GameRootDirectory, "savecustomkey");
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                    MelonLogger.Msg($"[ModConfig] 디렉토리 생성: {dirPath}");
                }

                if (File.Exists(ConfigFilePath))
                {
                    LoadFromTxt();
                    MelonLogger.Msg($"[ModConfig] config.txt 설정 파일 로드 완료 (AutoPlay={Instance.AutoPlay}, NoteSway={Instance.NoteSway}, BlockSave={Instance.BlockSave}, LogLevel={Instance.LogLevel}{(Instance.SmokeTest ? ", SmokeTest=ON" : "")})");
                    return;
                }

                // 파일이 없으면 기본값으로 템플릿 주석 포함 config.txt 새로 작성
                Instance = new ModConfig();
                Save();
                MelonLogger.Msg($"[ModConfig] 기본 설정 파일 생성됨: {ConfigFilePath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ModConfig] 설정 초기화 실패: {ex.Message}");
            }
        }

        private static readonly char[] KeyValueSeparators = { '=' };

        private static readonly HashSet<string> TrueWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "true", "트루", "참", "켜기", "켜짐", "활성화", "on", "1", "enable", "enabled", "y", "yes"
        };

        private static readonly HashSet<string> FalseWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "false", "폴스", "거짓", "끄기", "꺼짐", "비활성화", "off", "0", "disable", "disabled", "n", "no"
        };

        private static void LoadFromTxt()
        {
            var config = new ModConfig();
            string[] lines = File.ReadAllLines(ConfigFilePath);
            foreach (var line in lines)
            {
                string trimmed = line.Trim().TrimStart('\uFEFF');
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || !trimmed.Contains("=")) continue;

                string[] parts = trimmed.Split(KeyValueSeparators, 2);
                string key = parts[0].Trim();
                string val = parts[1].Trim();

                if (key.Equals("AutoPlay", StringComparison.OrdinalIgnoreCase) || key.Equals("AutoMode", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.AutoPlay = b;
                }
                else if (key.Equals("BlockSave", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.BlockSave = b;
                }
                else if (key.Equals("NoteSway", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.NoteSway = b;
                }
                else if (key.Equals("NoteSwayAmplitude", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFloat(val, out float f)) config.NoteSwayAmplitude = f;
                }
                else if (key.Equals("NoteSwaySpeed", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFloat(val, out float f)) config.NoteSwaySpeed = f;
                }
                else if (key.Equals("NoteSwayDamping", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.NoteSwayDamping = b;
                }
                else if (key.Equals("NoteSwayDampingTime", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFloat(val, out float f)) config.NoteSwayDampingTime = f;
                }
                else if (key.Equals("NoteSpeedChaos", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.NoteSpeedChaos = b;
                }
                else if (key.Equals("NoteSpeedChaosMin", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFloat(val, out float f)) config.NoteSpeedChaosMin = f;
                }
                else if (key.Equals("NoteSpeedChaosMax", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFloat(val, out float f)) config.NoteSpeedChaosMax = f;
                }
                else if (key.Equals("NoteSpeedChaosPerLane", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.NoteSpeedChaosPerLane = b;
                }
                else if (key.Equals("SmokeTest", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.SmokeTest = b;
                }
                else if (key.Equals("LogLevel", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lv)) config.LogLevel = Math.Clamp(lv, 0, 2);
                }
            }

            config.UpdateDerivedValues();
            Instance = config;
        }

        private static bool TryParseFloat(string val, out float result)
        {
            return float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && float.IsFinite(result);
        }

        private static bool TryParseFlexibleBool(string input, out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string trimmed = input.Trim();
            if (TrueWords.Contains(trimmed))
            {
                result = true;
                return true;
            }
            if (FalseWords.Contains(trimmed))
            {
                result = false;
                return true;
            }

            return bool.TryParse(trimmed, out result);
        }

        public static void Save()
        {
            try
            {
                var lines = new List<string>
                {
                    "# 설정 변경 후 저장하면 게임 실행 시 자동 적용됩니다.",
                    "# 지원 형식: 1/0, true/false, 켜짐/꺼짐, on/off",
                    "",
                    "# 오토 플레이 (1 = 켜짐, 0 = 꺼짐)",
                    $"AutoPlay={(Instance.AutoPlay ? 1 : 0)}",
                    "",
                    "# 베스트 스코어 / 랭킹 저장 차단 (1 = 켜짐, 0 = 꺼짐)",
                    $"BlockSave={(Instance.BlockSave ? 1 : 0)}",
                    "",
                    "# 노트가 눈송이처럼 좌우로 흔들리며 내려오는 연출 (1 = 켜짐, 0 = 꺼짐)",
                    "# 판정에는 전혀 영향이 없는 순수 시각 효과입니다.",
                    $"NoteSway={(Instance.NoteSway ? 1 : 0)}",
                    "",
                    "# 흔들림 폭 (픽셀). 너무 크면 레인 밖으로 나가 잘릴 수 있습니다.",
                    $"NoteSwayAmplitude={Instance.NoteSwayAmplitude.ToString(CultureInfo.InvariantCulture)}",
                    "",
                    "# 흔들림 속도. 노트 하나가 판정선까지 내려오는 동안 (값 × 5)번 왕복합니다.",
                    $"NoteSwaySpeed={Instance.NoteSwaySpeed.ToString(CultureInfo.InvariantCulture)}",
                    "",
                    "# 판정선에 가까워지면 흔들림을 잦아들게 함 (1 = 켜짐, 0 = 꺼짐)",
                    "# 끄면 판정선에 닿는 순간까지 계속 흔들립니다.",
                    $"NoteSwayDamping={(Instance.NoteSwayDamping ? 1 : 0)}",
                    "",
                    "# 판정선 도달 몇 초 전부터 흔들림이 잦아들지",
                    $"NoteSwayDampingTime={Instance.NoteSwayDampingTime.ToString(CultureInfo.InvariantCulture)}",
                    "",
                    "# [챌린지] 노트마다 낙하 속도를 제각각으로 (1 = 켜짐, 0 = 꺼짐)",
                    "# 노트끼리 서로 추월하므로 읽기가 매우 어려워집니다. 판정에는 영향이 없습니다.",
                    $"NoteSpeedChaos={(Instance.NoteSpeedChaos ? 1 : 0)}",
                    "",
                    "# 속도 배율 범위 (1 = 원래 속도). 예: 0.6 ~ 1.8",
                    $"NoteSpeedChaosMin={Instance.NoteSpeedChaosMin.ToString(CultureInfo.InvariantCulture)}",
                    $"NoteSpeedChaosMax={Instance.NoteSpeedChaosMax.ToString(CultureInfo.InvariantCulture)}",
                    "",
                    "# 1 = 레인마다 속도가 다름(같은 레인 안에서는 순서 유지, 읽을 수는 있음)",
                    "# 0 = 노트마다 속도가 다름(완전 카오스)",
                    $"NoteSpeedChaosPerLane={(Instance.NoteSpeedChaosPerLane ? 1 : 0)}",
                    "",
                    "# [개발용] 무인 스모크 테스트: 실행 직후 첫 커스텀 곡을 자동 선택·오토플레이하고",
                    "# 로그에 [SmokeTest] PASS/FAIL을 남깁니다. 게임 업데이트 후 점검할 때만 1로 켜세요.",
                    $"SmokeTest={(Instance.SmokeTest ? 1 : 0)}",
                    "",
                    "# 로그 상세도: 0 = 요약(기본, 곡당 한 줄), 1 = 상세(레인별 주입·에셋 교체), 2 = 디버그(커서 이동·진행률 등 전부)",
                    $"LogLevel={Instance.LogLevel}"
                };
                File.WriteAllLines(ConfigFilePath, lines);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ModConfig] 설정 저장 실패: {ex.Message}");
            }
        }
    }
}
