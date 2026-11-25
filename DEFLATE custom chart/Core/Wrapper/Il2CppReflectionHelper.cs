using System;
using System.Collections.Concurrent;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;

namespace DEFLATE_custom_chart.Core.Wrapper
{
    /// <summary>
    /// 게임 패치 및 업데이트로 인한 Il2Cpp 클래스/프로퍼티/필드 변경 시
    /// 모드가 예외로 튕기지 않고 백업 리플렉션과 가드로 안전하게 동작하도록 지원하는 내성 강화 유틸리티
    /// </summary>
    public static class Il2CppReflectionHelper
    {
        private const BindingFlags DeclaredInstanceFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly;

        private static readonly ConcurrentDictionary<(Type, string), PropertyInfo> PropertyCache = new ConcurrentDictionary<(Type, string), PropertyInfo>();
        private static readonly ConcurrentDictionary<(Type, string), FieldInfo> FieldCache = new ConcurrentDictionary<(Type, string), FieldInfo>();
        private static readonly ConcurrentDictionary<(Type, string), MethodInfo> MethodCache = new ConcurrentDictionary<(Type, string), MethodInfo>();
        private static readonly ConcurrentDictionary<string, byte> LoggedWarnings = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        /// <summary>
        /// 게임 업데이트로 <b>구조적으로</b> 깨진 직접 접근(람다) 목록. (대상 런타임 타입, 멤버 이름, 'g'/'s'/'i' = 읽기/쓰기/호출)
        ///
        /// 직접 접근 람다는 참조 멤버가 사라지면 호출될 때마다 JIT 단계에서 <see cref="MissingMemberException"/>/<see cref="TypeLoadException"/>을
        /// 다시 던집니다(JIT 실패는 캐시되지 않음). 예전에는 업데이트 후 곡 목록 생성(원곡 약 116곡 × 래퍼 멤버 18개)마다 수천 번의 예외가
        /// 나며 화면이 멈칫할 수 있었습니다. 한 번 구조적으로 실패한 직접 접근은 기억해 두고 곧장 백업 리플렉션으로 갑니다.
        /// NullReference·Il2Cpp 객체 수거 같은 <b>일시적</b> 실패는 기록하지 않습니다(다음 호출에서 다시 직접 접근).
        /// </summary>
        private static readonly ConcurrentDictionary<(Type, string, char), byte> BrokenDirectAccess = new ConcurrentDictionary<(Type, string, char), byte>();

        private static void WarnOnce(string warnKey, string message)
        {
            if (LoggedWarnings.TryAdd(warnKey, 0))
            {
                MelonLogger.Warning(message);
            }
        }

        /// <summary>업데이트로 멤버·타입이 사라져 생기는 예외인지 (재시도해도 같은 결과).</summary>
        internal static bool IsStructuralFailure(Exception ex)
        {
            return ex is MissingMemberException || ex is TypeLoadException || ex is BadImageFormatException;
        }

        private static bool IsDirectAccessBroken(object target, string memberName, char kind)
        {
            if (BrokenDirectAccess.IsEmpty || string.IsNullOrEmpty(memberName)) return false;
            return BrokenDirectAccess.ContainsKey((target.GetType(), memberName, kind));
        }

        /// <summary>구조적 실패면 기록하고 경고 문구에 덧붙일 안내를, 아니면 빈 문자열을 돌려줍니다.</summary>
        private static string RecordDirectAccessFailure(object target, string memberName, char kind, Exception ex)
        {
            if (!IsStructuralFailure(ex) || string.IsNullOrEmpty(memberName)) return string.Empty;
            BrokenDirectAccess.TryAdd((target.GetType(), memberName, kind), 0);
            return " 게임 업데이트로 멤버가 사라진 것으로 보고, 이번 실행 동안 이 멤버는 직접 접근을 생략합니다(MemberFallbacks.cs에 새 이름 추가 권장).";
        }

        /// <summary>
        /// Harmony 패치 클래스의 Prepare()에서 대상 메서드의 실제 존재 여부를 사전 확인합니다.
        /// 게임 업데이트로 비핵심/문자열 바인딩 메서드가 삭제되거나 이름이 바뀌어도
        /// MelonLoader의 PatchAll() 전체가 중단되지 않고 해당 훅만 안전하게 건너뛰도록 보호합니다.
        /// </summary>
        public static bool HasPatchTarget(Type targetType, string methodName)
        {
            if (targetType == null || string.IsNullOrEmpty(methodName)) return false;

            const BindingFlags anyMethodFlags =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            try
            {
                for (Type t = targetType; t != null && t != typeof(object); t = t.BaseType)
                {
                    var methods = t.GetMethods(anyMethodFlags);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        if (string.Equals(methods[i].Name, methodName, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WarnOnce($"harmony_err:{targetType.FullName}:{methodName}",
                    $"[HarmonyGuard] '{targetType.Name}.{methodName}' 탐색 중 예외 ({ex.GetType().Name}): {ex.Message}");
                return false;
            }

            WarnOnce($"harmony_miss:{targetType.FullName}:{methodName}",
                $"[HarmonyGuard] 패치 대상 메서드를 찾지 못해 훅을 건너뜁니다: '{targetType.Name}.{methodName}' (게임 업데이트로 시그니처가 변경되었을 수 있습니다)");
            return false;
        }

        /// <summary>
        /// Il2CppObjectBase 인스턴스 또는 object를 안전하게 T 타입으로 Cast합니다.
        /// </summary>
        public static T SafeCast<T>(object obj) where T : Il2CppObjectBase
        {
            if (obj == null) return null;
            if (obj is T directMatch) return directMatch;

            if (obj is Il2CppObjectBase il2cppObj)
            {
                try
                {
                    return il2cppObj.TryCast<T>();
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SafeCast] {obj.GetType().Name} -> {typeof(T).Name} Il2Cpp TryCast 실패: {ex.Message}");
                }
            }
            return null;
        }

        /// <summary>
        /// 직접 프로퍼티/필드 읽기를 우선 시도하고, 실패 시 백업 룩업 및 리플렉션 탐색으로 안전하게 값을 구합니다.
        /// </summary>
        public static T GetValueResilient<T>(object target, Func<T> directGetter, string directPropertyName, string[] fallbackNames, T defaultValue = default)
        {
            if (target == null) return defaultValue;

            // 1. 직접 접근 (Fast Path)
            if (directGetter != null && !IsDirectAccessBroken(target, directPropertyName, 'g'))
            {
                try
                {
                    T result = directGetter();
                    return result != null ? result : defaultValue;
                }
                catch (Exception ex)
                {
                    string note = RecordDirectAccessFailure(target, directPropertyName, 'g', ex);
                    WarnOnce($"get:{target.GetType().FullName}:{directPropertyName}",
                        $"[GetValueResilient] 직접 접근 '{directPropertyName}' 실패 ({ex.GetType().Name}), 백업 리플렉션을 시도합니다.{note}");
                }
            }

            Type targetType = target.GetType();
            if (!string.IsNullOrEmpty(directPropertyName) && TryReadMember(target, targetType, directPropertyName, out T directVal))
            {
                return directVal;
            }

            if (fallbackNames != null)
            {
                for (int i = 0; i < fallbackNames.Length; i++)
                {
                    string name = fallbackNames[i];
                    if (!string.IsNullOrEmpty(name) && TryReadMember(target, targetType, name, out T fallbackVal))
                    {
                        return fallbackVal;
                    }
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// 직접 프로퍼티/필드 쓰기를 우선 시도하고, 실패 시 백업 룩업 및 리플렉션 탐색으로 안전하게 값을 주입합니다.
        /// </summary>
        public static bool SetValueResilient(object target, Action directSetter, string directPropertyName, string[] fallbackNames, object value)
        {
            if (target == null) return false;

            // 1. 직접 접근 (Fast Path)
            if (directSetter != null && !IsDirectAccessBroken(target, directPropertyName, 's'))
            {
                try
                {
                    directSetter();
                    return true;
                }
                catch (Exception ex)
                {
                    string note = RecordDirectAccessFailure(target, directPropertyName, 's', ex);
                    WarnOnce($"set:{target.GetType().FullName}:{directPropertyName}",
                        $"[SetValueResilient] 직접 접근 '{directPropertyName}' 주입 실패 ({ex.GetType().Name}), 백업 리플렉션을 시도합니다.{note}");
                }
            }

            Type targetType = target.GetType();
            if (!string.IsNullOrEmpty(directPropertyName) && TryWriteMember(target, targetType, directPropertyName, value))
            {
                return true;
            }

            if (fallbackNames != null)
            {
                for (int i = 0; i < fallbackNames.Length; i++)
                {
                    string name = fallbackNames[i];
                    if (!string.IsNullOrEmpty(name) && TryWriteMember(target, targetType, name, value))
                    {
                        return true;
                    }
                }
            }

            WarnOnce($"setfail:{targetType.FullName}:{directPropertyName}",
                $"[SetValueResilient] '{targetType.Name}' 객체에서 '{directPropertyName}' 프로퍼티/필드를 찾지 못해 값 주입에 실패했습니다.");
            return false;
        }

        /// <summary>
        /// 매개변수 없는 메서드를 직접 호출 우선으로 실행하고, 실패 시 상속 체인까지 백업 리플렉션으로 탐색해 호출합니다.
        /// </summary>
        public static bool InvokeMethodResilient(object target, Action directCall, string methodName, string[] fallbackNames = null)
        {
            if (target == null) return false;

            if (directCall != null && !IsDirectAccessBroken(target, methodName, 'i'))
            {
                try
                {
                    directCall();
                    return true;
                }
                catch (Exception ex)
                {
                    string note = RecordDirectAccessFailure(target, methodName, 'i', ex);
                    WarnOnce($"invoke:{target.GetType().FullName}:{methodName}",
                        $"[InvokeMethodResilient] 직접 호출 '{methodName}' 실패 ({ex.GetType().Name}), 백업 리플렉션을 시도합니다.{note}");
                }
            }

            Type targetType = target.GetType();
            if (!string.IsNullOrEmpty(methodName) && TryInvokeParameterlessMethod(target, targetType, methodName))
            {
                return true;
            }

            if (fallbackNames != null)
            {
                for (int i = 0; i < fallbackNames.Length; i++)
                {
                    string name = fallbackNames[i];
                    if (!string.IsNullOrEmpty(name) && TryInvokeParameterlessMethod(target, targetType, name))
                    {
                        return true;
                    }
                }
            }

            WarnOnce($"invokefail:{targetType.FullName}:{methodName}",
                $"[InvokeMethodResilient] '{targetType.Name}' 객체에서 '{methodName}' 메서드를 호출하지 못했습니다.");
            return false;
        }

        private static bool TryReadMember<T>(object target, Type targetType, string name, out T value)
        {
            PropertyInfo prop = FindPropertyResilient(targetType, name);
            if (prop != null && prop.CanRead)
            {
                try
                {
                    if (TryConvertValue(prop.GetValue(target), out value))
                    {
                        return true;
                    }
                }
                catch { }
            }

            FieldInfo field = FindFieldResilient(targetType, name);
            if (field != null)
            {
                try
                {
                    if (TryConvertValue(field.GetValue(target), out value))
                    {
                        return true;
                    }
                }
                catch { }
            }

            value = default;
            return false;
        }

        private static bool TryWriteMember(object target, Type targetType, string name, object value)
        {
            PropertyInfo prop = FindPropertyResilient(targetType, name);
            if (prop != null && prop.CanWrite)
            {
                try
                {
                    if (TryAdaptValueForTargetType(value, prop.PropertyType, out object adapted))
                    {
                        prop.SetValue(target, adapted);
                        return true;
                    }
                }
                catch { }
            }

            FieldInfo field = FindFieldResilient(targetType, name);
            if (field != null && !field.IsInitOnly && !field.IsLiteral)
            {
                try
                {
                    if (TryAdaptValueForTargetType(value, field.FieldType, out object adapted))
                    {
                        field.SetValue(target, adapted);
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        private static bool TryAdaptValueForTargetType(object value, Type memberType, out object adapted)
        {
            if (value == null)
            {
                if (!memberType.IsValueType || Nullable.GetUnderlyingType(memberType) != null)
                {
                    adapted = null;
                    return true;
                }
                adapted = null;
                return false;
            }

            if (memberType.IsInstanceOfType(value))
            {
                adapted = value;
                return true;
            }

            if (memberType == typeof(string))
            {
                adapted = value.ToString();
                return true;
            }

            try
            {
                Type underlying = Nullable.GetUnderlyingType(memberType) ?? memberType;
                if (underlying.IsEnum)
                {
                    adapted = value is string s
                        ? Enum.Parse(underlying, s, true)
                        : Enum.ToObject(underlying, value);
                    return true;
                }

                adapted = Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                adapted = null;
                return false;
            }
        }

        private static bool TryInvokeParameterlessMethod(object target, Type targetType, string name)
        {
            MethodInfo method = FindMethodResilient(targetType, name);
            if (method == null) return false;

            try
            {
                method.Invoke(target, null);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[InvokeMethodResilient] 리플렉션 '{name}' 호출 실패: {ex.Message}");
                return false;
            }
        }

        private static bool TryConvertValue<T>(object val, out T result)
        {
            if (val is T typedVal)
            {
                result = typedVal;
                return true;
            }

            if (val != null)
            {
                if (typeof(T) == typeof(string))
                {
                    result = (T)(object)val.ToString();
                    return true;
                }

                Type underlying = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                result = (T)Convert.ChangeType(val, underlying, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            result = default;
            return false;
        }

        private static PropertyInfo FindPropertyResilient(Type type, string name)
        {
            return PropertyCache.GetOrAdd((type, name), key =>
            {
                for (Type t = key.Item1; t != null && t != typeof(object); t = t.BaseType)
                {
                    PropertyInfo prop = t.GetProperty(key.Item2, DeclaredInstanceFlags);
                    if (prop != null) return prop;
                }
                return null;
            });
        }

        private static FieldInfo FindFieldResilient(Type type, string name)
        {
            return FieldCache.GetOrAdd((type, name), key =>
            {
                for (Type t = key.Item1; t != null && t != typeof(object); t = t.BaseType)
                {
                    FieldInfo field = t.GetField(key.Item2, DeclaredInstanceFlags);
                    if (field != null) return field;
                }
                return null;
            });
        }

        private static MethodInfo FindMethodResilient(Type type, string name)
        {
            return MethodCache.GetOrAdd((type, name), key =>
            {
                for (Type t = key.Item1; t != null && t != typeof(object); t = t.BaseType)
                {
                    MethodInfo method = t.GetMethod(key.Item2, DeclaredInstanceFlags, null, Type.EmptyTypes, null);
                    if (method != null) return method;
                }
                return null;
            });
        }
    }
}
