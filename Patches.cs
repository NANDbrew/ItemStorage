﻿using HarmonyLib;
using UnityEngine;

namespace ItemStorage
{
    [HarmonyPatch(typeof(ShipItem))]
    static class ItemPatch
    {
        static ShipItemCrate targetedCrate;

        // TODO: replace with a reversepatch
        static void OverrideContainedPrefab(ShipItemCrate crate, int prefabIndex)
        {
            var newPrefab = PrefabsDirectory.instance.directory[prefabIndex];
            Traverse.Create(crate).Field<GameObject>("containedPrefab").Value = newPrefab;
            crate.name = newPrefab.GetComponent<ShipItem>().name;
        }

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
                    OverrideContainedPrefab(targetedCrate, thisPrefabIndex);
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
}