using System;
using System.IO;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HoldTheLine
{
    internal sealed class OfferCard
    {
        private static readonly Lazy<Sprite> Treasury = new Lazy<Sprite>(() => LoadArt("Coin"));
        private static readonly Lazy<Sprite> Reliquary = new Lazy<Sprite>(() => LoadArt("Relic"));
        private static readonly Lazy<Sprite> BronzeFrame = new Lazy<Sprite>(() => LoadArt("CoinFrame"));
        private static readonly Lazy<Sprite> SilverFrame = new Lazy<Sprite>(() => LoadArt("RelicFrame"));
        private const float CardWidth = 280f, CardHeight = 420f, Gap = 24f;
        private readonly UIOverlayMessage view;
        private readonly Vector2 center;
        private Vector2 viewport;
        private readonly Button accept, pass, relic, lower, higher;
        private readonly ChoiceEdge lowerEdge, higherEdge;
        private readonly StakeShortcutHint lowerHint, higherHint;
        private readonly TextMeshProUGUI priceText, coinRequirement, relicRequirement;
        private readonly Text stakeCount;
        private bool hasThree;
        private int profit = 1, lastCoins = -1;
        private readonly int opened, night;
        private bool release;
        private readonly TextMeshProUGUI body;
        internal bool Exists => view != null;

        private static InputSystemUIInputModule Input =>
            EventSystem.current?.currentInputModule as InputSystemUIInputModule;

        internal OfferCard(int day, bool relicAvailable, Action<int> stake, Action stakeRelic, Action decline)
        {
            night = day;
            hasThree = relicAvailable;
            var overlays = OverlayManager.Instance;
            var overlay = overlays.OverlayEmpty();
            UIManager.Instance.HideUI(alsoResources: false, alsoClock: true);
            var messagePrefab = Resources.Load<GameObject>("UI/OverlayMessage");
            ChooserPointerShield.Add(overlay, new Color32(9, 12, 19, 190));
            view = UnityEngine.Object.Instantiate(messagePrefab, overlay).GetComponent<UIOverlayMessage>();
            view.GetComponent<Image>().enabled = false;
            center = ((RectTransform)view.transform).anchoredPosition;
            FitDialog();
            opened = Time.frameCount;
            release = Input?.submit?.action.IsPressed() == true;
            body = view.transform.Find("Message").GetComponent<TextMeshProUGUI>();
            const float width = 2f * CardWidth + Gap;
            body.GetComponent<LayoutElement>().preferredWidth = width;
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            body.enableWordWrapping = true;
            body.color = new Color32(248, 233, 204, 255);
            var layout = view.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.padding = new RectOffset(0, 0, 8, 8);
            layout.spacing = 14f;
            Heading(width);
            view.SetMessage($"Keep the Castle Wall unharmed through Night {night}.\nAny damage costs your stake, even if repaired.");
            view.RemoveAccept();
            pass = view.transform.Find("Buttons/Cancel").GetComponent<Button>();
            view.SetCancel("Pass", () => Invoke(decline));
            pass.GetComponent<ContentSizeFitter>().enabled = false;
            pass.GetComponent<VerticalLayoutGroup>().enabled = false;
            var footerLabel = pass.transform.Find("Text").GetComponent<TextMeshProUGUI>();
            footerLabel.rectTransform.anchorMin = Vector2.zero;
            footerLabel.rectTransform.anchorMax = Vector2.one;
            footerLabel.rectTransform.offsetMin = footerLabel.rectTransform.offsetMax = Vector2.zero;
            ChooserFooter.Fit(pass, footerLabel);
            var actions = pass.transform.parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            actions.childAlignment = TextAnchor.MiddleCenter;
            actions.childControlWidth = actions.childControlHeight = false;
            actions.childForceExpandWidth = actions.childForceExpandHeight = false;

            var choices = Row("Wager cards", Gap);
            accept = Choice(choices, "Coin Wager", Treasury.Value, BronzeFrame.Value, () => stake(profit));
            relic = Choice(choices, "Relic Wager", Reliquary.Value, SilverFrame.Value, stakeRelic);

            Label(accept.transform, "Stake in coins", 282f, 27f, 25f);
            var worker = Resources.Load<GameObject>("UI/OverlayElementWorker");
            var amount = (RectTransform)UnityEngine.Object.Instantiate(worker.transform.Find("Amount"), accept.transform, false);
            amount.name = "Stake";
            Place(amount, 0f, 314f, 94f, 32f);
            lower = Arrow(amount, "Less", -1);
            higher = Arrow(amount, "More", 1);
            lowerEdge = ChoiceEdge.Add(lower, ChoiceEdge.Arrow.Value, new Vector2(26f / 12f, 2f));
            higherEdge = ChoiceEdge.Add(higher, ChoiceEdge.Arrow.Value, new Vector2(26f / 12f, 2f));
            lowerHint = ArrowHint(lower, -1);
            higherHint = ArrowHint(higher, 1);
            var box = amount.GetComponent<Image>() ?? amount.gameObject.AddComponent<Image>();
            box.color = new Color(.09f, .08f, .08f);
            box.raycastTarget = true;
            amount.gameObject.AddComponent<StakeCounter>();
            stakeCount = amount.Find("Text").GetComponent<Text>();
            stakeCount.color = new Color(1f, .96f, .84f);
            stakeCount.raycastTarget = false;
            stakeCount.resizeTextForBestFit = true;
            stakeCount.resizeTextMinSize = 11;
            stakeCount.resizeTextMaxSize = 24;
            priceText = Label(accept.transform, "", 369f, 60f, 26f);
            coinRequirement = Requirement(accept.transform, 391f, 23f);
            Label(relic.transform, $"Stake {Wager.RelicCost} coins", 308f, 46f, 29f);
            Label(relic.transform, "Choose 1 of 3 relics", 352f, 30f, 26f);
            relicRequirement = Requirement(relic.transform, 387f, 39f);
            UpdateTerms(ResourceManager.Instance.GetCoins());
            view.gameObject.AddComponent<ChooserNavigation>().Initialize(view.transform, pass);
        }

        private void FitDialog()
        {
            float uiScale = SettingsManager.Instance.GetUIScaling();
            var available = new Vector2(Screen.width, Screen.height) / uiScale;
            if (available == viewport) return;
            viewport = available;
            // Fit both portrait cards, the common dialog controls, and the resource HUD.
            float scale = Mathf.Min(1f, available.x / (CardWidth * 2 + Gap + 80f), available.y / 756f);
            var rect = (RectTransform)view.transform;
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = center + Vector2.down * (32f * scale);
        }

        private void Heading(float width)
        {
            var title = Label(view.transform, "Hold the Line", 0f, 52f, 42f);
            title.name = "Wager heading";
            title.fontStyle = FontStyles.Bold;
            title.transform.SetAsFirstSibling();
            var size = title.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = width;
            size.minHeight = size.preferredHeight = 52f;
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

        // Position a control from the top center of its card. Card widths follow the dialog.
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private Button Choice(GameObject row, string title, Sprite artwork, Sprite frameArt, Action action)
        {
            var card = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(WagerChoiceButton));
            card.transform.SetParent(row.transform, false);
            var size = card.GetComponent<LayoutElement>();
            size.preferredWidth = CardWidth;
            size.preferredHeight = CardHeight;
            var face = card.GetComponent<Image>();
            face.sprite = frameArt;
            var art = new GameObject("Illustration", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            art.transform.SetParent(card.transform, false);
            art.sprite = artwork;
            art.raycastTarget = false;
            // The inset and title align with the 140 x 210 pixel frame at twice its design size.
            Place(art.rectTransform, 0, 114f, 264f, 212f);
            var heading = Label(card.transform, title, 243f, 42f, 34f);
            heading.fontStyle = FontStyles.Bold;

            var button = card.GetComponent<WagerChoiceButton>();
            button.SetArtwork(face, art);
            ChoiceEdge.Add(button, ChoiceEdge.Card.Value, new Vector2(2f, 2f));
            button.onClick.AddListener(() =>
            {
                if (button.IsActive() && button.IsInteractable()) Invoke(action);
            });
            return button;
        }

        internal static Sprite LoadArt(string name)
        {
            using var stream = typeof(OfferCard).Assembly.GetManifestResourceStream("HoldTheLine.Art." + name);
            using var image = new MemoryStream();
            stream.CopyTo(image);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(texture, image.ToArray(), true);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private TextMeshProUGUI Label(Transform parent, string value, float y, float height, float size)
        {
            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.font = body.font;
            text.fontSharedMaterial = body.fontSharedMaterial;
            text.fontSize = size;
            text.color = new Color32(255, 245, 220, 255);
            text.raycastTarget = false;
            text.text = value;
            text.alignment = TextAlignmentOptions.Center;
            Place(text.rectTransform, 0, y, 0, height);
            text.rectTransform.anchorMin = new Vector2(0, 1);
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = new Vector2(-12f, height);
            return text;
        }

        private TextMeshProUGUI Requirement(Transform parent, float y, float height)
        {
            var text = Label(parent, "", y, height, 22f);
            text.name = "Requirement";
            text.color = new Color32(255, 211, 145, 255);
            return text;
        }

        private Button Arrow(RectTransform amount, string name, int step)
        {
            var button = amount.Find(name).GetComponent<Button>();
            var rect = (RectTransform)button.transform;
            Place(rect, step * 66f, 16f, 26f, 32f);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => Invoke(() => Adjust(step)));
            return button;
        }

        private StakeShortcutHint ArrowHint(Button arrow, int direction)
        {
            var hint = new GameObject("Shortcut", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            hint.transform.SetParent(arrow.transform, false);
            // The native left arrow mirrors its sprite. Keep the attached hint upright.
            var arrowTransform = arrow.transform;
            float facing = Mathf.Sign(Vector3.Dot(arrowTransform.right, arrowTransform.parent.right) * arrowTransform.localScale.x);
            Place(hint.rectTransform, 43f * direction * facing, 16f, 48f, 30f);
            hint.rectTransform.localScale = new Vector3(facing, 1f, 1f);
            // The shortcut label extends the arrow's mouse target.
            hint.color = Color.clear;
            var label = Label(hint.transform, "", 15f, 30f, 21f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 21f;
            label.enableWordWrapping = false;
            var glyph = new GameObject("Game glyph", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            glyph.transform.SetParent(hint.transform, false);
            Place(glyph.rectTransform, 0f, 15f, 46f, 28f);
            glyph.preserveAspect = true;
            glyph.raycastTarget = false;
            return new StakeShortcutHint(hint.gameObject, label, glyph);
        }

        private void UpdateShortcuts(GamepadManager pad)
        {
            var player = pad.GetPlayerInput();
            var actions = player?.actions;
            var left = actions?.FindAction("Overlay/Left");
            var right = actions?.FindAction("Overlay/Right");
            bool hasLeft = lowerHint.Refresh(left, player, pad, lower.interactable);
            bool hasRight = higherHint.Refresh(right, player, pad, higher.interactable);
            bool previous = hasLeft && left.WasPerformedThisFrame();
            bool next = hasRight && right.WasPerformedThisFrame();
            if (previous == next) return;
            var arrow = previous ? lower : higher;
            if (!arrow.IsInteractable()) return;
            (previous ? lowerEdge : higherEdge).Pulse();
            arrow.onClick.Invoke();
        }

        internal void RefreshRelicOffer(bool available)
        {
            hasThree = available;
            UpdateTerms(ResourceManager.Instance.GetCoins());
        }

        private void Adjust(int delta)
        {
            int next = profit + delta;
            if (!CoinTerms.Quote(next, out int stake, out _) || stake > lastCoins) return;
            profit = next;
            UpdateTerms(lastCoins);
            if (EventSystem.current?.currentSelectedGameObject?.GetComponent<Button>()?.interactable == false)
            {
                var target = delta > 0 ? lower : higher;
                EventSystem.current.SetSelectedGameObject(target.interactable ? target.gameObject : accept.gameObject);
            }
        }

        private void UpdateTerms(int coins)
        {
            lastCoins = coins;
            while (profit > 1 && CoinTerms.Quote(profit, out int cost, out _) && cost > coins) profit--;
            CoinTerms.Quote(profit, out int stake, out int total);
            stakeCount.text = stake.ToString();
            priceText.text = $"Total return {total}\nNet profit +{profit}";
            accept.interactable = coins >= stake && (long)coins + profit <= int.MaxValue;
            relic.interactable = hasThree && coins >= Wager.RelicCost;
            var pricePosition = priceText.rectTransform.anchoredPosition;
            pricePosition.y = accept.interactable ? -369f : -353f;
            priceText.rectTransform.anchoredPosition = pricePosition;
            coinRequirement.text = coins < stake ? NeedCoins(stake - coins)
                : accept.interactable ? "" : "Coin pouch is full";
            var relicCost = coins < Wager.RelicCost ? NeedCoins(Wager.RelicCost - coins) : "";
            relicRequirement.text = hasThree ? relicCost
                : relicCost.Length == 0 ? "Need 3 eligible relics" : relicCost + "\nNeed 3 eligible relics";
            lower.interactable = profit > 1;
            higher.interactable = CoinTerms.Quote(profit + 1, out int next, out _) && next <= coins && (long)coins + profit + 1 <= int.MaxValue;
            Button coinChoice = accept.interactable ? accept : pass;
            Button otherChoice = relic.interactable ? relic : pass;
            Button adjust = higher.interactable ? higher : lower.interactable ? lower : coinChoice;
            Navigate(pass, coinChoice, otherChoice == pass ? coinChoice : otherChoice, coinChoice, otherChoice);
            Navigate(accept, pass, otherChoice, pass, adjust == accept ? pass : adjust);
            Navigate(relic, coinChoice, pass, pass, pass);
            Navigate(lower, coinChoice, higher.interactable ? higher : otherChoice, coinChoice, pass);
            Navigate(higher, lower.interactable ? lower : coinChoice, otherChoice, coinChoice, pass);
        }

        private static string NeedCoins(int count) => count == 1 ? "Need 1 more coin" : $"Need {count} more coins";

        private static void Navigate(Button button, Button left, Button right, Button up, Button down)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = left,
                selectOnRight = right, selectOnUp = up, selectOnDown = down };
        }

        private void Invoke(Action action)
        {
            if (view != null && view.gameObject.activeInHierarchy && !release && Time.frameCount != opened)
                action();
        }

        internal void Refresh()
        {
            if (view == null || !view.gameObject.activeInHierarchy) return;
            FitDialog();
            var input = Input;
            if (release && input?.submit?.action.IsPressed() != true) release = false;
            var pad = GamepadManager.Instance;
            int coins = ResourceManager.Instance.GetCoins();
            if (lastCoins != coins) UpdateTerms(coins);
            if (PauseMenuManger.Instance.InMenu()) return;
            UpdateShortcuts(pad);
        }
    }

    // The readout is part of the selector. Clicking its digits must never place a bet.
    internal sealed class StakeCounter : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData _) { }
    }

    internal sealed class WagerChoiceButton : Button
    {
        private Image face, illustration;
        internal void SetArtwork(Image face, Image illustration)
        {
            this.face = face;
            this.illustration = illustration;
            targetGraphic = face;
            transition = Transition.None;
            DoStateTransition(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (face == null) return;
            bool disabled = state == SelectionState.Disabled;
            face.color = disabled ? new Color(.72f, .72f, .72f) : Color.white;
            illustration.color = disabled ? new Color(.52f, .52f, .52f) : Color.white;
        }
    }
}
