using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using DEFLATE_custom_chart.Core;

namespace DEFLATE_custom_chart.Core
{
    /// <summary>
    /// 실시간 5구간 판정바 (Judgment Bar / Early-Late Indicator) 렌더러
    /// </summary>
    public class JudgmentBarController : MonoBehaviour
    {
        public static JudgmentBarController Instance { get; private set; }

        private GameObject _barContainer;
        private RectTransform _barRectTransform;
        private List<Image> _hitMarkers = new List<Image>();

        private const float BarLength = 200.0f; // 판정바 전체 길이
        private const float BarWidth = 14.0f;   // 판정바 두께

        public static void EnsureInstance(Transform parentTransform)
        {
            if (!ModConfig.Instance.EnableJudgmentBar) return;

            if (Instance == null || Instance.gameObject == null)
            {
                var holder = new GameObject("HwaCustomJudgmentBarHolder");
                holder.transform.SetParent(parentTransform, false);
                Instance = holder.AddComponent<JudgmentBarController>();
                Instance.InitUI(parentTransform);
            }
        }

        private void InitUI(Transform parentTransform)
        {
            _barContainer = new GameObject("JudgmentBarContainer");
            _barContainer.transform.SetParent(this.transform, false);

            _barRectTransform = _barContainer.AddComponent<RectTransform>();
            bool isVertical = ModConfig.Instance.JudgmentBarVertical;
            bool isCapsule = ModConfig.Instance.JudgmentBarCapsule;
            string side = ModConfig.Instance.JudgmentBarSide ?? "Center";

            // 세로/가로 판정바 피벗 및 크기 설정
            if (isVertical)
            {
                _barRectTransform.sizeDelta = new Vector2(BarWidth, BarLength);
            }
            else
            {
                _barRectTransform.sizeDelta = new Vector2(BarLength, BarWidth);
            }

            // 위치 (Side) 설정
            SetBarPosition(side, isVertical);

            // 5구간 박스 배경 생성 (Far Late, Late, PERFECT, Early, Far Early)
            CreateBoxSegments(isVertical, isCapsule);

            // 중앙 0ms 기준선 생성
            CreateCenterLine(isVertical);

            MelonLogger.Msg($"[JudgmentBar] 실시간 판정바 UI 로드 완료 (Vertical={isVertical}, Side='{side}', Capsule={isCapsule})");
        }

        private void SetBarPosition(string side, bool isVertical)
        {
            _barRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _barRectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            float posX = 0f;
            float posY = 0f;

            string s = side.Trim().ToLowerInvariant();
            if (s == "left")
            {
                posX = -500f;
            }
            else if (s == "right")
            {
                posX = 500f;
            }
            else
            {
                // Center
                if (isVertical)
                {
                    posX = -580f; // 세로 기본: 화면 왼쪽 레인 옆
                }
                else
                {
                    posY = -280f; // 가로 기본: 화면 중앙 아래
                }
            }

            _barRectTransform.anchoredPosition = new Vector2(posX, posY);
        }

        private void CreateBoxSegments(bool isVertical, bool isCapsule)
        {
            // 5개 박스 구간 비율 (-1.0 ~ +1.0)
            // Far Late (-1.0 ~ -0.5), Late (-0.5 ~ -0.18), PERFECT (-0.18 ~ +0.18), Early (+0.18 ~ +0.5), Far Early (+0.5 ~ +1.0)
            var segments = new[]
            {
                new { Min = -1.0f, Max = -0.50f, Color = new Color(0.9f, 0.2f, 0.2f, 0.75f) }, // Far Late (Red)
                new { Min = -0.50f, Max = -0.18f, Color = new Color(0.95f, 0.75f, 0.2f, 0.75f) },// Late Good (Yellow)
                new { Min = -0.18f, Max = 0.18f, Color = new Color(0.2f, 0.9f, 0.95f, 0.85f) },  // PERFECT (Cyan)
                new { Min = 0.18f, Max = 0.50f, Color = new Color(0.2f, 0.5f, 0.95f, 0.75f) },  // Early Good (Blue)
                new { Min = 0.50f, Max = 1.0f, Color = new Color(0.7f, 0.2f, 0.9f, 0.75f) }    // Far Early (Purple)
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
                rect.sizeDelta = new Vector2(BarWidth + 6.0f, 2.5f);
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                rect.sizeDelta = new Vector2(2.5f, BarWidth + 6.0f);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>
        /// 노트 타격 시 오차(timeDiff ms)를 전달받아 히트 마커 인디케이터 틱을 표시합니다.
        /// </summary>
        public void OnNoteHit(float timeDiffMs, float maxHitWindowMs)
        {
            if (!ModConfig.Instance.EnableJudgmentBar || _barContainer == null) return;

            if (maxHitWindowMs <= 0) maxHitWindowMs = 90.0f; // 기본 판정 윈도우 ms

            float norm = Mathf.Clamp(timeDiffMs / maxHitWindowMs, -1.0f, 1.0f);
            bool isVertical = ModConfig.Instance.JudgmentBarVertical;

            var markerObj = new GameObject("HitMarker");
            markerObj.transform.SetParent(_barContainer.transform, false);

            var img = markerObj.AddComponent<Image>();
            img.color = Math.Abs(norm) <= 0.18f ? new Color(1.0f, 1.0f, 1.0f, 0.95f) : new Color(1.0f, 0.9f, 0.3f, 0.95f);

            var rect = markerObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);

            float offsetPos = norm * (BarLength * 0.5f);
            if (isVertical)
            {
                rect.sizeDelta = new Vector2(BarWidth + 4.0f, 3.0f);
                rect.anchoredPosition = new Vector2(0, offsetPos);
            }
            else
            {
                rect.sizeDelta = new Vector2(3.0f, BarWidth + 4.0f);
                rect.anchoredPosition = new Vector2(offsetPos, 0);
            }

            // 서서히 사라지는 페이드 아웃 애니메이션
            MelonCoroutines.Start(FadeOutMarker(img, markerObj));
        }

        private System.Collections.IEnumerator FadeOutMarker(Image img, GameObject obj)
        {
            float duration = 0.45f;
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
