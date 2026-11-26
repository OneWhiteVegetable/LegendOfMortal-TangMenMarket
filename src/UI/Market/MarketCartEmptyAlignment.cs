using System;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketCartEmptyView : IDisposable
    {
        private const float MinimumHeight = 180f;
        private const float HorizontalPadding = 25f;
        private static readonly Color IconColor = new Color32(180, 147, 88, 255);
        private static readonly Color TitleColor = new Color32(216, 207, 186, 255);
        private static readonly Color BodyColor = new Color32(156, 164, 152, 255);

        private readonly UiFrontendServices _services;
        private readonly RectTransform _root;
        private readonly Image _icon;
        private readonly Text _title;
        private readonly Text _body;
        private readonly MarketCartEmptyAlignment _alignment;
        private bool _disposed;

        internal MarketCartEmptyView(
            UiFrontendServices services,
            ScrollRect scroll,
            RectTransform content)
        {
            _services = services;
            _root = CreateRect("CartEmpty", content);
            LayoutElement layout = _root.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = MinimumHeight;
            layout.preferredHeight = MinimumHeight;
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 0f;

            _icon = CreateImage(_root);
            Center(_icon.rectTransform, 46f, 53f, 53f);
            _title = CreateText(
                "TitleText",
                _root,
                services.Assets.SerifFont,
                18,
                TitleColor,
                TextAnchor.MiddleCenter);
            Center(_title.rectTransform, -8.5f, 240f, 24f);
            _body = CreateText(
                "BodyText",
                _root,
                services.Assets.RegularFont,
                11,
                BodyColor,
                TextAnchor.MiddleCenter);
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.lineSpacing = 1.6f;
            Center(_body.rectTransform, -51.5f, 240f, 42f);

            _alignment = _root.gameObject.AddComponent<MarketCartEmptyAlignment>();
            _alignment.Configure(
                scroll,
                content.GetComponent<VerticalLayoutGroup>(),
                layout,
                _body.rectTransform,
                MinimumHeight,
                HorizontalPadding);
            _root.gameObject.SetActive(false);
        }

        internal void Bind(bool visible, bool pending)
        {
            if (_disposed || _root == null)
                return;
            if (visible)
            {
                _icon.sprite = _services.Assets.GetSprite(pending ? "icon_scroll" : "icon_check");
                _icon.color = IconColor;
                _title.text = pending ? "货签尚空" : "尚无已结契约";
                _body.text = pending
                    ? "点货品的契印，纳入此匣。\n红为买货，绿为借货。"
                    : "结算后的买卖与借还记录\n会归入这册账簿。";
            }
            _root.gameObject.SetActive(visible);
            if (visible)
            {
                _root.SetAsLastSibling();
                _alignment.RefreshLayout();
            }
        }

        internal void RefreshLayout()
        {
            if (!_disposed && _root != null && _root.gameObject.activeSelf)
                _alignment.RefreshLayout();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_root.gameObject);
            }
        }

        private static Image CreateImage(RectTransform parent)
        {
            RectTransform rect = CreateRect("EmptyIcon", parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            rect.localRotation = Quaternion.Euler(0f, 0f, 5f);
            return image;
        }

        private static Text CreateText(
            string name,
            RectTransform parent,
            Font font,
            int fontSize,
            Color color,
            TextAnchor alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.lineSpacing = 1f;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static void Center(RectTransform rect, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }

    internal sealed class MarketCartEmptyAlignment : MonoBehaviour
    {
        private ScrollRect _scroll;
        private VerticalLayoutGroup _contentLayout;
        private LayoutElement _layout;
        private RectTransform _body;
        private RectTransform _root;
        private float _minimumHeight;
        private float _horizontalPadding;
        private float _appliedHeight = -1f;
        private float _appliedWidth = -1f;

        internal void Configure(
            ScrollRect scroll,
            VerticalLayoutGroup contentLayout,
            LayoutElement layout,
            RectTransform body,
            float minimumHeight,
            float horizontalPadding)
        {
            _scroll = scroll;
            _contentLayout = contentLayout;
            _layout = layout;
            _body = body;
            _root = transform as RectTransform;
            _minimumHeight = minimumHeight;
            _horizontalPadding = horizontalPadding;
            RefreshLayout();
        }

        internal void RefreshLayout()
        {
            if (_scroll == null || _layout == null || _body == null || _root == null)
                return;
            RectTransform viewport = _scroll.viewport == null
                ? _scroll.GetComponent<RectTransform>()
                : _scroll.viewport;
            if (viewport == null)
                return;
            int verticalPadding = _contentLayout == null ? 0 : _contentLayout.padding.vertical;
            int horizontalPadding = _contentLayout == null ? 0 : _contentLayout.padding.horizontal;
            float height = Mathf.Max(_minimumHeight, viewport.rect.height - verticalPadding);
            float rootWidth = _root.rect.width;
            if (rootWidth <= 0f)
                rootWidth = viewport.rect.width - horizontalPadding;
            float bodyWidth = Mathf.Max(1f, rootWidth - _horizontalPadding * 2f);
            if (!Mathf.Approximately(height, _appliedHeight))
            {
                _appliedHeight = height;
                _layout.preferredHeight = height;
                LayoutRebuilder.MarkLayoutForRebuild(_root);
            }
            if (!Mathf.Approximately(bodyWidth, _appliedWidth))
            {
                _appliedWidth = bodyWidth;
                _body.sizeDelta = new Vector2(bodyWidth, _body.sizeDelta.y);
            }
        }

        private void OnEnable()
        {
            RefreshLayout();
        }

        private void LateUpdate()
        {
            RefreshLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshLayout();
        }
    }
}
