using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static partial class UiBuilder
    {
        private static RectTransform BuildInventorySlot()
        {
            Button rootButton = UiElements.Button("InventorySlot", null, null, "card_goods_hover", null, null, true);
            RectTransform root = rootButton.GetComponent<RectTransform>();
            currentRoot = root;
            UiElements.Center(root, 286f, 142f);
            LayoutElement slotLayout = UiElements.Layout(rootButton, 286f, 142f);
            slotLayout.minWidth = 286f;
            slotLayout.minHeight = 142f;
            Image hit = rootButton.GetComponent<Image>();
            hit.sprite = null;
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Bind("OpenButton", root, "Button");

            RectTransform motion = UiElements.Rect("MotionPivot", root);
            UiElements.Stretch(motion);
            RectTransform clip = UiElements.Rect("CardClip", motion);
            UiElements.Stretch(clip);
            clip.gameObject.AddComponent<RectMask2D>();
            Image background = UiElements.Image("Background", clip, "slot_inventory_long_9s", Color.white, true);
            UiElements.Stretch(background.rectTransform);
            rootButton.targetGraphic = background;
            Bind("Background", background.rectTransform, "Image");
            Bind("MotionPivot", motion, "RectTransform");

            RectTransform filled = UiElements.Rect("FilledContent", clip);
            UiElements.Stretch(filled, 0f, 0f, 0f, 38f);
            Image item = UiElements.Image("ItemImage", filled, "04_铁拳暖食盒", Color.white);
            RelativeFill(item.rectTransform, 0f, 0.42f, 10f, -2f, 6f, 4f);
            item.preserveAspect = true;
            Text profit = UiElements.Text("ProfitText", filled, "+474\n▲ +6.35%", 16, UiTheme.Red, TextAnchor.UpperLeft, true);
            RelativeTop(profit.rectTransform, 0.42f, 0.60f, 8f, 44f, 2f, 2f);
            Text name = UiElements.Text("NameText", filled, "铁拳暖食盒", 12, UiTheme.Ink, TextAnchor.UpperRight, true);
            RelativeTop(name.rectTransform, 0.60f, 1f, 5f, 22f, 2f, 6f);
            name.verticalOverflow = VerticalWrapMode.Truncate;
            Text quantity = UiElements.Text("QuantityText", filled, "×6", 30, UiTheme.Ink, TextAnchor.MiddleRight, true);
            RelativeTop(quantity.rectTransform, 0.60f, 1f, 26f, 34f, 2f, 6f);
            Text cost = UiElements.Text(
                "CostText",
                filled,
                "<size=9>购入 </size><b>1,245 文</b>\n<size=9>现价 </size><b>1,324 文</b>",
                12,
                UiTheme.Muted,
                TextAnchor.UpperRight);
            RelativeTop(cost.rectTransform, 0.60f, 1f, 61f, 32f, 2f, 6f);
            cost.lineSpacing = 1f;
            cost.horizontalOverflow = HorizontalWrapMode.Overflow;
            cost.verticalOverflow = VerticalWrapMode.Truncate;
            Text meta = UiElements.Text("MetaText", clip, "", 11, UiTheme.Muted, TextAnchor.LowerLeft);
            meta.gameObject.SetActive(false);
            UiElements.BottomStretch(meta.rectTransform, 3f, 32f, 6f, 6f);
            meta.lineSpacing = 1f;
            meta.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform actions = UiElements.Rect("Actions", clip);
            UiElements.BottomStretch(actions, 0f, 38f);
            CanvasGroup actionsGroup = actions.gameObject.AddComponent<CanvasGroup>();
            actionsGroup.alpha = 0f;
            actionsGroup.interactable = false;
            actionsGroup.blocksRaycasts = false;
            Button view = UiElements.TextButton("ViewButton", actions, "看行情", "text_action", 12, UiTheme.PaperLight);
            view.GetComponent<Image>().sprite = null;
            UiElements.SetColorTint(view, UiTheme.InkSoft, UiTheme.Celadon, UiTheme.Gold);
            RelativeBottomHalf(view.GetComponent<RectTransform>(), true);
            Image viewIcon = UiElements.Icon("ViewIcon", view.transform, "icon_chart", UiTheme.PaperLight, 9f, 9f, 19f);
            viewIcon.preserveAspect = true;
            UiElements.Stretch(view.transform.Find("Label").GetComponent<RectTransform>(), 34f, 1f, 5f, 1f);
            Button close = UiElements.TextButton("ClosePositionButton", actions, "平仓", "contract_red", 12, UiTheme.PaperLight);
            close.GetComponent<Image>().sprite = null;
            UiElements.SetColorTint(close, UiTheme.InkSoft, UiTheme.Red, UiTheme.RedDark);
            RelativeBottomHalf(close.GetComponent<RectTransform>(), false);
            Image closeIcon = UiElements.Icon("CloseIcon", close.transform, "icon_trade", UiTheme.PaperLight, 9f, 9f, 19f);
            closeIcon.preserveAspect = true;
            UiElements.Stretch(close.transform.Find("Label").GetComponent<RectTransform>(), 34f, 1f, 5f, 1f);
            Image divider = UiElements.Image("ActionDivider", actions, null, UiTheme.Gold);
            divider.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            divider.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            divider.rectTransform.pivot = new Vector2(0.5f, 0f);
            divider.rectTransform.anchoredPosition = Vector2.zero;
            divider.rectTransform.sizeDelta = new Vector2(4f, 43f);
            divider.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);

            Text empty = UiElements.Text("EmptyMark", clip, "空", 25, UiTheme.Alpha(UiTheme.Ink, 0.16f), TextAnchor.MiddleCenter);
            UiElements.Stretch(empty.rectTransform, 0f, 0f, 0f, 38f);
            empty.gameObject.SetActive(false);
            RectTransform focus = UiElements.Rect("FocusFrame", motion);
            UiElements.Stretch(focus, -3f, -3f, -3f, -3f);
            Image focusTop = UiElements.Image("Top", focus, null, UiTheme.GoldLight);
            UiElements.TopStretch(focusTop.rectTransform, 0f, 3f);
            Image focusBottom = UiElements.Image("Bottom", focus, null, UiTheme.GoldLight);
            UiElements.BottomStretch(focusBottom.rectTransform, 0f, 3f);
            Image focusLeft = UiElements.Image("Left", focus, null, UiTheme.GoldLight);
            UiElements.LeftStretch(focusLeft.rectTransform, 0f, 3f, 3f, 3f);
            Image focusRight = UiElements.Image("Right", focus, null, UiTheme.GoldLight);
            UiElements.RightStretch(focusRight.rectTransform, 0f, 3f, 3f, 3f);
            focus.gameObject.SetActive(false);

            Bind("FilledContent", filled, "RectTransform");
            Bind("ItemImage", item.rectTransform, "Image");
            Bind("ProfitText", profit.rectTransform, "Text");
            Bind("NameText", name.rectTransform, "Text");
            Bind("QuantityText", quantity.rectTransform, "Text");
            Bind("CostText", cost.rectTransform, "Text");
            Bind("MetaText", meta.rectTransform, "Text");
            Bind("Actions", actions, "RectTransform");
            Bind("ViewButton", view.GetComponent<RectTransform>(), "Button");
            Bind("ClosePositionButton", close.GetComponent<RectTransform>(), "Button");
            Bind("CloseIcon", closeIcon.rectTransform, "Image");
            Bind("EmptyMark", empty.rectTransform, "Text");
            Bind("FocusFrame", focus, "RectTransform");
            return root;
        }

        private static void RelativeFill(
            RectTransform rect,
            float left,
            float right,
            float top,
            float bottom,
            float leftPadding,
            float rightPadding)
        {
            rect.anchorMin = new Vector2(left, 0f);
            rect.anchorMax = new Vector2(right, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(leftPadding, bottom);
            rect.offsetMax = new Vector2(-rightPadding, -top);
        }

        private static void RelativeTop(
            RectTransform rect,
            float left,
            float right,
            float top,
            float height,
            float leftPadding,
            float rightPadding)
        {
            rect.anchorMin = new Vector2(left, 1f);
            rect.anchorMax = new Vector2(right, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(leftPadding, -top - height);
            rect.offsetMax = new Vector2(-rightPadding, -top);
        }

        private static void RelativeBottomHalf(RectTransform rect, bool left)
        {
            rect.anchorMin = new Vector2(left ? 0f : 0.5f, 0f);
            rect.anchorMax = new Vector2(left ? 0.5f : 1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left ? 0f : 2f, 0f);
            rect.offsetMax = new Vector2(left ? -2f : 0f, 38f);
        }

        private static RectTransform BuildInventoryWindow()
        {
            RectTransform window;
            RectTransform root = AddWindowShell("InventoryWindow", out window);
            RectTransform page = UiElements.Rect("ProfilePage", window);
            UiElements.Stretch(page, 26f, 108f, 26f, 42f);
            RectTransform header = UiElements.Rect("ProfileHeader", page);
            UiElements.TopStretch(header, 0f, 72f);
            Image emblemBack = UiElements.Image("EmblemBack", header, null, UiTheme.Ink);
            UiElements.Place(emblemBack.rectTransform, 0f, 3f, 56f, 56f);
            emblemBack.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            UiElements.Icon("Emblem", emblemBack.transform, "icon_bag", UiTheme.PaperLight, 12f, 10f, 32f);
            UiElements.PlaceText("Title", header, "行商货囊", 31, UiTheme.Ink, 76f, 0f, 320f, 44f, TextAnchor.MiddleLeft, true);
            UiElements.PlaceText("Subtitle", header, "MOD PROFILE · 货囊与资产", 13, UiTheme.Muted, 78f, 43f, 360f, 24f);
            Button inventoryTab = BuildProfileTab(header, "InventoryTabButton", "InventoryTabActiveLine", "货囊", -188f, 100f, true);
            Button assetsTab = BuildProfileTab(header, "AssetsTabButton", "AssetsTabActiveLine", "资产分析", -66f, 120f, false);
            Button close = UiElements.Button("ProfileCloseButton", header, "button_paper_icon_normal", "button_paper_icon_hover", "button_paper_icon_pressed", "button_paper_icon_disabled");
            close.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 1f);
            close.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            close.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
            close.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -4f);
            close.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 48f);
            Image closeIcon = UiElements.Image("Icon", close.transform, "icon_close", UiTheme.Ink);
            UiElements.Center(closeIcon.rectTransform, 26f, 26f);
            closeIcon.preserveAspect = true;
            Image headerLine = UiElements.Image("HeaderLine", page, "brush_ink_long", Color.white, true);
            UiElements.TopStretch(headerLine.rectTransform, 72f, 4f);
            Bind("InventoryTabButton", inventoryTab.GetComponent<RectTransform>(), "Button");
            Bind("AssetsTabButton", assetsTab.GetComponent<RectTransform>(), "Button");
            Bind("ProfileCloseButton", close.GetComponent<RectTransform>(), "Button");

            RectTransform content = UiElements.Rect("ProfileContent", page);
            UiElements.Stretch(content, 0f, 86f, 0f, 0f);
            Bind("ProfileContent", content, "RectTransform");
            BuildInventorySummary(content);
            RectTransform sections = UiElements.Rect("Sections", content);
            UiElements.Stretch(sections, 0f, 82f, 0f, 0f);
            VerticalLayoutGroup sectionLayout = sections.gameObject.AddComponent<VerticalLayoutGroup>();
            sectionLayout.spacing = 12f;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = true;
            sectionLayout.childAlignment = TextAnchor.UpperLeft;
            BuildInventorySection(sections, "Long", true);
            BuildInventorySection(sections, "Short", false);
            return root;
        }

        private static Button BuildProfileTab(
            RectTransform header,
            string name,
            string activeLineKey,
            string label,
            float right,
            float width,
            bool active)
        {
            Button tab = UiElements.Button(name, header, "tab_range_normal_9s", "tab_range_hover_9s", "tab_range_active_9s", null, true);
            Text tabLabel = UiElements.Text("Label", tab.transform, label, 16, active ? UiTheme.Ink : UiTheme.Muted, TextAnchor.MiddleCenter, true);
            UiElements.Stretch(tabLabel.rectTransform, 4f, 1f, 4f, 1f);
            Image activeLine = UiElements.Image("ActiveLine", tab.transform, null, UiTheme.Red);
            UiElements.BottomStretch(activeLine.rectTransform, 0f, 4f, 14f, 14f);
            activeLine.transform.localRotation = Quaternion.Euler(0f, 0f, -2f);
            activeLine.gameObject.SetActive(active);
            tab.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 1f);
            tab.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            tab.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
            tab.GetComponent<RectTransform>().anchoredPosition = new Vector2(right, -4f);
            tab.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 42f);
            Bind(activeLineKey, activeLine.rectTransform, "RectTransform");
            return tab;
        }

        private static void BuildInventorySummary(RectTransform parent)
        {
            Image summary = UiElements.Image("Summary", parent, null, UiTheme.Ink, false, true);
            UiElements.TopStretch(summary.rectTransform, 0f, 72f);
            Text caption;
            Text goods;
            RectTransform goodsCell = BuildInventoryMetric(
                summary.transform, 0, "Goods", "持货总市值", 13, "7,944 文", 26,
                UiTheme.GoldLight, TextAnchor.MiddleLeft, out caption, out goods);
            Text total;
            RectTransform totalCell = BuildInventoryMetric(
                summary.transform, 1, "Total", "总盈亏", 13, "+712 文 · ▲ +6.89%", 19,
                UiTheme.Red, TextAnchor.MiddleLeft, out caption, out total);
            Text weekly;
            RectTransform weeklyCell = BuildInventoryMetric(
                summary.transform, 2, "Weekly", "本周盈亏", 13, "+284 文 · ▲ +4.08%", 19,
                UiTheme.Red, TextAnchor.MiddleLeft, out caption, out weekly);
            Text capacityCaption;
            Text capacity;
            RectTransform capacityCell = BuildInventoryMetric(
                summary.transform, 3, "Capacity", "货囊容量 · 体力 30", 13, "3/5 格", 20,
                UiTheme.GoldLight, TextAnchor.MiddleLeft, out capacityCaption, out capacity);
            Text weightCaption;
            Text weight;
            RectTransform weightCell = BuildInventoryMetric(
                summary.transform, 4, "Weight", "单格负重 · 轻功 10 / 内功 10", 12, "100 / 格", 20,
                UiTheme.PaperLight, TextAnchor.MiddleLeft, out weightCaption, out weight);
            Text loans;
            RectTransform loansCell = BuildInventoryMetric(
                summary.transform, 5, "Loans", "待还借货市值", 13, "− 3,980 文", 19,
                UiTheme.Green, TextAnchor.MiddleRight, out caption, out loans);
            Bind("SummaryGoodsCell", goodsCell, "RectTransform");
            Bind("SummaryTotalCell", totalCell, "RectTransform");
            Bind("SummaryWeeklyCell", weeklyCell, "RectTransform");
            Bind("SummaryCapacityCell", capacityCell, "RectTransform");
            Bind("SummaryWeightCell", weightCell, "RectTransform");
            Bind("SummaryLoansCell", loansCell, "RectTransform");
            Bind("GoodsValueText", goods.rectTransform, "Text");
            Bind("TotalProfitText", total.rectTransform, "Text");
            Bind("WeeklyProfitText", weekly.rectTransform, "Text");
            Bind("CapacityCaptionText", capacityCaption.rectTransform, "Text");
            Bind("CapacityText", capacity.rectTransform, "Text");
            Bind("WeightCaptionText", weightCaption.rectTransform, "Text");
            Bind("WeightText", weight.rectTransform, "Text");
            Bind("LoansValueText", loans.rectTransform, "Text");
        }

        private static RectTransform BuildInventoryMetric(
            Transform parent,
            int index,
            string name,
            string caption,
            int captionSize,
            string value,
            int valueSize,
            Color valueColor,
            TextAnchor alignment,
            out Text captionText,
            out Text valueText)
        {
            RectTransform cell = UiElements.Rect(name + "Cell", parent);
            cell.anchorMin = new Vector2(index / 6f, 0f);
            cell.anchorMax = new Vector2((index + 1) / 6f, 1f);
            cell.pivot = new Vector2(0.5f, 0.5f);
            cell.offsetMin = new Vector2(index == 0 ? 18f : 6f, 0f);
            cell.offsetMax = new Vector2(index == 5 ? -18f : -6f, 0f);
            captionText = UiElements.Text(
                name + "Caption", cell, caption, captionSize, UiTheme.Hex("AEB5AA"), alignment);
            UiElements.TopStretch(captionText.rectTransform, 4f, 20f);
            captionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            captionText.verticalOverflow = VerticalWrapMode.Overflow;
            valueText = UiElements.Text(name + "ValueText", cell, value, valueSize, valueColor, alignment, true);
            UiElements.TopStretch(valueText.rectTransform, 27f, 36f);
            valueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            valueText.verticalOverflow = VerticalWrapMode.Overflow;
            return cell;
        }

        private static void BuildInventorySection(RectTransform parent, string name, bool isLong)
        {
            RectTransform section = UiElements.Rect(name + "Section", parent);
            Image background = section.gameObject.AddComponent<Image>();
            background.sprite = UiAssets.Sprite("slot_inventory_empty_9s");
            background.type = Image.Type.Sliced;
            background.color = UiTheme.Alpha(UiTheme.PaperLight, 0.74f);
            background.raycastTarget = true;
            LayoutElement sectionLayoutElement = section.gameObject.AddComponent<LayoutElement>();
            sectionLayoutElement.preferredHeight = 200f;
            sectionLayoutElement.flexibleHeight = 1f;
            Bind(name + "Section", section, "LayoutElement");
            RectTransform header = UiElements.Rect("Header", section);
            UiElements.TopStretch(header, 6f, 38f, 16f, 16f);
            UiElements.PlaceText("Title", header, isLong ? "货 · 做多" : "借货 · 做空", 18, isLong ? UiTheme.Red : UiTheme.Green, 0f, 0f, 150f, 34f, TextAnchor.MiddleLeft, true);
            Text summary = UiElements.PlaceText("SummaryText", header, isLong ? "持货 8 件 / 成本 7,470 文" : "待还 2 件 / 实收 4,218 文", 12, UiTheme.Muted, 160f, 2f, 480f, 30f, TextAnchor.MiddleLeft);
            Button closeAll = UiElements.TextButton("CloseAll" + name + "Button", header, "全部平仓", "text_action", 13, isLong ? UiTheme.Red : UiTheme.Green);
            closeAll.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 1f);
            closeAll.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            closeAll.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
            closeAll.GetComponent<RectTransform>().anchoredPosition = new Vector2(-144f, -1f);
            closeAll.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 32f);
            Text profit = UiElements.PlaceText("ProfitText", header, isLong ? "浮动 +474" : "浮动 +238", 16, UiTheme.Red, 0f, 0f, 136f, 34f, TextAnchor.MiddleRight, true);
            profit.rectTransform.anchorMin = new Vector2(1f, 1f);
            profit.rectTransform.anchorMax = new Vector2(1f, 1f);
            profit.rectTransform.pivot = new Vector2(1f, 1f);
            profit.rectTransform.anchoredPosition = new Vector2(0f, -1f);

            RectTransform content;
            ScrollRect scroll = UiElements.VerticalScroll(name + "GridScroll", section, out content, 8f, 0f);
            UiElements.Stretch(scroll.GetComponent<RectTransform>(), 16f, 48f, 16f, 10f);
            Object.DestroyImmediate(content.GetComponent<VerticalLayoutGroup>());
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(286f, 142f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            int count = isLong ? 3 : 2;
            for (int i = 0; i < count; i++)
            {
                GameObject slot = Instantiate("InventorySlot", content);
                slot.name = "InventorySlot" + i.ToString("00");
                bool filled = isLong ? i < 2 : i == 0;
                if (filled)
                    SetInventorySlotSample(slot.GetComponent<RectTransform>(), isLong, i + 1);
                else
                    SetInventorySlotEmpty(slot.GetComponent<RectTransform>());
            }
            Text emptyState = UiElements.Text(name + "EmptyState", scroll.transform, "暂无仓位", 15, UiTheme.Muted, TextAnchor.MiddleCenter);
            UiElements.Stretch(emptyState.rectTransform, 12f, 12f, 12f, 12f);
            emptyState.gameObject.SetActive(false);
            Bind(name + "SummaryText", summary.rectTransform, "Text");
            Bind("CloseAll" + name + "Button", closeAll.GetComponent<RectTransform>(), "Button");
            Bind(name + "ProfitText", profit.rectTransform, "Text");
            Bind(name + "GridScroll", scroll.GetComponent<RectTransform>(), "ScrollRect");
            Bind(name + "GridContent", content, "GridLayoutGroup");
            Bind(name + "EmptyStateText", emptyState.rectTransform, "Text");
        }

        private static void SetInventorySlotSample(RectTransform slot, bool isLong, int slotIndex)
        {
            Transform clip = slot.Find("MotionPivot/CardClip");
            Image background = clip.Find("Background").GetComponent<Image>();
            background.sprite = UiAssets.Sprite(isLong ? "slot_inventory_long_9s" : "slot_inventory_short_9s");
            Transform filled = clip.Find("FilledContent");
            filled.Find("ItemImage").GetComponent<Image>().sprite = UiAssets.Sprite(isLong ? "04_铁拳暖食盒" : "19_盆砂");
            filled.Find("NameText").GetComponent<Text>().text = isLong ? "铁拳暖食盒" : "盆砂";
            filled.Find("QuantityText").GetComponent<Text>().text = !isLong || slotIndex == 2 ? "×2" : "×6";
            filled.Find("ProfitText").GetComponent<Text>().text = isLong ? "+237\n▲ +3.18%" : "+238\n▲ +5.64%";
            filled.Find("CostText").GetComponent<Text>().text = isLong
                ? "<size=9>购入 </size><b>1,245 文</b>\n<size=9>现价 </size><b>1,324 文</b>"
                : "<size=9>实收 </size><b>2,109 文</b>\n<size=9>现价 </size><b>1,990 文</b>";
        }

        private static void SetInventorySlotEmpty(RectTransform slot)
        {
            Transform clip = slot.Find("MotionPivot/CardClip");
            clip.Find("Background").GetComponent<Image>().sprite = UiAssets.Sprite("slot_inventory_empty_9s");
            slot.GetComponent<Button>().interactable = false;
            clip.Find("FilledContent").gameObject.SetActive(false);
            clip.Find("MetaText").gameObject.SetActive(false);
            clip.Find("Actions").gameObject.SetActive(false);
            clip.Find("EmptyMark").gameObject.SetActive(true);
        }
    }
}
