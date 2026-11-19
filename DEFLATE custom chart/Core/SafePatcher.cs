using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MelonLoader;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>
    /// MelonLoader 자동 <c>PatchAll()</c>을 대체하는 패치 클래스 단위 격리 패처.
    ///
    /// <b>왜 필요한가:</b> Harmony의 <c>PatchAll()</c>은 패치 클래스를 어셈블리 정의 순서대로 처리하다가
    /// 하나라도 예외(대상 메서드 삭제·개명, 오버로드 추가로 인한 모호성, 파라미터명 변경 등)를 던지면
    /// <b>그 뒤의 모든 훅이 적용되지 않은 채</b> 중단됩니다. <c>nameof(...)</c>로 쓴 대상명도 컴파일 시점에
    /// 문자열로 박제되므로, 게임 업데이트 뒤 재빌드 전의 DLL에서는 문자열 기반 훅과 똑같이 깨질 수 있습니다.
    ///
    /// <b>무엇을 하나:</b>
    /// <list type="number">
    /// <item>패치 클래스마다 <c>CreateClassProcessor(type).Patch()</c>를 개별 try/catch로 실행해 실패를 그 클래스로 한정.</item>
    /// <item>패치 전 Prefix/Postfix 등의 본문을 <see cref="RuntimeHelpers.PrepareMethod(RuntimeMethodHandle)"/>로 미리 JIT해,
    ///   게임에서 사라진 필드/프로퍼티/타입을 참조하는 훅을 <b>적용 전에</b> 걸러냄(최선 노력 검사).
    ///   훅 본문이 깨진 채 적용되면 호출 시점 JIT 예외가 훅 내부 try/catch 바깥에서 터져,
    ///   Prefix의 경우 원본 메서드까지 실행되지 못하기 때문.</item>
    /// <item>마지막에 적용/건너뜀/실패 요약을 1회 출력 → 업데이트 직후 <c>Latest.log</c>에서 <c>[SafePatcher]</c>로 즉시 진단.</item>
    /// </list>
    /// <c>Prepare()</c>가 false를 돌려 건너뛴 훅은 오류가 아니라 '건너뜀'으로 집계됩니다.
    /// </summary>
    public static class SafePatcher
    {
        private static readonly string[] PatchMethodNames = { "Prefix", "Postfix", "Finalizer", "Transpiler" };

        public sealed class Report
        {
            public int Applied;
            public int Skipped;
            public readonly List<string> Failed = new List<string>();
            public readonly List<string> SkippedNames = new List<string>();
        }

        /// <summary>마지막 <see cref="PatchAll"/> 결과. 진단/로그용.</summary>
        public static Report LastReport { get; private set; }

        public static Report PatchAll(HarmonyLib.Harmony harmony, Assembly assembly)
        {
            var report = new Report();
            LastReport = report;
            if (harmony == null || assembly == null) return report;

            List<Type> patchTypes;
            try
            {
                patchTypes = AccessTools.GetTypesFromAssembly(assembly)
                    .Where(t => t != null && t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SafePatcher] 패치 클래스 목록 수집 실패: {ex.GetType().Name}: {ex.Message}");
                return report;
            }

            foreach (var type in patchTypes)
            {
                string name = DescribeType(type);

                if (!TryPrejitPatchMethods(type, out string jitError))
                {
                    report.Failed.Add(name);
                    MelonLogger.Error($"[SafePatcher] '{name}' 훅 본문이 현재 게임에 없는 멤버를 참조해 적용하지 않습니다: {jitError}");
                    continue;
                }

                try
                {
                    var patched = harmony.CreateClassProcessor(type).Patch();
                    if (patched == null || patched.Count == 0)
                    {
                        report.Skipped++;
                        report.SkippedNames.Add(name);
                    }
                    else
                    {
                        report.Applied++;
                    }
                }
                catch (Exception ex)
                {
                    report.Failed.Add(name);
                    Exception root = ex;
                    while (root.InnerException != null) root = root.InnerException;
                    MelonLogger.Error($"[SafePatcher] '{name}' 패치 실패 (나머지 훅은 계속 적용): {root.GetType().Name}: {root.Message}");
                }
            }

            WarnBrokenHelpers(assembly);

            string summary = $"[SafePatcher] 훅 적용 요약: 성공 {report.Applied} / 건너뜀(Prepare=false) {report.Skipped} / 실패 {report.Failed.Count} (총 {patchTypes.Count})";
            if (report.Failed.Count > 0)
            {
                MelonLogger.Warning(summary);
                MelonLogger.Warning($"[SafePatcher] 실패 훅: {string.Join(", ", report.Failed)} → AGENTS.md '업데이트 대응 플레이북' 참고");
            }
            else
            {
                MelonLogger.Msg(summary);
            }
            if (report.SkippedNames.Count > 0)
            {
                MelonLogger.Msg($"[SafePatcher] 건너뛴 훅: {string.Join(", ", report.SkippedNames)}");
            }

            return report;
        }

        /// <summary>패치 클래스의 Prefix/Postfix/Finalizer/Transpiler(이름 또는 Harmony 속성 지정)를 미리 JIT합니다.</summary>
        private static bool TryPrejitPatchMethods(Type patchType, out string error)
        {
            error = null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var method in patchType.GetMethods(flags))
            {
                if (!IsPatchMethod(method) || method.ContainsGenericParameters) continue;
                try
                {
                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                }
                catch (Exception ex)
                {
                    error = $"{method.Name} → {ex.GetType().Name}: {ex.Message}";
                    return false;
                }
            }
            return true;
        }

        private static bool IsPatchMethod(MethodInfo method)
        {
            if (Array.IndexOf(PatchMethodNames, method.Name) >= 0) return true;
            return method.GetCustomAttributes(typeof(HarmonyPrefix), false).Length > 0
                || method.GetCustomAttributes(typeof(HarmonyPostfix), false).Length > 0
                || method.GetCustomAttributes(typeof(HarmonyFinalizer), false).Length > 0
                || method.GetCustomAttributes(typeof(HarmonyTranspiler), false).Length > 0;
        }

        /// <summary>
        /// 훅 컨테이너(…Hooks 정적 클래스)의 공용 헬퍼 메서드도 미리 JIT해, 게임 업데이트로 깨진 헬퍼를 경고만 합니다.
        /// 헬퍼는 여러 훅이 공유하므로 훅을 막지 않고, 어떤 헬퍼가 깨졌는지 로그로 알려 수정 지점을 좁힙니다.
        /// 람다/코루틴 등 컴파일러 생성 타입은 의도적으로 실패를 흡수하는 백업 경로(directGetter 등)이므로 제외합니다.
        /// </summary>
        private static void WarnBrokenHelpers(Assembly assembly)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
            try
            {
                foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
                {
                    if (type == null || type.IsNested || type.Namespace == null) continue;
                    if (!type.Namespace.EndsWith(".Hooks", StringComparison.Ordinal)) continue;

                    foreach (var method in type.GetMethods(flags))
                    {
                        if (method.ContainsGenericParameters || method.Name.IndexOf('<') >= 0) continue;
                        try
                        {
                            RuntimeHelpers.PrepareMethod(method.MethodHandle);
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"[SafePatcher] 헬퍼 '{type.Name}.{method.Name}'가 현재 게임에 없는 멤버를 참조합니다 (이 헬퍼를 쓰는 훅이 동작하지 않을 수 있음): {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SafePatcher] 헬퍼 사전 검사 중 예외: {ex.Message}");
            }
        }

        private static string DescribeType(Type type)
        {
            return type.DeclaringType != null ? $"{type.DeclaringType.Name}.{type.Name}" : type.Name;
        }
    }
}
