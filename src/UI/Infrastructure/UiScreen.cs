using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiFrontendServices
    {
        internal UiFrontendServices(UiAssetBundle assets, UiInputStack input, UiMotion motion)
        {
            Assets = assets;
            Input = input;
            Motion = motion;
        }

        internal UiAssetBundle Assets { get; private set; }
        internal UiInputStack Input { get; private set; }
        internal UiMotion Motion { get; private set; }
        internal UiOverlayService Overlays { get; set; }
    }

    internal static class UiSorting
    {
        internal const int Market = 30000;
        internal const int Detail = 30010;
        internal const int Inventory = 30020;
        internal const int Modal = 31000;
    }

    internal abstract class UiScreen : IUiCancelLayer, IDisposable
    {
        private GameObject _previousSelected;
        private Selectable _lastControl;
        private Action _hiddenCallbacks;
        private UiWindowShell _windowShell;
        private UiWindowMotion _windowMotion;
        private bool _closing;
        private bool _disposed;

        protected UiScreen(UiFrontendServices services, string prefabName, int sortingOrder)
        {
            Services = services;
            View = services.Assets.Create(prefabName);
            Canvas canvas = View.Root.GetComponent<Canvas>();
            if (canvas == null)
                throw new InvalidOperationException(prefabName + " requires a root Canvas.");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            GraphicRaycaster raycaster = View.Root.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
                raycaster = View.Root.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = true;
            View.Image("Blocker").raycastTarget = true;
            CanvasGroup = View.Root.GetComponent<CanvasGroup>();
            if (CanvasGroup == null)
                CanvasGroup = View.Root.gameObject.AddComponent<CanvasGroup>();
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;

            _windowShell = View.Root.gameObject.AddComponent<UiWindowShell>();
            _windowShell.Configure(View.Root, View.Rect("Window"));
            UiMastheadFeedback.Bind(View, services);
            if (prefabName == "InventoryWindow") UiCloseInk.Attach(View.Button("ProfileCloseButton"));
            _windowMotion = new UiWindowMotion(
                _windowShell.MotionPivot,
                _windowShell.MotionTarget,
                _windowShell.MotionGroup,
                _windowShell.WindowGroup,
                Services.Motion);
            View.SetActive(false);
        }

        protected UiFrontendServices Services { get; private set; }
        protected UiPrefabInstance View { get; private set; }
        protected CanvasGroup CanvasGroup { get; private set; }
        internal RectTransform Root { get { return View.Root; } }
        internal bool IsOpen { get { return !_disposed && !_closing && View.IsAlive && View.GameObject.activeSelf; } }
        internal bool IsClosing { get { return !_disposed && _closing; } }
        public bool IsCancelLayerActive { get { return IsOpen; } }

        protected void SetWalletBalance(long? balance)
        {
            Text text = View.Text("MoneyText");
            string formatted = balance.HasValue ? UiFormatting.Money(balance.Value) + " <size=11>文</size>" : "—";
            if (text.text == formatted)
                return;
            text.text = formatted;
            _windowShell.RefreshMoneyLayout();
        }

        protected void SetIndexQuote(double? price, double change)
        {
            // The masthead uses the prototype's vermilion. News on dark paper keeps its lighter ink.
            Color ink = change > 0 ? UiFormatting.Red : UiFormatting.Trend(change, true);
            View.Text("IndexNameText").text = "唐门指数";
            Text value = View.Text("IndexPriceText");
            Text movement = View.Text("IndexChangeText");
            value.text = price.HasValue ? UiFormatting.Number(price.Value, 2) : "—";
            movement.text = price.HasValue ? UiFormatting.Percent(change) : "—";
            value.color = movement.color = ink;
        }

        protected void Open()
        {
            if (_disposed || !View.IsAlive)
                return;
            if (_closing && _windowMotion != null)
                _windowMotion.CompleteClose();
            if (_disposed || !View.IsAlive)
                return;

            bool wasOpen = IsOpen;
            if (!wasOpen && EventSystem.current != null)
                _previousSelected = EventSystem.current.currentSelectedGameObject;
            View.SetActive(true);
            View.Root.SetAsLastSibling();
            CanvasGroup.alpha = 1f;
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;
            _windowShell.ApplyNow();
            Services.Overlays.Attach(View.Root);
            Services.Input.Activate(this);
            if (!wasOpen)
            {
                Services.Motion.Kill(View.GameObject);
                _windowMotion.PlayOpen();
            }
            OnOpened();
            EventSystem events = EventSystem.current;
            if (!wasOpen && events != null)
            {
                // Keep Submit off the underlying game menu. A returning page restores its invoking control.
                GameObject focus = _lastControl != null && _lastControl.isActiveAndEnabled &&
                    _lastControl.interactable ? _lastControl.gameObject : null;
                events.SetSelectedGameObject(UiFocusOrigin.KeyboardActive ? focus : null);
            }
        }

        internal virtual void Hide()
        {
            if (!View.IsAlive || _closing)
                return;
            if (!View.GameObject.activeSelf)
            {
                InvokeHiddenCallbacks();
                return;
            }

            EventSystem events = EventSystem.current;
            GameObject selected = events == null ? null : events.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(Root))
                _lastControl = selected.GetComponent<Selectable>();
            _closing = true;
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
            Services.Input.Unregister(this);
            Services.Overlays.Detach(View.Root);
            RestorePreviousSelection();
            _windowMotion.PlayClose(CompleteHide);
        }

        internal void Hide(Action onHidden)
        {
            if (onHidden == null)
            {
                Hide();
                return;
            }
            if (_disposed || !View.IsAlive)
            {
                onHidden();
                return;
            }
            if (_closing)
            {
                _hiddenCallbacks += onHidden;
                return;
            }
            _hiddenCallbacks += onHidden;
            Hide();
        }

        internal void FocusForNavigation()
        {
            EventSystem events = EventSystem.current;
            if (!IsOpen || events == null)
                return;
            GameObject selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(Root))
                return;
            Selectable focus = _lastControl;
            if (focus == null || !focus.isActiveAndEnabled || !focus.IsInteractable())
                focus = View.Button(View.Name == "ItemDetail" ? "BackButton" :
                    View.Name == "InventoryWindow" ? "InventoryTabButton" : "IndexButton");
            if (focus.isActiveAndEnabled && focus.IsInteractable())
                events.SetSelectedGameObject(focus.gameObject);
        }

        public abstract bool CancelFromInput();

        protected virtual void OnOpened()
        {
        }

        public virtual void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _closing = false;
            _hiddenCallbacks = null;
            Services.Input.Unregister(this);
            if (View.IsAlive)
            {
                Services.Overlays.Detach(View.Root);
                Services.Motion.Kill(View.GameObject);
                if (_windowMotion != null)
                    _windowMotion.Shutdown();
                View.Dispose();
            }
            _previousSelected = null;
        }

        private void CompleteHide()
        {
            if (!_closing)
                return;
            _closing = false;
            if (View.IsAlive)
            {
                Services.Motion.Kill(View.GameObject);
                CanvasGroup.alpha = 1f;
                CanvasGroup.interactable = true;
                CanvasGroup.blocksRaycasts = true;
                View.SetActive(false);
            }
            InvokeHiddenCallbacks();
        }

        private void InvokeHiddenCallbacks()
        {
            Action callbacks = _hiddenCallbacks;
            _hiddenCallbacks = null;
            if (callbacks != null)
                callbacks();
        }

        private void RestorePreviousSelection()
        {
            EventSystem events = EventSystem.current;
            if (events != null)
            {
                if (_previousSelected != null && _previousSelected.activeInHierarchy)
                    UiFocusOrigin.Restore(events, _previousSelected);
                else if (events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(Root))
                    events.SetSelectedGameObject(null);
            }
            _previousSelected = null;
        }
    }
}
