using System;

namespace HoldTheLine
{
    internal enum Decision { Offered, Passed, Staked, Lost, Paid }

    internal sealed class Wager
    {
        internal const int Stake = 1;
        internal const int Payout = 3;
        public string Run { get; set; }
        public int Day { get; set; }
        public Decision State { get; set; }

        internal bool Matches(string run, int day) => Run == run && Day == day;

        internal static bool CanOffer(Wager previous, string run, int day, bool hasWall, bool beforeBattle) =>
            !string.IsNullOrEmpty(run) && day > 0 && hasWall && beforeBattle &&
            previous?.Matches(run, day) != true;

        // AddToCoins changes a single int without notifying observers. Publish UI/stat
        // events only after both the wallet and this receipt have been updated.
        internal bool Accept(int coins, Action<int> changeCoins)
        {
            if (State != Decision.Offered || coins < Stake) return false;
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
            changeCoins(Payout);
            State = Decision.Paid;
            return true;
        }
    }
}
