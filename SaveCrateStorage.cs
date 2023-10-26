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

            Utilities.Log("Squashed position for crates:");
            foreach (var crate in crateData)
                Utilities.Log("> {0}", SquashVector(crate.position));

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
                Utilities.Log("Searching for data matching crate {0}", crate.name);
                var matchingData = crateStorageData.Find(data =>
                SquashVector(crate.transform.position) == SquashVector(data.position));
                if (matchingData != null)
                {
                    Utilities.Log("Found matching data:");
                    Utilities.Log("> position: {0}", matchingData.position);
                    Utilities.Log("> contained prefab: {0} ({1})", matchingData.containedPrefabIndex,
                        PrefabsDirectory.instance.directory[matchingData.containedPrefabIndex].name);
                }
                else Utilities.Log("No matching data found.");
            }

            Utilities.Log("Squashed vectors of spawned crates:");
            foreach (var crate in crates)
                Utilities.Log("> {0}", SquashVector(crate.transform.position));

            Utilities.Log("Squashed vectors of loaded crates:");
            foreach (var savedCrate in crateStorageData)
                Utilities.Log("> {0}", SquashVector(savedCrate.position));

            Utilities.Log("TEST: {0}", SquashVector(crates.First().transform.position - FloatingOriginManager.instance.outCurrentOffset));
        }

        static string SquashVector(Vector3 vector)
        {
            return string.Format("{0:F4}{1:F4}{2:F4}",
                vector.x, vector.y, vector.z);
        }
    }
}
