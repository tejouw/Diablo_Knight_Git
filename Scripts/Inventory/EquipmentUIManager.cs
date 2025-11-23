// Path: Assets/Game/Scripts/EquipmentUIManager.cs

using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
public class EquipmentUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject equipmentPanel;

    [Header("Equipment Slot References")]
    [SerializeField] private UISlot MeleeWeapon2HSlot;
    [SerializeField] private UISlot CompositeWeaponSlot;
    [SerializeField] private UISlot helmetSlot;
    [SerializeField] private UISlot chestSlot;
    [SerializeField] private UISlot bracersSlot;
    [SerializeField] private UISlot leggingsSlot;
    [SerializeField] private UISlot[] ringSlots = new UISlot[2];
    [SerializeField] private UISlot[] earringSlots = new UISlot[2];
    [SerializeField] private UISlot beltSlot;

    private EquipmentSystem playerEquipmentSystem;

    private void Start()
    {
        StartCoroutine(FindPlayerEquipment());
    }

private IEnumerator FindPlayerEquipment()
{
    while (true)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.HasInputAuthority)
            {
                playerEquipmentSystem = player.GetComponent<EquipmentSystem>();
                if (playerEquipmentSystem != null)
                {
                    // Event subscribe
                    playerEquipmentSystem.OnItemEquipped += HandleItemEquipped;
                    playerEquipmentSystem.OnItemUnequipped += HandleItemUnequipped;
                    
                    // ✅ YENİ: Event subscription sonrası mevcut equipment'ı UI'a yükle
                    RefreshAllEquipmentSlots();
                    
                    yield break;
                }
            }
        }
        yield return new WaitForSeconds(0.5f);
    }
}private void RefreshAllEquipmentSlots()
{
    if (playerEquipmentSystem == null) return;
    
    var allEquippedItems = playerEquipmentSystem.GetAllEquippedItems();
    
    foreach (var kvp in allEquippedItems)
    {
        EquipmentSlotType slotType = kvp.Key;
        List<ItemData> items = kvp.Value;
        
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
            {
                UISlot targetSlot = GetSlotByType(slotType, i);
                if (targetSlot != null)
                {
                    targetSlot.UpdateSlot(items[i]);
                }
            }
        }
    }
}
    private void HandleItemEquipped(ItemData item, EquipmentSlotType slotType, int slotIndex)
    {
        UISlot targetSlot = GetSlotByType(slotType, slotIndex);
        if (targetSlot != null)
        {
            targetSlot.UpdateSlot(item);
        }
    }

    private void HandleItemUnequipped(ItemData item, EquipmentSlotType slotType, int slotIndex)
    {
        UISlot targetSlot = GetSlotByType(slotType, slotIndex);
        if (targetSlot != null)
        {
            targetSlot.ClearSlot();
        }
    }

    private UISlot GetSlotByType(EquipmentSlotType slotType, int slotIndex)
    {
        return slotType switch
        {
            EquipmentSlotType.MeleeWeapon2H => MeleeWeapon2HSlot,
            EquipmentSlotType.CompositeWeapon => CompositeWeaponSlot,
            EquipmentSlotType.Head => helmetSlot,
            EquipmentSlotType.Chest => chestSlot,
            EquipmentSlotType.Bracers => bracersSlot,
            EquipmentSlotType.Leggings => leggingsSlot,
            EquipmentSlotType.Ring when slotIndex < ringSlots.Length => ringSlots[slotIndex],
            EquipmentSlotType.Earring when slotIndex < earringSlots.Length => earringSlots[slotIndex],
            EquipmentSlotType.Belt => beltSlot,
            _ => null
        };
    }


    private void OnDestroy()
    {
        if (playerEquipmentSystem != null)
        {
            playerEquipmentSystem.OnItemEquipped -= HandleItemEquipped;
            playerEquipmentSystem.OnItemUnequipped -= HandleItemUnequipped;
        }
    }
}