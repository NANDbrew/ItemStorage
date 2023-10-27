using HarmonyLib;
using SailwindModUtilities.Utilities;
using SailwindModUtilities.Utilities.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace ItemStorage
{
    static class Main
    {
        [Serializable]
        struct SaveCrateStorageData
        {
            public SerializableVector3 position;
            public int containedPrefabIndex;
        }

        static UnityModManager.ModEntry.ModLogger logger;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            logger = modEntry.Logger;
            Logging.SetLogger(logger);
            DataManager.RegisterSaveFunction(modEntry.Info.Id, Save);
            DataManager.RegisterLoadFunction(modEntry.Info.Id, Load);

            return true;
        }

        static object Save()
        {
            Logging.Log("Accessing list of prefabs registered to save.");
            var currentPrefabs = GetPrefabsRegisteredForSave();
            Logging.Log("List contains {0} objects:", currentPrefabs.Count);
            foreach (var prefab in currentPrefabs)
                Logging.Log("> {0}, location: ({1})", prefab.name, prefab.transform.position);
            Logging.Log("Storing list of locations to file.");
            var cachedLocations = currentPrefabs.Select(prefab => (SerializableVector3)prefab.transform.position).ToList();
            return cachedLocations;
        }

        static void Load(object savedData)
        {
            var cachedLocations = (List<SerializableVector3>)savedData;
            Logging.Log("Received saved list of locations.");
            Logging.Log("List contains {0} objects:", cachedLocations.Count);
            foreach (var location in cachedLocations)
                Logging.Log("> ({0})", (Vector3)location);
            // This list should contain everything currently loaded, since part of the loading process is re-registering with the SaveLoadManager.
            Logging.Log("Retrieving list of prefabs registered to save.");
            var currentPrefabs = GetPrefabsRegisteredForSave();
            Logging.Log("List contains {0} objects:", currentPrefabs.Count);
            foreach (var prefab in currentPrefabs)
                Logging.Log("> {0}, location: ({1})", prefab.name, prefab.transform.position);
            if (cachedLocations.Count != currentPrefabs.Count)
                Logging.Log("CRITICAL ERROR: List sizes do not match!");
            else
            {
                Logging.Log("Comparing locations:");
                for (int index = 0; index < cachedLocations.Count; index++)
                {
                    var matching = SamePosition((Vector3)cachedLocations[index], currentPrefabs[index].transform.position);
                    Logging.Log("> {0}", matching ? "MATCHING" : "NOT MATCHING");
                }
            }
            Logging.Log("Locations compared, loading finished.");
        }

        // TODO: replace with a reversepatch
        public static void OverrideContainedPrefab(ShipItemCrate crate, int prefabIndex)
        {
            var newPrefab = PrefabsDirectory.instance.directory[prefabIndex];
            Traverse.Create(crate).Field<GameObject>("containedPrefab").Value = newPrefab;
            crate.name = newPrefab.GetComponent<ShipItem>().name;
        }

        public static List<SaveablePrefab> GetPrefabsRegisteredForSave()
        {
            return Traverse.Create(SaveLoadManager.instance).Field<List<SaveablePrefab>>("currentPrefabs").Value;
        }

        static bool SamePosition(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 1e-4f;  // value found experimentally, may need tweaking
        }
    }

    // TODO: split patches into separate file?
    [HarmonyPatch(typeof(ShipItem))]
    static class ItemPatch
    {
        static ShipItemCrate targetedCrate;

        [HarmonyPatch("Update"), HarmonyPostfix]
        static void UpdatePostfix(ref ShipItem __instance, ref GoPointer ___pointedAtBy)
        {
            var instanceAsCrate = __instance as ShipItemCrate;

            if (instanceAsCrate != null && targetedCrate == null && ___pointedAtBy != null)
                targetedCrate = __instance as ShipItemCrate;

            if (___pointedAtBy == null && targetedCrate == __instance)
                targetedCrate = null;
        }

        [HarmonyPatch("OnAltActivate"), HarmonyPrefix]
        static bool OnAltActivatePrefix(ref ShipItem __instance)
        {
            // Crates are not a valid item to store in crates
            // Only proceed if this instance is something else
            if ((__instance is ShipItemCrate) == false)
            {
                // Verify this instance has a prefabIndex
                var thisSaveablePrefabComponent = __instance.gameObject.GetComponent<SaveablePrefab>();
                if (thisSaveablePrefabComponent == null)
                    return true;
                var thisPrefabIndex = thisSaveablePrefabComponent.prefabIndex;

                // Small detour here; if the crate is empty, we don't care what item it had before;
                //  so we should skip all of the code that calculates that and just overwrite it.
                if (targetedCrate.amount < 1)   // < 1 since it's a float and we don't care if it's 1e-4
                {
                    // TODO: update various properties of crate: item category, mass, goodC, etc.
                    // TODO: experimentally verify crates of the same size are the same weight
                    // TO TEST: if it's another good, just grab that good's crate and trade them out?

                    // Time for some code atrocities, courtesy of reflection!
                    Main.OverrideContainedPrefab(targetedCrate, thisPrefabIndex);
                    targetedCrate.amount = 1f;  // why the hell is this a float, RL?
                                                // is it cause of liquids?

                    __instance.DestroyItem();

                    return false;
                }

                // Check for the prefab index of the crate's stored item
                // I think this HAS to exist, but just in case we'll do a null check
                // We can at least assume targetedCrate has a containedPrefab, since it must by definition
                var crateItemSaveablePrefabComponent = targetedCrate.GetContainedPrefab().
                    GetComponent<SaveablePrefab>();
                if (crateItemSaveablePrefabComponent == null)
                    return true;
                var crateItemPrefabIndex = crateItemSaveablePrefabComponent.prefabIndex;

                // The moment of truth: do they match?
                if (thisPrefabIndex != crateItemPrefabIndex)
                    return true;

                targetedCrate.amount += 1;
                __instance.DestroyItem();

                return false;
            }
            else return true;
        }
    }

    [HarmonyPatch(typeof(ShipItemCrate))]
    class ItemCratePatch
    {
        [HarmonyPatch("UpdateLookText"), HarmonyPostfix]
        static void UpdateLookTextPostfix(ref ShipItemCrate __instance)
        {
            if (__instance.amount < 1f)
                __instance.lookText = "empty crate";
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager))]
    class SailwindSavePatches
    {
        [HarmonyPatch("SaveModData"), HarmonyPostfix]
        static void SaveModDataPostfix()
        {
            Logging.Log("Beginning save procedure...");
            DataManager.Save();
            Logging.Log("Saving finished.");
        }

        [HarmonyPatch("LoadModData"), HarmonyPostfix]
        static void LoadModDataPostfix()
        {
            Logging.Log("Beginning load procedure...");
            DataManager.Load();
            Logging.Log("Loading finished.");
        }
    }
}