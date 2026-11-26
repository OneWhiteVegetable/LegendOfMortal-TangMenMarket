using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketTradeButtonMotion : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float Duration = 0.12f;
        private const float HoverBrightness = 1.12f;
        private UiMotion _motion;
        private Button _button;
        private RectTransform _rect;
        private Graphic _background;
        private Graphic _icon;
        private Image _shadow;
        private Image _border;
        private Image _face;
        private bool _paintedInteractable;
        private Color _backgroundRestColor;
        private Color _iconRestColor;
        private Color _faceRestColor;
        private Vector2 _restPosition;
        private Vector3 _restScale;
        private Quaternion _restRotation;
        private Vector2 _fromPosition;
        private Vector3 _fromScale;
        private Quaternion _fromRotation;
        private float _fromBrightness;
        private float _brightness = 1f;
        private bool _hovered;
        private bool _pressed;
        private Tween _transition;

        internal static MarketTradeButtonMotion Attach(Button button, UiMotion motion, Color faceColor)
        {
            if (button == null)
                return null;
            MarketTradeButtonMotion driver = button.GetComponent<MarketTradeButtonMotion>();
            if (driver != null)
                return driver;
            driver = button.gameObject.AddComponent<MarketTradeButtonMotion>();
            driver._motion = motion;
            driver._button = button;
            driver._rect = button.GetComponent<RectTransform>();
            driver._background = button.targetGraphic == null
                ? button.GetComponent<Graphic>()
                : button.targetGraphic;
            Transform icon = button.transform.Find("Icon");
            driver._icon = icon == null ? null : icon.GetComponent<Graphic>();
            driver._restPosition = driver._rect.anchoredPosition;
            driver._restScale = driver._rect.localScale;
            driver._restRotation = driver._rect.localRotation;
            driver._faceRestColor = faceColor;
            driver._shadow = CreatePanel(button.transform, "TradeShadow");
            driver._border = CreatePanel(button.transform, "TradeBorder");
            driver._face = CreatePanel(driver._border.transform, "TradeFace");
            driver._shadow.gameObject.AddComponent<MarketPaperEdge>();
            driver._border.gameObject.AddComponent<MarketPaperEdge>();
            driver._face.gameObject.AddComponent<MarketPaperEdge>();
            if (icon != null)
                icon.SetAsLastSibling();
            if (driver._background != null)
                driver._backgroundRestColor = driver._background.color;
            if (driver._icon != null)
                driver._iconRestColor = driver._icon.color;
            driver.ResetToRest();
            return driver;
        }

        private static Image CreatePanel(Transform parent, string name)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            Image image = gameObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        internal void SetRestPosition(Vector2 value, float cssPixel)
        {
            _restPosition = value;
            _face.rectTransform.offsetMin = new Vector2(cssPixel, cssPixel);
            _face.rectTransform.offsetMax = -_face.rectTransform.offsetMin;
            _shadow.rectTransform.anchoredPosition = new Vector2(2f * cssPixel, -2f * cssPixel);
            StopTransition();
            ApplyCurrent(1f);
        }

        internal void Settle()
        {
            StopTransition();
            ApplyCurrent(1f);
        }

        internal void ResetToRest()
        {
            _hovered = false;
            _pressed = false;
            StopTransition();
            if (_rect == null)
                return;
            ApplyCurrent(1f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Transition();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
            Transition();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left ||
                _button == null || !_button.IsInteractable())
                return;
            _pressed = true;
            Transition();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                return;
            _pressed = false;
            Transition();
        }

        private void Transition()
        {
            if (_rect == null)
                return;
            StopTransition();
            _fromPosition = _rect.anchoredPosition;
            _fromScale = _rect.localScale;
            _fromRotation = _rect.localRotation;
            _fromBrightness = _brightness;
            if (_motion != null && _motion.ReducedMotion)
            {
                ApplyCurrent(1f);
                return;
            }
            try
            {
                Tween tween = null;
                tween = DOVirtual.Float(0f, 1f, Duration, delegate(float value)
                {
                    if (_rect == null)
                        return;
                    float eased = UiMotionEasing.CubicBezier(value, 0.25f, 0.1f, 0.25f, 1f);
                    ApplyCurrent(eased);
                }).SetEase(Ease.Linear).SetUpdate(true).OnKill(delegate
                {
                    if (ReferenceEquals(_transition, tween))
                        _transition = null;
                });
                _transition = tween;
            }
            catch (Exception)
            {
                StopTransition();
                ApplyCurrent(1f);
            }
        }

        private void ApplyCurrent(float value)
        {
            bool interactable = _button != null && _button.IsInteractable();
            bool pressed = interactable && _pressed;
            bool hovered = interactable && _hovered;
            Vector2 targetPosition = _restPosition + (pressed
                ? new Vector2(1f, -1f)
                : hovered ? new Vector2(0f, 3f) : Vector2.zero);
            Vector3 targetScale = pressed ? _restScale * 0.93f : _restScale;
            Quaternion targetRotation = hovered && !pressed
                ? _restRotation * Quaternion.Euler(0f, 0f, 3f)
                : _restRotation;
            float targetBrightness = hovered ? HoverBrightness : 1f;
            _paintedInteractable = interactable;
            if (_border != null && _border.gameObject.activeSelf != interactable)
                _border.gameObject.SetActive(interactable);
            bool showShadow = interactable && !pressed;
            if (_shadow != null && _shadow.gameObject.activeSelf != showShadow)
                _shadow.gameObject.SetActive(showShadow);
            _rect.anchoredPosition = Vector2.LerpUnclamped(_fromPosition, targetPosition, value);
            _rect.localScale = Vector3.LerpUnclamped(_fromScale, targetScale, value);
            _rect.localRotation = Quaternion.SlerpUnclamped(_fromRotation, targetRotation, value);
            ApplyBrightness(Mathf.LerpUnclamped(_fromBrightness, targetBrightness, value));
        }

        private void ApplyBrightness(float value)
        {
            _brightness = value;
            if (_background != null)
                _background.color = Scale(_backgroundRestColor, value);
            if (_icon != null)
                _icon.color = Scale(_iconRestColor, value);
            Color ink = Scale(UiFormatting.Ink, value);
            if (_border != null)
                _border.color = ink;
            if (_shadow != null)
                _shadow.color = ink;
            if (_face != null)
                _face.color = Scale(_faceRestColor, value);
        }

        private void LateUpdate()
        {
            if (_button != null && _button.IsInteractable() != _paintedInteractable)
                Settle();
        }

        private static Color Scale(Color color, float value)
        {
            color.r *= value;
            color.g *= value;
            color.b *= value;
            return color;
        }

        private void StopTransition()
        {
            SafeTween.Kill(_transition);
            _transition = null;
        }

        private void OnDisable()
        {
            ResetToRest();
        }

        private void OnDestroy()
        {
            StopTransition();
        }
    }
}
