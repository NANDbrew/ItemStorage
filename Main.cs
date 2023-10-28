using HarmonyLib;
using ModUtilities;
using System.Collections.Generic;
using System.Reflection;
using UnityModManagerNet;

namespace ItemStorage
{
    static class Main
    {
        internal static UnityModManager.ModEntry.ModLogger logger;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            logger = modEntry.Logger;
            Persistence.Register(SaveCrateOverrides, LoadCrateOverrides);

            return true;
        }

        static object SaveCrateOverrides()
        {
            Main.logger.Log($"Saving {ItemPatch.overrides.Count} crate prefab overrides.");
            return ItemPatch.overrides;
        }

        static void LoadCrateOverrides(object savedData)
        {
            var overrides = (Dictionary<int, int>)savedData;
            foreach(var overrideEntry in overrides)
            {
                var crate = GUID.FindObjectByID(overrideEntry.Key).GetComponent<ShipItemCrate>();
                ItemPatch.OverrideContainedPrefab(crate, overrideEntry.Value);
                // The below is not necessary since OverrideContainedPrefab already registers the crate
                //ItemPatch.overrides[overrideEntry.Key] = overrideEntry.Value;
            }

            
            Main.logger.Log($"Loaded {ItemPatch.overrides.Count} saved crate prefab overrides.");
        }
    }
}