// Path: Assets/Game/Scripts/DroppedLoot.cs

using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering.Universal;
public class DroppedLoot : NetworkBehaviour
{
    #region NETWORK PROPERTIES
    [Networked] public int NetworkOwnerActorNumber { get; set; }
    [Networked] public int NetworkCoinAmount { get; set; }
    [Networked] public int NetworkPotionAmount { get; set; }
    [Networked] public bool NetworkIsBeingDestroyed { get; set; }
    [Networked] public TickTimer NetworkCreationTime { get; set; }
    [Networked, Capacity(8)] public NetworkArray<PlayerRef> AuthorizedPlayers => default;
    #endregion

    #region CORE DATA
    [Header("Drop Settings")]
    [SerializeField] private float destroyTime = 60f;
    [SerializeField] private float potionCollectionRadius = 3f;
    
    // Core State
    private int ownerActorNumber = -1;
    private int coinAmount;
    private int potionAmount;
    private List<ItemData> droppedItems = new List<ItemData>();
    private bool isBeingDestroyed = false;
    private bool hasTriggeredDestroy = false;
    
    // Component References
    private CombatInitializer combatInitializer;
    private DroppedLootUI lootUI;
    #endregion
    #region SONRADAN EKLENENLER
public void InitializeDroppedLootWithRecipients(PlayerRef[] recipients, int coins, int potions,
    string[] itemIds, int[] upgradeLevels, int[] rarities, string[] statsJsons,
    float[] armorValues, float[] attackPowers, int[] itemLevels, int[] effectiveLevels) // YENİ parametreler
{
    if (!Object.HasStateAuthority) 
    {
        Debug.LogError($"[DroppedLoot-{Object.Id}] InitializeDroppedLootWithRecipients called without StateAuthority!");
        return;
    }

    for (int i = 0; i < recipients.Length && i < AuthorizedPlayers.Length; i++)
    {
        AuthorizedPlayers.Set(i, recipients[i]);
    }

    NetworkOwnerActorNumber = recipients.Length > 0 ? recipients[0].PlayerId : -1;
    NetworkCoinAmount = coins;
    NetworkPotionAmount = potions;

    SyncNetworkToLocal();
    
    LoadItemsWithDetails(itemIds, upgradeLevels, rarities, statsJsons, armorValues, attackPowers, itemLevels, effectiveLevels); // YENİ parametreler

    if (lootUI != null)
    {
        lootUI.UpdateVisuals(coinAmount, potionAmount, droppedItems);
    }

    StartCoroutine(SendRPCWithDelay(recipients, coins, potions, itemIds, upgradeLevels, rarities, statsJsons, armorValues, attackPowers, itemLevels, effectiveLevels)); // YENİ parametreler
}

private IEnumerator SendRPCWithDelay(PlayerRef[] recipients, int coins, int potions, 
    string[] itemIds, int[] upgradeLevels, int[] rarities, string[] statsJsons,
    float[] armorValues, float[] attackPowers, int[] itemLevels, int[] effectiveLevels) // YENİ parametreler
{
    yield return new WaitForSeconds(0.1f);
    
    InitializeDroppedLootWithRecipientsRPC(recipients, coins, potions, itemIds,
        upgradeLevels, rarities, statsJsons, armorValues, attackPowers, itemLevels, effectiveLevels); // YENİ parametreler
}
// SetAuthorizedPlayersOnSpawn metodunu güncelle
public void SetAuthorizedPlayersOnSpawn(PlayerRef[] recipients)
{
    if (!Object.HasStateAuthority) 
    {
        Debug.LogError($"[DroppedLoot-{Object.Id}] SetAuthorizedPlayersOnSpawn called without StateAuthority!");
        return;
    }
    
    
    // Spawn anında authorized players'ı ayarla
    for (int i = 0; i < recipients.Length && i < AuthorizedPlayers.Length; i++)
    {
        AuthorizedPlayers.Set(i, recipients[i]);
    }
    
    // Interest ayarını hemen yap
    SetLootInterest(recipients);
}

private void SetLootInterest(PlayerRef[] recipients)
{
    if (!Object.HasStateAuthority) return;
    
    
    // Sadece authorized player'lar için interest set et
    foreach (var recipient in recipients)
    {
        if (recipient != PlayerRef.None)
        {
            Object.SetPlayerAlwaysInterested(recipient, true);
        }
    }
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void InitializeDroppedLootWithRecipientsRPC(PlayerRef[] recipients, int coins, int potions, 
    string[] itemIds, int[] upgradeLevels, int[] rarities, string[] statsJsons,
    float[] armorValues, float[] attackPowers, int[] itemLevels, int[] effectiveLevels) // YENİ parametreler
{
    bool isAuthorized = Object.HasStateAuthority;
    
    if (!isAuthorized)
    {
        foreach (var recipient in recipients)
        {
            if (recipient == Runner.LocalPlayer)
            {
                isAuthorized = true;
                break;
            }
        }
    }
    
    if (!isAuthorized) return;

    for (int i = 0; i < recipients.Length && i < AuthorizedPlayers.Length; i++)
    {
        AuthorizedPlayers.Set(i, recipients[i]);
    }

    ownerActorNumber = recipients.Length > 0 ? recipients[0].PlayerId : -1;
    coinAmount = coins;
    potionAmount = potions;

    if (Object.HasStateAuthority)
    {
        NetworkOwnerActorNumber = ownerActorNumber;
        NetworkCoinAmount = coins;
        NetworkPotionAmount = potions;
    }

    LoadItemsWithDetails(itemIds, upgradeLevels, rarities, statsJsons, armorValues, attackPowers, itemLevels, effectiveLevels); // YENİ parametreler
    
    if (lootUI != null)
    {
        lootUI.UpdateVisuals(coinAmount, potionAmount, droppedItems);
    }
}

   #endregion
    #region INITIALIZATION
public override void Spawned()
{
    
    lootUI = GetComponent<DroppedLootUI>();
    if (lootUI == null)
    {
        lootUI = gameObject.AddComponent<DroppedLootUI>();
    }
    else
    {
    }

    // Light2D component'ini runtime'da ekle
    Light2D light = GetComponent<Light2D>();
    if (light == null)
    {
        light = gameObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.intensity = 1f;
        light.pointLightOuterRadius = 1f;
        light.color = Color.white;
        light.enabled = true;
    }

    if (Object.HasStateAuthority)
    {
        NetworkCreationTime = TickTimer.CreateFromSeconds(Runner, destroyTime);
    }

    SyncNetworkToLocal();
    
    lootUI.Initialize();
    
    StartCoroutine(FindCombatInitializerWithRetry());
}

private IEnumerator DelayedLightRegistration(InverseLightComponent lightComponent)
{
    yield return new WaitForSeconds(0.5f);
    lightComponent.RegisterToController();
}

    private IEnumerator FindCombatInitializerWithRetry()
    {
        int attempts = 0;
        while (combatInitializer == null && attempts < 10)
        {
            combatInitializer = FindFirstObjectByType<CombatInitializer>();
            if (combatInitializer == null)
            {
                yield return new WaitForSeconds(0.5f);
                attempts++;
            }
            else
            {
                break;
            }
        }
    }

public void InitializeDroppedLoot(int ownerActor, int coins, int potions, string[] itemIds)
{
    if (!Object.HasStateAuthority) return;

    NetworkOwnerActorNumber = ownerActor;
    NetworkCoinAmount = coins;
    NetworkPotionAmount = potions;

    SyncNetworkToLocal();
    
    int[] emptyUpgrades = new int[itemIds.Length];
    int[] emptyRarities = new int[itemIds.Length];
    string[] emptyStats = new string[itemIds.Length];
    float[] emptyArmor = new float[itemIds.Length];
    float[] emptyAttack = new float[itemIds.Length];
    int[] emptyItemLevels = new int[itemIds.Length]; // YENİ
    int[] emptyEffectiveLevels = new int[itemIds.Length]; // YENİ
    
    for (int i = 0; i < itemIds.Length; i++)
    {
        emptyItemLevels[i] = 1;
        emptyEffectiveLevels[i] = 1;
    }
    
    LoadItemsWithDetails(itemIds, emptyUpgrades, emptyRarities, emptyStats, emptyArmor, emptyAttack, emptyItemLevels, emptyEffectiveLevels);
    
    InitializeDroppedLootRPC(ownerActor, coins, potions, itemIds, 
        emptyUpgrades, emptyRarities, emptyStats);
}
    #endregion

    #region ITEM MANAGEMENT
    private void LoadItems(string[] itemIds)
    {
        droppedItems.Clear();
        foreach (string itemId in itemIds)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                ItemData item = ItemDatabase.Instance?.GetItemById(itemId);
                if (item != null)
                {
                    droppedItems.Add(item.CreateCopy());
                }
            }
        }
        
        lootUI?.UpdateVisuals(coinAmount, potionAmount, droppedItems);
    }

    public bool HasItems()
    {
        return coinAmount > 0 || potionAmount > 0 || 
               (droppedItems != null && droppedItems.Any(item => item != null));
    }

    public List<ItemData> GetDroppedItems()
    {
        return droppedItems;
    }

    public int GetCoinAmount()
    {
        return coinAmount;
    }
    #endregion

    #region COLLECTION LOGIC
// Bu değişkeni DroppedLoot.cs'e ekle
private bool isCollecting = false;

public void CollectItems()
{
    
    if (!CanCollectLoot() || isCollecting)
    {
        return;
    }
    
    isCollecting = true;

    GameObject localPlayer = FusionUtilities.FindLocalPlayerGameObject();
    if (localPlayer == null) 
    {
        Debug.LogError("[DroppedLoot] Local player bulunamadı");
        isCollecting = false;
        return;
    }

    NetworkObject netObj = localPlayer.GetComponent<NetworkObject>();
    if (netObj == null || !netObj.HasInputAuthority) 
    {
        Debug.LogError("[DroppedLoot] NetworkObject null veya InputAuthority yok");
        isCollecting = false;
        return;
    }

    try
    {
        // Coin collection
        if (coinAmount > 0)
        {
            OnCoinCollected(netObj);
        }

        InventorySystem inventorySystem = localPlayer.GetComponent<InventorySystem>();
        if (inventorySystem != null)
        {
            
            foreach (var item in droppedItems.ToList())
            {
                if (item != null)
                {
                    
                    bool isEquipment = item.IsArmorItem() || item.IsWeaponItem();
                    
                    if (isEquipment && QuestManager.Instance != null)
                    {
                        QuestManager.Instance.UpdateQuestProgress(
                            "", 
                            QuestType.PickupEquipment, 
                            item.itemId
                        );
                    }
                    else if (isEquipment && QuestManager.Instance == null)
                    {
                        Debug.LogError("[DroppedLoot] QuestManager.Instance NULL!");
                    }
                    
                    inventorySystem.RequestLootCollection(this, item.itemId);
                }
            }
        }
    }
    finally
    {
        isCollecting = false;
    }
}

    public void OnCoinCollected(NetworkObject playerNetworkObject)
    {
        if (playerNetworkObject == null || !CanCollectLoot() || coinAmount <= 0) return;

        PlayerStats playerStats = playerNetworkObject.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            // Coin effect
            if (CoinEffectManager.Instance != null)
            {
                CoinEffectManager.Instance.PlayCoinEffect(transform.position, coinAmount);
            }

            if (Object.HasStateAuthority)
            {
                RequestCoinCollectionRPC(playerNetworkObject.InputAuthority);
            }
        }
    }

    public void OnPotionCollected(NetworkObject playerNetworkObject)
    {
        if (playerNetworkObject == null || !CanCollectLoot() || potionAmount <= 0) return;

        PlayerStats playerStats = playerNetworkObject.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.AddPotion(potionAmount);
            RequestPotionCollectionRPC(playerNetworkObject.InputAuthority);
        }
    }

private bool CanCollectLoot()
{
    if (Runner == null || !Runner.IsRunning) return false;
    
    // Check if local player is in authorized list
    for (int i = 0; i < AuthorizedPlayers.Length; i++)
    {
        if (AuthorizedPlayers[i] == Runner.LocalPlayer)
            return true;
    }
    
    return false;
}
    #endregion

    #region NETWORK SYNC
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void InitializeDroppedLootRPC(int ownerActor, int coins, int potions, 
    string[] itemIds, int[] upgradeLevels, int[] rarities, string[] statsJsons)
{
    ownerActorNumber = ownerActor;
    coinAmount = coins;
    potionAmount = potions;

    if (Object.HasStateAuthority)
    {
        NetworkOwnerActorNumber = ownerActor;
        NetworkCoinAmount = coins;
        NetworkPotionAmount = potions;
    }

    float[] emptyArmorValues = new float[itemIds.Length];
    float[] emptyAttackPowers = new float[itemIds.Length];
    int[] emptyItemLevels = new int[itemIds.Length]; // YENİ
    int[] emptyEffectiveLevels = new int[itemIds.Length]; // YENİ
    
    for (int i = 0; i < itemIds.Length; i++)
    {
        emptyItemLevels[i] = 1;
        emptyEffectiveLevels[i] = 1;
    }
    
    LoadItemsWithDetails(itemIds, upgradeLevels, rarities, statsJsons, emptyArmorValues, emptyAttackPowers, emptyItemLevels, emptyEffectiveLevels);
    lootUI?.UpdateVisuals(coinAmount, potionAmount, droppedItems);
}

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RequestCoinCollectionRPC(PlayerRef collectorPlayer)
    {
        if (!Object.HasStateAuthority || coinAmount <= 0) return;

        NetworkCoinAmount = 0;
        SyncCoinAmountRPC(0);
        CheckForDestroy();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RequestPotionCollectionRPC(PlayerRef collectorPlayer)
    {
        if (!Object.HasStateAuthority || potionAmount <= 0) return;

        NetworkPotionAmount = 0;
        SyncPotionAmountRPC(0);
        CheckForDestroy();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SyncCoinAmountRPC(int newAmount)
    {
        coinAmount = newAmount;
        lootUI?.UpdateVisuals(coinAmount, potionAmount, droppedItems);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SyncPotionAmountRPC(int newAmount)
    {
        potionAmount = newAmount;
        lootUI?.UpdateVisuals(coinAmount, potionAmount, droppedItems);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_RemoveCollectedItem(string itemId)
    {
        RemoveItemFromList(itemId);
        CheckForDestroy();
    }

    private void SyncNetworkToLocal()
    {
        ownerActorNumber = NetworkOwnerActorNumber;
        coinAmount = NetworkCoinAmount;
        potionAmount = NetworkPotionAmount;
    }
    #endregion

    #region ITEM HELPERS
private void LoadItemsWithDetails(string[] itemIds, int[] upgradeLevels, 
    int[] rarities, string[] statsJsons, float[] armorValues, float[] attackPowers,
    int[] itemLevels, int[] effectiveLevels) // YENİ parametreler
{
    droppedItems.Clear();
    
    for (int i = 0; i < itemIds.Length; i++)
    {
        if (string.IsNullOrEmpty(itemIds[i])) continue;

        ItemData item = ItemDatabase.Instance?.GetItemById(itemIds[i]);
        if (item != null)
        {
            ItemData droppedItem = item.CreateExactCopy();
            
            if (i < upgradeLevels.Length)
                droppedItem.upgradeLevel = upgradeLevels[i];
            
            if (i < rarities.Length)
                droppedItem.currentRarity = (GameItemRarity)rarities[i];
            
            // YENİ - itemLevel ve effectiveLevel set et
            if (i < itemLevels.Length)
                droppedItem.itemLevel = itemLevels[i];
            
            if (i < effectiveLevels.Length)
                droppedItem.effectiveLevel = effectiveLevels[i];
            
            if (i < armorValues.Length)
            {
                droppedItem.armorValue = armorValues[i];
            }
            
            if (i < attackPowers.Length)
            {
                droppedItem.attackPower = attackPowers[i];
            }
            
            if (i < statsJsons.Length && !string.IsNullOrEmpty(statsJsons[i]))
            {
                LoadItemStats(droppedItem, statsJsons[i]);
            }
            
            droppedItems.Add(droppedItem);
        }
    }
}

    private void LoadItemStats(ItemData item, string statsJson)
    {
        try
        {
            var statsData = JsonUtility.FromJson<SerializableItemStats>(statsJson);
            if (statsData?.stats != null)
            {
                item.stats = statsData.stats;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DroppedLoot] Stats parsing error: {e.Message}");
        }
    }

    private void RemoveItemFromList(string itemId)
    {
        droppedItems.RemoveAll(x => x != null && x.itemId == itemId);
        lootUI?.UpdateVisuals(coinAmount, potionAmount, droppedItems);
    }

    [System.Serializable]
    private class SerializableItemStats
    {
        public List<ItemStat> stats = new List<ItemStat>();
    }
    #endregion

    #region LIFECYCLE MANAGEMENT
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || isBeingDestroyed) return;

        // Timeout check
        if (NetworkCreationTime.Expired(Runner))
        {
            StartDestroySequence();
            return;
        }

        // Sync network to local
        SyncNetworkToLocal();
    }

private void CheckForDestroy()
{
    if (!Object.HasStateAuthority || isBeingDestroyed) return;
    
    bool shouldDestroy = coinAmount <= 0 && potionAmount <= 0 && droppedItems.Count == 0;
    
    if (shouldDestroy)
    {
        StartDestroySequence();
    }
}

    private void StartDestroySequence()
    {
        if (hasTriggeredDestroy) return;
        
        hasTriggeredDestroy = true;
        NetworkIsBeingDestroyed = true;
        
        CleanupReferences();
        
        if (Object.HasStateAuthority)
        {
            Runner.Despawn(Object);
        }
    }

    private void CleanupReferences()
    {
        combatInitializer?.RemoveNearbyItem(this);
        
        var collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        CleanupReferences();
        lootUI?.Cleanup();
    }

    private void OnDestroy()
    {
        CleanupReferences();
        lootUI?.Cleanup();
    }
    #endregion

    #region TRIGGER HANDLING
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isBeingDestroyed) return;

        GameObject playerObject = GetPlayerObject(other);
        if (playerObject == null) return;

        NetworkObject netObj = playerObject.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsOwnedByLocalPlayer()) return;

        HandlePlayerInteraction(playerObject, netObj);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (isBeingDestroyed || Runner == null || !Runner.IsRunning) return;
        
        GameObject playerObject = GetPlayerObject(other);
        if (playerObject == null) return;

        NetworkObject netObj = playerObject.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsOwnedByLocalPlayer()) return;

        combatInitializer?.RemoveNearbyItem(this);
    }

    private GameObject GetPlayerObject(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            return other.gameObject;
        }
        else if (other.transform.parent != null && other.transform.parent.CompareTag("Player"))
        {
            return other.transform.parent.gameObject;
        }
        return null;
    }

    private void HandlePlayerInteraction(GameObject playerObject, NetworkObject netObj)
    {
        // Check if player is authorized to collect this loot
        bool isAuthorized = false;
        for (int i = 0; i < AuthorizedPlayers.Length; i++)
        {
            if (AuthorizedPlayers[i] == netObj.InputAuthority)
            {
                isAuthorized = true;
                break;
            }
        }

        if (!isAuthorized) return;

        float distance = Vector2.Distance(transform.position, playerObject.transform.position);

        // YENÄ° - Craft material otomatik toplama (3f mesafede)
        bool hasCraftMaterials = droppedItems.Any(item => item != null && item.IsCraftItem());
        if (hasCraftMaterials && distance <= 3f)
        {
            OnCraftMaterialCollected(netObj);
        }

        // Coin and normal Item collection (4f range)
        if ((coinAmount > 0 || droppedItems.Any(item => item != null && !item.IsCraftItem())) && distance <= 4f)
        {
            if (combatInitializer != null && !combatInitializer.nearbyItems.Contains(this))
            {
                combatInitializer.nearbyItems.Add(this);
                combatInitializer.UpdateNearestItem();
                combatInitializer.UpdatePickupButton();
                combatInitializer.UpdateItemInfoPanel();
            }
        }

        // Potion collection (direct pickup)
        if (potionAmount > 0 && distance <= potionCollectionRadius)
        {
            OnPotionCollected(netObj);
        }
    }
public void OnCraftMaterialCollected(NetworkObject playerNetworkObject)
{
    if (playerNetworkObject == null || !CanCollectLoot()) return;

    // Craft itemlarÄ± bul
    var craftItems = droppedItems.Where(item => item != null && item.IsCraftItem()).ToList();
    if (craftItems.Count == 0) return;

    CraftInventorySystem craftInventory = playerNetworkObject.GetComponent<CraftInventorySystem>();
    if (craftInventory == null) return;

    // DEÄžÄ°ÅžÄ°KLÄ°K: Client'lar da RPC gÃ¶nderebilsin
    foreach (var craftItem in craftItems)
    {
        // Input authority kontrolÃ¼ - sadece item'Ä±n sahibi olan player RPC gÃ¶nderebilir
        if (playerNetworkObject.HasInputAuthority)
        {
            RequestCraftMaterialCollectionRPC(playerNetworkObject.InputAuthority, craftItem.itemId);
        }
    }
}

[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
private void RequestCraftMaterialCollectionRPC(PlayerRef collectorPlayer, string itemId)
{
    if (!Object.HasStateAuthority) return;

    // Craft item'Ä± bul
    var craftItem = droppedItems.FirstOrDefault(x => x != null && x.itemId == itemId && x.IsCraftItem());
    if (craftItem == null) return;

    // Craft item'Ä± listeden kaldÄ±r
    droppedItems.RemoveAll(x => x != null && x.itemId == itemId && x.IsCraftItem());
    
    // Client'a craft item'Ä±n toplandÄ±ÄŸÄ±nÄ± bildir
    RPC_RemoveCollectedCraftItem(itemId);
    
    // Server'da craft inventory'ye ekle
    NetworkObject playerObj = null;
    foreach (var player in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
    {
        if (player.InputAuthority == collectorPlayer)
        {
            playerObj = player;
            break;
        }
    }

    if (playerObj != null)
    {
        CraftInventorySystem craftInventory = playerObj.GetComponent<CraftInventorySystem>();
        if (craftInventory != null)
        {
            bool success = craftInventory.TryAddCraftItem(craftItem, 1, true);
            if (!success)
            {
                // Craft inventory dolu ise normal inventory'ye gÃ¶nder
                InventorySystem normalInventory = playerObj.GetComponent<InventorySystem>();
                if (normalInventory != null)
                {
                    normalInventory.TryAddItem(craftItem, 1, true);
                }
            }
        }
    }

    CheckForDestroy();
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_RemoveCollectedCraftItem(string itemId)
{
    droppedItems.RemoveAll(x => x != null && x.itemId == itemId && x.IsCraftItem());
    lootUI?.UpdateVisuals(coinAmount, potionAmount, droppedItems);
}
    #endregion
}