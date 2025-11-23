using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class ItemDropData
{
    public ItemData item;
    [Range(0f, 100f)]
    public float dropChance = 10f;
}

public class MonsterLootSystem : NetworkBehaviour
{
    #region REFERENCES
    private MonsterBehaviour monsterBehaviour;
    #endregion

    #region LOOT SETTINGS
    [Header("Coin Settings")]
    [SerializeField] private int minCoinDrop = 10;
    [SerializeField] private int maxCoinDrop = 50;
    [SerializeField] private float potionDropChance = 10f;

    [Header("Item Drops")]
    [SerializeField] private List<ItemDropData> possibleDrops = new List<ItemDropData>();
    [SerializeField] private List<ItemDropData> magicDrops = new List<ItemDropData>();
    [SerializeField] private List<ItemDropData> rareDrops = new List<ItemDropData>();

    [Header("Craft Material Drops")]
    [SerializeField] private List<ItemDropData> craftMaterialDrops = new List<ItemDropData>();
    [SerializeField] private bool dropCraftMaterials = true;
    [SerializeField] private float craftMaterialBaseChance = 70f;
    #endregion

    #region INITIALIZATION
    private void Awake()
    {
        monsterBehaviour = GetComponent<MonsterBehaviour>();
    }
    #endregion

    #region PUBLIC API
    public void HandleLootDrop(PlayerRef killerRef, Vector2 deathPosition)
    {
        if (!Object.HasStateAuthority) return;

        try
        {
            List<PlayerRef> recipients = GetLootRecipients(killerRef);
            SpawnLootForPlayers(recipients, deathPosition);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"HandleLootDrop error: {e.Message}\n{e.StackTrace}");
        }
    }

    public void UpdateCoinRange(int min, int max)
    {
        minCoinDrop = min;
        maxCoinDrop = max;
    }
    #endregion

    #region LOOT SPAWNING
private void SpawnLootForPlayers(List<PlayerRef> recipients, Vector2 basePosition)
{
    if (!Object.HasStateAuthority) return;

    try
    {
        GameObject droppedLootPrefab = Resources.Load<GameObject>("DroppedLoot");
        GameObject coinDropPrefab = Resources.Load<GameObject>("CoinDrop");
        GameObject fragmentDropPrefab = Resources.Load<GameObject>("FragmentDrop"); // YENİ
        
        int coinAmount = Random.Range(minCoinDrop, maxCoinDrop + 1);
        List<ItemData> itemsToDrop = CalculateItemDropsForPlayer();
        PlayerRef[] recipientArray = recipients.ToArray();
        
        // Coin drop
        if (coinAmount > 0 && coinDropPrefab != null)
        {
            Vector2 coinPosition = GetValidDropPosition(basePosition, true);
            var coinDropObj = Runner.Spawn(coinDropPrefab, coinPosition, Quaternion.identity, PlayerRef.None,
                (runner, obj) =>
                {
                    var coinDrop = obj.GetComponent<CoinDrop>();
                    if (coinDrop != null)
                    {
                        coinDrop.SetAuthorizedPlayersOnSpawn(recipientArray);
                    }
                });

            if (coinDropObj != null)
            {
                var coinDrop = coinDropObj.GetComponent<CoinDrop>();
                if (coinDrop != null)
                {
                    coinDrop.InitializeCoinRPC(coinAmount, recipients[0], recipientArray, coinPosition);
                }
            }
        }

        // YENİ - Fragment ve normal item'ları ayır
        var normalItems = itemsToDrop.Where(item => !item.IsCraftItem()).ToList();
        var craftItems = itemsToDrop.Where(item => item.IsCraftItem() && !item.IsFragment()).ToList();
        var fragmentItems = itemsToDrop.Where(item => item.IsFragment()).ToList();
        
foreach (ItemData item in normalItems)
{
    if (item != null)
    {
        Vector2 itemPosition = GetValidDropPosition(basePosition, false);

        var itemLootObj = Runner.Spawn(droppedLootPrefab, itemPosition, Quaternion.identity, PlayerRef.None,
            (runner, obj) =>
            {
                var droppedLoot = obj.GetComponent<DroppedLoot>();
                if (droppedLoot != null)
                {
                    droppedLoot.SetAuthorizedPlayersOnSpawn(recipientArray);
                }
            });

        if (itemLootObj != null)
        {
            var droppedLoot = itemLootObj.GetComponent<DroppedLoot>();
            if (droppedLoot != null)
            {
                string statsJson = "";
                if (item.stats != null && item.stats.Count > 0)
                {
                    SerializableItemStats serializableStats = new SerializableItemStats();
                    serializableStats.stats = new List<ItemStat>(item.stats);
                    statsJson = JsonUtility.ToJson(serializableStats);
                }

                droppedLoot.InitializeDroppedLootWithRecipients(
                    recipientArray,
                    0,
                    0,
                    new string[] { item.itemId },
                    new int[] { item.upgradeLevel },
                    new int[] { (int)item.currentRarity },
                    new string[] { statsJson },
                    new float[] { item.armorValue },
                    new float[] { item.attackPower },
                    new int[] { item.itemLevel }, // YENİ
                    new int[] { item.effectiveLevel } // YENİ
                );
            }
        }
    }
}

        // Craft items (fragment olmayan)
foreach (ItemData craftItem in craftItems)
{
    if (craftItem != null)
    {
        Vector2 itemPosition = GetValidDropPosition(basePosition, false);
        var craftLootObj = Runner.Spawn(droppedLootPrefab, itemPosition, Quaternion.identity, PlayerRef.None,
            (runner, obj) =>
            {
                var droppedLoot = obj.GetComponent<DroppedLoot>();
                if (droppedLoot != null)
                {
                    droppedLoot.SetAuthorizedPlayersOnSpawn(recipientArray);
                }
            });

        if (craftLootObj != null)
        {
            var droppedLoot = craftLootObj.GetComponent<DroppedLoot>();
            if (droppedLoot != null)
            {
                string statsJson = "";
                if (craftItem.stats != null && craftItem.stats.Count > 0)
                {
                    SerializableItemStats serializableStats = new SerializableItemStats();
                    serializableStats.stats = new List<ItemStat>(craftItem.stats);
                    statsJson = JsonUtility.ToJson(serializableStats);
                }

                droppedLoot.InitializeDroppedLootWithRecipients(
                    recipientArray,
                    0,
                    0,
                    new string[] { craftItem.itemId },
                    new int[] { craftItem.upgradeLevel },
                    new int[] { (int)craftItem.currentRarity },
                    new string[] { statsJson },
                    new float[] { 0f },
                    new float[] { 0f },
                    new int[] { craftItem.itemLevel }, // YENİ
                    new int[] { craftItem.effectiveLevel } // YENİ
                );
            }
        }
    }
}

foreach (ItemData fragmentItem in fragmentItems)
{
    if (fragmentItem != null && fragmentDropPrefab != null)
    {
        Vector2 fragmentPosition = GetValidDropPosition(basePosition, true);
        
        // Fragment amount hesapla
        int monsterLevel = monsterBehaviour.NetworkCoreData.MonsterLevel;
        float rarityMultiplier = monsterBehaviour.Rarity switch
        {
            MonsterRarity.Normal => 1.0f,
            MonsterRarity.Magic => 1.5f,
            MonsterRarity.Rare => 2.0f,
            _ => 1.0f
        };
        
        int minAmount = Mathf.Max(0, (monsterLevel - 5) / 5);
        int maxAmount = Mathf.Max(1, monsterLevel / 3);
        minAmount = Mathf.RoundToInt(minAmount * rarityMultiplier);
        maxAmount = Mathf.RoundToInt(maxAmount * rarityMultiplier);
        int fragmentAmount = Random.Range(minAmount, maxAmount + 1);
        
        if (fragmentAmount <= 0) fragmentAmount = 1;
        
        var fragmentDropObj = Runner.Spawn(fragmentDropPrefab, fragmentPosition, Quaternion.identity, PlayerRef.None,
            (runner, obj) =>
            {
                var fragmentDrop = obj.GetComponent<FragmentDrop>();
                if (fragmentDrop != null)
                {
                    fragmentDrop.SetAuthorizedPlayersOnSpawn(recipientArray);
                }
            });

        if (fragmentDropObj != null)
        {
            var fragmentDrop = fragmentDropObj.GetComponent<FragmentDrop>();
            if (fragmentDrop != null)
            {
                fragmentDrop.InitializeFragmentRPC(
                    fragmentItem.itemId,
                    fragmentAmount,
                    recipients[0],
                    recipientArray,
                    fragmentPosition
                );
            }
        }
    }
}

// Potion drop
float potionRoll = Random.Range(0f, 100f);
if (potionRoll <= potionDropChance)
{
    Vector2 potionPosition = GetValidDropPosition(basePosition, false);
    var potionLootObj = Runner.Spawn(droppedLootPrefab, potionPosition, Quaternion.identity, PlayerRef.None,
        (runner, obj) =>
        {
            var droppedLoot = obj.GetComponent<DroppedLoot>();
            if (droppedLoot != null)
            {
                droppedLoot.SetAuthorizedPlayersOnSpawn(recipientArray);
            }
        });

    if (potionLootObj != null)
    {
        var droppedLoot = potionLootObj.GetComponent<DroppedLoot>();
        if (droppedLoot != null)
        {
            droppedLoot.InitializeDroppedLootWithRecipients(
                recipientArray,
                0,
                1,
                new string[0],
                new int[0],
                new int[0],
                new string[0],
                new float[0],
                new float[0],
                new int[0], // YENİ - boş array
                new int[0]  // YENİ - boş array
            );
        }
    }
}
    }
    catch (System.Exception e)
    {
        Debug.LogError($"SpawnLootForPlayers error: {e.Message}\n{e.StackTrace}");
    }
}
    private List<ItemData> CalculateItemDropsForPlayer()
    {
        if (!Runner.IsServer) return new List<ItemData>();

        List<ItemData> itemsToDrop = new List<ItemData>();
        HashSet<string> processedItemIds = new HashSet<string>();

        float rarityMultiplier = monsterBehaviour.Rarity switch
        {
            MonsterRarity.Normal => 1.0f,
            MonsterRarity.Magic => 1.2f,
            MonsterRarity.Rare => 1.5f,
            _ => 1.0f
        };

        // Normal item drops
        foreach (var dropData in possibleDrops)
        {
            if (dropData.item != null && !processedItemIds.Contains(dropData.item.itemId))
            {
                processedItemIds.Add(dropData.item.itemId);
                float randomValue = Random.Range(0f, 100f);
                float finalChance = dropData.dropChance * rarityMultiplier;
                
                if (randomValue <= finalChance)
                {
                    ItemData droppedItem = CreateItemWithNewSystem(dropData.item);
                    if (droppedItem != null)
                    {
                        itemsToDrop.Add(droppedItem);
                    }
                }
            }
        }

        // Magic drops
        foreach (var dropData in magicDrops)
        {
            if (dropData.item != null && !processedItemIds.Contains(dropData.item.itemId))
            {
                processedItemIds.Add(dropData.item.itemId);
                float randomValue = Random.Range(0f, 100f);
                float finalChance = dropData.dropChance * rarityMultiplier;
                
                if (randomValue <= finalChance)
                {
                    ItemData droppedItem = CreateItemWithNewSystem(dropData.item);
                    if (droppedItem != null)
                    {
                        itemsToDrop.Add(droppedItem);
                    }
                }
            }
        }

        // Rare drops
        foreach (var dropData in rareDrops)
        {
            if (dropData.item != null && !processedItemIds.Contains(dropData.item.itemId))
            {
                processedItemIds.Add(dropData.item.itemId);
                float randomValue = Random.Range(0f, 100f);
                float finalChance = dropData.dropChance * rarityMultiplier;
                
                if (randomValue <= finalChance)
                {
                    ItemData droppedItem = CreateItemWithNewSystem(dropData.item);
                    if (droppedItem != null)
                    {
                        itemsToDrop.Add(droppedItem);
                    }
                }
            }
        }

        // Craft material drops
        if (dropCraftMaterials && craftMaterialDrops.Count > 0)
        {
            foreach (var dropData in craftMaterialDrops)
            {
                if (dropData.item != null && !processedItemIds.Contains(dropData.item.itemId))
                {
                    processedItemIds.Add(dropData.item.itemId);
                    float randomValue = Random.Range(0f, 100f);
                    float adjustedChance = craftMaterialBaseChance * (dropData.dropChance / 100f);

                    if (randomValue <= adjustedChance)
                    {
                        ItemData droppedMaterial = dropData.item.CreateCraftCopy();
                        itemsToDrop.Add(droppedMaterial);
                    }
                }
            }
        }
List<ItemData> fragmentDrops = CalculateFragmentDrops();
foreach (var fragment in fragmentDrops)
{
    if (fragment != null && !processedItemIds.Contains(fragment.itemId))
    {
        processedItemIds.Add(fragment.itemId);
        itemsToDrop.Add(fragment);
    }
}
        return itemsToDrop;
    }
private List<ItemData> CalculateFragmentDrops()
{
    if (!Runner.IsServer) return new List<ItemData>();

    List<ItemData> fragmentsToDrop = new List<ItemData>();
    
    if (ItemDatabase.Instance == null) return fragmentsToDrop;

    List<ItemData> allFragments = ItemDatabase.Instance.GetAllCraftItems()
        .Where(item => item.IsFragment())
        .ToList();

    if (allFragments.Count == 0) return fragmentsToDrop;

    int monsterLevel = monsterBehaviour.NetworkCoreData.MonsterLevel;
    
    float rarityMultiplier = monsterBehaviour.Rarity switch
    {
        MonsterRarity.Normal => 1.0f,
        MonsterRarity.Magic => 1.5f,
        MonsterRarity.Rare => 2.0f,
        _ => 1.0f
    };

    foreach (var fragmentBase in allFragments)
    {
        float baseChance = fragmentBase.FragmentBaseDropChance;
        
        // ❌ ESKİ:
        // float levelScaling = Mathf.Pow(monsterLevel / 10f, 1.5f);
        
        // ✅ YENİ (3 kat artırıldı):
        float levelScaling = Mathf.Pow(monsterLevel / 10f, 1.5f) * 3f;
        
        float finalChance = Mathf.Min(95f, baseChance * levelScaling * rarityMultiplier);

        float randomRoll = Random.Range(0f, 100f);
        
        if (randomRoll <= finalChance)
        {
            int minAmount = Mathf.Max(0, (monsterLevel - 5) / 5);
            int maxAmount = Mathf.Max(1, monsterLevel / 3);
            
            minAmount = Mathf.RoundToInt(minAmount * rarityMultiplier);
            maxAmount = Mathf.RoundToInt(maxAmount * rarityMultiplier);
            
            int dropAmount = Random.Range(minAmount, maxAmount + 1);
            
            if (dropAmount > 0)
            {
                ItemData fragmentCopy = fragmentBase.CreateFragmentCopy();
                fragmentsToDrop.Add(fragmentCopy);
            }
        }
    }

    return fragmentsToDrop;
}
private ItemData CreateItemWithNewSystem(ItemData baseItem)
{
    if (baseItem.IsCraftItem() && !baseItem.IsFragment())
    {
        return baseItem.CreateCraftCopy();
    }
    
    if (baseItem.IsCollectible())
    {
        return baseItem.CreateCollectibleCopy();
    }
    
    if (baseItem.IsFragment())
    {
        return baseItem.CreateFragmentCopy();
    }

    ItemData newItem = Instantiate(baseItem);
    
    // Monster level al
    int monsterLevel = monsterBehaviour.NetworkCoreData.MonsterLevel;
    
    // Equippable item kontrolü
    int effectiveLevel;
    if (newItem.IsEquippableItem())
    {
        // Equippable item: monsterLevel + itemLevel
        effectiveLevel = monsterLevel + newItem.itemLevel;
    }
    else
    {
        // Non-equippable: sadece monsterLevel
        effectiveLevel = monsterLevel;
        newItem.itemLevel = 1; // Default
    }

    // YENİ - effectiveLevel'ı item'a kaydet
    newItem.effectiveLevel = effectiveLevel;

    // Rarity belirleme
    float rarityRoll = Random.Range(0f, 100f);
    GameItemRarity itemRarity;

    if (rarityRoll <= 80f)
        itemRarity = GameItemRarity.Normal;
    else if (rarityRoll <= 97f)
        itemRarity = GameItemRarity.Magic;
    else
        itemRarity = GameItemRarity.Rare;

    if ((monsterBehaviour.Rarity == MonsterRarity.Magic || monsterBehaviour.Rarity == MonsterRarity.Rare) &&
        Random.Range(0f, 100f) <= 15f)
    {
        itemRarity = itemRarity switch
        {
            GameItemRarity.Normal => GameItemRarity.Magic,
            GameItemRarity.Magic => GameItemRarity.Rare,
            _ => itemRarity
        };
    }

    newItem.currentRarity = itemRarity;

    // Stat hesaplama (equippable itemler için)
    if (newItem.IsEquippableItem())
    {
        EquipmentSlotType slotType = newItem.GetEquipmentSlotType();
        List<StatType> availableStats = ItemStatSystem.GetWhitelistForSlot(slotType);

        if (availableStats.Count == 0)
        {
            return null;
        }

        int affixCount = ItemStatSystem.GetAffixCountForRarity(itemRarity);

        newItem._selectedStats = new List<ItemStat>();
        List<StatType> shuffledStats = availableStats.OrderBy(x => Random.value).ToList();
        int statsToSelect = Mathf.Min(affixCount, shuffledStats.Count);

        for (int i = 0; i < statsToSelect; i++)
        {
            StatType selectedStatType = shuffledStats[i];
            // effectiveLevel kullanılıyor
            float statValue = ItemStatSystem.CalculateStatValue(selectedStatType, effectiveLevel, itemRarity);

            newItem._selectedStats.Add(new ItemStat
            {
                type = selectedStatType,
                value = statValue
            });
        }

        // Armor/Attack power hesaplama
        if (newItem.IsArmorItem())
        {
            newItem.armorValue = newItem.CalculateArmorValue(effectiveLevel, itemRarity);
        }
        else if (newItem.IsWeaponItem())
        {
            newItem.attackPower = newItem.CalculateAttackPower(effectiveLevel, itemRarity);
        }

        newItem.CalculateRequiredLevel(effectiveLevel);
    }

    return newItem;
}
    #endregion

    #region POSITION HELPERS
    private Vector2 GetValidDropPosition(Vector2 basePosition, bool isGold)
    {
        if (!Runner.IsServer) return basePosition;

        float radius = isGold ? 8.0f : 4.0f;
        float minSpread = isGold ? 3f : 3f;
        int maxAttempts = 3;
        float minDistanceBetweenDrops = 3.0f;
        float minPlayerDistance = 3f;

        List<Vector2> playerPositions = GetNearbyPlayerPositions(basePosition);

        for (int i = 0; i < maxAttempts; i++)
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float randomDistance = Random.Range(minSpread, radius);

            Vector2 offset = new Vector2(
                Mathf.Cos(randomAngle) * randomDistance,
                Mathf.Sin(randomAngle) * randomDistance
            );

            Vector2 testPosition = basePosition + offset;

            bool tooCloseToPlayer = false;
            foreach (Vector2 playerPos in playerPositions)
            {
                if (Vector2.Distance(testPosition, playerPos) < minPlayerDistance)
                {
                    tooCloseToPlayer = true;
                    break;
                }
            }

            if (tooCloseToPlayer) continue;

            bool isValid = !Physics2D.OverlapCircle(testPosition, 0.3f,
                                                   LayerMask.GetMask("Obstacles", "Wall"));

            if (isValid)
            {
                bool hasNearbyLoot = Physics2D.OverlapCircle(testPosition, minDistanceBetweenDrops,
                                                             LayerMask.GetMask("Loot")) != null;
                
                if (!hasNearbyLoot)
                {
                    return testPosition;
                }
            }
        }

        float fallbackAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float fallbackDist = isGold ? 4f : 3.5f;
        Vector2 fallbackOffset = new Vector2(
            Mathf.Cos(fallbackAngle) * fallbackDist,
            Mathf.Sin(fallbackAngle) * fallbackDist
        );

        return basePosition + fallbackOffset;
    }

    private List<Vector2> GetNearbyPlayerPositions(Vector2 center)
    {
        List<Vector2> positions = new List<Vector2>();
        
        if (PlayerManager.Instance != null)
        {
            var nearbyPlayers = PlayerManager.Instance.GetPlayersNear(center, 20f);
            foreach (var playerData in nearbyPlayers)
            {
                if (playerData.transform != null)
                {
                    positions.Add(playerData.transform.position);
                }
            }
        }
        else
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject player in players)
            {
                if (player != null)
                {
                    positions.Add(player.transform.position);
                }
            }
        }
        
        return positions;
    }
    #endregion

    #region PARTY HELPERS
    private List<PlayerRef> GetLootRecipients(PlayerRef killer)
    {
        List<PlayerRef> recipients = new List<PlayerRef> { killer };

        if (PartyManager.Instance != null)
        {
            PlayerStats killerStats = GetPlayerStats(killer);
            if (killerStats != null && killerStats.IsInParty())
            {
                int partyId = killerStats.GetPartyId();
                var partyMembers = PartyManager.Instance.GetPartyMembers(partyId);

                foreach (var member in partyMembers)
                {
                    if (member != killer && !recipients.Contains(member))
                    {
                        recipients.Add(member);
                    }
                }
            }
        }

        return recipients;
    }

    private PlayerStats GetPlayerStats(PlayerRef player)
    {
        NetworkObject[] allPlayers = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

        foreach (NetworkObject netObj in allPlayers)
        {
            if (netObj != null && netObj.IsValid && netObj.InputAuthority == player)
            {
                PlayerStats stats = netObj.GetComponent<PlayerStats>();
                return stats;
            }
        }

        return null;
    }
    #endregion

    #region SERIALIZATION HELPER
    [System.Serializable]
    private class SerializableItemStats
    {
        public List<ItemStat> stats = new List<ItemStat>();
    }
    #endregion
}