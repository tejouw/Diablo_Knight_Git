// Path: Assets/Game/Scripts/ItemDatabase.cs

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class ItemStat
{
    public StatType type;
    public float value;
}

public enum StatType
{
    Health,            // Can
    PhysicalDamage,    // Fiziksel hasar
    Armor,             // ZÄ±rh
    AttackSpeed,       // SaldÄ±rÄ± hÄ±zÄ±
    CriticalChance,    // Kritik ÅŸansÄ±
    CriticalMultiplier,// Kritik Ã§arpanÄ±
    Range,             // Menzil (silahlar iÃ§in)
    MoveSpeed,         // Hareket hÄ±zÄ±
    HealthRegen,       // Can yenilenmesi
    ProjectileSpeed,    // YENÄ° - Projectile hÄ±zÄ±
    LifeSteal, // IMPLEMENTE EDILMEDI, EDILECEK
    ArmorPenetration, // IMPLEMENTE EDILMEDI, EDILECEK
    Evasion, // IMPLEMENTE EDILMEDI, EDILECEK
    GoldFind, // IMPLEMENTE EDILMEDI, EDILECEK
    ItemRarity, // IMPLEMENTE EDILMEDI, EDILECEK
    DamageVsElites // IMPLEMENTE EDILMEDI, EDILECEK
}

public enum GameItemType
{
    // Silahlar
    MeleeWeapon2H,
    CompositeWeapon,
    
    // Zırhlar
    Helmet,
    Bracers,
    ChestArmor,
    Leggings,
    
    // Takılar
    Ring,
    Earring,
    Belt,
    
    // Craft Sistemleri
    CraftMaterial,
    CraftComponent,
    CraftConsumable,
    Fragment,        // YENİ - Otomatik toplanan craft item

    QuestItem,
    Collectible
}
// Equipment slot tipleri
public enum EquipmentSlotType
{
    None,
    MeleeWeapon2H,  // YakÄ±n dÃ¶vÃ¼ÅŸ silah slotu
    CompositeWeapon, // Uzak dÃ¶vÃ¼ÅŸ silah slotu
    Head,         // Kafa slotu
    Bracers,        // El slotu
    Chest,        // GÃ¶ÄŸÃ¼s slotu
    Leggings,         // Ayak slotu
    Ring,         // YÃ¼zÃ¼k slotu
    Earring,      // KÃ¼pe slotu
    Belt,         // Kemer slotu
    
    // Craft iÃ§in slot tanÄ±mlamÄ±yoruz - inventory'de stacklenecek
}

public class ItemDatabase : MonoBehaviour
{
    // ItemDatabase.cs  (alan ekle)
[Header("Weapon Items")]
[SerializeField] private List<ItemData> weaponItems = new List<ItemData>();

    [Header("Quest Items")]
[SerializeField] private List<ItemData> questItems = new List<ItemData>();
    [Header("Craft Items")]
[SerializeField] private List<ItemData> craftItems = new List<ItemData>();
    public static ItemDatabase Instance;
    
    [SerializeField] private List<ItemData> allItems = new List<ItemData>();
    private Dictionary<string, ItemData> itemLookup = new Dictionary<string, ItemData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeDatabase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

// ItemDatabase.cs  (InitializeDatabase metodunu güncelle)
private void InitializeDatabase()
{
    itemLookup.Clear();

    // Normal item'ları işle
    foreach (ItemData item in allItems)
    {
        if (item != null && !string.IsNullOrEmpty(item.itemId))
        {
            if (!itemLookup.ContainsKey(item.itemId))
                itemLookup.Add(item.itemId, item);
        }
    }

    // Craft item'ları işle
    foreach (ItemData craftItem in craftItems)
    {
        if (craftItem != null && !string.IsNullOrEmpty(craftItem.itemId))
        {
            if (!itemLookup.ContainsKey(craftItem.itemId))
                itemLookup.Add(craftItem.itemId, craftItem);
        }
    }

    // Quest item'ları işle
    foreach (ItemData questItem in questItems)
    {
        if (questItem != null && !string.IsNullOrEmpty(questItem.itemId))
        {
            if (!itemLookup.ContainsKey(questItem.itemId))
                itemLookup.Add(questItem.itemId, questItem);
        }
    }

    // YENİ: Weapon item'ları işle (global sözlüğe otomatik kaydet)
    foreach (ItemData weapon in weaponItems)
    {
        if (weapon != null && !string.IsNullOrEmpty(weapon.itemId))
        {
            if (!itemLookup.ContainsKey(weapon.itemId))
                itemLookup.Add(weapon.itemId, weapon);
        }
    }
}

public List<ItemData> GetAllQuestItems()
{
    return new List<ItemData>(questItems);
}

public ItemData GetQuestItemById(string questItemId)
{
    return questItems.FirstOrDefault(item => item.itemId == questItemId);
}

public int GetQuestItemCount()
{
    return questItems.Count;
}
public List<ItemData> GetAllCraftItems()
{
    return new List<ItemData>(craftItems);
}

public List<ItemData> GetCraftItemsByType(GameItemType craftType)
{
    return craftItems.Where(item => item.GameItemType == craftType).ToList();
}

public int GetCraftItemCount()
{
    return craftItems.Count;
}

public ItemData GetRandomCraftItem()
{
    if (craftItems.Count == 0) return null;
    return craftItems[UnityEngine.Random.Range(0, craftItems.Count)];
}

// ItemDatabase.cs  (GetNormalItems metodunu güncelle)
public List<ItemData> GetNormalItems()
{
    return allItems
        .Concat(weaponItems)                     // YENİ: silahları da normal item havuzuna dahil et
        .Where(item => item != null && !item.IsCraftItem())
        .Distinct()
        .ToList();
}


    public ItemData GetItemById(string id)
    {
        if (itemLookup.TryGetValue(id, out ItemData item))
        {
            return item;
        }
        return null;
    }

}