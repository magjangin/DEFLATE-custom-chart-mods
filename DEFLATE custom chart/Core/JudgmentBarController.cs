using System;
using System.Collections.Generic;
using MelonLoader;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.UI;
using DEFLATE_custom_chart.Core;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>
    /// 실시간 5구간 판정바 (Judgment Bar / Early-Late Indicator) 렌더러
    /// UGUI Canvas 내부 상위에 자동 배치하여 화면 렌더링 보장
    /// </summary>
    [RegisterTypeInIl2Cpp]
    public class JudgmentBarController : MonoBehaviour
    {
        public JudgmentBarController(IntPtr ptr) : base(ptr) { }

        public static JudgmentBarController Instance { get; private set; }

        private static bool _isTypeRegistered = false;

        private GameObject _barContainer;
        private RectTransform _barRectTransform;

        private const float BarLength = 220.0f; // 판정바 전체 길이
        private const float BarWidth = 16.0f;   // 판정바 두께

        public static void RegisterType()
        {
            if (!_isTypeRegistered)
            {
                try
                {
                    ClassInjector.RegisterTypeInIl2Cpp<JudgmentBarController>();
                    _isTypeRegistered = true;
                    MelonLogger.Msg("[JudgmentBarController] Il2Cpp Type Registration Success!");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[JudgmentBarController] Il2Cpp Type Registration Failed: {ex.Message}");
                }
            }
        }

        public static void EnsureInstance(Transform fallbackParent)
        {
            if (!ModConfig.Instance.EnableJudgmentBar) return;

            RegisterType();

            // 씬 내부 UGUI Canvas 탐색
            Canvas targetCanvas = null;
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            if (canvases != null && canvases.Length > 0)
            {
                foreach (var c in canvases)
                {
                    if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                    {
                        targetCanvas = c;
                        break;
                    }
                }
            }

            Transform parentTransform = targetCanvas != null ? targetCanvas.transform : fallbackParent;

            if (Instance == null || Instance.gameObject == null)
            {
                var holder = new GameObject("HwaCustomJudgmentBarHolder");
                holder.transform.SetParent(parentTransform, false);

                var holderRect = holder.AddComponent<RectTransform>();
                holderRect.anchorMin = new Vector2(0.5f, 0.5f);
                holderRect.anchorMax = new Vector2(0.5f, 0.5f);
                holderRect.anchoredPosition = Vector2.zero;

                Instance = holder.AddComponent<JudgmentBarController>();
                Instance.InitUI();
            }
        }

        [HideFromIl2Cpp]
        private void InitUI()
        {
            _barContainer = new GameObject("JudgmentBarContainer");
            _barContainer.transform.SetParent(this.transform, false);

            _barRectTransform = _barContainer.AddComponent<RectTransform>();
            bool isVertical = ModConfig.Instance.JudgmentBarVertical;
            bool isCapsule = ModConfig.Instance.JudgmentBarCapsule;
            string side = ModConfig.Instance.JudgmentBarSide ?? "Center";

            if (isVertical)
            {
                _barRectTransform.sizeDelta = new Vector2(BarWidth, BarLength);
            }
            else
            {
                _barRectTransform.sizeDelta = new Vector2(BarLength, BarWidth);
            }

            SetBarPosition(side, isVertical);
            CreateBoxSegments(isVertical, isCapsule);
            CreateCenterLine(isVertical);

            MelonLogger.Msg($"[JudgmentBar] ★ 실시간 5구간 판정바 UI 렌더링 활성화 ★ (Vertical={isVertical}, Side='{side}', Parent='{this.transform.parent?.name}')");
        }

        [HideFromIl2Cpp]
        private void SetBarPosition(string side, bool isVertical)
        {
            _barRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _barRectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            float posX = 0f;
            float posY = 0f;

            string s = side.Trim().ToLowerInvariant();
            if (s == "left")
            {
                posX = -450f;
            }
            else if (s == "right")
            {
                posX = 450f;
            }
            else
            {
                if (isVertical)
                {
                    posX = -500f;
                }
                else
                {
                    posY = -250f;
                }
            }

            _barRectTransform.anchoredPosition = new Vector2(posX, posY);
        }

        [HideFromIl2Cpp]
        private void CreateBoxSegments(bool isVertical, bool isCapsule)
        {
            var segments = new[]
            {
                new { Min = -1.0f, Max = -0.50f, Color = new Color(0.9f, 0.2f, 0.2f, 0.85f) }, // Far Late (Red)
                new { Min = -0.50f, Max = -0.18f, Color = new Color(0.95f, 0.75f, 0.2f, 0.85f) },// Late Good (Yellow)
                new { Min = -0.18f, Max = 0.18f, Color = new Color(0.2f, 0.9f, 0.95f, 0.95f) },  // PERFECT (Cyan)
                new { Min = 0.18f, Max = 0.50f, Color = new Color(0.2f, 0.5f, 0.95f, 0.85f) },  // Early Good (Blue)
                new { Min = 0.50f, Max = 1.0f, Color = new Color(0.7f, 0.2f, 0.9f, 0.85f) }    // Far Early (Purple)
            };

            foreach (var seg in segments)
            {
                var segObj = new GameObject("Segment");
                segObj.transform.SetParent(_barContainer.transform, false);

                var img = segObj.AddComponent<Image>();
                img.color = seg.Color;

                var rect = segObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);

                float center = (seg.Min + seg.Max) * 0.5f;
                float span = (seg.Max - seg.Min);

                if (isVertical)
                {
                    rect.sizeDelta = new Vector2(BarWidth, span * (BarLength * 0.5f));
                    rect.anchoredPosition = new Vector2(0, center * (BarLength * 0.5f));
                }
                else
                {
                    rect.sizeDelta = new Vector2(span * (BarLength * 0.5f), BarWidth);
                    rect.anchoredPosition = new Vector2(center * (BarLength * 0.5f), 0);
                }
            }
        }

        [HideFromIl2Cpp]
        private void CreateCenterLine(bool isVertical)
        {
            var lineObj = new GameObject("CenterLine");
            lineObj.transform.SetParent(_barContainer.transform, false);

            var img = lineObj.AddComponent<Image>();
            img.color = Color.white;

            var rect = lineObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);

            if (isVertical)
            {
                rect.sizeDelta = new Vector2(BarWidth + 8.0f, 3.0f);
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                rect.sizeDelta = new Vector2(3.0f, BarWidth + 8.0f);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>
        /// 노트 타격 시 오차(timeDiff ms)를 전달받아 히트 마커 인디케이터 틱을 표시합니다.
        /// </summary>
        public void OnNoteHit(float timeDiffMs, float maxHitWindowMs)
        {
            if (!ModConfig.Instance.EnableJudgmentBar || _barContainer == null) return;

            if (maxHitWindowMs <= 0) maxHitWindowMs = 90.0f;

            float norm = Mathf.Clamp(timeDiffMs / maxHitWindowMs, -1.0f, 1.0f);
            bool isVertical = ModConfig.Instance.JudgmentBarVertical;

            var markerObj = new GameObject("HitMarker");
            markerObj.transform.SetParent(_barContainer.transform, false);

            var img = markerObj.AddComponent<Image>();
            img.color = Math.Abs(norm) <= 0.18f ? new Color(1.0f, 1.0f, 1.0f, 1.0f) : new Color(1.0f, 0.95f, 0.3f, 1.0f);

            var rect = markerObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);

            float offsetPos = norm * (BarLength * 0.5f);
            if (isVertical)
            {
                rect.sizeDelta = new Vector2(BarWidth + 6.0f, 4.0f);
                rect.anchoredPosition = new Vector2(0, offsetPos);
            }
            else
            {
                rect.sizeDelta = new Vector2(4.0f, BarWidth + 6.0f);
                rect.anchoredPosition = new Vector2(offsetPos, 0);
            }

            MelonCoroutines.Start(FadeOutMarker(img, markerObj));
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator FadeOutMarker(Image img, GameObject obj)
        {
            float duration = 0.5f;
            float elapsed = 0.0f;
            Color startColor = img.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startColor.a, 0.0f, elapsed / duration);
                if (img != null)
                {
                    img.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                }
                yield return null;
            }

            if (obj != null)
            {
                Destroy(obj);
            }
        }
    }
}
