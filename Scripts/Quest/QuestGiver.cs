// QuestGiver.cs - Önceki inheritance yerine bileşen yapısına dönüştürülmüş

using UnityEngine;
using Fusion;
using UnityEngine.UI;
using System.Collections.Generic;

public class QuestGiver : NetworkBehaviour
{
[Header("Quest Marker Settings")]
// questMarkerObject field'ını kaldır - gereksiz
private GameObject questMarkerObject; // SerializeField kaldırıldı
private Image questMarkerImage;
    private QuestStatus currentQuestStatus = QuestStatus.NotStarted;
    private string currentQuestId;
    
    private BaseNPC parentNPC;
    
private void Awake()
{
    parentNPC = GetComponent<BaseNPC>();
    
    if (parentNPC == null)
    {
        Debug.LogError("[QuestGiver] BaseNPC bileşeni bulunamadı!");
        return;
    }
    
    
    // QuestManager eventlerini dinle
    if (QuestManager.Instance != null)
    {
        QuestManager.Instance.OnQuestStarted += OnQuestStatusChanged;
        QuestManager.Instance.OnQuestCompleted += OnQuestStatusChanged;
        QuestManager.Instance.OnQuestTurnedIn += OnQuestStatusChanged;
    }
}
    
    private void OnDestroy()
    {
        // Event listener'ları temizle
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStarted -= OnQuestStatusChanged;
            QuestManager.Instance.OnQuestCompleted -= OnQuestStatusChanged;
            QuestManager.Instance.OnQuestTurnedIn -= OnQuestStatusChanged;
        }
    }
    
private void Start()
{
    // Quest marker'ı oluştur
    CreateQuestMarker();
    
    // NPC ilk yüklendiğinde quest durumunu kontrol et
    if (Runner != null && Runner.IsRunning)
    {
        UpdateQuestMarker();
    }
}
    
private void CreateQuestMarker()
{
    if (parentNPC == null)
    {
        Debug.LogError("[QuestGiver] CreateQuestMarker - parentNPC null");
        return;
    }
    
    Canvas npcCanvas = parentNPC.GetNPCCanvas();
    if (npcCanvas == null)
    {
        Debug.LogError($"[QuestGiver] {parentNPC.NPCName} - NPC Canvas bulunamadı");
        return;
    }
    
    // Prefab'dan quest marker'ı bul
    questMarkerObject = npcCanvas.transform.Find("QuestMarker")?.gameObject;
    
    if (questMarkerObject == null)
    {
        Debug.LogError($"[QuestGiver] {parentNPC.NPCName} - Canvas prefab'ında QuestMarker bulunamadı!");
        return;
    }

    // Bileşenleri al
    questMarkerImage = questMarkerObject.GetComponent<Image>();
    if (questMarkerImage == null)
    {
        Debug.LogError($"[QuestGiver] {parentNPC.NPCName} - QuestMarker'da Image component bulunamadı!");
        return;
    }
    
    // Başlangıçta kapalı
    questMarkerObject.SetActive(false);
}
    
    private void OnQuestStatusChanged(string questId)
    {
        // Quest durumu değiştiğinde marker'ı güncelle
        UpdateQuestMarker();
    }
public bool HasActiveQuest()
{
    if (parentNPC == null || QuestManager.Instance == null)
        return false;
        
    // Önce quest durumunu güncelle  
    UpdateQuestMarker();
    
    // Aktif quest durumu var mı kontrol et
    bool hasAvailable = QuestManager.Instance.HasAvailableQuestFromNPC(parentNPC.NPCName);
    bool hasCompleted = QuestManager.Instance.HasCompletedQuestForNPC(parentNPC.NPCName);
    
    return hasAvailable || hasCompleted;
}
public void UpdateQuestMarker()
{
    // Kontrolleri tek tek yap ve hangisinin eksik olduğunu logla
    if (Runner == null || !Runner.IsRunning)
    {
        return;
    }
    
    if (QuestManager.Instance == null)
    {
        return;
    }
    
    if (questMarkerObject == null)
    {
        return;
    }
    
    if (parentNPC == null)
    {
        return;
    }

    bool shouldShowMarker = false;
    QuestStatus newStatus = QuestStatus.NotStarted;
    
    // 1. ÖNCELİK: Bu NPC ile konuşma objective'i olan aktif quest var mı? (Dialog quest için completed marker)
    if (HasActiveTalkToNPCObjective(parentNPC.NPCName))
    {
        shouldShowMarker = true;
        newStatus = QuestStatus.Completed;
    }
    // 2. Bu NPC'den alınabilecek quest var mı?
    else if (QuestManager.Instance.HasAvailableQuestFromNPC(parentNPC.NPCName))
    {
        shouldShowMarker = true;
        newStatus = QuestStatus.NotStarted;
        
        QuestData availableQuest = QuestManager.Instance.GetAvailableQuestFromNPC(parentNPC.NPCName);
        if (availableQuest != null)
        {
            currentQuestId = availableQuest.questId;
        }
    }
    // 3. Bu NPC'ye teslim edilebilecek quest var mı?
    else if (QuestManager.Instance.HasCompletedQuestForNPC(parentNPC.NPCName))
    {
        shouldShowMarker = true;
        newStatus = QuestStatus.Completed;
        
        QuestData completedQuest = QuestManager.Instance.GetCompletedQuestForNPC(parentNPC.NPCName);
        if (completedQuest != null)
        {
            currentQuestId = completedQuest.questId;
        }
    }
    // 4. Bu NPC'den alınmış ama henüz tamamlanmamış quest var mı?
    else if (QuestManager.Instance.HasInProgressQuestFromNPC(parentNPC.NPCName))
    {
        shouldShowMarker = true;
        newStatus = QuestStatus.InProgress;
        
        QuestData inProgressQuest = QuestManager.Instance.GetInProgressQuestFromNPC(parentNPC.NPCName);
        if (inProgressQuest != null)
        {
            currentQuestId = inProgressQuest.questId;
        }
    }
    
    // Durumu güncelle
    currentQuestStatus = newStatus;
    
    if (shouldShowMarker)
    {
        QuestMarkerStyle animationStyle = QuestMarkerStyle.Available;
        
        if (questMarkerImage != null)
        {
            switch (newStatus)
            {
                case QuestStatus.NotStarted:
                    questMarkerImage.sprite = QuestMarkerIcons.AvailableQuestIcon;
                    animationStyle = QuestMarkerStyle.Available;
                    break;
                case QuestStatus.InProgress:
                    questMarkerImage.sprite = QuestMarkerIcons.ActiveQuestIcon;
                    animationStyle = QuestMarkerStyle.Active;
                    break;
                case QuestStatus.Completed:
                    questMarkerImage.sprite = QuestMarkerIcons.CompletedQuestIcon;
                    animationStyle = QuestMarkerStyle.Completed;
                    break;
            }
            questMarkerImage.color = Color.white;
        }
        
        // Animasyon stilini ayarla
        QuestMarkerAnimator animator = questMarkerObject.GetComponent<QuestMarkerAnimator>();
        if (animator != null)
        {
            animator.SetAnimationStyle(animationStyle);
        }
        
        questMarkerObject.SetActive(true);
    }
    else
    {
        questMarkerObject.SetActive(false);
    }
}

private bool HasActiveTalkToNPCObjective(string npcName)
{
    if (QuestManager.Instance == null) return false;
    
    var activeQuests = QuestManager.Instance.GetActiveQuests();
    
    foreach (var playerQuest in activeQuests)
    {
        if (playerQuest.status == QuestStatus.InProgress)
        {
            // Ana objectives'i kontrol et
            foreach (var objective in playerQuest.objectives)
            {
                if (objective.type == QuestType.TalkToNPC && 
                    objective.MatchesTarget(npcName) &&
                    !objective.IsCompleted)
                {
                    return true;
                }
            }
            
            // ✅ YENİ: Hidden objective'i de kontrol et
            if (playerQuest.isHiddenObjectiveActive && 
                playerQuest.hiddenObjective != null &&
                playerQuest.hiddenObjective.type == QuestType.TalkToNPC &&
                playerQuest.hiddenObjective.MatchesTarget(npcName) &&
                !playerQuest.hiddenObjective.IsCompleted)
            {
                return true;
            }
        }
    }
    
    return false;
}

public void HandleNPCInteraction()
{
    
    if (parentNPC == null || !parentNPC.IsPlayerInRange() || QuestManager.Instance == null)
    {
        Debug.LogError("[QuestGiver] HandleNPCInteraction - Gerekli bileşenler eksik!");
        return;
    }

    UpdateQuestMarker();
    CheckAndCompleteTalkToNPCQuests(); // Bu metod içinde de item kontrolü yapılacak
    

    if (currentQuestStatus == QuestStatus.NotStarted)
    {
        if (QuestManager.Instance.HasAvailableQuestFromNPC(parentNPC.NPCName))
        {
            QuestData questData = QuestManager.Instance.GetAvailableQuestFromNPC(parentNPC.NPCName);
            if (questData != null)
            {
                UIManager uiManager = UIManager.Instance;
                if (uiManager != null)
                {
                    uiManager.ShowQuestDialog(questData, this);
                }
                else
                {
                    Debug.LogError("[QuestGiver] UIManager bulunamadı!");
                }
            }
        }
        else
        {
            parentNPC.HandleNPCTypeInteraction();
        }
    }
    else if (currentQuestStatus == QuestStatus.Completed)
    {
        if (QuestManager.Instance.HasCompletedQuestForNPC(parentNPC.NPCName))
        {
            QuestData questData = QuestManager.Instance.GetCompletedQuestForNPC(parentNPC.NPCName);
            if (questData != null)
            {
                UIManager uiManager = UIManager.Instance;
                if (uiManager != null)
                {
                    uiManager.ShowQuestCompletionDialog(questData, this);
                }
                else
                {
                    Debug.LogError("[QuestGiver] UIManager bulunamadı!");
                }
            }
            else
            {
                Debug.LogError("[QuestGiver] Tamamlanmış quest data bulunamadı!");
            }
        }
        else
        {
            Debug.LogError("[QuestGiver] Tamamlanmış quest bulunamadı!");
        }
    }
    else
    {
        parentNPC.HandleNPCTypeInteraction();
    }
}
    private void CheckAndCompleteTalkToNPCQuests()
    {
        if (QuestManager.Instance == null || parentNPC == null) return;

        System.Collections.Generic.List<PlayerQuest> activeQuests = QuestManager.Instance.GetActiveQuests();

        foreach (var playerQuest in activeQuests)
        {
            if (playerQuest.status == QuestStatus.InProgress)
            {
                QuestData questData = QuestManager.Instance.GetQuestData(playerQuest.questId);
                if (questData != null)
                {
                    // Ana objectives'deki TalkToNPC'leri kontrol et
                    foreach (var objective in playerQuest.objectives)
                    {
                        if (objective.type == QuestType.TalkToNPC &&
                            objective.targetId == parentNPC.NPCName &&
                            !objective.IsCompleted)
                        {
                            // ✅ YENİ: Item kontrolü
                            if (objective.requiresItemGive && !string.IsNullOrEmpty(objective.requiredItemId))
                            {
                                InventorySystem inventory = FindLocalPlayerInventory();
                                if (inventory == null || !inventory.HasItem(objective.requiredItemId))
                                {
                                    // Item yok, bu objective'i tamamlama
                                    continue;
                                }

                                // Item var, sil
                                inventory.RemoveItemById(objective.requiredItemId, 1);
                            }

                            QuestManager.Instance.UpdateQuestProgress(
                                playerQuest.questId,
                                QuestType.TalkToNPC,
                                parentNPC.NPCName
                            );
                        }
                    }

                    // ✅ YENİ: Hidden objective'i de kontrol et
                    if (playerQuest.isHiddenObjectiveActive &&
                        playerQuest.hiddenObjective != null &&
                        playerQuest.hiddenObjective.type == QuestType.TalkToNPC &&
                        playerQuest.hiddenObjective.targetId == parentNPC.NPCName &&
                        !playerQuest.hiddenObjective.IsCompleted)
                    {
                        // Item kontrolü
                        if (playerQuest.hiddenObjective.requiresItemGive &&
                            !string.IsNullOrEmpty(playerQuest.hiddenObjective.requiredItemId))
                        {
                            InventorySystem inventory = FindLocalPlayerInventory();
                            if (inventory == null || !inventory.HasItem(playerQuest.hiddenObjective.requiredItemId))
                            {
                                // Item yok, tamamlama
                                continue;
                            }

                            // Item var, sil
                            inventory.RemoveItemById(playerQuest.hiddenObjective.requiredItemId, 1);
                        }

                        QuestManager.Instance.UpdateQuestProgress(
                            playerQuest.questId,
                            QuestType.TalkToNPC,
                            parentNPC.NPCName
                        );
                    }
                }
            }
        }
    }
// ✅ YENİ HELPER METOD: Local player'ın inventory'sini bul
private InventorySystem FindLocalPlayerInventory()
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject networkObject = player.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.HasInputAuthority)
        {
            return player.GetComponent<InventorySystem>();
        }
    }
    return null;
}
    
    // Quest kabul edildiğinde
    public void OnQuestAccepted(string questId)
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StartQuest(questId);
            UpdateQuestMarker();
        }
    }
    
    // Quest teslim edildiğinde
    public void OnQuestTurnedIn(string questId)
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.TurnInQuest(questId);
            UpdateQuestMarker();
        }
    }
}