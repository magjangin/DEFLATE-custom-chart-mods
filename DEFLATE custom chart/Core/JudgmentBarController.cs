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
    /// 어두운 고대비 배경 패널 & Early/Late 마커 색상 명확화 적용판
    /// </summary>
    [RegisterTypeInIl2Cpp]
    public class JudgmentBarController : MonoBehaviour
    {
        public JudgmentBarController(IntPtr ptr) : base(ptr) { }

        public static JudgmentBarController Instance { get; private set; }

        private static bool _isTypeRegistered = false;

        private GameObject _barContainer;
        private RectTransform _barRectTransform;

        private const float BarLength = 380.0f; // 판정바 전체 길이
        private const float BarWidth = 26.0f;   // 판정바 두께

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

            // 1. 어두운 블랙 패널 배경 생성 (가시성 대폭 향상)
            CreateDarkBackgroundPanel(isVertical);

            // 2. 5구간 색상 세그먼트 생성
            CreateBoxSegments(isVertical, isCapsule);

            // 3. 중앙 0ms 기준선 생성
            CreateCenterLine(isVertical);

            MelonLogger.Msg($"[JudgmentBar] ★ 어두운 배경 패널 포함 5구간 판정바 UI 렌더링 활성화 ★ (Length={BarLength}, Width={BarWidth}, Side='{side}')");
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
                posX = -720f;
            }
            else if (s == "right")
            {
                posX = 720f;
            }
            else
            {
                if (isVertical)
                {
                    posX = -710f;
                }
                else
                {
                    posY = -340f;
                }
            }

            _barRectTransform.anchoredPosition = new Vector2(posX, posY);
        }

        [HideFromIl2Cpp]
        private void CreateDarkBackgroundPanel(bool isVertical)
        {
            var bgObj = new GameObject("DarkBackgroundPanel");
            bgObj.transform.SetParent(_barContainer.transform, false);

            var img = bgObj.AddComponent<Image>();
            img.color = new Color(0.03f, 0.03f, 0.04f, 0.92f); // 묵직하고 선명한 어두운 블랙 패널

            var rect = bgObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            float pad = 16.0f; // 판정바 주변 여유 외곽 테두리
            if (isVertical)
            {
                rect.sizeDelta = new Vector2(BarWidth + pad, BarLength + pad);
            }
            else
            {
                rect.sizeDelta = new Vector2(BarLength + pad, BarWidth + pad);
            }
        }

        [HideFromIl2Cpp]
        private void CreateBoxSegments(bool isVertical, bool isCapsule)
        {
            var segments = new[]
            {
                new { Min = -1.0f, Max = -0.50f, Color = new Color(0.85f, 0.15f, 0.15f, 0.85f) }, // Far Late (Red)
                new { Min = -0.50f, Max = -0.18f, Color = new Color(0.95f, 0.65f, 0.15f, 0.85f) },// Late Good (Yellow)
                new { Min = -0.18f, Max = 0.18f, Color = new Color(0.1f, 0.85f, 0.95f, 0.95f) },  // PERFECT (Cyan)
                new { Min = 0.18f, Max = 0.50f, Color = new Color(0.15f, 0.45f, 0.95f, 0.85f) }, // Early Good (Blue)
                new { Min = 0.50f, Max = 1.0f, Color = new Color(0.65f, 0.15f, 0.85f, 0.85f) }   // Far Early (Purple)
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
            // 중앙선(0ms)은 뚜렷한 다크 가이드라인으로 배치
            img.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            var rect = lineObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);

            if (isVertical)
            {
                rect.sizeDelta = new Vector2(BarWidth + 10.0f, 4.0f);
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                rect.sizeDelta = new Vector2(4.0f, BarWidth + 10.0f);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>
        /// 노트 타격 시 오차(timeDiff ms)를 전달받아 EARLY(파란색) / LATE(노란색) 마커 틱을 명확히 구분하여 표시합니다.
        /// </summary>
        public void OnNoteHit(float timeDiffMs, float maxHitWindowMs)
        {
            if (!ModConfig.Instance.EnableJudgmentBar || _barContainer == null) return;

            if (maxHitWindowMs <= 0) maxHitWindowMs = 90.0f;

            float norm = Mathf.Clamp(timeDiffMs / maxHitWindowMs, -1.0f, 1.0f);
            bool isVertical = ModConfig.Instance.JudgmentBarVertical;

            float offsetPos = norm * (BarLength * 0.5f);

            var markerObj = new GameObject("HitMarker");
            markerObj.transform.SetParent(_barContainer.transform, false);

            var img = markerObj.AddComponent<Image>();

            // 마커 색상 구분 명확화:
            // 1. PERFECT (|norm| <= 0.18): 눈부신 순백색
            // 2. EARLY (norm > 0.18): 선명한 네온 푸른색 / 시안
            // 3. LATE (norm < -0.18): 선명한 형광 옐로우 / 오렌지
            if (Math.Abs(norm) <= 0.18f)
            {
                img.color = new Color(1.0f, 1.0f, 1.0f, 1.0f); // PERFECT: White
            }
            else if (norm > 0)
            {
                img.color = new Color(0.0f, 0.95f, 1.0f, 1.0f); // EARLY: Neon Blue/Cyan
            }
            else
            {
                img.color = new Color(1.0f, 0.85f, 0.0f, 1.0f); // LATE: Neon Yellow
            }

            var rect = markerObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);

            if (isVertical)
            {
                rect.sizeDelta = new Vector2(BarWidth + 18.0f, 6.0f);
                rect.anchoredPosition = new Vector2(0, offsetPos);
            }
            else
            {
                rect.sizeDelta = new Vector2(6.0f, BarWidth + 18.0f);
                rect.anchoredPosition = new Vector2(offsetPos, 0);
            }

            MelonCoroutines.Start(FadeOutMarker(img, markerObj));
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator FadeOutMarker(Image img, GameObject obj)
        {
            float duration = 0.65f;
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
