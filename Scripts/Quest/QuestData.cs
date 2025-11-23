// Path: Assets/Game/Scripts/QuestData.cs

using UnityEngine;
using System.Collections.Generic;
using System;

public enum QuestType
{
    KillMonsters,
    CollectItems,
    ReachLocation,
    TalkToNPC,
    BindToBindstone,
    PickupEquipment,
    BuyFromMerchant,
    EquipItems,
    EquipUpgradedItems  // YENİ
}

public enum QuestRaceRequirement
{
    All = -1,      // Tüm ırklar için geçerli
    Human = 0,     // Sadece Human için
    Goblin = 1     // Sadece Goblin için
}

[Serializable]
public class AlternativeTargetDialogue
{
    [Tooltip("Hedef NPC'nin ID'si (alternativeTargetIds ile eşleşmeli)")]
    public string targetId;

    [Tooltip("Bu hedef için gösterilecek özel diyaloglar")]
    public string[] dialogues;
}

[Serializable]
public class QuestObjective
{
    public QuestType type;
    public string targetId;
    public int requiredAmount;
    public int currentAmount;
    
    [Header("Quest Description")]
    public string description;
    
    [Header("Compass Settings")]
    public bool useCompass = false;
    public string compassCoordinates = "";
    
    [Header("Alternative Targets")]
    [Tooltip("Birden fazla hedef ID. Bunlardan biri tamamlanınca objective tamamlanır")]
    public string[] alternativeTargetIds;
    
    [Header("Optional Item Give")]
    [Tooltip("Bu objective tamamlanırken player'dan item alınacak mı?")]
    public bool requiresItemGive = false;

    [Tooltip("Alınacak item'ın ID'si (requiresItemGive true ise zorunlu)")]
    public string requiredItemId = "";

    [Tooltip("Kaç adet item alınacak (varsayılan 1)")]
    public int requiredItemAmount = 1;

    // YENİ - EquipUpgradedItems için
    [Header("Upgrade Level Settings")]
    [Tooltip("EquipUpgradedItems quest tipi için minimum upgrade level (0 = kontrol edilmez)")]
    public int minUpgradeLevel = 0;

    public bool IsCompleted 
    { 
        get 
        {
            if (type == QuestType.ReachLocation)
            {
                return currentAmount >= 1;
            }
            
            return currentAmount >= requiredAmount;
        } 
    }

    // ✅ YENİ METOD: Herhangi bir hedef eşleşiyor mu kontrol et
    public bool MatchesTarget(string checkTargetId)
    {
        // Ana hedef kontrolü
        if (targetId == checkTargetId)
            return true;

        // Alternatif hedefler kontrolü
        if (alternativeTargetIds != null && alternativeTargetIds.Length > 0)
        {
            foreach (string altTarget in alternativeTargetIds)
            {
                if (altTarget == checkTargetId)
                    return true;
            }
        }

        return false;
    }
    [Header("Objective-Specific Dialogues")]
[Tooltip("Bu objective için özel diyaloglar. DialogOnly mode'daki NPC'ler için kullanılır. Boşsa NPC'nin varsayılan diyalogları gösterilir.")]
public string[] objectiveDialogues;

[Tooltip("Alternative target'ların her biri için özel diyaloglar. targetId ile eşleştirilir.")]
public AlternativeTargetDialogue[] alternativeTargetDialogues;

    // ✅ YENİ METOD: Belirli bir target için diyalogları getir
    public string[] GetDialoguesForTarget(string checkTargetId)
    {

        // Ana hedef kontrolü
        if (targetId == checkTargetId)
        {
            // Ana hedef için objectiveDialogues döndür
            if (objectiveDialogues != null && objectiveDialogues.Length > 0)
            {
                return objectiveDialogues;
            }
            else
            {
            }
        }

        // Alternatif hedefler için özel diyalogları kontrol et
        if (alternativeTargetDialogues != null && alternativeTargetDialogues.Length > 0)
        {

            for (int i = 0; i < alternativeTargetDialogues.Length; i++)
            {
                var altDialogue = alternativeTargetDialogues[i];

                if (altDialogue.targetId == checkTargetId)
                {

                    if (altDialogue.dialogues != null && altDialogue.dialogues.Length > 0)
                    {
                        return altDialogue.dialogues;
                    }
                    else
                    {
                    }
                }
            }
        }
        else
        {
        }

        // Eşleşme yoksa null döndür
        return null;
    }
}

[Serializable]
public class QuestReward
{
    public int xpReward;
    public int coinReward;
    public List<string> itemRewards = new List<string>();
    public int potionReward;
}

[CreateAssetMenu(fileName = "New Quest", menuName = "MMORPG/Quest")]
public class QuestData : ScriptableObject
{
[Header("Quest Bilgileri")]
public string questId;
public string questName;
public Sprite questIcon;

[Header("Race Requirement")]
[Tooltip("Bu quest hangi ırk için geçerli? All = Tüm ırklar")]
public QuestRaceRequirement raceRequirement = QuestRaceRequirement.All;

// ✅ YENİ EKLENEN
[Header("Quest Açıklaması (Opsiyonel)")]
[TextArea(2, 4)]
[Tooltip("Quest'in genel açıklaması. Boş bırakılırsa gösterilmez.")]
public string questDescription = "";

[Header("Chain Quest Ayarları")]
public string previousQuestId;
public string nextQuestId;
    
[Header("Quest Hedefleri")]
public List<QuestObjective> objectives = new List<QuestObjective>();

// ✅ YENİ EKLENEN
[Header("Hidden Objective (Opsiyonel)")]
[Tooltip("Ana objective'ler tamamlandıktan sonra aktif olacak gizli hedef")]
public QuestObjective hiddenObjective;
    
    [Header("Quest Ödülleri")]
    public QuestReward rewards = new QuestReward();
    
    [Header("Quest NPC")]
    public string questGiverNPC;   // Quest veren NPC'nin ID'si
    public string questTurnInNPC;  // Quest tamamlandığında konuşulacak NPC (farklı olabilir)

    [Header("Quest Diyalogları")]
    public string startDialogue;   // Görevi almadan önce
    public string progressDialogue; // Görev devam ederken
    public string completionDialogue; // Görev tamamlandığında

[Header("Dialog Quest Settings")]
public bool isDialogQuest = false; // Dialog quest olup olmadığını belirler

[Header("Dialog Quest - Multiple Dialogs")]
[Tooltip("Birden fazla başlangıç diyalogu (Max 3)")]
public string[] startDialogues = new string[0]; // Multiple start dialogs

[Tooltip("Birden fazla ilerleme diyalogu (Max 3)")]  
public string[] progressDialogues = new string[0]; // Multiple progress dialogs

[Tooltip("Birden fazla tamamlama diyalogu (Max 3)")]
public string[] completionDialogues = new string[0]; // Multiple completion dialogs

// Dialog quest için otomatik completion kontrolü
public bool HasCompletionDialogs => completionDialogues != null && completionDialogues.Length > 0;
    
    // Quest tamamlanma durumunu kontrol et
    public bool IsCompleted()
    {
        foreach (var objective in objectives)
        {
            if (!objective.IsCompleted)
                return false;
        }
        return true;
    }
    
    // İlerleme durumunu sıfırla
    public void ResetProgress()
    {
        foreach (var objective in objectives)
        {
            objective.currentAmount = 0;
        }
    }

    // Quest'in belirli bir ırk için uygun olup olmadığını kontrol et
    public bool IsAvailableForRace(PlayerRace playerRace)
    {
        // All = tüm ırklar için geçerli
        if (raceRequirement == QuestRaceRequirement.All)
            return true;

        // Irk bazlı kontrol
        return (int)raceRequirement == (int)playerRace;
    }
}