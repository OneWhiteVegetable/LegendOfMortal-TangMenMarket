using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static partial class UiBuilder
    {
        private static RectTransform BuildItemDetail()
        {
            RectTransform window;
            RectTransform root = AddWindowShell("ItemDetail", out window);
            AddTicker(window);
            RectTransform page = UiElements.Rect("DetailPage", window);
            UiElements.Stretch(page, 0f, 126f, 0f, 30f);
            Bind("DetailPage", page, "RectTransform");

            RectTransform workspace = UiElements.Rect("ChartWorkspace", page);
            UiElements.Stretch(workspace, 28f, 20f, 360f, 16f);
            RectTransform heading = UiElements.Rect("Heading", workspace);
            UiElements.TopStretch(heading, 0f, 100f);
            Button back = UiElements.Button("BackButton", heading, "button_brush_arrow_normal", "button_brush_arrow_hover", "button_brush_arrow_pressed", "button_brush_arrow_disabled");
            UiElements.Place(back.GetComponent<RectTransform>(), 0f, 17f, 54f, 52f);
            Image backIcon = UiElements.Image("Icon", back.transform, "icon_left", UiTheme.Ink);
            UiElements.Center(backIcon.rectTransform, 34f, 34f);
            backIcon.preserveAspect = true;
            Text eyebrow = UiElements.PlaceText("ItemKeyText", heading, "ITEM / MOD:01", 9, UiTheme.Muted, 72f, 3f, 450f, 23f, TextAnchor.MiddleLeft, true);
            PlaceDetailHeadingSpan(eyebrow.rectTransform, 0f, 0.62f, 72f, 3f, 8f, 23f);
            Text title = UiElements.PlaceText("NameText", heading, "飞天门辟邪玉", 43, UiTheme.Ink, 72f, 27f, 690f, 62f, TextAnchor.MiddleLeft, true);
            PlaceDetailHeadingSpan(title.rectTransform, 0f, 0.62f, 72f, 27f, 8f, 62f);
            title.verticalOverflow = VerticalWrapMode.Truncate;
            Image trend = UiElements.Image("TrendIcon", heading, "trend_up_brush", Color.white);
            UiElements.Place(trend.rectTransform, 760f, 30f, 60f, 48f);
            trend.rectTransform.anchorMin = new Vector2(0.63f, 1f);
            trend.rectTransform.anchorMax = new Vector2(0.63f, 1f);
            trend.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            trend.preserveAspect = true;
            trend.raycastTarget = true;
            Button trendButton = trend.gameObject.AddComponent<Button>();
            trendButton.targetGraphic = trend;
            trendButton.transition = Selectable.Transition.ColorTint;
            UiElements.SetColorTint(trendButton, Color.white, new Color(1.08f, 1.08f, 1.08f, 1f), new Color(0.88f, 0.88f, 0.88f, 1f));
            ColorBlock trendColors = trendButton.colors;
            trendColors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            trendButton.colors = trendColors;
            Text price = UiElements.PlaceText("PriceText", heading, "991 <size=12><color=#6B726B>文</color></size>", 58, UiTheme.Red, 940f, 0f, 450f, 65f, TextAnchor.MiddleRight, true);
            PlaceDetailHeadingSpan(price.rectTransform, 0.68f, 1f, 16f, 0f, 0f, 65f);
            price.fontStyle = FontStyle.BoldAndItalic;
            price.horizontalOverflow = HorizontalWrapMode.Overflow;
            price.verticalOverflow = VerticalWrapMode.Overflow;
            price.lineSpacing = 0.95f;
            Text delta = UiElements.PlaceText("DeltaText", heading, "+60 文", 12, UiTheme.Red, 990f, 69f, 170f, 25f, TextAnchor.MiddleRight, true);
            PlaceDetailHeadingSpan(delta.rectTransform, 0.68f, 0.84f, 16f, 69f, 8f, 25f);
            Text change = UiElements.PlaceText("ChangeText", heading, "▲ +6.44%", 18, UiTheme.Red, 1160f, 65f, 230f, 31f, TextAnchor.MiddleRight, true);
            PlaceDetailHeadingSpan(change.rectTransform, 0.84f, 1f, 8f, 65f, 0f, 31f);
            price.rectTransform.SetAsLastSibling();
            delta.rectTransform.SetAsLastSibling();
            change.rectTransform.SetAsLastSibling();
            Image headingLine = UiElements.Image("HeadingLine", workspace, "brush_ink_long", Color.white, true);
            UiElements.TopStretch(headingLine.rectTransform, 99f, 2f);

            RectTransform toolbar = UiElements.Rect("ChartToolbar", workspace);
            UiElements.TopStretch(toolbar, 108f, 43f);
            Image captionMark = UiElements.Image("CaptionMark", toolbar, null, UiTheme.Ink);
            UiElements.Place(captionMark.rectTransform, 0f, 18f, 18f, 5f);
            captionMark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Text chartCaption = UiElements.PlaceText("ChartCaption", toolbar, "逐周墨笔 · 红涨绿跌", 11, UiTheme.Muted, 28f, 4f, 330f, 34f, TextAnchor.MiddleLeft);
            chartCaption.font = UiAssets.SerifFont;
            string[] labels = { "近月", "半年", "一年", "全部" };
            string[] keys = { "RangeMonthButton", "RangeHalfYearButton", "RangeYearButton", "RangeAllButton" };
            for (int i = 0; i < 4; i++)
            {
                Button range = UiElements.Button(keys[i], toolbar, i == 1 ? "tab_range_active_9s" : "tab_range_normal_9s", "tab_range_hover_9s", "tab_range_active_9s", null, true);
                RectTransform rangeRect = range.GetComponent<RectTransform>();
                rangeRect.anchorMin = new Vector2(1f, 0.5f);
                rangeRect.anchorMax = new Vector2(1f, 0.5f);
                rangeRect.pivot = new Vector2(1f, 0.5f);
                rangeRect.anchoredPosition = new Vector2(-86f - (3 - i) * 69f, 0f);
                rangeRect.sizeDelta = new Vector2(66f, 29f);
                Text label = UiElements.Text("Label", range.transform, labels[i], 10, i == 1 ? UiTheme.PaperLight : UiTheme.Muted, TextAnchor.MiddleCenter, false);
                UiElements.Stretch(label.rectTransform, 3f, 1f, 3f, 1f);
                Bind(keys[i], rangeRect, "Button");
            }
            Button reset = UiElements.Button("ResetChartButton", toolbar, "button_text_action_normal_9s", "button_text_action_hover_9s", null, "button_text_action_disabled_9s", true);
            RectTransform resetRect = reset.GetComponent<RectTransform>();
            resetRect.anchorMin = new Vector2(1f, 0.5f);
            resetRect.anchorMax = new Vector2(1f, 0.5f);
            resetRect.pivot = new Vector2(1f, 0.5f);
            resetRect.anchoredPosition = Vector2.zero;
            resetRect.sizeDelta = new Vector2(78f, 30f);
            UiElements.SetColorTint(reset, UiTheme.Alpha(UiTheme.Paper, 0.1f), UiTheme.Alpha(UiTheme.Gold, 0.15f), UiTheme.Alpha(UiTheme.Gold, 0.25f));
            UiElements.Icon("Icon", reset.transform, "icon_reset", UiTheme.Muted, 7f, 8f, 13f);
            UiElements.PlaceText("Label", reset.transform, "复位", 9, UiTheme.Muted, 25f, 0f, 47f, 30f, TextAnchor.MiddleLeft);

            Image chartPanel = UiElements.Image("ChartPanel", workspace, "panel_detail_chart_9s", UiTheme.Alpha(Color.white, 0.65f), true, true);
            UiElements.Stretch(chartPanel.rectTransform, 0f, 155f, 0f, 36f);
            GameObject chart = Instantiate("ChartHost", chartPanel.transform);
            chart.name = "ChartHost";
            UiElements.Stretch(chart.GetComponent<RectTransform>(), 12f, 12f, 12f, 12f);
            Bind("ChartHost", chart.GetComponent<RectTransform>(), "RectTransform");
            Text footnoteLeft = UiElements.PlaceText("FootnoteLeft", workspace, "━ 红涨    ━ 绿跌", 9, UiTheme.Muted, 0f, 0f, 280f, 30f, TextAnchor.MiddleLeft);
            footnoteLeft.rectTransform.anchorMin = Vector2.zero;
            footnoteLeft.rectTransform.anchorMax = Vector2.zero;
            footnoteLeft.rectTransform.pivot = Vector2.zero;
            footnoteLeft.rectTransform.anchoredPosition = Vector2.zero;
            Text footnote = UiElements.Text("ChartHintText", workspace, "拖动看前期 · 滚轮缩放 · 尺线亦可操作", 9, UiTheme.Muted, TextAnchor.MiddleRight);
            UiElements.BottomStretch(footnote.rectTransform, 0f, 30f, 250f, 0f);

            BuildDetailSidebar(page);
            Bind("BackButton", back.GetComponent<RectTransform>(), "Button");
            Bind("ItemKeyText", eyebrow.rectTransform, "Text");
            Bind("NameText", title.rectTransform, "Text");
            Bind("TrendIcon", trend.rectTransform, "Image");
            Bind("PriceText", price.rectTransform, "Text");
            Bind("DeltaText", delta.rectTransform, "Text");
            Bind("ChangeText", change.rectTransform, "Text");
            Bind("ChartCaptionText", chartCaption.rectTransform, "Text");
            Bind("ResetChartButton", resetRect, "Button");
            Bind("ChartHintText", footnote.rectTransform, "Text");
            return root;
        }

        private static void BuildDetailSidebar(RectTransform page)
        {
            Image sidebar = UiElements.Image("Sidebar", page, "panel_cart_rail_9s", Color.white, true, true);
            UiElements.RightStretch(sidebar.rectTransform, 0f, 360f);
            RectTransform content;
            ScrollRect scroll = UiElements.VerticalScroll("SidebarScroll", sidebar.transform, out content, 0f, 0f);
            UiElements.Stretch(scroll.GetComponent<RectTransform>(), 0f, 0f, 0f, 168f);
            Bind("SidebarScroll", scroll.GetComponent<RectTransform>(), "ScrollRect");
            Bind("SidebarContent", content, "VerticalLayoutGroup");

            RectTransform visual = UiElements.Rect("ItemStage", content);
            UiElements.Layout(visual, -1f, 220f, 1f, 0f);
            Image paper = UiElements.Image("StagePaper", visual, "detail_stage_paper", Color.white);
            UiElements.Place(paper.rectTransform, 28f, 27f, 260f, 178f);
            Image shard = UiElements.Image("RedShard", visual, "detail_stage_red_shard", Color.white);
            UiElements.Place(shard.rectTransform, 235f, 18f, 94f, 152f);
            Text ghost = UiElements.PlaceDisplayText("GhostNumber", visual, "01", 108, UiTheme.Alpha(UiTheme.PaperLight, 0.32f), 8f, -5f, 180f, 110f, TextAnchor.UpperLeft);
            ghost.fontStyle = FontStyle.Italic;
            Image shadow = UiElements.Image("ItemShadowImage", visual, "01_飞天门辟邪玉", new Color(0f, 0f, 0f, 0.17f));
            PlaceDetailStageImage(shadow.rectTransform, new Vector2(6f, -8f));
            shadow.preserveAspect = true;
            Image item = UiElements.Image("ItemImage", visual, "01_飞天门辟邪玉", Color.white);
            PlaceDetailStageImage(item.rectTransform, Vector2.zero);
            item.preserveAspect = true;
            Image indexStage = UiElements.Image("IndexStageImage", visual, "icon_chart", UiTheme.Ink);
            UiElements.Center(indexStage.rectTransform, 110f, 110f);
            indexStage.rectTransform.anchoredPosition = new Vector2(0f, 7f);
            indexStage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -5f);
            indexStage.preserveAspect = true;
            indexStage.gameObject.SetActive(false);
            Image stageSlash = UiElements.Image("StageSlash", visual, "detail_stage_gold_slash", UiTheme.Alpha(Color.white, 0.8f));
            UiElements.Place(stageSlash.rectTransform, 20f, 184f, 158f, 9f);
            stageSlash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -5f);

            RectTransform description = SidebarSection(content, "DescriptionSection", "货物小传", 166f);
            Image descriptionViewport = UiElements.Image("DescriptionScroll", description, null, Color.clear);
            UiElements.Place(descriptionViewport.rectTransform, 20f, 36f, 320f, 118f);
            descriptionViewport.raycastTarget = true;
            descriptionViewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect descriptionScroll = descriptionViewport.gameObject.AddComponent<ScrollRect>();
            descriptionScroll.horizontal = false;
            descriptionScroll.vertical = true;
            descriptionScroll.movementType = ScrollRect.MovementType.Clamped;
            descriptionScroll.scrollSensitivity = 24f;
            descriptionScroll.viewport = descriptionViewport.rectTransform;
            RectTransform descriptionContent = UiElements.Rect("Content", descriptionViewport.transform);
            descriptionContent.anchorMin = new Vector2(0f, 1f);
            descriptionContent.anchorMax = new Vector2(1f, 1f);
            descriptionContent.pivot = new Vector2(0.5f, 1f);
            descriptionContent.offsetMin = Vector2.zero;
            descriptionContent.offsetMax = Vector2.zero;
            descriptionScroll.content = descriptionContent;
            Text descriptionText = UiElements.Text("DescriptionText", descriptionContent, "崆峒飞天门监制，据称经掌门真人亲手开光，佩之百邪不侵。玉是好玉，就是不太管用——那便下回再请一块便是。坊间盛传此玉专克妖物；至于人家究竟是人是妖，答复向来是：信则有，诚则灵。", 12, UiTheme.Hex("D0D2C7"), TextAnchor.UpperLeft);
            descriptionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            descriptionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            descriptionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            descriptionText.rectTransform.offsetMin = Vector2.zero;
            descriptionText.rectTransform.offsetMax = Vector2.zero;
            descriptionText.rectTransform.sizeDelta = new Vector2(0f, 108f);
            descriptionText.font = UiAssets.SerifFont;
            descriptionText.lineSpacing = 1.85f;
            Bind("DescriptionSection", description, "RectTransform");
            Bind("DescriptionTitleText", description.Find("Title").GetComponent<RectTransform>(), "Text");

            RectTransform news = SidebarSection(content, "NewsSection", "本周行情解读", 254f);
            Text newsDirection = UiElements.PlaceText("NewsDirectionText", news, "▲ +6.44% · +60 文", 11, UiTheme.Red, 20f, 35f, 320f, 23f, TextAnchor.MiddleLeft, true);
            Image newsPaper = UiElements.Image("NewsPaper", news, null, UiTheme.Alpha(UiTheme.Paper, 0.07f));
            UiElements.Stretch(newsPaper.rectTransform, 12f, 64f, 12f, 8f);
            Image newsGoldEdge = UiElements.Image("GoldEdge", newsPaper.transform, null, UiTheme.Gold);
            UiElements.LeftStretch(newsGoldEdge.rectTransform, 0f, 3f);
            Text eventTitle = UiElements.PlaceText("EventTitleText", news, "妖讯南来", 11, UiTheme.GoldLight, 31f, 73f, 298f, 20f, TextAnchor.MiddleLeft, true);
            Text newsBody = UiElements.PlaceText("NewsBodyText", news, "飞鸽夜入崆峒：江陵城被魔教匪众围了。妖讯所过之处，飞天门辟邪玉一日三价，市集玉摊前长龙排过了铁拳巷口。", 11, UiTheme.Hex("D0D2C7"), 31f, 99f, 298f, 72f);
            newsBody.font = UiAssets.SerifFont;
            newsBody.lineSpacing = 1.75f;

            RectTransform positions = SidebarSection(content, "PositionSection", "货囊账目", 156f);
            CreatePositionCell(positions, "Long", 20f, 38f, "持货 / 成本", "0 件 · 0", UiTheme.Red);
            CreatePositionCell(positions, "Short", 184f, 38f, "待还 / 借价与实收", "0 件\n借价 — · 97%实收 —", UiTheme.Green, 9);
            CreatePositionCell(positions, "Weekly", 20f, 96f, "本周涨跌", "▲ +6.44%", UiTheme.Gold);
            CreatePositionCell(positions, "Total", 184f, 96f, "开局至今", "▲ +21.30%", UiTheme.Gold);
            Bind("LongPositionText", positions.Find("Long/Value").GetComponent<RectTransform>(), "Text");
            Bind("ShortPositionText", positions.Find("Short/Value").GetComponent<RectTransform>(), "Text");
            Bind("WeeklyPositionText", positions.Find("Weekly/Value").GetComponent<RectTransform>(), "Text");
            Bind("TotalPositionText", positions.Find("Total/Value").GetComponent<RectTransform>(), "Text");

            RectTransform indexMetrics = SidebarSection(content, "IndexMetricsSection", "本周 / 累计", 102f);
            CreatePositionCell(indexMetrics, "Weekly", 20f, 38f, "本周指数", "▲ +6.44%", UiTheme.Gold);
            CreatePositionCell(indexMetrics, "Total", 184f, 38f, "开局至今", "▲ +21.30%", UiTheme.Gold);
            indexMetrics.gameObject.SetActive(false);
            Bind("IndexMetricsSection", indexMetrics, "RectTransform");
            Bind("IndexWeeklyText", indexMetrics.Find("Weekly/Value").GetComponent<RectTransform>(), "Text");
            Bind("IndexTotalText", indexMetrics.Find("Total/Value").GetComponent<RectTransform>(), "Text");

            RectTransform constituents = SidebarSection(content, "ConstituentSection", "成分货物", 84f);
            RectTransform constituentList = UiElements.Rect("ConstituentList", constituents);
            UiElements.Place(constituentList, 20f, 36f, 320f, 36f);
            UiElements.AddVerticalLayout(constituentList, 0, 0, 0, 0, 5f);
            Button constituentTemplate = UiElements.Button("ConstituentRowTemplate", constituentList, "panel_position_cell_9s", "panel_position_cell_9s", "panel_position_cell_9s", null, true);
            UiElements.Layout(constituentTemplate, -1f, 36f, 1f, 0f);
            UiElements.SetColorTint(constituentTemplate, Color.white, UiTheme.Hex("C5BDAA"), UiTheme.Gold);
            Image constituentIcon = UiElements.Image("Icon", constituentTemplate.transform, "01_飞天门辟邪玉", Color.white);
            UiElements.Place(constituentIcon.rectTransform, 7f, 5f, 25f, 25f);
            constituentIcon.preserveAspect = true;
            UiElements.PlaceText("Name", constituentTemplate.transform, "飞天门辟邪玉", 10, UiTheme.Hex("C7CCBF"), 41f, 3f, 188f, 30f, TextAnchor.MiddleLeft);
            UiElements.PlaceText("Change", constituentTemplate.transform, "▲ +6.44%", 10, UiTheme.GoldLight, 230f, 3f, 81f, 30f, TextAnchor.MiddleRight, true);
            Text constituentEmpty = UiElements.PlaceText("ConstituentEmptyText", constituents, "暂无可导航货物", 10, UiTheme.Hex("9DA69A"), 20f, 38f, 320f, 28f, TextAnchor.MiddleLeft);
            constituentEmpty.gameObject.SetActive(false);
            constituents.gameObject.SetActive(false);
            Bind("ConstituentSection", constituents, "RectTransform");
            Bind("ConstituentList", constituentList, "RectTransform");
            Bind("ConstituentRowTemplate", constituentTemplate.GetComponent<RectTransform>(), "Button");
            Bind("ConstituentEmptyText", constituentEmpty.rectTransform, "Text");

            RectTransform trade = UiElements.Rect("TradeSection", sidebar.transform);
            UiElements.Layout(trade, -1f, 168f, 1f, 0f);
            UiElements.BottomStretch(trade, 0f, 168f);
            Image tradeBackground = UiElements.Image("TradeBackground", trade, "panel_cart_rail_9s", Color.white, true, true);
            UiElements.Stretch(tradeBackground.rectTransform);
            Image tradeSeparator = UiElements.Image("Separator", trade, null, UiTheme.Hex("4C544B"));
            UiElements.TopStretch(tradeSeparator.rectTransform, 0f, 1f);
            UiElements.PlaceText("Title", trade, "立契数量", 10, UiTheme.Hex("9DA69A"), 20f, 8f, 180f, 28f, TextAnchor.MiddleLeft);
            RectTransform stepper = UiElements.Stepper("QuantityStepper", trade, "Quantity");
            stepper.anchorMin = new Vector2(1f, 1f);
            stepper.anchorMax = new Vector2(1f, 1f);
            stepper.pivot = new Vector2(1f, 1f);
            stepper.anchoredPosition = new Vector2(-20f, -2f);
            stepper.sizeDelta = new Vector2(112f, 34f);
            Text quantity = stepper.Find("QuantityText").GetComponent<Text>();
            quantity.fontSize = 12;
            Button buy = UiElements.TextButton("BuyButton", trade, "加入购物车\n<size=9>买货 · 做多</size>", "contract_red", 13, UiTheme.PaperLight);
            UiElements.Place(buy.GetComponent<RectTransform>(), 20f, 48f, 154f, 52f);
            UiElements.Icon("ActionIcon", buy.transform, "icon_cart", UiTheme.PaperLight, 13f, 14f, 24f);
            buy.transform.Find("Label").GetComponent<Text>().rectTransform.offsetMin = new Vector2(38f, 2f);
            Button borrow = UiElements.TextButton("BorrowButton", trade, "立下借货契\n<size=9>借货 · 做空</size>", "contract_green", 13, UiTheme.PaperLight);
            UiElements.Place(borrow.GetComponent<RectTransform>(), 186f, 48f, 154f, 52f);
            UiElements.Icon("ActionIcon", borrow.transform, "icon_scroll", UiTheme.PaperLight, 13f, 14f, 24f);
            borrow.transform.Find("Label").GetComponent<Text>().rectTransform.offsetMin = new Vector2(38f, 2f);
            Button sell = UiElements.TextButton("SellButton", trade, "卖出持货", "text_action", 10, UiTheme.Hex("C4C9BD"));
            UiElements.Place(sell.GetComponent<RectTransform>(), 20f, 111f, 154f, 36f);
            UiElements.Icon("ActionIcon", sell.transform, "icon_trade", UiTheme.Hex("C4C9BD"), 13f, 10f, 16f);
            sell.transform.Find("Label").GetComponent<Text>().rectTransform.offsetMin = new Vector2(32f, 2f);
            Button cover = UiElements.TextButton("CoverButton", trade, "归还借货", "text_action", 10, UiTheme.Hex("C4C9BD"));
            UiElements.Place(cover.GetComponent<RectTransform>(), 186f, 111f, 154f, 36f);
            UiElements.Icon("ActionIcon", cover.transform, "icon_return", UiTheme.Hex("C4C9BD"), 13f, 10f, 16f);
            cover.transform.Find("Label").GetComponent<Text>().rectTransform.offsetMin = new Vector2(32f, 2f);
            RectTransform tradeSpacer = UiElements.Rect("TradeSpacer", content);
            UiElements.Layout(tradeSpacer, -1f, 0f, 1f, 0f);
            tradeSpacer.gameObject.SetActive(false);
            Bind("TradeSection", trade, "RectTransform");
            Bind("TradeSpacer", tradeSpacer, "RectTransform");

            Bind("ItemImage", item.rectTransform, "Image");
            Bind("ItemShadowImage", shadow.rectTransform, "Image");
            Bind("GhostNumber", ghost.rectTransform, "Text");
            Bind("DescriptionText", descriptionText.rectTransform, "Text");
            Bind("NewsSection", news, "RectTransform");
            Bind("NewsDirectionText", newsDirection.rectTransform, "Text");
            Bind("EventTitleText", eventTitle.rectTransform, "Text");
            Bind("NewsBodyText", newsBody.rectTransform, "Text");
            Bind("BuyButton", buy.GetComponent<RectTransform>(), "Button");
            Bind("BorrowButton", borrow.GetComponent<RectTransform>(), "Button");
            Bind("SellButton", sell.GetComponent<RectTransform>(), "Button");
            Bind("CoverButton", cover.GetComponent<RectTransform>(), "Button");
        }

        private static void PlaceDetailHeadingSpan(
            RectTransform rect,
            float leftAnchor,
            float rightAnchor,
            float left,
            float top,
            float right,
            float height)
        {
            rect.anchorMin = new Vector2(leftAnchor, 1f);
            rect.anchorMax = new Vector2(rightAnchor, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void PlaceDetailStageImage(RectTransform rect, Vector2 shadowOffset)
        {
            rect.anchorMin = new Vector2(0.06f, 0.02f);
            rect.anchorMax = new Vector2(0.94f, 0.98f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = new Vector2(shadowOffset.x, 10f + shadowOffset.y);
        }

        private static RectTransform SidebarSection(RectTransform parent, string name, string title, float height)
        {
            RectTransform section = UiElements.Rect(name, parent);
            UiElements.Layout(section, -1f, height, 1f, 0f);
            Image separator = UiElements.Image("Separator", section, null, UiTheme.Hex("4C544B"));
            UiElements.TopStretch(separator.rectTransform, 0f, 1f, 18f, 18f);
            UiElements.PlaceText("Title", section, title, 11, UiTheme.GoldLight, 20f, 8f, 320f, 26f, TextAnchor.MiddleLeft, true);
            return section;
        }

        private static void CreatePositionCell(RectTransform parent, string name, float x, float y, string caption, string value, Color accent, int valueSize = 15)
        {
            Image cell = UiElements.Image(name, parent, "panel_position_cell_9s", UiTheme.Alpha(accent, 0.16f), true);
            UiElements.Place(cell.rectTransform, x, y, 156f, 50f);
            UiElements.PlaceText("Caption", cell.transform, caption, 9, UiTheme.Hex("9DA69A"), 10f, 2f, 136f, 20f, TextAnchor.MiddleLeft);
            bool compactValue = valueSize <= 10;
            Text valueText = UiElements.PlaceText("Value", cell.transform, value, valueSize, UiTheme.PaperLight, 10f, compactValue ? 18f : 21f, 136f, compactValue ? 30f : 26f, TextAnchor.MiddleLeft, true);
            if (compactValue)
                valueText.lineSpacing = 1.05f;
        }
    }
}
