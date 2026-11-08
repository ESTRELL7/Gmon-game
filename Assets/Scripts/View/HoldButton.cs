using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepDwarf
{
    /// <summary>On-screen control that stays active for as long as it is pressed.</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }

        Image image;
        Sprite normal, pressed;

        public void Init(Image target)
        {
            image = target;
            normal = Art.Sprite("ui_button");
            pressed = Art.Sprite("ui_button_down");
        }

        public void OnPointerDown(PointerEventData eventData) => SetHeld(true);
        public void OnPointerUp(PointerEventData eventData) => SetHeld(false);
        public void OnPointerExit(PointerEventData eventData) => SetHeld(false);
        void OnDisable() => SetHeld(false);

        void SetHeld(bool held)
        {
            Held = held;
            if (image != null) image.sprite = held ? pressed : normal;
        }
    }
}
