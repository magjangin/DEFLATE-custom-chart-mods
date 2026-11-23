using System;
using System.Runtime.CompilerServices;
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
    ///
    /// <b>지연 보간 (v1.1.2):</b> <c>ModLog.Verbose($"... {player.url} ...")</c>처럼 보간 문자열을 넘기면 C# 10
    /// 보간 문자열 핸들러(<see cref="VerboseHandler"/> / <see cref="DebugHandler"/>)가 선택되어, 레벨이 모자라면
    /// <b>구멍(<c>{...}</c>) 안의 식 자체를 평가하지 않습니다.</b> 예전에는 LogLevel 0에서도 모든 Verbose 호출이
    /// 문자열을 만들었고, 구멍 안의 <c>videoPlayer.name</c>·<c>player.url</c>·<c>__instance.hitWindowRangeInMS</c> 같은
    /// IL2CPP 프로퍼티 읽기(네이티브 호출 + 문자열 마샬링)까지 매번 실행했습니다(VideoPlayer 훅·레인 재로드 등).
    /// 그러므로 <b>구멍 안에 부수효과가 있는 식을 쓰지 마세요</b>(레벨에 따라 실행 여부가 달라집니다).
    /// 이 핸들러는 ref struct라 코루틴(iterator) 안에서는 쓸 수 없습니다 → 코루틴에서는 MelonLogger를 직접 쓰거나 일반 메서드로 분리하세요.
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

        /// <summary>LogLevel ≥ 1에서만 기록 (이미 만들어진 문자열).</summary>
        public static void Verbose(string message)
        {
            if (Level >= Detail) MelonLogger.Msg(message);
        }

        /// <summary>LogLevel ≥ 1에서만 기록. 보간 문자열이면 레벨이 모자랄 때 구멍을 평가하지 않습니다.</summary>
        public static void Verbose(ref VerboseHandler message)
        {
            if (message.Enabled) MelonLogger.Msg(message.ToStringAndClear());
        }

        /// <summary>LogLevel ≥ 2에서만 기록 (이미 만들어진 문자열).</summary>
        public static void Debug(string message)
        {
            if (Level >= DebugLevel) MelonLogger.Msg(message);
        }

        /// <summary>LogLevel ≥ 2에서만 기록. 보간 문자열이면 레벨이 모자랄 때 구멍을 평가하지 않습니다.</summary>
        public static void Debug(ref DebugHandler message)
        {
            if (message.Enabled) MelonLogger.Msg(message.ToStringAndClear());
        }

        /// <summary>로그 전용 훅의 Prepare(): 지정 레벨 미만이면 패치 자체를 건너뜁니다.</summary>
        public static bool Prepare(int requiredLevel) => Level >= requiredLevel;

        /// <summary>LogLevel ≥ 1일 때만 문자열을 조립하는 보간 문자열 핸들러.</summary>
        [InterpolatedStringHandler]
        public ref struct VerboseHandler
        {
            private DefaultInterpolatedStringHandler _inner;
            public readonly bool Enabled;

            public VerboseHandler(int literalLength, int formattedCount, out bool shouldAppend)
            {
                Enabled = shouldAppend = Level >= Detail;
                _inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
            }

            public void AppendLiteral(string value) => _inner.AppendLiteral(value);
            public void AppendFormatted<T>(T value) => _inner.AppendFormatted(value);
            public void AppendFormatted<T>(T value, string format) => _inner.AppendFormatted(value, format);
            public void AppendFormatted<T>(T value, int alignment) => _inner.AppendFormatted(value, alignment);
            public void AppendFormatted<T>(T value, int alignment, string format) => _inner.AppendFormatted(value, alignment, format);
            public void AppendFormatted(string value) => _inner.AppendFormatted(value);
            internal string ToStringAndClear() => _inner.ToStringAndClear();
        }

        /// <summary>LogLevel ≥ 2일 때만 문자열을 조립하는 보간 문자열 핸들러.</summary>
        [InterpolatedStringHandler]
        public ref struct DebugHandler
        {
            private DefaultInterpolatedStringHandler _inner;
            public readonly bool Enabled;

            public DebugHandler(int literalLength, int formattedCount, out bool shouldAppend)
            {
                Enabled = shouldAppend = Level >= DebugLevel;
                _inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
            }

            public void AppendLiteral(string value) => _inner.AppendLiteral(value);
            public void AppendFormatted<T>(T value) => _inner.AppendFormatted(value);
            public void AppendFormatted<T>(T value, string format) => _inner.AppendFormatted(value, format);
            public void AppendFormatted<T>(T value, int alignment) => _inner.AppendFormatted(value, alignment);
            public void AppendFormatted<T>(T value, int alignment, string format) => _inner.AppendFormatted(value, alignment, format);
            public void AppendFormatted(string value) => _inner.AppendFormatted(value);
            internal string ToStringAndClear() => _inner.ToStringAndClear();
        }
    }
}
