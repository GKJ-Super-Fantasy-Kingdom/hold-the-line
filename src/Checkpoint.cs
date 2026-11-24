using System;
using Newtonsoft.Json;

namespace HoldTheLine
{
    internal static class Checkpoint
    {
        internal sealed class Header
        {
            public string runID { get; set; }
            public string hash { get; set; }
            public Wager gkjHoldTheLine { get; set; }
        }

        internal static Header Read(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonConvert.DeserializeObject<Header>(json); }
            catch (JsonException) { return null; }
        }

        internal static Wager Restore(Header record, string run, string hash)
        {
            var wager = record?.gkjHoldTheLine;
            return wager != null && !string.IsNullOrEmpty(run) && record.runID == run &&
                record.hash == hash && wager.Run == run && wager.Day > 0 &&
                Enum.IsDefined(typeof(Decision), wager.State) ? wager : null;
        }

        internal static string Attach(string json, Wager wager)
        {
            if (wager == null) return json;
            var header = Read(json);
            if (header == null || header.runID != wager.Run || header.gkjHoldTheLine != null) return json;
            int end = json.LastIndexOf('}');
            return json.Insert(end, ",\"gkjHoldTheLine\":" + JsonConvert.SerializeObject(wager));
        }
    }
}
