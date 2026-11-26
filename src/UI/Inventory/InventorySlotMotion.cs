using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Inventory
{
    internal sealed class InventorySlotMotion : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private static readonly Vector2 HiddenActions = new Vector2(0f, -40f);

        private UiMotion _motion;
        private RectTransform _pivot;
        private Image _shadow;
        private RectTransform _actions;
        private CanvasGroup _actionsGroup;
        private GameObject _focusFrame;
        private bool _filled;
        private bool _actionsEnabled;
        private bool _pointerInside;
        private bool _focusWithin;
        private bool _lastReducedMotion;

        internal void Configure(
            UiMotion motion,
            RectTransform pivot,
            RectTransform actions,
            CanvasGroup actionsGroup,
            GameObject focusFrame)
        {
            _motion = motion;
            _pivot = pivot;
            _shadow = pivot.Find("ContractShadow").GetComponent<Image>();
            _actions = actions;
            _actionsGroup = actionsGroup;
            _focusFrame = focusFrame;
            _lastReducedMotion = _motion != null && _motion.ReducedMotion;
            Apply(true);
        }

        internal void SetState(bool filled, bool actionsEnabled)
        {
            _filled = filled;
            _actionsEnabled = filled && actionsEnabled;
            if (_actions != null)
                _actions.gameObject.SetActive(_actionsEnabled);
            Apply(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            Apply(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            GameObject current = eventData == null ? null : eventData.pointerCurrentRaycast.gameObject;
            _pointerInside = current != null && IsDescendant(current.transform);
            Apply(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            _focusWithin = RefreshFocus();
            Apply(false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _focusWithin = RefreshFocus();
            Apply(false);
        }

        private void Update()
        {
            bool focus = RefreshFocus();
            UpdateFocusFrame(focus);
            bool reduced = _motion != null && _motion.ReducedMotion;
            if (focus != _focusWithin || reduced != _lastReducedMotion)
            {
                _focusWithin = focus;
                _lastReducedMotion = reduced;
                Apply(reduced);
            }
        }

        private bool RefreshFocus()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
            return UiFocusOrigin.KeyboardActive && selected != null && selected.activeInHierarchy && IsDescendant(selected.transform);
        }

        private bool IsDescendant(Transform candidate)
        {
            Transform current = candidate;
            while (current != null)
            {
                if (current == transform)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private void Apply(bool instant)
        {
            if (_pivot == null || _actions == null || _actionsGroup == null)
                return;
            SafeTween.Kill(gameObject);
            bool highlighted = _filled && (_pointerInside || _focusWithin);
            if (_shadow != null)
            {
                _shadow.color = highlighted ? new Color32(180,147,88,255) : new Color32(32,35,33,125);
                _shadow.rectTransform.offsetMin = highlighted ? new Vector2(5,-1) : new Vector2(3,3);
                _shadow.rectTransform.offsetMax = highlighted ? new Vector2(0,-7) : new Vector2(-2,-4);
            }
            bool showActions = _actionsEnabled && highlighted;
            bool reduced = _motion != null && _motion.ReducedMotion;
            instant = instant || reduced;
            Vector2 pivotPosition = highlighted ? new Vector2(0f, 3f) : Vector2.zero;
            Vector3 pivotRotation = highlighted ? new Vector3(0f, 0f, 0.5f) : Vector3.zero;
            Vector2 actionsPosition = showActions ? Vector2.zero : HiddenActions;
            UpdateFocusFrame(_focusWithin);
            _actionsGroup.blocksRaycasts = showActions;
            _actionsGroup.interactable = showActions;
            if (instant)
            {
                _pivot.anchoredPosition = pivotPosition;
                _pivot.localRotation = Quaternion.Euler(pivotRotation);
                _actions.anchoredPosition = actionsPosition;
                _actionsGroup.alpha = showActions ? 1f : 0f;
                return;
            }

            _pivot.DOAnchorPos(pivotPosition, 0.13f).SetEase(Ease.OutCubic).SetUpdate(true).SetId(gameObject);
            _pivot.DOLocalRotate(pivotRotation, 0.13f).SetEase(Ease.OutCubic).SetUpdate(true).SetId(gameObject);
            _actions.DOAnchorPos(actionsPosition, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true).SetId(gameObject);
            _actionsGroup.DOFade(showActions ? 1f : 0f, 0.13f).SetUpdate(true).SetId(gameObject);
        }

        private void OnDisable()
        {
            SafeTween.Kill(gameObject);
            _pointerInside = false;
            _focusWithin = false;
            Apply(true);
        }

        private void UpdateFocusFrame(bool within)
        {
            bool onCard = _filled && within && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject;
            if (_focusFrame != null && _focusFrame.activeSelf != onCard)
                _focusFrame.SetActive(onCard);
        }

        private void OnDestroy()
        {
            SafeTween.Kill(gameObject);
            _motion = null;
            _pivot = null;
            _actions = null;
            _actionsGroup = null;
            _focusFrame = null;
        }
    }
}
