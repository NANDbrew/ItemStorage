using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Playables;
using UnityModManagerNet;

namespace ItemStorage
{
    [Serializable]
    public class SaveCrateStorageData
    {
        public SerializableVector3 position;
        public int containedPrefabIndex;

        public SaveCrateStorageData(Vector3 position, int containedPrefabIndex)
        {
            this.position = position;
            this.containedPrefabIndex = containedPrefabIndex;
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager))]
    class SaveLoadManagerPatch
    {
        [HarmonyPatch("SaveModData"), HarmonyPostfix]
        // Normal practice would be to provide the mod ID as a parameter,
        // but since this is assuming other mods may be patching this method as well,
        // no reason not to hardcode it.
        static void SaveModDataPostfix(ref List<SaveablePrefab> ___currentPrefabs)
        {
            var crates = ___currentPrefabs
                .Select(prefab => prefab.gameObject.GetComponent<ShipItemCrate>())
                .Where(crateComponent => crateComponent != null);

            // We'll just store every crate; the Load function can check whether
            //  a crate doesn't match its assigned containedPrefab.
            var crateData = crates.Select(crate =>
                new SaveCrateStorageData(
                    crate.gameObject.transform.position,
                    crate.GetContainedPrefab().GetComponent<SaveablePrefab>().prefabIndex
                )
            ).ToList();

            foreach (var dataObj in crateData)
            {
                Utilities.Log("Stored data for crate:");
                Utilities.Log("> {0}", (Vector3)dataObj.position);
                Utilities.Log("> {0} ({1})", dataObj.containedPrefabIndex,
                    PrefabsDirectory.instance.directory[dataObj.containedPrefabIndex].name);
            }

            // this should go in utilities eventually
            var formatter = new BinaryFormatter();
            var filepath = string.Format("{0}/slot{1}_{2}.modsave",
                Application.persistentDataPath, SaveSlots.currentSlot, Main.modID);
            var filestream = File.Create(filepath);
            formatter.Serialize(filestream, crateData);
            filestream.Close();
        }

        [HarmonyPatch("LoadModData"), HarmonyPostfix]
        static void LoadModDataPostfix()
        {
            var crates = GameObject.FindObjectsOfType<ShipItemCrate>();
            Utilities.Log("Found {0} crates spawned", crates.Length);

            var formatter = new BinaryFormatter();
            var filepath = string.Format("{0}/slot{1}_{2}.modsave",
                Application.persistentDataPath, SaveSlots.currentSlot, Main.modID);

            // TODO: ensure mod save file creation date and save slot file creation date are the same

            if (File.Exists(filepath) == false)
            {
                Utilities.Log("No mod save file found.");
                return;
            }
                
            var filestream = File.Open(filepath, FileMode.Open);
            var crateStorageData = formatter.Deserialize(filestream) as List<SaveCrateStorageData>;
            filestream.Close();
            Utilities.Log("Loaded {0} crate storage data containers", crateStorageData.Count);

            foreach (var crate in crates)
            {
                var matchingDataContainers = crateStorageData.Where(data => SamePosition(crate.transform.position, data.position)).ToList();
                Utilities.Log("> {0} matching data containers found for crate \"{1}\"", matchingDataContainers.Count, crate.gameObject.name);

                Main.OverrideContainedPrefab(crate, matchingDataContainers.First().containedPrefabIndex);
            }

            
        }

        static bool SamePosition(Vector3 a, Vector3 b)
        {
            //Utilities.Log("SQRMAG: {0}", (a - b).sqrMagnitude);
            return (a - b).sqrMagnitude < 0.0001f;
        }
    }
}
