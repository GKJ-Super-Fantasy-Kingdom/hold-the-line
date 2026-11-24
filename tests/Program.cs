using System;
using System.IO;
using System.Linq;
using HoldTheLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
        Console.WriteLine("PASS " + name);
    }

    private static void Main(string[] args)
    {
        var baseline = args.Length == 0
            ? new JObject { ["runID"] = "fixture", ["day"] = 12, ["coins"] = 6, ["hash"] = "fixture-hash" }
            : JObject.Parse(File.ReadAllText(args[0]));
        var run = (string)baseline["runID"];
        int day = (int)baseline["day"] + 1;
        int start = (int)baseline["coins"];
        int coins = start;
        void Change(int n) => coins += n;
        Wager Offer() => new Wager { Run = run, Day = day };
        string Save(Wager wager, bool tavern = false)
        {
            var save = (JObject)baseline.DeepClone();
            save["coins"] = coins;
            save["isNight"] = !tavern;
            save["day"] = tavern ? day : day - 1;
            return Checkpoint.Attach(save.ToString(Formatting.None), wager);
        }
        Wager Load(string raw)
        {
            var save = JObject.Parse(raw);
            coins = (int)save["coins"];
            return Checkpoint.Restore(Checkpoint.Read(raw), (string)save["runID"], (string)save["hash"]);
        }

        Check(Wager.CanOffer(null, run, day, true, true), "first eligible offer");
        Check(!Wager.CanOffer(null, run, day, false, true), "no wall");
        Check(!Wager.CanOffer(null, run, day, true, false), "too late to enter");
        var wager = Offer();
        var pending = Load(Save(wager));
        Check(Wager.CanOffer(pending, run, day, true, true) && coins == start,
            "unanswered checkpoint reopens the free decision");
        Check(!Wager.CanOffer(pending, run, day, true, false), "unanswered offer expires at battle");
        Check(!wager.AcceptCoin(1, 0, Change) && coins == start, "insufficient funds");
        wager.Pass();
        var declined = Load(Save(wager));
        Check(!declined.AcceptCoin(1, coins, Change) && !declined.Finish(true, Change) && coins == start, "pass stays free on reload");
        Check(!Wager.CanOffer(declined, run, day, true, true), "one offer per night");
        Check(Wager.CanOffer(declined, run, day + 1, true, true), "following night offers again");

        wager = Offer();
        Check(wager.AcceptCoin(1, coins, Change) && coins == start - 1, "stake charged once");
        string staked = Save(wager);
        wager = Load(staked);
        Check(!wager.AcceptCoin(1, coins, Change) && coins == start - 1, "no second charge on resume");
        Check(!Wager.CanOffer(wager, run, day, true, true), "accepted checkpoint cannot reopen the offer");
        wager.Hit(30, 30);
        Check(wager.State == Decision.Staked, "shield or zero-damage hit");
        wager.Hit(30, 24);
        wager.Hit(24, 30);
        wager = Load(Save(wager));
        Check(!wager.Finish(true, Change) && coins == start - 1 && wager.State == Decision.Lost,
            "hit followed by full repair remains lost after reload");
        wager = Load(staked);
        wager.Hit(30, 0);
        Check(!wager.Finish(true, Change) && coins == start - 1, "wall destroyed");
        wager = Load(staked);
        Check(!wager.Finish(false, Change) && !wager.Finish(true, Change), "defeat or incomplete night cannot pay");

        wager = Load(staked);
        Check(wager.Finish(true, Change) && coins == start + 1, "gross payout and net wallet");
        Check(!wager.Finish(true, Change) && coins == start + 1, "duplicate end callback");
        string paid = Save(wager);
        for (int i = 0; i < 3; i++)
        {
            wager = Load(paid);
            Check(!wager.Finish(true, Change) && coins == start + 1, "paid checkpoint reload " + (i + 1));
        }
        Change(-1); // A purchase in the tavern changes the wallet after payout.
        string morning = Save(wager, tavern: true);
        wager = Load(morning);
        Check(!wager.Finish(true, Change) && coins == start, "tavern round-trip with unrelated spending");
        Check(Checkpoint.Attach(morning, wager) == morning, "idempotent checkpoint decoration");
        wager = Load(staked); // A legitimate rollback restores the wallet and wager together.
        Check(wager.Finish(true, Change) && coins == start + 1, "backup replay reaches same wallet");
        Check(Checkpoint.Restore(Checkpoint.Read(paid), "other-run", (string)baseline["hash"]) == null, "run mismatch");
        Check(Checkpoint.Restore(Checkpoint.Read(paid), run, "other-hash") == null, "checkpoint mismatch");
        Check(Load(baseline.ToString()) == null && coins == start, "legacy save has no wager");
        Check(Checkpoint.Read("{") == null, "malformed primary leaves no wager for backup load");
        Check(Checkpoint.Attach(baseline.ToString(), new Wager { Run = "other-run" }) == baseline.ToString(),
            "unrelated checkpoint unchanged");
        var expected = (JObject)baseline.DeepClone();
        expected["coins"] = start + 1;
        expected["isNight"] = true;
        var actual = JObject.Parse(paid);
        actual.Remove("gkjHoldTheLine");
        Check(JToken.DeepEquals(actual, expected), "all native checkpoint fields preserved");
        foreach (var sample in new[] { (1, 1, 2), (2, 3, 5), (3, 6, 9), (4, 10, 14), (10, 55, 65) })
            Check(CoinTerms.Quote(sample.Item1, out int stake, out int total) &&
                stake == sample.Item2 && total == sample.Item3, "triangular stake for profit " + sample.Item1);
        Check(!CoinTerms.Quote(0, out _, out _) && !CoinTerms.Quote(int.MaxValue, out _, out _), "reject invalid or overflowing terms");
        coins = 6;
        wager = Offer();
        Check(!wager.AcceptCoin(4, coins, Change) && coins == 6 && wager.AcceptCoin(3, coins, Change) && coins == 0,
            "charge six coins for three net only when affordable");
        wager = Load(Save(wager));
        Check(wager.Payout == 9 && wager.Finish(true, Change) && coins == 9 && !wager.Finish(true, Change),
            "nine-coin return once after Continue");
        var oldSave = (JObject)baseline.DeepClone();
        oldSave["gkjHoldTheLine"] = new JObject { ["Run"] = run, ["Day"] = day, ["State"] = 2 };
        wager = Load(oldSave.ToString());
        Check(wager.Stake == 1 && wager.Payout == 3 && wager.Finish(true, Change) && coins == start + 3,
            "already accepted 0.1.5 receipt keeps exact original gross payout");

        var ids = new[] { "Dice", "Fangs", "Lute" };
        var pool = new[] { "Dice", "Fangs", "Lute", "WeakSpot", "Hourglass", "Fangs" };
        var draw = RelicPrize.Draw(run, day, pool);
        Check(RelicPrize.Valid(draw) && draw.SequenceEqual(RelicPrize.Draw(run, day, pool.Reverse())),
            "reordered catalog and duplicate IDs reproduce the exact three choices");
        Check(!RelicPrize.Valid(RelicPrize.Draw(run, day, new[] { "Dice", "Dice", "Lute" })),
            "a two-relic pool cannot enable a twenty-coin charge");
        coins = 19;
        wager = Offer();
        Check(!wager.AcceptRelic(ids, coins, Change) && coins == 19, "nineteen coins cannot buy the relic wager");
        coins++;
        foreach (var invalid in new[] { new[] { "a", "b" }, new[] { "a", "b", "a" }, new[] { "a", "b", "" } })
            Check(!wager.AcceptRelic(invalid, coins, Change) && coins == 20, "bad draw leaves wallet and decision untouched");
        Check(wager.AcceptRelic(ids, coins, Change) && coins == 0 && !wager.AcceptCoin(1, 10, Change), "one twenty-coin charge excludes other wager");
        ids[0] = "replacement";
        string relicCheckpoint = Save(wager);
        wager = Load(relicCheckpoint);
        Check(wager.Choices[0] == "Dice" && !wager.AcceptRelic(ids, 20, Change), "accepted draw survives Continue without reroll or reentry");
        wager.Hit(30, 29);
        wager.Hit(29, 30);
        wager = Load(Save(wager));
        Check(!wager.Finish(true, Change) && wager.Unclaimed.Count == 0 && coins == 0, "repaired relic night loses stake and awards nothing");
        wager = Load(relicCheckpoint);
        Check(wager.Finish(true, Change) && !wager.Finish(true, Change) && coins == 0 && wager.Unclaimed.Count == 1,
            "flawless wall creates a single relic entitlement with no coin payout");
        string prizeCheckpoint = Save(wager);
        Check(Wager.Open(Load(prizeCheckpoint), "different", day + 1).Unclaimed.Count == 0,
            "new run never inherits another run's pending reward");
        wager = Wager.Open(Load(prizeCheckpoint), run, day + 1);
        wager.Pass();
        wager = Load(Save(wager, true));
        var prize = wager.Unclaimed[0];
        Check(prize.Night == day && string.Join(",", prize.Choices) == "Dice,Fangs,Lute", "Claim Later preserves all three across next offer and Continue");
        Check(!wager.Claim(prize, "outside", _ => true) && !wager.Claim(prize, "Dice", _ => false) && wager.Unclaimed.Count == 1,
            "wrong id or failed grant preserves the pending claim");
        int grants = 0;
        Check(wager.Claim(prize, "Fangs", id => { grants++; return !wager.Claim(prize, id, _ => true); }) &&
            !wager.Claim(prize, "Lute", _ => true) && grants == 1, "one selection excludes reentrant and alternate rewards");
        Check(Load(Save(wager)).Unclaimed.Count == 0, "settled relic claim stays consumed after Continue");

        Console.WriteLine($"{checks} checks passed.");
    }
}
