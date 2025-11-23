using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Skill", menuName = "Skill System/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Basic Info")]
    public string skillId;
    public string skillName;
    public string description;
    public Sprite skillIcon;
    
    [Header("Skill Properties")]
    public SkillType skillType;
    public ClassType requiredClass;
    public int requiredLevel;
    public bool isPassiveSkill;
    
    [Header("Cooldown & Costs")]
    public float baseCooldown;
    
    [Header("Skill Progression")]
    public int maxSkillLevel = 10;
    public List<SkillLevelData> levelProgression = new List<SkillLevelData>();
    
    [Header("XP Requirements")]
    public List<int> xpRequirements = new List<int>(); // Her seviye için gereken XP
    
    public SkillLevelData GetDataForLevel(int level)
    {
        if (level <= 0 || level > levelProgression.Count) 
            return levelProgression.Count > 0 ? levelProgression[0] : null;
        
        return levelProgression[level - 1];
    }
    
    public int GetXPRequirement(int level)
    {
        if (level <= 1 || level > xpRequirements.Count) return 0;
        return xpRequirements[level - 2]; // Level 2 için index 0
    }
}

[System.Serializable]
public class SkillLevelData
{
    [Header("Level Stats")]
    public int level;
    public float cooldownMultiplier = 1f;
    public float damageMultiplier = 1f;
    public float durationMultiplier = 1f;
    public float rangeMultiplier = 1f;
    
    [Header("Level Description")]
    public string levelDescription;
}