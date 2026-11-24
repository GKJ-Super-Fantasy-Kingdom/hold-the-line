using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SuperFantasyKingdom;

namespace HoldTheLine
{
    [HarmonyPatch(typeof(WallManager), nameof(WallManager.WallGetsHit))]
    internal static class WallHit
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var hp = typeof(WallManager).GetField("m_Hitpoints", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(WallManager).FullName, "m_Hitpoints");
            var stores = code.FindAll(i => i.StoresField(hp));
            if (stores.Count != 1)
                throw new InvalidOperationException("Expected one HP write in WallGetsHit. Wagers disabled.");
            // Observe the clamped result before native damage listeners can repair it.
            // The helper returns the exact original float to the original field store.
            var store = stores[0];
            var loadWall = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(store);
            code.InsertRange(code.IndexOf(store), new[] { loadWall,
                new CodeInstruction(OpCodes.Call, ((Func<float, WallManager, float>)Observe).Method) });
            return code;
        }

        private static float Observe(float remaining, WallManager wall)
        {
            Plugin.Current?.Hit(wall.GetWallHitpoints(), remaining);
            return remaining;
        }
    }
}
