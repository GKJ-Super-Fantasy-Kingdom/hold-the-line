using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HoldTheLine
{
    internal sealed class ChooserPointerShield : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        internal static GameObject Add(Transform chooser, Color color)
        {
            // SizeEmpty and SizeMerchant stretch across the overlay. Put the shield
            // above the overlay's click-to-close image and behind the actual controls.
            var shield = new GameObject("Hold the Line input shield", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)shield.transform;
            rect.SetParent(chooser, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            shield.GetComponent<Image>().color = color;
            shield.AddComponent<ChooserPointerShield>();
            return shield;
        }

        public void OnPointerDown(PointerEventData eventData) => eventData.Use();
        public void OnPointerClick(PointerEventData eventData) => eventData.Use();
    }
}
