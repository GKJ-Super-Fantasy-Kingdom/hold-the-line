using System;
using SuperFantasyKingdom;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HoldTheLine
{
    internal sealed class ChoiceEdge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISubmitHandler
    {
        internal static readonly Lazy<Sprite> Card = new Lazy<Sprite>(() => OfferCard.LoadArt("CardEdge"));
        internal static readonly Lazy<Sprite> Arrow = new Lazy<Sprite>(() => OfferCard.LoadArt("ArrowEdge"));
        private Button button;
        private GameObject edge;
        private PointerEventData pointer;
        private float pressedUntil;

        internal static ChoiceEdge Add(Button button, Sprite sprite, Vector2 outset, Image plate = null)
        {
            var edge = new GameObject("Interaction edge", typeof(RectTransform), typeof(Image));
            var image = edge.GetComponent<Image>();
            image.sprite = sprite;
            if (plate != null)
            {
                image.type = Image.Type.Sliced;
                image.fillCenter = false;
                image.pixelsPerUnitMultiplier = plate.pixelsPerUnitMultiplier;
            }
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.SetParent(button.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = -outset;
            rect.offsetMax = outset;
            edge.SetActive(false);
            var interaction = button.gameObject.AddComponent<ChoiceEdge>();
            interaction.button = button;
            interaction.edge = edge;
            return interaction;
        }

        internal void Pulse() => pressedUntil = Time.unscaledTime + .14f;

        internal void Remove()
        {
            enabled = false;
            Destroy(edge);
            Destroy(this);
        }

        public void OnSubmit(BaseEventData _) => Pulse();
        public void OnPointerEnter(PointerEventData input) => pointer = input;
        public void OnPointerExit(PointerEventData _) => pointer = null;

        private void LateUpdate()
        {
            // The arrows and readout are inside the Coin Wager card. Follow
            // the actual click handler so hovering an arrow highlights it alone.
            var hit = pointer?.pointerCurrentRaycast.gameObject;
            bool hovering = hit != null && !GamepadManager.Instance.IsGamepadInUse() &&
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) == gameObject;
            bool focused = EventSystem.current?.currentSelectedGameObject == gameObject;
            bool visible = button.IsInteractable() && (hovering || focused || Time.unscaledTime < pressedUntil);
            if (edge.activeSelf != visible) edge.SetActive(visible);
        }

        private void OnDisable()
        {
            pointer = null;
            pressedUntil = 0f;
            if (edge != null) edge.SetActive(false);
        }
    }
}
