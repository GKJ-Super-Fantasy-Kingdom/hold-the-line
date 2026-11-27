using System;
using System.Reflection;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Tavern;

namespace HoldTheLine
{
    internal static class SaveHooks
    {
        private static bool reading;
        private static Checkpoint.Header candidate;
        private static void Begin()
        {
            reading = true;
            candidate = null;
            Plugin.Current = null;
        }

        private static void End()
        {
            reading = false;
            candidate = null;
        }

        private static void Restore(GameData data)
        {
            Plugin.Current = data == null ? null : Checkpoint.Restore(candidate, data.runID, data.hash);
        }

        [HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.PositionWorldBuildings))]
        internal static class WorldRead
        {
            private static void Prefix() => Begin();
            private static void Postfix(GameData ___m_GameData) => Restore(___m_GameData);
            private static void Finalizer() => End();
        }

        [HarmonyPatch(typeof(TavernSaveManager), "Load")]
        internal static class TavernRead
        {
            private static void Prefix() => Begin();
            private static void Finalizer() => End();
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadGameData))]
        internal static class TavernData
        {
            private static void Postfix(GameData __result)
            {
                if (reading) Restore(__result);
            }
        }

        [HarmonyPatch]
        internal static class RawRead
        {
            private static MethodBase TargetMethod() => AccessTools.FirstMethod(typeof(SaveManager),
                m => m.Name == "Load" && !m.IsGenericMethod && m.ReturnType == typeof(string));
            private static void Postfix(string __0, string __result)
            {
                if (reading && IsGame(__0)) candidate = Checkpoint.Read(__result);
            }
        }

        private static bool IsGame(string path) => path?.EndsWith("_game", StringComparison.Ordinal) == true;

        [HarmonyPatch]
        internal static class Write
        {
            private static MethodBase TargetMethod()
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                var type = typeof(SaveManager);
                var name = nameof(SaveManager.Save);
                // 0.8.070 added doBackUp. Both values write the primary checkpoint.
                var save = type.GetMethod(name, flags, null, new[] { typeof(string), typeof(string), typeof(bool) }, null)
                    ?? type.GetMethod(name, flags, null, new[] { typeof(string), typeof(string) }, null);
                if (save == null || save.ReturnType != typeof(void))
                    throw new MissingMethodException(type.FullName, name);
                return save;
            }

            private static void Prefix(string __0, ref string __1)
            {
                if (IsGame(__0)) __1 = Checkpoint.Attach(__1, Plugin.Current);
            }
        }
    }
}
