using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    public sealed partial class RuntimePreviewInteractionRunner
    {
        private static Button ForegroundButton(string root, string name)
        {
            return GameObject.Find("XingShangMod." + root).GetComponentsInChildren<Button>(false)
                .Single(b => b.name == name);
        }

        private static IEnumerator ChromeFocusAndEdges(RuntimePreviewInteractionContext c)
        {
            c.Open("market", false);
            yield return c.Wait(.6f);
            Button inventory = ForegroundButton("MarketWindow", "InventoryButton");
            Click(c, inventory.gameObject);
            yield return c.Wait(.6f);
            Button ranking = ForegroundButton("InventoryWindow", "RankingButton");
            Pointer(c, ranking.gameObject, true);
            yield return c.Wait(.25f);
            Click(c, ranking.gameObject);
            yield return c.Wait(.6f);
            Transform index = ForegroundButton("MarketWindow", "IndexButton").transform;
            Color32 indexInk = index.Find("IndexPriceText").GetComponent<Text>().color;
            Color32 changeInk = index.Find("IndexChangeText").GetComponent<Text>().color;
            var risingInk = new Color32(182, 64, 54, 255);
            c.Check(indexInk.Equals(risingInk) && changeInk.Equals(risingInk),
                "A rising index must use the same vermilion as the HTML quote.");
            RectTransform title = GameObject.Find("RankingTitleText").GetComponent<RectTransform>();
            RectTransform lastWick = title.Find("TitleRule/Wick3") as RectTransform;
            c.Check(lastWick != null && lastWick.rect.height >= title.rect.height * 0.6f,
                "The final rising candle must reach into the height of the printed title.");
            inventory = ForegroundButton("MarketWindow", "InventoryButton");
            c.Check(inventory.image.color.a < .01f, "Pointer navigation restored the old inventory highlight on the ranking page.");
            Click(c, inventory.gameObject);
            yield return c.Wait(.6f);
            ranking = ForegroundButton("InventoryWindow", "RankingButton");
            c.Check(ranking.image.color.a < .01f, "Ranking was highlighted after navigating back to inventory with the mouse.");
            Button square = ForegroundButton("InventoryWindow", "ProfileCloseButton");
            Pointer(c, square.gameObject, true);
            yield return c.Wait(.25f);
            var cross = square.transform.Find("Icon").GetComponent<Image>();
            c.Check(cross.color.r > .94f && cross.color.g > .94f && cross.color.b > .94f, "Close glyph was dark on the hover face.");
            c.Capture("square-close-hover", c.Now, "White cross on the highlighted paper button", true);

            Click(c, square.gameObject);
            yield return c.Wait(.6f);
            Button rankingBack = ForegroundButton("MarketWindow", "RankingBackButton");
            Click(c, rankingBack.gameObject);
            yield return c.Wait(.3f);
            Click(c, c.Adapter.CardActionButton(0, "Open").gameObject);
            yield return c.Wait(.6f);
            Button back = ForegroundButton("ItemDetail", "BackButton");
            Pointer(c, back.gameObject, false);
            yield return c.Wait(.25f);
            c.Check(back.transform.Find("BrushPaper").localScale.x < .015f, "Opening a detail gave the return arrow unsolicited focus paint.");
            Button leave = ForegroundButton("ItemDetail", "CloseButton");
            Pointer(c, leave.gameObject, true);
            yield return c.Wait(.3f);
            var top = (RectTransform)leave.transform.parent.parent;
            var face = leave.GetComponent<RectTransform>();
            float clearance = top.rect.height - face.rect.height;
            c.Check(Mathf.Abs(clearance - 5f) < .1f, "Header action must end above the five-pixel gold rule.");
            c.Check(!top.Find("IndexButton/Divider") && !top.Find("MoneyBlock/Divider"), "Unwanted index or wallet divider remains.");
            c.Capture("header-close-hover", c.Now, "Header ink and gold seam", true);
            Pointer(c, leave.gameObject, false);
            EventSystem.current.SetSelectedGameObject(back.gameObject);
            yield return c.Wait(.3f);
            c.Check(back.transform.Find("BrushPaper").localScale.x > .97f, "Explicit keyboard focus did not open the return arrow.");
        }
    }
}
