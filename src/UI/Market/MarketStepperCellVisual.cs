using UnityEngine;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketStepperCellVisual
    {
        internal const string FaceName = "StepperFace";
        internal const float Duration = 0.1f;
        internal const float PressScale = 0.88f;

        private readonly Button _button;
        private readonly Image _icon;
        private readonly RectTransform _iconRect;
        private readonly Image _face;
        private readonly RectTransform _faceRect;
        private readonly Vector3 _iconRestScale;
        private readonly Color _normal;
        private readonly Color _highlighted;
        private readonly float _disabledAlpha;
        private readonly UiMotion _motion;
        private Color _fromFace;
        private Color _targetFace;
        private Color _currentFace;
        private Color _fromIcon;
        private Color _targetIcon;
        private Color _currentIcon;
        private float _scale;
        private float _opacity = 1f;
        private float _elapsed;
        private bool _transitioning;

        internal MarketStepperCellVisual(
            Button button,
            Image icon,
            Color normal,
            Color highlighted,
            float disabledAlpha,
            UiMotion motion)
        {
            _button = button;
            _icon = icon;
            _iconRect = icon.rectTransform;
            _iconRestScale = _iconRect.localScale;
            _normal = normal;
            _highlighted = highlighted;
            _disabledAlpha = disabledAlpha;
            _motion = motion;
            _button.transition = Selectable.Transition.None;
            if (_button.targetGraphic != null)
            {
                _button.targetGraphic.raycastTarget = true;
                _button.targetGraphic.color = Color.clear;
            }

            Transform existing = _button.transform.Find(FaceName);
            if (existing == null)
            {
                var faceObject = new GameObject(FaceName, typeof(RectTransform), typeof(Image));
                faceObject.layer = _button.gameObject.layer;
                existing = faceObject.transform;
                existing.SetParent(_button.transform, false);
            }
            _face = existing.GetComponent<Image>();
            if (_face == null)
                _face = existing.gameObject.AddComponent<Image>();
            _faceRect = _face.rectTransform;
            _faceRect.anchorMin = Vector2.zero;
            _faceRect.anchorMax = Vector2.one;
            _faceRect.pivot = new Vector2(0.5f, 0.5f);
            _faceRect.offsetMin = Vector2.zero;
            _faceRect.offsetMax = Vector2.zero;
            _faceRect.localRotation = Quaternion.identity;
            _faceRect.localScale = Vector3.one;
            _face.sprite = null;
            _face.type = Image.Type.Simple;
            _face.raycastTarget = false;
            _face.transform.SetSiblingIndex(0);
            Reset(_button.IsInteractable());
        }

        internal void Apply(bool highlighted, bool pressed, bool interactable)
        {
            Color face = highlighted && interactable ? _normal : Color.clear;
            Color icon = highlighted && interactable ? _highlighted : _normal;
            SetScale(pressed && interactable ? PressScale : 1f);
            float opacity = interactable ? 1f : _disabledAlpha;
            bool opacityChanged = opacity != _opacity;
            _opacity = opacity;
            if (face != _targetFace || icon != _targetIcon)
            {
                _fromFace = _currentFace;
                _fromIcon = _currentIcon;
                _targetFace = face;
                _targetIcon = icon;
                _elapsed = 0f;
                _transitioning = true;
            }
            if (_transitioning && _motion != null && _motion.ReducedMotion)
                CompleteTransition();
            else if (opacityChanged)
                AssignColors();
        }

        internal void Advance(float deltaTime)
        {
            if (!_transitioning)
                return;
            _elapsed = Mathf.Min(Duration, _elapsed + Mathf.Max(0f, deltaTime));
            if (_elapsed >= Duration || (_motion != null && _motion.ReducedMotion))
            {
                CompleteTransition();
                return;
            }
            float eased = UiMotionEasing.CubicBezier(_elapsed / Duration, 0.25f, 0.1f, 0.25f, 1f);
            _currentFace = Color.LerpUnclamped(_fromFace, _targetFace, eased);
            _currentIcon = Color.LerpUnclamped(_fromIcon, _targetIcon, eased);
            AssignColors();
        }

        internal void Reset(bool interactable)
        {
            _targetFace = Color.clear;
            _targetIcon = _normal;
            _opacity = interactable ? 1f : _disabledAlpha;
            SetScale(1f);
            CompleteTransition();
        }

        private void CompleteTransition()
        {
            _currentFace = _targetFace;
            _currentIcon = _targetIcon;
            _elapsed = Duration;
            _transitioning = false;
            AssignColors();
        }

        private void AssignColors()
        {
            if (_face != null)
            {
                Color face = _currentFace;
                face.a *= _opacity;
                _face.color = face;
            }
            if (_icon != null)
            {
                Color icon = _currentIcon;
                icon.a *= _opacity;
                _icon.color = icon;
            }
        }

        private void SetScale(float scale)
        {
            if (scale == _scale)
                return;
            _scale = scale;
            if (_faceRect != null)
                _faceRect.localScale = new Vector3(scale, scale, 1f);
            if (_iconRect != null)
            {
                _iconRect.localScale = new Vector3(
                    _iconRestScale.x * scale,
                    _iconRestScale.y * scale,
                    _iconRestScale.z);
            }
        }
    }
}
