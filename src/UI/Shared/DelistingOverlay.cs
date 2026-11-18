using System;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Shared
{
    public sealed class DelistingOverlay : MonoBehaviour
    {
        public RectTransform Root { get; private set; }
        public Image Wordmark { get; private set; }
        public bool IsDelisted { get { return Root != null && Root.gameObject.activeSelf; } }

        public static DelistingOverlay Create(UiFactory factory, Transform parent)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }
            if (parent == null)
            {
                throw new ArgumentNullException("parent");
            }

            RectTransform root = UiFactory.CreateRect("DelistingOverlay", parent);
            UiFactory.Stretch(root);
            Image shade = root.gameObject.AddComponent<Image>();
            shade.color = new Color(0.20f, 0.19f, 0.17f, 0.32f);
            shade.raycastTarget = false;
            DelistingOverlay overlay = root.gameObject.AddComponent<DelistingOverlay>();
            overlay.Root = root;
            overlay.Wordmark = factory.CreateImage(
                root,
                "WastedWordmark",
                "wasted_delist_wordmark",
                new Vector2(560f, 196f),
                true);
            overlay.Wordmark.raycastTarget = false;
            overlay.Wordmark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            root.gameObject.SetActive(false);
            return overlay;
        }

        public void SetDelisted(bool delisted)
        {
            if (Root != null)
            {
                Root.gameObject.SetActive(delisted);
                if (delisted)
                {
                    Root.SetAsLastSibling();
                }
            }
        }
    }
}
