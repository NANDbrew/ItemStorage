using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

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

            // this should go in utilities eventually
            var formatter = new BinaryFormatter();
            var filepath = string.Format("{0}/slot{1}_is.modsave",
                Application.persistentDataPath, SaveSlots.currentSlot);
            var filestream = File.Create(filepath);
            formatter.Serialize(filestream, crateData);
            filestream.Close();
        }

        [HarmonyPatch("LoadModData"), HarmonyPostfix]
        static void LoadModDataPostfix()
        {
            var crates = GameObject.FindObjectsOfType<ShipItemCrate>();

            var formatter = new BinaryFormatter();
            var filepath = string.Format("{0}/slot{1}_is.modsave",
                Application.persistentDataPath, SaveSlots.currentSlot);

            // TODO: ensure mod save file creation date and save slot file creation date are the same
            // TODO: nice messaging for warning players if:
            //  - there's no mod save data
            //  - the creation dates don't match

            if (File.Exists(filepath) == false)
            {
                return;
            }
                
            var filestream = File.Open(filepath, FileMode.Open);
            var crateStorageData = formatter.Deserialize(filestream) as List<SaveCrateStorageData>;
            filestream.Close();

            foreach (var crate in crates)
            {
                // TODO: check and warn player if multiple matching crates found
                //  definitely a critical error: maybe pop up a notification telling the player
                //  "CRITICAL ERROR: please forward your save files to Natorius to review"
                //  or similar
                //  ...do I want to ask for that

                var matchingData = crateStorageData.Where(
                    data => SamePosition(crate.transform.position, data.position))
                    .First();

                Main.OverrideContainedPrefab(crate, matchingData.containedPrefabIndex);
            }

            
        }

        static bool SamePosition(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 0.0001f;  // value found experimentally, may need tweaking
        }
    }
}
