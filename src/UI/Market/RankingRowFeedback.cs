using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Market
{
    internal sealed class RankingRowFeedback : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private UiMotion _motion;
        private RectTransform _motionPivot;
        private GameObject _highlight;
        private Shadow _inkShadow;
        private Vector2 _restPosition;
        private Tween _move;
        private bool _pointerInside;
        private bool _selected;
        private bool _interactable = true;
        private bool _disposed;

        internal void Configure(
            UiMotion motion,
            RectTransform motionPivot,
            GameObject highlight,
            Shadow inkShadow)
        {
            _motion = motion;
            _motionPivot = motionPivot;
            _highlight = highlight;
            _inkShadow = inkShadow;
            _restPosition = motionPivot.anchoredPosition;
            Reset();
        }

        internal void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            Reset();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactable || _disposed)
                return;
            _pointerInside = true;
            ApplyState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_disposed)
                return;
            _pointerInside = false;
            ApplyState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!_interactable || _disposed)
                return;
            _selected = !(eventData is PointerEventData);
            ApplyState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (_disposed)
                return;
            _selected = false;
            ApplyState();
        }

        internal void Reset()
        {
            if (_disposed)
                return;
            _pointerInside = false;
            _selected = false;
            KillMove();
            if (_motionPivot != null)
                _motionPivot.anchoredPosition = _restPosition;
            SetVisualState(false);
        }

        private void ApplyState()
        {
            bool active = _pointerInside || (_selected && UiFocusOrigin.KeyboardActive);
            SetVisualState(active);
            if (_motionPivot == null)
                return;
            KillMove();
            bool reduced = _motion != null && _motion.ReducedMotion;
            Vector2 target = _restPosition;
            if (active && !reduced)
                target += new Vector2(5f, 0f);
            if (reduced)
            {
                _motionPivot.anchoredPosition = target;
                return;
            }
            try
            {
                _move = _motionPivot.DOAnchorPos(target, 0.14f);
                if (_move == null)
                {
                    _motionPivot.anchoredPosition = target;
                    return;
                }
                _move.SetUpdate(true)
                    .SetEase(Ease.OutCubic)
                    .SetId(this)
                    .OnComplete(delegate { _move = null; });
            }
            catch (Exception)
            {
                _move = null;
                _motionPivot.anchoredPosition = target;
            }
        }

        private void SetVisualState(bool active)
        {
            if (_highlight != null)
                _highlight.SetActive(active);
            if (_inkShadow != null)
                _inkShadow.enabled = active;
        }

        private void Update()
        {
            if (_disposed || _motion == null || !_motion.ReducedMotion || _motionPivot == null)
                return;
            if (_move != null || _motionPivot.anchoredPosition != _restPosition)
            {
                KillMove();
                _motionPivot.anchoredPosition = _restPosition;
            }
        }

        private void KillMove()
        {
            _move = null;
            SafeTween.Kill(this);
        }

        private void OnDisable()
        {
            Reset();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            KillMove();
            _disposed = true;
            _pointerInside = false;
            _selected = false;
            SetVisualState(false);
            _motion = null;
            _motionPivot = null;
            _highlight = null;
            _inkShadow = null;
        }
    }
}
