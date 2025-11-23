using System;

[Serializable]
public enum SkillType
{
    Utility,    // Kaçış, kalkan, buff
    Combat,     // Kısa cooldown, hasar/CC
    Ultimate    // Uzun cooldown, yüksek etki
}

// SkillEnums.cs
[Serializable]
public enum ClassType
{
    None,       // Sınıf seçmemiş (default)
    Warrior,
    Ranger,     // Archer yerine
    Rogue
}

[Serializable]
public enum SkillSlot
{
    Skill1 = 0, // Utility
    Skill2 = 1, // Combat  
    Skill3 = 2  // Ultimate
}

[Serializable]
public enum PassiveSkillSlot
{
    Passive1 = 0,
    Passive2 = 1
}