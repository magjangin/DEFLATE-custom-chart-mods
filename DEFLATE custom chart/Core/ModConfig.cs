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

        // 2. 판정바 & 키뷰어 설정
        public bool EnableJudgmentBar { get; set; } = false;
        public bool JudgmentBarVertical { get; set; } = true;
        public bool EnableKeyViewer { get; set; } = true;
        public bool JudgmentBarCapsule { get; set; } = false;
        public string JudgmentBarSide { get; set; } = "Center";

        // 3. 눈송이 흔들림 노트 연출 (NoteSway)
        public bool NoteSway { get; set; } = false;
        public float NoteSwayAmplitude { get; set; } = 20.0f;
        public float NoteSwaySpeed { get; set; } = 0.8f;
        public bool NoteSwayDamping { get; set; } = true;
        public float NoteSwayDampingTime { get; set; } = 0.4f;

        // 4. 카오스 노트 스피드 (NoteSpeedChaos)
        public bool NoteSpeedChaos { get; set; } = false;
        public float NoteSpeedChaosMin { get; set; } = 0.6f;
        public float NoteSpeedChaosMax { get; set; } = 1.8f;
        public bool NoteSpeedChaosPerLane { get; set; } = true;

        // 5. 키뷰어 색상 설정
        public string KeyViewerPressedColor { get; set; } = "#26BFD9D9";
        public string KeyViewerNormalColor { get; set; } = "#141414A6";
        public string KeyViewerDropPressedColor { get; set; } = "#FF4081E6";

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
                    MelonLogger.Msg($"[ModConfig] config.txt 설정 파일 로드 완료 (AutoPlay={Instance.AutoPlay}, NoteSway={Instance.NoteSway})");
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

                if (key.Equals("AutoPlay", StringComparison.OrdinalIgnoreCase) || key.Equals("AutoMode", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.AutoPlay = b;
                }
                else if (key.Equals("BlockSave", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.BlockSave = b;
                }
                else if (key.Equals("EnableJudgmentBar", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.EnableJudgmentBar = b;
                }
                else if (key.Equals("JudgmentBarVertical", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.JudgmentBarVertical = b;
                }
                else if (key.Equals("EnableKeyViewer", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.EnableKeyViewer = b;
                }
                else if (key.Equals("NoteSway", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.NoteSway = b;
                }
                else if (key.Equals("NoteSwayAmplitude", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float f)) config.NoteSwayAmplitude = f;
                }
                else if (key.Equals("NoteSwaySpeed", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float f)) config.NoteSwaySpeed = f;
                }
                else if (key.Equals("NoteSwayDamping", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.NoteSwayDamping = b;
                }
                else if (key.Equals("NoteSwayDampingTime", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float f)) config.NoteSwayDampingTime = f;
                }
                else if (key.Equals("NoteSpeedChaos", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.NoteSpeedChaos = b;
                }
                else if (key.Equals("NoteSpeedChaosMin", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float f)) config.NoteSpeedChaosMin = f;
                }
                else if (key.Equals("NoteSpeedChaosMax", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float f)) config.NoteSpeedChaosMax = f;
                }
                else if (key.Equals("NoteSpeedChaosPerLane", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.NoteSpeedChaosPerLane = b;
                }
                else if (key.Equals("JudgmentBarCapsule", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFlexibleBool(val, out bool b)) config.JudgmentBarCapsule = b;
                }
                else if (key.Equals("JudgmentBarSide", StringComparison.OrdinalIgnoreCase))
                {
                    config.JudgmentBarSide = val;
                }
                else if (key.Equals("KeyViewerPressedColor", StringComparison.OrdinalIgnoreCase))
                {
                    config.KeyViewerPressedColor = val;
                }
                else if (key.Equals("KeyViewerNormalColor", StringComparison.OrdinalIgnoreCase))
                {
                    config.KeyViewerNormalColor = val;
                }
                else if (key.Equals("KeyViewerDropPressedColor", StringComparison.OrdinalIgnoreCase) || key.Equals("KeyViewerGatePressedColor", StringComparison.OrdinalIgnoreCase))
                {
                    config.KeyViewerDropPressedColor = val;
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
                normalized == "켜기" || normalized == "켜짐" || normalized == "활성화" ||
                normalized == "on" || normalized == "1" || normalized == "enable" ||
                normalized == "enabled" || normalized == "y" || normalized == "yes")
            {
                result = true;
                return true;
            }

            // False 의미를 갖는 단어들
            if (normalized == "false" || normalized == "폴스" || normalized == "거짓" ||
                normalized == "끄기" || normalized == "꺼짐" || normalized == "비활성화" ||
                normalized == "off" || normalized == "0" || normalized == "disable" ||
                normalized == "disabled" || normalized == "n" || normalized == "no")
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
                    "# 설정 변경 후 저장하면 게임 실행 시 자동 적용됩니다.",
                    "# 지원 형식: 1/0, true/false, 켜짐/꺼짐, on/off",
                    "",
                    "# 오토 플레이 (1 = 켜짐, 0 = 꺼짐)",
                    $"AutoPlay={(Instance.AutoPlay ? 1 : 0)}",
                    "",
                    "# 베스트 스코어 / 랭킹 저장 차단 (1 = 켜짐, 0 = 꺼짐)",
                    $"BlockSave={(Instance.BlockSave ? 1 : 0)}",
                    "",
                    "# 실시간 판정바 표시 (1 = 켜짐, 0 = 꺼짐)",
                    $"EnableJudgmentBar={(Instance.EnableJudgmentBar ? 1 : 0)}",
                    "",
                    "# 판정바 형태 (1 = 세로 판정바, 0 = 가로 판정바)",
                    $"JudgmentBarVertical={(Instance.JudgmentBarVertical ? 1 : 0)}",
                    "",
                    "# 실시간 키뷰어 표시 (1 = 켜짐, 0 = 꺼짐)",
                    $"EnableKeyViewer={(Instance.EnableKeyViewer ? 1 : 0)}",
                    "",
                    "# 노트가 눈송이처럼 좌우로 흔들리며 내려오는 연출 (1 = 켜짐, 0 = 꺼짐)",
                    "# 판정에는 전혀 영향이 없는 순수 시각 효과입니다.",
                    $"NoteSway={(Instance.NoteSway ? 1 : 0)}",
                    "",
                    "# 흔들림 폭 (픽셀). 너무 크면 레인 밖으로 나가 잘릴 수 있습니다.",
                    $"NoteSwayAmplitude={Instance.NoteSwayAmplitude.ToString(CultureInfo.InvariantCulture)}",
                    "",
                    "# 흔들림 속도 (초당 왕복 횟수)",
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
                    "# 판정바 모양 (1 = 알약 캡슐 모양(양끝 둥글게), 0 = 사각 바)",
                    $"JudgmentBarCapsule={(Instance.JudgmentBarCapsule ? 1 : 0)}",
                    "",
                    "# 판정바 위치 (Left = 화면 왼쪽, Right = 화면 오른쪽, Center = 기본 위치)",
                    "# 기본 위치는 세로 판정바는 왼쪽 고정, 가로 판정바는 화면 정중앙입니다.",
                    $"JudgmentBarSide={Instance.JudgmentBarSide}",
                    "",
                    "# 키뷰어 눌림 색상 (일반 노트 키 입력 시)",
                    "# 지원 형식: #RRGGBB, #RRGGBBAA, R,G,B,A, 영문/한글 색상명 (시안, 마젠타, 노랑, 빨강, 파랑, 초록, 흰색, 검정, 주황, 보라, 분홍, 하늘, 민트)",
                    $"KeyViewerPressedColor={Instance.KeyViewerPressedColor}",
                    "",
                    "# 키뷰어 미입력 기본 색상",
                    $"KeyViewerNormalColor={Instance.KeyViewerNormalColor}",
                    "",
                    "# 키뷰어 Drop 키 전용 눌림 색상",
                    $"KeyViewerDropPressedColor={Instance.KeyViewerDropPressedColor}"
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
