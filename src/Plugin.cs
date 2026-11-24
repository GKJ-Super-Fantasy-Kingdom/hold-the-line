using System;
using BepInEx;
using HarmonyLib;
using SuperFantasyKingdom;
using UnityEngine;

namespace HoldTheLine
{
    [BepInPlugin("gkj.holdtheline", "Hold the Line", "0.1.4")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Wager Current;
        private Harmony patches;
        private OfferCard card;
        private float readySince = -1;
        private Wager announced;
        private float nextOfferAt;

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
            announced = null;
            nextOfferAt = 0f;
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
            bool worldReady = UIManager.Instance.IsWorldInteractable() && !DialogueManager.Instance.IsInDialogue();
            if (Current != null && announced != Current && Current.Run == run && worldReady)
            {
                bool previousNight = Current.Day == day - 1;
                bool wallHit = Current.Day == day && Current.State == Decision.Lost;
                if ((previousNight || wallHit) && ShowResult(Current, wallHit))
                {
                    announced = Current;
                    // Give the native alert time to finish before the next wager opens.
                    nextOfferAt = Time.unscaledTime + 5f;
                }
            }

            if (!Wager.CanOffer(Current, run, day, HasWall(), BeforeBattle(clock)) ||
                !worldReady || Time.unscaledTime < nextOfferAt)
            {
                readySince = -1;
                return;
            }
            // Let daily events and other native overlays claim focus first.
            if (readySince < 0) readySince = Time.unscaledTime;
            if (Time.unscaledTime - readySince < .65f) return;
            var wager = Current = new Wager { Run = run, Day = day };
            card = new OfferCard(day,
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
            if ((state == GameState.Win || state == GameState.Lose) && announced != Current)
                Notify(() => { if (ShowResult(Current)) announced = Current; });
        }

        private static bool ShowResult(Wager wager, bool wallHit = false)
        {
            if (AlertManager.Instance == null) return false;
            string message;
            if (wager.State == Decision.Paid)
                message = $"The wall held! {Wager.Payout} coins paid (+{Wager.Payout - Wager.Stake} net).";
            else if (wager.State == Decision.Lost)
                message = wallHit ? $"Wall damaged! Your {Wager.Stake} coin stake is lost." : $"Wager lost. Your {Wager.Stake} coin stake is spent.";
            else return false;
            AlertManager.Instance.DisplayMessage($"Hold the Line / Night {wager.Day}", message);
            return true;
        }

        private void Notify(Action publish)
        {
            try { publish(); }
            catch (Exception error) { Logger.LogWarning("Wager recorded. Notification failed: " + error.Message); }
        }
    }
}
