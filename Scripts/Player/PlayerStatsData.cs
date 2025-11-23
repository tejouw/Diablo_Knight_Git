// Path: Assets/Game/Scripts/PlayerStatsData.cs

using UnityEngine;
using System;

[CreateAssetMenu(fileName = "PlayerStatsData", menuName = "MMORPG/Player Stats Data")]
public class PlayerStatsData : ScriptableObject
{
    [Serializable]
    public class StatRange
    {
        public float minValue;
        public float maxValue;
        public float baseValue;
        public AnimationCurve levelScalingCurve = AnimationCurve.Linear(1, 1, 99, 2);
        
        public float GetValueAtLevel(int level)
        {
            float scaling = levelScalingCurve.Evaluate(Mathf.Clamp(level, 1, 99));
            return Mathf.Clamp(baseValue * scaling, minValue, maxValue);
        }
    }

    [Serializable]
    public class RegenerationStat
    {
        public float baseValue;
        public float regenRate;
        public float regenTickInterval = 1f;
        public bool regenInCombat = true;
        public float combatRegenMultiplier = 0.5f;
        public AnimationCurve regenCurve = AnimationCurve.Linear(0, 0, 1, 1);
    }

    [Header("Level System")]
    [Tooltip("Maximum character level")]
    public int maxLevel = 99;
    
    [Tooltip("XP required per level")]
    public StatRange xpRequirement = new StatRange 
    { 
        minValue = 100,
        maxValue = 1000000,
        baseValue = 100
    };
    
    [Tooltip("XP loss percentage on death")]
    [Range(0f, 1f)]
    public float xpLossOnDeath = 0.1f;

    [Header("Primary Stats")]
    [Tooltip("Health Points")]
    public StatRange hp = new StatRange 
    { 
        minValue = 100,
        maxValue = 10000,
        baseValue = 100
    };
    


    [Header("Regeneration")]
    [Tooltip("HP regeneration (out of combat)")]
    public RegenerationStat hpRegen = new RegenerationStat
    {
        baseValue = 5,
        regenRate = 1f,
        regenInCombat = false
    };
    


    [Header("Combat Stats")]
    [Tooltip("Base physical damage")]
    public StatRange physicalDamage = new StatRange
    {
        minValue = 5,
        maxValue = 1000,
        baseValue = 10
    };
    
    [Tooltip("Physical defense")]
    public StatRange armor = new StatRange
    {
        minValue = 0,
        maxValue = 500,
        baseValue = 10
    };

    [Header("Critical Hit System")]
    [Tooltip("Critical hit chance percentage")]
    public StatRange criticalChance = new StatRange
    {
        minValue = 0,
        maxValue = 50,
        baseValue = 5
    };
    
    [Tooltip("Critical damage multiplier percentage")]
    public StatRange criticalDamage = new StatRange
    {
        minValue = 150,
        maxValue = 300,
        baseValue = 150
    };

    [Header("Movement & Attack")]
    [Tooltip("Base movement speed")]
    public StatRange moveSpeed = new StatRange
    {
        minValue = 3,
        maxValue = 10,
        baseValue = 5
    };
    
    
    [Tooltip("Base attack speed")]
    public StatRange attackSpeed = new StatRange
    {
        minValue = 0.5f,
        maxValue = 2.5f,
        baseValue = 1f
    };

    [Header("Economic System")]
    [Tooltip("Coin gain multiplier per level")]
    public StatRange coinGainMultiplier = new StatRange
    {
        minValue = 1,
        maxValue = 3,
        baseValue = 1
    };
    
    [Tooltip("XP gain multiplier per level")]
    public StatRange xpGainMultiplier = new StatRange
    {
        minValue = 1,
        maxValue = 3,
        baseValue = 1
    };

    [Header("Combat Formulas")]
    [Tooltip("Global cooldown for all abilities")]
    public float globalCooldown = 1f;

    // Combat damage calculation
    public float CalculatePhysicalDamage(float baseDamage, float targetArmor)
    {
        float damageReduction = targetArmor / (100f + targetArmor);
        return baseDamage * (1f - damageReduction);
    }

    // Health regeneration calculation
    public float GetHealthRegenAmount(float maxHealth, float currentHealthPercentage, bool inCombat)
    {
        float regenMultiplier = hpRegen.regenCurve.Evaluate(currentHealthPercentage);
        float baseRegen = hpRegen.baseValue * regenMultiplier;
        return inCombat && !hpRegen.regenInCombat ? 0f : 
               inCombat ? baseRegen * hpRegen.combatRegenMultiplier : baseRegen;
    }


}