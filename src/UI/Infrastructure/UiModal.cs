using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    public sealed class UiModal : MonoBehaviour
    {
        private CanvasGroup _group;
        private Tween _fade;
        private Tween _scale;
        private Action _onConfirm;
        private Action _onCancel;
        private GameObject _previousSelected;

        public RectTransform Root { get; private set; }
        public RectTransform Panel { get; private set; }
        public Text TitleText { get; private set; }
        public Text MessageText { get; private set; }
        public Button ConfirmButton { get; private set; }
        public Button CancelButton { get; private set; }

        public bool IsOpen
        {
            get { return Root != null && Root.gameObject.activeSelf; }
        }

        public static UiModal Create(UiFactory factory, Transform parent)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }
            if (parent == null)
            {
                throw new ArgumentNullException("parent");
            }

            Image overlay = factory.CreatePanel(
                parent,
                "XingShangMod.Modal",
                null,
                null,
                new Color(0.03f, 0.025f, 0.02f, 0.76f));
            UiFactory.Stretch(overlay.rectTransform);
            UiModal modal = overlay.gameObject.AddComponent<UiModal>();
            modal.Root = overlay.rectTransform;
            modal._group = overlay.gameObject.AddComponent<CanvasGroup>();
            modal._group.interactable = true;
            modal._group.blocksRaycasts = true;

            Image panel = factory.CreatePanel(
                overlay.transform,
                "Panel",
                "panel_contract_modal_9s",
                new Vector2(680f, 420f));
            modal.Panel = panel.rectTransform;
            modal.TitleText = factory.CreateText(
                panel.transform,
                "Title",
                string.Empty,
                30,
                new Color(0.16f, 0.11f, 0.075f, 1f),
                TextAnchor.MiddleCenter);
            UiFactory.SetRect(
                modal.TitleText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(-64f, 56f));

            modal.MessageText = factory.CreateText(
                panel.transform,
                "Message",
                string.Empty,
                24,
                new Color(0.19f, 0.15f, 0.11f, 1f),
                TextAnchor.UpperLeft);
            modal.MessageText.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Stretch(
                modal.MessageText.rectTransform,
                new Vector2(40f, 124f),
                new Vector2(-40f, -96f));

            modal.ConfirmButton = factory.CreateButton(
                panel.transform,
                "ConfirmButton",
                "確定",
                modal.Confirm,
                24,
                new Vector2(200f, 72f),
                "button_modal_primary_normal_9s",
                "button_modal_primary_hover_9s",
                "button_modal_primary_pressed_9s");
            UiFactory.SetRect(
                modal.ConfirmButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-32f, 28f),
                new Vector2(200f, 72f));

            modal.CancelButton = factory.CreateButton(
                panel.transform,
                "CancelButton",
                "取消",
                modal.Cancel,
                24,
                new Vector2(200f, 72f),
                "button_modal_neutral_normal_9s",
                "button_modal_neutral_hover_9s",
                "button_modal_neutral_pressed_9s");
            UiFactory.SetRect(
                modal.CancelButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-244f, 28f),
                new Vector2(200f, 72f));

            overlay.gameObject.SetActive(false);
            return modal;
        }

        public void Show(
            string title,
            string message,
            string confirmText,
            Action onConfirm,
            string cancelText = null,
            Action onCancel = null)
        {
            if (Root == null)
            {
                return;
            }
            bool wasOpen = IsOpen;
            if (!wasOpen && EventSystem.current != null)
            {
                _previousSelected = EventSystem.current.currentSelectedGameObject;
            }

            SafeTween.Kill(_fade);
            SafeTween.Kill(_scale);
            _fade = null;
            _scale = null;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            TitleText.text = title ?? string.Empty;
            MessageText.text = message ?? string.Empty;
            SetButtonLabel(ConfirmButton, string.IsNullOrEmpty(confirmText) ? "確定" : confirmText);
            SetButtonLabel(CancelButton, string.IsNullOrEmpty(cancelText) ? "取消" : cancelText);
            CancelButton.gameObject.SetActive(cancelText != null);

            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            _group.alpha = 0f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            Panel.localScale = new Vector3(0.98f, 0.98f, 1f);
            _fade = DOTween.To(() => _group.alpha, value => _group.alpha = value, 1f, 0.12f)
                .SetTarget(Root.gameObject)
                .SetUpdate(true);
            _scale = DOTween.To(() => Panel.localScale, value => Panel.localScale = value, Vector3.one, 0.12f)
                .SetTarget(Panel.gameObject)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);

            if (EventSystem.current != null)
            {
                GameObject selected = CancelButton.gameObject.activeSelf
                    ? CancelButton.gameObject
                    : ConfirmButton.gameObject;
                EventSystem.current.SetSelectedGameObject(selected);
            }
        }

        public void ShowMessage(string title, string message, string buttonText = "確定", Action onClosed = null)
        {
            Show(title, message, buttonText, onClosed);
        }

        public void Confirm()
        {
            if (!IsOpen)
            {
                return;
            }
            Action callback = _onConfirm;
            Hide();
            if (callback != null)
            {
                callback();
            }
        }

        public void Cancel()
        {
            if (!IsOpen)
            {
                return;
            }
            Action callback = _onCancel;
            Hide();
            if (callback != null)
            {
                callback();
            }
        }

        public void Hide()
        {
            SafeTween.Kill(_fade);
            SafeTween.Kill(_scale);
            _fade = null;
            _scale = null;
            _onConfirm = null;
            _onCancel = null;
            if (Panel != null)
            {
                Panel.localScale = Vector3.one;
            }
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
            }
            if (EventSystem.current != null && _previousSelected != null &&
                _previousSelected.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(_previousSelected);
            }
            _previousSelected = null;
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
            }
        }

        private void OnDestroy()
        {
            SafeTween.Kill(_fade);
            SafeTween.Kill(_scale);
            _fade = null;
            _scale = null;
            _onConfirm = null;
            _onCancel = null;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
