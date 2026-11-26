using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Market;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiTooltip : IDisposable
    {
        private const float MaximumWidth = 300f;
        private const float HorizontalPadding = 30f;
        private const float EdgeMargin = 10f;

        private readonly UiFrontendServices _services;
        private readonly UiOverlayService _overlays;
        private readonly UiPrefabInstance _view;
        private readonly CanvasGroup _group;
        private readonly ContentSizeFitter _fitter;
        private readonly int _bodySiblingIndex;
        private readonly float _bodyLineSpacing;
        private RectTransform _bodyViewport;
        private ScrollRect _bodyScroll;
        private UiTooltipTrigger _owner;
        private UiTooltipTrigger _pendingOwner;
        private Vector2 _pointerPosition;
        private bool _usePointer;
        private Tween _delay;
        private Tween _move;
        private Tween _fade;
        private Vector2 _finalPosition;
        private int _requestVersion;
        private bool _disposed;

        internal UiTooltip(UiFrontendServices services, UiOverlayService overlays)
        {
            _services = services;
            _overlays = overlays;
            _view = services.Assets.Create("Tooltip");
            _group = _view.Get<CanvasGroup>("CanvasGroup");
            _fitter = _view.Root.GetComponent<ContentSizeFitter>();
            _bodySiblingIndex = _view.Text("BodyText").transform.GetSiblingIndex();
            _bodyLineSpacing = _view.Text("BodyText").lineSpacing;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            Canvas canvas = _view.Root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30900;
            Graphic[] graphics = _view.Root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
            MarketTextInkEffect.Attach(_view, "TitleText", MarketTextInkKind.PhysicalText);
            MarketTextInkEffect.Attach(_view, "BodyText", MarketTextInkKind.PhysicalText);
            MarketTextInkEffect.Attach(_view, "HintText", MarketTextInkKind.PhysicalText);
            _view.SetActive(false);
        }

        internal void Attach(
            GameObject target,
            Func<string> title,
            Func<string> body,
            Func<string> hint,
            bool resolveItemFromHierarchy)
        {
            if (target == null)
                return;
            UiTooltipTrigger trigger = target.GetComponent<UiTooltipTrigger>();
            if (trigger == null)
                trigger = target.AddComponent<UiTooltipTrigger>();
            trigger.Configure(this, title, body, hint, resolveItemFromHierarchy);
        }

        internal void Schedule(UiTooltipTrigger owner, Vector2 pointerPosition, bool usePointer)
        {
            if (_disposed || owner == null || _overlays.CurrentRoot == null)
                return;
            if (_delay != null && owner == _pendingOwner && usePointer == _usePointer)
            {
                _pointerPosition = pointerPosition;
                return;
            }
            if (_delay == null && _pendingOwner == null && owner == _owner &&
                usePointer == _usePointer && _view.IsAlive && _view.GameObject.activeSelf)
            {
                _pointerPosition = pointerPosition;
                return;
            }
            CancelTweens();
            _requestVersion++;
            int version = _requestVersion;
            _pendingOwner = owner;
            _pointerPosition = pointerPosition;
            _usePointer = usePointer;
            if (_view.IsAlive)
                _view.SetActive(false);
            _owner = null;
            try
            {
                _delay = DOVirtual.DelayedCall(0.15f, delegate { ShowNow(owner, version); }, false).SetUpdate(true);
            }
            catch (Exception)
            {
                ShowNow(owner, version);
            }
        }

        private void ShowNow(UiTooltipTrigger owner, int version)
        {
            _delay = null;
            if (_disposed || version != _requestVersion || owner == null || owner != _pendingOwner ||
                !owner.gameObject.activeInHierarchy || _overlays.CurrentRoot == null)
                return;
            string title = Resolve(owner.Title);
            string body = Resolve(owner.Body);
            string hint = Resolve(owner.Hint);
            if (owner.ResolveItemFromHierarchy)
                _overlays.TryResolveItemTooltip(owner.transform, ref title, ref body, ref hint);
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body) && string.IsNullOrEmpty(hint))
                return;

            _owner = owner;
            _pendingOwner = null;
            RestoreBodyText();
            _view.Text("TitleText").text = title;
            _view.Text("BodyText").text = body;
            _view.Text("HintText").text = hint;
            _view.Text("TitleText").gameObject.SetActive(!string.IsNullOrEmpty(title));
            _view.Text("BodyText").gameObject.SetActive(!string.IsNullOrEmpty(body));
            _view.Text("HintText").gameObject.SetActive(!string.IsNullOrEmpty(hint));
            _view.Root.SetParent(_overlays.CurrentRoot, false);
            _view.Root.SetAsLastSibling();
            _view.Root.localScale = Vector3.one;
            _view.Text("TitleText").SetVerticesDirty();
            _view.Text("BodyText").SetVerticesDirty();
            _view.Text("HintText").SetVerticesDirty();
            _group.alpha = 0f;
            _view.SetActive(true);
            _fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            FitContentWidth();
            ConfigureBodyLineSpacing();
            Canvas.ForceUpdateCanvases();
            ConfigureBodyLayoutHeight();
            Canvas.ForceUpdateCanvases();
            FitContentHeight();
            Canvas.ForceUpdateCanvases();
            Vector2 finalPosition = Place(owner.transform as RectTransform);
            PlayEnter(finalPosition);
        }

        private void FitContentWidth()
        {
            RectTransform space = _view.Root.parent as RectTransform;
            float maximumTextWidth = MaximumWidth - HorizontalPadding;
            if (space != null)
            {
                maximumTextWidth = Mathf.Max(1f, Mathf.Min(
                    maximumTextWidth,
                    space.rect.width - HorizontalPadding - EdgeMargin * 2f));
            }
            FitTextWidth("TitleText", maximumTextWidth);
            FitTextWidth("BodyText", maximumTextWidth);
            FitTextWidth("HintText", maximumTextWidth);
        }

        private void FitTextWidth(string key, float maximumWidth)
        {
            Text text = _view.Text(key);
            if (!text.gameObject.activeSelf)
                return;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            LayoutElement layout = text.GetComponent<LayoutElement>();
            layout.preferredWidth = Mathf.Max(1f, maximumWidth);
        }

        private void ConfigureBodyLineSpacing()
        {
            Text body = _view.Text("BodyText");
            if (!body.gameObject.activeSelf)
                return;
            body.lineSpacing = 1f;
            Canvas.ForceUpdateCanvases();
            int lineCount = Mathf.Max(1, body.cachedTextGenerator.lineCount);
            float unitLineHeight = body.preferredHeight / lineCount;
            body.lineSpacing = unitLineHeight <= 0.01f
                ? _bodyLineSpacing
                : body.fontSize * _bodyLineSpacing / unitLineHeight;
        }

        private void ConfigureBodyLayoutHeight()
        {
            Text body = _view.Text("BodyText");
            if (!body.gameObject.activeSelf)
                return;
            int lineCount = Mathf.Max(1, body.cachedTextGenerator.lineCount);
            body.GetComponent<LayoutElement>().preferredHeight =
                lineCount * body.fontSize * _bodyLineSpacing + 1f;
        }

        private void FitContentHeight()
        {
            RectTransform space = _view.Root.parent as RectTransform;
            Text body = _view.Text("BodyText");
            if (space == null || !body.gameObject.activeSelf)
                return;
            float maximumHeight = Mathf.Max(1f,
                space.rect.height - EdgeMargin * 2f);
            if (_view.Root.rect.height <= maximumHeight + 0.01f)
                return;

            float fixedHeight = _view.Root.rect.height - body.rectTransform.rect.height;
            float availableBodyHeight = Mathf.Max(1f, maximumHeight - fixedHeight);
            if (availableBodyHeight >= body.rectTransform.rect.height)
                return;

            EnsureBodyViewport();
            LayoutElement bodyLayout = body.GetComponent<LayoutElement>();
            LayoutElement viewportLayout = _bodyViewport.GetComponent<LayoutElement>();
            float fullBodyHeight = Mathf.Max(1f, Mathf.Ceil(Mathf.Max(
                body.rectTransform.rect.height, body.preferredHeight)));
            viewportLayout.preferredWidth = bodyLayout.preferredWidth;
            viewportLayout.flexibleWidth = 1f;
            viewportLayout.preferredHeight = availableBodyHeight;
            bodyLayout.preferredHeight = fullBodyHeight;
            bodyLayout.flexibleHeight = 0f;
            body.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.offsetMin = new Vector2(0f, -fullBodyHeight);
            bodyRect.offsetMax = Vector2.zero;
            bodyRect.localScale = Vector3.one;
            _bodyViewport.gameObject.SetActive(true);
            _bodyScroll.content = bodyRect;
            _bodyScroll.verticalNormalizedPosition = 1f;
        }

        private void EnsureBodyViewport()
        {
            if (_bodyViewport != null)
            {
                _bodyViewport.SetSiblingIndex(_bodySiblingIndex);
                _view.Text("BodyText").transform.SetParent(_bodyViewport, false);
                return;
            }

            GameObject viewport = new GameObject("BodyViewport",
                typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect), typeof(LayoutElement));
            _bodyViewport = viewport.GetComponent<RectTransform>();
            _bodyViewport.SetParent(_view.Root, false);
            _bodyViewport.SetSiblingIndex(_bodySiblingIndex);
            _bodyViewport.localScale = Vector3.one;
            LayoutElement layout = viewport.GetComponent<LayoutElement>();
            layout.minWidth = -1f;
            layout.minHeight = -1f;
            layout.flexibleHeight = 0f;
            _bodyScroll = viewport.GetComponent<ScrollRect>();
            _bodyScroll.viewport = _bodyViewport;
            _bodyScroll.horizontal = false;
            _bodyScroll.vertical = true;
            _bodyScroll.inertia = false;
            _bodyScroll.movementType = ScrollRect.MovementType.Clamped;
            _bodyScroll.scrollSensitivity = 0f;
            _view.Text("BodyText").transform.SetParent(_bodyViewport, false);
        }

        private void RestoreBodyText()
        {
            Text body = _view.Text("BodyText");
            LayoutElement bodyLayout = body.GetComponent<LayoutElement>();
            bodyLayout.preferredHeight = -1f;
            bodyLayout.flexibleHeight = 0f;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            if (_bodyViewport == null)
                return;

            _bodyScroll.content = null;
            _bodyViewport.gameObject.SetActive(false);
            RectTransform bodyRect = body.rectTransform;
            bodyRect.SetParent(_view.Root, false);
            bodyRect.SetSiblingIndex(_bodySiblingIndex);
            bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.sizeDelta = new Vector2(100f, 100f);
            bodyRect.localScale = Vector3.one;
        }

        internal bool Scroll(UiTooltipTrigger owner, Vector2 scrollDelta)
        {
            if (owner == null || owner != _owner || _bodyScroll == null ||
                !_bodyViewport.gameObject.activeSelf || !_view.GameObject.activeSelf)
                return false;
            float range = _bodyScroll.content.rect.height - _bodyScroll.viewport.rect.height;
            if (range <= 0.01f)
                return false;
            Text body = _view.Text("BodyText");
            float lineHeight = Mathf.Max(1f,
                body.preferredHeight / Mathf.Max(1, body.cachedTextGenerator.lineCount));
            _bodyScroll.verticalNormalizedPosition = Mathf.Clamp01(
                _bodyScroll.verticalNormalizedPosition + scrollDelta.y * lineHeight / range);
            return true;
        }

        private Vector2 Place(RectTransform target)
        {
            RectTransform space = _view.Root.parent as RectTransform;
            if (space == null)
                return Vector2.zero;
            Camera camera = ResolveCamera(space);
            float edgeMargin = EdgeMargin;
            Vector2 size = _view.Root.rect.size;
            size.x = Mathf.Min(MaximumWidth, Mathf.Max(1f, size.x));
            size.y = Mathf.Max(1f, size.y);
            Rect bounds = space.rect;
            _view.Text("BodyText").verticalOverflow = VerticalWrapMode.Overflow;

            _view.Root.anchorMin = new Vector2(0.5f, 0.5f);
            _view.Root.anchorMax = new Vector2(0.5f, 0.5f);
            _view.Root.pivot = new Vector2(0f, 1f);
            bool inventoryCard = target != null &&
                target.Find("MotionPivot/CardClip/FilledContent") != null;
            Vector2 local = Vector2.zero;
            Vector2 targetLeft = Vector2.zero;
            if (inventoryCard || (!_usePointer && target != null))
            {
                Vector3[] corners = new Vector3[4];
                target.GetWorldCorners(corners);
                Vector2 rightScreen = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(space, rightScreen, camera, out local);
                if (inventoryCard)
                {
                    Vector2 leftScreen = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(space, leftScreen, camera, out targetLeft);
                }
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(space, _pointerPosition, camera, out local);
            }

            Vector2 desired = inventoryCard
                ? local + new Vector2(12f, 0f)
                : local + new Vector2(16f, -18f);
            if (inventoryCard && desired.x + size.x > bounds.xMax - edgeMargin)
                desired.x = targetLeft.x - size.x - 12f;
            float minimumX = bounds.xMin + edgeMargin;
            float maximumX = Mathf.Max(minimumX, bounds.xMax - size.x - edgeMargin);
            float minimumY = Mathf.Min(bounds.yMax - edgeMargin, bounds.yMin + size.y + edgeMargin);
            float maximumY = bounds.yMax - edgeMargin;
            desired.x = Mathf.Clamp(desired.x, minimumX, maximumX);
            desired.y = Mathf.Clamp(desired.y, minimumY, maximumY);
            return desired;
        }

        private static Camera ResolveCamera(RectTransform space)
        {
            Canvas canvas = space.GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;
            Canvas rootCanvas = canvas.rootCanvas;
            if (rootCanvas != null)
                canvas = rootCanvas;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;
            return canvas.worldCamera == null ? Camera.main : canvas.worldCamera;
        }

        private void PlayEnter(Vector2 finalPosition)
        {
            _finalPosition = finalPosition;
            if (_services.Motion.ReducedMotion)
            {
                _view.Root.anchoredPosition = finalPosition;
                _group.alpha = 1f;
                return;
            }
            _view.Root.anchoredPosition = finalPosition + new Vector2(0f, -5f);
            try
            {
                _move = _view.Root.DOAnchorPos(finalPosition, 0.12f).SetUpdate(true).SetEase(UiMotionEasing.CssEaseOut);
                _fade = _group.DOFade(1f, 0.12f).SetUpdate(true).SetEase(UiMotionEasing.CssEaseOut);
            }
            catch (Exception)
            {
                _view.Root.anchoredPosition = finalPosition;
                _group.alpha = 1f;
            }
        }

        internal void SettleForReducedMotion()
        {
            if (_disposed)
                return;
            SafeTween.Kill(_move);
            SafeTween.Kill(_fade);
            _move = null;
            _fade = null;
            if (_owner == null || !_view.IsAlive || !_view.GameObject.activeSelf)
                return;
            _view.Root.anchoredPosition = _finalPosition;
            _group.alpha = 1f;
        }

        internal void PointerEntered(UiTooltipTrigger source, PointerEventData eventData)
        {
            UiTooltipTrigger owner = ResolvePointerOwner(eventData) ?? source;
            if (owner != null)
                Schedule(owner, eventData == null ? Vector2.zero : eventData.position, true);
        }

        internal void PointerExited(UiTooltipTrigger source, PointerEventData eventData)
        {
            UiTooltipTrigger owner = ResolvePointerOwner(eventData);
            if (owner != null)
                Schedule(owner, eventData.position, true);
            else
                Hide();
        }

        private UiTooltipTrigger ResolvePointerOwner(PointerEventData eventData)
        {
            GameObject hit = eventData == null ? null : eventData.pointerCurrentRaycast.gameObject;
            Transform current = hit == null ? null : hit.transform;
            while (current != null)
            {
                UiTooltipTrigger trigger = current.GetComponent<UiTooltipTrigger>();
                if (trigger != null && trigger.isActiveAndEnabled && trigger.BelongsTo(this))
                    return trigger;
                current = current.parent;
            }
            return null;
        }

        internal void Hide(UiTooltipTrigger owner = null)
        {
            if (owner != null && owner != _owner && owner != _pendingOwner)
                return;
            _requestVersion++;
            _owner = null;
            _pendingOwner = null;
            CancelTweens();
            if (_group != null)
                _group.alpha = 0f;
            if (_view.IsAlive)
                _view.SetActive(false);
        }

        private void CancelTweens()
        {
            SafeTween.Kill(_delay);
            SafeTween.Kill(_move);
            SafeTween.Kill(_fade);
            _delay = null;
            _move = null;
            _fade = null;
        }

        private static string Resolve(Func<string> value)
        {
            return value == null ? string.Empty : value() ?? string.Empty;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            Hide();
            _disposed = true;
            _view.Dispose();
        }
    }

    internal sealed class UiTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IScrollHandler
    {
        private UiTooltip _tooltip;
        private PointerEventData _pointerEventData;
        private bool _pointerInside;
        private bool _selected;
        internal Func<string> Title;
        internal Func<string> Body;
        internal Func<string> Hint;
        internal bool ResolveItemFromHierarchy { get; private set; }

        internal void Configure(
            UiTooltip tooltip,
            Func<string> title,
            Func<string> body,
            Func<string> hint,
            bool resolveItemFromHierarchy)
        {
            _tooltip = tooltip;
            Title = title;
            Body = body;
            Hint = hint;
            ResolveItemFromHierarchy = resolveItemFromHierarchy;
        }

        internal bool BelongsTo(UiTooltip tooltip)
        {
            return _tooltip == tooltip;
        }

        internal void InvalidateBinding()
        {
            if (_tooltip == null)
                return;
            _tooltip.Hide(this);
            if (_pointerInside && _pointerEventData != null)
                _tooltip.PointerEntered(this, _pointerEventData);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            _pointerEventData = eventData;
            if (_tooltip != null)
                _tooltip.PointerEntered(this, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            GameObject hit = eventData == null ? null : eventData.pointerCurrentRaycast.gameObject;
            _pointerInside = hit != null && hit.transform.IsChildOf(transform);
            _pointerEventData = _pointerInside ? eventData : null;
            if (_tooltip != null)
                _tooltip.PointerExited(this, eventData);
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = !(eventData is PointerEventData);
            if (_selected && _tooltip != null)
                _tooltip.Schedule(this, Vector2.zero, false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            if (_tooltip != null && !_pointerInside)
                _tooltip.Hide(this);
        }

        public void OnScroll(PointerEventData eventData)
        {
            HideForScroll(eventData == null ? Vector2.zero : eventData.scrollDelta);
        }

        private void Update()
        {
            if ((_pointerInside || _selected) && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
                HideForScroll(Input.mouseScrollDelta);
        }

        private void HideForScroll(Vector2 scrollDelta)
        {
            if (_tooltip != null && !_tooltip.Scroll(this, scrollDelta))
                _tooltip.Hide(this);
        }

        private void OnDisable()
        {
            _pointerInside = false;
            _pointerEventData = null;
            _selected = false;
            if (_tooltip != null)
                _tooltip.Hide(this);
        }

        private void OnDestroy()
        {
            if (_tooltip != null)
                _tooltip.Hide(this);
            _pointerEventData = null;
            _tooltip = null;
            Title = null;
            Body = null;
            Hint = null;
        }
    }
}
