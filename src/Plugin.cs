using System;
using BepInEx;
using HarmonyLib;
using SuperFantasyKingdom;
using UnityEngine;

namespace HoldTheLine
{
    [BepInPlugin("gkj.holdtheline", "Hold the Line", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Wager Current;
        private Harmony patches;
        private OfferCard card;
        private float readySince = -1;
        private bool announced;

        private void Awake()
        {
            patches = new Harmony("gkj.holdtheline");
            try { patches.PatchAll(typeof(Plugin).Assembly); }
            catch (Exception error)
            {
                enabled = false;
                patches.UnpatchSelf();
                Logger.LogError("Hold the Line could not install its game hooks. " + error);
                return;
            }
            GameManager.OnGameInit += Reset;
            GameManager.BeforeStateChange += Transition;
            OverlayManager.OnOverlayClose += Closed;
            Logger.LogInfo("Hold the Line ready.");
        }

        private void OnDestroy()
        {
            GameManager.OnGameInit -= Reset;
            GameManager.BeforeStateChange -= Transition;
            OverlayManager.OnOverlayClose -= Closed;
            patches?.UnpatchSelf();
            Reset();
        }

        private void Reset()
        {
            Current = null;
            card = null;
            readySince = -1;
            announced = false;
        }

        private static bool HasWall() => WallManager.Instance != null && WallManager.Instance.HasWall() &&
            WallManager.Instance.GetWallHitpoints() > 0f &&
            (CityManager.Instance.DidCityEventHappen(CityEvent.WallsWood) ||
             CityManager.Instance.DidCityEventHappen(CityEvent.WallsStone));

        private static bool BeforeBattle(DaytimeManager clock) => (clock.IsMorning() || clock.IsDay()) &&
            !CombatManager.Instance.IsCombat() && !CombatManager.Instance.IsCombatOver() &&
            CombatManager.Instance.CurrentMonstersAmount() == 0;

        private void Update()
        {
            if (card != null)
            {
                if (card.Exists) { card.Refresh(); return; }
                Closed();
            }
            var game = GameManager.Instance;
            if (game == null || !game.IsGameRunning() || game.IsGameOver() || game.IsUnloading()) return;
            var clock = DaytimeManager.Instance;
            var run = GameDataManager.Instance.GetRunID();
            int day = clock.CurrentDay();
            if (!announced && Current != null && Current.Run == run && Current.Day == day - 1 && clock.IsMorning())
            {
                announced = true;
                if (Current.State == Decision.Paid || Current.State == Decision.Lost)
                    AlertManager.Instance.DisplayMessage("Hold the Line", Current.State == Decision.Paid
                        ? $"The wall held. You won {Wager.Payout} coins." : "The wall wager was lost.");
            }

            if (!Wager.CanOffer(Current, run, day, HasWall(), BeforeBattle(clock)) ||
                !UIManager.Instance.IsWorldInteractable() || DialogueManager.Instance.IsInDialogue())
            {
                readySince = -1;
                return;
            }
            // Let daily events and other native overlays claim focus first.
            if (readySince < 0) readySince = Time.unscaledTime;
            if (Time.unscaledTime - readySince < .65f) return;
            var wager = Current = new Wager { Run = run, Day = day };
            card = new OfferCard(day, ResourceManager.Instance.GetCoins() >= Wager.Stake,
                () => Accept(wager), () => Close(wager));
        }

        private void Accept(Wager wager)
        {
            if (card == null || Current != wager) return;
            var game = GameManager.Instance;
            var clock = DaytimeManager.Instance;
            var purse = ResourceManager.Instance;
            if (game != null && !game.IsUnloading() && game.IsGameRunning() && !game.IsGameOver() &&
                wager.Matches(GameDataManager.Instance.GetRunID(), clock.CurrentDay()) && HasWall() &&
                BeforeBattle(clock) && wager.Accept(purse.GetCoins(), purse.AddToCoins))
            {
                Notify(() => purse.ResourceAmountChanged(ResourceType.Coins));
                Logger.LogInfo($"Night {wager.Day}: 1 coin staked.");
            }
            Close(wager);
        }

        private void Close(Wager wager)
        {
            if (card != null && Current == wager) OverlayManager.Instance.CloseOverlay();
        }

        private void Closed()
        {
            if (card == null) return;
            Current?.Pass();
            card = null;
        }

        private void Transition(GameState state)
        {
            bool survived = state == GameState.Survived || state == GameState.Win;
            if (!survived && state != GameState.Lose && state != GameState.GiveUp) return;
            if (Current == null || !Current.Matches(GameDataManager.Instance.GetRunID(),
                DaytimeManager.Instance.CurrentDay())) return;
            var resources = ResourceManager.Instance;
            bool completed = survived && (CombatManager.Instance.IsCombatOver() || DaytimeManager.Instance.IsNight());
            if (Current.Finish(completed, resources.AddToCoins))
            {
                Notify(() => StatisticsManager.Instance.AddCoins(Wager.Payout));
                Notify(() => resources.ResourceAmountChanged(ResourceType.Coins));
                Logger.LogInfo($"Night {Current.Day}: paid {Wager.Payout} coins.");
            }
        }

        private void Notify(Action publish)
        {
            try { publish(); }
            catch (Exception error) { Logger.LogWarning("Wager recorded. Coin notification failed: " + error.Message); }
        }
    }
}
