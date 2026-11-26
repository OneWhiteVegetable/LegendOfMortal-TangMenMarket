using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketDetailStickyFooter : MonoBehaviour
    {
        private RectTransform _scrollRect;
        private RectTransform _trade;
        private RectTransform _spacer;
        private LayoutElement _tradeLayout;
        private float _height;
        private bool _initialized;

        internal void Initialize(ScrollRect scroll, RectTransform trade, RectTransform spacer)
        {
            _scrollRect = scroll.transform as RectTransform;
            _trade = trade;
            _spacer = spacer;
            _tradeLayout = trade.GetComponent<LayoutElement>();
            _height = _tradeLayout.preferredHeight;
            _tradeLayout.ignoreLayout = true;
            RectTransform sidebar = scroll.transform.parent as RectTransform;
            if (sidebar != null)
                _trade.SetParent(sidebar, false);
            _trade.SetAsLastSibling();
            _spacer.gameObject.SetActive(false);
            _initialized = true;
            LayoutButtons();
        }

        internal void SetVisible(bool visible)
        {
            if (!_initialized)
                return;
            _trade.gameObject.SetActive(visible);
            _spacer.gameObject.SetActive(false);
            if (_scrollRect != null)
            {
                Vector2 offsetMin = _scrollRect.offsetMin;
                offsetMin.y = visible ? _height : 0f;
                _scrollRect.offsetMin = offsetMin;
            }
            if (visible)
            {
                _trade.anchorMin = new Vector2(0f, 0f);
                _trade.anchorMax = new Vector2(1f, 0f);
                _trade.pivot = new Vector2(0.5f, 0f);
                _trade.offsetMin = Vector2.zero;
                _trade.offsetMax = new Vector2(0f, _height);
            }
            Canvas.ForceUpdateCanvases();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_initialized)
                LayoutButtons();
        }

        private void LayoutButtons()
        {
            float scale = MarketLayoutMetrics.ContentScale(Screen.width, Screen.height);
            float width = MarketLayoutMetrics.EffectiveSize(Screen.width, scale);
            float pad = width <= 1450f ? 13f : width >= 1700f ? 22f : 17f;
            RectTransform stepper = _trade.Find("QuantityStepper") as RectTransform;
            stepper.sizeDelta = new Vector2(86f, 29f);
            stepper.anchoredPosition = new Vector2(-pad, -10f);
            PlaceCell(stepper.Find("DecrementButton") as RectTransform, 0f, 28f);
            PlaceCell(stepper.Find("QuantityText") as RectTransform, 28f, 30f);
            PlaceCell(stepper.Find("IncrementButton") as RectTransform, 58f, 28f);
            SetHalf("BuyButton", true, pad, 48f, 52f);
            SetHalf("BorrowButton", false, pad, 48f, 52f);
            SetHalf("SellButton", true, pad, 109f, 36f);
            SetHalf("CoverButton", false, pad, 109f, 36f);
        }

        private static void PlaceCell(RectTransform cell, float x, float width)
        {
            cell.anchorMin = cell.anchorMax = new Vector2(0f, 1f);
            cell.pivot = new Vector2(0f, 1f);
            cell.anchoredPosition = new Vector2(x, -1f);
            cell.sizeDelta = new Vector2(width, 27f);
        }

        private void SetHalf(string key, bool left, float pad, float top, float height)
        {
            RectTransform rect = _trade.Find(key) as RectTransform;
            rect.anchorMin = new Vector2(left ? 0f : 0.5f, 1f);
            rect.anchorMax = new Vector2(left ? 0.5f : 1f, 1f);
            rect.offsetMin = new Vector2(left ? pad : 4.5f, -top - height);
            rect.offsetMax = new Vector2(left ? -4.5f : -pad, -top);
        }

        internal void Shutdown()
        {
            _initialized = false;
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}
