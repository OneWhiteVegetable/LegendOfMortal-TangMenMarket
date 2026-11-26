using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiMastheadFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        private UiMotion _motion;
        private Selectable _control;
        private RectTransform _coin, _chart;
        private Text _name, _price, _change, _label;
        private Image _icon, _face, _nameRule, _changeRule;
        private Shadow _priceShadow;
        private UiControlInkOffset _nameOffset, _priceOffset, _changeOffset, _labelOffset, _iconOffset;
        private Color _nameInk, _labelInk, _iconInk;
        private bool _pointer, _focus, _close;
        private float _progress = -1f;

        internal static void Bind(UiPrefabInstance view, UiFrontendServices services)
        {
            RectTransform header = view.Rect("Masthead");
            foreach (string path in new[] { "IndexButton", "MoneyBlock", "HeaderNav/RankingButton",
                "HeaderNav/InventoryButton", "HeaderNav/CloseButton" })
            {
                Transform target = header.Find(path);
                UiMastheadFeedback fx = target.gameObject.AddComponent<UiMastheadFeedback>();
                fx._motion = services.Motion;
                fx._control = target.GetComponent<Selectable>();
                fx._face = target.GetComponent<Image>();
                if (fx._control == null)
                    fx._control = target.gameObject.AddComponent<Selectable>();
                if (fx._face == null)
                {
                    fx._face = target.gameObject.AddComponent<Image>();
                    fx._face.color = Color.clear;
                }
                fx._close = target.name == "CloseButton";
                if (target.name == "IndexButton")
                {
                    fx._name = target.Find("IndexNameText").GetComponent<Text>();
                    fx._price = target.Find("IndexPriceText").GetComponent<Text>();
                    fx._change = target.Find("IndexChangeText").GetComponent<Text>();
                    fx._chart = (RectTransform)target.Find("IndexChartHost");
                    fx._nameOffset = fx._name.gameObject.AddComponent<UiControlInkOffset>();
                    fx._changeOffset = fx._change.gameObject.AddComponent<UiControlInkOffset>();
                    fx._priceOffset = fx._price.gameObject.AddComponent<UiControlInkOffset>();
                    fx._nameInk = fx._name.color;
                    fx._nameRule = Rule(fx._name);
                    fx._changeRule = Rule(fx._change);
                    fx._priceShadow = fx._price.gameObject.AddComponent<Shadow>();
                    fx._priceShadow.effectDistance = new Vector2(3f, -3f);
                    services.Overlays.AttachTooltip(target.gameObject, () => string.Empty,
                        () => fx._control.interactable ? "查看唐门指数大盘走势" : "此入口仅可查看货囊与资产",
                        () => string.Empty, false);
                }
                else if (target.name == "MoneyBlock")
                {
                    fx._coin = (RectTransform)target.Find("MoneyCoin");
                    services.Overlays.AttachTooltip(target.gameObject, "", "当前可用于结算的现金", "", false);
                }
                else
                {
                    fx._label = target.Find("Label").GetComponent<Text>();
                    fx._icon = target.Find("Icon").GetComponent<Image>();
                    fx._labelOffset = fx._label.gameObject.AddComponent<UiControlInkOffset>();
                    fx._iconOffset = fx._icon.gameObject.AddComponent<UiControlInkOffset>();
                    fx._labelInk = fx._label.color;
                    fx._iconInk = fx._icon.color;
                    if (fx._close)
                        services.Overlays.AttachTooltip(target.gameObject, "", "关闭当前窗口", "", false);
                }
                fx._control.transition = Selectable.Transition.None;
                fx._face.canvasRenderer.SetColor(Color.white);
                fx.Paint(0f);
            }
        }

        private static Image Rule(Text text)
        {
            GameObject child = new GameObject("FocusUnderline", typeof(RectTransform), typeof(Image));
            Image image = child.GetComponent<Image>();
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.SetParent(text.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -text.fontSize * 0.65f);
            return image;
        }

        public void OnPointerEnter(PointerEventData e) { _pointer = true; }
        public void OnPointerExit(PointerEventData e)
        {
            GameObject hit = e == null ? null : e.pointerCurrentRaycast.gameObject;
            _pointer = hit != null && hit.transform.IsChildOf(transform);
        }
        public void OnSelect(BaseEventData e) { _focus = !(e is PointerEventData); }
        public void OnDeselect(BaseEventData e) { _focus = false; }

        private void OnDisable()
        {
            _pointer = _focus = false;
            if (_control != null)
                Paint(0f);
        }

        private void LateUpdate()
        {
            float target = (_pointer || (_focus && UiFocusOrigin.KeyboardActive)) && _control.IsInteractable() ? 1f : 0f;
            if (_progress != target)
                Paint(_motion.ReducedMotion ? target : Mathf.MoveTowards(_progress, target, Time.unscaledDeltaTime / 0.16f));
        }

        private void Paint(float p)
        {
            _progress = p;
            if (_name != null)
            {
                _name.color = Color.Lerp(_nameInk, UiFormatting.Gold, p);
                SetRule(_nameRule, _name, UiFormatting.Gold, p, 3f);
                SetRule(_changeRule, _change, _change.color, p, 2f);
                _nameOffset.Set(new Vector2(3f * p, 0f));
                _priceOffset.Set(new Vector2(5f * p, 0f), 0.0875f * p);
                _changeOffset.Set(new Vector2(4f * p, 0f));
                _priceShadow.effectColor = new Color(0, 0, 0, p);
                _change.rectTransform.localRotation = Quaternion.Euler(0, 0, 4f * p);
                _change.rectTransform.localScale = Vector3.one * (1f + 0.07f * p);
                _chart.localScale = Vector3.one * (1f + 0.055f * p);
            }
            else if (_coin != null)
            {
                _coin.localRotation = Quaternion.Euler(0f, 0f, 8f * p);
                _coin.localScale = Vector3.one * (1f + 0.07f * p);
            }
            else if (_label != null)
            {
                Color ink = _close ? UiFormatting.Paper : UiFormatting.Ink;
                _labelOffset.Set(Vector2.up * (2f * p));
                _iconOffset.Set(Vector2.up * (2f * p));
                _label.color = Color.Lerp(_labelInk, ink, p);
                _icon.color = Color.Lerp(_iconInk, ink, p);
                _face.color = Color.Lerp(Color.clear, _close ? UiFormatting.Red : UiFormatting.Gold, p);
            }
        }

        private static void SetRule(Image rule, Text text, Color ink, float progress, float height)
        {
            rule.color = ink;
            rule.rectTransform.sizeDelta = new Vector2(Mathf.Min(text.rectTransform.rect.width, text.preferredWidth) * progress, height);
        }
    }
}
