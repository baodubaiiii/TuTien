using UnityEngine;
using UnityEngine.EventSystems;

namespace TuTien.Client
{
    public class DemoJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Handle;
        public Vector2 Direction { get; private set; }

        public void OnPointerDown(PointerEventData data)
        {
            OnDrag(data);
        }

        public void OnDrag(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, data.position, data.pressEventCamera, out Vector2 point);
            Direction = Vector2.ClampMagnitude(point / 55f, 1);
            Handle.anchoredPosition = Direction * 40;
        }

        public void OnPointerUp(PointerEventData data)
        {
            Direction = Vector2.zero;
            Handle.anchoredPosition = Vector2.zero;
        }

        private void OnDisable()
        {
            Direction = Vector2.zero;
            if (Handle)
            {
                Handle.anchoredPosition = Vector2.zero;
            }
        }
    }
}
