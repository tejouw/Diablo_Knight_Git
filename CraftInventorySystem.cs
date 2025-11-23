// Path: Assets/Game/Scripts/CraftInventorySystem.cs

using UnityEngine;
using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class CraftInventorySlot
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

    public CraftInventorySlot(Vector2Int pos)
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

public class CraftInventorySystem : NetworkBehaviour
{
    public const int CRAFT_INVENTORY_ROWS = 6;
    public const int CRAFT_INVENTORY_COLS = 8;
    public const int MAX_CRAFT_STACK_SIZE = 999;

    private Dictionary<Vector2Int, CraftInventorySlot> craftSlots = new Dictionary<Vector2Int, CraftInventorySlot>();
    
    // Events
    public event Action<CraftInventorySlot> OnCraftItemAdded;
    public event Action<CraftInventorySlot> OnCraftItemRemoved;
    public event Action<CraftInventorySlot> OnCraftSlotUpdated;
    
    private PlayerStats playerStats;

    public bool HasEmptySlot()
    {
        foreach (var slot in craftSlots.Values)
        {
            if (slot.isEmpty)
            {
                return true;
            }
        }
        return false;
    }

    public CraftInventorySlot GetCraftSlot(Vector2Int position)
    {
        if (craftSlots == null)
        {
            return null;
        }

        if (craftSlots.TryGetValue(position, out CraftInventorySlot slot))
        {
            return slot;
        }
        
        return null;
    }

    public Dictionary<Vector2Int, CraftInventorySlot> GetAllCraftSlots()
    {
        return craftSlots;
    }

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogError("[CraftInventorySystem] PlayerStats component not found!");
            return;
        }

        InitializeCraftInventory();
    }

    private void InitializeCraftInventory()
    {
        if (craftSlots != null && craftSlots.Count > 0)
        {
            return;
        }
        
        if (craftSlots == null)
        {
            craftSlots = new Dictionary<Vector2Int, CraftInventorySlot>();
        }
        
        for (int y = 0; y < CRAFT_INVENTORY_ROWS; y++)
        {
            for (int x = 0; x < CRAFT_INVENTORY_COLS; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                craftSlots[position] = new CraftInventorySlot(position);
            }
        }
    }

public bool TryAddCraftItem(ItemData item, int amount = 1, bool fromDroppedLoot = false)
{
    if (item == null)
    {
        Debug.LogError("[CraftInventorySystem] TryAddCraftItem called with null item");
        return false;
    }


    // Sadece craft itemları kabul et
    if (!item.IsCraftItem())
    {
        return false;
    }

    bool isServerLootOperation = Object.HasStateAuthority && !Object.HasInputAuthority;

    if (isServerLootOperation)
    {
        RPC_RequestAddCraftItemToClient(
            item.itemId, 
            item.upgradeLevel, 
            (int)item.currentRarity,
            amount, 
            JsonUtility.ToJson(new SerializableItemStats { stats = item.stats }),
            fromDroppedLoot
        );
        return true;
    }

    if (!Object.HasInputAuthority)
    {
        return false;
    }


    // Stack'leme öncelikli - aynı item varsa stack'le
    if (item.isStackable)
    {
        var existingSlot = craftSlots.Values.FirstOrDefault(slot =>
            !slot.isEmpty && slot.item != null &&
            slot.item.itemId == item.itemId &&
            slot.item.upgradeLevel == item.upgradeLevel &&
            slot.amount < MAX_CRAFT_STACK_SIZE);

        if (existingSlot != null)
        {
            RPC_AddCraftToStack(existingSlot.position.x, existingSlot.position.y, amount, fromDroppedLoot);
            return true;
        }
    }

    // Boş slot'a ekle
    var emptySlot = craftSlots.Values.FirstOrDefault(slot => slot.isEmpty);
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

        RPC_AddCraftItemToSlot(emptySlot.position.x, emptySlot.position.y,
            item.itemId, item.upgradeLevel, (int)item.currentRarity, amount, statsJson, fromDroppedLoot);

        return true;
    }
    else
    {
        ChatManager.Instance?.ShowCraftInventoryFullMessage();
        return false;
    }
}
// TryAddCraftItem metodundan sonra ekle
public bool AddFragmentDirectly(ItemData fragmentItem, int amount)
{
    if (fragmentItem == null || !fragmentItem.IsFragment())
    {
        Debug.LogError("[CraftInventorySystem] AddFragmentDirectly called with invalid fragment");
        return false;
    }
    
    
    // Stack'lenebilir mi kontrol et
    if (fragmentItem.isStackable)
    {
        var existingSlot = craftSlots.Values.FirstOrDefault(slot =>
            !slot.isEmpty && slot.item != null &&
            slot.item.itemId == fragmentItem.itemId &&
            slot.amount < MAX_CRAFT_STACK_SIZE);

        if (existingSlot != null)
        {
            int newAmount = Mathf.Min(existingSlot.amount + amount, MAX_CRAFT_STACK_SIZE);
            existingSlot.amount = newAmount;
            OnCraftSlotUpdated?.Invoke(existingSlot);

if (Object.HasInputAuthority)
{
    if (QuestManager.Instance != null)
    {
        QuestManager.Instance.UpdateQuestProgress("", QuestType.CollectItems, fragmentItem.itemId, amount);
    }

    
    if (FragmentNotificationUI.Instance != null)
    {
        FragmentNotificationUI.Instance.ShowFragmentNotification(
            fragmentItem._baseItemName, 
            amount, 
            fragmentItem.itemIcon
        );
    }
    else
    {
    }

    SaveCraftInventoryState();
}

            return true;
        }
    }

    // Boş slot bul
    var emptySlot = craftSlots.Values.FirstOrDefault(slot => slot.isEmpty);
    if (emptySlot != null)
    {
        
        ItemData fragmentCopy = fragmentItem.CreateExactCopy();
        emptySlot.item = fragmentCopy;
        emptySlot.amount = amount;

        OnCraftItemAdded?.Invoke(emptySlot);
        OnCraftSlotUpdated?.Invoke(emptySlot);

if (Object.HasInputAuthority)
{
    if (QuestManager.Instance != null)
    {
        QuestManager.Instance.UpdateQuestProgress("", QuestType.CollectItems, fragmentItem.itemId, amount);
    }

    
    if (FragmentNotificationUI.Instance != null)
    {
        FragmentNotificationUI.Instance.ShowFragmentNotification(
            fragmentItem._baseItemName, 
            amount, 
            fragmentItem.itemIcon
        );
    }
    else
    {
    }

    SaveCraftInventoryState();
}

        return true;
    }
    else
    {
        ChatManager.Instance?.ShowCraftInventoryFullMessage();
        return false;
    }
}
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_RequestAddCraftItemToClient(string itemId, int upgradeLevel, int rarityValue, int amount, string statsJson, bool fromDroppedLoot = false)
    {
        ItemData baseItem = ItemDatabase.Instance.GetItemById(itemId);
        if (baseItem != null)
        {
            ItemData itemCopy = baseItem.CreateExactCopy();
            itemCopy.upgradeLevel = upgradeLevel;
            itemCopy.currentRarity = (GameItemRarity)rarityValue;

            if (!string.IsNullOrEmpty(statsJson))
            {
                try 
                {
                    SerializableItemStats statsData = JsonUtility.FromJson<SerializableItemStats>(statsJson);
                    if (statsData != null && statsData.stats != null)
                    {
                        itemCopy.stats = statsData.stats;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[CraftInventorySystem] Stats parsing error: {e.Message}");
                }
            }

            bool success = TryAddCraftItemLocal(itemCopy, amount, fromDroppedLoot);
            RPC_ReportAddCraftItemResult(itemId, success);
        }
        else
        {
            Debug.LogError($"[CraftInventorySystem] Item not found: {itemId}");
            RPC_ReportAddCraftItemResult(itemId, false);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_ReportAddCraftItemResult(string itemId, bool success)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }
        
        OnCraftItemAddResult?.Invoke(itemId, success);
    }

    public static event System.Action<string, bool> OnCraftItemAddResult;

private bool TryAddCraftItemLocal(ItemData item, int amount = 1, bool fromDroppedLoot = false)
{
    if (item == null) return false;

    if (item.isStackable)
    {
        var existingSlot = craftSlots.Values.FirstOrDefault(slot =>
            !slot.isEmpty && slot.item != null &&
            slot.item.itemId == item.itemId &&
            slot.item.upgradeLevel == item.upgradeLevel &&
            slot.amount < MAX_CRAFT_STACK_SIZE);

        if (existingSlot != null)
        {
            int newAmount = Mathf.Min(existingSlot.amount + amount, MAX_CRAFT_STACK_SIZE);
            existingSlot.amount = newAmount;
            OnCraftSlotUpdated?.Invoke(existingSlot);

            if (Object.HasInputAuthority)
            {
                if (QuestManager.Instance != null)
                {
                    QuestManager.Instance.UpdateQuestProgress("", QuestType.CollectItems, item.itemId, amount);
                }

if (fromDroppedLoot)
{
    // Tüm craft itemler için notification göster
    if (FragmentNotificationUI.Instance != null)
    {
        FragmentNotificationUI.Instance.ShowFragmentNotification(
            item._baseItemName,
            amount,
            item.itemIcon
        );
    }
}
            }

            SaveCraftInventoryState();
            return true;
        }
    }

    var emptySlot = craftSlots.Values.FirstOrDefault(slot => slot.isEmpty);
    if (emptySlot != null)
    {
        ItemData itemCopy = item.CreateExactCopy();
        emptySlot.item = itemCopy;
        emptySlot.amount = amount;

        OnCraftItemAdded?.Invoke(emptySlot);
        OnCraftSlotUpdated?.Invoke(emptySlot);

        if (Object.HasInputAuthority)
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.UpdateQuestProgress("", QuestType.CollectItems, item.itemId, amount);
            }

            if (fromDroppedLoot)
            {
                // YENİ - Fragment için özel mesaj
                if (item.IsFragment())
                {
                    //ChatManager.Instance?.ShowFragmentPickupMessage(item.itemName, amount);
                }
                else
                {
                    //ChatManager.Instance?.ShowCraftMaterialPickupMessage(item.itemName, amount);
                }
            }
        }

        SaveCraftInventoryState();
        return true;
    }
    else
    {
        ChatManager.Instance?.ShowCraftInventoryFullMessage();
        return false;
    }
}
// CraftInventorySystem.cs'e eklenecek metodlar

public bool HasItem(string itemId, int requiredAmount)
{
    int totalAmount = 0;
    
    foreach (var slot in craftSlots.Values)
    {
        if (!slot.isEmpty && slot.item != null && slot.item.itemId == itemId)
        {
            totalAmount += slot.amount;
        }
    }
    
    return totalAmount >= requiredAmount;
}

public bool ConsumeItems(string itemId, int amount)
{
    if (!Object.HasInputAuthority)
    {
        return false;
    }

    if (!HasItem(itemId, amount))
    {
        return false;
    }

    int remainingToConsume = amount;

    foreach (var slot in craftSlots.Values)
    {
        if (remainingToConsume <= 0) break;

        if (!slot.isEmpty && slot.item != null && slot.item.itemId == itemId)
        {
            int consumeFromThisSlot = Mathf.Min(remainingToConsume, slot.amount);

            if (slot.amount <= consumeFromThisSlot)
            {
                // Slotu tamamen boşalt - RPC yerine doğrudan değiştir
                slot.item = null;
                slot.amount = 0;
                OnCraftItemRemoved?.Invoke(slot);
                OnCraftSlotUpdated?.Invoke(slot);
            }
            else
            {
                // Sadece miktarı azalt - RPC yerine doğrudan değiştir
                slot.amount -= consumeFromThisSlot;
                OnCraftSlotUpdated?.Invoke(slot);
            }

            remainingToConsume -= consumeFromThisSlot;
        }
    }

    SaveCraftInventoryState();
    return remainingToConsume == 0;
}
    [Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority)]
    private void RPC_AddCraftToStack(int x, int y, int amount, bool fromDroppedLoot = false)
    {
        Vector2Int position = new Vector2Int(x, y);
        if (craftSlots.TryGetValue(position, out CraftInventorySlot slot) && !slot.isEmpty)
        {
            string itemName = slot.item.itemName;
            string itemId = slot.item.itemId;
            int newAmount = Mathf.Min(slot.amount + amount, MAX_CRAFT_STACK_SIZE);
            slot.amount = newAmount;
            OnCraftSlotUpdated?.Invoke(slot);

            if (Object.HasInputAuthority)
            {
                // Quest progress güncelle
                if (QuestManager.Instance != null)
                {
                    QuestManager.Instance.UpdateQuestProgress("", QuestType.CollectItems, itemId, amount);
                }

                if (fromDroppedLoot)
                {
                    ChatManager.Instance?.ShowCraftMaterialPickupMessage(itemName, amount);
                }

                RPC_ReportAddCraftItemResult(itemId, true);
                SaveCraftInventoryState();
            }
        }
        else
        {
            Debug.LogError($"[CraftInventorySystem] Stack operation failed - slot empty or not found at {position}");
            
            if (Object.HasInputAuthority)
            {
                RPC_ReportAddCraftItemResult("unknown", false);
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority)]
    private void RPC_AddCraftItemToSlot(int x, int y, string itemId, int upgradeLevel, int rarityValue, int amount, string statsJson, bool fromDroppedLoot = false)
    {
        Vector2Int position = new Vector2Int(x, y);
        if (craftSlots.TryGetValue(position, out CraftInventorySlot slot))
        {
            if (!slot.isEmpty)
            {
                slot.Clear();
                OnCraftSlotUpdated?.Invoke(slot);
            }

            ItemData baseItem = ItemDatabase.Instance.GetItemById(itemId);
            if (baseItem != null)
            {
                ItemData itemCopy = baseItem.CreateExactCopy();
                itemCopy.upgradeLevel = upgradeLevel;
                itemCopy.currentRarity = (GameItemRarity)rarityValue;

                if (!string.IsNullOrEmpty(statsJson))
                {
                    try 
                    {
                        SerializableItemStats statsData = JsonUtility.FromJson<SerializableItemStats>(statsJson);
                        if (statsData != null && statsData.stats != null)
                        {
                            itemCopy.stats = statsData.stats;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[CraftInventorySystem] Stats loading error: {e.Message}");
                    }
                }

                slot.item = itemCopy;
                slot.amount = amount;

                OnCraftItemAdded?.Invoke(slot);
                OnCraftSlotUpdated?.Invoke(slot);

                if (Object.HasInputAuthority)
                {
                    // Quest progress güncelle
                    if (QuestManager.Instance != null)
                    {
                        QuestManager.Instance.UpdateQuestProgress("", QuestType.CollectItems, itemId, amount);
                    }

                    if (fromDroppedLoot)
                    {
                        ChatManager.Instance?.ShowCraftMaterialPickupMessage(itemCopy.itemName, amount);
                    }

                    RPC_ReportAddCraftItemResult(itemId, true);
                    SaveCraftInventoryState();
                }
            }
            else
            {
                Debug.LogError($"[CraftInventorySystem] Item not found in database: {itemId}");
                
                if (Object.HasInputAuthority)
                {
                    RPC_ReportAddCraftItemResult(itemId, false);
                }
            }
        }
        else
        {
            Debug.LogError($"[CraftInventorySystem] Slot not found at position: {position}");
            
            if (Object.HasInputAuthority)
            {
                RPC_ReportAddCraftItemResult(itemId, false);
            }
        }
    }

    public bool RemoveCraftItem(Vector2Int position, int amount = 1)
    {
        if (!Object.HasInputAuthority) return false;

        if (craftSlots.TryGetValue(position, out CraftInventorySlot slot))
        {
            if (slot.item != null)
            {
                if (slot.amount <= amount)
                {
                    RPC_ClearCraftSlot(position.x, position.y);
                }
                else
                {
                    RPC_ReduceCraftAmount(position.x, position.y, amount);
                }
                
                if (Object.HasInputAuthority)
                {
                    SaveCraftInventoryState();
                }
                
                return true;
            }
        }

        return false;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority)]
    private void RPC_ClearCraftSlot(int x, int y)
    {
        Vector2Int position = new Vector2Int(x, y);

        if (craftSlots.TryGetValue(position, out CraftInventorySlot slot))
        {
            slot.item = null;
            slot.amount = 0;

            OnCraftItemRemoved?.Invoke(slot);
            OnCraftSlotUpdated?.Invoke(slot);

            if (Object.HasInputAuthority)
            {
                SaveCraftInventoryState();
            }
        }
        else
        {
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReduceCraftAmount(int x, int y, int amount)
    {
        Vector2Int position = new Vector2Int(x, y);

        if (craftSlots.TryGetValue(position, out CraftInventorySlot slot))
        {
            slot.amount -= amount;
            if (slot.amount <= 0)
            {
                slot.item = null;
                slot.amount = 0;
                OnCraftItemRemoved?.Invoke(slot);
            }

            OnCraftSlotUpdated?.Invoke(slot);

            if (Object.HasInputAuthority)
            {
                SaveCraftInventoryState();
            }
        }
    }

    // Firebase save/load
    public Dictionary<string, object> GetCraftInventoryData()
    {
        var data = new Dictionary<string, object>();
        
        foreach (var kvp in craftSlots)
        {
            if (kvp.Value.item != null && !kvp.Value.isEmpty)
            {
                string slotKey = $"craft_slot_{kvp.Key.x}_{kvp.Key.y}";
                
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

    public void LoadCraftInventoryData(Dictionary<string, object> inventoryData)
    {
        try
        {
            if (ItemDatabase.Instance == null)
            {
                Debug.LogError("[CraftInventorySystem] ItemDatabase.Instance is null! Retrying in 1 second...");
                StartCoroutine(RetryLoadCraftInventoryData(inventoryData));
                return;
            }

            if (craftSlots == null || craftSlots.Count == 0)
            {
                InitializeCraftInventory();
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

                    if (!slotData.ContainsKey("itemId") || !slotData.ContainsKey("amount")) continue;

                    string itemId = slotData["itemId"].ToString();
                    int amount = Convert.ToInt32(slotData["amount"]);
                    int upgradeLevel = slotData.ContainsKey("upgradeLevel") ? Convert.ToInt32(slotData["upgradeLevel"]) : 1;

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
                            string[] slotParts = kvp.Key.Replace("craft_slot_", "").Split('_');
                            if (slotParts.Length == 2)
                            {
                                position = new Vector2Int(
                                    int.Parse(slotParts[0]),
                                    int.Parse(slotParts[1])
                                );
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                    else
                    {
                        continue;
                    }

                    if (position.x < 0 || position.x >= CRAFT_INVENTORY_COLS ||
                        position.y < 0 || position.y >= CRAFT_INVENTORY_ROWS)
                    {
                        continue;
                    }

                    ItemData baseItem = ItemDatabase.Instance.GetItemById(itemId);

                    if (baseItem != null && craftSlots.TryGetValue(position, out CraftInventorySlot slot))
                    {
                        ItemData upgradedItem = baseItem.CreateExactCopy();
                        upgradedItem.upgradeLevel = upgradeLevel;

                        GameItemRarity rarity = GameItemRarity.Normal;
                        if (slotData.ContainsKey("rarity"))
                        {
                            rarity = (GameItemRarity)Convert.ToInt32(slotData["rarity"]);
                        }
                        upgradedItem.currentRarity = rarity;

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
                                        Debug.LogError($"[CraftInventorySystem] Stat conversion error: {e.Message}");
                                    }
                                }
                            }
                        }

                        upgradedItem.stats = selectedStats;
                        upgradedItem.SetupAsCraftItem();

                        slot.item = upgradedItem;
                        slot.amount = amount;

                        OnCraftItemAdded?.Invoke(slot);
                        OnCraftSlotUpdated?.Invoke(slot);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CraftInventorySystem] Error loading slot {kvp.Key}: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CraftInventorySystem] Load error: {e.Message}");
        }
    }

    private System.Collections.IEnumerator RetryLoadCraftInventoryData(Dictionary<string, object> inventoryData)
    {
        yield return new WaitForSeconds(1f);
        
        if (ItemDatabase.Instance != null)
        {
            LoadCraftInventoryData(inventoryData);
        }
        else
        {
            Debug.LogError("[CraftInventorySystem] ItemDatabase still null after retry, craft inventory data lost!");
        }
    }

    private async void SaveCraftInventoryState()
    {
        if (playerStats != null)
        {
            try
            {
                await playerStats.SaveStats();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CraftInventorySystem] Save error: {e.Message}");
            }
        }
    }

    [System.Serializable]
    private class SerializableItemStats
    {
        public List<ItemStat> stats = new List<ItemStat>();
    }
}