// Path: Assets/Game/Scripts/ItemStatSystem.cs

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class ItemStatSystem
{
private static readonly Dictionary<EquipmentSlotType, List<StatType>> SlotWhitelist = new Dictionary<EquipmentSlotType, List<StatType>>()
{
    { EquipmentSlotType.MeleeWeapon2H, new List<StatType> { StatType.PhysicalDamage, StatType.AttackSpeed, StatType.CriticalChance, StatType.CriticalMultiplier, StatType.ArmorPenetration, StatType.DamageVsElites, StatType.LifeSteal } },
    { EquipmentSlotType.CompositeWeapon, new List<StatType> { StatType.PhysicalDamage, StatType.AttackSpeed, StatType.CriticalChance, StatType.CriticalMultiplier, StatType.Range, StatType.ProjectileSpeed, StatType.ArmorPenetration, StatType.DamageVsElites, StatType.LifeSteal } },
    { EquipmentSlotType.Head, new List<StatType> { StatType.Health, StatType.HealthRegen, StatType.Evasion, StatType.CriticalChance } }, // Armor kaldırıldı
    { EquipmentSlotType.Bracers, new List<StatType> { StatType.AttackSpeed, StatType.CriticalChance, StatType.CriticalMultiplier, StatType.ArmorPenetration, StatType.Evasion } },
    { EquipmentSlotType.Chest, new List<StatType> { StatType.Health, StatType.HealthRegen, StatType.Evasion } }, // Armor kaldırıldı
    { EquipmentSlotType.Leggings, new List<StatType> { StatType.MoveSpeed, StatType.Health, StatType.HealthRegen, StatType.Evasion } }, // Armor kaldırıldı
    { EquipmentSlotType.Ring, new List<StatType> { StatType.CriticalChance, StatType.CriticalMultiplier, StatType.AttackSpeed, StatType.ArmorPenetration, StatType.LifeSteal, StatType.DamageVsElites, StatType.GoldFind, StatType.ItemRarity } },
    { EquipmentSlotType.Earring, new List<StatType> { StatType.CriticalChance, StatType.AttackSpeed, StatType.CriticalMultiplier, StatType.LifeSteal, StatType.ArmorPenetration, StatType.DamageVsElites, StatType.GoldFind, StatType.ItemRarity } },
    { EquipmentSlotType.Belt, new List<StatType> { StatType.Health, StatType.HealthRegen, StatType.LifeSteal, StatType.Evasion, StatType.GoldFind, StatType.ItemRarity } } // Armor kaldırıldı
};

    // Base deÄŸerler
// Base deÄŸerler - hiÃ§biri 0 olmasÄ±n
private static readonly Dictionary<StatType, float> BaseValues = new Dictionary<StatType, float>()
{
    { StatType.PhysicalDamage, 2f },
    { StatType.Armor, 3f },
    { StatType.Health, 20f },
    { StatType.AttackSpeed, 1f },      // 0f -> 1f (%1 base attack speed)
    { StatType.CriticalChance, 0.5f }, // 0f -> 0.5f (%0.5 base crit chance)
    { StatType.CriticalMultiplier, 2f }, // 0f -> 2f (%2 base crit multiplier)
    { StatType.Range, 0.1f },          // 0f -> 0.1f (0.1 base range)
    { StatType.MoveSpeed, 1f },        // 0f -> 1f (%1 base move speed)
    { StatType.HealthRegen, 0.5f },
    { StatType.ProjectileSpeed, 1f },  // 0f -> 1f (%1 base projectile speed)
    { StatType.LifeSteal, 0.1f },      // 0f -> 0.1f (%0.1 base life steal)
    { StatType.ArmorPenetration, 1f }, // 0f -> 1f (1 base armor pen)
    { StatType.Evasion, 0.5f },        // 0f -> 0.5f (%0.5 base evasion)
    { StatType.GoldFind, 2f },
    { StatType.ItemRarity, 1f },
    { StatType.DamageVsElites, 2f }
};

    // Per Level deÄŸerler
    private static readonly Dictionary<StatType, float> PerLevelValues = new Dictionary<StatType, float>()
    {
        { StatType.PhysicalDamage, 0.9f },
        { StatType.Armor, 0.5f },
        { StatType.Health, 10f },
        { StatType.AttackSpeed, 0.04f },
        { StatType.CriticalChance, 0.08f },
        { StatType.CriticalMultiplier, 0.25f },
        { StatType.Range, 0.02f },
        { StatType.MoveSpeed, 0.03f },
        { StatType.HealthRegen, 0.15f },
        { StatType.ProjectileSpeed, 0.03f },
        { StatType.LifeSteal, 0.0012f },
        { StatType.ArmorPenetration, 0.8f },
        { StatType.Evasion, 0.5f },
        { StatType.GoldFind, 0.6f },
        { StatType.ItemRarity, 0.28f },
        { StatType.DamageVsElites, 0.46f }
    };
// Bu metodları ItemStatSystem sınıfına ekle
public static float GetBaseValue(StatType statType)
{
    return BaseValues.TryGetValue(statType, out float value) ? value : 0f;
}

public static float GetPerLevelValue(StatType statType)  
{
    return PerLevelValues.TryGetValue(statType, out float value) ? value : 0f;
}

public static float GetRarityMultiplier(GameItemRarity rarity)
{
    return RarityMultipliers.TryGetValue(rarity, out float value) ? value : 1f;
}
    // Rarity multiplier
    private static readonly Dictionary<GameItemRarity, float> RarityMultipliers = new Dictionary<GameItemRarity, float>()
    {
        { GameItemRarity.Normal, 0.95f },
        { GameItemRarity.Magic, 1.05f },
        { GameItemRarity.Rare, 1.20f }
    };

    // Stat cap'leri
    private static readonly Dictionary<StatType, float> StatCaps = new Dictionary<StatType, float>()
    {
        { StatType.CriticalChance, 60f },
        { StatType.AttackSpeed, 50f },
        { StatType.MoveSpeed, 30f },
        { StatType.Evasion, 25f },
        { StatType.LifeSteal, 6f },
        { StatType.ArmorPenetration, 40f },
        { StatType.ItemRarity, 15f }
    };

    public static List<StatType> GetWhitelistForSlot(EquipmentSlotType slotType)
    {
        return SlotWhitelist.TryGetValue(slotType, out List<StatType> whitelist) ? 
               new List<StatType>(whitelist) : new List<StatType>();
    }

    public static float CalculateStatValue(StatType statType, int itemLevel, GameItemRarity rarity)
    {
        float baseValue = BaseValues.TryGetValue(statType, out float baseVal) ? baseVal : 0f;
        float perLevelValue = PerLevelValues.TryGetValue(statType, out float perLvl) ? perLvl : 0f;
        float rarityMultiplier = RarityMultipliers.TryGetValue(rarity, out float rarityMul) ? rarityMul : 1f;
        
        // FormÃ¼l: (Base + perLvl * ilvl) * RarityMul * random(0.85â€“1.15)
        float randomMultiplier = Random.Range(0.85f, 1.15f);
        float finalValue = (baseValue + perLevelValue * itemLevel) * rarityMultiplier * randomMultiplier;
        
        // Cap kontrolÃ¼
        if (StatCaps.TryGetValue(statType, out float cap))
        {
            finalValue = Mathf.Min(finalValue, cap);
        }
        
        return finalValue;
    }

    public static int GetAffixCountForRarity(GameItemRarity rarity)
    {
        return rarity switch
        {
            GameItemRarity.Normal => 1,
            GameItemRarity.Magic => 2,
            GameItemRarity.Rare => 3,
            _ => 1
        };
    }
}