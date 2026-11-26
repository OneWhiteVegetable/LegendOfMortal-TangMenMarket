using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static partial class UiBuilder
    {
        private static RectTransform BuildGoodsCard()
        {
            Button rootButton = UiElements.Button("GoodsCard", null, "card_goods_normal", "card_goods_normal", null, "card_goods_delisted", true);
            RectTransform root = rootButton.GetComponent<RectTransform>();
            currentRoot = root;
            UiElements.Center(root, 468f, 384f);
            UiElements.Layout(rootButton, -1f, 150f, 1f, 1f);
            Bind("OpenButton", root, "Button");
            Bind("Background", root, "Image");
            Shadow rootShadow = root.gameObject.AddComponent<Shadow>();
            rootShadow.effectColor = UiTheme.Alpha(UiTheme.Ink, 0.9f);
            rootShadow.effectDistance = new Vector2(4f, -5f);
            rootShadow.useGraphicAlpha = true;
            rootShadow.enabled = false;

            RectTransform motionPivot = UiElements.Rect("MotionPivot", root);
            UiElements.Stretch(motionPivot);
            RectTransform visualContent = UiElements.Rect("VisualContent", motionPivot);
            UiElements.Stretch(visualContent);
            Image goldLine = UiElements.Image("HoverGoldLine", visualContent, null, UiTheme.Gold);
            SetGoldLine(goldLine.rectTransform);

            RectTransform chart = UiElements.Host("ChartHost", root, "ChartHost");
            UiElements.Stretch(chart, 0f, 0f, 0f, 42f);
            Text ghost = UiElements.PlaceText("GhostNumber", root, "01", 91, UiTheme.Alpha(UiTheme.Ink, 0.045f), 272f, 30f, 184f, 100f, TextAnchor.UpperRight, true);
            ghost.font = UiAssets.DisplayFont;
            ghost.fontStyle = FontStyle.Italic;
            Bind("IndexText", ghost.rectTransform, "Text");

            Image nameBack = UiElements.Image("NameBack", root, null, UiTheme.Alpha(UiTheme.PaperLight, 0.86f));
            UiElements.Place(nameBack.rectTransform, 15f, 14f, 328f, 27f);
            Shadow nameShadow = nameBack.gameObject.AddComponent<Shadow>();
            nameShadow.effectColor = UiTheme.Red;
            nameShadow.effectDistance = new Vector2(3f, 0f);
            nameShadow.useGraphicAlpha = false;
            nameShadow.enabled = false;
            Image nameRedLine = UiElements.Image("NameRedLine", nameBack.transform, null, UiTheme.Red);
            nameRedLine.rectTransform.anchorMin = new Vector2(1f, 0f);
            nameRedLine.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameRedLine.rectTransform.pivot = new Vector2(0f, 0.5f);
            nameRedLine.rectTransform.offsetMin = Vector2.zero;
            nameRedLine.rectTransform.offsetMax = new Vector2(3f, 0f);
            Text name = UiElements.PlaceText("NameText", root, "飞天门辟邪玉", 19, UiTheme.Ink, 21f, 16f, 316f, 23f, TextAnchor.MiddleLeft, true);
            ConfigureSingleLine(name, 14, 19);
            float nameWidth = Mathf.Clamp(Mathf.Ceil(name.preferredWidth), 1f, 316f);
            name.rectTransform.sizeDelta = new Vector2(nameWidth, 23f);
            nameBack.rectTransform.sizeDelta = new Vector2(Mathf.Min(328f, nameWidth + 12f), 27f);
            Shadow nameTextShadow = name.gameObject.AddComponent<Shadow>();
            nameTextShadow.effectColor = UiTheme.PaperLight;
            nameTextShadow.effectDistance = new Vector2(1f, -1f);
            nameTextShadow.useGraphicAlpha = false;
            RectTransform categorySkew = UiElements.Rect("CategorySkew", root);
            Image categoryBack = UiElements.Image("CategoryBack", categorySkew, null, UiTheme.Ink);
            Text category = UiElements.PlaceText("CategoryText", categorySkew, "行货 · 01", 8, UiTheme.PaperLight, 0f, 0f, 92f, 16f, TextAnchor.MiddleCenter, true);
            float categoryWidth = Mathf.Ceil(category.preferredWidth) + 12f;
            PlaceTopRight(categorySkew, 15f, 14f, categoryWidth, 16f);
            UiElements.Stretch(categoryBack.rectTransform);
            UiElements.Stretch(category.rectTransform);
            Bind("NameText", name.rectTransform, "Text");
            Bind("CategorySkew", categorySkew, "RectTransform");
            Bind("CategoryText", category.rectTransform, "Text");

            Image halftone = UiElements.Image("Halftone", root, "texture_celadon_halftone", UiTheme.Alpha(UiTheme.Celadon, 0.14f));
            UiElements.Place(halftone.rectTransform, 28f, 91f, 158f, 158f);
            halftone.transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            Image item = UiElements.Image("ItemImage", root, "01_飞天门辟邪玉", Color.white);
            UiElements.Place(item.rectTransform, 20f, 64f, 190f, 186f);
            item.preserveAspect = true;
            Bind("ItemImage", item.rectTransform, "Image");

            Image quoteBack = UiElements.Image("QuoteBack", root, null, UiTheme.Alpha(UiTheme.PaperLight, 0.86f));
            UiElements.Place(quoteBack.rectTransform, 238f, 120f, 202f, 112f);
            Image changeBack = UiElements.Image("ChangeBack", root, null, UiTheme.Alpha(UiTheme.Ink, 0.045f));
            UiElements.Place(changeBack.rectTransform, 280f, 202f, 153f, 26f);
            Text price = UiElements.PlaceText("PriceText", root, "991", 37, UiTheme.Red, 235f, 124f, 180f, 46f, TextAnchor.MiddleRight, true);
            price.font = UiAssets.BoldFont;
            price.fontStyle = FontStyle.Italic;
            price.horizontalOverflow = HorizontalWrapMode.Overflow;
            price.verticalOverflow = VerticalWrapMode.Overflow;
            Text priceUnit = UiElements.PlaceText("PriceUnitText", root, "文", 10, UiTheme.Hex("6B726B"), 418f, 124f, 15f, 46f, TextAnchor.MiddleRight);
            Text delta = UiElements.PlaceText("DeltaText", root, "+60 文", 11, UiTheme.Red, 242f, 175f, 191f, 22f, TextAnchor.MiddleRight, true);
            Text change = UiElements.PlaceText("ChangeText", root, "▲ +6.44%", 15, UiTheme.Red, 240f, 201f, 193f, 28f, TextAnchor.MiddleRight, true);
            Bind("PriceText", price.rectTransform, "Text");
            Bind("PriceUnitText", priceUnit.rectTransform, "Text");
            Bind("DeltaText", delta.rectTransform, "Text");
            Bind("ChangeText", change.rectTransform, "Text");

            Image actionBack = UiElements.Image("ActionBand", root, null, UiTheme.Alpha(UiTheme.CardPaper, 0.96f));
            UiElements.BottomStretch(actionBack.rectTransform, 13f, 39f, 16f, 16f);
            Image actionTop = UiElements.Image("ActionTopLine", actionBack.transform, null, UiTheme.Alpha(UiTheme.Ink, 0.14f));
            actionTop.rectTransform.anchorMin = new Vector2(0f, 1f);
            actionTop.rectTransform.anchorMax = new Vector2(1f, 1f);
            actionTop.rectTransform.offsetMin = new Vector2(0f, -1f);
            actionTop.rectTransform.offsetMax = Vector2.zero;
            RectTransform stepper = UiElements.Stepper("QuantityStepper", root, "Quantity");
            TuneStepper(stepper, 28f, 30f, 29f, 12, 14f);
            stepper.anchorMin = Vector2.zero;
            stepper.anchorMax = Vector2.zero;
            stepper.pivot = Vector2.zero;
            stepper.anchoredPosition = new Vector2(15f, 17f);
            stepper.sizeDelta = new Vector2(86f, 29f);
            Button buy = UiElements.TradeButton("BuyButton", root, "red", "icon_cart");
            TuneTradeButton(buy, 56f, 16f);
            Button borrow = UiElements.TradeButton("BorrowButton", root, "green", "icon_scroll");
            TuneTradeButton(borrow, 15f, 16f);
            Bind("BuyButton", buy.GetComponent<RectTransform>(), "Button");
            Bind("BorrowButton", borrow.GetComponent<RectTransform>(), "Button");

            Image delisted = UiElements.Image("DelistedMark", root, "wasted_delist_wordmark", Color.white);
            UiElements.Center(delisted.rectTransform, 330f, 116f);
            delisted.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            delisted.gameObject.SetActive(false);
            Bind("DelistedMark", delisted.rectTransform, "Image");
            return root;
        }

        private static RectTransform BuildCartEntry()
        {
            RectTransform root = SimpleRoot("CartEntry", 310f, 88f);
            Image background = UiElements.Image("Background", root, "card_cart_entry_long_9s", Color.white, true, true);
            UiElements.Stretch(background.rectTransform);
            Shadow shadow = background.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(3f, -3f);
            shadow.useGraphicAlpha = true;
            UiElements.Layout(root, -1f, 88f, 1f, 0f);
            Bind("Background", background.rectTransform, "Image");
            Image item = UiElements.Image("ItemImage", root, "01_飞天门辟邪玉", Color.white);
            UiElements.Place(item.rectTransform, 12f, 9f, 39f, 39f);
            item.preserveAspect = true;
            Text name = UiElements.PlaceText("NameText", root, "飞天门辟邪玉", 12, UiTheme.Ink, 60f, 8f, 196f, 23f, TextAnchor.MiddleLeft, true);
            Text type = UiElements.PlaceText("TypeText", root, "买入 · 货契", 9, UiTheme.RedDark, 60f, 29f, 190f, 17f, TextAnchor.MiddleLeft, true);
            Button remove = UiElements.Button("RemoveButton", root, "button_text_action_normal_9s", "button_text_action_hover_9s", null, "button_text_action_disabled_9s", true);
            PlaceTopRight(remove.GetComponent<RectTransform>(), 9f, 9f, 23f, 23f);
            UiElements.SetColorTint(remove, UiTheme.Alpha(UiTheme.PaperLight, 0.05f), UiTheme.Alpha(UiTheme.Red, 0.18f), UiTheme.Alpha(UiTheme.Red, 0.3f));
            Image removeIcon = UiElements.Icon("Icon", remove.transform, "icon_close", UiTheme.Muted, 0f, 0f, 13f);
            UiElements.Center(removeIcon.rectTransform, 13f, 13f);
            removeIcon.transform.SetAsLastSibling();
            RectTransform stepper = UiElements.Stepper("QuantityStepper", root, "Quantity");
            TuneStepper(stepper, 23f, 24f, 24f, 11, 14f);
            UiElements.Place(stepper, 12f, 56f, 70f, 24f);
            Text subtotal = UiElements.PlaceText("SubtotalText", root, "1,982 <size=9>文</size>", 14, UiTheme.Ink, 151f, 52f, 145f, 27f, TextAnchor.MiddleRight, true);
            PlaceTopRight(subtotal.rectTransform, 12f, 52f, 145f, 27f);
            Text time = UiElements.PlaceText("SettledText", root, "1年1月1周 · ×2", 9, UiTheme.Muted, 12f, 55f, 116f, 24f, TextAnchor.MiddleLeft);
            time.gameObject.SetActive(false);
            Bind("ItemImage", item.rectTransform, "Image");
            Bind("NameText", name.rectTransform, "Text");
            Bind("TypeText", type.rectTransform, "Text");
            Bind("RemoveButton", remove.GetComponent<RectTransform>(), "Button");
            Bind("SubtotalText", subtotal.rectTransform, "Text");
            Bind("SettledText", time.rectTransform, "Text");
            return root;
        }

        private static RectTransform BuildRankingRow()
        {
            Button rootButton = UiElements.Button("RankingRow", null, "row_ranking_normal_9s", "row_ranking_hover_9s", null, null, true);
            RectTransform root = rootButton.GetComponent<RectTransform>();
            currentRoot = root;
            UiElements.Center(root, 1760f, 68f);
            UiElements.Layout(rootButton, -1f, 68f, 1f, 0f);
            Bind("OpenButton", root, "Button");
            Text rank = UiElements.PlaceText("RankText", root, "01", 29, UiTheme.Red, 14f, 6f, 74f, 56f, TextAnchor.MiddleCenter, true);
            rank.font = UiAssets.DisplayFont;
            rank.fontStyle = FontStyle.Italic;

            Image item = UiElements.Image("ItemImage", root, "01_飞天门辟邪玉", Color.white);
            PlaceRankingFixed(item.rectTransform, 100f, 10f, 48f, 48f);
            item.preserveAspect = true;
            Text name = UiElements.PlaceText("NameText", root, "飞天门辟邪玉", 14, UiTheme.Ink, 159f, 11f, 475f, 27f, TextAnchor.MiddleLeft, true);
            PlaceRankingColumn(name.rectTransform, 159f, 475f, 11f, 27f);
            Text key = UiElements.PlaceText("KeyText", root, "mod:01", 8, UiTheme.Muted, 159f, 40f, 475f, 16f, TextAnchor.MiddleLeft);
            PlaceRankingColumn(key.rectTransform, 159f, 475f, 40f, 16f);

            Text price = UiElements.PlaceText("PriceText", root, "991 <size=9>文</size>", 16, UiTheme.Ink, 646f, 6f, 317f, 56f, TextAnchor.MiddleLeft, true);
            PlaceRankingColumn(price.rectTransform, 646f, 317f, 6f, 56f);
            Text change = UiElements.PlaceText("ChangeText", root, "▲ +21.30%", 16, UiTheme.Red, 975f, 6f, 317f, 56f, TextAnchor.MiddleLeft, true);
            PlaceRankingColumn(change.rectTransform, 975f, 317f, 6f, 56f);
            RectTransform chart = UiElements.Host("ChartHost", root, "ChartHost");
            PlaceRankingColumn(chart, 1304f, 395f, 13f, 42f);
            Image open = UiElements.Image("OpenIcon", root, "icon_right", UiTheme.Muted);
            PlaceTopRight(open.rectTransform, 22f, 25f, 18f, 18f);
            open.preserveAspect = true;
            Bind("RankText", rank.rectTransform, "Text");
            Bind("ItemImage", item.rectTransform, "Image");
            Bind("NameText", name.rectTransform, "Text");
            Bind("KeyText", key.rectTransform, "Text");
            Bind("PriceText", price.rectTransform, "Text");
            Bind("ChangeText", change.rectTransform, "Text");
            return root;
        }

        private static void ConfigureSingleLine(Text text, int minimum, int maximum)
        {
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimum;
            text.resizeTextMaxSize = maximum;
        }

        private static void TuneStepper(RectTransform stepper, float buttonWidth, float valueWidth, float height, int fontSize, float iconSize)
        {
            Button decrement = stepper.Find("DecrementButton").GetComponent<Button>();
            Button increment = stepper.Find("IncrementButton").GetComponent<Button>();
            Text quantity = stepper.Find("QuantityText").GetComponent<Text>();
            UiElements.Place(decrement.GetComponent<RectTransform>(), 0f, 0f, buttonWidth, height);
            UiElements.Place(quantity.rectTransform, buttonWidth, 0f, valueWidth, height);
            UiElements.Place(increment.GetComponent<RectTransform>(), buttonWidth + valueWidth, 0f, buttonWidth, height);
            quantity.font = UiAssets.BoldFont;
            quantity.fontSize = fontSize;
            quantity.fontStyle = FontStyle.Italic;
            UiElements.Center(decrement.transform.Find("Icon").GetComponent<RectTransform>(), iconSize, iconSize);
            UiElements.Center(increment.transform.Find("Icon").GetComponent<RectTransform>(), iconSize, iconSize);
        }

        private static void TuneTradeButton(Button button, float right, float bottom)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-right, bottom);
            rect.sizeDelta = new Vector2(34f, 31f);
            UiElements.Center(button.transform.Find("Icon").GetComponent<RectTransform>(), 20f, 20f);
        }

        private static void SetGoldLine(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(-1f, 18f);
            rect.offsetMax = new Vector2(3f, -16f);
            rect.localScale = new Vector3(1f, 0f, 1f);
        }

        private static void PlaceTopRight(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceRankingFixed(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(left / 1760f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceRankingColumn(RectTransform rect, float left, float width, float top, float height)
        {
            rect.anchorMin = new Vector2(left / 1760f, 1f);
            rect.anchorMax = new Vector2((left + width) / 1760f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(0f, -top - height);
            rect.offsetMax = new Vector2(0f, -top);
        }

        private static RectTransform BuildChartHost()
        {
            return SimpleRoot("ChartHost", 1200f, 600f);
        }
    }
}
