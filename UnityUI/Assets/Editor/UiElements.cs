using System;
using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static class UiElements
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        public static RectTransform Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static RectTransform TopStretch(RectTransform rect, float top, float height, float left = 0f, float right = 0f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static RectTransform BottomStretch(RectTransform rect, float bottom, float height, float left = 0f, float right = 0f)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
            return rect;
        }

        public static RectTransform LeftStretch(RectTransform rect, float left, float width, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(left + width, -top);
            return rect;
        }

        public static RectTransform RightStretch(RectTransform rect, float right, float width, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-right - width, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static RectTransform Center(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static Image Image(string name, Transform parent, string spriteId, Color color, bool sliced = false, bool raycast = false)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            if (!string.IsNullOrEmpty(spriteId))
            {
                image.sprite = UiAssets.Sprite(spriteId);
            }

            image.color = color;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = raycast;
            return image;
        }

        public static Image Icon(string name, Transform parent, string spriteId, Color color, float x, float y, float size)
        {
            Image image = Image(name, parent, spriteId, color);
            Place(image.rectTransform, x, y, size, size);
            image.preserveAspect = true;
            return image;
        }

        public static Text Text(string name, Transform parent, string value, int size, Color color, TextAnchor alignment = TextAnchor.UpperLeft, bool bold = false)
        {
            return Text(name, parent, value, size, color, alignment, bold ? UiFontRole.Bold : UiFontRole.Body);
        }

        public static Text Text(string name, Transform parent, string value, int size, Color color, TextAnchor alignment, UiFontRole role)
        {
            RectTransform rect = Rect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = UiFonts.ForRole(role, value);
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.text = value;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
            text.lineSpacing = role == UiFontRole.Serif
                ? UiTheme.LineSpacing.Serif
                : role == UiFontRole.Display ? UiTheme.LineSpacing.Display : UiTheme.LineSpacing.Body;
            return text;
        }

        public static Text DisplayText(string name, Transform parent, string value, int size, Color color, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            return Text(name, parent, value, size, color, alignment, UiFontRole.Display);
        }

        public static Text PlaceText(string name, Transform parent, string value, int size, Color color, float x, float y, float width, float height, TextAnchor alignment = TextAnchor.UpperLeft, bool bold = false)
        {
            return PlaceText(name, parent, value, size, color, x, y, width, height, alignment, bold ? UiFontRole.Bold : UiFontRole.Body);
        }

        public static Text PlaceText(string name, Transform parent, string value, int size, Color color, float x, float y, float width, float height, TextAnchor alignment, UiFontRole role)
        {
            Text text = Text(name, parent, value, size, color, alignment, role);
            Place(text.rectTransform, x, y, width, height);
            return text;
        }

        public static Text PlaceDisplayText(string name, Transform parent, string value, int size, Color color, float x, float y, float width, float height, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            return PlaceText(name, parent, value, size, color, x, y, width, height, alignment, UiFontRole.Display);
        }

        public static Button Button(string name, Transform parent, string normal, string hover, string pressed, string disabled, bool sliced = false)
        {
            Image image = Image(name, parent, normal, Color.white, sliced, true);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            if (!string.IsNullOrEmpty(hover) && UiAssets.Has(hover)) state.highlightedSprite = UiAssets.Sprite(hover);
            if (!string.IsNullOrEmpty(pressed) && UiAssets.Has(pressed)) state.pressedSprite = UiAssets.Sprite(pressed);
            if (!string.IsNullOrEmpty(disabled) && UiAssets.Has(disabled)) state.disabledSprite = UiAssets.Sprite(disabled);
            button.spriteState = state;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;
            return button;
        }

        public static Button TextButton(string name, Transform parent, string label, string family, int fontSize, Color textColor)
        {
            Button button = Button(name, parent,
                "button_" + family + "_normal_9s",
                "button_" + family + "_hover_9s",
                "button_" + family + "_pressed_9s",
                "button_" + family + "_disabled_9s", true);
            Text text = Text("Label", button.transform, label, fontSize, textColor, TextAnchor.MiddleCenter, true);
            Stretch(text.rectTransform, 8f, 2f, 8f, 2f);
            return button;
        }

        public static Button IconButton(string name, Transform parent, string icon, string family, Color iconColor, float iconSize = 24f)
        {
            Button button = Button(name, parent,
                "button_" + family + "_normal_9s",
                "button_" + family + "_hover_9s",
                "button_" + family + "_pressed_9s",
                "button_" + family + "_disabled_9s", true);
            Image image = Image("Icon", button.transform, icon, iconColor);
            Center(image.rectTransform, iconSize, iconSize);
            image.preserveAspect = true;
            return button;
        }

        public static Button TradeButton(string name, Transform parent, string family, string icon)
        {
            Button button = Button(name, parent,
                "button_trade_" + family + "_normal",
                "button_trade_" + family + "_hover",
                "button_trade_" + family + "_pressed",
                "button_trade_" + family + "_disabled");
            Image image = Image("Icon", button.transform, icon, UiTheme.PaperLight);
            Center(image.rectTransform, 25f, 25f);
            image.preserveAspect = true;
            return button;
        }

        public static RectTransform Stepper(string name, Transform parent, string bindingPrefix)
        {
            RectTransform root = Rect(name, parent);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = UiTheme.Alpha(UiTheme.PaperLight, 0.94f);
            background.raycastTarget = true;
            AddOutline(root.gameObject, UiTheme.Alpha(UiTheme.Ink, 0.48f), new Vector2(1f, -1f));

            Button decrement = TradeButton("DecrementButton", root, "green", "icon_minus");
            decrement.GetComponent<Image>().sprite = null;
            decrement.GetComponent<Image>().color = Color.clear;
            decrement.transform.Find("Icon").GetComponent<Image>().color = UiTheme.Ink;
            decrement.transition = Selectable.Transition.ColorTint;
            Place(decrement.GetComponent<RectTransform>(), 0f, 0f, 34f, 34f);
            Text quantity = PlaceText("QuantityText", root, "1", 19, UiTheme.Ink, 34f, 0f, 44f, 34f, TextAnchor.MiddleCenter, true);
            Button increment = TradeButton("IncrementButton", root, "red", "icon_plus");
            increment.GetComponent<Image>().sprite = null;
            increment.GetComponent<Image>().color = Color.clear;
            increment.transform.Find("Icon").GetComponent<Image>().color = UiTheme.Ink;
            increment.transition = Selectable.Transition.ColorTint;
            Place(increment.GetComponent<RectTransform>(), 78f, 0f, 34f, 34f);
            UiBuilder.Bind(bindingPrefix + "DecrementButton", decrement.GetComponent<RectTransform>(), "Button");
            UiBuilder.Bind(bindingPrefix + "Text", quantity.rectTransform, "Text");
            UiBuilder.Bind(bindingPrefix + "IncrementButton", increment.GetComponent<RectTransform>(), "Button");
            return root;
        }

        public static RectTransform Host(string name, Transform parent, string bindingKey)
        {
            RectTransform host = Rect(name, parent);
            UiBuilder.Bind(bindingKey, host, "RectTransform");
            return host;
        }

        public static ScrollRect VerticalScroll(string name, Transform parent, out RectTransform content, float spacing = 8f, float padding = 8f)
        {
            RectTransform viewport = Rect(name, parent);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            scroll.viewport = viewport;

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(Mathf.RoundToInt(padding), Mathf.RoundToInt(padding), Mathf.RoundToInt(padding), Mathf.RoundToInt(padding));
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return scroll;
        }

        public static LayoutElement Layout(Component component, float preferredWidth, float preferredHeight, float flexibleWidth = 0f, float flexibleHeight = 0f)
        {
            LayoutElement layout = component.gameObject.GetComponent<LayoutElement>();
            if (layout == null) layout = component.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f) layout.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f) layout.preferredHeight = preferredHeight;
            layout.flexibleWidth = flexibleWidth;
            layout.flexibleHeight = flexibleHeight;
            return layout;
        }

        public static void AddOutline(GameObject gameObject, Color color, Vector2 distance)
        {
            Outline outline = gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        public static void SetColorTint(Button button, Color normal, Color highlighted, Color pressed)
        {
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = pressed;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        public static void AddHorizontalLayout(RectTransform rect, int left, int top, int right, int bottom, float spacing, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        public static void AddVerticalLayout(RectTransform rect, int left, int top, int right, int bottom, float spacing, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }
    }
}
