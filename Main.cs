using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace ItemStorage
{
    public class ModSettings : UnityModManager.ModSettings, IDrawable
    {
        // place settings here

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void OnChange() { }
    }

    internal static class Main
    {
        public static ModSettings settings;
        public static UnityModManager.ModEntry.ModLogger logger;

        public static float timeSinceLastDebugOut = 0f;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            settings = UnityModManager.ModSettings.Load<ModSettings>(modEntry);
            logger = modEntry.Logger;
            Utilities.SetLogger(modEntry.Logger);

            // uncomment if using settings
            //modEntry.OnGUI = OnGUI;
            //modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnUpdate = OnUpdate;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            timeSinceLastDebugOut += dt;
            //if (timeSinceLastDebugOut > 10)
            //{
            //    Utilities.Log("heartbeat");
            //    timeSinceLastDebugOut = 0;
            //}
        }
    }

    [HarmonyPatch(typeof(ShipItem))]
    static class ItemPatch
    {
        static ShipItemCrate targetedCrate;

        [HarmonyPatch("Update"), HarmonyPostfix]
        static void UpdatePostfix(ref ShipItem __instance, ref GoPointer ___pointedAtBy)
        {
            // I believe simply attempting a cast is better than checking the type based on:
            // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/type-testing-and-cast:
            // "...where E is an expression that returns a value and T is the name of a type or
            // a type parameter, produces the same result as:
            //      E is T ? (T)(E) : (T)null
            var instanceAsCrate = __instance as ShipItemCrate;
            if (instanceAsCrate != null && targetedCrate == null && ___pointedAtBy != null)
            {
                targetedCrate = __instance as ShipItemCrate;
                Utilities.Log("targetedCrate updated: {0}", __instance.name);
            }
            // This instance is not being looked at - but targetedCrate implies is is.
            // This fixes the inconsistency.
            if (___pointedAtBy == null && targetedCrate == __instance)
            {
                targetedCrate = null;
                Utilities.Log("targetedCrate cleared");
            }
        }

        // This method seems inefficient, since it has to retrieve the targetedCrate prefab,
        //  and you might guess since we're patching a ShipItem, now a bunch of different ShipItems
        //  will do duplicate work.
        // Fortunately this isn't the case, since OnAltActivate implies the item is held, and the
        //  player can only hold one item.
        // Really, OnAltActivate shouldn't be called often enough that any of these values should be cached.
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
                    // TO TEST: if it's another good, just grab that good's crate and trade them out?

                    // Time for some code atrocities, courtesy of reflection!
                    var newPrefab = PrefabsDirectory.instance.directory[thisPrefabIndex];
                    Traverse.Create(targetedCrate).Field<GameObject>("containedPrefab").Value = newPrefab;
                    targetedCrate.name = __instance.name;
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