using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiTradeFeedback : IDisposable
    {
        private const int MaximumTokens = 8;
        private const int FlySortingOrder = 30950;
        private const int StampSortingOrder = 31300;
        private readonly UiFrontendServices _services;
        private readonly List<UiPrefabInstance> _tokens = new List<UiPrefabInstance>();
        private readonly Dictionary<UiPrefabInstance, Tween> _tokenTweens =
            new Dictionary<UiPrefabInstance, Tween>();
        private UiPrefabInstance _stamp;
        private Tween _stampTween;
        private bool _disposed;

        internal UiTradeFeedback(UiFrontendServices services)
        {
            _services = services;
        }

        internal void Play(RectTransform root, IList<Sprite> itemIcons, bool closing)
        {
            if (_disposed || root == null)
                return;
            PlayStamp(root, closing);
        }

        internal void PlayAddToCart(
            RectTransform root,
            RectTransform source,
            RectTransform cartTarget,
            Sprite itemIcon)
        {
            if (_disposed || root == null || source == null || _services.Motion.ReducedMotion ||
                !source.gameObject.activeInHierarchy)
                return;
            RectTransform target = cartTarget == null ? FindFlightTarget(root) : cartTarget;
            if (target == null || !target.gameObject.activeInHierarchy)
                return;
            PlayToken(root, source, target, itemIcon);
        }

        private void PlayToken(
            RectTransform parent,
            RectTransform source,
            RectTransform target,
            Sprite itemIcon)
        {
            UiPrefabInstance token = null;
            for (int i = 0; i < _tokens.Count; i++)
            {
                if (_tokens[i].IsAlive && !_tokens[i].GameObject.activeSelf)
                {
                    token = _tokens[i];
                    break;
                }
            }
            if (token == null)
            {
                if (_tokens.Count >= MaximumTokens)
                    return;
                token = _services.Assets.Create("FlyToken");
                _tokens.Add(token);
                Prepare(token, FlySortingOrder);
            }
            CancelTokenTween(token);
            token.Root.SetParent(parent, false);
            token.Root.SetAsLastSibling();
            token.Root.anchorMin = new Vector2(0.5f, 0.5f);
            token.Root.anchorMax = new Vector2(0.5f, 0.5f);
            token.Root.pivot = new Vector2(0.5f, 0.5f);
            token.Root.localScale = Vector3.one;
            token.Root.localRotation = Quaternion.identity;
            Vector2 tokenSize = UiMotionCoordinates.CartTokenSize(parent, target);
            Vector2 tokenScale = new Vector2(tokenSize.x / 55f, tokenSize.y / 55f);
            token.Root.sizeDelta = tokenSize;
            Image itemImage = token.Image("TokenImage");
            itemImage.sprite = itemIcon == null ? _services.Assets.GetSprite("icon_scroll") : itemIcon;
            itemImage.rectTransform.sizeDelta = new Vector2(45f * tokenScale.x, 45f * tokenScale.y);
            RectTransform shadow = token.Root.Find("Shadow") as RectTransform;
            if (shadow != null)
            {
                shadow.offsetMin = new Vector2(3f * tokenScale.x, -3f * tokenScale.y);
                shadow.offsetMax = shadow.offsetMin;
            }
            Outline outline = token.Root.Find("Background").GetComponent<Outline>();
            if (outline != null)
                outline.effectDistance = new Vector2(2f * tokenScale.x, -2f * tokenScale.y);
            CanvasGroup group = token.Get<CanvasGroup>("CanvasGroup");
            group.alpha = 1f;
            Vector2 start;
            Vector2 end;
            if (!UiMotionCoordinates.TryGetCartFlight(
                parent, source, target, token.Root.rect.size, out start, out end))
            {
                token.SetActive(false);
                return;
            }
            token.Root.anchoredPosition = start;
            token.SetActive(true);
            try
            {
                Tween tween = null;
                tween = DOVirtual.Float(0f, 1f, 0.36f, delegate(float value)
                {
                    if (!token.IsAlive)
                        return;
                    float progress;
                    float rotation;
                    float scale;
                    float alpha;
                    UiMotionKeyframes.EvaluateToken(value, out progress, out rotation, out scale, out alpha);
                    token.Root.anchoredPosition = Vector2.LerpUnclamped(start, end, progress);
                    token.Root.localRotation = Quaternion.Euler(0f, 0f, rotation);
                    token.Root.localScale = Vector3.one * scale;
                    group.alpha = alpha;
                }).SetEase(Ease.Linear).SetUpdate(true).OnComplete(delegate
                {
                    if (token.IsAlive)
                        token.SetActive(false);
                }).OnKill(delegate { ForgetTokenTween(token, tween); });
                _tokenTweens[token] = tween;
            }
            catch (Exception)
            {
                CancelTokenTween(token);
                token.SetActive(false);
            }
        }

        private void PlayStamp(RectTransform parent, bool closing)
        {
            if (_stamp == null)
            {
                _stamp = _services.Assets.Create("DealStamp");
                Prepare(_stamp, StampSortingOrder);
            }
            SafeTween.Kill(_stampTween);
            _stampTween = null;
            _stamp.Root.SetParent(parent, false);
            _stamp.Root.SetAsLastSibling();
            _stamp.Root.anchorMin = new Vector2(0.5f, 0.5f);
            _stamp.Root.anchorMax = new Vector2(0.5f, 0.5f);
            _stamp.Root.pivot = new Vector2(0.5f, 0.5f);
            _stamp.Root.anchoredPosition = Vector2.zero;
            _stamp.Image("StampImage").sprite = _services.Assets.GetSprite(closing ? "stamp_deal_pingcang" : "stamp_deal_chengjiao");
            CanvasGroup group = _stamp.Get<CanvasGroup>("CanvasGroup");
            if (_services.Motion.ReducedMotion)
            {
                group.alpha = 0f;
                _stamp.SetActive(false);
                return;
            }
            _stamp.Root.localScale = Vector3.one * 2f;
            _stamp.Root.localRotation = Quaternion.Euler(0f, 0f, 18f);
            group.alpha = 0f;
            _stamp.SetActive(true);
            try
            {
                _stampTween = DOVirtual.Float(0f, 1f, 0.65f, delegate(float value)
                {
                    if (!_stamp.IsAlive)
                        return;
                    float scale;
                    float rotation;
                    float alpha;
                    UiMotionKeyframes.EvaluateStamp(value, out scale, out rotation, out alpha);
                    _stamp.Root.localScale = Vector3.one * scale;
                    _stamp.Root.localRotation = Quaternion.Euler(0f, 0f, rotation);
                    group.alpha = alpha;
                }).SetEase(Ease.Linear).SetUpdate(true).OnComplete(delegate
                {
                    if (_stamp.IsAlive)
                        _stamp.SetActive(false);
                });
            }
            catch (Exception)
            {
                _stamp.SetActive(false);
            }
        }

        private static RectTransform FindFlightTarget(RectTransform root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            RectTransform fallback = null;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;
                RectTransform target = candidate as RectTransform;
                if (target == null)
                    continue;
                if (candidate.name == "CartRail")
                    return target;
                if (fallback == null && candidate.name == "BrandBlock")
                    fallback = target;
            }
            return fallback;
        }

        private static void Prepare(UiPrefabInstance view, int sortingOrder)
        {
            Canvas canvas = view.Root.GetComponent<Canvas>();
            if (canvas == null)
                canvas = view.Root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            Graphic[] graphics = view.Root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        internal void SettleForReducedMotion()
        {
            if (!_disposed)
                Clear();
        }

        internal void Clear()
        {
            for (int i = 0; i < _tokens.Count; i++)
            {
                if (!_tokens[i].IsAlive)
                    continue;
                CancelTokenTween(_tokens[i]);
                _tokens[i].SetActive(false);
            }
            SafeTween.Kill(_stampTween);
            _stampTween = null;
            if (_stamp != null && _stamp.IsAlive)
                _stamp.SetActive(false);
        }

        private void CancelTokenTween(UiPrefabInstance token)
        {
            Tween tween;
            if (!_tokenTweens.TryGetValue(token, out tween))
                return;
            _tokenTweens.Remove(token);
            SafeTween.Kill(tween);
        }

        private void ForgetTokenTween(UiPrefabInstance token, Tween tween)
        {
            Tween current;
            if (_tokenTweens.TryGetValue(token, out current) && ReferenceEquals(current, tween))
                _tokenTweens.Remove(token);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            Clear();
            _disposed = true;
            for (int i = 0; i < _tokens.Count; i++)
                _tokens[i].Dispose();
            _tokens.Clear();
            _tokenTweens.Clear();
            if (_stamp != null)
                _stamp.Dispose();
            _stamp = null;
        }
    }
}
