using MelonLoader;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>
    /// 로그 상세도 게이트 (config.txt <c>LogLevel</c>).
    /// <list type="bullet">
    /// <item>0 = 요약(기본): 시작 요약, 곡 단위 한 줄 요약, 경고/오류만.</item>
    /// <item>1 = 상세: 레인별 주입 수, 에셋 오버라이드, 씬별 메타데이터 주입 등.</item>
    /// <item>2 = 디버그: 커서 이동·클릭·난이도 변경·드럼 모드 전환·10초 진행률 등 이벤트마다 기록.</item>
    /// </list>
    /// 경고/오류는 레벨과 무관하게 항상 <see cref="MelonLogger.Warning(string)"/> / <see cref="MelonLogger.Error(string)"/>로 남깁니다.
    /// 로그만 찍는 훅은 <see cref="Prepare"/>에서 레벨이 모자라면 아예 패치하지 않아 런타임 비용도 없앱니다.
    /// 문자열 보간 비용이 큰 곳은 <c>if (ModLog.IsVerbose)</c>로 감싸 호출 전에 거르세요.
    /// </summary>
    public static class ModLog
    {
        public const int Summary = 0;
        public const int Detail = 1;
        public const int DebugLevel = 2;

        public static int Level => ModConfig.Instance.LogLevel;
        public static bool IsVerbose => Level >= Detail;
        public static bool IsDebug => Level >= DebugLevel;

        /// <summary>항상 기록 (요약 수준).</summary>
        public static void Info(string message) => MelonLogger.Msg(message);

        /// <summary>LogLevel ≥ 1에서만 기록.</summary>
        public static void Verbose(string message)
        {
            if (Level >= Detail) MelonLogger.Msg(message);
        }

        /// <summary>LogLevel ≥ 2에서만 기록.</summary>
        public static void Debug(string message)
        {
            if (Level >= DebugLevel) MelonLogger.Msg(message);
        }

        /// <summary>로그 전용 훅의 Prepare(): 지정 레벨 미만이면 패치 자체를 건너뜁니다.</summary>
        public static bool Prepare(int requiredLevel) => Level >= requiredLevel;
    }
}
