// Path: Assets/Game/Scripts/EquipmentSystem.cs

using UnityEngine;
using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Collections;
public class EquipmentSystem : NetworkBehaviour
{
    private Dictionary<string, string> lastSyncedEquipment = new Dictionary<string, string>();
    private bool needsSync = false;


    [Header("Equipment UI References")]
    [SerializeField] private Image MeleeWeapon2HSlotImage;  // Unity Inspector'dan atanacak    
    [Header("Equipment Slots")]
    [SerializeField] private Dictionary<EquipmentSlotType, List<EquippedItem>> equipmentSlots = new Dictionary<EquipmentSlotType, List<EquippedItem>>();

    // Events
    public event Action<ItemData, EquipmentSlotType, int> OnItemEquipped;
    public event Action<ItemData, EquipmentSlotType, int> OnItemUnequipped;
    public event Action OnStatsUpdated;

    private PlayerStats playerStats;
    private WeaponSystem weaponSystem;
    private Character character; // Character4D yerine Character
    public event System.Action<ItemData, EquipmentSlotType> OnEquipmentChanged;
    private Character frontCharacter;
    private Character backCharacter;
    private Character leftCharacter;
    private Character rightCharacter;

    [Networked] private NetworkString<_256> EquipmentStateJson { get; set; }

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        weaponSystem = GetComponent<WeaponSystem>();
        lastSyncedEquipment = new Dictionary<string, string>();
        needsSync = false;
        character = GetComponent<Character>();

        Transform characterHolder = transform.Find("Front");
        if (characterHolder != null)
        {
            frontCharacter = characterHolder.GetComponent<Character>();
            backCharacter = transform.Find("Back")?.GetComponent<Character>();
            leftCharacter = transform.Find("Left")?.GetComponent<Character>();
            rightCharacter = transform.Find("Right")?.GetComponent<Character>();
        }

        // Fusion authority kontrolü
        if (playerStats == null || weaponSystem == null)
        {
            return;
        }

        InitializeEquipmentSlots();
        InitializeLastSyncedState();
    }

public override void Spawned()
{
    if (Object.HasInputAuthority)
    {
        // Local player - Load ve broadcast
        StartCoroutine(LoadAndBroadcastEquipment());
    }
    else
    {
        // Remote player - Sync request
        // Delay ekle - character sync ile충돌 önleme
        StartCoroutine(DelayedEquipmentSyncRequest());
    }
}

// YENİ METOD
private IEnumerator DelayedEquipmentSyncRequest()
{
    // Character sync'in bitmesini bekle
    yield return new WaitForSeconds(0.3f);
    RequestEquipmentSyncRPC();
}

// DEĞİŞTİRİLECEK
private System.Collections.IEnumerator LoadAndBroadcastEquipment()
{
    // PlayerStats initialize olana kadar bekle
    PlayerStats stats = GetComponent<PlayerStats>();
    int maxAttempts = 50;
    int attempts = 0;
    
    while (stats != null && !stats.isInitialized && attempts < maxAttempts)
    {
        yield return new WaitForSeconds(0.1f);
        attempts++;
    }
    
    // Küçük delay - character appearance için
    yield return new WaitForSeconds(0.1f);
    
    BroadcastCurrentEquipment();
}

[Rpc(RpcSources.All, RpcTargets.InputAuthority)]
public void RequestEquipmentSyncRPC()
{
    if (Object.HasInputAuthority)
    {
        StartCoroutine(BroadcastWhenReady());
    }
}

// YENİ METOD
private IEnumerator BroadcastWhenReady()
{
    PlayerStats stats = GetComponent<PlayerStats>();
    int maxAttempts = 50; // 5 saniye max
    int attempts = 0;
    
    // Equipment load olana kadar bekle
    while (stats != null && !stats.isInitialized && attempts < maxAttempts)
    {
        yield return new WaitForSeconds(0.1f);
        attempts++;
    }
    
    // Küçük ek delay - character appearance sync için
    yield return new WaitForSeconds(0.1f);
    
    // Broadcast yap
    BroadcastCurrentEquipment();
}
    // Equipment değişikliklerinde PlayerStats'a bildir
    private void RefreshPlayerColorsAfterEquipment()
    {
        var playerStats = GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.RefreshOriginalColors();
        }
    }

    private void UnequipMeleeWeapon()
    {
        Character[] characters = { frontCharacter, backCharacter, leftCharacter, rightCharacter };

        foreach (var targetCharacter in characters)
        {
            if (targetCharacter != null)
            {
                targetCharacter.UnEquip(Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.MeleeWeapon2H);
                targetCharacter.Initialize();
            }
        }

        OnEquipmentChanged?.Invoke(null, EquipmentSlotType.MeleeWeapon2H);
    }

    private void UnequipCompositeWeapon()
    {
        Character[] characters = { frontCharacter, backCharacter, leftCharacter, rightCharacter };

        foreach (var targetCharacter in characters)
        {
            if (targetCharacter != null)
            {
                targetCharacter.UnEquip(Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Bow);
                targetCharacter.Initialize();
            }
        }

        OnEquipmentChanged?.Invoke(null, EquipmentSlotType.CompositeWeapon);
    }
// DEĞİŞTİRİLECEK
private void UpdateMeleeWeaponAppearance(ItemData meleeWeapon)
{
    if (meleeWeapon == null) return;

    try
    {
        // Character4D üzerinden parts'a eriş
        Character4D character4D = GetComponent<Character4D>();
        if (character4D != null && character4D.Parts != null)
        {
            foreach (var character in character4D.Parts)
            {
                if (character != null)
                {
                    var entry = character.SpriteCollection.MeleeWeapon2H.Find(i => i.Id == meleeWeapon.SpriteId);
                    if (entry != null)
                    {
                        character.Equip(entry, Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.MeleeWeapon2H);
                    }
                }
            }
            
            character4D.Initialize();
        }
        else
        {
            // Fallback
            UpdateMeleeWeaponForCharacter(frontCharacter, meleeWeapon);
            UpdateMeleeWeaponForCharacter(backCharacter, meleeWeapon);
            UpdateMeleeWeaponForCharacter(leftCharacter, meleeWeapon);
            UpdateMeleeWeaponForCharacter(rightCharacter, meleeWeapon);
        }

        OnEquipmentChanged?.Invoke(meleeWeapon, EquipmentSlotType.MeleeWeapon2H);
        RefreshPlayerColorsAfterEquipment();
    }
    catch (Exception e)
    {
        Debug.LogError($"[EquipmentSystem] Error equipping melee weapon {meleeWeapon.itemName}: {e.Message}\n{e.StackTrace}");
    }
}

    private void UpdateMeleeWeaponForCharacter(Character targetCharacter, ItemData meleeWeapon)
    {
        if (targetCharacter == null) return;

        var entry = targetCharacter.SpriteCollection.MeleeWeapon2H.Find(i => i.Id == meleeWeapon.SpriteId);
        if (entry != null)
        {
            targetCharacter.Equip(entry, Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.MeleeWeapon2H);
            targetCharacter.Initialize();
        }
    }

// DEĞİŞTİRİLECEK
private void UpdateCompositeWeaponAppearance(ItemData compositeWeapon)
{
    if (compositeWeapon == null) return;

    try
    {
        // Character4D üzerinden parts'a eriş
        Character4D character4D = GetComponent<Character4D>();
        if (character4D != null && character4D.Parts != null)
        {
            foreach (var character in character4D.Parts)
            {
                if (character != null)
                {
                    var entry = character.SpriteCollection.Bow.Find(i => i.Id == compositeWeapon.SpriteId);
                    if (entry != null)
                    {
                        character.Equip(entry, Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Bow);
                    }
                }
            }
            
            // Tüm parts'ı tek seferde initialize et
            character4D.Initialize();
        }
        else
        {
            // Fallback - eski metod
            UpdateCompositeWeaponForCharacter(frontCharacter, compositeWeapon);
            UpdateCompositeWeaponForCharacter(backCharacter, compositeWeapon);
            UpdateCompositeWeaponForCharacter(leftCharacter, compositeWeapon);
            UpdateCompositeWeaponForCharacter(rightCharacter, compositeWeapon);
        }

        OnEquipmentChanged?.Invoke(compositeWeapon, EquipmentSlotType.CompositeWeapon);
        RefreshPlayerColorsAfterEquipment();
    }
    catch (Exception e)
    {
        Debug.LogError($"[EquipmentSystem] Error equipping composite weapon {compositeWeapon.itemName}: {e.Message}\n{e.StackTrace}");
    }
}

    private void UpdateCompositeWeaponForCharacter(Character targetCharacter, ItemData compositeWeapon)
    {
        if (targetCharacter == null) return;

        var entry = targetCharacter.SpriteCollection.Bow.Find(i => i.Id == compositeWeapon.SpriteId);
        if (entry != null)
        {
            targetCharacter.Equip(entry, Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Bow);
            targetCharacter.Initialize();
        }
    }
public void BroadcastCurrentEquipment()
{
    if (!Object.HasInputAuthority) return;

    try
    {
        foreach (var kvp in equipmentSlots)
        {
            foreach (var slot in kvp.Value)
            {
                if (slot.isOccupied && slot.item != null)
                {
                    SerializableItemStats serializableStats = new SerializableItemStats();
                    serializableStats.stats = new List<ItemStat>(slot.item.stats);

                    SyncEquipmentStateRPC(
                        slot.item.itemId,
                        slot.item.upgradeLevel,
                        (int)slot.item.currentRarity,
                        (int)kvp.Key,
                        slot.slotIndex,
                        JsonUtility.ToJson(serializableStats),
                        slot.item.armorValue,    // YENİ
                        slot.item.attackPower    // YENİ
                    );
                }
            }
        }
    }
    catch (Exception e)
    {
        Debug.LogError($"[EquipmentSystem] Error broadcasting equipment: {e.Message}");
    }
}
    public bool EquipDefaultItem(ItemData item)
    {

        if (item == null)
        {
            Debug.LogError("[EquipmentSystem] Item is null");
            return false;
        }

        EquipmentSlotType slotType = item.GetEquipmentSlotType();

        if (!equipmentSlots.ContainsKey(slotType))
        {
            Debug.LogError($"[EquipmentSystem] Slot type not found: {slotType}");
            return false;
        }

        var slots = equipmentSlots[slotType];
        if (slots.Count == 0)
        {
            Debug.LogError($"[EquipmentSystem] No slots available for: {slotType}");
            return false;
        }
        int slotIndex = 0;
        SerializableItemStats serializableStats = new SerializableItemStats();
        serializableStats.stats = new List<ItemStat>(item._selectedStats);

        slots[slotIndex].item = item;

        // Görünümü güncelle
        switch (slotType)
        {
            case EquipmentSlotType.Head:
                UpdateHelmetAppearance(item);
                break;
            case EquipmentSlotType.Chest:
                UpdateArmorAppearance(item);
                break;
            case EquipmentSlotType.Bracers:
                UpdateBracersAppearance(item);
                break;
            case EquipmentSlotType.Leggings:
                UpdateLeggingsAppearance(item);
                break;
            case EquipmentSlotType.CompositeWeapon:
                UpdateCompositeWeaponAppearance(item);
                break;
            case EquipmentSlotType.MeleeWeapon2H:
                UpdateMeleeWeaponAppearance(item);
                break;
        }

        OnItemEquipped?.Invoke(item, slotType, slotIndex);
        UpdateStats();

        return true;
    }
[Rpc(RpcSources.InputAuthority, RpcTargets.All)]
private void SyncEquipmentStateRPC(string itemId, int upgradeLevel, int rarityValue, int slotTypeInt, int slotIndex, string statsJson, float armorValue, float attackPower) // YENİ parametreler
{
    try
    {
        ItemData baseItem = ItemDatabase.Instance.GetItemById(itemId);
        if (baseItem == null) return;

        ItemData syncedItem = baseItem.CreateExactCopy();
        syncedItem.upgradeLevel = upgradeLevel;
        syncedItem.currentRarity = (GameItemRarity)rarityValue;
        syncedItem.armorValue = armorValue;     // YENİ
        syncedItem.attackPower = attackPower;   // YENİ

        if (!string.IsNullOrEmpty(statsJson))
        {
            try
            {
                var statsData = JsonUtility.FromJson<SerializableItemStats>(statsJson);
                if (statsData != null && statsData.stats != null)
                {
                    syncedItem.stats = new List<ItemStat>(statsData.stats);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EquipmentSystem] Stats parsing error: {e.Message}");
            }
        }

        EquipmentSlotType slotType = (EquipmentSlotType)slotTypeInt;
        if (equipmentSlots.TryGetValue(slotType, out var slots) && slotIndex < slots.Count)
        {
            slots[slotIndex].item = syncedItem;

            switch (slotType)
            {
                case EquipmentSlotType.Head:
                    UpdateHelmetAppearance(syncedItem);
                    break;
                case EquipmentSlotType.Chest:
                    UpdateArmorAppearance(syncedItem);
                    break;
                case EquipmentSlotType.Bracers:
                    UpdateBracersAppearance(syncedItem);
                    break;
                case EquipmentSlotType.Leggings:
                    UpdateLeggingsAppearance(syncedItem);
                    break;
                case EquipmentSlotType.CompositeWeapon:
                    UpdateCompositeWeaponAppearance(syncedItem);
                    break;
                case EquipmentSlotType.MeleeWeapon2H:
                    UpdateMeleeWeaponAppearance(syncedItem);
                    break;
            }
            OnItemEquipped?.Invoke(syncedItem, slotType, slotIndex);
        }
    }
    catch (Exception e)
    {
        Debug.LogError($"[EquipmentSystem] Error syncing equipment state: {e.Message}");
    }
}

    public void RefreshEquipmentVisuals()
    {
        try
        {
            foreach (var slotPair in equipmentSlots)
            {
                foreach (var slot in slotPair.Value)
                {
                    if (slot.isOccupied && slot.item != null)
                    {
                        // Slot tipine göre görünümü güncelle
                        switch (slotPair.Key)
                        {
                            case EquipmentSlotType.Head:
                                UpdateHelmetAppearance(slot.item);
                                break;
                            case EquipmentSlotType.Bracers:
                                UpdateBracersAppearance(slot.item);
                                break;
                            case EquipmentSlotType.Leggings:
                                UpdateLeggingsAppearance(slot.item);
                                break;
                            case EquipmentSlotType.Chest:
                                UpdateArmorAppearance(slot.item);
                                break;
                            case EquipmentSlotType.CompositeWeapon:
                                UpdateCompositeWeaponAppearance(slot.item);
                                break;
                            case EquipmentSlotType.MeleeWeapon2H:
                                UpdateMeleeWeaponAppearance(slot.item);
                                break;
                        }
                    }
                }
            }
            RefreshPlayerColorsAfterEquipment();
        }
        catch (Exception e)
        {
            Debug.LogError($"[EquipmentSystem] Error refreshing visuals: {e.Message}\n{e.StackTrace}");
        }
    }
    private void UpdateArmorAppearance(ItemData armorItem)
    {
        if (armorItem == null) return;

        try
        {
            UpdateArmorForCharacter(frontCharacter, armorItem);
            UpdateArmorForCharacter(backCharacter, armorItem);
            UpdateArmorForCharacter(leftCharacter, armorItem);
            UpdateArmorForCharacter(rightCharacter, armorItem);

            OnEquipmentChanged?.Invoke(armorItem, EquipmentSlotType.Chest);

            // YENİ EKLEME: PlayerStats color refresh
            RefreshPlayerColorsAfterEquipment();
        }
        catch (Exception e)
        {
            Debug.LogError($"[EquipmentSystem] Error equipping armor {armorItem.itemName}: {e.Message}\n{e.StackTrace}");
        }
    }

    private void UpdateArmorForCharacter(Character targetCharacter, ItemData armorItem)
    {
        if (targetCharacter == null) return;

        var entry = targetCharacter.SpriteCollection.Armor.Find(i => i.Id == armorItem.SpriteId);
        if (entry != null)
        {
            targetCharacter.Equip(entry, Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Armor);
            targetCharacter.Equip(entry, Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Vest);
            targetCharacter.Initialize();
        }
    }

    private void UnequipArmor()
    {
        Character[] characters = { frontCharacter, backCharacter, leftCharacter, rightCharacter };

        foreach (var targetCharacter in characters)
        {
            if (targetCharacter != null)
            {
                targetCharacter.UnEquip(Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Armor);
                targetCharacter.UnEquip(Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Vest);
                targetCharacter.Initialize();
            }
        }

        OnEquipmentChanged?.Invoke(null, EquipmentSlotType.Chest);
    }
    private void UpdateBracersAppearance(ItemData bracersItem)
    {
        if (bracersItem == null) return;

        try
        {
            // Tüm yönlere uygula
            UpdateBracersForCharacter(frontCharacter, bracersItem);
            UpdateBracersForCharacter(backCharacter, bracersItem);
            UpdateBracersForCharacter(leftCharacter, bracersItem);
            UpdateBracersForCharacter(rightCharacter, bracersItem);
            OnEquipmentChanged?.Invoke(bracersItem, EquipmentSlotType.Bracers);
            RefreshPlayerColorsAfterEquipment();
        }
        catch (Exception e)
        {
            Debug.LogError($"[EquipmentSystem] Error equipping bracers {bracersItem.itemName}: {e.Message}\n{e.StackTrace}");
        }
    }

    private void UpdateBracersForCharacter(Character targetCharacter, ItemData bracersItem)
    {
        if (targetCharacter == null) return;

        var entry = targetCharacter.SpriteCollection.Armor.Find(i => i.Id == bracersItem.SpriteId);
        if (entry != null)
        {
            targetCharacter.Equip(entry, Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Bracers);
            targetCharacter.Initialize();
        }
    }
    private void InitializeLastSyncedState()
    {
        lastSyncedEquipment.Clear();
        foreach (var kvp in equipmentSlots)
        {
            foreach (var slot in kvp.Value)
            {
                string slotKey = $"{kvp.Key}_{slot.slotIndex}";
                lastSyncedEquipment[slotKey] = "";
            }
        }
    }

    private void HandleExistingItem(EquippedItem slot)
    {
        var inventorySystem = GetComponent<InventorySystem>();
        if (inventorySystem != null)
        {
            inventorySystem.TryAddItem(slot.item);
        }
    }
    private void InitializeEquipmentSlots()
    {
        foreach (EquipmentSlotType slotType in Enum.GetValues(typeof(EquipmentSlotType)))
        {
            if (slotType == EquipmentSlotType.None) continue;

            int slotCount = GetSlotCount(slotType);
            var slotList = new List<EquippedItem>();

            for (int i = 0; i < slotCount; i++)
            {
                slotList.Add(new EquippedItem
                {
                    slotType = slotType,
                    slotIndex = i
                });
            }

            equipmentSlots[slotType] = slotList;
        }
    }

    private int GetSlotCount(EquipmentSlotType slotType)
    {
        switch (slotType)
        {
            case EquipmentSlotType.Ring:
            case EquipmentSlotType.Earring:
                return 2; // İki slot
            default:
                return 1; // Tek slot
        }
    }

// EquipItem - RPC çağrısında armor/attack gönder
public bool EquipItem(ItemData item, int targetSlotIndex = 0)
{
    if (!Object.HasInputAuthority || item == null) return false;

    EquipmentSlotType slotType = item.GetEquipmentSlotType();
    if (!equipmentSlots.ContainsKey(slotType))
    {
        return false;
    }

    var slots = equipmentSlots[slotType];
    if (targetSlotIndex >= slots.Count)
    {
        return false;
    }

    var inventorySystem = GetComponent<InventorySystem>();
    if (slots[targetSlotIndex].item != null && inventorySystem != null)
    {
        if (!inventorySystem.HasEmptySlot())
        {
            return false;
        }
    }

    if (slots[targetSlotIndex].item != null)
    {
        HandleExistingItem(slots[targetSlotIndex]);
    }

    SerializableItemStats serializableStats = new SerializableItemStats();
    serializableStats.stats = new List<ItemStat>(item._selectedStats);
    string statsJson = JsonUtility.ToJson(serializableStats);

    EquipItemRPC(item.itemId, (int)slotType, targetSlotIndex, item.upgradeLevel, (int)item.Rarity, statsJson,
        item.armorValue, item.attackPower);  // YENİ

    needsSync = true;
    SaveEquipmentState(immediate: true);

    return true;
}

    [Serializable]
    private class SerializableItemStats
    {
        public List<ItemStat> stats = new List<ItemStat>();
    }

    // Item unequip etme
    public bool UnequipItem(EquipmentSlotType slotType, int slotIndex)
    {
        if (!Object.HasInputAuthority) return false;

        if (!equipmentSlots.ContainsKey(slotType))
        {
            return false;
        }

        var slots = equipmentSlots[slotType];
        if (slotIndex >= slots.Count || slots[slotIndex].item == null)
        {
            return false;
        }

        UnequipItemRPC((int)slotType, slotIndex);

        ItemData unequippedItem = slots[slotIndex].item;
        slots[slotIndex].Clear();
        needsSync = true;
        OnItemUnequipped?.Invoke(unequippedItem, slotType, slotIndex);

        // Görünümleri kaldır
        if (slotType == EquipmentSlotType.Head)
        {
            UnequipHelmet();
        }
        else if (slotType == EquipmentSlotType.Bracers)
        {
            UnequipBracers();
        }
        else if (slotType == EquipmentSlotType.Leggings)
        {
            UnequipLeggings();
        }
        else if (slotType == EquipmentSlotType.Chest)
        {
            UnequipArmor();
        }
        else if (slotType == EquipmentSlotType.CompositeWeapon)
        {
            UnequipCompositeWeapon();
        }
        else if (slotType == EquipmentSlotType.MeleeWeapon2H)
        {
            UnequipMeleeWeapon();
        }

        UpdateStats();

        SaveEquipmentState(immediate: true);

        return true;
    }
    private void UpdateLeggingsAppearance(ItemData leggingsItem)
    {
        if (leggingsItem == null) return;

        try
        {
            UpdateLeggingsForCharacter(frontCharacter, leggingsItem);
            UpdateLeggingsForCharacter(backCharacter, leggingsItem);
            UpdateLeggingsForCharacter(leftCharacter, leggingsItem);
            UpdateLeggingsForCharacter(rightCharacter, leggingsItem);
            OnEquipmentChanged?.Invoke(leggingsItem, EquipmentSlotType.Leggings);
            RefreshPlayerColorsAfterEquipment();
        }
        catch (Exception e)
        {
            Debug.LogError($"[EquipmentSystem] Error equipping leggings {leggingsItem.itemName}: {e.Message}\n{e.StackTrace}");
        }
    }

    private void UpdateLeggingsForCharacter(Character targetCharacter, ItemData leggingsItem)
    {
        if (targetCharacter == null) return;

        var entry = targetCharacter.SpriteCollection.Armor.Find(i => i.Id == leggingsItem.SpriteId);
        if (entry != null)
        {
            targetCharacter.Equip(entry, Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Leggings);
            targetCharacter.Initialize();
        }
    }

    private void UnequipLeggings()
    {
        Character[] characters = { frontCharacter, backCharacter, leftCharacter, rightCharacter };

        foreach (var targetCharacter in characters)
        {
            if (targetCharacter != null)
            {
                targetCharacter.UnEquip(Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Leggings);
                targetCharacter.Initialize();
            }
        }

        OnEquipmentChanged?.Invoke(null, EquipmentSlotType.Leggings);
    }
    public List<ItemData> GetEquippedItems(EquipmentSlotType slotType)
    {
        List<ItemData> items = new List<ItemData>();
        if (equipmentSlots.TryGetValue(slotType, out var slots))
        {
            foreach (var slot in slots)
            {
                if (slot.isOccupied)
                {
                    items.Add(slot.item);
                }
            }
        }
        return items;
    }

    public Dictionary<EquipmentSlotType, List<ItemData>> GetAllEquippedItems()
    {
        var result = new Dictionary<EquipmentSlotType, List<ItemData>>();
        foreach (var kvp in equipmentSlots)
        {
            result[kvp.Key] = GetEquippedItems(kvp.Key);
        }
        return result;
    }

private void UpdateStats()
{
    Dictionary<StatType, float> totalStats = new Dictionary<StatType, float>();
    float totalArmor = 0f;
    float totalAttackPower = 0f; // YENI

    foreach (var slotList in equipmentSlots.Values)
    {
        foreach (var slot in slotList)
        {
            if (!slot.isOccupied) continue;

            // Stat bonusları
            foreach (var stat in slot.item.stats)
            {
                if (!totalStats.ContainsKey(stat.type))
                {
                    totalStats[stat.type] = 0;
                }
                totalStats[stat.type] += slot.item.GetStatValue(stat.type);
            }
            
            // Armor değerini topla (armor item ise)
            if (slot.item.IsArmorItem() && slot.item.armorValue > 0)
            {
                totalArmor += slot.item.armorValue;
            }
            
            // YENI - Attack Power değerini topla (weapon item ise)
            if (slot.item.IsWeaponItem() && slot.item.attackPower > 0)
            {
                totalAttackPower += slot.item.attackPower;
            }
        }
    }
    
    // Armor'u stats dictionary'ye ekle
    if (totalArmor > 0)
    {
        totalStats[StatType.Armor] = totalArmor;
    }
    
    // YENI - Attack Power'ı PhysicalDamage olarak ekle
    if (totalAttackPower > 0)
    {
        totalStats[StatType.PhysicalDamage] = totalStats.GetValueOrDefault(StatType.PhysicalDamage, 0f) + totalAttackPower;
    }
    
    playerStats.UpdateEquipmentStats(totalStats);
    OnStatsUpdated?.Invoke();
}
    private void UpdateHelmetAppearance(ItemData helmetItem)
    {
        if (helmetItem == null) return;

        try
        {
            // Tüm yönlere uygula
            UpdateHelmetForCharacter(frontCharacter, helmetItem);
            UpdateHelmetForCharacter(backCharacter, helmetItem);
            UpdateHelmetForCharacter(leftCharacter, helmetItem);
            UpdateHelmetForCharacter(rightCharacter, helmetItem);

            OnEquipmentChanged?.Invoke(helmetItem, EquipmentSlotType.Head);

            RefreshPlayerColorsAfterEquipment();
        }
        catch (Exception e)
        {
            Debug.LogError($"[EquipmentSystem] Error equipping helmet {helmetItem.itemName}: {e.Message}\n{e.StackTrace}");
        }
    }
    private void UpdateHelmetForCharacter(Character targetCharacter, ItemData helmetItem)
    {
        if (targetCharacter == null) return;

        var entry = targetCharacter.SpriteCollection.Armor.Find(i => i.Id == helmetItem.SpriteId);
        if (entry != null)
        {
            targetCharacter.Equip(entry, Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Helmet);
            targetCharacter.Initialize();
        }
    }

// EquipItemRPC - armor/attack parametreleri ekle
[Rpc(RpcSources.InputAuthority, RpcTargets.All)]
private void EquipItemRPC(string itemId, int slotTypeInt, int slotIndex, int upgradeLevel, int rarityValue, string statsJson, float armorValue, float attackPower)
{
    EquipmentSlotType slotType = (EquipmentSlotType)slotTypeInt;
    ItemData baseItem = ItemDatabase.Instance.GetItemById(itemId);

    if (baseItem == null || !equipmentSlots.ContainsKey(slotType))
    {
        return;
    }

    var slots = equipmentSlots[slotType];
    if (slotIndex < slots.Count)
    {
        ItemData previousItem = slots[slotIndex].item;
        if (previousItem != null)
        {
            OnItemUnequipped?.Invoke(previousItem, slotType, slotIndex);
        }

        ItemData upgradedItem = baseItem.CreateExactCopy();
        upgradedItem.upgradeLevel = upgradeLevel;
        upgradedItem.currentRarity = (GameItemRarity)rarityValue;
        upgradedItem.armorValue = armorValue;      // YENİ
        upgradedItem.attackPower = attackPower;    // YENİ
        
        if (!string.IsNullOrEmpty(statsJson))
        {
            try
            {
                var serializableStats = JsonUtility.FromJson<SerializableItemStats>(statsJson);
                if (serializableStats != null && serializableStats.stats != null)
                {
                    upgradedItem._selectedStats = serializableStats.stats;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EquipmentSystem] Stats yükleme hatası: {e.Message}");
            }
        }

        if (slotType == EquipmentSlotType.Head)
        {
            UpdateHelmetAppearance(upgradedItem);
        }
        if (slotType == EquipmentSlotType.Bracers)
        {
            UpdateBracersAppearance(upgradedItem);
        }
        if (slotType == EquipmentSlotType.Leggings)
        {
            UpdateLeggingsAppearance(upgradedItem);
        }
        if (slotType == EquipmentSlotType.Chest)
        {
            UpdateArmorAppearance(upgradedItem);
        }
        if (slotType == EquipmentSlotType.MeleeWeapon2H)
        {
            UpdateMeleeWeaponAppearance(upgradedItem);
        }
        else if (slotType == EquipmentSlotType.CompositeWeapon)
        {
            UpdateCompositeWeaponAppearance(upgradedItem);
        }

        slots[slotIndex].item = upgradedItem;

        OnItemEquipped?.Invoke(upgradedItem, slotType, slotIndex);
        UpdateStats();
    }
}
    private void UnequipBracers()
    {
        Character[] characters = { frontCharacter, backCharacter, leftCharacter, rightCharacter };

        foreach (var targetCharacter in characters)
        {
            if (targetCharacter != null)
            {
                targetCharacter.UnEquip(Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Bracers);
                targetCharacter.Initialize();
            }
        }

        OnEquipmentChanged?.Invoke(null, EquipmentSlotType.Bracers);
    }
    private void UnequipHelmet()
    {
        Character[] characters = { frontCharacter, backCharacter, leftCharacter, rightCharacter };

        foreach (var targetCharacter in characters)
        {
            if (targetCharacter != null)
            {
                targetCharacter.UnEquip(Assets.HeroEditor4D.Common.Scripts.Enums.EquipmentPart.Helmet);
                targetCharacter.Initialize();
            }
        }

        // Unequip event'ini de tetikle
        OnEquipmentChanged?.Invoke(null, EquipmentSlotType.Head);

        // YENİ EKLEME: PlayerStats color refresh
        RefreshPlayerColorsAfterEquipment();
    }


    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]  // [PunRPC] yerine
    private void UnequipItemRPC(int slotTypeInt, int slotIndex)
    {
        EquipmentSlotType slotType = (EquipmentSlotType)slotTypeInt;

        // Önce slots kontrolü
        if (!equipmentSlots.ContainsKey(slotType))
        {
            return;
        }

        var slots = equipmentSlots[slotType];
        if (slotIndex >= slots.Count)
        {
            return;
        }

        // Slotta item var mı kontrol et
        ItemData currentItem = slots[slotIndex].item;
        if (currentItem == null)
        {
            return;
        }

        // Görünümü her client için güncelle
        switch (slotType)
        {
            case EquipmentSlotType.Head:
                UnequipHelmet();
                break;
            case EquipmentSlotType.Bracers:
                UnequipBracers();
                break;
            case EquipmentSlotType.Leggings:
                UnequipLeggings();
                break;
            case EquipmentSlotType.Chest:
                UnequipArmor();
                break;
            case EquipmentSlotType.MeleeWeapon2H:
                UnequipMeleeWeapon();
                break;
            case EquipmentSlotType.CompositeWeapon:
                UnequipCompositeWeapon();
                break;
        }

        // Slot'u temizle
        slots[slotIndex].Clear();

        // Event'i tetikle
        OnItemUnequipped?.Invoke(currentItem, slotType, slotIndex);

        // UI güncelleme
        if (slotType == EquipmentSlotType.MeleeWeapon2H && MeleeWeapon2HSlotImage != null)
        {
            MeleeWeapon2HSlotImage.sprite = null;
            MeleeWeapon2HSlotImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        }

        UpdateStats();
    }

    private async void SaveEquipmentState(bool immediate = false)
    {
        if (playerStats != null)
        {
            try
            {
                if (immediate)
                {
                    await playerStats.SaveStats();
                }
                else
                {
                    var requestSaveMethod = playerStats.GetType().GetMethod("RequestSave",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    requestSaveMethod?.Invoke(playerStats, new object[] { false });
                }

            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EquipmentSystem] Equipment kaydetme hatası: {e.Message}");
            }
        }
    }
    public void UpdateSlotVisuals()
    {
        foreach (var slotList in equipmentSlots)
        {
            if (slotList.Key == EquipmentSlotType.MeleeWeapon2H && slotList.Value.Count > 0)
            {
                var slot = slotList.Value[0];
                if (slot.isOccupied && MeleeWeapon2HSlotImage != null)
                {
                    MeleeWeapon2HSlotImage.sprite = slot.item.itemIcon;
                    MeleeWeapon2HSlotImage.color = Color.white;
                }
            }
        }
    }
    public override void FixedUpdateNetwork()
    {
        // Authority kontrolü - sadece input authority serialize eder
        if (!Object.HasInputAuthority) return;

        // Equipment state değişiklikleri kontrol et
        if (needsSync)
        {
            SerializeEquipmentState();
            needsSync = false;
        }
    }

    private void SerializeEquipmentState()
    {
        try
        {
            var equipmentData = new Dictionary<string, object>();

            foreach (var slotPair in equipmentSlots)
            {
                for (int i = 0; i < slotPair.Value.Count; i++)
                {
                    var slot = slotPair.Value[i];
                    if (slot.isOccupied && slot.item != null)
                    {
                        string key = $"{slotPair.Key}_{i}";
                        equipmentData[key] = new
                        {
                            itemJson = JsonUtility.ToJson(slot.item),
                            stats = slot.item._selectedStats
                        };
                    }
                }
            }

            string jsonData = JsonUtility.ToJson(equipmentData);
            EquipmentStateJson = jsonData;
        }
        catch (Exception e)
        {
            Debug.LogError($"[EquipmentSystem] Equipment serialization error: {e.Message}");
        }
    }
public Dictionary<string, object> GetEquipmentData()
{
    var data = new Dictionary<string, object>();

    foreach (var kvp in equipmentSlots)
    {
        foreach (var slot in kvp.Value)
        {
            if (slot.isOccupied && slot.item != null)
            {
                string slotKey = $"{kvp.Key}_{slot.slotIndex}";

                List<Dictionary<string, object>> selectedStats = new List<Dictionary<string, object>>();
                foreach (var stat in slot.item.stats)
                {
                    selectedStats.Add(new Dictionary<string, object>
                    {
                        { "type", (int)stat.type },
                        { "value", stat.value }
                    });
                }

                data[slotKey] = new Dictionary<string, object>
                {
                    { "itemId", slot.item.itemId },
                    { "upgradeLevel", slot.item.upgradeLevel },
                    { "rarity", (int)slot.item.Rarity },
                    { "armorValue", slot.item.armorValue },      // YENİ
                    { "attackPower", slot.item.attackPower },    // YENİ
                    { "selectedStats", selectedStats }
                };
            }
        }
    }

    return data;
}

public void LoadEquipmentData(Dictionary<string, object> equipData)
{
    try
    {
        foreach (var kvp in equipData)
        {
            try
            {
                string[] slotInfo = kvp.Key.Split('_');
                if (slotInfo.Length != 2) continue;

                if (System.Enum.TryParse(slotInfo[0], out EquipmentSlotType slotType))
                {
                    int slotIndex = int.Parse(slotInfo[1]);

                    var itemData = kvp.Value as Dictionary<string, object>;
                    if (itemData != null)
                    {
                        string itemId = itemData["itemId"].ToString();
                        int upgradeLevel = Convert.ToInt32(itemData["upgradeLevel"]);
                        GameItemRarity rarity = GameItemRarity.Normal;

                        if (itemData.ContainsKey("rarity"))
                        {
                            rarity = (GameItemRarity)Convert.ToInt32(itemData["rarity"]);
                        }

                        var item = ItemDatabase.Instance.GetItemById(itemId);
                        if (item != null)
                        {
                            ItemData upgradedItem = item.CreateExactCopy();
                            upgradedItem.upgradeLevel = upgradeLevel;
                            upgradedItem.currentRarity = rarity;
                            
                            // YENİ: Armor ve Attack Power load
                            if (itemData.ContainsKey("armorValue"))
                            {
                                upgradedItem.armorValue = Convert.ToSingle(itemData["armorValue"]);
                            }
                            
                            if (itemData.ContainsKey("attackPower"))
                            {
                                upgradedItem.attackPower = Convert.ToSingle(itemData["attackPower"]);
                            }

                            if (itemData.ContainsKey("selectedStats"))
                            {
                                var selectedStats = new List<ItemStat>();
                                var statsData = itemData["selectedStats"] as List<object>;

                                foreach (var stat in statsData)
                                {
                                    var statDict = stat as Dictionary<string, object>;
                                    if (statDict != null)
                                    {
                                        selectedStats.Add(new ItemStat
                                        {
                                            type = (StatType)Convert.ToInt32(statDict["type"]),
                                            value = Convert.ToSingle(statDict["value"])
                                        });
                                    }
                                }

                                upgradedItem.stats = selectedStats;
                            }

                            if (equipmentSlots.TryGetValue(slotType, out var slots) &&
                                slotIndex < slots.Count)
                            {
                                slots[slotIndex].item = upgradedItem;
                                OnItemEquipped?.Invoke(upgradedItem, slotType, slotIndex);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EquipmentSystem] Failed to load equipment slot {kvp.Key}: {e.Message}");
            }
        }

        ClearEmptySlotVisuals();

        if (Object.HasInputAuthority)
        {
            BroadcastCurrentEquipment();
        }
    }
    catch (Exception e)
    {
        Debug.LogError($"[EquipmentSystem] Failed to load equipment: {e.Message}");
    }

    UpdateStats();
}
    public void ClearEmptySlotVisuals()
    {
        try
        {

            // Tüm equipment slotlarını kontrol et
            foreach (var slotPair in equipmentSlots)
            {
                foreach (var slot in slotPair.Value)
                {
                    // Eğer slot boşsa görselini temizle
                    if (!slot.isOccupied || slot.item == null)
                    {
                        switch (slotPair.Key)
                        {
                            case EquipmentSlotType.MeleeWeapon2H:
                                UnequipMeleeWeapon();
                                break;
                            case EquipmentSlotType.CompositeWeapon:
                                UnequipCompositeWeapon();
                                break;
                            case EquipmentSlotType.Head:
                                UnequipHelmet();
                                break;
                            case EquipmentSlotType.Chest:
                                UnequipArmor();
                                break;
                            case EquipmentSlotType.Bracers:
                                UnequipBracers();
                                break;
                            case EquipmentSlotType.Leggings:
                                UnequipLeggings();
                                break;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[EquipmentSystem] Error clearing empty slot visuals: {e.Message}");
        }
    }
}
[System.Serializable]
    public class EquippedItem
    {
        private ItemData _item;
        public EquipmentSlotType slotType;
        public int slotIndex;

        public ItemData item 
        {
            get => _item;
            set 
            {
                _item = value;
            }
        }

        public bool isOccupied 
        {
            get 
            {
                bool occupied = _item != null;
                return occupied;
            }
        }

        public EquippedItem()
        {
            _item = null;
        }

        public void Clear()
        {
            _item = null;
        }
    }