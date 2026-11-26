using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiFocusOrigin : MonoBehaviour, IPointerDownHandler, IMoveHandler, ISelectHandler
    {
        internal static bool KeyboardActive { get; private set; }

        internal static void Attach(Selectable control)
        {
            if (control.GetComponent<UiFocusOrigin>() == null)
                control.gameObject.AddComponent<UiFocusOrigin>();
        }

        public void OnPointerDown(PointerEventData data) { KeyboardActive = false; }
        public void OnMove(AxisEventData data) { KeyboardActive = true; }
        public void OnSelect(BaseEventData data) { KeyboardActive = !(data is PointerEventData); }
        internal static void FromKeyboard() { KeyboardActive = true; }

        internal static void Restore(EventSystem events, GameObject target)
        {
            if (KeyboardActive || target == null)
                events.SetSelectedGameObject(target);
            else
                events.SetSelectedGameObject(target, new PointerEventData(events));
        }
    }
}
