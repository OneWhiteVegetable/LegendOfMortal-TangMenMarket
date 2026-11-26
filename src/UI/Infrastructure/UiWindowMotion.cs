using System;
using DG.Tweening;
using UnityEngine;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiWindowMotion : IDisposable
    {
        private const float OpenDuration = 0.46f;
        private const float CloseDuration = 0.24f;
        private static readonly EaseFunction PaperIn = EvaluatePaperIn;

        private readonly UiMotionScope _scope;
        private readonly RectTransform _pivot;
        private readonly RectTransform _target;
        private readonly CanvasGroup _group;
        private readonly CanvasGroup _targetGroup;
        private readonly Vector2 _homePosition;
        private readonly Vector2 _targetHomePosition;
        private Action _closeCallback;
        private bool _closing;
        private bool _disposed;

        internal UiWindowMotion(
            RectTransform pivot,
            RectTransform target,
            CanvasGroup group,
            CanvasGroup targetGroup,
            UiMotion motion)
        {
            _pivot = pivot;
            _target = target;
            _group = group;
            _targetGroup = targetGroup;
            _scope = motion.CreateScope(pivot);
            _homePosition = pivot.anchoredPosition;
            _targetHomePosition = target.anchoredPosition;
            ApplyOpenPose();
            SetInteraction(false);
        }

        internal void PlayOpen()
        {
            if (_disposed || !HasTargets())
                return;
            if (_closing)
                CompleteClose();
            if (_disposed)
                return;

            _scope.ResetForReuse();
            _closeCallback = null;
            _closing = false;
            SetInteraction(false);
            if (_scope.ReducedMotion)
            {
                ApplyOpenPose();
                return;
            }

            _group.alpha = 0f;
            _targetGroup.alpha = 0f;
            _pivot.anchoredPosition = _homePosition + new Vector2(0f, -28f);
            _pivot.localRotation = Quaternion.Euler(0f, -4f, 0.5f);
            _pivot.localScale = Vector3.one;
            CopyPivotToTarget();
            Sequence sequence = null;
            try
            {
                sequence = DOTween.Sequence();
                if (sequence == null)
                {
                    ApplyOpenPose();
                    return;
                }
                sequence.SetId(_pivot.gameObject);
                sequence.Append(_group.DOFade(1f, OpenDuration).SetEase(UiMotionEasing.PaperOutEase));
                sequence.Join(_targetGroup.DOFade(1f, OpenDuration).SetEase(UiMotionEasing.PaperOutEase));
                sequence.Join(_pivot.DOAnchorPos(_homePosition, OpenDuration).SetEase(UiMotionEasing.PaperOutEase));
                sequence.Join(_pivot.DOLocalRotate(Vector3.zero, OpenDuration).SetEase(UiMotionEasing.PaperOutEase));
                sequence.OnUpdate(CopyPivotToTarget);
                _scope.Track(sequence, ApplyOpenPose);
            }
            catch (Exception)
            {
                SafeTween.Kill(sequence);
                ApplyOpenPose();
            }
        }

        internal void PlayClose(Action completed)
        {
            if (_disposed || !HasTargets())
            {
                if (completed != null)
                    completed();
                return;
            }
            if (_closing)
                return;

            _scope.ResetForReuse();
            _closing = true;
            _closeCallback = completed;
            SetInteraction(false);
            if (_scope.ReducedMotion)
            {
                CompleteClose();
                return;
            }

            Sequence sequence = null;
            try
            {
                sequence = DOTween.Sequence();
                if (sequence == null)
                {
                    CompleteClose();
                    return;
                }
                sequence.SetId(_pivot.gameObject);
                sequence.Append(_group.DOFade(0f, CloseDuration).SetEase(PaperIn));
                sequence.Join(_targetGroup.DOFade(0f, CloseDuration).SetEase(PaperIn));
                sequence.Join(_pivot.DOAnchorPos(_homePosition + new Vector2(-45f, 0f), CloseDuration).SetEase(PaperIn));
                sequence.Join(_pivot.DOLocalRotate(new Vector3(0f, 7f, 0f), CloseDuration).SetEase(PaperIn));
                sequence.OnUpdate(CopyPivotToTarget);
                _scope.Track(sequence, CompleteClose);
            }
            catch (Exception)
            {
                SafeTween.Kill(sequence);
                CompleteClose();
            }
        }

        internal void CompleteClose()
        {
            if (!_closing)
                return;
            _closing = false;
            _scope.ResetForReuse();
            SetInteraction(false);
            if (_group != null)
                _group.alpha = 0f;
            if (_targetGroup != null)
                _targetGroup.alpha = 0f;
            if (_pivot != null)
            {
                _pivot.anchoredPosition = _homePosition + new Vector2(-45f, 0f);
                _pivot.localRotation = Quaternion.Euler(0f, 7f, 0f);
            }
            CopyPivotToTarget();
            Action callback = _closeCallback;
            _closeCallback = null;
            if (callback != null)
                callback();
        }

        internal void Shutdown()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _closing = false;
            _closeCallback = null;
            SetInteraction(false);
            _scope.Dispose();
        }

        private bool HasTargets()
        {
            return _pivot != null && _target != null && _group != null && _targetGroup != null;
        }

        private void ApplyOpenPose()
        {
            if (_disposed)
            {
                SetInteraction(false);
                return;
            }
            SetInteraction(true);
            if (_group != null)
                _group.alpha = 1f;
            if (_targetGroup != null)
                _targetGroup.alpha = 1f;
            if (_pivot != null)
            {
                _pivot.anchoredPosition = _homePosition;
                _pivot.localRotation = Quaternion.identity;
                _pivot.localScale = Vector3.one;
            }
            if (_target != null)
            {
                _target.anchoredPosition = _targetHomePosition;
                _target.localRotation = Quaternion.identity;
                _target.localScale = Vector3.one;
            }
        }

        private void SetInteraction(bool value)
        {
            if (_group != null)
            {
                _group.interactable = value;
                _group.blocksRaycasts = value;
            }
            if (_targetGroup != null)
            {
                _targetGroup.interactable = value;
                _targetGroup.blocksRaycasts = value;
            }
        }

        private void CopyPivotToTarget()
        {
            if (_pivot == null || _target == null)
                return;
            _target.anchoredPosition = _targetHomePosition + (_pivot.anchoredPosition - _homePosition);
            _target.localRotation = _pivot.localRotation;
            _target.localScale = _pivot.localScale;
        }

        private static float EvaluatePaperIn(float time, float duration, float overshootOrAmplitude, float period)
        {
            float normalizedTime = duration <= 0f ? 1f : time / duration;
            return UiMotionEasing.CubicBezier(normalizedTime, 0.7f, 0f, 0.84f, 0f);
        }
    }
}
