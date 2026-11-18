using System;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.Trading;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Shared
{
    public sealed class AssetBar : MonoBehaviour
    {
        private Text _indexValue;
        private Text _indexChange;
        private Text _cashValue;
        private Text _cashCaption;
        private Text _netValue;
        private Text _profitValue;

        public RectTransform Root { get; private set; }
        public Button IndexButton { get; private set; }

        public event Action IndexClicked;

        public static AssetBar Create(UiFactory factory, Transform parent)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }
            if (parent == null)
            {
                throw new ArgumentNullException("parent");
            }

            Image panel = factory.CreatePanel(parent, "SharedAssetBar", "panel_masthead_9s");
            AssetBar bar = panel.gameObject.AddComponent<AssetBar>();
            bar.Root = panel.rectTransform;
            UiFactory.SetRect(
                bar.Root,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 104f));

            Image brand = factory.CreateImage(
                panel.transform,
                "Brand",
                "panel_brand_block",
                new Vector2(248f, 88f));
            UiFactory.SetTopLeft(brand.rectTransform, new Vector2(12f, -8f), new Vector2(248f, 88f));
            Image seal = factory.CreateImage(
                brand.transform,
                "Seal",
                "brand_shang_mark",
                new Vector2(66f, 66f),
                true);
            UiFactory.SetTopLeft(seal.rectTransform, new Vector2(12f, -10f), new Vector2(66f, 66f));
            Text title = factory.CreateText(
                brand.transform,
                "Title",
                "唐門行市",
                31,
                UiFormatting.Paper,
                TextAnchor.MiddleLeft,
                new Vector2(152f, 44f));
            UiFactory.SetTopLeft(title.rectTransform, new Vector2(82f, -10f), new Vector2(152f, 44f));
            Text subtitle = factory.CreateText(
                brand.transform,
                "Subtitle",
                "TANGMEN EXCHANGE",
                11,
                UiFormatting.Gold,
                TextAnchor.MiddleLeft,
                new Vector2(152f, 24f));
            UiFactory.SetTopLeft(subtitle.rectTransform, new Vector2(84f, -53f), new Vector2(152f, 24f));

            bar.IndexButton = factory.CreateButton(
                panel.transform,
                "IndexButton",
                string.Empty,
                bar.RaiseIndexClicked,
                20,
                new Vector2(340f, 80f),
                "button_text_action_normal_9s",
                "button_text_action_hover_9s",
                null,
                "button_text_action_disabled_9s");
            UiFactory.SetTopLeft(
                bar.IndexButton.GetComponent<RectTransform>(),
                new Vector2(276f, -12f),
                new Vector2(340f, 80f));
            Text oldLabel = bar.IndexButton.GetComponentInChildren<Text>();
            if (oldLabel != null)
            {
                oldLabel.gameObject.SetActive(false);
            }
            Text indexName = factory.CreateText(
                bar.IndexButton.transform,
                "IndexName",
                "唐門指數",
                16,
                UiFormatting.Ink,
                TextAnchor.MiddleLeft,
                new Vector2(148f, 26f));
            UiFactory.SetTopLeft(indexName.rectTransform, new Vector2(18f, -8f), new Vector2(148f, 26f));
            bar._indexValue = factory.CreateText(
                bar.IndexButton.transform,
                "IndexValue",
                "—",
                28,
                UiFormatting.Ink,
                TextAnchor.MiddleLeft,
                new Vector2(190f, 40f));
            UiFactory.SetTopLeft(bar._indexValue.rectTransform, new Vector2(18f, -34f), new Vector2(190f, 40f));
            bar._indexChange = factory.CreateText(
                bar.IndexButton.transform,
                "IndexChange",
                "—",
                18,
                UiFormatting.Ink,
                TextAnchor.MiddleRight,
                new Vector2(116f, 48f));
            UiFactory.SetRect(
                bar._indexChange.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-16f, 0f),
                new Vector2(116f, 48f));

            Text profitCaption;
            CreateMetric(factory, panel.transform, "Profit", "累計盈虧", -548f, out profitCaption, out bar._profitValue);
            Text netCaption;
            CreateMetric(factory, panel.transform, "NetAssets", "淨資產", -306f, out netCaption, out bar._netValue);
            CreateMetric(factory, panel.transform, "Cash", "隨身銀錢", -24f, out bar._cashCaption, out bar._cashValue);
            return bar;
        }

        public void SetIndex(double value, double returnRate)
        {
            if (_indexValue != null)
            {
                _indexValue.text = UiFormatting.Index(value);
            }
            if (_indexChange != null)
            {
                _indexChange.text = UiFormatting.Percent(returnRate);
                _indexChange.color = UiFormatting.TrendColorOnLight(returnRate);
            }
        }

        public void SetIndexInteractive(bool interactive)
        {
            if (IndexButton != null)
            {
                IndexButton.interactable = interactive;
            }
        }

        public void SetAssets(AssetSnapshot snapshot)
        {
            if (snapshot == null)
            {
                SetAssets(0L, 0.0, 0.0, 0L);
                return;
            }
            SetAssets(snapshot.Cash, snapshot.NetAssets, snapshot.TotalProfit, snapshot.Debt);
        }

        public void SetAssets(long cash, double netAssets, double totalProfit, long debt)
        {
            if (_cashValue != null)
            {
                _cashValue.text = UiFormatting.Money(cash) + " 文";
            }
            if (_cashCaption != null)
            {
                _cashCaption.text = debt > 0L
                    ? "隨身銀錢 · 欠款 " + UiFormatting.Money(debt)
                    : "隨身銀錢";
            }
            if (_netValue != null)
            {
                _netValue.text = UiFormatting.Money(netAssets) + " 文";
            }
            if (_profitValue != null)
            {
                _profitValue.text = UiFormatting.SignedMoney(totalProfit) + " 文";
                _profitValue.color = UiFormatting.TrendColor(totalProfit);
            }
        }

        public void DockNavigation(ButtonNavigation navigation)
        {
            if (navigation == null || navigation.Root == null || Root == null)
            {
                return;
            }
            if (_profitValue != null)
            {
                _profitValue.transform.parent.gameObject.SetActive(false);
            }
            if (_netValue != null)
            {
                _netValue.transform.parent.gameObject.SetActive(false);
            }
            float navigationWidth = navigation.Root.sizeDelta.x;
            if (_cashValue != null)
            {
                RectTransform cashRoot = _cashValue.transform.parent as RectTransform;
                if (cashRoot != null)
                {
                    cashRoot.anchoredPosition = new Vector2(-navigationWidth - 34f, -12f);
                }
            }
            navigation.Root.SetParent(Root, false);
            UiFactory.SetRect(
                navigation.Root,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-12f, -20f),
                new Vector2(navigationWidth, 62f));
        }

        public void SetFont(Font font)
        {
            if (font == null || Root == null)
            {
                return;
            }
            Text[] texts = Root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
            }
        }

        private void RaiseIndexClicked()
        {
            Action handler = IndexClicked;
            if (handler != null)
            {
                handler();
            }
        }

        private static void CreateMetric(
            UiFactory factory,
            Transform parent,
            string name,
            string caption,
            float right,
            out Text captionText,
            out Text valueText)
        {
            RectTransform root = UiFactory.CreateRect(name, parent);
            UiFactory.SetRect(
                root,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(right, -12f),
                new Vector2(226f, 80f));
            captionText = factory.CreateText(
                root,
                "Caption",
                caption,
                15,
                UiFormatting.MutedPaper,
                TextAnchor.MiddleRight,
                new Vector2(226f, 24f));
            UiFactory.SetRect(
                captionText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 24f));
            valueText = factory.CreateText(
                root,
                "Value",
                "—",
                25,
                UiFormatting.Paper,
                TextAnchor.MiddleRight,
                new Vector2(226f, 44f));
            UiFactory.SetRect(
                valueText.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, 44f));
        }
    }
}
