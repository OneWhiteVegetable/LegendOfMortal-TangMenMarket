using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static partial class UiBuilder
    {
        private static RectTransform BuildAssetsPage()
        {
            RectTransform root = SimpleRoot("AssetsPage", 1828f, 830f);
            RectTransform cards = UiElements.Rect("AssetCards", root);
            UiElements.TopStretch(cards, 0f, 78f);
            UiElements.AddHorizontalLayout(cards, 0, 0, 0, 0, 10f);
            Bind("AssetCardsLayout", cards, "HorizontalLayoutGroup");

            string[] captions = { "随身金钱", "货物总产值", "净总资产", "累计盈亏", "收益率" };
            string[] captionKeys = { "CashCaptionText", "GoodsCaptionText", "EquityCaptionText", "ProfitCaptionText", "ReturnCaptionText" };
            string[] values = { "196,036<size=12> 文</size>", "7,944<size=12> 文</size>", "200,000<size=12> 文</size>", "+0", "— 0.00%" };
            string[] keys = { "CashText", "GoodsText", "EquityText", "ProfitText", "ReturnText" };
            string[] accentKeys = { "CashAccent", "GoodsAccent", "EquityAccent", "ProfitAccent", "ReturnAccent" };
            for (int i = 0; i < 5; i++)
            {
                Image card = UiElements.Image("AssetCard" + i, cards, "card_asset_9s", Color.white, true, true);
                UiElements.Layout(card, -1f, 78f, 1f, 0f);
                Color accentColor = i == 1 ? UiTheme.Green : i >= 3 ? UiTheme.Red : UiTheme.Gold;
                Image accent = UiElements.Image("Accent", card.transform, null, accentColor);
                UiElements.LeftStretch(accent.rectTransform, 0f, 4f);
                Bind(accentKeys[i], accent.rectTransform, "Image");

                Text caption = UiElements.Text(captionKeys[i], card.transform, captions[i], 11, UiTheme.Muted, TextAnchor.MiddleLeft);
                UiElements.TopStretch(caption.rectTransform, 9f, 20f, 16f, 12f);
                Bind(captionKeys[i], caption.rectTransform, "Text");

                Text value = UiElements.Text(keys[i], card.transform, values[i], 26, i == 1 ? UiTheme.Green : i >= 3 ? UiTheme.Red : UiTheme.Ink, TextAnchor.MiddleLeft, true);
                value.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiElements.TopStretch(value.rectTransform, 31f, 39f, 16f, 12f);
                Bind(keys[i], value.rectTransform, "Text");
            }

            Image chartPanel = UiElements.Image("ChartPanel", root, "panel_detail_chart_9s", UiTheme.Alpha(Color.white, 0.62f), true, true);
            UiElements.Stretch(chartPanel.rectTransform, 0f, 90f, 0f, 66f);
            Bind("ChartPanel", chartPanel.rectTransform, "RectTransform");

            RectTransform header = UiElements.Rect("ChartHeader", chartPanel.transform);
            UiElements.TopStretch(header, 0f, 46f);
            Bind("ChartHeader", header, "RectTransform");
            Text title = UiElements.PlaceText("Title", header, "总盈亏走势", 16, UiTheme.Ink, 18f, 6f, 260f, 34f, TextAnchor.MiddleLeft, true);
            Bind("ChartTitleText", title.rectTransform, "Text");

            RectTransform legend = UiElements.Rect("Legend", header);
            legend.anchorMin = new Vector2(1f, 1f);
            legend.anchorMax = new Vector2(1f, 1f);
            legend.pivot = new Vector2(1f, 1f);
            legend.anchoredPosition = new Vector2(-104f, -5f);
            legend.sizeDelta = new Vector2(250f, 36f);
            HorizontalLayoutGroup legendLayout = legend.gameObject.AddComponent<HorizontalLayoutGroup>();
            legendLayout.spacing = 12f;
            legendLayout.childAlignment = TextAnchor.MiddleRight;
            legendLayout.childControlWidth = true;
            legendLayout.childControlHeight = true;
            legendLayout.childForceExpandWidth = false;
            legendLayout.childForceExpandHeight = false;
            BuildAssetLegendEntry(legend, "Cash", "总盈亏 · 文", UiTheme.Gold, 100f);
            BuildAssetLegendEntry(legend, "Goods", "总收益率 · %", UiTheme.Green, 118f);

            Button reset = UiElements.TextButton("ResetChartButton", header, "复位", "text_action", 12, UiTheme.Muted);
            RectTransform resetRect = reset.GetComponent<RectTransform>();
            resetRect.anchorMin = new Vector2(1f, 1f);
            resetRect.anchorMax = new Vector2(1f, 1f);
            resetRect.pivot = new Vector2(1f, 1f);
            resetRect.anchoredPosition = new Vector2(-12f, -7f);
            resetRect.sizeDelta = new Vector2(82f, 32f);

            GameObject host = Instantiate("ChartHost", chartPanel.transform);
            host.name = "AssetChartHost";
            UiElements.Stretch(host.GetComponent<RectTransform>(), 14f, 46f, 14f, 12f);
            RectTransform goodsAxis = UiElements.Rect("GoodsAxisHost", host.transform);
            UiElements.Stretch(goodsAxis);
            Bind("AssetChartHost", host.GetComponent<RectTransform>(), "RectTransform");
            Bind("GoodsAxisHost", goodsAxis, "RectTransform");
            Bind("ResetChartButton", resetRect, "Button");

            Text disclaimer = UiElements.Text(
                "DisclaimerText",
                root,
                "金线读左轴（文），收益率读右轴（%）。正收益率用朱红，负收益率用青绿，零值用灰。总盈亏为已实现与持仓浮盈之和，收益率以累计买入成本与借货实收之和为分母。",
                11,
                UiTheme.Muted,
                TextAnchor.UpperLeft);
            disclaimer.lineSpacing = 1.22f;
            UiElements.BottomStretch(disclaimer.rectTransform, 0f, 54f, 4f, 4f);
            Bind("DisclaimerText", disclaimer.rectTransform, "Text");
            return root;
        }

        private static void BuildAssetLegendEntry(Transform parent, string name, string label, Color color, float width)
        {
            RectTransform root = UiElements.Rect(name, parent);
            UiElements.Layout(root, width, 36f);
            Image stroke = UiElements.Image("Stroke", root, null, color);
            UiElements.Place(stroke.rectTransform, 0f, 16f, 20f, 3f);
            Text text = UiElements.PlaceText("Label", root, label, 12, UiTheme.Muted, 26f, 3f, width - 26f, 30f, TextAnchor.MiddleLeft);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }
}
