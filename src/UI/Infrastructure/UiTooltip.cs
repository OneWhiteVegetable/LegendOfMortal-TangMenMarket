using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    public sealed class UiTooltip : MonoBehaviour
    {
        private Text _text;
        private RectTransform _coordinateSpace;
        private Canvas _canvas;
        private UiTooltipTrigger _owner;
        private RectTransform _anchor;
        private Vector2 _screenPoint;
        private bool _followPointer;

        public RectTransform Root { get; private set; }
        public float MaxWidth { get; set; }
        public Vector2 ScreenOffset { get; set; }

        public static UiTooltip Create(UiFactory factory, Transform parent)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }
            if (parent == null)
            {
                throw new ArgumentNullException("parent");
            }

            Image panel = factory.CreatePanel(
                parent,
                "XingShangMod.Tooltip",
                "panel_paper_tooltip_9s",
                new Vector2(320f, 80f));
            panel.raycastTarget = false;
            UiTooltip tooltip = panel.gameObject.AddComponent<UiTooltip>();
            tooltip.Root = panel.rectTransform;
            tooltip.Root.pivot = new Vector2(0f, 1f);
            tooltip.MaxWidth = 420f;
            tooltip.ScreenOffset = new Vector2(18f, -18f);
            tooltip._coordinateSpace = parent as RectTransform;
            if (tooltip._coordinateSpace == null)
            {
                tooltip._coordinateSpace = parent.GetComponentInParent<RectTransform>();
            }
            if (tooltip._coordinateSpace == null)
            {
                throw new ArgumentException("The tooltip parent must be inside a uGUI canvas.", "parent");
            }
            tooltip._canvas = parent.GetComponentInParent<Canvas>();

            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            tooltip._text = factory.CreateText(
                panel.transform,
                "Text",
                string.Empty,
                21,
                new Color(0.17f, 0.13f, 0.09f, 1f),
                TextAnchor.UpperLeft);
            tooltip._text.verticalOverflow = VerticalWrapMode.Overflow;
            panel.gameObject.SetActive(false);
            return tooltip;
        }

        public UiTooltipTrigger Attach(GameObject target, string text)
        {
            UiTooltipTrigger trigger = GetOrAddTrigger(target);
            trigger.Configure(this, text, null);
            return trigger;
        }

        public UiTooltipTrigger Attach(GameObject target, Func<string> textProvider)
        {
            UiTooltipTrigger trigger = GetOrAddTrigger(target);
            trigger.Configure(this, null, textProvider);
            return trigger;
        }

        public void ShowAt(string text, Vector2 screenPoint)
        {
            _owner = null;
            _anchor = null;
            _followPointer = false;
            _screenPoint = screenPoint;
            ShowInternal(text);
        }

        public void Hide()
        {
            _owner = null;
            _anchor = null;
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
            }
        }

        internal void Show(
            UiTooltipTrigger owner,
            RectTransform anchor,
            string text,
            Vector2 screenPoint,
            bool followPointer)
        {
            _owner = owner;
            _anchor = anchor;
            _screenPoint = screenPoint;
            _followPointer = followPointer;
            ShowInternal(text);
        }

        internal void Hide(UiTooltipTrigger owner)
        {
            if (_owner == owner)
            {
                Hide();
            }
        }

        internal Vector2 GetAnchorScreenPoint(RectTransform anchor)
        {
            if (anchor == null)
            {
                return _screenPoint;
            }
            Vector3 worldPoint = anchor.TransformPoint(new Vector3(anchor.rect.xMax, anchor.rect.yMax, 0f));
            Camera camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;
            return RectTransformUtility.WorldToScreenPoint(camera, worldPoint);
        }

        private void Update()
        {
            if (Root == null || !Root.gameObject.activeSelf)
            {
                return;
            }
            if (_owner != null && _anchor == null)
            {
                Hide();
                return;
            }
            if (_followPointer)
            {
                _screenPoint = Input.mousePosition;
            }
            else if (_anchor != null)
            {
                _screenPoint = GetAnchorScreenPoint(_anchor);
            }
            PositionAt(_screenPoint);
        }

        private void ShowInternal(string text)
        {
            if (Root == null)
            {
                return;
            }
            _text.text = text ?? string.Empty;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            ResizeToText();
            PositionAt(_screenPoint);
        }

        private void ResizeToText()
        {
            float maxWidth = Mathf.Max(120f, MaxWidth);
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            float width = Mathf.Clamp(_text.preferredWidth + 32f, 120f, maxWidth);
            RectTransform textRect = _text.rectTransform;
            UiFactory.SetTopLeft(textRect, new Vector2(16f, -12f), new Vector2(width - 32f, 24f));
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            Root.sizeDelta = new Vector2(width, 80f);
            Canvas.ForceUpdateCanvases();
            float height = Mathf.Max(56f, _text.preferredHeight + 24f);
            Root.sizeDelta = new Vector2(width, height);
            textRect.sizeDelta = new Vector2(width - 32f, height - 24f);
        }

        private void PositionAt(Vector2 screenPoint)
        {
            if (_coordinateSpace == null)
            {
                return;
            }
            Camera camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _coordinateSpace, screenPoint, camera, out localPoint))
            {
                return;
            }

            Vector2 size = Root.rect.size;
            Rect bounds = _coordinateSpace.rect;
            float x = localPoint.x + ScreenOffset.x;
            float y = localPoint.y + ScreenOffset.y;
            float minX = bounds.xMin;
            float maxX = Mathf.Max(minX, bounds.xMax - size.x);
            float minY = Mathf.Min(bounds.yMax, bounds.yMin + size.y);
            float maxY = bounds.yMax;
            Root.anchoredPosition = new Vector2(Mathf.Clamp(x, minX, maxX), Mathf.Clamp(y, minY, maxY));
        }

        private static UiTooltipTrigger GetOrAddTrigger(GameObject target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }
            UiTooltipTrigger trigger = target.GetComponent<UiTooltipTrigger>();
            if (trigger == null)
            {
                trigger = target.AddComponent<UiTooltipTrigger>();
            }
            return trigger;
        }
    }

    public sealed class UiTooltipTrigger : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private UiTooltip _tooltip;
        private string _text;
        private Func<string> _textProvider;
        private bool _pointerInside;
        private bool _selected;

        public string Text
        {
            get { return _text; }
            set { _text = value; }
        }

        public Func<string> TextProvider
        {
            get { return _textProvider; }
            set { _textProvider = value; }
        }

        internal void Configure(UiTooltip tooltip, string text, Func<string> textProvider)
        {
            _tooltip = tooltip;
            _text = text;
            _textProvider = textProvider;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            Show(eventData != null ? eventData.position : (Vector2)Input.mousePosition, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            if (!_selected && _tooltip != null)
            {
                _tooltip.Hide(this);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            RectTransform anchor = transform as RectTransform;
            Vector2 point = _tooltip != null ? _tooltip.GetAnchorScreenPoint(anchor) : Vector2.zero;
            Show(point, false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            if (!_pointerInside && _tooltip != null)
            {
                _tooltip.Hide(this);
            }
        }

        private void OnDisable()
        {
            _pointerInside = false;
            _selected = false;
            if (_tooltip != null)
            {
                _tooltip.Hide(this);
            }
        }

        private void Show(Vector2 screenPoint, bool followPointer)
        {
            if (_tooltip == null)
            {
                return;
            }
            string content = _textProvider != null ? _textProvider() : _text;
            if (string.IsNullOrEmpty(content))
            {
                _tooltip.Hide(this);
                return;
            }
            _tooltip.Show(this, transform as RectTransform, content, screenPoint, followPointer);
        }
    }
}
