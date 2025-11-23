using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Class", menuName = "Skill System/Class Data")]
public class ClassData : ScriptableObject
{
    [Header("Class Info")]
    public ClassType classType;
    public string className;
    public string description;
    public Sprite classIcon;
    
    [Header("Skills")]
    public List<SkillData> utilitySkills = new List<SkillData>();
    public List<SkillData> combatSkills = new List<SkillData>();
    public List<SkillData> ultimateSkills = new List<SkillData>();
    public List<SkillData> passiveSkills = new List<SkillData>();
    
    public List<SkillData> GetSkillsByType(SkillType type)
    {
        return type switch
        {
            SkillType.Utility => utilitySkills,
            SkillType.Combat => combatSkills,
            SkillType.Ultimate => ultimateSkills,
            _ => new List<SkillData>()
        };
    }
}