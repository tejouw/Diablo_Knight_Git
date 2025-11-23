using System;
using UnityEngine;

[Serializable]
public class SkillInstance
{
    public string skillId;
    public int currentLevel = 1;
    public int currentXP = 0;
    public float lastUsedTime = 0f;
    
    [NonSerialized]
    public SkillData skillData;
    
    public SkillInstance(string id)
    {
        skillId = id;
        currentLevel = 1;
        currentXP = 0;
    }
    
public bool IsOnCooldown(float currentTime)
{
    if (skillData == null) 
    {
        Debug.LogError($"[SkillInstance] IsOnCooldown called but skillData is null for {skillId}");
        return false;
    }
    
    var levelData = skillData.GetDataForLevel(currentLevel);
    
    float cooldownMultiplier = 1f; // Default
    if (levelData != null)
    {
        cooldownMultiplier = levelData.cooldownMultiplier;
    }
    
    float actualCooldown = skillData.baseCooldown * cooldownMultiplier;
    float timeDifference = currentTime - lastUsedTime;
    
    bool isOnCooldown = timeDifference < actualCooldown;

    
    return isOnCooldown;
}
    
public float GetRemainingCooldown(float currentTime)
{
    if (skillData == null) return 0f;
    
    var levelData = skillData.GetDataForLevel(currentLevel);
    
    float cooldownMultiplier = 1f; // Default
    if (levelData != null)
    {
        cooldownMultiplier = levelData.cooldownMultiplier;
    }
    
    float actualCooldown = skillData.baseCooldown * cooldownMultiplier;
    float elapsed = currentTime - lastUsedTime;
    return Mathf.Max(0f, actualCooldown - elapsed);
}
    
    public void AddXP(int amount)
    {
        if (skillData == null || currentLevel >= skillData.maxSkillLevel) return;
        
        currentXP += amount;
        
        // Seviye atlama kontrolü
        while (currentLevel < skillData.maxSkillLevel)
        {
            int requiredXP = skillData.GetXPRequirement(currentLevel + 1);
            if (requiredXP == 0 || currentXP < requiredXP) break;
            
            currentXP -= requiredXP;
            currentLevel++;
        }
    }
    
    public bool CanLevelUp()
    {
        if (skillData == null || currentLevel >= skillData.maxSkillLevel) return false;
        
        int requiredXP = skillData.GetXPRequirement(currentLevel + 1);
        return requiredXP > 0 && currentXP >= requiredXP;
    }
}