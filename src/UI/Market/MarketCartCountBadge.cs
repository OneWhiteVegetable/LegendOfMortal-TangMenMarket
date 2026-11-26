using UnityEngine;
using UnityEngine.UI;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal static class MarketCartCountBadge
    {
        private const float MinimumWidth = 30f;
        private const float Height = 28f;
        private const float HorizontalPadding = 12f;

        internal static void Apply(RectTransform rail)
        {
            if (rail == null)
                return;
            RectTransform badge = rail.Find("CountBack") as RectTransform;
            if (badge == null)
                return;
            Image image = badge.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.raycastTarget = false;
                image.color = UiFormatting.Gold;
                if (badge.GetComponent<MarketPaperEdge>() == null)
                    badge.gameObject.AddComponent<MarketPaperEdge>();
            }
            Transform textTransform = badge.Find("CartCountText");
            Text text = textTransform == null ? null : textTransform.GetComponent<Text>();
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.resizeTextForBestFit = false;
                text.raycastTarget = false;
                text.color = UiFormatting.Ink;
                Stretch(text.rectTransform);
            }
            badge.localScale = Vector3.one;
            badge.localRotation = Quaternion.Euler(0f, 0f, -3f);
            ApplyGeometry(badge);
            MarketCartCountBadgeLayout layout = badge.GetComponent<MarketCartCountBadgeLayout>();
            if (layout == null)
                badge.gameObject.AddComponent<MarketCartCountBadgeLayout>();
        }

        internal static void ApplyGeometry(RectTransform badge)
        {
            if (badge == null)
                return;
            float contentScale = MarketLayoutMetrics.ContentScale(Screen.width, Screen.height);
            float viewportWidth = MarketLayoutMetrics.EffectiveSize(Screen.width, contentScale);
            float viewportHeight = MarketLayoutMetrics.EffectiveSize(Screen.height, contentScale);
            bool compact = viewportWidth <= 1450f || viewportHeight <= 790f;
            Transform textTransform = badge.Find("CartCountText");
            Text text = textTransform == null ? null : textTransform.GetComponent<Text>();
            float preferredWidth = text == null ? 0f : text.preferredWidth / contentScale;
            float width = Mathf.Max(MinimumWidth, Mathf.Ceil(preferredWidth + HorizontalPadding));
            float right = compact ? 13f : 17f;
            float top = compact ? 13f : 20f;
            badge.anchorMin = new Vector2(1f, 1f);
            badge.anchorMax = new Vector2(1f, 1f);
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.sizeDelta = new Vector2(width, Height);
            badge.anchoredPosition = new Vector2(-right - width * 0.5f, -top - Height * 0.5f);
            RectTransform clear = badge.parent.Find("CartClearButton") as RectTransform;
            if (clear != null)
                clear.anchoredPosition = new Vector2(-right - width - 10f, clear.anchoredPosition.y);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    [DefaultExecutionOrder(1000)]
    internal sealed class MarketCartCountBadgeLayout : MonoBehaviour
    {
        private void LateUpdate()
        {
            MarketCartCountBadge.ApplyGeometry(transform as RectTransform);
        }
    }
}
