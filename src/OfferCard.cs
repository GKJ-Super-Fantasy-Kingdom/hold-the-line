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
        private static readonly Color32 DialogInk = new Color32(46, 32, 24, 255);
        private static readonly Color32 OfferText = new Color32(248, 233, 204, 255);
        private static readonly Color32 OfferHeading = new Color32(255, 245, 220, 255);
        private const float CardWidth = 280f, CardHeight = 420f, Gap = 24f;
        private const string ConfirmWager = "Confirm Wager";
        private const string StakeInCoins = "Stake in Coins";
        private readonly UIOverlayMessage view;
        private readonly Vector2 center;
        private Vector2 viewport;
        private readonly Button accept, pass, relic, lower, higher, confirm;
        private readonly GameObject choices;
        private readonly ChooserNavigation navigation;
        private readonly ChoiceEdge lowerEdge, higherEdge;
        private readonly StakeShortcutHint lowerHint, higherHint;
        private readonly RectTransform stakeControls;
        private readonly TextMeshProUGUI stakeLabel, priceText, coinRequirement, relicRequirement;
        private readonly Text stakeCount;
        private bool hasThree;
        private int profit = 1, lastCoins = -1;
        private readonly int night;
        private int enteredFrame, backFrame = -1;
        private int? confirmationProfit;
        private bool release;
        private readonly TextMeshProUGUI body, heading, backLabel;
        internal bool Exists => view != null;

        private static InputSystemUIInputModule Input =>
            EventSystem.current?.currentInputModule as InputSystemUIInputModule;

        internal OfferCard(int day, bool relicAvailable, Action<int> stake, Action decline)
        {
            night = day;
            hasThree = relicAvailable;
            var overlays = OverlayManager.Instance;
            var overlay = overlays.OverlayEmpty();
            UIManager.Instance.HideUI(alsoResources: false, alsoClock: true);
            var messagePrefab = Resources.Load<GameObject>("UI/OverlayMessage");
            ChooserPointerShield.Add(overlay, new Color32(9, 12, 19, 190));
            view = UnityEngine.Object.Instantiate(messagePrefab, overlay).GetComponent<UIOverlayMessage>();
            var panel = view.GetComponent<Image>();
            panel.enabled = false;
            panel.raycastTarget = false;
            center = ((RectTransform)view.transform).anchoredPosition;
            FitChooser();
            GuardNextInput();
            body = view.transform.Find("Message").GetComponent<TextMeshProUGUI>();
            const float width = 2f * CardWidth + Gap;
            body.GetComponent<LayoutElement>().preferredWidth = width;
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            body.enableWordWrapping = true;
            body.raycastTarget = false;
            var layout = view.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.padding = new RectOffset(0, 0, 8, 8);
            layout.spacing = 14f;
            heading = Heading(width);
            ShowOfferText();
            pass = view.transform.Find("Buttons/Cancel").GetComponent<Button>();
            view.SetCancel("Pass", () => Invoke(() =>
            {
                if (confirmationProfit.HasValue) BackToOffer();
                else decline();
            }));
            backLabel = pass.transform.Find("Text").GetComponent<TextMeshProUGUI>();
            FitFooter(pass, backLabel);
            confirm = view.transform.Find("Buttons/BuyButton").GetComponent<Button>();
            confirm.onClick = new Button.ButtonClickedEvent();
            view.SetAccept(ConfirmWager, () => Invoke(() =>
            {
                if (confirmationProfit is int selectedProfit && confirm.IsInteractable()) stake(selectedProfit);
            }));
            confirm.transform.Find("Text").gameObject.SetActive(false);
            var confirmLabel = Label(confirm.transform, ConfirmWager, 0f, 44f, 30f);
            confirmLabel.name = "Confirm label";
            confirmLabel.color = DialogInk;
            confirmLabel.enableWordWrapping = false;
            FitFooter(confirm, confirmLabel);
            SetReviewFooter(confirm, confirmLabel, true);
            var confirmTextRect = confirmLabel.rectTransform;
            confirmTextRect.anchorMin = confirmTextRect.anchorMax = confirmTextRect.pivot = new Vector2(.5f, .5f);
            confirmTextRect.anchoredPosition = Vector2.zero;
            confirmTextRect.sizeDelta = new Vector2(214f, 40f);
            confirm.gameObject.SetActive(false);
            var actions = pass.transform.parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            actions.childAlignment = TextAnchor.MiddleCenter;
            actions.childControlWidth = actions.childControlHeight = false;
            actions.childForceExpandWidth = actions.childForceExpandHeight = false;
            actions.spacing = Gap;

            choices = Row("Wager cards", Gap);
            accept = Choice(choices, "Coin Wager", Treasury.Value, BronzeFrame.Value, () => OpenConfirmation(profit));
            relic = Choice(choices, "Relic Wager", Reliquary.Value, SilverFrame.Value, () => OpenConfirmation(0));

            stakeLabel = Label(accept.transform, StakeInCoins, 282f, 27f, 25f);
            var worker = Resources.Load<GameObject>("UI/OverlayElementWorker");
            stakeControls = (RectTransform)UnityEngine.Object.Instantiate(worker.transform.Find("Amount"), accept.transform, false);
            stakeControls.name = "Stake";
            Place(stakeControls, 0f, 314f, 94f, 32f);
            lower = Arrow(stakeControls, "Less", -1);
            higher = Arrow(stakeControls, "More", 1);
            lowerEdge = ChoiceEdge.Add(lower, ChoiceEdge.Arrow.Value, new Vector2(26f / 12f, 2f));
            higherEdge = ChoiceEdge.Add(higher, ChoiceEdge.Arrow.Value, new Vector2(26f / 12f, 2f));
            lowerHint = ArrowHint(lower, -1);
            higherHint = ArrowHint(higher, 1);
            var box = stakeControls.GetComponent<Image>() ?? stakeControls.gameObject.AddComponent<Image>();
            box.color = new Color(.09f, .08f, .08f);
            box.raycastTarget = true;
            stakeControls.gameObject.AddComponent<StakeCounter>();
            stakeCount = stakeControls.Find("Text").GetComponent<Text>();
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
            navigation = view.gameObject.AddComponent<ChooserNavigation>();
            navigation.Initialize(view.transform, pass);
        }

        private static void FitFooter(Button button, TextMeshProUGUI label)
        {
            var fitter = button.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            var layout = button.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            ChooserFooter.Fit(button, label);
        }

        private static void SetReviewFooter(Button button, TextMeshProUGUI label, bool reviewing)
        {
            var rect = (RectTransform)button.transform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, reviewing ? 44f : 36f);
            label.enableAutoSizing = !reviewing;
            if (reviewing) label.fontSize = 30f;
        }

        private void ShowOfferText()
        {
            heading.text = "Hold the Line";
            heading.color = OfferHeading;
            body.color = OfferText;
            view.SetMessage($"Keep the Castle Wall unharmed through Night {night}.\nAny damage costs your stake, even if repaired.");
        }

        private void OpenConfirmation(int selectedProfit)
        {
            if (confirmationProfit.HasValue) return;
            confirmationProfit = selectedProfit;
            GuardNextInput();
            heading.text = "Review Wager";
            backLabel.text = "Back";
            SetReviewFooter(pass, backLabel, true);
            accept.gameObject.SetActive(selectedProfit != 0);
            relic.gameObject.SetActive(selectedProfit == 0);
            var selected = selectedProfit == 0 ? relic : accept;
            // The card remains visible for review. Only the footer can act.
            selected.enabled = false;
            selected.targetGraphic.raycastTarget = false;
            selected.GetComponent<ChoiceEdge>().enabled = false;
            choices.transform.SetSiblingIndex(body.transform.GetSiblingIndex());
            stakeControls.gameObject.SetActive(false);
            SetStakeLabelPosition(308f, 46f);
            confirm.gameObject.SetActive(true);
            UpdateTerms(ResourceManager.Instance.GetCoins());
            navigation.Focus(pass);
        }

        private void BackToOffer()
        {
            var source = confirmationProfit == 0 ? relic : accept;
            confirmationProfit = null;
            backFrame = Time.frameCount;
            GuardNextInput();
            ShowOfferText();
            backLabel.text = "Pass";
            SetReviewFooter(pass, backLabel, false);
            confirm.gameObject.SetActive(false);
            source.enabled = true;
            source.targetGraphic.raycastTarget = true;
            source.GetComponent<ChoiceEdge>().enabled = true;
            accept.gameObject.SetActive(true);
            relic.gameObject.SetActive(true);
            body.transform.SetSiblingIndex(choices.transform.GetSiblingIndex());
            stakeControls.gameObject.SetActive(true);
            stakeLabel.text = StakeInCoins;
            SetStakeLabelPosition(282f, 27f);
            UpdateTerms(ResourceManager.Instance.GetCoins());
            navigation.Focus(source.IsInteractable() ? source : pass);
        }

        private void SetStakeLabelPosition(float y, float height)
        {
            var rect = stakeLabel.rectTransform;
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        internal bool HandleConfirmationBack(OverlayManager overlays)
        {
            if (view == null || !view.gameObject.activeInHierarchy || !overlays.IsOpen() ||
                view.transform.parent != overlays.GetOverlayEmpty() || PauseMenuManger.Instance.InMenu()) return false;
            if (backFrame == Time.frameCount) return true;
            if (!confirmationProfit.HasValue) return false;
            if (Time.frameCount != enteredFrame) BackToOffer();
            return true;
        }

        private void FitChooser()
        {
            float uiScale = SettingsManager.Instance.GetUIScaling();
            var available = new Vector2(Screen.width, Screen.height) / uiScale;
            if (available == viewport) return;
            viewport = available;
            // Fit the portrait cards and footers with room for the resource HUD.
            float scale = Mathf.Min(1f, available.x / (CardWidth * 2 + Gap + 80f), available.y / 756f);
            var rect = (RectTransform)view.transform;
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = center + Vector2.down * (32f * scale);
        }

        private TextMeshProUGUI Heading(float width)
        {
            var title = Label(view.transform, "Hold the Line", 0f, 52f, 42f);
            title.name = "Wager heading";
            title.fontStyle = FontStyles.Bold;
            title.transform.SetAsFirstSibling();
            var size = title.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = width;
            size.minHeight = size.preferredHeight = 52f;
            return title;
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
            button.onClick.AddListener(() => Invoke(() => { if (!confirmationProfit.HasValue) Adjust(step); }));
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
            if (confirmationProfit is int selectedProfit)
            {
                UpdateConfirmation(selectedProfit, coins);
                return;
            }
            while (profit > 1 && CoinTerms.Quote(profit, out int cost, out _) && cost > coins) profit--;
            CoinTerms.Quote(profit, out int stake, out int total);
            stakeCount.text = stake.ToString();
            priceText.text = $"Total Return {total}\nNet Profit +{profit}";
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

        private void UpdateConfirmation(int selectedProfit, int coins)
        {
            int stake = Wager.RelicCost;
            string requirement = hasThree ? "" : "Need 3 eligible relics";
            if (selectedProfit != 0)
            {
                CoinTerms.Quote(selectedProfit, out stake, out int total);
                stakeLabel.text = stake == 1 ? "Stake 1 coin" : $"Stake {stake} coins";
                priceText.text = $"Total Return {total}\nNet Profit +{selectedProfit}";
                requirement = (long)coins + selectedProfit <= int.MaxValue ? "" : "Coin pouch is full";
            }
            if (coins < stake) requirement = NeedCoins(stake - coins);
            view.SetMessage($"Keep the Castle Wall unharmed through Night {night}.\nAny damage costs your stake, even if repaired." +
                (requirement.Length == 0 ? "" : "\n" + requirement));
            confirm.interactable = requirement.Length == 0;
            var target = confirm.IsInteractable() ? confirm : pass;
            Navigate(pass, target, target, target, target);
            Navigate(confirm, pass, pass, pass, pass);
        }

        private static string NeedCoins(int count) => count == 1 ? "Need 1 more coin" : $"Need {count} more coins";

        private static void Navigate(Button button, Button left, Button right, Button up, Button down)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = left,
                selectOnRight = right, selectOnUp = up, selectOnDown = down };
        }

        private void GuardNextInput()
        {
            enteredFrame = Time.frameCount;
            release = Input?.submit?.action.IsPressed() == true;
        }

        private void Invoke(Action action)
        {
            if (view != null && view.gameObject.activeInHierarchy && !release && Time.frameCount != enteredFrame)
                action();
        }

        internal void Refresh()
        {
            if (view == null || !view.gameObject.activeInHierarchy) return;
            FitChooser();
            var input = Input;
            if (release && input?.submit?.action.IsPressed() != true) release = false;
            var pad = GamepadManager.Instance;
            int coins = ResourceManager.Instance.GetCoins();
            if (lastCoins != coins) UpdateTerms(coins);
            if (confirmationProfit.HasValue || PauseMenuManger.Instance.InMenu()) return;
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
