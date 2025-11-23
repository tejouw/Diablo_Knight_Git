// Path: Assets/Game/Scripts/InventorySystem.cs

using UnityEngine;
using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class InventorySlot
{
    private ItemData _item;
    private int _amount;
    public Vector2Int position;

    public ItemData item
    {
        get => _item;
        set
        {
            _item = value;
            if (_item == null)
            {
                _amount = 0;
            }
        }
    }

    public int amount
    {
        get => _amount;
        set
        {
            _amount = value;
            if (_amount <= 0)
            {
                _item = null;
                _amount = 0;
            }
        }
    }

    public bool isEmpty => _item == null || _amount <= 0;

    public InventorySlot(Vector2Int pos)
    {
        position = pos;
        _item = null;
        _amount = 0;
    }

    public void Clear()
    {
        _item = null;
        _amount = 0;
        hasChanged = true;
    }
    public bool hasChanged { get; set; } = false;

public void MarkAsChanged()
{
    hasChanged = true;
}
}

public class InventorySystem : NetworkBehaviour
{
    public const int INVENTORY_ROWS = 3;
    public const int INVENTORY_COLS = 6;

    private Dictionary<Vector2Int, InventorySlot> slots = new Dictionary<Vector2Int, InventorySlot>();
    
    // Events
    public event Action<InventorySlot> OnItemAdded;
    public event Action<InventorySlot> OnItemRemoved;
    public event Action<InventorySlot> OnSlotUpdated;
    
    private PlayerStats playerStats;
    private EquipmentSystem equipmentSystem;

        public bool HasEmptySlot()
    {
        foreach (var slot in slots.Values)
        {
            if (slot.isEmpty)
            {
                return true;
            }
        }
        return false;
    }
    public InventorySlot GetSlot(Vector2Int position)
    {
        
        if (slots == null)
        {
            return null;
        }

        if (slots.TryGetValue(position, out InventorySlot slot))
        {
            return slot;
        }
        
        return null;
    }
    public Dictionary<Vector2Int, InventorySlot> GetAllSlots()
    {
        return slots;
    }
private void Awake()
{
    playerStats = GetComponent<PlayerStats>();
    equipmentSystem = GetComponent<EquipmentSystem>();

    if (playerStats == null || equipmentSystem == null)
    {
        return;
    }

    InitializeInventory();
}
    public bool HasItem(string itemId)
    {
        foreach (var slot in slots.Values)
        {
            if (!slot.isEmpty && slot.item != null && slot.item.itemId == itemId)
            {
                return true;
            }
        }
        return false;
    }
public bool RemoveItemById(string itemId, int amount = 1)
{
    
    if (!Object.HasInputAuthority)
    {
        return false;
    }
    
    int remainingAmount = amount;
    List<Vector2Int> slotsToRemove = new List<Vector2Int>();
    
    
    // Önce tüm slotları tara ve topla
    foreach (var kvp in slots)
    {
        if (!kvp.Value.isEmpty && kvp.Value.item != null && kvp.Value.item.itemId == itemId)
        {
            
            if (kvp.Value.amount >= remainingAmount)
            {
                RPC_RemoveItemAmount(kvp.Key.x, kvp.Key.y, remainingAmount);
                remainingAmount = 0;
                break;
            }
            else
            {
                remainingAmount -= kvp.Value.amount;
                slotsToRemove.Add(kvp.Key);
            }
        }
    }
    
    
    // Biriken slotları tamamen temizle
    foreach (var pos in slotsToRemove)
    {
        RPC_ClearSlot(pos.x, pos.y);
    }
    
    if (Object.HasInputAuthority)
    {
        SaveInventoryState(immediate: true);
    }
    
    bool success = remainingAmount == 0;
    
    return success;
}

// ✅ YENİ RPC: Belirli miktarda item sil
[Rpc(RpcSources.InputAuthority, RpcTargets.All)]
private void RPC_RemoveItemAmount(int x, int y, int amount)
{
    Vector2Int position = new Vector2Int(x, y);

    if (slots.TryGetValue(position, out InventorySlot slot))
    {
        if (slot.amount > amount)
        {
            slot.amount -= amount;
            OnSlotUpdated?.Invoke(slot);
        }
        else
        {
            slot.item = null;
            slot.amount = 0;
            OnItemRemoved?.Invoke(slot);
            OnSlotUpdated?.Invoke(slot);
        }

        if (Object.HasInputAuthority)
        {
            SaveInventoryState();
        }
    }
}
private void InitializeInventory()
{
    
    // Eğer slotlar zaten oluşturulmuşsa, tekrar oluşturma
    if (slots != null && slots.Count > 0)
    {
        return;
    }
    
    // Slots dictionary'sini initialize et
    if (slots == null)
    {
        slots = new Dictionary<Vector2Int, InventorySlot>();
    }
    
    // Slotlar boşsa yeni slotlar oluştur
    for (int y = 0; y < INVENTORY_ROWS; y++)
    {
        for (int x = 0; x < INVENTORY_COLS; x++)
        {
            Vector2Int position = new Vector2Int(x, y);
            slots[position] = new InventorySlot(position);
        }
    }
    
}

// TryAddItem - RPC çağrılarında armor/attack gönder
public bool TryAddItem(ItemData item, int amount = 1, bool fromDroppedLoot = false)
{
    bool isServerLootOperation = Object.HasStateAuthority && !Object.HasInputAuthority;

    if (isServerLootOperation)
    {
        RPC_RequestAddItemToClient(
            item.itemId, 
            item.upgradeLevel, 
            (int)item.currentRarity,
            amount, 
            JsonUtility.ToJson(new SerializableItemStats { stats = item.stats }),
            item.armorValue,      // YENİ
            item.attackPower,     // YENİ
            fromDroppedLoot
        );
        return true;
    }

    if (item.isStackable)
    {
        var existingSlot = slots.Values.FirstOrDefault(slot =>
            !slot.isEmpty && slot.item != null &&
            slot.item.itemId == item.itemId &&
            slot.item.upgradeLevel == item.upgradeLevel &&
            slot.amount < item.maxStackSize);

        if (existingSlot != null)
        {
            RPC_AddToStack(existingSlot.position.x, existingSlot.position.y, amount, fromDroppedLoot);
            return true;
        }
    }

    var emptySlot = slots.Values.FirstOrDefault(slot => slot.isEmpty);
    if (emptySlot != null)
    {
        SerializableItemStats serializableStats = new SerializableItemStats();
        serializableStats.stats = new List<ItemStat>();

        foreach (var stat in item.stats)
        {
            serializableStats.stats.Add(new ItemStat
            {
                type = stat.type,
                value = stat.value
            });
        }

        string statsJson = JsonUtility.ToJson(serializableStats);

        RPC_AddItemToSlot(emptySlot.position.x, emptySlot.position.y,
            item.itemId, item.upgradeLevel, (int)item.currentRarity, amount, statsJson, 
            item.armorValue, item.attackPower,  // YENİ
            fromDroppedLoot);

        return true;
    }
    else
    {
        ChatManager.Instance?.ShowInventoryFullMessage();
        return false;
    }
}
// RPC_RequestAddItemToClient - armor/attack parametreleri ekle
[Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
private void RPC_RequestAddItemToClient(string itemId, int upgradeLevel, int rarityValue, int amount, string statsJson, float armorValue, float attackPower, bool fromDroppedLoot = false)
{
    // 🔒 PRODUCTION SAFETY: Comprehensive logging
    Debug.Log($"[InventorySystem] 📦 RPC_RequestAddItemToClient START - ItemId: {itemId}, Amount: {amount}, FromDroppedLoot: {fromDroppedLoot}");

    // 🔒 PRODUCTION SAFETY: ItemDatabase validation
    if (ItemDatabase.Instance == null)
    {
        Debug.LogError($"[InventorySystem] ❌ CRITICAL: ItemDatabase.Instance is NULL on client! Cannot add item: {itemId}");
        RPC_ReportAddItemResult(itemId, false);
        return;
    }

    Debug.Log($"[InventorySystem] ✅ ItemDatabase.Instance is valid, fetching item: {itemId}");

    ItemData baseItem = ItemDatabase.Instance.GetItemById(itemId);
    if (baseItem != null)
    {
        Debug.Log($"[InventorySystem] ✅ Base item found: {itemId}, Name: {baseItem.itemName}");

        // 🔒 PRODUCTION SAFETY: Validate sprite before copy
        if (baseItem.itemIcon == null)
        {
            Debug.LogError($"[InventorySystem] ❌ WARNING: Base item {itemId} has NULL itemIcon! UI may not display correctly.");
        }

        ItemData itemCopy = baseItem.CreateExactCopy();
        itemCopy.upgradeLevel = upgradeLevel;
        itemCopy.currentRarity = (GameItemRarity)rarityValue;
        itemCopy.armorValue = armorValue;
        itemCopy.attackPower = attackPower;

        // 🔒 PRODUCTION SAFETY: Validate copy
        if (itemCopy.itemIcon == null)
        {
            Debug.LogError($"[InventorySystem] ❌ CRITICAL: itemCopy.itemIcon is NULL after CreateExactCopy()! Item: {itemId}");
        }
        else
        {
            Debug.Log($"[InventorySystem] ✅ Item copy created successfully with sprite: {itemCopy.itemIcon.name}");
        }

        if (!string.IsNullOrEmpty(statsJson))
        {
            try
            {
                SerializableItemStats statsData = JsonUtility.FromJson<SerializableItemStats>(statsJson);
                if (statsData != null && statsData.stats != null)
                {
                    itemCopy.stats = statsData.stats;
                    Debug.Log($"[InventorySystem] ✅ Stats parsed successfully: {statsData.stats.Count} stats");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[InventorySystem] ❌ Stats parsing error: {e.Message}");
            }
        }

        Debug.Log($"[InventorySystem] 📥 Calling TryAddItemLocal for item: {itemId}");
        bool success = TryAddItemLocal(itemCopy, amount, fromDroppedLoot);

        Debug.Log($"[InventorySystem] {(success ? "✅ SUCCESS" : "❌ FAILED")} - TryAddItemLocal result: {success} for item: {itemId}");

        RPC_ReportAddItemResult(itemId, success);
    }
    else
    {
        Debug.LogError($"[InventorySystem] ❌ CRITICAL: Base item not found in ItemDatabase for ItemId: {itemId}!");
        Debug.LogError($"[InventorySystem] ItemDatabase has {ItemDatabase.Instance?.GetNormalItems()?.Count ?? 0} normal items and {ItemDatabase.Instance?.GetAllCraftItems()?.Count ?? 0} craft items");
        RPC_ReportAddItemResult(itemId, false);
    }
}
public void RequestLootCollection(DroppedLoot droppedLoot, string itemId)
{
    if (!Object.HasInputAuthority) return;
    
    RPC_RequestLootCollection(droppedLoot.Object.Id, itemId);
}

[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void RPC_RequestLootCollection(NetworkId droppedLootId, string itemId)
{
    
    if (Runner.TryFindObject(droppedLootId, out NetworkObject droppedLootObj))
    {
        DroppedLoot droppedLoot = droppedLootObj.GetComponent<DroppedLoot>();
        if (droppedLoot != null)
        {
            var itemToCollect = droppedLoot.GetDroppedItems().FirstOrDefault(x => x.itemId == itemId);
            if (itemToCollect != null)
            {
                bool success = TryAddItem(itemToCollect, 1, true);
                if (success)
                {
                    droppedLoot.RPC_RemoveCollectedItem(itemId);
                }
            }
        }
    }
}
[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void RPC_ReportAddItemResult(string itemId, bool success)
{
    if (!Object.HasStateAuthority)
    {
        return;
    }
    
    // Event'i fırlat
    OnItemAddResult?.Invoke(itemId, success);
    
}

// Event tanımla
public static event System.Action<string, bool> OnItemAddResult;

private bool TryAddItemLocal(ItemData item, int amount = 1, bool fromDroppedLoot = false)
{
    // 🔒 PRODUCTION SAFETY: Input validation
    if (item == null)
    {
        Debug.LogError("[InventorySystem] ❌ TryAddItemLocal called with NULL item!");
        return false;
    }

    Debug.Log($"[InventorySystem] 🎯 TryAddItemLocal START - Item: {item.itemId}, Amount: {amount}, Stackable: {item.isStackable}, FromDroppedLoot: {fromDroppedLoot}");

    // 🔒 PRODUCTION SAFETY: Validate item sprite
    if (item.itemIcon == null)
    {
        Debug.LogError($"[InventorySystem] ❌ WARNING: Item {item.itemId} has NULL itemIcon in TryAddItemLocal!");
    }

    if (item.isStackable)
    {
        var existingSlot = slots.Values.FirstOrDefault(slot =>
            !slot.isEmpty && slot.item != null &&
            slot.item.itemId == item.itemId &&
            slot.item.upgradeLevel == item.upgradeLevel &&
            slot.amount < item.maxStackSize);

        if (existingSlot != null)
        {
            Debug.Log($"[InventorySystem] 📚 Found existing stack at ({existingSlot.position.x}, {existingSlot.position.y}), current amount: {existingSlot.amount}, adding: {amount}");

            existingSlot.amount += amount;
            OnSlotUpdated?.Invoke(existingSlot);

            Debug.Log($"[InventorySystem] ✅ Stack updated, new amount: {existingSlot.amount}, Event fired: OnSlotUpdated");

            // ✅ YENİ: Quest progress güncelle
            if (Object.HasInputAuthority && QuestManager.Instance != null)
            {
                QuestManager.Instance.UpdateQuestProgress("", QuestType.CollectItems, item.itemId, amount);
            }

            if (Object.HasInputAuthority && fromDroppedLoot)
            {
                ChatManager.Instance?.ShowItemPickupMessage(item.itemName);

                // Tüm itemler için notification göster
                if (FragmentNotificationUI.Instance != null)
                {
                    FragmentNotificationUI.Instance.ShowFragmentNotification(
                        item.itemName,
                        amount,
                        item.itemIcon
                    );
                }
            }

            SaveInventoryState();
            return true;
        }
        else
        {
            Debug.Log($"[InventorySystem] No existing stack found for stackable item: {item.itemId}, will create new slot");
        }
    }

    var emptySlot = slots.Values.FirstOrDefault(slot => slot.isEmpty);
    if (emptySlot != null)
    {
        Debug.Log($"[InventorySystem] 📦 Found empty slot at ({emptySlot.position.x}, {emptySlot.position.y})");

        ItemData itemCopy = item.CreateExactCopy();

        // 🔒 PRODUCTION SAFETY: Validate copy result
        if (itemCopy.itemIcon == null)
        {
            Debug.LogError($"[InventorySystem] ❌ CRITICAL: CreateExactCopy returned item with NULL itemIcon! Item: {item.itemId}");
        }

        emptySlot.item = itemCopy;
        emptySlot.amount = amount;

        Debug.Log($"[InventorySystem] ✅ Item added to slot ({emptySlot.position.x}, {emptySlot.position.y}), firing events...");

        OnItemAdded?.Invoke(emptySlot);
        OnSlotUpdated?.Invoke(emptySlot);

        Debug.Log($"[InventorySystem] ✅ Events fired: OnItemAdded + OnSlotUpdated");

        // ✅ YENİ: Quest progress güncelle
        if (Object.HasInputAuthority && QuestManager.Instance != null)
        {
            QuestManager.Instance.UpdateQuestProgress("", QuestType.CollectItems, item.itemId, amount);
        }

        if (Object.HasInputAuthority && fromDroppedLoot)
        {
            ChatManager.Instance?.ShowItemPickupMessage(item.itemName);

            // Tüm itemler için notification göster
            if (FragmentNotificationUI.Instance != null)
            {
                FragmentNotificationUI.Instance.ShowFragmentNotification(
                    item.itemName,
                    amount,
                    item.itemIcon
                );
            }
        }

        SaveInventoryState();

        Debug.Log($"[InventorySystem] ✅ TryAddItemLocal COMPLETE SUCCESS - Item {item.itemId} added to inventory");
        return true;
    }
    else
    {
        Debug.LogWarning($"[InventorySystem] ❌ TryAddItemLocal FAILED - No empty slots available! Item: {item.itemId}");
        ChatManager.Instance?.ShowInventoryFullMessage();
        return false;
    }
}
[System.Serializable]
private class SerializableItemStats
{
    public List<ItemStat> stats = new List<ItemStat>();
}
    // Unequip edilen item'ı inventory'ye ekle
    public bool HandleUnequippedItem(ItemData item)
    {
        return TryAddItem(item);
    }

[Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority)]
private void RPC_AddToStack(int x, int y, int amount, bool fromDroppedLoot = false)
{
    Vector2Int position = new Vector2Int(x, y);
    if (slots.TryGetValue(position, out InventorySlot slot) && !slot.isEmpty)
    {
        string itemId = slot.item.itemId;
        string itemName = slot.item.itemName;
        slot.amount += amount;
        OnSlotUpdated?.Invoke(slot);

        if (Object.HasInputAuthority)
        {
            // ✅ YENİ: Quest progress güncelle
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.UpdateQuestProgress("", QuestType.CollectItems, itemId, amount);
            }
            
            if (fromDroppedLoot)
            {
                ChatManager.Instance?.ShowItemPickupMessage(itemName);
            }
            
            RPC_ReportAddItemResult(itemId, true);
            SaveInventoryState();
        }
    }
    else
    {
        if (Object.HasInputAuthority)
        {
            RPC_ReportAddItemResult("unknown", false);
        }
    }
}

[Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority)]
private void RPC_AddItemToSlot(int x, int y, string itemId, int upgradeLevel, int rarityValue, int amount, string statsJson, float armorValue, float attackPower, bool fromDroppedLoot = false)
{
    Vector2Int position = new Vector2Int(x, y);
    if (slots.TryGetValue(position, out InventorySlot slot))
    {
        if (!slot.isEmpty)
        {
            slot.Clear();
            OnSlotUpdated?.Invoke(slot);
        }

        ItemData baseItem = ItemDatabase.Instance.GetItemById(itemId);
        if (baseItem != null)
        {
            ItemData itemCopy = baseItem.CreateExactCopy();
            itemCopy.upgradeLevel = upgradeLevel;
            itemCopy.currentRarity = (GameItemRarity)rarityValue;
            itemCopy.armorValue = armorValue;
            itemCopy.attackPower = attackPower;

            if (!string.IsNullOrEmpty(statsJson))
            {
                try 
                {
                    SerializableItemStats statsData = JsonUtility.FromJson<SerializableItemStats>(statsJson);
                    if (statsData != null && statsData.stats != null)
                    {
                        itemCopy._selectedStats = statsData.stats;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[InventorySystem] Stats yükleme hatası: {e.Message}\nstatsJson: {statsJson}");
                }
            }

            slot.item = itemCopy;
            slot.amount = amount;
            
            OnItemAdded?.Invoke(slot);
            OnSlotUpdated?.Invoke(slot);

            if (Object.HasInputAuthority)
            {
                // ✅ YENİ: Quest progress güncelle
                if (QuestManager.Instance != null)
                {
                    QuestManager.Instance.UpdateQuestProgress("", QuestType.CollectItems, itemId, amount);
                }
                
                if (fromDroppedLoot)
                {
                    ChatManager.Instance?.ShowItemPickupMessage(itemCopy.itemName);
                }
                
                RPC_ReportAddItemResult(itemId, true);
                SaveInventoryState();
            }
        }
        else
        {
            if (Object.HasInputAuthority)
            {
                RPC_ReportAddItemResult(itemId, false);
            }
        }
    }
    else
    {
        if (Object.HasInputAuthority)
        {
            RPC_ReportAddItemResult(itemId, false);
        }
    }
}
public bool RemoveItem(Vector2Int position, int amount = 1)
{
    if (!Object.HasInputAuthority) return false;

    if (slots.TryGetValue(position, out InventorySlot slot))
    {
        if (slot.item != null)
        {
            // Stackable item ise amount kadar azalt
            if (slot.item.isStackable && slot.amount > amount)
            {
                RPC_RemoveItemAmount(position.x, position.y, amount);
                
                if (Object.HasInputAuthority)
                {
                    SaveInventoryState(immediate: true);
                }
                
                return true;
            }
            else
            {
                // Ya stackable değil ya da amount yeterli değil - tüm slotu temizle
                RPC_ClearSlot(position.x, position.y);
                
                if (Object.HasInputAuthority)
                {
                    SaveInventoryState(immediate: true);
                }
                
                return true;
            }
        }
    }

    return false;
}

[Rpc(RpcSources.InputAuthority, RpcTargets.All)]
private void RPC_ClearSlot(int x, int y)
{
    Vector2Int position = new Vector2Int(x, y);

    if (slots.TryGetValue(position, out InventorySlot slot))
    {            
        slot.item = null;
        slot.amount = 0;
        
        OnItemRemoved?.Invoke(slot);
        OnSlotUpdated?.Invoke(slot);

        if (Object.HasInputAuthority)
        {
            SaveInventoryState();
        }
    }
}
public Dictionary<string, object> GetInventoryData()
{
    var data = new Dictionary<string, object>();
    
    foreach (var kvp in slots)
    {
        if (kvp.Value.item != null && !kvp.Value.isEmpty)
        {
            string slotKey = $"slot_{kvp.Key.x}_{kvp.Key.y}";
            
            // Seçilmiş statları kaydet
            List<Dictionary<string, object>> selectedStats = new List<Dictionary<string, object>>();
            foreach (var stat in kvp.Value.item.stats)
            {
                selectedStats.Add(new Dictionary<string, object>
                {
                    { "type", (int)stat.type },
                    { "value", stat.value }
                });
            }
            
            data[slotKey] = new Dictionary<string, object>
            {
                { "itemId", kvp.Value.item.itemId },
                { "amount", kvp.Value.amount },
                { "upgradeLevel", kvp.Value.item.upgradeLevel },
                { "rarity", (int)kvp.Value.item.currentRarity },
                { "armorValue", kvp.Value.item.armorValue },      // YENİ
                { "attackPower", kvp.Value.item.attackPower },    // YENİ
                { "selectedStats", selectedStats },
                { "position", new Dictionary<string, int>
                    {
                        { "x", kvp.Value.position.x },
                        { "y", kvp.Value.position.y }
                    }
                }
            };
        }
    }
    
    return data;
}

public void LoadInventoryData(Dictionary<string, object> inventoryData)
{
    try
    {
        if (ItemDatabase.Instance == null)
        {
            StartCoroutine(RetryLoadInventoryData(inventoryData));
            return;
        }

        if (slots == null || slots.Count == 0)
        {
            InitializeInventory();
        }

        if (inventoryData == null || inventoryData.Count == 0)
        {
            return;
        }

        foreach (var kvp in inventoryData)
        {
            try
            {
                var slotData = kvp.Value as Dictionary<string, object>;
                if (slotData == null) continue;

                if (!slotData.ContainsKey("itemId") || !slotData.ContainsKey("amount"))
                    continue;

                string itemId = slotData["itemId"].ToString();
                int amount = Convert.ToInt32(slotData["amount"]);
                int upgradeLevel = slotData.ContainsKey("upgradeLevel") ? 
                    Convert.ToInt32(slotData["upgradeLevel"]) : 1;
                
                Vector2Int position;
                if (slotData.ContainsKey("position"))
                {
                    var posData = slotData["position"] as Dictionary<string, object>;
                    if (posData != null && posData.ContainsKey("x") && posData.ContainsKey("y"))
                    {
                        position = new Vector2Int(
                            Convert.ToInt32(posData["x"]),
                            Convert.ToInt32(posData["y"])
                        );
                    }
                    else
                    {
                        string[] slotParts = kvp.Key.Replace("slot_", "").Split('_');
                        if (slotParts.Length == 2)
                        {
                            position = new Vector2Int(
                                int.Parse(slotParts[0]),
                                int.Parse(slotParts[1])
                            );
                        }
                        else continue;
                    }
                }
                else continue;

                if (position.x < 0 || position.x >= INVENTORY_COLS ||
                    position.y < 0 || position.y >= INVENTORY_ROWS)
                    continue;

                ItemData baseItem = ItemDatabase.Instance.GetItemById(itemId);

                if (baseItem != null && slots.TryGetValue(position, out InventorySlot slot))
                {
                    ItemData upgradedItem = baseItem.CreateExactCopy();
                    upgradedItem.upgradeLevel = upgradeLevel;

                    GameItemRarity rarity = GameItemRarity.Normal;
                    if (slotData.ContainsKey("rarity"))
                    {
                        rarity = (GameItemRarity)Convert.ToInt32(slotData["rarity"]);
                    }
                    upgradedItem.currentRarity = rarity;
                    
                    // YENİ: Armor ve Attack Power load
                    if (slotData.ContainsKey("armorValue"))
                    {
                        upgradedItem.armorValue = Convert.ToSingle(slotData["armorValue"]);
                    }
                    
                    if (slotData.ContainsKey("attackPower"))
                    {
                        upgradedItem.attackPower = Convert.ToSingle(slotData["attackPower"]);
                    }

                    var selectedStats = new List<ItemStat>();
                    if (slotData.ContainsKey("selectedStats"))
                    {
                        var statsData = slotData["selectedStats"] as List<Dictionary<string, object>>;
                        
                        if (statsData != null)
                        {
                            foreach (var statDict in statsData)
                            {
                                try
                                {
                                    if (statDict != null && statDict.ContainsKey("type") && statDict.ContainsKey("value"))
                                    {
                                        StatType statType = (StatType)Convert.ToInt32(statDict["type"]);
                                        float statValue = Convert.ToSingle(statDict["value"]);
                                        
                                        selectedStats.Add(new ItemStat
                                        {
                                            type = statType,
                                            value = statValue
                                        });
                                    }
                                }
                                catch (Exception e)
                                {
                                    Debug.LogError($"[InventorySystem] Stat dönüştürme hatası: {e.Message}");
                                }
                            }
                        }
                    }

                    upgradedItem.stats = selectedStats;

                    slot.item = upgradedItem;
                    slot.amount = amount;

                    OnItemAdded?.Invoke(slot);
                    OnSlotUpdated?.Invoke(slot);
                }
                else
                {
                    if (baseItem == null)
                        Debug.LogError($"[InventorySystem] Item not found in database: {itemId}");
                    else
                        Debug.LogError($"[InventorySystem] Slot not found at position: {position}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[InventorySystem] Error loading slot {kvp.Key}: {e.Message}\nStack trace: {e.StackTrace}");
            }
        }
    }
    catch (Exception e)
    {
        Debug.LogError($"[InventorySystem] Load error: {e.Message}\n{e.StackTrace}");
    }
}
private System.Collections.IEnumerator RetryLoadInventoryData(Dictionary<string, object> inventoryData)
{
    yield return new WaitForSeconds(1f);
    
    if (ItemDatabase.Instance != null)
    {
        LoadInventoryData(inventoryData);
    }
}

private async void SaveInventoryState(bool immediate = false)
{
    if (playerStats != null)
    {
        try
        {
            var inventoryData = GetInventoryData();
            foreach (var kvp in inventoryData)
            {
                var slotData = kvp.Value as Dictionary<string, object>;


            }

            if (immediate)
            {
                await playerStats.SaveStats();
            }
            else
            {
                // PlayerStats'taki RequestSave metodunu çağır
                var requestSaveMethod = playerStats.GetType().GetMethod("RequestSave", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                requestSaveMethod?.Invoke(playerStats, new object[] { false });
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[InventorySystem] Kaydetme hatası: {e.Message}\n{e.StackTrace}");
        }
    }
}
}