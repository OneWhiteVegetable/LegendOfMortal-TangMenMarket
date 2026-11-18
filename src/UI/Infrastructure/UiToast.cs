using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    public sealed class UiToast : MonoBehaviour
    {
        private readonly Queue<ToastMessage> _queue = new Queue<ToastMessage>();
        private Text _text;
        private CanvasGroup _group;
        private Tween _fade;
        private float _remaining;
        private bool _visible;
        private bool _fadingOut;

        public RectTransform Root { get; private set; }
        public int MaxQueuedMessages { get; set; }

        public bool IsVisible
        {
            get { return _visible && Root != null && Root.gameObject.activeSelf; }
        }

        public static UiToast Create(UiFactory factory, Transform parent)
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
                "XingShangMod.Toast",
                "panel_toast_9s",
                new Vector2(640f, 80f));
            panel.raycastTarget = false;
            UiFactory.SetRect(
                panel.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -32f),
                new Vector2(640f, 80f));

            UiToast toast = panel.gameObject.AddComponent<UiToast>();
            toast.Root = panel.rectTransform;
            toast.MaxQueuedMessages = 16;
            toast._group = panel.gameObject.AddComponent<CanvasGroup>();
            toast._group.interactable = false;
            toast._group.blocksRaycasts = false;
            toast._text = factory.CreateText(
                panel.transform,
                "Text",
                string.Empty,
                24,
                new Color(0.18f, 0.13f, 0.09f, 1f),
                TextAnchor.MiddleCenter);
            toast._text.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Stretch(
                toast._text.rectTransform,
                new Vector2(28f, 10f),
                new Vector2(-28f, -10f));
            panel.gameObject.SetActive(false);
            return toast;
        }

        public void Show(string message, float duration = 2.5f)
        {
            if (Root == null || string.IsNullOrEmpty(message))
            {
                return;
            }
            int limit = Mathf.Max(1, MaxQueuedMessages);
            while (_queue.Count >= limit)
            {
                _queue.Dequeue();
            }
            _queue.Enqueue(new ToastMessage(message, Mathf.Max(0.1f, duration)));
            if (!_visible)
            {
                BeginNext();
            }
        }

        public void Clear()
        {
            _queue.Clear();
            SafeTween.Kill(_fade);
            _fade = null;
            _visible = false;
            _fadingOut = false;
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_visible || _fadingOut)
            {
                return;
            }
            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f)
            {
                FadeOut();
            }
        }

        private void BeginNext()
        {
            if (_queue.Count == 0 || Root == null)
            {
                _visible = false;
                return;
            }
            ToastMessage next = _queue.Dequeue();
            _text.text = next.Message;
            _remaining = next.Duration;
            _visible = true;
            _fadingOut = false;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            _group.alpha = 0f;
            FadeTo(1f, 0.12f, null);
        }

        private void FadeOut()
        {
            if (_fadingOut)
            {
                return;
            }
            _fadingOut = true;
            FadeTo(0f, 0.18f, CompleteCurrent);
        }

        private void FadeTo(float alpha, float duration, TweenCallback onComplete)
        {
            SafeTween.Kill(_fade);
            _fade = DOTween.To(
                    () => _group.alpha,
                    value =>
                    {
                        if (_group != null)
                        {
                            _group.alpha = value;
                        }
                    },
                    alpha,
                    duration)
                .SetTarget(Root.gameObject)
                .SetUpdate(true);
            if (onComplete != null)
            {
                _fade.OnComplete(onComplete);
            }
        }

        private void CompleteCurrent()
        {
            _fade = null;
            _visible = false;
            _fadingOut = false;
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
            }
            if (_queue.Count > 0)
            {
                BeginNext();
            }
        }

        private void OnDestroy()
        {
            SafeTween.Kill(_fade);
            _fade = null;
            _queue.Clear();
        }

        private sealed class ToastMessage
        {
            public ToastMessage(string message, float duration)
            {
                Message = message;
                Duration = duration;
            }

            public string Message { get; private set; }
            public float Duration { get; private set; }
        }
    }
}
