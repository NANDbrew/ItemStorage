using HarmonyLib;
using System;
using UnityEngine;

namespace ItemStorage
{
    public class StorageComponent : GoPointerButton
    {
        public static StorageComponent targetedStorage;
        public static GoPointer targetingPointer;
        public bool uiActive;
        GameObject[] storedItems;
        int maxSlots;

        // TODO: Add method to update mass based on items contained

        // Replace with Start?
        void Start()
        {
            maxSlots = 8;
            storedItems = new GameObject[maxSlots];
            uiActive = false;
        }

        public override void ExtraLateUpdate()
        {
            // Could probably optimize this by caching the ShipItemCrate component on start.
            if (this.gameObject.GetComponent<ShipItemCrate>().IsLookedAt())
            {
                if (targetedStorage == null)
                {
                    targetedStorage = this;
                }

                if (targetingPointer == null)
                {
                    // Fortunately this only needs to run once.
                    // Can I do this elsewhere? I think the name "mouse" is unique among the GoPointers.
                    // Then I wouldn't have to use reflection.
                    targetingPointer = Traverse.Create(this.gameObject.GetComponent<ShipItemCrate>())
                        .Field("pointedAtBy").GetValue<GoPointer>();
                }

                if (uiActive)
                {

                }
            }
            else
            {
                // We're NOT being looked at, but targetedStorage implies we ARE
                // This fixes the inconsistency
                if (targetedStorage == this)
                {
                    targetedStorage = null;
                }
            }
        }



        //
        // The below were good options for testing, but will need to be replaced.
        //  Any system based on retrieving selected items based on a UI can't assume
        //  consecutive slot usage.
        // Additional note: might need to use async for storage so the item doesn't bump stuff
        //  while being stored.
        //
        //public bool AddItemToStorage(GameObject item)
        //{
        //    if (nextAvailableSlot == storedItems.Length)
        //        return false;

        //    storedItems[nextAvailableSlot++] = item;
        //    item.SetActive(false);
        //    item.transform.parent = this.transform;
        //    targetingPointer.DropItem();
        //    Traverse.Create(item.GetComponent<ShipItem>()).Method("ExitBoat").GetValue();

        //    return true;
        //}

        //public bool GetItemFromStorage(GoPointer receivingPointer)
        //{
        //    if (nextAvailableSlot == 0)
        //        return false;

        //    var storedObject = storedItems[--nextAvailableSlot];
        //    storedObject.transform.parent = this.transform.parent;  // correctly identifies boat or world
        //    receivingPointer.PickUpItem(storedObject.GetComponent<PickupableItem>());
        //    storedObject.SetActive(true);

        //    return true;
        //}

        public string StatusOut()
        {
            var status = new System.Text.StringBuilder();

            status.AppendLine(string.Format("Contents of storage {0}:", this.name));

            for (int item = 0; item < storedItems.Length; item++)
            {
                if (storedItems[item])
                    status.AppendLine(string.Format("> [{0}] - {1}", item, storedItems[item].name));
                else
                    status.AppendLine(string.Format("> [{0}] - empty", item));
            }

            return status.ToString();

        }
    }
}
