using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    public sealed class UiFactory
    {
        private readonly SpriteAssetLibrary _sprites;
        private Font _font;

        public UiFactory(SpriteAssetLibrary sprites, Font gameFont)
        {
            _sprites = sprites;
            _font = gameFont;
        }

        public SpriteAssetLibrary Sprites
        {
            get { return _sprites; }
        }

        public Font GameFont
        {
            get { return _font; }
            set { _font = value; }
        }

        public Canvas CreateCanvas(
            string name = "XingShangMod.Canvas",
            int sortingOrder = 30000,
            Vector2? referenceResolution = null,
            bool persistAcrossScenes = true)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            SetUiLayer(root);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution ?? new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            if (persistAcrossScenes)
            {
                UnityEngine.Object.DontDestroyOnLoad(root);
            }
            return canvas;
        }

        public Image CreatePanel(
            Transform parent,
            string name,
            string spriteId = null,
            Vector2? size = null,
            Color? color = null)
        {
            Sprite sprite = _sprites != null && !string.IsNullOrWhiteSpace(spriteId)
                ? _sprites.GetSprite(spriteId)
                : null;
            Image image = CreateImage(parent, name, sprite, size, false, true);
            image.color = color ?? (sprite == null
                ? new Color(0.10f, 0.09f, 0.075f, 0.96f)
                : Color.white);
            return image;
        }

        public Image CreateImage(
            Transform parent,
            string name,
            string spriteId,
            Vector2? size = null,
            bool preserveAspect = false,
            bool raycastTarget = false)
        {
            Sprite sprite = _sprites != null ? _sprites.GetSprite(spriteId) : null;
            return CreateImage(parent, name, sprite, size, preserveAspect, raycastTarget);
        }

        public Image CreateImage(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2? size = null,
            bool preserveAspect = false,
            bool raycastTarget = false)
        {
            RectTransform rect = CreateRect(name, parent);
            if (size.HasValue)
            {
                rect.sizeDelta = size.Value;
            }
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = raycastTarget;
            ApplySpriteShape(image);
            return image;
        }

        public Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize = 24,
            Color? color = null,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            Vector2? size = null)
        {
            RectTransform rect = CreateRect(name, parent);
            if (size.HasValue)
            {
                rect.sizeDelta = size.Value;
            }
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = ResolveFont();
            text.text = content ?? string.Empty;
            text.fontSize = fontSize;
            text.color = color ?? new Color(0.16f, 0.13f, 0.10f, 1f);
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public Button CreateButton(
            Transform parent,
            string name,
            string label,
            UnityAction onClick,
            int fontSize = 24,
            Vector2? size = null,
            string normalSpriteId = null,
            string highlightedSpriteId = null,
            string pressedSpriteId = null,
            string disabledSpriteId = null)
        {
            Image background = CreatePanel(parent, name, normalSpriteId, size);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.88f, 0.74f, 1f);
            colors.pressedColor = new Color(0.78f, 0.72f, 0.62f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.55f, 0.53f, 0.49f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            SetButtonSprites(button, normalSpriteId, highlightedSpriteId, pressedSpriteId, disabledSpriteId);

            Text text = CreateText(
                background.transform,
                "Label",
                label,
                fontSize,
                new Color(0.18f, 0.13f, 0.09f, 1f),
                TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, new Vector2(12f, 6f), new Vector2(-12f, -6f));
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
            return button;
        }

        public void SetButtonSprites(
            Button button,
            string normalSpriteId,
            string highlightedSpriteId = null,
            string pressedSpriteId = null,
            string disabledSpriteId = null)
        {
            if (button == null)
            {
                throw new ArgumentNullException("button");
            }
            Image background = button.targetGraphic as Image;
            if (background == null)
            {
                background = button.GetComponent<Image>();
            }
            if (background != null && !string.IsNullOrWhiteSpace(normalSpriteId) && _sprites != null)
            {
                background.sprite = _sprites.GetSprite(normalSpriteId);
                background.color = Color.white;
                ApplySpriteShape(background);
            }

            bool hasState = !string.IsNullOrWhiteSpace(highlightedSpriteId) ||
                            !string.IsNullOrWhiteSpace(pressedSpriteId) ||
                            !string.IsNullOrWhiteSpace(disabledSpriteId);
            if (!hasState || _sprites == null)
            {
                return;
            }
            SpriteState state = button.spriteState;
            if (!string.IsNullOrWhiteSpace(highlightedSpriteId))
            {
                state.highlightedSprite = _sprites.GetSprite(highlightedSpriteId);
            }
            if (!string.IsNullOrWhiteSpace(pressedSpriteId))
            {
                state.pressedSprite = _sprites.GetSprite(pressedSpriteId);
            }
            if (!string.IsNullOrWhiteSpace(disabledSpriteId))
            {
                state.disabledSprite = _sprites.GetSprite(disabledSpriteId);
            }
            button.spriteState = state;
            button.transition = Selectable.Transition.SpriteSwap;
        }

        public ScrollRect CreateScrollRect(
            Transform parent,
            string name,
            Vector2 size,
            out RectTransform content,
            bool horizontal = false,
            bool vertical = true)
        {
            Image root = CreatePanel(parent, name, null, size, new Color(0f, 0f, 0f, 0f));
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();

            Image viewportImage = CreatePanel(
                root.transform,
                "Viewport",
                null,
                null,
                new Color(0f, 0f, 0f, 0f));
            Stretch(viewportImage.rectTransform);
            Mask mask = viewportImage.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            content = CreateRect("Content", viewportImage.transform);
            if (vertical && !horizontal)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
            }
            else if (horizontal && !vertical)
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
            }
            else
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 1f);
            }
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            scroll.viewport = viewportImage.rectTransform;
            scroll.content = content;
            scroll.horizontal = horizontal;
            scroll.vertical = vertical;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 24f;
            return scroll;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            SetUiLayer(gameObject);
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        public static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            if (rect == null)
            {
                throw new ArgumentNullException("rect");
            }
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        public static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
        }

        public static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        public static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                throw new ArgumentNullException("rect");
            }
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private Font ResolveFont()
        {
            if (_font != null)
            {
                return _font;
            }
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }

        private static void ApplySpriteShape(Image image)
        {
            if (image.sprite != null && image.sprite.border.sqrMagnitude > 0f)
            {
                image.type = Image.Type.Sliced;
                image.fillCenter = true;
                image.useSpriteMesh = false;
            }
            else
            {
                image.type = Image.Type.Simple;
            }
        }

        private static void SetUiLayer(GameObject gameObject)
        {
            int layer = LayerMask.NameToLayer("UI");
            if (layer >= 0)
            {
                gameObject.layer = layer;
            }
        }
    }
}
