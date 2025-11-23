// Path: Assets/Game/Scripts/ItemData.cs

using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
public enum GameItemRarity
{
    Normal,
    Magic,
    Rare
}
public enum GameItemTag
{
    ShowEars,     // Helmetin kulaklarÄ± gÃ¶sterip gÃ¶stermediÄŸi
    FullHair,     // Helmetin tam saÃ§ gÃ¶sterip gÃ¶stermediÄŸi
}

[CreateAssetMenu(fileName = "New Item", menuName = "MMORPG/Item")]
public class ItemData : ScriptableObject
{

[Header("Collectible Settings")]
[Tooltip("Bu item Collectible ise manuel fiyat belirle")]
[SerializeField] private int manualBuyPrice = 100;
    [SerializeField] private int manualSellPrice = 50;

[SerializeField, HideInInspector] 
public GameItemRarity currentRarity = GameItemRarity.Normal;
public GameItemRarity Rarity => currentRarity;

[Header("Character Appearance")]
public string SpriteId;

[Header("Upgrade Ã–zellikleri")]
[SerializeField, HideInInspector] 
public int upgradeLevel = 1;
public const int MAX_UPGRADE_LEVEL = 10;
public const float UPGRADE_STAT_MULTIPLIER = 0.10f;

[Header("Temel Bilgiler")]
[SerializeField] public string _baseItemName;
    [SerializeField, HideInInspector] // Bu satırı ekle
    public int requiredLevel;

public List<ItemStat> _selectedStats = new List<ItemStat>();
public string itemId;
public string itemName
{
    get
    {
        string rarityPrefix = currentRarity switch
        {
            GameItemRarity.Magic => "<color=#0080FF>[Büyülü]</color> ",
            GameItemRarity.Rare => "<color=#CC33CC>[Nadir]</color> ",
            _ => ""
        };
        string upgradeSuffix = upgradeLevel > 1 ? $" +{upgradeLevel}" : "";
        return $"{rarityPrefix}{_baseItemName}{upgradeSuffix}";
    }
    set
    {
        _baseItemName = value.Split('+')[0].Trim()
            .Replace("[Magic]", "")
            .Replace("[Rare]", "")
            .Replace("[Büyülü]", "")
            .Replace("[Nadir]", "")
            .Trim();
    }
}
public Sprite itemIcon;
public GameItemType GameItemType;

public string description;

[Header("Item Level")]
[SerializeField] // Bu satırı ekle
public int itemLevel = 1;

// YENİ - Effective Level field'ı ekle
[SerializeField, HideInInspector]
public int effectiveLevel = 1;

[Header("Ä°tem Ã–zellikleri")]
public List<ItemStat> stats
{
    get { return _selectedStats; }
    set { _selectedStats = value; }
}

    private class DroppedItemData
    {
        public string itemId;
        public List<ItemStat> stats;
        public int upgradeLevel;
    }

[Header("Ekstra Ã–zellikler")]
[SerializeField, HideInInspector]

public bool isStackable
{
    get
    {
        return GameItemType == GameItemType.CraftMaterial ||
               GameItemType == GameItemType.CraftComponent ||
               GameItemType == GameItemType.CraftConsumable ||
               GameItemType == GameItemType.Fragment ||           // YENİ
               GameItemType == GameItemType.QuestItem ||
               GameItemType == GameItemType.Collectible;
    }
}
public bool IsQuestItem()
{
    return GameItemType == GameItemType.QuestItem;
}
public int maxStackSize = 1;
[Header("Dynamic Pricing")]
public int buyPrice => CalculateBuyPrice();
public int sellPrice => IsCollectible() ? manualSellPrice : Mathf.RoundToInt(buyPrice * 0.5f);

    private int CalculateBuyPrice()
    {
        if (IsCraftItem()) return 0;
        if (IsQuestItem()) return 0;
        if (IsCollectible()) return manualBuyPrice;  // YENİ

        float basePrice = 100f;
        float rarityMultiplier = GetRarityMultiplier();
        float levelTierMultiplier = GetLevelTierMultiplier();
        float statQualityBonus = CalculateStatQualityBonus();

        float finalPrice = basePrice * rarityMultiplier * levelTierMultiplier * (1f + statQualityBonus);

        return Mathf.RoundToInt(finalPrice / 5f) * 5;
    }
public bool IsCollectible()
{
    return GameItemType == GameItemType.Collectible;
}

public void SetupAsCollectible(int stackSize = 999, int buyPrice = 100, int sellPrice = 50)
{
    maxStackSize = stackSize;
    manualBuyPrice = buyPrice;
    manualSellPrice = sellPrice;
    requiredLevel = 1;

    // Tüm stat sistemlerini temizle
    stats.Clear();
    _selectedStats.Clear();

    // Rarity'yi normal yap
    currentRarity = GameItemRarity.Normal;

    // Upgrade level'ı sıfırla
    upgradeLevel = 1;
    
    // Armor ve attack power sıfırla
    armorValue = 0f;
    attackPower = 0f;
}

public ItemData CreateCollectibleCopy()
{
    if (!IsCollectible())
    {
        return CreateCopy();
    }

    ItemData copy = Instantiate(this);
    copy._baseItemName = this._baseItemName;
    copy.upgradeLevel = 1;
    copy.currentRarity = GameItemRarity.Normal;
    copy._selectedStats = new List<ItemStat>();
    copy.manualBuyPrice = this.manualBuyPrice;
    copy.manualSellPrice = this.manualSellPrice;
    copy.SetupAsCollectible(this.maxStackSize, this.manualBuyPrice, this.manualSellPrice);

    return copy;
}
public void CalculateRequiredLevel(int monsterLevel)
{
    // Craft item'lar için required level yok
    if (IsCraftItem())
    {
        requiredLevel = 1;
        return;
    }

    // Stat quality hesapla
    float averageQuality = CalculateAverageStatQuality();
    
    // Quality'ye göre level adjustment
    int levelAdjustment = 0;
    
    if (averageQuality >= 0.8f) // Çok yüksek roll (%80+)
    {
        levelAdjustment = UnityEngine.Random.Range(1, 3); // +1 veya +2
    }
    else if (averageQuality >= 0.6f) // Yüksek roll (%60-80)
    {
        levelAdjustment = UnityEngine.Random.Range(0, 2); // 0 veya +1
    }
    else if (averageQuality >= 0.4f) // Ortalama roll (%40-60)
    {
        levelAdjustment = 0; // Monster level
    }
    else if (averageQuality >= 0.2f) // Düşük roll (%20-40)
    {
        levelAdjustment = UnityEngine.Random.Range(-1, 1); // -1 veya 0
    }
    else // Çok düşük roll (%20-)
    {
        levelAdjustment = UnityEngine.Random.Range(-2, 0); // -2 veya -1
    }
    
    // Final required level (minimum 1)
    requiredLevel = Mathf.Max(1, monsterLevel + levelAdjustment);
}

private float CalculateAverageStatQuality()
{
    if (_selectedStats.Count == 0) return 0.5f; // Ortalama
    
    float totalQualityScore = 0f;
    int validStatsCount = 0;
    
    foreach (var stat in _selectedStats)
    {
        // GoldFind ve ItemRarity quality hesabına dahil edilmez
        if (stat.type == StatType.GoldFind || stat.type == StatType.ItemRarity)
            continue;
            
        float qualityRatio = CalculateStatQualityRatio(stat);
        totalQualityScore += qualityRatio;
        validStatsCount++;
    }
    
    if (validStatsCount == 0) return 0.5f; // Ortalama
    
    return totalQualityScore / validStatsCount;
}
private float GetRarityMultiplier()
{
    return currentRarity switch
    {
        GameItemRarity.Normal => 1.0f,
        GameItemRarity.Magic => 1.8f,
        GameItemRarity.Rare => 3.0f,
        _ => 1.0f
    };
}

private float GetLevelTierMultiplier()
{
    if (itemLevel <= 15) return 1.0f;
    if (itemLevel <= 30) return 2.0f;
    if (itemLevel <= 45) return 3.0f;
    return 4.0f;
}

private float CalculateStatQualityBonus()
{
    if (_selectedStats.Count == 0) return 0f;
    
    float totalQualityScore = 0f;
    int validStatsCount = 0;
    
    foreach (var stat in _selectedStats)
    {
        // Skip GoldFind and ItemRarity from quality calculation
        if (stat.type == StatType.GoldFind || stat.type == StatType.ItemRarity)
            continue;
            
        float qualityRatio = CalculateStatQualityRatio(stat);
        
        // Range and ProjectileSpeed get half weight
        float weight = (stat.type == StatType.Range || stat.type == StatType.ProjectileSpeed) ? 0.5f : 1.0f;
        
        totalQualityScore += qualityRatio * weight;
        validStatsCount++;
    }
    
    if (validStatsCount == 0) return 0f;
    
    float averageQuality = totalQualityScore / validStatsCount;
    
    // Convert to bonus percentage (max 20%)
    return Mathf.Clamp(averageQuality * 0.2f, 0f, 0.2f);
}

private float CalculateStatQualityRatio(ItemStat stat)
{
    // Get base calculation values
    float baseValue = ItemStatSystem.GetBaseValue(stat.type);
    float perLevelValue = ItemStatSystem.GetPerLevelValue(stat.type);
    float rarityMultiplier = ItemStatSystem.GetRarityMultiplier(currentRarity);
    
    float baseCalculatedValue = (baseValue + perLevelValue * itemLevel) * rarityMultiplier;
    
    // Min and max possible values based on random multiplier range (0.85-1.15)
    float minValue = baseCalculatedValue * 0.85f;
    float maxValue = baseCalculatedValue * 1.15f;
    
    if (maxValue <= minValue) return 0f;
    
    // Calculate quality ratio (0.0 = worst roll, 1.0 = best roll)
    return Mathf.Clamp01((stat.value - minValue) / (maxValue - minValue));
}
    public float GetStatValue(StatType statType)
    {
        if (IsCraftItem()) return 0f; // Craft itemlarda stat yok

        var currentStats = stats;
        var stat = currentStats.Find(s => s.type == statType);
        return stat?.value ?? 0f;
    }
    [Header("Armor & Attack Power")]
[SerializeField, HideInInspector]
public float armorValue;

    [SerializeField, HideInInspector]
    public float attackPower;
public bool IsArmorItem()
{
    return GameItemType == GameItemType.Helmet ||
           GameItemType == GameItemType.Bracers ||
           GameItemType == GameItemType.ChestArmor ||
           GameItemType == GameItemType.Leggings ||
           GameItemType == GameItemType.Belt;
}

    // Weapon item kontrolü için yeni metod ekle
    public bool IsWeaponItem()
    {
        return GameItemType == GameItemType.MeleeWeapon2H ||
               GameItemType == GameItemType.CompositeWeapon;
    }
    public bool IsEquippableItem()
{
    return GameItemType == GameItemType.MeleeWeapon2H ||
           GameItemType == GameItemType.CompositeWeapon ||
           GameItemType == GameItemType.Helmet ||
           GameItemType == GameItemType.Bracers ||
           GameItemType == GameItemType.ChestArmor ||
           GameItemType == GameItemType.Leggings ||
           GameItemType == GameItemType.Belt ||
           GameItemType == GameItemType.Ring ||
           GameItemType == GameItemType.Earring;
}
public float CalculateArmorValue(int effectiveLevel, GameItemRarity rarity)
{
    float baseArmor = 10f;
    float levelMultiplier = 1f + (effectiveLevel * 0.1f);
    float rarityMultiplier = GetRarityArmorMultiplier(rarity);
    float randomFactor = UnityEngine.Random.Range(0.9f, 1.1f);
    
    return baseArmor * levelMultiplier * rarityMultiplier * randomFactor;
}

public float CalculateAttackPower(int effectiveLevel, GameItemRarity rarity)
{
    float baseAttack = 5f;
    float levelMultiplier = 1f + (effectiveLevel * 0.1f);
    float rarityMultiplier = GetRarityArmorMultiplier(rarity);
    float randomFactor = UnityEngine.Random.Range(0.9f, 1.1f);
    
    return baseAttack * levelMultiplier * rarityMultiplier * randomFactor;
}

// Rarity multiplier
private float GetRarityArmorMultiplier(GameItemRarity rarity)
{
    return rarity switch
    {
        GameItemRarity.Normal => 1.0f,
        GameItemRarity.Magic => 1.2f,
        GameItemRarity.Rare => 1.5f,
        _ => 1.0f
    };
}
    // Upgrade maliyeti hesaplama
    public int CalculateUpgradeCost()
    {
        // Her seviye iÃ§in maliyet katlanarak artÄ±yor
        // Ã–rnek: +1 -> 1000 gold
        //        +2 -> 2000 gold
        //        +3 -> 4000 gold ÅŸeklinde
        int baseUpgradeCost = 1;
        return baseUpgradeCost * (int)Mathf.Pow(2, upgradeLevel - 1);
    }
    public float CalculateUpgradeChance()
    {
        // Her seviye iÃ§in ÅŸans azalÄ±yor
        // +1->+2: %90
        // +2->+3: %80
        // +3->+4: %70 ÅŸeklinde
        float baseChance = 100f;
        float chanceReductionPerLevel = 10f;
        return Mathf.Max(0, baseChance - (chanceReductionPerLevel * (upgradeLevel - 1)));
    }

    // Item'Ä± klonlayÄ±p yeni upgrade seviyesi iÃ§in hazÄ±rlama
    // ItemData.cs iÃ§inde CreateUpgradedVersion metodunu gÃ¼ncelle
public ItemData CreateUpgradedVersion()
{
    if (upgradeLevel >= MAX_UPGRADE_LEVEL)
        return null;
    ItemData upgradedItem = CreateExactCopy();
    upgradedItem.upgradeLevel += 1;

    // Her stat için upgrade bonus uygula
    for (int i = 0; i < upgradedItem._selectedStats.Count; i++)
    {
        float originalValue = upgradedItem._selectedStats[i].value;
        float upgradeBonus = originalValue * UPGRADE_STAT_MULTIPLIER;
        upgradedItem._selectedStats[i].value = originalValue + upgradeBonus;
    }

    // Armor değeri için upgrade bonus (armor item ise)
    if (upgradedItem.IsArmorItem() && upgradedItem.armorValue > 0)
    {
        float armorBonus = upgradedItem.armorValue * UPGRADE_STAT_MULTIPLIER;
        upgradedItem.armorValue += armorBonus;
    }

    // Attack power için upgrade bonus (weapon item ise)
    if (upgradedItem.IsWeaponItem() && upgradedItem.attackPower > 0)
    {
        float attackBonus = upgradedItem.attackPower * UPGRADE_STAT_MULTIPLIER;
        upgradedItem.attackPower += attackBonus;
    }

    return upgradedItem;
}

public ItemData CreateCopy(int monsterLevel = 1)
{
    if (IsCraftItem())
    {
        return CreateCraftCopy();
    }

    if (IsQuestItem())
    {
        return CreateQuestCopy();
    }
    
    if (IsCollectible())
    {
        return CreateCollectibleCopy();
    }
    
    if (IsFragment())  // YENİ
    {
        return CreateFragmentCopy();
    }

    ItemData copy = Instantiate(this);
    copy._baseItemName = this._baseItemName;
    copy.upgradeLevel = this.upgradeLevel;
    copy.itemLevel = monsterLevel;

    float randomValue = UnityEngine.Random.Range(0f, 100f);
    GameItemRarity selectedRarity;
    int statCount;

    if (randomValue <= 60f)
    {
        selectedRarity = GameItemRarity.Normal;
        statCount = 1;
    }
    else if (randomValue <= 90f)
    {
        selectedRarity = GameItemRarity.Magic;
        statCount = 2;
    }
    else
    {
        selectedRarity = GameItemRarity.Rare;
        statCount = 3;
    }

    copy.currentRarity = selectedRarity;
    copy.SelectRandomStats(statCount);

    if (copy.IsArmorItem())
    {
        if (copy.useManualStats && copy.manualArmorValue > 0f)
        {
            copy.armorValue = copy.manualArmorValue;
        }
        else
        {
            copy.armorValue = copy.CalculateArmorValue(monsterLevel, selectedRarity);
        }
    }
    else if (copy.IsWeaponItem())
    {
        if (copy.useManualStats && copy.manualAttackPower > 0f)
        {
            copy.attackPower = copy.manualAttackPower;
        }
        else
        {
            copy.attackPower = copy.CalculateAttackPower(monsterLevel, selectedRarity);
        }
    }

    return copy;
}
// Quest item setup metodu
public void SetupAsQuestItem(int stackSize = 1, bool clearExistingStats = true)
{
    maxStackSize = stackSize;
    requiredLevel = 1;

    if (clearExistingStats)
    {
        // Tüm stat sistemlerini temizle
        stats.Clear();
        _selectedStats.Clear();

        // Rarity'yi normal yap
        currentRarity = GameItemRarity.Normal;

        // Upgrade level'ı sıfırla
        upgradeLevel = 1;
        
        // Armor ve attack power sıfırla
        armorValue = 0f;
        attackPower = 0f;
    }
}

// Quest item copy metodu
public ItemData CreateQuestCopy()
{
    if (!IsQuestItem())
    {
        return CreateCopy();
    }

    ItemData copy = Instantiate(this);
    copy._baseItemName = this._baseItemName;
    copy.upgradeLevel = 1;
    copy.currentRarity = GameItemRarity.Normal;
    copy._selectedStats = new List<ItemStat>();
    copy.SetupAsQuestItem(this.maxStackSize, false);

    return copy;
}
public ItemData CreateExactCopy()
{
    ItemData copy = Instantiate(this);
    copy._baseItemName = this._baseItemName;
    copy.upgradeLevel = this.upgradeLevel;
    copy.currentRarity = this.currentRarity;
    copy.itemLevel = this.itemLevel;
    copy.effectiveLevel = this.effectiveLevel; // YENİ - effectiveLevel'ı da kopyala

    // Mevcut statları kopyala
    copy._selectedStats = new List<ItemStat>();
    foreach (var stat in this._selectedStats)
    {
        copy._selectedStats.Add(new ItemStat
        {
            type = stat.type,
            value = stat.value
        });
    }

    // Armor ve Attack Power değerlerini kopyala
    copy.armorValue = this.armorValue;
    copy.attackPower = this.attackPower;

    // 🔒 PRODUCTION SAFETY: Sprite referansını garanti et
    if (copy.itemIcon == null && this.itemIcon != null)
    {
        copy.itemIcon = this.itemIcon;
        Debug.LogWarning($"[ItemData] Sprite reference was null after Instantiate, manually copied for item: {itemId}");
    }

    // 🔒 PRODUCTION SAFETY: Critical field validation
    if (copy.itemIcon == null)
    {
        Debug.LogError($"[ItemData] CreateExactCopy FAILED - itemIcon is NULL for item: {itemId}. UI will not display this item!");
    }

    if (string.IsNullOrEmpty(copy.itemId))
    {
        Debug.LogError($"[ItemData] CreateExactCopy FAILED - itemId is NULL or empty!");
    }

    return copy;
}

private void SelectRandomStats(int statCount)
{
    _selectedStats = new List<ItemStat>();
    
    // Slot bazlÄ± whitelist al
    EquipmentSlotType slotType = GetEquipmentSlotType();
    List<StatType> availableStats = ItemStatSystem.GetWhitelistForSlot(slotType);
    
    if (availableStats.Count == 0)
    {
        return;
    }
    
    // Shuffled list oluÅŸtur
    List<StatType> shuffledStats = availableStats.OrderBy(x => UnityEngine.Random.value).ToList();
    
    // Ä°stenen sayÄ±da stat seÃ§
    int statsToSelect = Mathf.Min(statCount, shuffledStats.Count);
    
    for (int i = 0; i < statsToSelect; i++)
    {
        StatType selectedStatType = shuffledStats[i];
        float statValue = ItemStatSystem.CalculateStatValue(selectedStatType, itemLevel, currentRarity);
        
        _selectedStats.Add(new ItemStat
        {
            type = selectedStatType,
            value = statValue
        });
    }
}

    public void ReduceDurability(int amount)
    {
        // TODO: Durability sistemi eklendiÄŸinde implement edilecek
    }

public Dictionary<string, object> GetItemData()
{
    return new Dictionary<string, object>
    {
        { "itemId", itemId },
        { "upgradeLevel", upgradeLevel },
        { "rarity", (int)currentRarity },
        { "itemLevel", itemLevel },
        { "armorValue", armorValue },      // YENİ
        { "attackPower", attackPower },    // YENİ
        { "selectedStats", _selectedStats.Select(stat => new Dictionary<string, object>
            {
                { "type", (int)stat.type },
                { "value", stat.value }
            }).ToList()
        }
    };
}
public void LoadFromData(Dictionary<string, object> data)
{
    try
    {
        if (data.ContainsKey("rarity"))
        {
            currentRarity = (GameItemRarity)Convert.ToInt32(data["rarity"]);
        }

        if (data.ContainsKey("itemLevel"))
        {
            itemLevel = Convert.ToInt32(data["itemLevel"]);
        }
        
        // YENİ: Armor ve Attack Power load
        if (data.ContainsKey("armorValue"))
        {
            armorValue = Convert.ToSingle(data["armorValue"]);
        }
        
        if (data.ContainsKey("attackPower"))
        {
            attackPower = Convert.ToSingle(data["attackPower"]);
        }

        if (data.ContainsKey("selectedStats"))
        {
            _selectedStats = new List<ItemStat>();
            var statsData = data["selectedStats"] as List<object>;

            foreach (Dictionary<string, object> statData in statsData)
            {
                _selectedStats.Add(new ItemStat
                {
                    type = (StatType)Convert.ToInt32(statData["type"]),
                    value = Convert.ToSingle(statData["value"])
                });
            }
        }
    }
    catch (Exception e)
    {
        Debug.LogError($"[ItemData] LoadFromData error: {e.Message}");
    }
}

    public string GetDisplayName()
    {
        return upgradeLevel > 1 ? $"{_baseItemName} +{upgradeLevel}" : _baseItemName;
    }

public bool CanUpgrade()
{
    if (IsCraftItem()) return false;
    if (IsQuestItem()) return false;
    if (IsCollectible()) return false;
    if (IsFragment()) return false;  // YENİ
    return upgradeLevel < MAX_UPGRADE_LEVEL;
}
    // Ä°temin giyilebilir olup olmadÄ±ÄŸÄ±nÄ± kontrol et
    public bool CanEquip(int playerLevel)
    {
        return playerLevel >= requiredLevel;
    }

public EquipmentSlotType GetEquipmentSlotType()
{
    switch (GameItemType)
    {
        case GameItemType.MeleeWeapon2H:
            return EquipmentSlotType.MeleeWeapon2H;
        case GameItemType.CompositeWeapon:
            return EquipmentSlotType.CompositeWeapon;
        case GameItemType.Helmet:
            return EquipmentSlotType.Head;
        case GameItemType.Bracers:
            return EquipmentSlotType.Bracers;
        case GameItemType.ChestArmor:
            return EquipmentSlotType.Chest;
        case GameItemType.Leggings:
            return EquipmentSlotType.Leggings;
        case GameItemType.Ring:
            return EquipmentSlotType.Ring;
        case GameItemType.Earring:
            return EquipmentSlotType.Earring;
        case GameItemType.Belt:
            return EquipmentSlotType.Belt;

        // Craft itemları, quest itemlar ve collectible'lar equipment değil
        case GameItemType.CraftMaterial:
        case GameItemType.CraftComponent:
        case GameItemType.CraftConsumable:
        case GameItemType.QuestItem:
        case GameItemType.Collectible:  // YENİ
            return EquipmentSlotType.None;

        default:
            return EquipmentSlotType.None;
    }
}
    public bool IsCraftItem()
    {
        bool isCraft = GameItemType == GameItemType.CraftMaterial ||
                       GameItemType == GameItemType.CraftComponent ||
                       GameItemType == GameItemType.CraftConsumable ||
                       GameItemType == GameItemType.Fragment;        // YENİ

        return isCraft;
    }
// SetupAsCraftItem metodundan sonra ekle
public void SetupAsFragment(int stackSize = 999, bool clearExistingStats = true)
{
    maxStackSize = stackSize;
    requiredLevel = 1;

    if (clearExistingStats)
    {
        stats.Clear();
        _selectedStats.Clear();
        currentRarity = GameItemRarity.Normal;
        upgradeLevel = 1;
        armorValue = 0f;
        attackPower = 0f;
    }
}
// CreateCraftCopy metodundan sonra ekle
public ItemData CreateFragmentCopy()
{
    if (!IsFragment())
    {
        return CreateCopy();
    }

    ItemData copy = Instantiate(this);
    copy._baseItemName = this._baseItemName;
    copy.upgradeLevel = 1;
    copy.currentRarity = GameItemRarity.Normal;
    copy._selectedStats = new List<ItemStat>();
    copy.SetupAsFragment(this.maxStackSize, false);

    return copy;
}
// IsFragment metodu ekle (IsCraftItem metodundan sonra)
public bool IsFragment()
{
    return GameItemType == GameItemType.Fragment;
}
public void SetupAsCraftItem(int stackSize = 99, bool clearExistingStats = true)
{
    // isStackable artÄ±k property olduÄŸu iÃ§in GameItemType'a gÃ¶re otomatik belirleniyor
    // Manuel set etmeye gerek yok
    maxStackSize = stackSize;

    // Required level yok
    requiredLevel = 1;

    // dropChance kaldÄ±rÄ±ldÄ±ÄŸÄ± iÃ§in bu satÄ±rÄ± sil

    if (clearExistingStats)
    {
        // TÃ¼m stat sistemlerini temizle
        stats.Clear();
        _selectedStats.Clear();

        // Rarity'yi normal yap
        currentRarity = GameItemRarity.Normal;

        // Upgrade level'Ä± sÄ±fÄ±rla
        upgradeLevel = 1;
    }
}
    public ItemData CreateCraftCopy()
    {
        if (!IsCraftItem())
        {
            return CreateCopy();
        }

        ItemData copy = Instantiate(this);
        copy._baseItemName = this._baseItemName;
        copy.upgradeLevel = 1; // Craft itemlarda upgrade yok
        copy.currentRarity = GameItemRarity.Normal; // Craft itemlarda rarity yok

        // Stat sistemi yok
        copy._selectedStats = new List<ItemStat>();

        // Craft ayarlarÄ±nÄ± uygula
        copy.SetupAsCraftItem(this.maxStackSize, false);

        return copy;
    }
public float DisplayArmorValue
{
    get
    {
        if (useManualStats && manualArmorValue > 0f)
            return manualArmorValue;
        return armorValue;
    }
}

public float DisplayAttackPower
{
    get
    {
        if (useManualStats && manualAttackPower > 0f)
            return manualAttackPower;
        return attackPower;
    }
}
[Header("Fragment Drop Settings")]
[Tooltip("Bu fragment'ın temel düşme şansı (%) - Sadece Fragment tipinde kullanılır")]
[SerializeField] private float fragmentBaseDropChance = 15f;

public float FragmentBaseDropChance 
{ 
    get => fragmentBaseDropChance; 
    set => fragmentBaseDropChance = Mathf.Clamp(value, 0f, 100f);
}
[Header("Manual Stats Settings")]
[Tooltip("Bu item için manuel armor/attack power kullanılacaksa işaretle (Merchant veya Craft için)")]
[SerializeField] public bool useManualStats = false;

[Tooltip("Manuel armor değeri (0 ise otomatik hesaplanır)")]
[SerializeField] public float manualArmorValue = 0f;

[Tooltip("Manuel attack power değeri (0 ise otomatik hesaplanır)")]
[SerializeField] public float manualAttackPower = 0f;
    [Header("Craft Item Quick Setup")]
[SerializeField] private bool isCraftItemSetup = false;

[ContextMenu("Setup As Craft Material")]
private void SetupAsCraftMaterial()
{
    GameItemType = GameItemType.CraftMaterial;
    SetupAsCraftItem(999);
    isCraftItemSetup = true;
    
    #if UNITY_EDITOR
    UnityEditor.EditorUtility.SetDirty(this);
    #endif
}
[ContextMenu("Setup As Quest Item")]
private void SetupAsQuestItemMenu()
{
    GameItemType = GameItemType.QuestItem;
    SetupAsQuestItem(1);
    
    #if UNITY_EDITOR
    UnityEditor.EditorUtility.SetDirty(this);
    #endif
}
[ContextMenu("Setup As Craft Component")]  
private void SetupAsCraftComponent()
{
    GameItemType = GameItemType.CraftComponent;
    SetupAsCraftItem(99);
    isCraftItemSetup = true;
    
    #if UNITY_EDITOR
    UnityEditor.EditorUtility.SetDirty(this);
    #endif
}

[ContextMenu("Setup As Craft Consumable")]
private void SetupAsCraftConsumable()
{
    GameItemType = GameItemType.CraftConsumable;
    SetupAsCraftItem(50);
    isCraftItemSetup = true;
    
    #if UNITY_EDITOR
    UnityEditor.EditorUtility.SetDirty(this);
    #endif
}

    private void OnValidate()
    {
        if (IsCraftItem() && !isCraftItemSetup && !IsFragment())
        {
            SetupAsCraftItem();
            isCraftItemSetup = true;
        }

        if (IsQuestItem())
        {
            if (maxStackSize <= 0) maxStackSize = 1;
            requiredLevel = 1;
        }

        if (IsCollectible())
        {
            if (maxStackSize <= 0) maxStackSize = 999;
            requiredLevel = 1;
        }

        // YENİ
        if (IsFragment())
        {
            if (maxStackSize <= 0) maxStackSize = 999;
            requiredLevel = 1;
        }
    }
// CalculateUpgradeChance() metodundan sonra ekle

public Dictionary<string, int> GetRequiredUpgradeMaterials()
{
    if (IsCraftItem() || IsQuestItem() || IsCollectible()) 
        return new Dictionary<string, int>();
        
    if (upgradeLevel >= MAX_UPGRADE_LEVEL) 
        return new Dictionary<string, int>();

    Dictionary<string, int> materials = new Dictionary<string, int>();
    
    // +1 - +5 arası: sadece bronze
    if (upgradeLevel >= 1 && upgradeLevel < 5)
    {
        materials["yagiz_bronz_parcasi"] = upgradeLevel * 5;
    }
    // +5 - +10 arası: bronze + temper
    else if (upgradeLevel >= 5 && upgradeLevel < 10)
    {
        materials["yagiz_bronz_parcasi"] = upgradeLevel * 5;
        materials["temper_tozu"] = (upgradeLevel - 5) * 5;
    }
    
    return materials;
}
}