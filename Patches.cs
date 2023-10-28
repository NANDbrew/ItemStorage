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

        internal static void OverrideContainedPrefab(ShipItemCrate crate, int prefabIndex)
        {
            Traverse.Create(crate).Field<GameObject>("containedPrefab").Value
                = PrefabsDirectory.instance.directory[prefabIndex];
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
            if ((__instance is ShipItemCrate) == false && targetedCrate != null)
            {
                // Verify this instance has a prefabIndex
                // TODO: is this necessary?
                var thisSaveablePrefabComponent = __instance.gameObject.GetComponent<SaveablePrefab>();
                if (thisSaveablePrefabComponent == null)
                    return true;
                var thisPrefabIndex = thisSaveablePrefabComponent.prefabIndex;

                // Small detour here; if the crate is empty, we don't care what item it had before;
                //  so we should skip all of the code that calculates that and just overwrite it.
                if (targetedCrate.amount < 1)   // < 1 since it's a float and we don't care if it's 1e-4
                {
                    // Check if item has a valid crate
                    // TODO: only allow replacement if crates are the same size
                    var existingCratePrefab = References.CratePrefabFromItem(__instance);
                    if (existingCratePrefab != null)
                    {
                        var position = targetedCrate.transform.position;
                        var rotation = targetedCrate.transform.rotation;
                        targetedCrate.DestroyItem();

                        var newCrate = GameObject.Instantiate(existingCratePrefab, position, rotation);
                        newCrate.GetComponent<ShipItem>().sold = true;
                        newCrate.GetComponent<SaveablePrefab>().RegisterToSave();
                        newCrate.GetComponent<Good>().RegisterAsMissionless();
                        newCrate.GetComponent<ShipItemCrate>().amount = 1f;

                        __instance.DestroyItem();
                        return false;
                    }

                    // TODO: allow for storing burnt food?
                    if (__instance.GetComponent<CookableFood>() && __instance.amount >= 1.75f)
                        return true;

                    // Time for some code atrocities, courtesy of reflection!
                    OverrideContainedPrefab(targetedCrate, thisPrefabIndex);

                    // Now set the relevant parameters
                    targetedCrate.gameObject.name = "custom crate";
                    targetedCrate.name = __instance.name;
                    targetedCrate.tag = __instance.tag;
                    targetedCrate.category = __instance.category;
                    targetedCrate.amount = 1f;

                    // Set Goods associated to null.
                    // If this crate had an associated Good, it would be replaced by a prefab above.
                    var crateTraverse = Traverse.Create(targetedCrate);
                    crateTraverse.Field<Good>("good").Value = null;
                    crateTraverse.Field<Good>("goodC").Value = null;
                    GameObject.Destroy(targetedCrate.GetComponent<Good>());

                    // Handle food items
                    var isCooked = __instance.GetComponent<CookableFood>() &&
                        __instance.amount >= 1f && __instance.amount < 1.75f;
                    if (isCooked)
                        targetedCrate.smokedFood = true;

                    RegisterOverride(targetedCrate, thisPrefabIndex, isCooked);

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
