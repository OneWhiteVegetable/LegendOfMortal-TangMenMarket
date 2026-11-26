using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiModalLine
    {
        internal string Label;
        internal string Quantity;
        internal string Amount;
        internal Color? Accent;
    }

    internal sealed class UiModalContent
    {
        internal string Title = "确认";
        internal string Subtitle = string.Empty;
        internal string Explanation = string.Empty;
        internal string TotalCaption = "合计";
        internal string Total = string.Empty;
        internal string ConfirmLabel = "确认";
        internal string CancelLabel = "取消";
        internal readonly List<UiModalLine> Lines = new List<UiModalLine>();
        internal Action Confirmed;
    }

    internal sealed class UiModal : IUiCancelLayer, IDisposable
    {
        private const float DialogWidth = 600f;
        private const float MaximumHeightFraction = 0.88f;

        private readonly UiFrontendServices _services;
        private readonly UiPrefabInstance _view;
        private readonly List<RectTransform> _lines = new List<RectTransform>();
        private readonly RectTransform _motion;
        private readonly RectTransform _bodyContent;
        private readonly RectTransform _explainPanel;
        private readonly CanvasGroup _backdropGroup;
        private readonly CanvasGroup _dialogGroup;
        private readonly LayoutElement _bodyLayout;
        private readonly LayoutElement _headerLayout;
        private readonly LayoutElement _footerLayout;
        private readonly VerticalLayoutGroup _dialogLayout;
        private readonly GameObject _previousSelected;
        private Action _confirmed;
        private bool _closed;
        private bool _layoutDirty = true;

        internal UiModal(UiFrontendServices services, UiModalContent content)
        {
            _services = services;
            _view = services.Assets.Create("Modal");
            Canvas canvas = _view.Root.GetComponent<Canvas>();
            if (canvas == null)
                throw new InvalidOperationException("Modal requires a root Canvas.");
            UiCanvasDensity.Apply(_view.Root);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = UiSorting.Modal;
            GraphicRaycaster raycaster = _view.Root.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
                raycaster = _view.Root.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = true;

            _motion = _view.Rect("DialogMotion");
            _bodyContent = _view.Get<VerticalLayoutGroup>("BodyContent").GetComponent<RectTransform>();
            _explainPanel = _view.Rect("ExplainPanel");
            _backdropGroup = _view.Get<CanvasGroup>("BackdropGroup");
            _dialogGroup = _view.Get<CanvasGroup>("DialogGroup");
            _bodyLayout = _view.Scroll("BodyScroll").GetComponent<LayoutElement>();
            _dialogLayout = _view.Rect("Dialog").GetComponent<VerticalLayoutGroup>();
            _headerLayout = _view.Rect("Dialog").Find("Header").GetComponent<LayoutElement>();
            _footerLayout = _view.Rect("Dialog").Find("Footer").GetComponent<LayoutElement>();
            _previousSelected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;

            Button blocker = _view.Button("Blocker");
            blocker.interactable = true;
            Image blockerImage = blocker.GetComponent<Image>();
            blockerImage.raycastTarget = true;
            blocker.targetGraphic = blockerImage;
            blocker.onClick.AddListener(Cancel);
            UiCloseInk.Attach(_view.Button("CloseButton"));
            _view.Button("CloseButton").onClick.AddListener(Cancel);
            _view.Button("CancelButton").onClick.AddListener(Cancel);
            _view.Button("ConfirmButton").onClick.AddListener(Confirm);
            foreach (string key in new[] { "CloseButton", "ConfirmButton", "CancelButton" })
            {
                Button button = _view.Button(key);
                if (button.transition == Selectable.Transition.ColorTint)
                {
                    ColorBlock colors = button.colors;
                    colors.selectedColor = colors.highlightedColor;
                    button.colors = colors;
                }
                button.gameObject.AddComponent<UiModalButtonMotion>().Configure(services.Motion);
            }
            ConfigureNavigation();

            _view.Text("TitleText").text = content.Title ?? string.Empty;
            _view.Text("SubtitleText").text = content.Subtitle ?? string.Empty;
            _view.Text("ExplainText").text = content.Explanation ?? string.Empty;
            _explainPanel.gameObject.SetActive(!string.IsNullOrEmpty(content.Explanation));
            _view.Text("TotalCaptionText").text = content.TotalCaption ?? string.Empty;
            _view.Text("TotalText").text = content.Total ?? string.Empty;
            UiView.SetLabel(_view.Button("ConfirmButton"), content.ConfirmLabel);
            UiView.SetLabel(_view.Button("CancelButton"), content.CancelLabel);
            _confirmed = content.Confirmed;
            BindLines(content.Lines);

            UiModalDriver driver = _view.Root.gameObject.AddComponent<UiModalDriver>();
            driver.Owner = this;
            _view.SetActive(true);
            ApplyLayout(true);
            _view.Scroll("BodyScroll").verticalNormalizedPosition = 1f;
            _services.Input.Activate(this);
            PlayOpenMotion();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                UiFocusOrigin.Restore(EventSystem.current, _view.Button("CancelButton").gameObject);
            }
        }

        internal bool IsOpen { get { return !_closed && _view.IsAlive && _view.GameObject.activeSelf; } }
        public bool IsCancelLayerActive { get { return IsOpen; } }
        internal event Action Closed;

        public bool CancelFromInput()
        {
            Cancel();
            return true;
        }

        internal void Cancel()
        {
            Close();
        }

        private void Confirm()
        {
            if (_closed)
                return;
            Action callback = _confirmed;
            _confirmed = null;
            Close();
            if (callback != null)
                callback();
        }

        private void Close()
        {
            if (_closed)
                return;
            _closed = true;
            Action closed = Closed;
            Dispose();
            RestoreSelection();
            if (closed != null)
                closed();
        }

        private void ConfigureNavigation()
        {
            Button cancel = _view.Button("CancelButton");
            Button confirm = _view.Button("ConfirmButton");
            Button close = _view.Button("CloseButton");
            Navigation cancelNavigation = cancel.navigation;
            cancelNavigation.mode = Navigation.Mode.Explicit;
            cancelNavigation.selectOnRight = confirm;
            cancelNavigation.selectOnUp = close;
            cancel.navigation = cancelNavigation;
            Navigation confirmNavigation = confirm.navigation;
            confirmNavigation.mode = Navigation.Mode.Explicit;
            confirmNavigation.selectOnLeft = cancel;
            confirmNavigation.selectOnUp = close;
            confirm.navigation = confirmNavigation;
            Navigation closeNavigation = close.navigation;
            closeNavigation.mode = Navigation.Mode.Explicit;
            closeNavigation.selectOnDown = cancel;
            close.navigation = closeNavigation;
        }

        private void PlayOpenMotion()
        {
            _backdropGroup.alpha = 0f;
            _dialogGroup.alpha = 0f;
            if (_services.Motion.ReducedMotion)
            {
                SetOpenFinalState();
                return;
            }
            _motion.anchoredPosition = new Vector2(0f, -30f);
            _motion.localRotation = Quaternion.Euler(0f, 0f, 2f);
            _motion.localScale = Vector3.one * 0.96f;
            try
            {
                _backdropGroup.DOFade(1f, 0.18f).SetUpdate(true).SetEase(UiMotionEasing.CssEaseOut).SetId(_backdropGroup.gameObject);
                Sequence sequence = DOTween.Sequence();
                sequence.SetId(_motion.gameObject).SetUpdate(true);
                sequence.Join(_dialogGroup.DOFade(1f, 0.28f).SetEase(UiMotionEasing.PaperOut));
                sequence.Join(_motion.DOAnchorPos(Vector2.zero, 0.28f).SetEase(UiMotionEasing.PaperOut));
                sequence.Join(_motion.DORotate(Vector3.zero, 0.28f).SetEase(UiMotionEasing.PaperOut));
                sequence.Join(_motion.DOScale(Vector3.one, 0.28f).SetEase(UiMotionEasing.PaperOut));
            }
            catch (Exception)
            {
                SetOpenFinalState();
            }
        }

        internal void SettleForReducedMotion()
        {
            if (_closed || !_view.IsAlive)
                return;
            _services.Motion.Kill(_backdropGroup.gameObject);
            _services.Motion.Kill(_motion.gameObject);
            SetOpenFinalState();
        }

        private void SetOpenFinalState()
        {
            _backdropGroup.alpha = 1f;
            _dialogGroup.alpha = 1f;
            _motion.anchoredPosition = Vector2.zero;
            _motion.localRotation = Quaternion.identity;
            _motion.localScale = Vector3.one;
        }

        private void BindLines(IList<UiModalLine> models)
        {
            RectTransform template = _view.Rect("LineTemplate");
            _lines.Add(template);
            while (_lines.Count < models.Count)
            {
                GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, _bodyContent, false);
                clone.name = "ContractLine" + _lines.Count;
                _lines.Add(clone.GetComponent<RectTransform>());
            }
            for (int i = 0; i < _lines.Count; i++)
            {
                bool active = i < models.Count;
                _lines[i].gameObject.SetActive(active);
                if (!active)
                    continue;
                UiModalLine model = models[i] ?? new UiModalLine();
                SetLineText(_lines[i], "Label", model.Label);
                SetLineText(_lines[i], "Quantity", model.Quantity);
                SetLineText(_lines[i], "Amount", model.Amount);
                Transform label = _lines[i].Find("Label");
                Text text = label == null ? null : label.GetComponent<Text>();
                if (text != null)
                    text.color = model.Accent ?? new Color32(32, 35, 33, 255);
            }
            _explainPanel.SetAsLastSibling();
            _layoutDirty = true;
        }

        internal void RequestLayout()
        {
            _layoutDirty = true;
        }

        internal void ApplyLayout(bool force)
        {
            if (_closed || (!_layoutDirty && !force))
                return;
            _layoutDirty = false;
            UiCanvasDensity.Apply(_view.Root);
            Rect rootRect = _view.Root.rect;
            if (rootRect.width < 1f || rootRect.height < 1f)
            {
                _layoutDirty = true;
                return;
            }
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Min(DialogWidth, Mathf.Max(280f, rootRect.width - 48f));
            float maximumHeight = Mathf.Max(1f, rootRect.height * MaximumHeightFraction);
            float contentHeight = Mathf.Max(36f, LayoutUtility.GetPreferredHeight(_bodyContent));
            float chromeHeight = _dialogLayout.padding.top + _dialogLayout.padding.bottom +
                _headerLayout.preferredHeight + _footerLayout.preferredHeight;
            float height = Mathf.Min(maximumHeight, chromeHeight + contentHeight);
            if (Mathf.Abs(_motion.sizeDelta.x - width) > 0.1f || Mathf.Abs(_motion.sizeDelta.y - height) > 0.1f)
                _motion.sizeDelta = new Vector2(width, height);
            _bodyLayout.preferredHeight = contentHeight;
            Canvas.ForceUpdateCanvases();
        }

        private static void SetLineText(RectTransform line, string path, string value)
        {
            Transform target = line.Find(path);
            Text text = target == null ? null : target.GetComponent<Text>();
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private void RestoreSelection()
        {
            if (EventSystem.current != null && _previousSelected != null && _previousSelected.activeInHierarchy)
                UiFocusOrigin.Restore(EventSystem.current, _previousSelected);
        }

        public void Dispose()
        {
            if (_closed && !_view.IsAlive)
                return;
            _closed = true;
            _services.Input.Unregister(this);
            if (_view.IsAlive)
            {
                _services.Motion.Kill(_backdropGroup.gameObject);
                _services.Motion.Kill(_motion.gameObject);
                UiModalDriver driver = _view.Root.GetComponent<UiModalDriver>();
                if (driver != null)
                    driver.Owner = null;
            }
            _view.Dispose();
            _confirmed = null;
            _lines.Clear();
        }
    }

    internal sealed class UiModalDriver : MonoBehaviour
    {
        internal UiModal Owner;

        private void LateUpdate()
        {
            if (Owner != null)
                Owner.ApplyLayout(false);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (Owner != null)
                Owner.RequestLayout();
        }

        private void OnDestroy()
        {
            Owner = null;
        }
    }
}
