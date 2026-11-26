using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.Progression;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiOverlayService : IDisposable
    {
        private readonly UiFrontendServices _services;
        private readonly Dictionary<string, ItemDefinition> _itemsByName =
            new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private UiToast _toast;
        private UiTooltip _tooltip;
        private UiModal _modal;
        private UiTradeFeedback _feedback;
        private bool _disposed;

        internal UiOverlayService(UiFrontendServices services)
        {
            _services = services;
            services.Overlays = this;
            _services.Motion.ReducedMotionChanged += OnReducedMotionChanged;
        }

        internal RectTransform CurrentRoot { get; private set; }
        internal bool HasModal { get { return _modal != null && _modal.IsOpen; } }

        internal void Attach(RectTransform root)
        {
            if (_disposed || root == null)
                return;
            if (CurrentRoot != root)
            {
                if (_tooltip != null)
                    _tooltip.Hide();
                if (_toast != null)
                    _toast.Clear();
                if (_feedback != null)
                    _feedback.Clear();
                CurrentRoot = root;
            }
        }

        internal void Detach(RectTransform root)
        {
            if (CurrentRoot != root)
                return;
            if (_tooltip != null)
                _tooltip.Hide();
            if (_toast != null)
                _toast.Clear();
            if (_feedback != null)
                _feedback.Clear();
            CurrentRoot = null;
        }

        internal void ShowToast(string message, float duration = 2.8f, UiToastKind kind = UiToastKind.Neutral)
        {
            if (_disposed || CurrentRoot == null)
                return;
            if (_toast == null)
                _toast = new UiToast(_services);
            _toast.Show(CurrentRoot, message, duration, kind);
        }

        internal void ShowModal(UiModalContent content)
        {
            if (_disposed || content == null)
                return;
            CloseModal();
            _modal = new UiModal(_services, content);
            _modal.Closed += OnModalClosed;
        }

        internal void CloseModal()
        {
            if (_modal != null)
                _modal.Cancel();
        }

        internal void AttachTooltip(
            GameObject target,
            string title,
            string body,
            string hint = "",
            bool resolveItemFromHierarchy = true)
        {
            AttachTooltip(target, () => title, () => body, () => hint, resolveItemFromHierarchy);
        }

        internal void AttachTooltip(
            GameObject target,
            Func<string> title,
            Func<string> body,
            Func<string> hint,
            bool resolveItemFromHierarchy = true)
        {
            if (_disposed || target == null)
                return;
            if (_tooltip == null)
                _tooltip = new UiTooltip(_services, this);
            _tooltip.Attach(target, title, body, hint, resolveItemFromHierarchy);
        }

        internal void RegisterItems(ItemCatalog catalog)
        {
            _itemsByName.Clear();
            if (catalog == null || catalog.Items == null)
                return;
            for (int i = 0; i < catalog.Items.Count; i++)
            {
                ItemDefinition item = catalog.Items[i];
                if (item != null && !string.IsNullOrEmpty(item.Name))
                    _itemsByName[item.Name] = item;
            }
        }

        internal void TryResolveItemTooltip(Transform target, ref string title, ref string body, ref string hint)
        {
            if (target == null || _itemsByName.Count == 0)
                return;
            Transform nameTransform = target.Find("NameText");
            if (nameTransform == null)
                nameTransform = target.Find("FilledContent/NameText");
            Text nameText = nameTransform == null ? null : nameTransform.GetComponent<Text>();
            if (nameText == null || string.IsNullOrEmpty(nameText.text))
                return;
            string itemName = nameText.text;
            const string delistedPrefix = "WASTED · ";
            if (itemName.StartsWith(delistedPrefix, StringComparison.Ordinal))
                itemName = itemName.Substring(delistedPrefix.Length);
            ItemDefinition item;
            if (!_itemsByName.TryGetValue(itemName, out item))
                return;
            title = item.Name;
            body = item.Description ?? string.Empty;
            string itemKey = "mod:" + item.ItemId;
            hint = string.IsNullOrEmpty(hint) ? itemKey : hint + " · " + itemKey;
        }

        internal void ShowTradeFeedback(IList<Sprite> itemIcons, bool closing)
        {
            if (_disposed || CurrentRoot == null)
                return;
            try
            {
                if (_feedback == null)
                    _feedback = new UiTradeFeedback(_services);
                _feedback.Play(CurrentRoot, itemIcons, closing);
            }
            catch (Exception exception)
            {
                Action<string> logger = SafeTween.LogWarning;
                if (logger != null)
                {
                    try
                    {
                        logger("[XingShangMod.UI] Trade feedback unavailable: " + exception.Message);
                    }
                    catch
                    {
                    }
                }
            }
        }

        internal void PlayAddToCart(RectTransform source, RectTransform cartTarget, Sprite itemIcon)
        {
            if (_disposed || CurrentRoot == null || source == null)
                return;
            try
            {
                if (_feedback == null)
                    _feedback = new UiTradeFeedback(_services);
                _feedback.PlayAddToCart(CurrentRoot, source, cartTarget, itemIcon);
            }
            catch (Exception exception)
            {
                Action<string> logger = SafeTween.LogWarning;
                if (logger != null)
                {
                    try
                    {
                        logger("[XingShangMod.UI] Add-to-cart feedback unavailable: " + exception.Message);
                    }
                    catch
                    {
                    }
                }
            }
        }

        internal void CloseAll()
        {
            CloseModal();
            if (_toast != null)
                _toast.Clear();
            if (_tooltip != null)
                _tooltip.Hide();
            if (_feedback != null)
                _feedback.Clear();
            CurrentRoot = null;
        }

        private void OnReducedMotionChanged(bool reduced)
        {
            if (_disposed || !reduced)
                return;
            if (_modal != null)
                _modal.SettleForReducedMotion();
            if (_toast != null)
                _toast.SettleForReducedMotion();
            if (_tooltip != null)
                _tooltip.SettleForReducedMotion();
            if (_feedback != null)
                _feedback.SettleForReducedMotion();
        }

        private void OnModalClosed()
        {
            if (_modal != null)
                _modal.Closed -= OnModalClosed;
            _modal = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _services.Motion.ReducedMotionChanged -= OnReducedMotionChanged;
            CloseAll();
            if (_toast != null)
                _toast.Dispose();
            if (_tooltip != null)
                _tooltip.Dispose();
            if (_feedback != null)
                _feedback.Dispose();
            _toast = null;
            _tooltip = null;
            _feedback = null;
            _modal = null;
            _itemsByName.Clear();
        }
    }
}
