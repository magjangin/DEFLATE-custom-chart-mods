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

        private static void WarnOnce(string warnKey, string message)
        {
            if (LoggedWarnings.TryAdd(warnKey, 0))
            {
                MelonLogger.Warning(message);
            }
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
            if (directGetter != null)
            {
                try
                {
                    T result = directGetter();
                    return result != null ? result : defaultValue;
                }
                catch (Exception ex)
                {
                    WarnOnce($"get:{target.GetType().FullName}:{directPropertyName}",
                        $"[GetValueResilient] 직접 접근 '{directPropertyName}' 실패 ({ex.GetType().Name}), 백업 리플렉션을 시도합니다.");
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
            if (directSetter != null)
            {
                try
                {
                    directSetter();
                    return true;
                }
                catch (Exception ex)
                {
                    WarnOnce($"set:{target.GetType().FullName}:{directPropertyName}",
                        $"[SetValueResilient] 직접 접근 '{directPropertyName}' 주입 실패 ({ex.GetType().Name}), 백업 리플렉션을 시도합니다.");
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

            if (directCall != null)
            {
                try
                {
                    directCall();
                    return true;
                }
                catch (Exception ex)
                {
                    WarnOnce($"invoke:{target.GetType().FullName}:{methodName}",
                        $"[InvokeMethodResilient] 직접 호출 '{methodName}' 실패 ({ex.GetType().Name}), 백업 리플렉션을 시도합니다.");
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
