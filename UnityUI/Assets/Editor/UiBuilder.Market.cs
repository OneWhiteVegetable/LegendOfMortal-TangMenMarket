using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static partial class UiBuilder
    {
        private static RectTransform BuildMarketWindow()
        {
            RectTransform window;
            RectTransform root = AddWindowShell("MarketWindow", out window);
            AddTicker(window);
            RectTransform page = UiElements.Rect("PageHost", window);
            UiElements.Stretch(page, 0f, 116f, 0f, 28f);
            Bind("PageHost", page, "RectTransform");

            RectTransform goodsArea = UiElements.Rect("GoodsArea", page);
            UiElements.Stretch(goodsArea, 26f, 20f, 383f, 15f);
            Bind("GoodsArea", goodsArea, "RectTransform");
            RectTransform section = UiElements.Rect("SectionHeader", goodsArea);
            UiElements.TopStretch(section, 0f, 34f);
            Image sectionSlash = UiElements.Image("Slash", section, null, UiTheme.Red);
            UiElements.Place(sectionSlash.rectTransform, 0f, 10f, 22f, 9f);
            sectionSlash.transform.localRotation = Quaternion.identity;
            UiElements.PlaceText("Title", section, "今日货价", 19, UiTheme.Ink, 31f, 0f, 154f, 34f, TextAnchor.MiddleLeft, true);
            UiElements.PlaceText("Subtitle", section, "WEEKLY COMMODITIES", 9, UiTheme.Muted, 127f, 7f, 180f, 20f, TextAnchor.MiddleLeft, true);

            Button sort = UiElements.Button("SortButton", section, "button_sort_normal_9s", "button_sort_hover_9s", "button_sort_pressed_9s", null, true);
            PlaceRight(sort.GetComponent<RectTransform>(), 216f, 5f, 144f, 34f);
            Text sortLabel = UiElements.Text("Label", sort.transform, "涨幅优先 · 降序", 11, UiTheme.PaperLight, TextAnchor.MiddleCenter, true);
            UiElements.Stretch(sortLabel.rectTransform, 8f, 2f, 8f, 2f);
            sort.gameObject.SetActive(false);
            Bind("SortButton", sort.GetComponent<RectTransform>(), "Button");

            RectTransform pager = UiElements.Rect("Pager", section);
            UiElements.RightStretch(pager, 0f, 208f, -5f, -5f);
            Button previous = UiElements.Button("PreviousPageButton", pager, "button_brush_arrow_normal", "button_brush_arrow_hover", "button_brush_arrow_pressed", "button_brush_arrow_disabled");
            UiElements.Place(previous.GetComponent<RectTransform>(), 0f, 0f, 48f, 44f);
            Image previousIcon = UiElements.Image("Icon", previous.transform, "icon_left", UiTheme.Ink);
            UiElements.Center(previousIcon.rectTransform, 34f, 34f);
            previousIcon.preserveAspect = true;
            Text pageText = UiElements.PlaceText("PageText", pager, "01  /  09", 12, UiTheme.Muted, 52f, 0f, 104f, 44f, TextAnchor.MiddleCenter, true);
            pageText.gameObject.SetActive(false);
            Text currentPage = UiElements.PlaceText("CurrentPageText", pager, "01", 28, UiTheme.Ink, 56f, 0f, 42f, 38f, TextAnchor.MiddleCenter, true);
            currentPage.font = UiAssets.DisplayFont;
            currentPage.fontSize = 28;
            currentPage.fontStyle = FontStyle.Italic;
            Image pageUnderline = UiElements.Image("PageUnderline", pager, null, UiTheme.Red);
            UiElements.Place(pageUnderline.rectTransform, 56f, 37f, 42f, 4f);
            Image totalBack = UiElements.Image("TotalPageBack", pager, null, UiTheme.Ink);
            UiElements.Place(totalBack.rectTransform, 104f, 10f, 44f, 24f);
            totalBack.transform.localRotation = Quaternion.Euler(0f, 0f, -3f);
            Text totalPage = UiElements.PlaceText("TotalPageText", totalBack.transform, "/ 09", 11, UiTheme.PaperLight, 0f, 0f, 44f, 24f, TextAnchor.MiddleCenter, true);
            Button next = UiElements.Button("NextPageButton", pager, "button_brush_arrow_normal", "button_brush_arrow_hover", "button_brush_arrow_pressed", "button_brush_arrow_disabled");
            UiElements.Place(next.GetComponent<RectTransform>(), 160f, 0f, 48f, 44f);
            Image nextIcon = UiElements.Image("Icon", next.transform, "icon_right", UiTheme.Ink);
            UiElements.Center(nextIcon.rectTransform, 34f, 34f);
            nextIcon.preserveAspect = true;
            Bind("PreviousPageButton", previous.GetComponent<RectTransform>(), "Button");
            Bind("NextPageButton", next.GetComponent<RectTransform>(), "Button");
            Bind("PageText", pageText.rectTransform, "Text");
            Bind("CurrentPageText", currentPage.rectTransform, "Text");
            Bind("TotalPageText", totalPage.rectTransform, "Text");

            RectTransform grid = UiElements.Rect("GoodsGrid", goodsArea);
            UiElements.Stretch(grid, 0f, 34f, 0f, 0f);
            VerticalLayoutGroup gridLayout = grid.gameObject.AddComponent<VerticalLayoutGroup>();
            gridLayout.spacing = 18f;
            gridLayout.childControlWidth = true;
            gridLayout.childControlHeight = true;
            gridLayout.childForceExpandWidth = true;
            gridLayout.childForceExpandHeight = true;
            string[] itemNames = { "01_飞天门辟邪玉", "02_藏宝图刺青拓样", "03_杂鱼收尸契", "04_铁拳暖食盒", "05_仿南宫尖肩甲", "06_飞石帮荣誉会员牌" };
            string[] prices = { "991", "926", "1,827", "1,324", "1,145", "908" };
            string[] deltas = { "+60 文", "+31 文", "-57 文", "+33 文", "-25 文", "+15 文" };
            string[] changes = { "▲ +6.44%", "▲ +3.46%", "▼ -3.03%", "▲ +2.56%", "▼ -2.14%", "▲ +1.68%" };
            for (int rowIndex = 0; rowIndex < 2; rowIndex++)
            {
                RectTransform row = UiElements.Rect("GoodsRow" + rowIndex, grid);
                HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 20f;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = true;
                rowLayout.childForceExpandHeight = true;
                Bind("GoodsRow" + rowIndex, row, "HorizontalLayoutGroup");
                for (int column = 0; column < 3; column++)
                {
                    int index = rowIndex * 3 + column;
                    GameObject card = Instantiate("GoodsCard", row);
                    card.name = "GoodsCard" + index.ToString("00");
                    RectTransform cardRoot = card.GetComponent<RectTransform>();
                    cardRoot.Find("ItemImage").GetComponent<Image>().sprite = UiAssets.Sprite(itemNames[index]);
                    Text nameText = cardRoot.Find("NameText").GetComponent<Text>();
                    nameText.text = itemNames[index].Substring(3);
                    Color trendColor = deltas[index].StartsWith("-") ? UiTheme.Green : UiTheme.Red;
                    Text priceText = cardRoot.Find("PriceText").GetComponent<Text>();
                    priceText.text = prices[index];
                    priceText.color = trendColor;
                    cardRoot.Find("PriceUnitText").GetComponent<Text>().text = "文";
                    Text deltaText = cardRoot.Find("DeltaText").GetComponent<Text>();
                    deltaText.text = deltas[index];
                    deltaText.color = trendColor;
                    Text changeText = cardRoot.Find("ChangeText").GetComponent<Text>();
                    changeText.text = changes[index];
                    changeText.color = trendColor;
                    cardRoot.Find("GhostNumber").GetComponent<Text>().text = (index + 1).ToString("00");
                    Transform categoryText = cardRoot.Find("CategorySkew/CategoryText");
                    if (categoryText == null)
                        categoryText = cardRoot.Find("CategoryText");
                    categoryText.GetComponent<Text>().text = "行货 · " + (index + 1).ToString("00");
                }
            }
            Bind("GoodsGrid", grid, "VerticalLayoutGroup");

            BuildCartRail(page);
            return root;
        }

        private static void BuildCartRail(RectTransform page)
        {
            Image rail = UiElements.Image("CartRail", page, "panel_cart_rail_9s", Color.white, true, true);
            UiElements.RightStretch(rail.rectTransform, 0f, 360f);
            Bind("CartRail", rail.rectTransform, "RectTransform");
            Image goldEdge = UiElements.Image("GoldEdge", rail.transform, null, UiTheme.Gold);
            UiElements.LeftStretch(goldEdge.rectTransform, 0f, 5f);
            Image hatch = UiElements.Image("Hatch", rail.transform, "texture_ink_diagonal_hatch_tile", UiTheme.Alpha(UiTheme.PaperLight, 0.08f));
            UiElements.Stretch(hatch.rectTransform);
            hatch.type = Image.Type.Tiled;
            UiElements.Icon("LedgerIcon", rail.transform, "icon_scroll", UiTheme.GoldLight, 20f, 17f, 27f);
            UiElements.PlaceText("Title", rail.transform, "契匣", 20, UiTheme.PaperLight, 58f, 10f, 126f, 32f, TextAnchor.MiddleLeft, true);
            UiElements.PlaceText("Subtitle", rail.transform, "CONTRACT LEDGER", 8, UiTheme.Hex("9DA69A"), 59f, 41f, 170f, 18f);
            Image countBack = UiElements.Image("CountBack", rail.transform, null, UiTheme.GoldLight);
            PlaceCartCount(countBack.rectTransform);
            Text count = UiElements.PlaceText("CartCountText", countBack.transform, "2", 14, UiTheme.Ink, 0f, 0f, 30f, 28f, TextAnchor.MiddleCenter, true);
            UiElements.Stretch(count.rectTransform);
            count.horizontalOverflow = HorizontalWrapMode.Overflow;
            Button clear = UiElements.Button("CartClearButton", rail.transform, "button_text_action_normal_9s", "button_text_action_hover_9s", null, "button_text_action_disabled_9s", true);
            PlaceRight(clear.GetComponent<RectTransform>(), 60f, 18f, 28f, 28f);
            UiElements.SetColorTint(clear, UiTheme.Alpha(UiTheme.Ink, 0.2f), UiTheme.Alpha(UiTheme.Red, 0.35f), UiTheme.Alpha(UiTheme.Red, 0.5f));
            Image trash = UiElements.Image("Icon", clear.transform, "icon_trash", UiTheme.PaperLight);
            UiElements.Center(trash.rectTransform, 16f, 16f);
            trash.preserveAspect = true;
            Bind("CartClearButton", clear.GetComponent<RectTransform>(), "Button");
            Bind("CartCountText", count.rectTransform, "Text");

            Button settledPrev = UiElements.Button("SettledPrevButton", rail.transform, "button_text_action_normal_9s", "button_text_action_hover_9s", null, "button_text_action_disabled_9s", true);
            PlaceRight(settledPrev.GetComponent<RectTransform>(), 140f, 20f, 24f, 26f);
            UiElements.Icon("Icon", settledPrev.transform, "icon_left", UiTheme.PaperLight, 6f, 6f, 14f);
            Text settledPage = UiElements.PlaceText("SettledPageText", rail.transform, "1/1", 11, UiTheme.PaperLight, 0f, 0f, 46f, 28f, TextAnchor.MiddleCenter, true);
            PlaceRight(settledPage.rectTransform, 88f, 19f, 46f, 28f);
            Button settledNext = UiElements.Button("SettledNextButton", rail.transform, "button_text_action_normal_9s", "button_text_action_hover_9s", null, "button_text_action_disabled_9s", true);
            PlaceRight(settledNext.GetComponent<RectTransform>(), 60f, 20f, 24f, 26f);
            UiElements.Icon("Icon", settledNext.transform, "icon_right", UiTheme.PaperLight, 6f, 6f, 14f);
            settledPrev.gameObject.SetActive(false);
            settledPage.gameObject.SetActive(false);
            settledNext.gameObject.SetActive(false);
            Bind("SettledPrevButton", settledPrev.GetComponent<RectTransform>(), "Button");
            Bind("SettledPageText", settledPage.rectTransform, "Text");
            Bind("SettledNextButton", settledNext.GetComponent<RectTransform>(), "Button");

            RectTransform tabs = UiElements.Rect("CartTabs", rail.transform);
            UiElements.TopStretch(tabs, 72f, 40f, 13f, 13f);
            Image tabBaseline = UiElements.Image("Baseline", tabs, null, UiTheme.Hex("656B63"));
            UiElements.BottomStretch(tabBaseline.rectTransform, 0f, 1f);
            Button pending = UiElements.TextButton("PendingTabButton", tabs, "待结算", "text_action", 12, UiTheme.PaperLight);
            FillTab(pending.GetComponent<RectTransform>(), 0f, 0.5f);
            Button settled = UiElements.TextButton("SettledTabButton", tabs, "已结算", "text_action", 12, UiTheme.Hex("9DA69A"));
            FillTab(settled.GetComponent<RectTransform>(), 0.5f, 1f);
            Image pendingLine = UiElements.Image("ActiveLine", pending.transform, null, UiTheme.Red);
            SetTabLine(pendingLine.rectTransform);
            Image settledLine = UiElements.Image("ActiveLine", settled.transform, null, UiTheme.Red);
            SetTabLine(settledLine.rectTransform);
            settledLine.gameObject.SetActive(false);
            Bind("PendingTabButton", pending.GetComponent<RectTransform>(), "Button");
            Bind("SettledTabButton", settled.GetComponent<RectTransform>(), "Button");

            RectTransform listContent;
            ScrollRect list = UiElements.VerticalScroll("CartListScroll", rail.transform, out listContent, 10f, 4f);
            UiElements.Stretch(list.GetComponent<RectTransform>(), 17f, 110f, 12f, 193f);
            for (int i = 0; i < 2; i++)
            {
                GameObject entry = Instantiate("CartEntry", listContent);
                entry.name = "CartEntry" + i;
                RectTransform entryRoot = entry.GetComponent<RectTransform>();
                entryRoot.Find("Background").GetComponent<Image>().sprite = UiAssets.Sprite(i == 0 ? "card_cart_entry_long_9s" : "card_cart_entry_short_9s");
                entryRoot.Find("ItemImage").GetComponent<Image>().sprite = UiAssets.Sprite(i == 0 ? "01_飞天门辟邪玉" : "07_快报印花");
                entryRoot.Find("NameText").GetComponent<Text>().text = i == 0 ? "飞天门辟邪玉" : "快报印花";
                entryRoot.Find("TypeText").GetComponent<Text>().text = i == 0 ? "买入 · 货契" : "借货 · 借契";
                entryRoot.Find("SubtotalText").GetComponent<Text>().text = i == 0 ? "1,982 文" : "628 文";
            }
            Bind("CartListScroll", list.GetComponent<RectTransform>(), "ScrollRect");
            Bind("CartListContent", listContent, "VerticalLayoutGroup");

            Image footer = UiElements.Image("CartFooter", rail.transform, null, UiTheme.Alpha(Color.black, 0.24f), false, true);
            UiElements.BottomStretch(footer.rectTransform, 0f, 193f, 5f, 0f);
            Image footerDivider = UiElements.Image("FooterDivider", footer.transform, null, UiTheme.Hex("4D554D"));
            UiElements.TopStretch(footerDivider.rectTransform, 0f, 1f);
            Text buyCaption = UiElements.PlaceText("BuyCaptionText", footer.transform, "买入/还货 · 成交额", 11, UiTheme.Hex("AEB5AA"), 15f, 8f, 130f, 26f, TextAnchor.MiddleLeft);
            Text buyTotal = UiElements.PlaceText("BuyTotalText", footer.transform, "− 1,982", 15, UiTheme.PaperLight, 0f, 0f, 140f, 26f, TextAnchor.MiddleRight, true);
            buyTotal.resizeTextForBestFit = true;
            buyTotal.resizeTextMinSize = 10;
            buyTotal.resizeTextMaxSize = 15;
            PlaceRight(buyTotal.rectTransform, 15f, 8f, 140f, 26f);
            Text sellCaption = UiElements.PlaceText("SellCaptionText", footer.transform, "卖出/借货 · 成交额", 11, UiTheme.Hex("AEB5AA"), 15f, 35f, 130f, 26f, TextAnchor.MiddleLeft);
            Text sellTotal = UiElements.PlaceText("SellTotalText", footer.transform, "+ 628", 15, UiTheme.PaperLight, 0f, 0f, 140f, 26f, TextAnchor.MiddleRight, true);
            sellTotal.resizeTextForBestFit = true;
            sellTotal.resizeTextMinSize = 10;
            sellTotal.resizeTextMaxSize = 15;
            PlaceRight(sellTotal.rectTransform, 15f, 35f, 140f, 26f);
            Image totalDivider = UiElements.Image("TotalDivider", footer.transform, null, UiTheme.Hex("636B60"));
            UiElements.TopStretch(totalDivider.rectTransform, 65f, 1f, 15f, 15f);
            Text cashCaption = UiElements.PlaceText("CashCaptionText", footer.transform, "预计银两应付", 11, UiTheme.Hex("AEB5AA"), 15f, 70f, 106f, 36f, TextAnchor.MiddleLeft);
            Text cashDelta = UiElements.PlaceText("CashDeltaText", footer.transform, "1,354 文", 23, UiTheme.GoldLight, 0f, 0f, 150f, 42f, TextAnchor.MiddleRight, true);
            cashDelta.resizeTextForBestFit = true;
            cashDelta.resizeTextMinSize = 12;
            cashDelta.resizeTextMaxSize = 23;
            PlaceRight(cashDelta.rectTransform, 15f, 66f, 150f, 42f);
            Button checkout = UiElements.TextButton("CheckoutButton", footer.transform, "✓  立契结算", "checkout_red", 15, UiTheme.PaperLight);
            checkout.GetComponent<Image>().type = Image.Type.Simple;
            UiElements.BottomStretch(checkout.GetComponent<RectTransform>(), 33f, 43f, 15f, 15f);
            Shadow checkoutShadow = checkout.gameObject.AddComponent<Shadow>();
            checkoutShadow.effectColor = Color.black;
            checkoutShadow.effectDistance = new Vector2(4f, -4f);
            checkoutShadow.useGraphicAlpha = true;
            Text footerNote = UiElements.Text("FooterNoteText", footer.transform, "成交额不等于现金变动；确认时重新校验", 8, UiTheme.Hex("9CA498"), TextAnchor.MiddleCenter);
            UiElements.TopStretch(footerNote.rectTransform, 169f, 12f, 15f, 15f);
            footerNote.lineSpacing = 1.05f;
            footerNote.resizeTextForBestFit = true;
            footerNote.resizeTextMinSize = 7;
            footerNote.resizeTextMaxSize = 8;
            Bind("BuyCaptionText", buyCaption.rectTransform, "Text");
            Bind("SellCaptionText", sellCaption.rectTransform, "Text");
            Bind("BuyTotalText", buyTotal.rectTransform, "Text");
            Bind("SellTotalText", sellTotal.rectTransform, "Text");
            Bind("CashCaptionText", cashCaption.rectTransform, "Text");
            Bind("CashDeltaText", cashDelta.rectTransform, "Text");
            Bind("FooterNoteText", footerNote.rectTransform, "Text");
            Bind("CheckoutButton", checkout.GetComponent<RectTransform>(), "Button");
        }

        private static void PlaceCartCount(RectTransform rect)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-32f, -34f);
            rect.sizeDelta = new Vector2(30f, 28f);
            rect.localRotation = Quaternion.Euler(0f, 0f, -3f);
        }

        private static void PlaceRight(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void FillTab(RectTransform rect, float left, float right)
        {
            rect.anchorMin = new Vector2(left, 0f);
            rect.anchorMax = new Vector2(right, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetTabLine(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.12f, 0f);
            rect.anchorMax = new Vector2(0.88f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(0f, -2f);
            rect.offsetMax = new Vector2(0f, 2f);
        }
    }
}
