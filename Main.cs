using HarmonyLib;
using ModUtilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityModManagerNet;

namespace ItemStorage
{
    static class Main
    {
        public static ModUtilities.LogManager logger;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            logger = new LogManager(modEntry.Logger);
            Persistence.Register(modEntry.Info.Id, SaveCrateOverrides, LoadCrateOverrides);

            return true;
        }

        static object SaveCrateOverrides()
        {
            Main.logger.Log($"Saving {ItemPatch.overrides.Count} crate prefab overrides.");
            return ItemPatch.overrides;
        }

        static void LoadCrateOverrides(object savedData)
        {
            ItemPatch.overrides = (Dictionary<int, Tuple<int, bool>>)savedData;
            ItemPatch.ApplyOverrides();
            Main.logger.Log($"Loaded {ItemPatch.overrides.Count} saved crate prefab overrides.");
        }
    }
}