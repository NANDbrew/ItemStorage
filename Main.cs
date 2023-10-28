using HarmonyLib;
using System.Reflection;
using UnityModManagerNet;

namespace ItemStorage
{
    static class Main
    {
        static UnityModManager.ModEntry.ModLogger logger;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            logger = modEntry.Logger;
            //Persistence.Register(Save, Load);

            return true;
        }

        static object Save()
        {
            return null;
        }

        static void Load(object savedData)
        {

        }
    }
}