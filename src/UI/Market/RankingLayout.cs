using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Market
{
    internal static class RankingLayout
    {
        internal static readonly Color Paper = new Color32(234, 227, 210, 255);
        internal static readonly Color PaperLight = new Color32(245, 240, 228, 255);
        internal static readonly Color Gold = new Color32(180, 147, 88, 255);
        internal static readonly Color GoldLight = new Color32(216, 188, 123, 255);

        internal static RectTransform Rect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        internal static Text Text(
            string name,
            Transform parent,
            string value,
            int size,
            Color color,
            Font font,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            RectTransform rect = Rect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.text = value ?? string.Empty;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
            text.lineSpacing = 1f;
            return text;
        }

        internal static Image Image(
            string name,
            Transform parent,
            Sprite sprite,
            Color fallbackColor,
            bool sliced = false)
        {
            RectTransform rect = Rect(name, parent);
            UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = sprite == null ? fallbackColor : Color.white;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        internal static LayoutElement Layout(
            Component component,
            float preferredWidth,
            float preferredHeight,
            float flexibleWidth = 0f,
            float flexibleHeight = 0f)
        {
            LayoutElement element = component.GetComponent<LayoutElement>();
            if (element == null)
                element = component.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f)
                element.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f)
                element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = flexibleHeight;
            return element;
        }

        internal static HorizontalLayoutGroup Columns(RectTransform root, bool compact)
        {
            HorizontalLayoutGroup layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            ConfigureColumns(layout, compact);
            return layout;
        }

        internal static void ConfigureColumns(HorizontalLayoutGroup layout, bool compact)
        {
            layout.padding = new RectOffset(14, 14, 0, 0);
            layout.spacing = compact ? 8f : 12f;
        }

        internal static LayoutElement Column(RectTransform rect, int index, bool compact)
        {
            LayoutElement element = Layout(rect, -1f, -1f);
            ConfigureColumn(element, index, compact);
            return element;
        }

        internal static void ConfigureColumn(LayoutElement element, int index, bool compact)
        {
            element.minWidth = -1f;
            element.preferredWidth = -1f;
            switch (index)
            {
                case 0:
                    element.preferredWidth = compact ? 56f : 68f;
                    element.flexibleWidth = 0f;
                    break;
                case 1:
                    element.minWidth = compact ? 120f : 150f;
                    element.flexibleWidth = 1.35f;
                    break;
                case 2:
                    element.minWidth = compact ? 78f : 100f;
                    element.flexibleWidth = 0.8f;
                    break;
                case 3:
                    element.minWidth = compact ? 96f : 120f;
                    element.flexibleWidth = 0.8f;
                    break;
                case 4:
                    element.minWidth = compact ? 100f : 150f;
                    element.flexibleWidth = 1f;
                    break;
                default:
                    element.preferredWidth = compact ? 26f : 34f;
                    element.flexibleWidth = 0f;
                    break;
            }
        }

        internal static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        internal static void Center(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static void IgnoreLayout(Component component)
        {
            LayoutElement element = Layout(component, -1f, -1f);
            element.ignoreLayout = true;
        }

        internal static Color Alpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
