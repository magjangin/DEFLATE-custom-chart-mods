using MelonLoader;
using HarmonyLib;
using Il2Cppdizzylab.castor;

namespace DEFLATE_custom_chart.Hooks
{
    public static class NoteHooks
    {
        [HarmonyPatch(typeof(RhythmGameController), "RecalculateAllNoteCount")]
        public static class RhythmGameController_RecalculateAllNoteCount_Patch
        {
            public static void Postfix(RhythmGameController __instance)
            {
                if (__instance == null) return;
                MelonLogger.Msg($"[노트 수 재계산 완료] 전체 노트 수(AllNoteCount): {__instance.AllNoteCount}");
            }
        }
    }
}
