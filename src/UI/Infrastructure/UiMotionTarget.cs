using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    /// <summary>
    /// Reusable Pointer/Select behavior for a child MotionPivot. Create it through UiMotionScope.Attach rather
    /// than AddComponent directly. It owns one Sequence at a time, so rapid reversal starts from the visible pose
    /// without stacking tweens. Selection uses the hover pose; press temporarily overrides hover/focus.
    /// </summary>
    internal sealed class UiMotionTarget : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler,
        IDisposable
    {
        private enum InteractionState
        {
            Rest,
            Hover,
            Press
        }

        private UiMotionScope _owner;
        private CanvasGroup _canvasGroup;
        private Tween _transition;
        private Vector2 _restPosition;
        private Vector3 _restScale;
        private Vector3 _restEulerAngles;
        private float _restAlpha;
        private InteractionState _goal;
        private bool _hasGoal;
        private bool _hovered;
        private bool _pressed;
        private bool _selected;
        private bool _disposed;
        private int _transitionVersion;

        /// <summary>The visual pivot being animated. It is never assumed to be the LayoutRoot.</summary>
        internal RectTransform Target { get; private set; }

        /// <summary>The scope that owns this target's tween and lifecycle.</summary>
        internal UiMotionScope Owner { get { return _owner; } }

        /// <summary>True after Dispose or Unity destruction.</summary>
        internal bool IsDisposed { get { return _disposed; } }

        /// <summary>
        /// Optional interactability source, initially the Selectable on the event-source GameObject.
        /// Set it when the actual Button/Selectable is on a different node; call Refresh after changing it.
        /// </summary>
        internal Selectable Control { get; set; }

        /// <summary>Hover/focus duration in unscaled seconds. Default: 0.12.</summary>
        internal float HoverDuration { get; set; }

        /// <summary>Press and release duration in unscaled seconds. Default: 0.12.</summary>
        internal float PressDuration { get; set; }

        /// <summary>Hover/focus easing. Default: CSS ease; use PaperOutEase only where the prototype specifies it.</summary>
        internal EaseFunction HoverEase { get; set; }

        /// <summary>Press/release easing. Default: CSS ease.</summary>
        internal EaseFunction PressEase { get; set; }

        /// <summary>Hover/focus offset from rest in Unity coordinates: positive Y moves up. Default: (0, 2).</summary>
        internal Vector2 HoverOffset { get; set; }

        /// <summary>Press offset from rest, not additive with hover. Positive Y moves up. Default: (1, -1).</summary>
        internal Vector2 PressOffset { get; set; }

        /// <summary>Multiplier applied to the captured rest scale on hover/focus. Default: 1.</summary>
        internal float HoverScale { get; set; }

        /// <summary>Multiplier applied to the captured rest scale on press. Default: 0.96.</summary>
        internal float PressScale { get; set; }

        /// <summary>Local Euler-angle delta from rest on hover/focus, in degrees.</summary>
        internal Vector3 HoverEulerDelta { get; set; }

        /// <summary>Local Euler-angle delta from rest on press, in degrees.</summary>
        internal Vector3 PressEulerDelta { get; set; }

        /// <summary>CanvasGroup alpha on hover/focus. Used only when a group was passed to Attach. Default: 1.</summary>
        internal float HoverAlpha { get; set; }

        /// <summary>CanvasGroup alpha on press. Used only when a group was passed to Attach. Default: 1.</summary>
        internal float PressAlpha { get; set; }

        /// <summary>Current pointer/focus/press state, exposed for View diagnostics.</summary>
        internal bool IsHovered { get { return _hovered; } }
        internal bool IsFocused { get { return _selected; } }
        internal bool IsPressed { get { return _pressed; } }

        /// <summary>True while this target has a live transition.</summary>
        internal bool IsAnimating
        {
            get { return _transition != null && _transition.IsActive() && _transition.IsPlaying(); }
        }

        internal void Bind(UiMotionScope owner, RectTransform target, CanvasGroup canvasGroup)
        {
            _owner = owner;
            Target = target;
            _canvasGroup = canvasGroup;
            Control = GetComponent<Selectable>();
            HoverDuration = 0.12f;
            PressDuration = 0.12f;
            HoverEase = UiMotionEasing.CssEase;
            PressEase = UiMotionEasing.CssEase;
            HoverOffset = new Vector2(0f, 2f);
            PressOffset = new Vector2(1f, -1f);
            HoverScale = 1f;
            PressScale = 0.96f;
            HoverEulerDelta = Vector3.zero;
            PressEulerDelta = Vector3.zero;
            HoverAlpha = 1f;
            PressAlpha = 1f;
            CaptureRestPose();
        }

        /// <summary>
        /// Kills this target's current transition without completion, clears interaction state and restores the
        /// exact anchoredPosition/localScale/localEulerAngles/alpha captured at Bind. Call before pooling or rebinding.
        /// </summary>
        internal void ResetForReuse()
        {
            if (_disposed)
                return;
            _hovered = false;
            _pressed = false;
            _selected = false;
            _goal = InteractionState.Rest;
            _hasGoal = true;
            StopTransition();
            ApplyRestPose();
        }

        /// <summary>Re-evaluates the current pointer/focus/press pose, including Control.interactable.</summary>
        internal void Refresh()
        {
            if (_disposed)
                return;
            ApplyCurrent(_owner == null || _owner.ReducedMotion, true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanInteract())
                return;
            if (!_hovered)
            {
                _hovered = true;
                ApplyCurrent(false, false);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_disposed)
                return;
            bool changed = _hovered || _pressed;
            _hovered = false;
            _pressed = false;
            if (changed)
                ApplyCurrent(false, false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || !CanInteract())
                return;
            if (!_pressed)
            {
                _pressed = true;
                ApplyCurrent(false, false);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_disposed || eventData == null || eventData.button != PointerEventData.InputButton.Left)
                return;
            if (_pressed)
            {
                _pressed = false;
                ApplyCurrent(false, false);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!CanInteract())
                return;
            if (!_selected)
            {
                _selected = true;
                ApplyCurrent(false, false);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (_disposed)
                return;
            if (_selected)
            {
                _selected = false;
                ApplyCurrent(false, false);
            }
        }

        internal void SettleForReducedMotion()
        {
            if (!_disposed)
                ApplyCurrent(true, true);
        }

        /// <summary>
        /// Detaches this target from its scope, restores rest pose and kills its transition without completion.
        /// Only this behavior component is destroyed; the host GameObject and visual pivot remain intact.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        internal void Dispose(bool destroyComponent)
        {
            if (_disposed)
                return;
            _disposed = true;
            _hovered = false;
            _pressed = false;
            _selected = false;
            StopTransition();
            ApplyRestPose();
            if (_owner != null)
            {
                _owner.Forget(this);
                _owner = null;
            }
            enabled = false;
            if (destroyComponent)
                UnityEngine.Object.Destroy(this);
        }

        private void OnDisable()
        {
            ResetForReuse();
        }

        private void OnDestroy()
        {
            Dispose(false);
        }

        private bool CanInteract()
        {
            return !_disposed && isActiveAndEnabled && (Control == null || Control.IsInteractable());
        }

        private void CaptureRestPose()
        {
            _restPosition = Target.anchoredPosition;
            _restScale = Target.localScale;
            _restEulerAngles = Target.localEulerAngles;
            _restAlpha = _canvasGroup == null ? 1f : _canvasGroup.alpha;
            _goal = InteractionState.Rest;
            _hasGoal = true;
            ApplyRestPose();
        }

        private void ApplyRestPose()
        {
            if (Target == null)
                return;
            Target.anchoredPosition = _restPosition;
            Target.localScale = _restScale;
            Target.localEulerAngles = _restEulerAngles;
            if (_canvasGroup != null)
                _canvasGroup.alpha = _restAlpha;
        }

        private void ApplyCurrent(bool immediate, bool force)
        {
            if (_disposed || Target == null || _owner == null)
                return;

            InteractionState desired = GetDesiredState();
            if (!force && _hasGoal && _goal == desired)
                return;
            _goal = desired;
            _hasGoal = true;

            float duration = desired == InteractionState.Press ? PressDuration : HoverDuration;
            if (immediate || _owner.ReducedMotion || duration <= 0f)
            {
                StopTransition();
                ApplyPose(desired);
                return;
            }

            StopTransition();
            int version = _transitionVersion;
            Sequence sequence = null;
            try
            {
                sequence = DOTween.Sequence();
                if (sequence == null)
                {
                    ApplyPose(desired);
                    return;
                }
                sequence.Pause();
                _transition = sequence;

                Vector2 position;
                Vector3 scale;
                Vector3 eulerAngles;
                float alpha;
                GetPose(desired, out position, out scale, out eulerAngles, out alpha);
                EaseFunction ease = desired == InteractionState.Press ? PressEase : HoverEase;
                if (ease == null)
                    ease = UiMotionEasing.CssEase;
                sequence.Join(Target.DOAnchorPos(position, duration)
                    .SetEase(ease).SetUpdate(true));
                sequence.Join(Target.DOScale(scale, duration)
                    .SetEase(ease).SetUpdate(true));
                sequence.Join(Target.DOLocalRotate(eulerAngles, duration)
                    .SetEase(ease).SetUpdate(true));
                if (_canvasGroup != null)
                {
                    sequence.Join(_canvasGroup.DOFade(alpha, duration)
                        .SetEase(ease).SetUpdate(true));
                }

                _owner.Track(sequence, delegate
                {
                    if (_disposed || version != _transitionVersion)
                        return;
                    _transition = null;
                    if (_goal == desired)
                        ApplyPose(desired);
                });
                sequence.Play();
            }
            catch (Exception)
            {
                StopTransition();
                ApplyPose(desired);
            }
        }

        private InteractionState GetDesiredState()
        {
            if (Control != null && !Control.IsInteractable())
                return InteractionState.Rest;
            if (_pressed)
                return InteractionState.Press;
            if (_hovered || _selected)
                return InteractionState.Hover;
            return InteractionState.Rest;
        }

        private void GetPose(
            InteractionState state,
            out Vector2 position,
            out Vector3 scale,
            out Vector3 eulerAngles,
            out float alpha)
        {
            if (state == InteractionState.Press)
            {
                position = _restPosition + PressOffset;
                scale = _restScale * PressScale;
                eulerAngles = _restEulerAngles + PressEulerDelta;
                alpha = PressAlpha;
                return;
            }
            if (state == InteractionState.Hover)
            {
                position = _restPosition + HoverOffset;
                scale = _restScale * HoverScale;
                eulerAngles = _restEulerAngles + HoverEulerDelta;
                alpha = HoverAlpha;
                return;
            }
            position = _restPosition;
            scale = _restScale;
            eulerAngles = _restEulerAngles;
            alpha = _restAlpha;
        }

        private void ApplyPose(InteractionState state)
        {
            if (Target == null)
                return;
            Vector2 position;
            Vector3 scale;
            Vector3 eulerAngles;
            float alpha;
            GetPose(state, out position, out scale, out eulerAngles, out alpha);
            Target.anchoredPosition = position;
            Target.localScale = scale;
            Target.localEulerAngles = eulerAngles;
            if (_canvasGroup != null)
                _canvasGroup.alpha = alpha;
        }

        private void StopTransition()
        {
            _transitionVersion = unchecked(_transitionVersion + 1);
            Tween transition = _transition;
            _transition = null;
            SafeTween.Kill(transition);
        }
    }
}
