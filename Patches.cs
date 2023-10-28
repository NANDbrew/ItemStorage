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

                    // Time for some code atrocities, courtesy of reflection!
                    OverrideContainedPrefab(targetedCrate, thisPrefabIndex);

                    targetedCrate.gameObject.name = "custom crate";
                    targetedCrate.name = __instance.name;
                    targetedCrate.tag = __instance.tag;
                    targetedCrate.category = __instance.category;
                    targetedCrate.amount = 1f;

                    var isCooked = __instance.GetComponent<CookableFood>() &&
                        __instance.amount >= 1f && __instance.amount < 1.75f;
                    if (isCooked)
                        targetedCrate.smokedFood = true;

                    RegisterOverride(targetedCrate, thisPrefabIndex, isCooked);

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
