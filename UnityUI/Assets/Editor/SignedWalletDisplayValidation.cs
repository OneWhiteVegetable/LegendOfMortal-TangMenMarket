using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    public sealed partial class RuntimePreviewInteractionRunner
    {
        private static IEnumerator SignedMastheadWallet(RuntimePreviewInteractionContext c)
        {
            c.Open("market-empty", true);
            yield return c.WaitFrames(2);
            long initial = c.Adapter.CurrentCash;
            Transform header = GameObject.Find("XingShangMod.MarketWindow").transform.Find("Window/Masthead");
            RectTransform block = (RectTransform)header.Find("MoneyBlock");
            RectTransform nav = (RectTransform)header.Find("HeaderNav");
            Text amount = block.Find("MoneyText").GetComponent<Text>();
            float smallWidth = 0f;
            try
            {
                foreach (int cash in new[] { 5, -4941, 100000000, int.MinValue, 5 })
                {
                    c.Adapter.SetDisplayedCash(cash);
                    yield return c.WaitFrames(2);
                    c.Check(amount.text == cash.ToString("N0", CultureInfo.InvariantCulture) + " <size=11>文</size>",
                        "The refreshed masthead lost the sign, digits or unit.");
                    c.Check(amount.preferredWidth <= amount.rectTransform.rect.width + 0.1f,
                        "Native money amount exceeds its allocated text area: " + amount.text);
                    Bounds valueBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(header, amount.rectTransform);
                    Bounds navBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(header, nav);
                    c.Check(valueBounds.max.x <= navBounds.min.x + .1f, "Wallet overlaps the navigation.");
                    if (cash == 5 && smallWidth == 0f) smallWidth = block.rect.width;
                    else if (cash == 5) c.Check(Mathf.Abs(block.rect.width - smallWidth)<.1f, "Wallet did not shrink back.");
                    if (cash == int.MinValue)
                    {
                        c.Check(block.rect.width > smallWidth, "A long signed balance did not expand its frame.");
                        c.Capture("negative-wallet", c.Now, "Signed int minimum with the normal masthead font", true);
                    }
                }
            }
            finally { c.Adapter.SetDisplayedCash((int)initial); }
        }
    }
}
