using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using UnityEditor;
using System.Linq;
using System;

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
    class ShipItemPatch
    {
        static ShipItemCrate crateBeingLookedAt;

        [HarmonyPatch("Update"), HarmonyPostfix]
        static void UpdatePostfix(ref ShipItem __instance, ref GoPointer ___pointedAtBy)
        {
            if (__instance.GetType() == typeof(ShipItemCrate) && ___pointedAtBy != null)
                crateBeingLookedAt = __instance as ShipItemCrate;
        }

        [HarmonyPatch("OnAltActivate"), HarmonyPrefix]
        static bool OnAltActivatePrefix(ref ShipItem __instance)
        {
            if (crateBeingLookedAt != null)
            {
                var heldItemPrefab = __instance.GetComponent<SaveablePrefab>();
                var cratePrefab = crateBeingLookedAt.GetContainedPrefab().GetComponent<SaveablePrefab>();

                if (heldItemPrefab == null || cratePrefab == null)
                    return true;

                var heldItemPrefabIndex = heldItemPrefab.prefabIndex;
                var cratePrefabIndex = cratePrefab.prefabIndex;

                if (heldItemPrefabIndex == cratePrefabIndex)
                {
                    crateBeingLookedAt.amount += 1;
                    crateBeingLookedAt.itemRigidbodyC.UpdateMass();
                    __instance.DestroyItem();

                    return false;
                }
                else if (crateBeingLookedAt.amount < 1)
                {
                    var heldItemDirectoryPrefab = PrefabsDirectory.instance.directory[heldItemPrefabIndex];

                    // time to get screwy with reflection, yeah!
                    Traverse.Create(crateBeingLookedAt).Field("containedPrefab").SetValue(heldItemDirectoryPrefab);

                    crateBeingLookedAt.amount = 1;
                    __instance.DestroyItem();

                    return false;
                }

                return true;
            }
            else
                return true;
        }
    }
}