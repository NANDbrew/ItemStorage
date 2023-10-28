using HarmonyLib;
using ModUtilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemStorage
{
    [HarmonyPatch(typeof(ShipItem))]
    static class ItemPatch
    {
        internal static Dictionary<int, Tuple<int, bool>> overrides = new Dictionary<int, Tuple<int, bool>>();
        static ShipItemCrate targetedCrate;

        internal static void OverrideContainedPrefab(ShipItemCrate crate, int prefabIndex, bool isCooked)
        {
            var newPrefab = PrefabsDirectory.instance.directory[prefabIndex];

            Main.logger.Log($"Overriding prefab in crate \"{crate.name}\" with prefab \"{newPrefab.name}\"");

            // TODO: if new prefab is contained in a pre-existing crate prefab, just replace values
            //  with those of the crate prefab

            Traverse.Create(crate).Field<GameObject>("containedPrefab").Value = newPrefab;
            var newPrefabItem = newPrefab.GetComponent<ShipItem>();
            crate.name = newPrefabItem.name;    // TODO: set name of gameObject as well
            crate.tag = newPrefabItem.tag;
            crate.category = newPrefabItem.category;
            var newGood = Traverse.Create(newPrefabItem).Field<Good>("good").Value;
            var crateTraverse = Traverse.Create(crate);
            crateTraverse.Field<Good>("good").Value = newGood;
            crateTraverse.Field<Good>("goodC").Value = newGood; // TODO: Verify that this is actually what goodC is supposed to be

            if (isCooked)
                targetedCrate.smokedFood = true;

            RegisterOverride(crate, prefabIndex, isCooked);
        }

        static void RegisterOverride(ShipItemCrate crate, int newPrefabIndex, bool isCooked)
        {
            var crateGUID = crate.GetComponent<GUID>().ID;
            overrides[crateGUID] = new Tuple<int, bool>(newPrefabIndex, isCooked);
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
                    // TODO: allow for storing burnt food?
                    if (__instance.GetComponent<CookableFood>() && __instance.amount >= 1.75f)
                        return true;

                    // Time for some code atrocities, courtesy of reflection!
                    var cooked = __instance.GetComponent<CookableFood>() &&
                        __instance.amount >= 1f && __instance.amount < 1.75f;
                    OverrideContainedPrefab(targetedCrate, thisPrefabIndex, cooked);
                    targetedCrate.amount = 1f;

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
                targetedCrate.itemRigidbodyC.UpdateMass();
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
