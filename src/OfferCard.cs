using System;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HoldTheLine
{
    internal sealed class OfferCard
    {
        private readonly UIOverlayMessage view;
        private readonly Button accept, pass;
        private readonly int opened;
        private bool release;
        private readonly GameObject prompt;
        private readonly Image glyph;
        private readonly TextMeshProUGUI hint;
        private string scheme;
        internal bool Exists => view != null;

        private static InputAction Submit =>
            (EventSystem.current?.currentInputModule as InputSystemUIInputModule)?.submit?.action;

        internal OfferCard(int day, bool affordable, Action stake, Action decline)
        {
            view = OverlayManager.Instance.OverlayMessage(true);
            opened = Time.frameCount;
            release = Submit?.IsPressed() == true;
            view.SetMessage($"<b>Hold the Line · Night {day}</b>\nStake {Wager.Stake} coin. Keep the Castle Wall unharmed until night ends.\nWin {Wager.Payout} coins total (+{Wager.Payout - Wager.Stake} net). A repaired hit still loses." +
                (affordable ? "" : $"\nYou need {Wager.Stake} coin to enter."));
            var body = view.transform.Find("Message").GetComponent<TextMeshProUGUI>();
            float width = Mathf.Min(500f, Screen.width / SettingsManager.Instance.GetUIScaling() - 64f);
            body.GetComponent<LayoutElement>().preferredWidth = width;
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            body.enableWordWrapping = true;

            accept = ButtonAt("Buttons/BuyButton");
            pass = ButtonAt("Buttons/Cancel");
            view.SetAccept($"Stake {Wager.Stake} coin", () => Invoke(stake));
            view.SetCancel("Pass", () => Invoke(decline));
            accept.interactable = affordable;
            Link(accept, pass);
            Link(pass, affordable ? accept : pass);

            // Keep the native prefab's content fitter, parent canvas and UI scale.
            prompt = new GameObject("Controller hint", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            prompt.transform.SetParent(view.transform, false);
            prompt.transform.SetSiblingIndex(view.transform.Find("Buttons").GetSiblingIndex());
            var group = prompt.GetComponent<HorizontalLayoutGroup>();
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
            group.spacing = 5;
            var icon = new GameObject("Submit", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            icon.transform.SetParent(prompt.transform, false);
            glyph = icon.GetComponent<Image>();
            glyph.raycastTarget = false;
            glyph.preserveAspect = true;
            var size = icon.GetComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = 28;
            var label = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(prompt.transform, false);
            hint = label.GetComponent<TextMeshProUGUI>();
            hint.font = body.font;
            hint.fontSharedMaterial = body.fontSharedMaterial;
            hint.fontSize = body.fontSize;
            hint.color = body.color;
            hint.raycastTarget = false;
            Refresh();
            GamepadManager.Instance.SetGameObjectToSelect(pass.gameObject, true);
            EventSystem.current?.SetSelectedGameObject(pass.gameObject);
        }

        private Button ButtonAt(string path)
        {
            var button = view.transform.Find(path).GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            return button;
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
            if (view == null) return;
            if (release && Submit?.IsPressed() != true) release = false;
            var pad = GamepadManager.Instance;
            var current = pad.GetPlayerInput()?.currentControlScheme ?? "";
            bool show = pad.IsGamepadInUse();
            var events = EventSystem.current;
            if (show && view.gameObject.activeInHierarchy && !PauseMenuManger.Instance.InMenu() &&
                events != null && events.currentSelectedGameObject != pass.gameObject &&
                events.currentSelectedGameObject != accept.gameObject)
                pad.SetGameObjectToSelect(pass.gameObject, true);
            if (current == scheme && prompt.activeSelf == show) return;
            scheme = current;
            prompt.SetActive(show);
            if (!show) return;
            var action = Submit;
            int index = string.IsNullOrEmpty(scheme) ? -1 : action?.GetBindingIndex(scheme) ?? -1;
            glyph.sprite = index < 0 ? null : pad.GetButtonPrompt(action.bindings[index].effectivePath);
            // UI/Submit can resolve through the generic */{Submit} usage.
            if (glyph.sprite == null && action != null)
            {
                foreach (var control in action.controls)
                {
                    if (!(control.device is Gamepad)) continue;
                    glyph.sprite = pad.GetButtonPrompt("<Gamepad>/" + control.name);
                    if (glyph.sprite != null) break;
                }
            }
            glyph.gameObject.SetActive(glyph.sprite != null);
            hint.text = glyph.sprite == null && index >= 0 ? action.GetBindingDisplayString(index) + "  Select" : "Select";
        }
    }
}
