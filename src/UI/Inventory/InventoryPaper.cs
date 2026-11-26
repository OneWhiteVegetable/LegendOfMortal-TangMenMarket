using UnityEngine;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Market;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Inventory
{
    internal static class InventoryPaper
    {
        internal static readonly Color Section = new Color32(247, 242, 229, 150);
        internal static readonly Color Border = new Color32(32, 35, 33, 84);
        internal static readonly Color InkBorder = new Color32(32, 35, 33, 230);

        internal static Image Solid(string name, Transform parent, Color color)
        {
            return RankingLayout.Image(name, parent, null, color);
        }

        internal static void Face(Image image, Color color)
        {
            image.overrideSprite = null;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
        }

        internal static void Box(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static void Right(RectTransform rect, float right, float top, float width, float height)
        {
            Box(rect, -right, top, width, height);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        }

        internal static void Frame(Transform parent, Color color, float leftWidth, Color leftColor)
        {
            Bar("LeftRule", parent, 0, 0, 0, 1, 0, 0, leftWidth, 0, leftColor);
            Bar("RightRule", parent, 1, 0, 1, 1, -1, 0, 0, 0, color);
            Bar("TopRule", parent, 0, 1, 1, 1, leftWidth, -1, 0, 0, color);
            Bar("BottomRule", parent, 0, 0, 1, 0, leftWidth, 0, 0, 1, color);
        }

        internal static Image Bar(string name, Transform parent, float ax, float ay, float bx, float by,
            float left, float bottom, float right, float top, Color color)
        {
            var image = Solid(name, parent, color);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(ax, ay);
            rect.anchorMax = new Vector2(bx, by);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
            return image;
        }

        internal static void Header(UiPrefabInstance view, UiFrontendServices services)
        {
            var page = (RectTransform)view.Find("Window/ProfilePage");
            var header = (RectTransform)page.Find("ProfileHeader");
            RankingLayout.Stretch(page, 24, 101, 24, 45);
            var emblem = header.Find("EmblemBack").GetComponent<Image>();
            Box(emblem.rectTransform, 0, 0, 48, 48);
            emblem.transform.localRotation = Quaternion.Euler(0, 0, 4);
            Image shade = Solid("EmblemShadow", header, RankingLayout.Gold);
            Box(shade.rectTransform, 4, 4, 48, 48);
            shade.rectTransform.localRotation = emblem.rectTransform.localRotation;
            shade.rectTransform.SetAsFirstSibling();
            UiPaperShape.Attach(emblem);
            UiPaperShape.Attach(shade);
            var bag = emblem.transform.Find("Emblem").GetComponent<Image>();
            RankingLayout.Center(bag.rectTransform, 29, 29);
            var name = header.Find("Title").GetComponent<Text>();
            Box(name.rectTransform, 64, 0, 220, 34);
            name.fontSize = 28;
            name.fontStyle = FontStyle.Italic;
            var sub = header.Find("Subtitle").GetComponent<Text>();
            Box(sub.rectTransform, 64, 34, 230, 18);
            sub.fontSize = 10;
            var rule = page.Find("HeaderLine").GetComponent<Image>();
            Face(rule, UiFormatting.Ink);
            rule.rectTransform.offsetMin = new Vector2(0, -65);
            rule.rectTransform.offsetMax = new Vector2(0, -62);
            var content = view.Rect("ProfileContent");
            content.offsetMax = new Vector2(content.offsetMax.x, -78);
            Button close = view.Button("ProfileCloseButton");
            Right(close.image.rectTransform, 0, 7, 35, 35);
            RankingLayout.Center((RectTransform)close.transform.Find("Icon"), 19, 19);
            RectTransform tabs = RankingLayout.Rect("ProfileTabs", header);
            Right(tabs, 51, 8, 152, 36);
            Bar("Baseline", tabs, 0, 0, 1, 0, 0, 0, 0, 1, UiFormatting.Ink);
            Tab(view.Button("InventoryTabButton"), tabs, 0, 64);
            Tab(view.Button("AssetsTabButton"), tabs, 64, 88);
            HeaderMetric(view.Rect("SummaryCapacityCell"), header, services, "icon_stamina", 277, 138);
            HeaderMetric(view.Rect("SummaryWeightCell"), header, services, "icon_bag", 428, 165);
        }

        private static void Tab(Button button, RectTransform parent, float x, float width)
        {
            RectTransform rect = button.image.rectTransform;
            rect.SetParent(parent, false);
            Box(rect, x, 0, width, 36);
            button.transition = Selectable.Transition.None;
            Face(button.image, Color.clear);
            var tabLabel = button.transform.Find("Label").GetComponent<Text>();
            tabLabel.fontSize = 12;
            var line = button.transform.Find("ActiveLine").GetComponent<Image>();
            line.rectTransform.localRotation = Quaternion.identity;
            line.rectTransform.offsetMin = new Vector2(12,-2);
            line.rectTransform.offsetMax = new Vector2(-12,3);
            UiPaperShape.Attach(line, 2f);
        }

        private static void HeaderMetric(RectTransform cell, Transform header, UiFrontendServices services, string iconKey,
            float x, float width)
        {
            cell.SetParent(header, false);
            Box(cell, x, 3, width, 44);
            var caption = cell.GetChild(0).GetComponent<Text>();
            var value = cell.GetChild(1).GetComponent<Text>();
            var fill = cell.gameObject.AddComponent<Image>();
            fill.color = new Color32(243,231,202,235);
            fill.raycastTarget = false;
            Bar("FootRule", cell, 0,0,1,0, 0,0,0,1, RankingLayout.Gold);
            var icon = RankingLayout.Image("Icon",cell,services.Assets.GetSprite(iconKey),RankingLayout.Gold);
            icon.color = new Color32(139,101,53,255);
            Box(icon.rectTransform, 8, 10, 23, 23);
            caption.color = UiFormatting.Muted;
            caption.fontSize = 10;
            value.fontSize = 16;
            caption.alignment = value.alignment = TextAnchor.MiddleLeft;
            caption.horizontalOverflow = value.horizontalOverflow = HorizontalWrapMode.Overflow;
            Box(caption.rectTransform, 39, 2, width-42, 15);
            Box(value.rectTransform, 39, 17, width-42, 24);
        }

        internal static float FitHeaderMetrics(UiPrefabInstance view)
        {
            RectTransform capacity = view.Rect("SummaryCapacityCell");
            RectTransform weight = view.Rect("SummaryWeightCell");
            var header = (RectTransform)capacity.parent;
            if (header.rect.width < 1f) return 0f;
            float capWidth = PreferredMetricWidth(capacity, 132f);
            float weightWidth = PreferredMetricWidth(weight, 152f);
            const float gap = 12f;
            // Keep the page title on the left and the tabs + close control on the right.
            float right = header.rect.width - 217f;
            float left = Mathf.Max(218f, Mathf.Min(277f, right - capWidth - weightWidth - gap));
            capWidth = Mathf.Min(capWidth, Mathf.Max(104f, right - left - gap - 130f));
            weightWidth = Mathf.Min(weightWidth, Mathf.Max(130f, right - left - gap - capWidth));
            PlaceMetric(capacity, left, capWidth);
            PlaceMetric(weight, left + capWidth + gap, weightWidth);
            return header.rect.width;
        }

        private static float PreferredMetricWidth(RectTransform cell, float minimum)
        {
            Text caption = cell.GetChild(0).GetComponent<Text>();
            Text value = cell.GetChild(1).GetComponent<Text>();
            caption.fontSize = 10;
            value.fontSize = 16;
            return Mathf.Max(minimum, 49f + Mathf.Ceil(Mathf.Max(caption.preferredWidth, value.preferredWidth)));
        }

        private static void PlaceMetric(RectTransform cell, float left, float width)
        {
            Box(cell, left, 3f, width, 44f);
            for (int i = 0; i < 2; i++)
            {
                var text = cell.GetChild(i).GetComponent<Text>();
                float room = width - 47f;
                if (text.preferredWidth > room)
                    text.fontSize = Mathf.Max(i == 0 ? 8 : 12, Mathf.FloorToInt(text.fontSize * room / text.preferredWidth));
                Box(text.rectTransform, 39f, i == 0 ? 2f : 17f, room, i == 0 ? 15f : 24f);
            }
        }
    }
}
