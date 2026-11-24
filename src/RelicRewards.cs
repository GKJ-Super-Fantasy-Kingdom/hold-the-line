using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace HoldTheLine
{
    internal static class RelicRewards
    {
        // Native 0.8.068 Addressables prefab names in the Relics and RelicsKingdom catalogs.
        private static readonly HashSet<string> Ordinary = LoadNames();
        private static HashSet<string> LoadNames()
        {
            using (var reader = new StreamReader(typeof(RelicRewards).Assembly.GetManifestResourceStream("HoldTheLine.OrdinaryRelics")))
                return new HashSet<string>(reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        }

        internal static string[] Draw(Wager wager)
        {
            var relics = RelicManager.Instance;
            var reserved = new HashSet<string>(wager.Unclaimed.SelectMany(p => p.Choices));
            var race = RaceManager.Instance.GetRace();
            var army = new HashSet<string>(UnitManager.Instance.GetAllUnits().Where(u => u != null)
                .SelectMany(u => u.GetRelatedRelics() ?? Enumerable.Empty<string>()));
            var eligible = AddressablesManager.Instance.GetAllRelics().Values.Where(r =>
                r != null && Ordinary.Contains(r.name) && r.GetIdentifier() == r.name && !r.IsLaw() && !r.IsRacial() && !r.SoldAtCartographer() &&
                (r.GetKingdom() == Races.None || r.GetKingdom() == race) &&
                r.GetRequiredKingdomLevel() <= RaceDataManager.Instance.GetKingdomLevel() &&
                (string.IsNullOrEmpty(r.GetRelatedUnit()) || army.Contains(r.GetIdentifier())))
                .Select(r => r.GetIdentifier()).Distinct()
                .Where(id => !reserved.Contains(id) && relics.IsUnlocked(id) && !relics.HasRelic(id) && relics.CanAddRelic(id));
            return RelicPrize.Draw(wager.Run, wager.Day, eligible);
        }

        internal static Action Show(RelicPrize prize, bool finalNight, Action<string> claim)
        {
            var overlays = OverlayManager.Instance;
            // DropChest and DropBossChest both use this merchant screen and its relic prefab.
            var cards = overlays.GetMerchantScreen();
            overlays.SetCanCloseOverlay(true);
            cards.Init("Relic Wager");
            cards.SetText($"Night {prize.Night} won. Choose one relic.");
            var selectable = new List<Button>();
            bool open = true;
            cards.SetUseCardSelectCallback(false);
            try
            {
                foreach (string id in prize.Choices)
                {
                    var relic = AddressablesManager.Instance.GetRelic(id);
                    if (relic == null) continue;
                    var card = cards.AddRelic(relic).GetComponent<UICardRelic>();
                    // Let the chest play its native selection animation, then settle the saved claim.
                    card.AddCallback(() =>
                    {
                        if (!open) return;
                        claim(id);
                        if (open) overlays.CloseOverlay();
                    });
                    var button = card.GetCardButton();
                    button.interactable = !RelicManager.Instance.HasRelic(id) && RelicManager.Instance.CanAddRelic(id);
                    if (button.interactable) selectable.Add(button);
                }
            }
            finally
            {
                cards.SetUseCardSelectCallback(true);
            }
            var cancelText = cards.transform.Find("Panel/Buttons/Cancel/Button/Text").GetComponent<TextMeshProUGUI>();
            var localize = cancelText.GetComponent<LocalizeStringEvent>();
            bool wasLocalized = localize != null && localize.enabled;
            string previousText = cancelText.text;
            if (wasLocalized) localize.enabled = false;
            var later = cards.SetCancel(finalNight ? "Finish Run" : "Claim Later");
            var cancel = later.GetComponent<Button>();
            var previousNavigation = cancel.navigation;
            if (selectable.Count > 0)
            {
                var center = selectable[selectable.Count / 2];
                cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = center,
                    selectOnDown = center, selectOnLeft = selectable[0], selectOnRight = selectable[selectable.Count - 1] };
                for (int i = 0; i < selectable.Count; i++)
                    selectable[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                        selectOnLeft = selectable[(i + selectable.Count - 1) % selectable.Count],
                        selectOnRight = selectable[(i + 1) % selectable.Count], selectOnUp = cancel, selectOnDown = cancel };
            }
            cards.Display();
            GamepadManager.Instance.SetGameObjectToSelect(later.gameObject, true);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(later.gameObject);
            return () =>
            {
                open = false;
                if (cancel != null) cancel.navigation = previousNavigation;
                if (cancelText != null) cancelText.text = previousText;
                if (wasLocalized && localize != null) localize.enabled = true;
            };
        }
    }
}
