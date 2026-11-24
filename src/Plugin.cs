using System;
using BepInEx;
using HarmonyLib;
using SuperFantasyKingdom;
using UnityEngine;

namespace HoldTheLine
{
    [BepInPlugin("gkj.holdtheline", "Hold the Line", "0.2.3")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Wager Current;
        private Harmony patches;
        private OfferCard card;
        private float readySince = -1;
        private Wager announced;
        private int claimPromptDay = -1;
        private RelicPrize openPrize;
        private Action restorePicker;
        private bool finishingRun, resumeVictory, claimedAtVictory;
        private static Plugin instance;
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
            instance = this;
            GameManager.OnGameInit += Reset;
            GameManager.BeforeStateChange += Transition;
            OverlayManager.OnOverlayClose += Closed;
            Logger.LogInfo("Hold the Line ready.");
        }

        private void OnDestroy()
        {
            instance = null;
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
            claimPromptDay = -1;
            restorePicker?.Invoke();
            restorePicker = null;
            openPrize = null;
            finishingRun = resumeVictory = claimedAtVictory = false;
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
            if (resumeVictory)
            {
                resumeVictory = false;
                if (claimedAtVictory && Current != null && Current.Unclaimed.Count > 0)
                {
                    claimedAtVictory = false;
                    OpenClaim(Current, Current.Unclaimed[0]);
                    return;
                }
                OverlayManager.Instance.OpenGameOver(false);
                finishingRun = false;
                return;
            }
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
            if (worldReady && Current?.Run == run && Current.Unclaimed.Count > 0 &&
                claimPromptDay != day && BeforeBattle(clock) && Time.unscaledTime >= nextOfferAt)
            {
                // Opening is free. Closing or Back defers to the next morning or Continue.
                claimPromptDay = day;
                var prize = Current.Unclaimed.Find(p => Array.Exists(p.Choices, id =>
                    !RelicManager.Instance.HasRelic(id) && RelicManager.Instance.CanAddRelic(id)));
                if (prize != null)
                {
                    OpenClaim(Current, prize);
                    return;
                }
            }
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
            var wager = Current = Wager.Open(Current, run, day);
            var choices = RelicRewards.Draw(wager);
            card = new OfferCard(day, choices.Length == 3, profit => Accept(wager, profit),
                () => Accept(wager, 0), () => Close(wager));
        }

        private void OpenClaim(Wager wager, RelicPrize prize)
        {
            openPrize = prize;
            restorePicker = RelicRewards.Show(prize, finishingRun, id =>
            {
                if (Current != wager || openPrize != prize) return;
                var relics = RelicManager.Instance;
                if (relics.HasRelic(id) || !relics.CanAddRelic(id)) return;
                if (wager.Claim(prize, id, name =>
                {
                    try { return relics.AddRelic(name) != null; }
                    catch (Exception error)
                    {
                        Logger.LogWarning("Native relic grant interrupted: " + error.Message);
                        return relics.HasRelic(name);
                    }
                }))
                {
                    claimedAtVictory = finishingRun;
                    OverlayManager.Instance.CloseOverlay();
                    Notify(() => AlertManager.Instance.DisplayMessage("Hold the Line", "Relic claimed: " +
                        AddressablesManager.Instance.GetRelic(id).GetTitle()));
                }
            });
        }

        private void Accept(Wager wager, int profit)
        {
            if (card == null || Current != wager) return;
            var game = GameManager.Instance;
            var clock = DaytimeManager.Instance;
            var purse = ResourceManager.Instance;
            if (game == null || game.IsUnloading() || !game.IsGameRunning() || game.IsGameOver() ||
                !wager.Matches(GameDataManager.Instance.GetRunID(), clock.CurrentDay()) || !HasWall() || !BeforeBattle(clock))
            {
                Close(wager);
                return;
            }
            bool charged;
            if (profit == 0)
            {
                var choices = RelicRewards.Draw(wager);
                card.RefreshRelicOffer(choices.Length == 3);
                charged = wager.AcceptRelic(choices, purse.GetCoins(), purse.AddToCoins);
            }
            else charged = wager.AcceptCoin(profit, purse.GetCoins(), purse.AddToCoins);
            if (!charged) return;
            Notify(() => purse.ResourceAmountChanged(ResourceType.Coins));
            Logger.LogInfo($"Night {wager.Day}: {wager.Stake} coins staked on {wager.Kind}.");
            Close(wager);
        }

        private void Close(Wager wager)
        {
            if (card != null && Current == wager) OverlayManager.Instance.CloseOverlay();
        }

        private void Closed()
        {
            if (openPrize != null)
            {
                restorePicker?.Invoke();
                restorePicker = null;
                openPrize = null;
                if (finishingRun) resumeVictory = true;
                nextOfferAt = Time.unscaledTime + 1f;
            }
            if (card == null) return;
            Current?.Pass();
            card = null;
        }

        // There is no following morning on the final night. Present the earned
        // cards before the victory screen can discard the run.
        [HarmonyPatch(typeof(OverlayManager), nameof(OverlayManager.OpenGameOver))]
        private static class VictoryReward
        {
            private static bool Prefix(bool __0)
            {
                if (__0 || instance == null || instance.finishingRun || Current == null || Current.Unclaimed.Count == 0 || Current.Run != GameDataManager.Instance.GetRunID() ||
                    GameManager.Instance.GetGameState() != GameState.Win) return true;
                instance.finishingRun = true;
                instance.OpenClaim(Current, Current.Unclaimed[0]);
                return false;
            }
        }

        private void Transition(GameState state)
        {
            bool survived = state == GameState.Survived || state == GameState.Win;
            if (!survived && state != GameState.Lose && state != GameState.GiveUp) return;
            if (Current == null || !Current.Matches(GameDataManager.Instance.GetRunID(),
                DaytimeManager.Instance.CurrentDay())) return;
            var resources = ResourceManager.Instance;
            bool completed = survived && (CombatManager.Instance.IsCombatOver() || DaytimeManager.Instance.IsNight());
            if (Current.Finish(completed, resources.AddToCoins) && Current.State == Decision.Paid)
            {
                Notify(() => StatisticsManager.Instance.AddCoins(Current.Payout));
                Notify(() => resources.ResourceAmountChanged(ResourceType.Coins));
                Logger.LogInfo($"Night {Current.Day}: paid {Current.Payout} coins.");
            }
            if ((state == GameState.Win || state == GameState.Lose) && announced != Current)
                Notify(() => { if (ShowResult(Current)) announced = Current; });
        }

        private static bool ShowResult(Wager wager, bool wallHit = false)
        {
            if (AlertManager.Instance == null) return false;
            string message;
            if (wager.State == Decision.Paid)
                message = $"The wall held! {wager.Payout} coins paid (+{wager.Payout - wager.Stake} net).";
            else if (wager.State == Decision.Lost)
                message = wallHit ? $"Wall damaged! Your {wager.Stake} coin stake is lost." : $"Wager lost. Your {wager.Stake} coin stake is spent.";
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
