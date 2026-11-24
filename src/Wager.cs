using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HoldTheLine
{
    // The first five values are serialized in released saves.
    internal enum Decision { Offered, Passed, Staked, Lost, Paid, RelicWon }
    internal enum WagerKind { Coin, Relic }

    internal static class CoinTerms
    {
        internal static bool Quote(int profit, out int stake, out int total)
        {
            stake = total = 0;
            if (profit < 1) return false;
            long cost = (long)profit * (profit + 1L) / 2;
            if (cost + profit > int.MaxValue) return false;
            stake = (int)cost;
            total = stake + profit;
            return true;
        }
    }

    internal sealed class RelicPrize
    {
        public int Night { get; set; }
        public string[] Choices { get; set; }
        internal static string[] Draw(string run, int day, IEnumerable<string> eligible)
        {
            // Rank independently of dictionary iteration order and gameplay RNG. The
            // same run/night and eligible pool reproduce the same draw on rollback.
            using (var hash = SHA256.Create())
                return eligible.Distinct(StringComparer.Ordinal).OrderBy(id => BitConverter.ToString(hash.ComputeHash(
                    Encoding.UTF8.GetBytes(run + "/" + day.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" + id))),
                    StringComparer.Ordinal).Take(3).ToArray();
        }

        internal static bool Valid(string[] choices) => choices?.Length == 3 &&
            choices.All(id => !string.IsNullOrWhiteSpace(id)) && choices.Distinct().Count() == 3;
    }

    internal sealed class Wager
    {
        internal const int RelicCost = 20;
        public string Run { get; set; }
        public int Day { get; set; }
        public Decision State { get; set; }
        public WagerKind Kind { get; set; }
        // Missing terms identify a pre-0.2.0 receipt.
        public int Stake { get; set; } = 1;
        public int Payout { get; set; } = 3;
        public string[] Choices { get; set; }
        public List<RelicPrize> Unclaimed { get; set; } = new List<RelicPrize>();

        internal bool Matches(string run, int day) => Run == run && Day == day;
        internal static bool CanOffer(Wager previous, string run, int day, bool hasWall, bool beforeBattle) =>
            !string.IsNullOrEmpty(run) && day > 0 && hasWall && beforeBattle &&
            (previous?.Matches(run, day) != true || previous.State == Decision.Offered);

        internal static Wager Open(Wager previous, string run, int day)
        {
            if (previous?.Matches(run, day) == true) return previous;
            return new Wager { Run = run, Day = day,
                Unclaimed = previous?.Run == run ? previous.Unclaimed : new List<RelicPrize>() };
        }

        internal bool AcceptCoin(int profit, int coins, Action<int> changeCoins)
        {
            if (!CoinTerms.Quote(profit, out int stake, out int total) || coins < stake ||
                (long)coins + profit > int.MaxValue || State != Decision.Offered) return false;
            Stake = stake;
            Payout = total;
            Kind = WagerKind.Coin;
            return Charge(changeCoins);
        }

        internal bool AcceptRelic(string[] choices, int coins, Action<int> changeCoins)
        {
            if (State != Decision.Offered || coins < RelicCost || !RelicPrize.Valid(choices)) return false;
            Stake = RelicCost;
            Payout = 0;
            Kind = WagerKind.Relic;
            Choices = (string[])choices.Clone();
            return Charge(changeCoins);
        }

        private bool Charge(Action<int> changeCoins)
        {
            changeCoins(-Stake);
            State = Decision.Staked;
            return true;
        }

        internal void Pass()
        {
            if (State == Decision.Offered) State = Decision.Passed;
        }

        internal void Hit(float previous, float remaining)
        {
            if (State == Decision.Staked && remaining < previous) State = Decision.Lost;
        }

        internal bool Finish(bool survived, Action<int> changeCoins)
        {
            if (State != Decision.Staked) return false;
            if (!survived) { State = Decision.Lost; return false; }
            if (Kind == WagerKind.Relic)
            {
                Unclaimed.Add(new RelicPrize { Night = Day, Choices = Choices });
                State = Decision.RelicWon;
            }
            else
            {
                changeCoins(Payout);
                State = Decision.Paid;
            }
            return true;
        }

        internal bool Claim(RelicPrize prize, string id, Func<string, bool> grant)
        {
            if (prize == null || !prize.Choices.Contains(id) || !Unclaimed.Remove(prize)) return false;
            // Native grant callbacks can save or re-enter. Remove the entitlement first.
            if (grant(id)) return true;
            Unclaimed.Insert(0, prize);
            return false;
        }
    }
}
