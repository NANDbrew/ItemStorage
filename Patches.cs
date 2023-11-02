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

        static void RegisterOverride(ShipItemCrate crate, int newPrefabIndex, bool isCooked)
        {
            var crateID = crate.GetComponent<GUID>().ID;
            overrides[crateID] = new Tuple<int, bool>(newPrefabIndex, isCooked);
        }

        internal static void ApplyOverrides()
        {
            foreach (var crateOverride in overrides)
            {
                var crateID = crateOverride.Key;
                var crateObject = GUID.FindObjectByID(crateID);
                var crate = crateObject.GetComponent<ShipItemCrate>();

                var newItemPrefabIndex = crateOverride.Value.Item1;
                var newItemPrefab = PrefabsDirectory.instance.directory[newItemPrefabIndex];
                var newItem = newItemPrefab.GetComponent<ShipItem>();

                var itemCooked = crateOverride.Value.Item2;

                ApplyOverride(crate, newItem, itemCooked);
            }
        }

        static void ApplyOverride(ShipItemCrate crate, ShipItem newItem, bool isCooked)
        {
            Traverse.Create(crate).Field<GameObject>("containedPrefab").Value = newItem.gameObject;
            crate.gameObject.name = "custom crate";
            crate.name = newItem.name;
            crate.tag = newItem.tag;
            crate.category = newItem.category;
            crate.smokedFood = isCooked;
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
            // TODO: allow mass transfer of crate contents like barrels
            if ((__instance is ShipItemCrate) == false && targetedCrate != null)
            {
                var thisPrefabIndex = __instance.gameObject.GetComponent<SaveablePrefab>().prefabIndex;
                var crateItemPrefabIndex = targetedCrate.GetContainedPrefab().
                    GetComponent<SaveablePrefab>().prefabIndex;

                if (targetedCrate.amount < 1f && thisPrefabIndex != crateItemPrefabIndex)
                {
                    var existingCratePrefab = References.CratePrefabFromItem(__instance);
                    if (existingCratePrefab != null &&
                        References.GetCrateSize(targetedCrate) == 
                        References.GetCrateSize(existingCratePrefab.GetComponent<ShipItemCrate>()
                        ))
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

                    var isCooked = __instance.GetComponent<CookableFood>() &&
                        __instance.amount >= 1f && __instance.amount < 1.75f;

                    // Time for some code atrocities, courtesy of reflection!
                    RegisterOverride(targetedCrate, thisPrefabIndex, isCooked);
                    ApplyOverride(targetedCrate, __instance, isCooked);

                    __instance.DestroyItem();
                    return false;
                }

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
