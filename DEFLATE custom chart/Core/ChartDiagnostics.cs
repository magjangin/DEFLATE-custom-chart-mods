using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2Cppdizzylab.castor;
using MelonLoader;
using UnityEngine;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>
    /// 디버그 진단 (LogLevel 2): 레인 배치와 숙주 원곡 차트 구조를 기록합니다.
    /// 커스텀 BMS를 어떤 LaneController에 넣어야 하는지(하이햇/킥·스네어 세트가 같은 열을 공유하는지,
    /// 원곡에서 두 세트가 동시에 나오는지)를 추측이 아닌 실측으로 판단하기 위한 도구입니다.
    /// </summary>
    public static class ChartDiagnostics
    {
        private static readonly Dictionary<string, List<int>> OriginalLaneSamples = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private static bool _dumped;

        public static void Reset()
        {
            OriginalLaneSamples.Clear();
            _dumped = false;
        }

        /// <summary>교체 직전 원본 레인 이벤트의 StartSample을 기록합니다.</summary>
        public static void RecordOriginalLane(string trackID, LaneController lane)
        {
            if (!ModLog.IsDebug || lane == null || lane.laneEvents == null || string.IsNullOrEmpty(trackID)) return;
            var list = new List<int>(lane.laneEvents.Count);
            for (int i = 0; i < lane.laneEvents.Count; i++)
            {
                var ev = lane.laneEvents[i];
                if (ev != null) list.Add(ev.StartSample);
            }
            OriginalLaneSamples[trackID] = list;
        }

        public static void DumpOnce(RhythmGameController c)
        {
            if (!ModLog.IsDebug || _dumped || c == null) return;
            _dumped = true;
            try
            {
                MelonLogger.Msg("[진단] ===== 레인 배치 (LaneController) =====");
                DumpLane("kick_left", c.lane_kick_left);
                DumpLane("kick_right", c.lane_kick_right);
                DumpLane("snare_left", c.lane_snare_left);
                DumpLane("snare_right", c.lane_snare_right);
                DumpLane("hihat_1", c.lane_hihat_1);
                DumpLane("hihat_2", c.lane_hihat_2);
                DumpLane("hihat_3", c.lane_hihat_3);
                DumpLane("hihat_4", c.lane_hihat_4);
                DumpLane("drop", c.lane_drop);

                int rate = c.playingKoreo != null && c.playingKoreo.SampleRate > 0 ? c.playingKoreo.SampleRate : 44100;
                MelonLogger.Msg($"[진단] ===== 숙주 원곡 차트 레인 구성 (샘플레이트 {rate}Hz) =====");
                foreach (var kv in OriginalLaneSamples.OrderBy(k => k.Key))
                {
                    var s = kv.Value;
                    string first = string.Join(", ", s.Take(6).Select(x => (x / (double)rate).ToString("F2")));
                    string range = s.Count > 0 ? $"{s.Min() / (double)rate:F1}~{s.Max() / (double)rate:F1}s" : "-";
                    MelonLogger.Msg($"[진단]   {kv.Key,-12} {s.Count,4}개 | 범위 {range} | 처음: {first}");
                }

                // 킥·스네어 세트와 하이햇 세트가 같은 시각(±15ms)에 함께 나오는 비율
                var ks = Collect("kick_left", "kick_right", "snare_left", "snare_right");
                var hh = Collect("hihat_1", "hihat_2", "hihat_3", "hihat_4");
                if (ks.Count > 0 && hh.Count > 0)
                {
                    int tol = rate * 15 / 1000;
                    int ksWithHh = ks.Count(x => NearAny(hh, x, tol));
                    int hhWithKs = hh.Count(x => NearAny(ks, x, tol));
                    MelonLogger.Msg($"[진단]   동시 출현(±15ms): 킥·스네어 {ks.Count}개 중 {ksWithHh}개에 하이햇 동반 / 하이햇 {hh.Count}개 중 {hhWithKs}개에 킥·스네어 동반");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[진단] 덤프 중 예외: {ex.Message}");
            }
        }

        private static List<int> Collect(params string[] ids)
        {
            var all = new List<int>();
            foreach (var id in ids)
                if (OriginalLaneSamples.TryGetValue(id, out var l)) all.AddRange(l);
            all.Sort();
            return all;
        }

        private static bool NearAny(List<int> sorted, int x, int tol)
        {
            int idx = sorted.BinarySearch(x);
            if (idx >= 0) return true;
            idx = ~idx;
            return (idx < sorted.Count && sorted[idx] - x <= tol) || (idx > 0 && x - sorted[idx - 1] <= tol);
        }

        private static void DumpLane(string name, LaneController lane)
        {
            if (lane == null) { MelonLogger.Msg($"[진단]   {name,-12} (null)"); return; }
            var sb = new StringBuilder();
            sb.Append($"[진단]   {name,-12} type={lane.laneType} active={lane.gameObject.activeInHierarchy}");
            var p = lane.transform.position;
            sb.Append($" pos=({p.x:F2},{p.y:F2})");
            sb.Append($" pad={PadName(lane.thisPad)}/{PadName(lane.thisPad1)}/{PadName(lane.thisPad2)}");
            if (lane.NoteVisuals != null) sb.Append($" noteSprite='{(lane.NoteVisuals.sprite != null ? lane.NoteVisuals.sprite.name : "null")}'");
            var c = lane.color;
            sb.Append($" color=({c.r:F2},{c.g:F2},{c.b:F2})");
            MelonLogger.Msg(sb.ToString());
        }

        private static string PadName(PadController pad)
        {
            if (pad == null) return "-";
            var pp = pad.transform.position;
            return $"{pad.name}@{pp.x:F2}";
        }
    }
}
