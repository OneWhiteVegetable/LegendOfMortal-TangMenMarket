using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static partial class UiBuilder
    {
        private static void AddMasthead(RectTransform window)
        {
            UiTheme.LayoutProfile profile = UiTheme.Desktop1920;
            Image header = UiElements.Image("Masthead", window, null, UiTheme.Ink, false, true);
            UiElements.TopStretch(header.rectTransform, 0f, profile.MastheadHeight);
            Bind("Masthead", header.rectTransform, "RectTransform");

            Image goldRule = UiElements.Image("GoldRule", header.transform, null, UiTheme.Gold);
            UiElements.BottomStretch(goldRule.rectTransform, 0f, 5f);

            Image brand = UiElements.Image("BrandBlock", header.transform, "panel_brand_block", Color.white);
            UiElements.LeftStretch(brand.rectTransform, 0f, profile.BrandWidth);
            Image sealBack = UiElements.Image("BrandSealBack", brand.transform, null, UiTheme.RedDark);
            UiElements.Place(sealBack.rectTransform, 22f, 17f, profile.BrandSealWidth, profile.BrandSealHeight);
            sealBack.transform.localRotation = Quaternion.Euler(0f, 0f, 5f);
            Shadow sealShadow = sealBack.gameObject.AddComponent<Shadow>();
            sealShadow.effectColor = UiTheme.Ink;
            sealShadow.effectDistance = new Vector2(3f, -3f);
            sealShadow.useGraphicAlpha = true;
            Image sealFace = UiElements.Image("SealFace", sealBack.transform, null, UiTheme.Red);
            UiElements.Stretch(sealFace.rectTransform, 2f, 2f, 2f, 2f);
            Image seal = UiElements.Image("BrandSeal", sealBack.transform, "brand_shang_mark", Color.white);
            UiElements.Stretch(seal.rectTransform, 4f, 4f, 4f, 4f);
            seal.preserveAspect = true;
            Text brandTangmen = UiElements.PlaceText("BrandTangmen", brand.transform, "唐门", 29, UiTheme.Ink, 80f, 9f, 68f, 38f, TextAnchor.MiddleLeft, true);
            brandTangmen.horizontalOverflow = HorizontalWrapMode.Overflow;
            brandTangmen.verticalOverflow = VerticalWrapMode.Overflow;
            Image brandRed = UiElements.Image("BrandRed", brand.transform, null, UiTheme.Red);
            UiElements.Place(brandRed.rectTransform, 148f, 12f, 76f, 38f);
            brandRed.transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            Shadow brandShadow = brandRed.gameObject.AddComponent<Shadow>();
            brandShadow.effectColor = UiTheme.Ink;
            brandShadow.effectDistance = new Vector2(4f, -4f);
            brandShadow.useGraphicAlpha = true;
            Text brandMarket = UiElements.PlaceText("BrandMarket", brandRed.transform, "行市", 27, UiTheme.PaperLight, 0f, 0f, 76f, 38f, TextAnchor.MiddleCenter, true);
            UiElements.Stretch(brandMarket.rectTransform);
            brandMarket.horizontalOverflow = HorizontalWrapMode.Overflow;
            brandMarket.verticalOverflow = VerticalWrapMode.Overflow;
            UiElements.PlaceText("BrandSubtitle", brand.transform, "TANGMEN EXCHANGE", 8, UiTheme.Muted, 80f, 53f, 160f, 14f, TextAnchor.MiddleLeft, true);

            Image indexImage = UiElements.Image("IndexButton", header.transform, null, Color.clear, false, true);
            UiElements.Place(indexImage.rectTransform, profile.BrandWidth, 0f, profile.IndexWidth, profile.MastheadHeight);
            Button index = indexImage.gameObject.AddComponent<Button>();
            UiElements.SetColorTint(index, Color.white, UiTheme.Alpha(UiTheme.PaperLight, 0.12f), UiTheme.Alpha(UiTheme.Gold, 0.18f));
            Text indexName = UiElements.PlaceText("IndexNameText", index.transform, "唐门指数", 13, UiTheme.PaperLight, 18f, 8f, 108f, 22f, TextAnchor.MiddleLeft, true);
            Text indexPrice = UiElements.PlaceText("IndexPriceText", index.transform, "101.61", 23, UiTheme.Red, 18f, 29f, 112f, 35f, TextAnchor.MiddleLeft, true);
            Text indexChange = UiElements.PlaceText("IndexChangeText", index.transform, "▲ +0.06%", 12, UiTheme.Red, 124f, 26f, 78f, 30f, TextAnchor.MiddleLeft, true);
            RectTransform indexChart = UiElements.Host("IndexChartHost", index.transform, "IndexChartHost");
            UiElements.Place(indexChart, profile.IndexWidth - profile.IndexSparkWidth - 16f, 18f, profile.IndexSparkWidth, profile.IndexSparkHeight);
            Bind("IndexButton", index.GetComponent<RectTransform>(), "Button");
            Bind("IndexNameText", indexName.rectTransform, "Text");
            Bind("IndexPriceText", indexPrice.rectTransform, "Text");
            Bind("IndexChangeText", indexChange.rectTransform, "Text");

            RectTransform money = UiElements.Image("MoneyBlock", header.transform, null, Color.clear, false, true).rectTransform;
            money.gameObject.AddComponent<Selectable>().transition = Selectable.Transition.None;
            UiElements.RightStretch(money, 240f, 244f);
            UiElements.Icon("MoneyCoin", money, "money_coin_v3", Color.white, 12f, 24f, 34f);
            UiElements.PlaceText("MoneyCaption", money, "随身银钱", 11, UiTheme.Hex("AEB5AA"), 58f, 11f, 160f, 20f);
            Text moneyValue = UiElements.PlaceText("MoneyText", money, "196,036 <size=11>文</size>", 22, UiTheme.GoldLight, 58f, 30f, 174f, 37f, TextAnchor.MiddleLeft, true);
            Bind("MoneyText", moneyValue.rectTransform, "Text");

            RectTransform nav = UiElements.Rect("HeaderNav", header.transform);
            UiElements.RightStretch(nav, 0f, 240f);
            string[] labels = { "榜单", "货囊", "离开" };
            string[] icons = { "icon_rank", "icon_bag", "icon_close" };
            string[] keys = { "RankingButton", "InventoryButton", "CloseButton" };
            for (int i = 0; i < 3; i++)
            {
                Image buttonImage = UiElements.Image(keys[i], nav, null, Color.clear, false, true);
                UiElements.Place(buttonImage.rectTransform, i * 80f, 0f, 80f, profile.MastheadHeight);
                AddHeaderDivider(buttonImage.rectTransform);
                Button button = buttonImage.gameObject.AddComponent<Button>();
                UiElements.SetColorTint(button, Color.white, UiTheme.Alpha(UiTheme.GoldLight, 0.26f), UiTheme.Alpha(UiTheme.Red, 0.4f));
                Image icon = UiElements.Image("Icon", button.transform, icons[i], UiTheme.PaperLight);
                UiElements.Place(icon.rectTransform, 26f, 10f, 28f, 28f);
                icon.preserveAspect = true;
                UiElements.PlaceText("Label", button.transform, labels[i], 12, UiTheme.PaperLight, 4f, 44f, 72f, 26f, TextAnchor.MiddleCenter);
                Bind(keys[i], button.GetComponent<RectTransform>(), "Button");
            }

            Image redStroke = UiElements.Image("MastheadRedStroke", header.transform, null, UiTheme.Red);
            redStroke.rectTransform.anchorMin = new Vector2(0f, 0f);
            redStroke.rectTransform.anchorMax = new Vector2(0.34f, 0f);
            redStroke.rectTransform.pivot = new Vector2(0f, 0f);
            redStroke.rectTransform.offsetMin = new Vector2(0f, -4f);
            redStroke.rectTransform.offsetMax = Vector2.zero;
            redStroke.raycastTarget = false;
        }

        private static void AddHeaderDivider(RectTransform parent)
        {
            Image rule = UiElements.Image("Divider", parent, null, UiTheme.Alpha(UiTheme.PaperLight, 0.14f));
            UiElements.LeftStretch(rule.rectTransform, 0f, 1f);
        }

        private static void AddStatusbar(RectTransform window)
        {
            UiTheme.LayoutProfile profile = UiTheme.Desktop1920;
            Image status = UiElements.Image("Statusbar", window, null, UiTheme.Alpha(UiTheme.PaperDeep, 0.88f), false, true);
            UiElements.BottomStretch(status.rectTransform, 0f, profile.StatusHeight);
            UiElements.PlaceText("Brand", status.transform, "唐门行市 · V4", 10, UiTheme.Ink, 18f, 0f, 210f, profile.StatusHeight, TextAnchor.MiddleLeft, true);
            UiElements.PlaceText("Legend", status.transform, "红涨 · 买货     绿跌 · 借货", 10, UiTheme.Muted, 250f, 0f, 360f, profile.StatusHeight, TextAnchor.MiddleLeft);
            Text hint = UiElements.Text("StatusText", status.transform, "行情与货囊账目", 10, UiTheme.Muted, TextAnchor.MiddleCenter);
            UiElements.Stretch(hint.rectTransform, 610f, 0f, 220f, 0f);
            Image motionImage = UiElements.Image("MotionButton", status.transform, null, Color.clear, false, true);
            UiElements.RightStretch(motionImage.rectTransform, 12f, 176f, 0f, 0f);
            Button motion = motionImage.gameObject.AddComponent<Button>();
            UiElements.SetColorTint(motion, Color.white, UiTheme.Alpha(UiTheme.Gold, 0.18f), UiTheme.Alpha(UiTheme.Gold, 0.28f));
            UiElements.Icon("Icon", motion.transform, "icon_motion", UiTheme.Muted, 8f, 6f, 16f);
            UiElements.PlaceText("Label", motion.transform, "减少动效", 10, UiTheme.Muted, 30f, 0f, 136f, profile.StatusHeight, TextAnchor.MiddleLeft);
            Bind("MotionButton", motion.GetComponent<RectTransform>(), "Button");
            Bind("StatusText", hint.rectTransform, "Text");
        }

        private static RectTransform AddWindowShell(string name, out RectTransform window)
        {
            UiTheme.LayoutProfile profile = UiTheme.Desktop1920;
            RectTransform root = CanvasRoot(name);
            Image scene = UiElements.Image("SceneBackground", root, "back_menu_screen_1__265", UiTheme.Alpha(UiTheme.Ink, 0.9f));
            UiElements.Stretch(scene.rectTransform);
            Image blocker = UiElements.Image("Blocker", root, null, new Color(0.04f, 0.055f, 0.045f, 0.76f), false, true);
            UiElements.Stretch(blocker.rectTransform);
            Bind("Blocker", blocker.rectTransform, "Image");

            Image windowImage = UiElements.Image("Window", root, "panel_merchant_window_9s", Color.white, true, true);
            UiElements.Stretch(windowImage.rectTransform, profile.WindowInset, profile.WindowInset, profile.WindowInset, profile.WindowInset);
            windowImage.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            CanvasGroup windowGroup = windowImage.gameObject.AddComponent<CanvasGroup>();
            windowGroup.alpha = 1f;
            windowGroup.interactable = false;
            windowGroup.blocksRaycasts = false;
            window = windowImage.rectTransform;
            Bind("Window", window, "RectTransform");

            AddMasthead(window);
            AddStatusbar(window);

            RectTransform motionPivot = UiElements.Rect("WindowMotion", root);
            UiElements.Stretch(motionPivot);
            CanvasGroup motionGroup = motionPivot.gameObject.AddComponent<CanvasGroup>();
            motionGroup.alpha = 1f;
            motionGroup.interactable = false;
            motionGroup.blocksRaycasts = false;
            Image paperMask = UiElements.Image("PaperGrain", motionPivot, "panel_merchant_window_9s", Color.white, true, false);
            UiElements.Stretch(paperMask.rectTransform, profile.WindowInset, profile.WindowInset, profile.WindowInset, profile.WindowInset);
            Mask mask = paperMask.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            Image paper = UiElements.Image("Grain", paperMask.transform, "texture_paper_grain_tile", Color.white, false, false);
            UiElements.Stretch(paper.rectTransform);
            paper.type = Image.Type.Tiled;
            paper.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI_PaperMultiply.mat");
            if (paper.material == null)
                throw new InvalidOperationException("Missing UI paper Multiply material: Assets/Materials/UI_PaperMultiply.mat");
            return root;
        }

        private static void AddTicker(RectTransform window, bool visible = true)
        {
            UiTheme.LayoutProfile profile = UiTheme.Desktop1920;
            GameObject ticker = Instantiate("NewsTicker", window);
            ticker.name = "NewsTicker";
            UiElements.TopStretch(ticker.GetComponent<RectTransform>(), profile.MastheadHeight, profile.TickerHeight);
            ticker.SetActive(visible);
            Bind("NewsTicker", ticker.GetComponent<RectTransform>(), "RectTransform");
            Transform masthead = window.Find("Masthead");
            if (masthead != null)
                masthead.SetAsLastSibling();
        }
    }
}
