using HarmonyLib;
using System.Reflection;
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

    [HarmonyPatch(typeof(ShipItemCrate))]
    static class CratePatch
    {
        //[HarmonyPatch("OnLoad"), HarmonyPostfix]
        //static void LoadPostfix(ref ShipItemCrate __instance)
        //{
        //    Utilities.Log("Added storage component to crate: {0}", __instance.name);
        //    __instance.gameObject.AddComponent<StorageComponent>();
        //}

        //[HarmonyPatch("OnAltActivate"), HarmonyPrefix]
        //static bool OnAltActivatePrefix(ref GoPointer activatingPointer, ref ShipItemCrate __instance)
        //{
        //    Utilities.Log("crate alt activated");
        //    var storage = __instance.gameObject.GetComponent<StorageComponent>();
        //    storage.GetItemFromStorage(activatingPointer);

        //    return false;
        //}
    }

    [HarmonyPatch(typeof(ShipItem))]
    static class ItemPatch
    {
        //[HarmonyPatch("OnAltActivate"), HarmonyPrefix]
        //static bool OnAltActivatePrefix(ref ShipItem __instance)
        //{
        //    if (__instance.GetType() == typeof(ShipItemCrate))
        //        return true;

        //    if (StorageComponent.targetedStorage != null)
        //    {
        //        StorageComponent.targetedStorage.AddItemToStorage(__instance.gameObject);
        //        return false;
        //    }

        //    return true;
        //}
    }
}