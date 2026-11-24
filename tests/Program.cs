using System;
using System.IO;
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
        Check(!wager.Accept(0, Change) && coins == start, "insufficient funds");
        wager.Pass();
        var declined = Load(Save(wager));
        Check(!declined.Accept(coins, Change) && !declined.Finish(true, Change) && coins == start, "pass stays free on reload");
        Check(!Wager.CanOffer(declined, run, day, true, true), "one offer per night");
        Check(Wager.CanOffer(declined, run, day + 1, true, true), "following night offers again");

        wager = Offer();
        Check(wager.Accept(coins, Change) && coins == start - 1, "stake charged once");
        string staked = Save(wager);
        wager = Load(staked);
        Check(!wager.Accept(coins, Change) && coins == start - 1, "no second charge on resume");
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
        Check(wager.Finish(true, Change) && coins == start + 2, "gross payout and net wallet");
        Check(!wager.Finish(true, Change) && coins == start + 2, "duplicate end callback");
        string paid = Save(wager);
        for (int i = 0; i < 3; i++)
        {
            wager = Load(paid);
            Check(!wager.Finish(true, Change) && coins == start + 2, "paid checkpoint reload " + (i + 1));
        }
        Change(-1); // A purchase in the tavern changes the wallet after payout.
        string morning = Save(wager, tavern: true);
        wager = Load(morning);
        Check(!wager.Finish(true, Change) && coins == start + 1, "tavern round-trip with unrelated spending");
        Check(Checkpoint.Attach(morning, wager) == morning, "idempotent checkpoint decoration");
        wager = Load(staked); // A legitimate rollback restores the wallet and wager together.
        Check(wager.Finish(true, Change) && coins == start + 2, "backup replay reaches same wallet");
        Check(Checkpoint.Restore(Checkpoint.Read(paid), "other-run", (string)baseline["hash"]) == null, "run mismatch");
        Check(Checkpoint.Restore(Checkpoint.Read(paid), run, "other-hash") == null, "checkpoint mismatch");
        Check(Load(baseline.ToString()) == null && coins == start, "legacy save has no wager");
        Check(Checkpoint.Read("{") == null, "malformed primary leaves no wager for backup load");
        Check(Checkpoint.Attach(baseline.ToString(), new Wager { Run = "other-run" }) == baseline.ToString(),
            "unrelated checkpoint unchanged");
        var expected = (JObject)baseline.DeepClone();
        expected["coins"] = start + 2;
        expected["isNight"] = true;
        var actual = JObject.Parse(paid);
        actual.Remove("gkjHoldTheLine");
        Check(JToken.DeepEquals(actual, expected), "all native checkpoint fields preserved");
        Console.WriteLine($"{checks} checks passed.");
    }
}
