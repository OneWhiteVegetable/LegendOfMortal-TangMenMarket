using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    internal enum UiToastKind
    {
        Neutral,
        Success,
        Error
    }

    internal sealed class UiToast : IDisposable
    {
        private const int MaximumVisible = 4;
        private const float StackGap = 8f;
        private const float BottomOffset = 46f;
        private const float MaximumTextWidth = 440f;

        private readonly UiFrontendServices _services;
        private readonly List<ToastEntry> _entries = new List<ToastEntry>();
        private RectTransform _stack;
        private bool _disposed;

        internal UiToast(UiFrontendServices services)
        {
            _services = services;
        }

        internal void Show(Transform parent, string message, float duration, UiToastKind kind)
        {
            if (_disposed || parent == null || string.IsNullOrEmpty(message))
                return;
            EnsureStack(parent);
            while (_entries.Count >= MaximumVisible)
            {
                ToastEntry oldest = _entries[0];
                _entries.RemoveAt(0);
                oldest.Dispose();
            }
            ToastEntry entry = new ToastEntry(_services, this, _stack, message, Mathf.Max(0.4f, duration), kind);
            _entries.Add(entry);
            Reflow(true, entry);
            entry.PlayEnter();
        }

        private void EnsureStack(Transform parent)
        {
            if (_stack != null && _stack.parent == parent)
            {
                _stack.gameObject.SetActive(true);
                _stack.SetAsLastSibling();
                return;
            }
            Clear();
            if (_stack != null)
                UnityEngine.Object.Destroy(_stack.gameObject);
            GameObject stackObject = new GameObject("XingShangMod.ToastStack", typeof(RectTransform), typeof(Canvas));
            _stack = stackObject.GetComponent<RectTransform>();
            _stack.SetParent(parent, false);
            _stack.anchorMin = Vector2.zero;
            _stack.anchorMax = Vector2.one;
            _stack.pivot = new Vector2(0.5f, 0.5f);
            _stack.offsetMin = Vector2.zero;
            _stack.offsetMax = Vector2.zero;
            Canvas canvas = stackObject.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 31200;
            _stack.SetAsLastSibling();
        }

        private void Reflow(bool animate, ToastEntry entering = null)
        {
            float y = BottomOffset;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                ToastEntry entry = _entries[i];
                entry.SetTarget(y, animate && !ReferenceEquals(entry, entering));
                y += entry.Height + StackGap;
            }
        }

        private void Complete(ToastEntry entry)
        {
            if (_disposed || !_entries.Remove(entry))
                return;
            entry.Dispose();
            Reflow(true);
            if (_entries.Count == 0 && _stack != null)
                _stack.gameObject.SetActive(false);
        }

        internal void SettleForReducedMotion()
        {
            if (_disposed)
                return;
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].SettleForReducedMotion();
        }

        internal void Clear()
        {
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].Dispose();
            _entries.Clear();
            if (_stack != null)
                _stack.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            Clear();
            _disposed = true;
            if (_stack != null)
                UnityEngine.Object.Destroy(_stack.gameObject);
            _stack = null;
        }

        private sealed class ToastEntry : IDisposable
        {
            private readonly UiToast _owner;
            private readonly UiPrefabInstance _view;
            private readonly CanvasGroup _group;
            private readonly float _duration;
            private Tween _delay;
            private Tween _move;
            private Tween _fade;
            private Tween _rotate;
            private float _targetY;
            private bool _leaving;
            private bool _disposed;

            internal ToastEntry(
                UiFrontendServices services,
                UiToast owner,
                Transform parent,
                string message,
                float duration,
                UiToastKind kind)
            {
                _owner = owner;
                _duration = duration;
                _view = services.Assets.Create("Toast", parent);
                _group = _view.Get<CanvasGroup>("CanvasGroup");
                _group.blocksRaycasts = false;
                _group.interactable = false;
                Graphic[] graphics = _view.Root.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length; i++)
                    graphics[i].raycastTarget = false;
                _view.Root.anchorMin = new Vector2(0.5f, 0f);
                _view.Root.anchorMax = new Vector2(0.5f, 0f);
                _view.Root.pivot = new Vector2(0.5f, 0f);
                _view.Root.anchoredPosition = new Vector2(0f, BottomOffset);
                _view.Root.localRotation = Quaternion.identity;
                _view.Root.localScale = Vector3.one;
                Text text = _view.Text("MessageText");
                text.text = message;
                Color accent = Accent(kind);
                _view.Image("StatusStripe").color = accent;
                _view.Image("StatusIcon").color = accent;
                _view.Image("StatusIcon").sprite = services.Assets.GetSprite(kind == UiToastKind.Error ? "icon_info" : "icon_check");
                _view.SetActive(true);
                Measure(text);
            }

            internal float Height { get; private set; }

            private void Measure(Text text)
            {
                LayoutElement layout = text.GetComponent<LayoutElement>();
                layout.preferredWidth = -1f;
                layout.preferredHeight = -1f;
                float width = Mathf.Min(MaximumTextWidth, text.preferredWidth);
                layout.preferredWidth = width;
                Canvas.ForceUpdateCanvases();
                layout.preferredHeight = Mathf.Max(18f, text.preferredHeight);
                Canvas.ForceUpdateCanvases();
                Height = Mathf.Max(40f, _view.Root.rect.height);
            }

            internal void SetTarget(float y, bool animate)
            {
                _targetY = y;
                SafeTween.Kill(_move);
                _move = null;
                Vector2 target = new Vector2(0f, y);
                if (!animate || _leaving || _owner._services.Motion.ReducedMotion || _view.Root == null)
                {
                    if (_view.Root != null)
                        _view.Root.anchoredPosition = target;
                    return;
                }
                try
                {
                    _move = _view.Root.DOAnchorPos(target, 0.22f).SetUpdate(true).SetEase(UiMotionEasing.PaperOut);
                }
                catch (Exception)
                {
                    _view.Root.anchoredPosition = target;
                }
            }

            internal void PlayEnter()
            {
                if (_disposed)
                    return;
                _view.Root.anchoredPosition = new Vector2(0f, _targetY - 12f);
                _view.Root.localRotation = Quaternion.Euler(0f, 0f, 1f);
                _group.alpha = 0f;
                ScheduleHide();
                UiMotion motion = _owner._services.Motion;
                if (motion.ReducedMotion)
                {
                    _view.Root.anchoredPosition = new Vector2(0f, _targetY);
                    _view.Root.localRotation = Quaternion.identity;
                    _group.alpha = 1f;
                    return;
                }
                try
                {
                    _move = _view.Root.DOAnchorPos(new Vector2(0f, _targetY), 0.22f).SetUpdate(true).SetEase(UiMotionEasing.PaperOut);
                    _rotate = _view.Root.DORotate(Vector3.zero, 0.22f).SetUpdate(true).SetEase(UiMotionEasing.PaperOut);
                    _fade = _group.DOFade(1f, 0.22f).SetUpdate(true).SetEase(UiMotionEasing.PaperOut);
                }
                catch (Exception)
                {
                    _view.Root.anchoredPosition = new Vector2(0f, _targetY);
                    _view.Root.localRotation = Quaternion.identity;
                    _group.alpha = 1f;
                }
            }

            internal void SettleForReducedMotion()
            {
                if (_disposed)
                    return;
                bool restartStay = _leaving;
                SafeTween.Kill(_move);
                SafeTween.Kill(_rotate);
                SafeTween.Kill(_fade);
                _move = null;
                _rotate = null;
                _fade = null;
                _leaving = false;
                if (_view.Root != null)
                {
                    _view.Root.anchoredPosition = new Vector2(0f, _targetY);
                    _view.Root.localRotation = Quaternion.identity;
                }
                if (_group != null)
                    _group.alpha = 1f;
                if (restartStay)
                    ScheduleHide();
            }

            private void ScheduleHide()
            {
                SafeTween.Kill(_delay);
                _delay = null;
                try
                {
                    _delay = DOVirtual.DelayedCall(_duration, BeginHide, false).SetUpdate(true);
                }
                catch (Exception)
                {
                    _delay = null;
                }
            }

            private void BeginHide()
            {
                if (_disposed || _leaving)
                    return;
                _delay = null;
                _leaving = true;
                SafeTween.Kill(_move);
                SafeTween.Kill(_rotate);
                SafeTween.Kill(_fade);
                _move = null;
                _rotate = null;
                _fade = null;
                if (_owner._services.Motion.ReducedMotion)
                {
                    _group.alpha = 0f;
                    _owner.Complete(this);
                    return;
                }
                try
                {
                    _move = _view.Root.DOAnchorPos(new Vector2(0f, _targetY - 8f), 0.2f).SetUpdate(true).SetEase(UiMotionEasing.CssEase);
                    _fade = _group.DOFade(0f, 0.2f).SetUpdate(true).SetEase(UiMotionEasing.CssEase).OnComplete(delegate { _owner.Complete(this); });
                }
                catch (Exception)
                {
                    _owner.Complete(this);
                }
            }

            private static Color Accent(UiToastKind kind)
            {
                switch (kind)
                {
                    case UiToastKind.Success: return new Color32(142, 196, 168, 255);
                    case UiToastKind.Error: return new Color32(239, 154, 130, 255);
                    default: return new Color32(216, 188, 123, 255);
                }
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                SafeTween.Kill(_delay);
                SafeTween.Kill(_move);
                SafeTween.Kill(_rotate);
                SafeTween.Kill(_fade);
                _delay = null;
                _move = null;
                _rotate = null;
                _fade = null;
                _view.Dispose();
            }
        }
    }
}
