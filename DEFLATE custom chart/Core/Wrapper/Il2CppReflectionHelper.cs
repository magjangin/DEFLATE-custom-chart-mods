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
        private static readonly ConcurrentDictionary<string, PropertyInfo> PropertyCache = new ConcurrentDictionary<string, PropertyInfo>();
        private static readonly ConcurrentDictionary<string, FieldInfo> FieldCache = new ConcurrentDictionary<string, FieldInfo>();

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
                    if (result != null) return result;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[GetValueResilient] 직접 접근 '{directPropertyName}' 실패 ({ex.GetType().Name}), 백업 리플렉션을 시도합니다.");
                }
            }

            Type targetType = target.GetType();
            var candidateNames = CombineCandidates(directPropertyName, fallbackNames);

            foreach (var name in candidateNames)
            {
                if (string.IsNullOrEmpty(name)) continue;

                // 2-1. Property 읽기 시도
                string propKey = $"{targetType.FullName}.P.{name}";
                PropertyInfo prop = PropertyCache.GetOrAdd(propKey, _ =>
                    targetType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase));

                if (prop != null && prop.CanRead)
                {
                    try
                    {
                        object val = prop.GetValue(target);
                        if (val is T typedVal) return typedVal;
                        if (val != null && typeof(T) == typeof(string)) return (T)(object)val.ToString();
                        if (val != null) return (T)Convert.ChangeType(val, typeof(T));
                    }
                    catch { }
                }

                // 2-2. Field 읽기 시도
                string fieldKey = $"{targetType.FullName}.F.{name}";
                FieldInfo field = FieldCache.GetOrAdd(fieldKey, _ =>
                    targetType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase));

                if (field != null)
                {
                    try
                    {
                        object val = field.GetValue(target);
                        if (val is T typedVal) return typedVal;
                        if (val != null && typeof(T) == typeof(string)) return (T)(object)val.ToString();
                        if (val != null) return (T)Convert.ChangeType(val, typeof(T));
                    }
                    catch { }
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
                    MelonLogger.Warning($"[SetValueResilient] 직접 접근 '{directPropertyName}' 주입 실패 ({ex.GetType().Name}), 백업 리플렉션을 시도합니다.");
                }
            }

            Type targetType = target.GetType();
            var candidateNames = CombineCandidates(directPropertyName, fallbackNames);

            foreach (var name in candidateNames)
            {
                if (string.IsNullOrEmpty(name)) continue;

                // 2-1. Property 주입 시도
                string propKey = $"{targetType.FullName}.P.{name}";
                PropertyInfo prop = PropertyCache.GetOrAdd(propKey, _ =>
                    targetType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase));

                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        prop.SetValue(target, value);
                        return true;
                    }
                    catch { }
                }

                // 2-2. Field 주입 시도
                string fieldKey = $"{targetType.FullName}.F.{name}";
                FieldInfo field = FieldCache.GetOrAdd(fieldKey, _ =>
                    targetType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase));

                if (field != null)
                {
                    try
                    {
                        field.SetValue(target, value);
                        return true;
                    }
                    catch { }
                }
            }

            MelonLogger.Warning($"[SetValueResilient] '{targetType.Name}' 객체에서 '{directPropertyName}' 프로퍼티/필드를 찾지 못해 값 주입에 실패했습니다.");
            return false;
        }

        private static string[] CombineCandidates(string primary, string[] fallbacks)
        {
            int fallbackLen = fallbacks?.Length ?? 0;
            var result = new string[1 + fallbackLen];
            result[0] = primary;
            if (fallbackLen > 0)
            {
                Array.Copy(fallbacks, 0, result, 1, fallbackLen);
            }
            return result;
        }
    }
}
