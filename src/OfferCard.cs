using System;
using System.IO;
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
        private static readonly Lazy<Sprite> Treasury = new Lazy<Sprite>(() => LoadArt("Coin"));
        private static readonly Lazy<Sprite> Reliquary = new Lazy<Sprite>(() => LoadArt("Relic"));
        private static readonly Lazy<Sprite> BronzeFrame = new Lazy<Sprite>(() => LoadArt("CoinFrame"));
        private static readonly Lazy<Sprite> SilverFrame = new Lazy<Sprite>(() => LoadArt("RelicFrame"));
        private const float CardWidth = 280f, CardHeight = 420f, Gap = 16f;
        private readonly UIOverlayMessage view;
        private readonly Vector2 center;
        private Vector2 viewport;
        private readonly Button accept, pass, relic, lower, higher;
        private readonly TextMeshProUGUI priceText, relicText;
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
            view = OverlayManager.Instance.OverlayMessage(true);
            center = ((RectTransform)view.transform).anchoredPosition;
            FitDialog();
            opened = Time.frameCount;
            release = Input?.submit?.action.IsPressed() == true;
            body = view.transform.Find("Message").GetComponent<TextMeshProUGUI>();
            const float width = 2f * CardWidth + Gap;
            body.GetComponent<LayoutElement>().preferredWidth = width;
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            body.enableWordWrapping = true;
            var layout = view.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 7f;
            Heading(width);
            view.SetMessage($"Keep the Castle Wall unharmed through Night {night}.\nAny damage costs your stake, even if repaired.");
            view.RemoveAccept();
            pass = view.transform.Find("Buttons/Cancel").GetComponent<Button>();
            view.SetCancel("Pass", () => Invoke(decline));
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
            Label(relic.transform, "Stake 20 coins", 308f, 46f, 29f);
            relicText = Label(relic.transform, "", 369f, 60f, 26f);
            UpdateTerms(ResourceManager.Instance.GetCoins());
            GamepadManager.Instance.SetGameObjectToSelect(pass.gameObject, true);
            EventSystem.current?.SetSelectedGameObject(pass.gameObject);
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
            rect.anchoredPosition = center + Vector2.down * (40f * scale);
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
            var card = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Button));
            card.transform.SetParent(row.transform, false);
            var size = card.GetComponent<LayoutElement>();
            size.preferredWidth = CardWidth;
            size.preferredHeight = CardHeight;
            var face = card.GetComponent<Image>();
            face.sprite = frameArt;
            var button = card.GetComponent<Button>();
            button.targetGraphic = face;
            var colors = button.colors;
            colors.normalColor = new Color(.94f, .94f, .94f);
            colors.selectedColor = colors.highlightedColor = Color.white;
            colors.disabledColor = new Color(.67f, .67f, .67f);
            button.colors = colors;
            // A bright border makes directional focus visible against either illustrated card.
            var frame = card.AddComponent<Outline>();
            frame.effectColor = Color.white;
            frame.effectDistance = new Vector2(3f, -3f);
            frame.enabled = false;
            card.AddComponent<WagerCardFocus>().Frame = frame;
            button.onClick.AddListener(() => Invoke(action));

            var art = new GameObject("Illustration", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            art.transform.SetParent(card.transform, false);
            art.sprite = artwork;
            art.raycastTarget = false;
            // The inset and title align with the 140 x 210 pixel frame at twice its design size.
            Place(art.rectTransform, 0, 114f, 264f, 212f);
            var heading = Label(card.transform, title, 243f, 42f, 34f);
            heading.fontStyle = FontStyles.Bold;
            return button;
        }

        private static Sprite LoadArt(string name)
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

        private Button Arrow(RectTransform amount, string name, int step)
        {
            var button = amount.Find(name).GetComponent<Button>();
            var rect = (RectTransform)button.transform;
            Place(rect, step * 66f, 16f, 26f, 32f);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => Invoke(() => Adjust(step)));
            return button;
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
            relicText.text = !hasThree ? "Fewer than 3 eligible relics" : coins < Wager.RelicCost
                ? $"Choose 1 of 3 relics\nNeed {Wager.RelicCost - coins} more coins" : "Choose 1 of 3 relics";
            accept.interactable = coins >= stake && (long)coins + profit <= int.MaxValue;
            relic.interactable = hasThree && coins >= Wager.RelicCost;
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
            var events = EventSystem.current;
            if (!PauseMenuManger.Instance.InMenu() && events != null &&
                (events.currentSelectedGameObject == null ||
                 events.currentSelectedGameObject.GetComponent<Button>()?.interactable == false))
            {
                pad.SetGameObjectToSelect(pass.gameObject, true);
                events.SetSelectedGameObject(pass.gameObject);
            }
        }
    }

    // The readout is part of the selector. Clicking its digits must never place a bet.
    internal sealed class StakeCounter : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData _) { }
    }

    internal sealed class WagerCardFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        internal Outline Frame;
        public void OnSelect(BaseEventData _) { Frame.enabled = true; }
        public void OnDeselect(BaseEventData _) { Frame.enabled = false; }
    }
}
