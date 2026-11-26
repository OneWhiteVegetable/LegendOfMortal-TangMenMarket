using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static partial class UiBuilder
    {
        private static RectTransform BuildFlyToken()
        {
            RectTransform root = SimpleRoot("FlyToken", 55f, 55f);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            Image shadow = UiElements.Image("Shadow", root, null, UiTheme.Ink);
            UiElements.Stretch(shadow.rectTransform, 3f, 3f, -3f, -3f);
            Image background = UiElements.Image("Background", root, null, UiTheme.PaperLight);
            UiElements.Stretch(background.rectTransform);
            UiElements.AddOutline(background.gameObject, UiTheme.Gold, new Vector2(2f, -2f));
            Image item = UiElements.Image("TokenImage", root, "icon_scroll", Color.white);
            UiElements.Center(item.rectTransform, 45f, 45f);
            item.preserveAspect = true;
            Bind("CanvasGroup", root, "CanvasGroup");
            Bind("TokenImage", item.rectTransform, "Image");
            return root;
        }

        private static RectTransform BuildDealStamp()
        {
            RectTransform root = SimpleRoot("DealStamp", 180f, 180f);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            Image stamp = UiElements.Image("StampImage", root, "stamp_deal_chengjiao", Color.white);
            UiElements.Stretch(stamp.rectTransform);
            stamp.preserveAspect = true;
            Bind("CanvasGroup", root, "CanvasGroup");
            Bind("StampImage", stamp.rectTransform, "Image");
            return root;
        }
    }
}
