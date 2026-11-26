using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static partial class UiBuilder
    {
        private static RectTransform BuildNewsTicker()
        {
            RectTransform root = SimpleRoot("NewsTicker", 1880f, 36f);
            Image background = UiElements.Image("Background", root, null, UiTheme.InkSoft, false, true);
            UiElements.Stretch(background.rectTransform);
            UiElements.Layout(root, -1f, 36f, 1f, 0f);
            Image label = UiElements.Image("LabelBackground", root, "panel_ticker_label", Color.white, false, true);
            UiElements.Place(label.rectTransform, 0f, 0f, 164f, 36f);
            UiElements.Icon("InfoIcon", label.transform, "icon_info", UiTheme.PaperLight, 14f, 8f, 20f);
            UiElements.PlaceText("LabelText", label.transform, "江湖快报", 13, UiTheme.PaperLight, 40f, 1f, 110f, 34f, TextAnchor.MiddleLeft, true);

            RectTransform viewport = UiElements.Rect("Viewport", root);
            UiElements.Stretch(viewport, 164f, 0f, 0f, 0f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform track = UiElements.Rect("Track", viewport);
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(0f, 0.5f);
            track.pivot = new Vector2(0f, 0.5f);
            track.anchoredPosition = Vector2.zero;
            track.sizeDelta = new Vector2(0f, 36f);
            UiElements.AddHorizontalLayout(track, 0, 0, 0, 0, 0f);
            ContentSizeFitter trackFitter = track.gameObject.AddComponent<ContentSizeFitter>();
            trackFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            trackFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            RectTransform groupA = BuildNewsTickerGroup("GroupA", track, true);
            RectTransform groupB = BuildNewsTickerGroup("GroupB", track, false);
            groupB.gameObject.SetActive(false);

            Bind("Background", background.rectTransform, "Image");
            Bind("Viewport", viewport, "RectTransform");
            Bind("Track", track, "HorizontalLayoutGroup");
            Bind("GroupA", groupA, "HorizontalLayoutGroup");
            Bind("GroupB", groupB, "HorizontalLayoutGroup");
            Bind("ItemTemplateA", groupA.GetChild(0) as RectTransform, "RectTransform");
            Bind("ItemTemplateB", groupB.GetChild(0) as RectTransform, "RectTransform");
            Bind("HeadlineText", groupA.Find("TickerItem/HeadlineText") as RectTransform, "Text");
            Bind("BodyText", groupA.Find("TickerItem/BodyText") as RectTransform, "Text");
            Bind("HeadlineTextB", groupB.Find("TickerItem/HeadlineText") as RectTransform, "Text");
            Bind("BodyTextB", groupB.Find("TickerItem/BodyText") as RectTransform, "Text");
            return root;
        }

        private static RectTransform BuildNewsTickerGroup(string name, Transform parent, bool active)
        {
            RectTransform group = UiElements.Rect(name, parent);
            UiElements.AddHorizontalLayout(group, 0, 0, 0, 0, 0f);
            ContentSizeFitter groupFitter = group.gameObject.AddComponent<ContentSizeFitter>();
            groupFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            groupFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            UiElements.Layout(group, -1f, 36f);

            RectTransform item = UiElements.Rect("TickerItem", group);
            UiElements.AddHorizontalLayout(item, 32, 0, 32, 0, 9f);
            ContentSizeFitter itemFitter = item.gameObject.AddComponent<ContentSizeFitter>();
            itemFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            itemFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            UiElements.Layout(item, -1f, 36f);
            Text headline = UiElements.Text("HeadlineText", item, "本周平讯", 12, UiTheme.Hex("EF9A82"), TextAnchor.MiddleLeft, true);
            headline.horizontalOverflow = HorizontalWrapMode.Overflow;
            headline.verticalOverflow = VerticalWrapMode.Overflow;
            Text body = UiElements.Text("BodyText", item, "本周暂无新的货物异动。", 12, UiTheme.PaperLight, TextAnchor.MiddleLeft);
            body.horizontalOverflow = HorizontalWrapMode.Overflow;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            group.gameObject.SetActive(active);
            return group;
        }

        private static RectTransform BuildToast()
        {
            RectTransform root = SimpleRoot("Toast", 230f, 40f);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
            UiElements.AddHorizontalLayout(root, 17, 11, 17, 11, 9f);
            ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LayoutElement rootLayout = UiElements.Layout(root, -1f, -1f);
            rootLayout.minWidth = 230f;
            rootLayout.minHeight = 40f;

            Image shadow = UiElements.Image("Shadow", root, "panel_toast_9s", UiTheme.Alpha(UiTheme.Ink, 0.55f), true);
            UiElements.Stretch(shadow.rectTransform, 5f, 6f, -5f, -6f);
            UiElements.Layout(shadow, -1f, -1f).ignoreLayout = true;
            Image background = UiElements.Image("Background", root, "panel_toast_9s", Color.white, true, false);
            UiElements.Stretch(background.rectTransform);
            UiElements.Layout(background, -1f, -1f).ignoreLayout = true;
            Image stripe = UiElements.Image("StatusStripe", root, null, UiTheme.GoldLight);
            UiElements.LeftStretch(stripe.rectTransform, 0f, 5f);
            UiElements.Layout(stripe, -1f, -1f).ignoreLayout = true;
            Image icon = UiElements.Icon("StatusIcon", root, "icon_check", UiTheme.GoldLight, 0f, 0f, 18f);
            UiElements.Layout(icon, 18f, 18f);
            Text message = UiElements.Text("MessageText", root, "契约已记入货囊。", 12, UiTheme.PaperLight, TextAnchor.MiddleLeft);
            message.lineSpacing = 1.25f;
            UiElements.Layout(message, -1f, -1f);

            Bind("CanvasGroup", root, "CanvasGroup");
            Bind("Background", background.rectTransform, "Image");
            Bind("StatusStripe", stripe.rectTransform, "Image");
            Bind("StatusIcon", icon.rectTransform, "Image");
            Bind("MessageText", message.rectTransform, "Text");
            return root;
        }

        private static RectTransform BuildTooltip()
        {
            RectTransform root = SimpleRoot("Tooltip", 300f, 120f);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            UiElements.AddVerticalLayout(root, 15, 13, 15, 13, 5f);
            ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image shadow = UiElements.Image("Shadow", root, "panel_paper_tooltip_9s", UiTheme.Alpha(UiTheme.Ink, 0.85f), true);
            UiElements.Stretch(shadow.rectTransform, 5f, 6f, -5f, -6f);
            UiElements.Layout(shadow, -1f, -1f).ignoreLayout = true;
            Image background = UiElements.Image("Background", root, "panel_paper_tooltip_9s", Color.white, true, false);
            UiElements.Stretch(background.rectTransform);
            UiElements.Layout(background, -1f, -1f).ignoreLayout = true;
            Text title = UiElements.Text("TitleText", root, "飞天门辟邪玉", 13, UiTheme.Ink, TextAnchor.UpperLeft, true);
            UiElements.Layout(title, -1f, -1f, 1f, 0f);
            Text body = UiElements.Text("BodyText", root, "崆峒飞天门监制，据称经掌门真人亲手开光。", 11, UiTheme.InkSoft, TextAnchor.UpperLeft, UiFontRole.Serif);
            body.lineSpacing = 1.75f;
            UiElements.Layout(body, -1f, -1f, 1f, 0f);
            Text hint = UiElements.Text("HintText", root, "点击查看行情 · mod:01", 8, UiTheme.Muted);
            UiElements.Layout(hint, -1f, -1f, 1f, 0f);

            Bind("CanvasGroup", root, "CanvasGroup");
            Bind("Background", background.rectTransform, "Image");
            Bind("TitleText", title.rectTransform, "Text");
            Bind("BodyText", body.rectTransform, "Text");
            Bind("HintText", hint.rectTransform, "Text");
            return root;
        }

        private static Button ContractButton(string name, RectTransform parent, string caption, bool primary)
        {
            Image border = UiElements.Image(name, parent, null, UiTheme.Ink, false, true);
            if (primary)
            {
                Shadow shadow = border.gameObject.AddComponent<Shadow>();
                shadow.effectColor = UiTheme.Ink;
                shadow.effectDistance = new Vector2(4f, -4f);
            }
            Image face = UiElements.Image("Face", border.transform, null, Color.white);
            UiElements.Stretch(face.rectTransform, 1f, 1f, 1f, 1f);
            Button button = border.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            Color rest = primary ? UiTheme.Red : UiTheme.PaperDeep;
            UiElements.SetColorTint(button, rest, primary ? UiTheme.RedDark : UiTheme.GoldLight,
                primary ? UiTheme.RedDark : UiTheme.Gold);
            Text label = UiElements.Text("Label", border.transform, caption, 12,
                primary ? UiTheme.PaperLight : UiTheme.Ink, TextAnchor.MiddleCenter, true);
            UiElements.Stretch(label.rectTransform);
            return button;
        }

        private static RectTransform BuildModal()
        {
            RectTransform root = CanvasRoot("Modal", 1100);
            Image blocker = UiElements.Image("Blocker", root, null, new Color(12f / 255f, 15f / 255f, 13f / 255f, 0.76f), false, true);
            UiElements.Stretch(blocker.rectTransform);
            CanvasGroup backdropGroup = blocker.gameObject.AddComponent<CanvasGroup>();
            backdropGroup.blocksRaycasts = true;
            Button blockerButton = blocker.gameObject.AddComponent<Button>();
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.targetGraphic = blocker;
            Bind("Blocker", blocker.rectTransform, "Button");
            Bind("BackdropGroup", blocker.rectTransform, "CanvasGroup");

            RectTransform motion = UiElements.Rect("DialogMotion", root);
            UiElements.Center(motion, 600f, 340f);
            CanvasGroup dialogGroup = motion.gameObject.AddComponent<CanvasGroup>();
            dialogGroup.blocksRaycasts = true;
            dialogGroup.interactable = true;
            Image shadow = UiElements.Image("Shadow", motion, "panel_contract_modal_9s", UiTheme.Ink, true, false);
            UiElements.Stretch(shadow.rectTransform, 10f, 12f, -10f, -12f);
            Image dialog = UiElements.Image("Dialog", motion, "panel_contract_modal_9s", Color.white, true, true);
            UiElements.Stretch(dialog.rectTransform);
            UiElements.AddVerticalLayout(dialog.rectTransform, 31, 23, 25, 23, 0f);

            RectTransform header = UiElements.Rect("Header", dialog.transform);
            UiElements.Layout(header, -1f, 74f, 1f, 0f);
            Text title = UiElements.PlaceText("TitleText", header, "立契结算", 29, UiTheme.Ink, 6f, 0f, 464f, 44f, TextAnchor.MiddleLeft, true);
            title.fontStyle = FontStyle.BoldAndItalic;
            Text subtitle = UiElements.PlaceText("SubtitleText", header, "核对货契，落印之后记入常态货囊", 11, UiTheme.Muted, 6f, 42f, 476f, 22f);
            Button close = UiElements.Button("CloseButton", header, "button_paper_icon_normal", "button_paper_icon_hover", "button_paper_icon_pressed", "button_paper_icon_disabled");
            UiElements.Place(close.GetComponent<RectTransform>(), 496f, 0f, 42f, 42f);
            Image closeIcon = UiElements.Image("Icon", close.transform, "icon_close", UiTheme.Ink);
            UiElements.Center(closeIcon.rectTransform, 24f, 24f);
            closeIcon.preserveAspect = true;
            Image headerLine = UiElements.Image("HeaderLine", header, null, UiTheme.Ink);
            UiElements.BottomStretch(headerLine.rectTransform, 0f, 3f, 6f, 6f);

            RectTransform bodyContent;
            ScrollRect body = UiElements.VerticalScroll("BodyScroll", dialog.transform, out bodyContent, 0f, 6f);
            LayoutElement bodyLayout = UiElements.Layout(body, -1f, 44f, 1f, 1f);
            bodyLayout.minHeight = 36f;
            RectTransform line = UiElements.Rect("ContractLineTemplate", bodyContent);
            UiElements.AddHorizontalLayout(line, 4, 8, 4, 8, 12f);
            ContentSizeFitter lineFitter = line.gameObject.AddComponent<ContentSizeFitter>();
            lineFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            lineFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiElements.Layout(line, -1f, -1f, 1f, 0f);
            Text label = UiElements.Text("Label", line, "买入 · 飞天门辟邪玉", 12, UiTheme.RedDark, TextAnchor.MiddleLeft, true);
            UiElements.Layout(label, 190f, -1f, 1f, 0f);
            Text quantity = UiElements.Text("Quantity", line, "× 2", 12, UiTheme.Ink, TextAnchor.MiddleRight);
            UiElements.Layout(quantity, 72f, -1f);
            Text amount = UiElements.Text("Amount", line, "1,982 文", 12, UiTheme.Ink, TextAnchor.MiddleRight, true);
            UiElements.Layout(amount, 112f, -1f);
            Image separator = UiElements.Image("Separator", line, null, UiTheme.Alpha(UiTheme.Ink, 0.22f));
            UiElements.BottomStretch(separator.rectTransform, 0f, 1f, 4f, 4f);
            UiElements.Layout(separator, -1f, -1f).ignoreLayout = true;

            RectTransform explainPanel = UiElements.Rect("ExplainPanel", bodyContent);
            Image explainBackground = UiElements.Image("Background", explainPanel, null, UiTheme.Paper);
            UiElements.Stretch(explainBackground.rectTransform);
            UiElements.Layout(explainBackground, -1f, -1f).ignoreLayout = true;
            UiElements.AddVerticalLayout(explainPanel, 10, 10, 10, 10, 0f);
            ContentSizeFitter explainFitter = explainPanel.gameObject.AddComponent<ContentSizeFitter>();
            explainFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            explainFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiElements.Layout(explainPanel, -1f, -1f, 1f, 0f);
            Text explain = UiElements.Text("ExplainText", explainPanel, "借货按现价 97% 实收，24 个市场周到期。", 11, UiTheme.Muted, TextAnchor.UpperLeft, UiFontRole.Serif);
            explain.lineSpacing = 1.28f;
            UiElements.Layout(explain, -1f, -1f, 1f, 0f);

            RectTransform footer = UiElements.Rect("Footer", dialog.transform);
            UiElements.Layout(footer, -1f, 108f, 1f, 0f);
            Image footerLine = UiElements.Image("FooterLine", footer, null, UiTheme.Ink);
            UiElements.Place(footerLine.rectTransform, 6f, 0f, 532f, 2f);
            Text totalCaption = UiElements.PlaceText("TotalCaptionText", footer, "本次预计应付", 11, UiTheme.Muted, 6f, 14f, 245f, 34f, TextAnchor.MiddleLeft);
            Text total = UiElements.PlaceText("TotalText", footer, "1,354 文", 26, UiTheme.Ink, 260f, 9f, 278f, 44f, TextAnchor.MiddleRight, true);
            Button cancel = ContractButton("CancelButton", footer, "再核一遍", false);
            UiElements.Place(cancel.GetComponent<RectTransform>(), 6f, 62f, 208f, 39f);
            Button confirm = ContractButton("ConfirmButton", footer, "确认落印", true);
            UiElements.Place(confirm.GetComponent<RectTransform>(), 226f, 62f, 312f, 39f);

            Bind("Dialog", dialog.rectTransform, "RectTransform");
            Bind("DialogMotion", motion, "RectTransform");
            Bind("DialogGroup", motion, "CanvasGroup");
            Bind("TitleText", title.rectTransform, "Text");
            Bind("SubtitleText", subtitle.rectTransform, "Text");
            Bind("CloseButton", close.GetComponent<RectTransform>(), "Button");
            Bind("BodyScroll", body.GetComponent<RectTransform>(), "ScrollRect");
            Bind("BodyContent", bodyContent, "VerticalLayoutGroup");
            Bind("LineTemplate", line, "RectTransform");
            Bind("ExplainPanel", explainPanel, "RectTransform");
            Bind("ExplainText", explain.rectTransform, "Text");
            Bind("TotalCaptionText", totalCaption.rectTransform, "Text");
            Bind("TotalText", total.rectTransform, "Text");
            Bind("CancelButton", cancel.GetComponent<RectTransform>(), "Button");
            Bind("ConfirmButton", confirm.GetComponent<RectTransform>(), "Button");
            return root;
        }
    }
}
