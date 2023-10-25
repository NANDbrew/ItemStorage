using HarmonyLib;
using System;
using UnityEngine;

namespace ItemStorage
{
    public class StorageComponent : GoPointerButton
    {
        [Serializable]
        public class StorageComponentData
        {
            SerializableVector3 referencePosition;
            SavePrefabData[] storedPrefabs;
        }

        public static StorageComponent targetedStorage;
        public static GoPointer targetingPointer;
        GameObject[] storedItems;
        int nextAvailableSlot = 0;

        void Awake()
        {
            storedItems = new GameObject[8];

        }

        public override void ExtraLateUpdate()
        {
            if (this.gameObject.GetComponent<ShipItemCrate>().IsLookedAt())
            {
                if (targetedStorage == null)
                {
                    targetedStorage = this;
                }

                if (targetingPointer == null)
                {
                    targetingPointer = Traverse.Create(this.gameObject.GetComponent<ShipItemCrate>())
                        .Field("pointedAtBy").GetValue<GoPointer>();
                    Utilities.Log("targeting pointer now {0}", targetingPointer.name);
                }
            }
            else
            {
                if (targetedStorage == this)
                {
                    targetedStorage = null;
                }
            }
        }

        public bool AddItemToStorage(GameObject item)
        {
            if (nextAvailableSlot == storedItems.Length)
                return false;

            storedItems[nextAvailableSlot++] = item;
            item.SetActive(false);
            item.transform.parent = this.transform;
            targetingPointer.DropItem();
            Traverse.Create(item.GetComponent<ShipItem>()).Method("ExitBoat").GetValue();

            return true;
        }

        public bool GetItemFromStorage(GoPointer receivingPointer)
        {
            Utilities.Log("item requested, next slot is {0}", nextAvailableSlot);

            if (nextAvailableSlot == 0)
                return false;

            var storedObject = storedItems[--nextAvailableSlot];
            Utilities.Log("retrieved item: {0}", storedObject.name);
            Utilities.Log("parent of host object: {0}", this.transform.parent.name);
            storedObject.transform.parent = this.transform.parent;
            receivingPointer.PickUpItem(storedObject.GetComponent<PickupableItem>());
            storedObject.SetActive(true);

            return true;
        }
    }
}
