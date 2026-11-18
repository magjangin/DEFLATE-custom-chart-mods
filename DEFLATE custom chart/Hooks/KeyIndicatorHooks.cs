using HarmonyLib;
using Il2Cppdizzylab.castor;
using MelonLoader;
using DEFLATE_custom_chart.Core.Wrapper;

namespace DEFLATE_custom_chart.Hooks
{
    /// <summary>
    /// 곡 시작 시 나타나는 키 가이드 안내 UI(Key_indicator) 차단 훅
    /// </summary>
    public static class KeyIndicatorHooks
    {
        [HarmonyPatch(typeof(Key_indicator), nameof(Key_indicator.Start))]
        public static class Key_indicator_Start_Patch
        {
            public static bool Prepare() => Il2CppReflectionHelper.HasPatchTarget(typeof(Key_indicator), nameof(Key_indicator.Start));

            public static bool Prefix(Key_indicator __instance)
            {
                try
                {
                    if (__instance != null && __instance.gameObject != null)
                    {
                        __instance.gameObject.SetActive(false);
                    }
                    MelonLogger.Msg("[Key_indicator] 곡 시작 키 가이드 안내 UI 비활성화 완료.");
                    return false; // 원본 Start 애니메이션 실행 차단
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Warning($"[Key_indicator] Start Prefix 처리 중 예외: {ex.Message}");
                    return true;
                }
            }
        }
    }
}
