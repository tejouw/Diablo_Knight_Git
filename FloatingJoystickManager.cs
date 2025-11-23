using UnityEngine;
using UnityEngine.EventSystems;

namespace DuloGames.UI
{
    [AddComponentMenu("UI/Floating Joystick Manager", 37)]
    public class FloatingJoystickManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private UIJoystick m_Joystick;
        [SerializeField] private RectTransform m_JoystickContainer;
        
        private bool m_IsDragging = false;
        private Vector2 m_StartPosition;

        private void Awake()
        {
            // Tüm ekranı kaplayacak şekilde ayarla
            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

public void OnPointerDown(PointerEventData eventData)
{
    if (m_Joystick == null || m_JoystickContainer == null) return;

    // Screen point'i local point'e çevir
    Vector2 localPoint;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(
        transform as RectTransform,
        eventData.position,
        eventData.pressEventCamera,
        out localPoint);

    // Joystick container'ı doğru pozisyona yerleştir
    m_JoystickContainer.localPosition = localPoint;
    m_StartPosition = localPoint;
    
    // Joystick'i göster
    m_Joystick.SetJoystickVisibility(true);
    m_Joystick.SetAxis(Vector2.zero);
}

        public void OnBeginDrag(PointerEventData eventData)
        {
            m_IsDragging = true;
            if (m_Joystick != null)
            {
                m_Joystick.OnBeginDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!m_IsDragging || m_Joystick == null) return;

            // Joystick'in drag işlemini başlat
            m_Joystick.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            m_IsDragging = false;
            if (m_Joystick != null)
            {
                m_Joystick.OnEndDrag(eventData);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            m_IsDragging = false;
            if (m_Joystick != null)
            {
                m_Joystick.SetJoystickVisibility(false);
                m_Joystick.SetAxis(Vector2.zero);
            }
        }
    }
}