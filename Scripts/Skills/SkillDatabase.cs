using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "Skill Database", menuName = "Skill System/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    public static SkillDatabase Instance { get; private set; }

    [Header("Classes")]
    public List<ClassData> classes = new List<ClassData>();

    [Header("Default Skills (No Class)")]
    public List<SkillData> defaultUtilitySkills = new List<SkillData>();
    public List<SkillData> defaultCombatSkills = new List<SkillData>();
    public List<SkillData> defaultUltimateSkills = new List<SkillData>();

    private Dictionary<string, SkillData> skillsById;
    private Dictionary<ClassType, ClassData> classesByType;

private void OnEnable()
{
    Instance = this;
    InitializeDictionaries();
    
    // Class'ları kontrol et
    foreach (var classData in classes)
    {
        if (classData != null)
        {
        }
    }
}

    private void InitializeDictionaries()
    {
        skillsById = new Dictionary<string, SkillData>();
        classesByType = new Dictionary<ClassType, ClassData>();

        // Sınıf skilllerini indexle
        if (classes != null)
        {
            foreach (var classData in classes)
            {
                // NULL CHECK - AWS build crash prevention
                if (classData == null)
                {
                    Debug.LogWarning("[SkillDatabase] Null ClassData found in classes list. Skipping.");
                    continue;
                }

                classesByType[classData.classType] = classData;

                IndexSkills(classData.utilitySkills);
                IndexSkills(classData.combatSkills);
                IndexSkills(classData.ultimateSkills);
                IndexSkills(classData.passiveSkills);
            }
        }

        // Default skilleri indexle
        IndexSkills(defaultUtilitySkills);
        IndexSkills(defaultCombatSkills);
        IndexSkills(defaultUltimateSkills);
    }

    private void IndexSkills(List<SkillData> skills)
    {
        // NULL CHECK - Prevent crash if list is null
        if (skills == null) return;

        foreach (var skill in skills)
        {
            if (skill != null && !string.IsNullOrEmpty(skill.skillId))
            {
                skillsById[skill.skillId] = skill;
            }
        }
    }

public SkillData GetSkillById(string skillId)
{
    
    if (skillsById != null)
    {
        foreach (var kvp in skillsById)
        {
        }
    }
    
    var result = skillsById?.TryGetValue(skillId, out var skill) == true ? skill : null;
    
    return result;
}
    public ClassData GetClassData(ClassType classType)
    {
        return classesByType.TryGetValue(classType, out var classData) ? classData : null;
    }

public List<SkillData> GetAvailableSkills(ClassType classType, SkillType skillType, int playerLevel)
{
    List<SkillData> skills;

    if (classType == ClassType.None)
    {
        skills = skillType switch
        {
            SkillType.Utility => defaultUtilitySkills,
            SkillType.Combat => defaultCombatSkills,
            SkillType.Ultimate => defaultUltimateSkills,
            _ => new List<SkillData>()
        };
    }
    else
    {
        var classData = GetClassData(classType);
        
        if (classData != null)
        {
            skills = classData.GetSkillsByType(skillType);
        }
        else
        {
            Debug.LogError($"[SkillDatabase] {classType} için ClassData bulunamadı!");
            skills = new List<SkillData>();
        }
    }

    if (skills != null)
    {
        foreach (var skill in skills)
        {
            if (skill != null)
            {
            }
        }
    }

    var filteredSkills = skills?.Where(s => s != null && s.requiredLevel <= playerLevel).ToList() ?? new List<SkillData>();
    

    return filteredSkills;
}

    public List<SkillData> GetAvailablePassiveSkills(ClassType classType, int playerLevel)
    {
        if (classType == ClassType.None) return new List<SkillData>();

        var classData = GetClassData(classType);
        return classData?.passiveSkills.Where(s => s != null && s.requiredLevel <= playerLevel).ToList()
               ?? new List<SkillData>();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void LoadInstance()
{
    if (Instance == null)
    {
        Instance = Resources.Load<SkillDatabase>("SkillDatabase");
        if (Instance != null)
        {
            Instance.InitializeDictionaries();
        }
    }
}
}