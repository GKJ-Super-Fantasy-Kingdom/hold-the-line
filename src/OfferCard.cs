using System;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace HoldTheLine
{
    internal sealed class OfferCard
    {
        private readonly UIOverlayMessage view;
        private readonly Button accept, pass;
        private readonly int opened, night;
        private bool release;
        private readonly TextMeshProUGUI body;
        internal bool Exists => view != null;

        private static InputSystemUIInputModule Input =>
            EventSystem.current?.currentInputModule as InputSystemUIInputModule;

        internal OfferCard(int day, Action stake, Action decline)
        {
            night = day;
            view = OverlayManager.Instance.OverlayMessage(true);
            opened = Time.frameCount;
            release = Input?.submit?.action.IsPressed() == true;
            body = view.transform.Find("Message").GetComponent<TextMeshProUGUI>();
            float width = Mathf.Min(540f, Screen.width / SettingsManager.Instance.GetUIScaling() - 64f);
            body.GetComponent<LayoutElement>().preferredWidth = width;
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            body.enableWordWrapping = true;
            var layout = view.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 7f;
            Heading(width);
            var price = Row("Wager terms", 7f);
            Icon(price, "Coins", 28f, RaceManager.Instance.GetResourceIcon(ResourceType.Coins));
            Label(price, $"Stake {Wager.Stake} coin     Win {Wager.Payout} coins total (+{Wager.Payout - Wager.Stake} net)");

            accept = ButtonAt("Buttons/BuyButton");
            pass = ButtonAt("Buttons/Cancel");
            view.SetAccept($"Stake {Wager.Stake} coin", () => Invoke(stake));
            view.SetCancel("Pass", () => Invoke(decline));
            SetAffordable(ResourceManager.Instance.GetCoins() >= Wager.Stake);

            GamepadManager.Instance.SetGameObjectToSelect(pass.gameObject, true);
        }

        private void Heading(float width)
        {
            var source = OverlayManager.Instance.transform.Find("Overlay/SizeSelection/HeaderContainer/Header");
            if (source == null) return;
            var banner = UnityEngine.Object.Instantiate(source, view.transform, false);
            banner.name = "Wager heading";
            banner.SetAsFirstSibling();
            banner.gameObject.SetActive(true);
            var size = banner.GetComponent<LayoutElement>() ?? banner.gameObject.AddComponent<LayoutElement>();
            size.ignoreLayout = false;
            size.preferredWidth = width;
            size.minHeight = size.preferredHeight = 46f;
            var title = banner.Find("Text").GetComponent<TextMeshProUGUI>();
            var localized = title.GetComponent<LocalizeStringEvent>();
            if (localized != null) localized.enabled = false;
            title.text = "Hold the Line";
            // The ribbon's text pivot sits above the center of its painted face.
            title.rectTransform.anchoredPosition += Vector2.down * 4f;
            foreach (var graphic in banner.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        }

        private GameObject Row(string name, float spacing)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(view.transform, false);
            row.transform.SetSiblingIndex(view.transform.Find("Buttons").GetSiblingIndex());
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = spacing;
            return row;
        }

        private static void Icon(GameObject row, string name, float pixels, Sprite sprite)
        {
            var icon = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            icon.transform.SetParent(row.transform, false);
            var image = icon.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.sprite = sprite;
            icon.SetActive(sprite != null);
            var size = icon.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = size.minHeight = size.preferredHeight = pixels;
        }

        private void Label(GameObject row, string value)
        {
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(row.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = body.font;
            text.fontSharedMaterial = body.fontSharedMaterial;
            text.fontSize = body.fontSize;
            text.color = body.color;
            text.raycastTarget = false;
            text.text = value;
        }

        private Button ButtonAt(string path)
        {
            var button = view.transform.Find(path).GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            return button;
        }

        private void SetAffordable(bool affordable)
        {
            string title = view.transform.Find("Wager heading") == null ? "Hold the Line\n" : "";
            view.SetMessage(title + $"Keep the Castle Wall unharmed through Night {night}.\nAny damage costs your stake, even if repaired." +
                (affordable ? "" : $"\nYou need {Wager.Stake} coin to enter."));
            accept.interactable = affordable;
            Link(accept, pass);
            Link(pass, affordable ? accept : pass);
        }

        private static void Link(Button button, Button other)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = other,
                selectOnRight = other, selectOnUp = other, selectOnDown = other };
        }

        private void Invoke(Action action)
        {
            if (view != null && view.gameObject.activeInHierarchy && !release && Time.frameCount != opened)
                action();
        }

        internal void Refresh()
        {
            if (view == null || !view.gameObject.activeInHierarchy) return;
            var input = Input;
            if (release && input?.submit?.action.IsPressed() != true) release = false;
            var pad = GamepadManager.Instance;
            bool affordable = ResourceManager.Instance.GetCoins() >= Wager.Stake;
            if (accept.interactable != affordable) SetAffordable(affordable);
            var events = EventSystem.current;
            if (pad.IsGamepadInUse() && !PauseMenuManger.Instance.InMenu() && events != null &&
                (events.currentSelectedGameObject == null ||
                 (!affordable && events.currentSelectedGameObject == accept.gameObject)))
                pad.SetGameObjectToSelect(pass.gameObject, true);
        }
    }
}
