using UnityEngine;
using Fusion;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public enum NPCInteractionPriority
{
    None,      // Hiç etkileşim yok
    Low,       // Merchant gibi normal etkileşim
    High       // Quest etkileşimi (tamamlanabilir)
}

public class DialogQuestGiver : NetworkBehaviour
{
    [Header("Dialog Quest Settings")]
    [SerializeField] private QuestData[] assignedQuests;
    private QuestData currentActiveQuest;

    [Header("NPC Info")]
    [SerializeField] private string npcName = "NPC";
    [SerializeField] private float interactionRange = 3f;

    [Header("UI Position Settings")]
    [SerializeField] private Vector3 canvasOffset = new Vector3(0f, 3f, 0f);
    [SerializeField] private float canvasUIScale = 0.02f;

    [Header("Dialogue Only Mode Settings")]
    [Tooltip("True ise bu NPC sadece diyalog gösterir, quest başlatmaz")]
    [SerializeField] private bool isDialogueOnlyMode = false;


    [Tooltip("Dialogue Only Mode için özel NPC sprite (boş bırakılırsa quest icon kullanılır)")]
    [SerializeField] private Sprite dialogueOnlyNPCSprite;

    [Header("UI Elements")]
    [SerializeField] private GameObject npcCanvasPrefab;

    private GameObject questMarkerObject;
    private Image questMarkerImage;
    private Canvas npcCanvas;
    private bool isDialogActive = false;
    private DialogQuestStatus currentStatus = DialogQuestStatus.NotStarted;
    private bool isPlayerInRange = false;
    private NetworkObject localPlayerObject;
    private float lastDistanceCheckTime = 0f;
    private const float DISTANCE_CHECK_INTERVAL = 1f; // Saniyede bir kontrol
    private string currentDialogueQuestId = ""; // Hangi quest için diyalog gösterildiğini tutar
    private enum DialogQuestStatus
    {
        NotStarted,
        InProgress,
        Completed
    }

    private void Awake()
    {
        if (assignedQuests != null && assignedQuests.Length > 0)
        {
            for (int i = 0; i < assignedQuests.Length; i++)
            {
                if (assignedQuests[i] != null)
                {
                    if (!assignedQuests[i].isDialogQuest)
                    {
                        assignedQuests[i] = null;
                    }
                }
            }

            bool hasValidQuest = false;
            foreach (var quest in assignedQuests)
            {
                if (quest != null)
                {
                    hasValidQuest = true;
                    break;
                }
            }

            if (!hasValidQuest)
            {
            }
        }

        CreateNPCCanvas();
        CreateQuestMarker();
    }

    private void Start()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStarted += OnQuestStatusChanged;
            QuestManager.Instance.OnQuestCompleted += OnQuestStatusChanged;
            QuestManager.Instance.OnQuestTurnedIn += OnQuestStatusChanged;
            QuestManager.Instance.OnQuestUpdated += OnQuestStatusChanged;
        }
        UpdateQuestStatus();
        UpdateQuestMarker();
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStarted -= OnQuestStatusChanged;
            QuestManager.Instance.OnQuestCompleted -= OnQuestStatusChanged;
            QuestManager.Instance.OnQuestTurnedIn -= OnQuestStatusChanged;
        }
    }

    private void Update()
    {
        // Saniyede bir oyuncu mesafesini kontrol et
        if (Time.time - lastDistanceCheckTime >= DISTANCE_CHECK_INTERVAL)
        {
            CheckPlayerDistance();
            lastDistanceCheckTime = Time.time;
        }
    }


public NPCInteractionPriority GetInteractionPriority()
{
    if (!isPlayerInRange)
    {
        return NPCInteractionPriority.None;
    }

    if (QuestManager.Instance != null)
    {
        var activeQuests = QuestManager.Instance.GetActiveQuests();

        foreach (var playerQuest in activeQuests)
        {
            if (playerQuest.status == QuestStatus.InProgress)
            {
                foreach (var objective in playerQuest.objectives)
                {
                    if (objective.type == QuestType.TalkToNPC &&
                        objective.MatchesTarget(npcName) &&
                        !objective.IsCompleted)
                    {
                        if (objective.requiresItemGive && !string.IsNullOrEmpty(objective.requiredItemId))
                        {
                            int itemCount = GetTotalItemCount(objective.requiredItemId);

                            if (itemCount < objective.requiredItemAmount)
                            {
                                continue;
                            }
                        }

                        return NPCInteractionPriority.High;
                    }
                }

                if (playerQuest.isHiddenObjectiveActive &&
                    playerQuest.hiddenObjective != null &&
                    playerQuest.hiddenObjective.type == QuestType.TalkToNPC &&
                    playerQuest.hiddenObjective.MatchesTarget(npcName) &&
                    !playerQuest.hiddenObjective.IsCompleted)
                {
                    if (playerQuest.hiddenObjective.requiresItemGive &&
                        !string.IsNullOrEmpty(playerQuest.hiddenObjective.requiredItemId))
                    {
                        int itemCount = GetTotalItemCount(playerQuest.hiddenObjective.requiredItemId);

                        if (itemCount < playerQuest.hiddenObjective.requiredItemAmount)
                        {
                            continue;
                        }
                    }

                    return NPCInteractionPriority.High;
                }
            }
        }
    }

    currentActiveQuest = GetCurrentActiveQuest();

    if (currentActiveQuest != null)
    {
        if (currentStatus == DialogQuestStatus.NotStarted)
        {
            return NPCInteractionPriority.High;
        }
        else if (currentStatus == DialogQuestStatus.InProgress &&
                 QuestManager.Instance != null &&
                 QuestManager.Instance.IsQuestCompleted(currentActiveQuest.questId))
        {
            PlayerQuest playerQuest = QuestManager.Instance.GetPlayerQuest(currentActiveQuest.questId);
            if (playerQuest != null)
            {
                foreach (var objective in playerQuest.objectives)
                {
                    if (objective.requiresItemGive && !string.IsNullOrEmpty(objective.requiredItemId))
                    {
                        if (objective.type == QuestType.TalkToNPC && !objective.MatchesTarget(npcName))
                        {
                            continue;
                        }

                        int itemCount = GetTotalItemCount(objective.requiredItemId);

                        if (itemCount < objective.requiredItemAmount)
                        {
                            return NPCInteractionPriority.Low;
                        }
                    }
                }

                if (playerQuest.isHiddenObjectiveActive &&
                    playerQuest.hiddenObjective != null &&
                    playerQuest.hiddenObjective.requiresItemGive &&
                    !string.IsNullOrEmpty(playerQuest.hiddenObjective.requiredItemId))
                {
                    if (playerQuest.hiddenObjective.type == QuestType.TalkToNPC &&
                        !playerQuest.hiddenObjective.MatchesTarget(npcName))
                    {
                    }
                    else
                    {
                        int itemCount = GetTotalItemCount(playerQuest.hiddenObjective.requiredItemId);

                        if (itemCount < playerQuest.hiddenObjective.requiredItemAmount)
                        {
                            return NPCInteractionPriority.Low;
                        }
                    }
                }
            }

            return NPCInteractionPriority.High;
        }
    }

    return NPCInteractionPriority.Low;
}
    private void ApplyCustomCanvasSettings()
    {
        if (npcCanvas != null)
        {
            npcCanvas.transform.localPosition = canvasOffset;
            npcCanvas.transform.localScale = new Vector3(canvasUIScale, canvasUIScale, canvasUIScale);
        }
    }

    private void TriggerNPCInteractionEvent()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.HandleNPCInteractionDirect(npcName, Object.InputAuthority.PlayerId);
        }
    }

    private void CreateNPCCanvas()
    {
        // Server modunda UI oluşturma - KRİTİK: Resources.Load bile yapma!
        if (IsServerMode())
        {
            return;
        }

        if (npcCanvasPrefab == null)
        {
            npcCanvasPrefab = Resources.Load<GameObject>("Prefabs/NPCCanvas");
            if (npcCanvasPrefab == null)
            {
                return;
            }
        }

        GameObject canvasInstance = Instantiate(npcCanvasPrefab, transform);
        npcCanvas = canvasInstance.GetComponent<Canvas>();

        if (npcCanvas == null)
        {
            return;
        }

        TMPro.TextMeshProUGUI nameText = canvasInstance.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = npcName;
        }

        ApplyCustomCanvasSettings();
    }

    /// <summary>
    /// Server mode detection - command line arguments
    /// </summary>
    private bool IsServerMode()
    {
        if (Application.isEditor) return false;

        string[] args = System.Environment.GetCommandLineArgs();
        return System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode");
    }

    private void CreateQuestMarker()
    {
        if (npcCanvas == null) return;

        questMarkerObject = npcCanvas.transform.Find("QuestMarker")?.gameObject;

        if (questMarkerObject == null)
        {
            return;
        }

        questMarkerImage = questMarkerObject.GetComponent<Image>();
        if (questMarkerImage == null)
        {
            return;
        }

        questMarkerObject.SetActive(false);
    }

    private void CheckPlayerDistance()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        bool wasInRange = isPlayerInRange;
        isPlayerInRange = false;

        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
            {
                localPlayerObject = netObj;
                float distance = Vector2.Distance(transform.position, player.transform.position);
                isPlayerInRange = distance <= interactionRange;

                if (wasInRange != isPlayerInRange)
                {
                    CombatInitializer combatInit = CombatInitializer.Instance;
                    if (combatInit != null)
                    {
                        if (isPlayerInRange)
                        {
                            combatInit.SetNearbyDialogNPC(this);
                        }
                        else
                        {
                            combatInit.RemoveNearbyDialogNPC(this);
                        }
                    }
                }
                break;
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void InitializeNPCRPC(string name, int type)
    {
        npcName = name;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void SetWanderBoundsRPC(Vector2 min, Vector2 max)
    {
    }

    private QuestData GetCurrentActiveQuest()
    {
        if (assignedQuests == null || assignedQuests.Length == 0)
        {
            return null;
        }

        foreach (var quest in assignedQuests)
        {
            if (quest == null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(quest.previousQuestId))
            {
                if (QuestManager.Instance == null || !QuestManager.Instance.IsQuestTurnedIn(quest.previousQuestId))
                {
                    continue;
                }
            }

            if (QuestManager.Instance != null)
            {
                bool isStarted = QuestManager.Instance.IsQuestStarted(quest.questId);
                bool isTurnedIn = QuestManager.Instance.IsQuestTurnedIn(quest.questId);
                bool isCompleted = QuestManager.Instance.IsQuestCompleted(quest.questId);

                if (!isStarted && !isTurnedIn)
                {
                    return quest;
                }
                else if (isStarted && !isTurnedIn)
                {
                    return quest;
                }
            }
        }

        return null;
    }

public void UpdateQuestMarker()
{
    if (questMarkerObject == null) return;

    currentActiveQuest = GetCurrentActiveQuest();
    bool shouldShowMarker = false;
    QuestMarkerStyle animationStyle = QuestMarkerStyle.Available;

    // Range'den bağımsız quest durumuna göre marker göster
    
    // 1. Önce TalkToNPC objective kontrolü (en yüksek öncelik)
    if (HasActiveTalkToNPCObjective())
    {
        shouldShowMarker = true;
        animationStyle = QuestMarkerStyle.Completed;
        if (questMarkerImage != null)
        {
            questMarkerImage.sprite = QuestMarkerIcons.CompletedQuestIcon;
            questMarkerImage.color = Color.white;
        }
    }
    // 2. Bu NPC'nin kendi quest'i varsa
    else if (currentActiveQuest != null)
    {
        if (currentStatus == DialogQuestStatus.NotStarted)
        {
            // Quest başlatılabilir - sarı marker
            shouldShowMarker = true;
            animationStyle = QuestMarkerStyle.Available;
            if (questMarkerImage != null)
            {
                questMarkerImage.sprite = QuestMarkerIcons.AvailableQuestIcon;
                questMarkerImage.color = Color.white;
            }
        }
        else if (currentStatus == DialogQuestStatus.InProgress && 
                 QuestManager.Instance != null &&
                 QuestManager.Instance.IsQuestCompleted(currentActiveQuest.questId))
        {
            // Quest tamamlandı, teslim edilebilir - yeşil marker
            // Ama item gereksinimleri varsa kontrol et (sadece player range içindeyse)
            bool canTurnIn = true;
            
            if (isPlayerInRange)
            {
                PlayerQuest playerQuest = QuestManager.Instance.GetPlayerQuest(currentActiveQuest.questId);
                if (playerQuest != null)
                {
                    InventorySystem inventory = FindLocalPlayerInventory();
                    if (inventory != null)
                    {
                        // Tüm item gereksinimlerini kontrol et
                        foreach (var objective in playerQuest.objectives)
                        {
                            if (objective.requiresItemGive && !string.IsNullOrEmpty(objective.requiredItemId))
                            {
                                if (objective.type == QuestType.TalkToNPC && !objective.MatchesTarget(npcName))
                                {
                                    continue;
                                }

                                int itemCount = 0;
                                foreach (var slot in inventory.GetAllSlots().Values)
                                {
                                    if (!slot.isEmpty && slot.item != null && slot.item.itemId == objective.requiredItemId)
                                    {
                                        itemCount += slot.amount;
                                    }
                                }

                                if (itemCount < objective.requiredItemAmount)
                                {
                                    canTurnIn = false;
                                    break;
                                }
                            }
                        }

                        // Hidden objective item kontrolü
                        if (canTurnIn && playerQuest.isHiddenObjectiveActive &&
                            playerQuest.hiddenObjective != null &&
                            playerQuest.hiddenObjective.requiresItemGive &&
                            !string.IsNullOrEmpty(playerQuest.hiddenObjective.requiredItemId))
                        {
                            if (playerQuest.hiddenObjective.type == QuestType.TalkToNPC &&
                                !playerQuest.hiddenObjective.MatchesTarget(npcName))
                            {
                                // Bu NPC için değil
                            }
                            else
                            {
                                int itemCount = 0;
                                foreach (var slot in inventory.GetAllSlots().Values)
                                {
                                    if (!slot.isEmpty && slot.item != null &&
                                        slot.item.itemId == playerQuest.hiddenObjective.requiredItemId)
                                    {
                                        itemCount += slot.amount;
                                    }
                                }

                                if (itemCount < playerQuest.hiddenObjective.requiredItemAmount)
                                {
                                    canTurnIn = false;
                                }
                            }
                        }
                    }
                }
            }
            
            // Item yoksa bile marker göster (uzaktan görünsün), ama style'ı Available yap
            shouldShowMarker = true;
            if (canTurnIn)
            {
                animationStyle = QuestMarkerStyle.Completed;
                if (questMarkerImage != null)
                {
                    questMarkerImage.sprite = QuestMarkerIcons.CompletedQuestIcon;
                    questMarkerImage.color = Color.white;
                }
            }
            else
            {
                // Item yok ama quest tamamlandı - sarı marker göster
                animationStyle = QuestMarkerStyle.Available;
                if (questMarkerImage != null)
                {
                    questMarkerImage.sprite = QuestMarkerIcons.AvailableQuestIcon;
                    questMarkerImage.color = Color.white;
                }
            }
        }
    }

    QuestMarkerAnimator animator = questMarkerObject.GetComponent<QuestMarkerAnimator>();
    if (animator != null)
    {
        animator.SetAnimationStyle(animationStyle);
    }

    questMarkerObject.SetActive(shouldShowMarker);

    // CombatInitializer'ın button sprite'ını da güncelle
    if (isPlayerInRange && CombatInitializer.Instance != null)
    {
        CombatInitializer.Instance.UpdateAutoAttackButtonState();
    }
}

    private bool HasActiveTalkToNPCObjective()
    {
        if (QuestManager.Instance == null) return false;

        var activeQuests = QuestManager.Instance.GetActiveQuests();

        foreach (var playerQuest in activeQuests)
        {
            if (playerQuest.status == QuestStatus.InProgress)
            {
                foreach (var objective in playerQuest.objectives)
                {
                    if (objective.type == QuestType.TalkToNPC &&
                        objective.MatchesTarget(npcName) &&
                        !objective.IsCompleted)
                    {
                        return true;
                    }
                }

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

    private void OnQuestStatusChanged(string questId)
    {
        bool shouldUpdate = false;

        // Bu NPC'nin kendi quest'lerinden biri mi kontrol et
        if (assignedQuests != null && assignedQuests.Length > 0)
        {
            foreach (var quest in assignedQuests)
            {
                if (quest != null && quest.questId == questId)
                {
                    shouldUpdate = true;
                    break;
                }
            }
        }

        // Veya bu quest'in bir TalkToNPC objective'i bu NPC için mi kontrol et
        if (!shouldUpdate && QuestManager.Instance != null)
        {
            var playerQuest = QuestManager.Instance.GetPlayerQuest(questId);
            if (playerQuest != null && playerQuest.status == QuestStatus.InProgress)
            {
                // Ana objectives'leri kontrol et
                foreach (var objective in playerQuest.objectives)
                {
                    if (objective.type == QuestType.TalkToNPC && objective.MatchesTarget(npcName))
                    {
                        shouldUpdate = true;
                        break;
                    }
                }

                // Hidden objective'i de kontrol et
                if (!shouldUpdate &&
                    playerQuest.isHiddenObjectiveActive &&
                    playerQuest.hiddenObjective != null &&
                    playerQuest.hiddenObjective.type == QuestType.TalkToNPC &&
                    playerQuest.hiddenObjective.MatchesTarget(npcName))
                {
                    shouldUpdate = true;
                }
            }
        }

        if (shouldUpdate)
        {
            UpdateQuestStatus();
            UpdateQuestMarker();
        }
    }

    public void UpdateQuestStatus()
    {
        currentActiveQuest = GetCurrentActiveQuest();

        if (currentActiveQuest == null || QuestManager.Instance == null)
        {
            currentStatus = DialogQuestStatus.NotStarted;
            return;
        }

        if (QuestManager.Instance.IsQuestTurnedIn(currentActiveQuest.questId))
        {
            currentStatus = DialogQuestStatus.Completed;
        }
        else if (QuestManager.Instance.IsQuestStarted(currentActiveQuest.questId))
        {
            currentStatus = DialogQuestStatus.InProgress;
        }
        else
        {
            currentStatus = DialogQuestStatus.NotStarted;
        }
    }

public bool CanInteract()
{
    if (!isPlayerInRange)
    {
        return false;
    }

    if (isDialogueOnlyMode)
    {
        currentActiveQuest = GetCurrentActiveQuest();
        if (currentActiveQuest != null)
        {
            UpdateQuestStatus();

            if (currentStatus == DialogQuestStatus.NotStarted ||
                (currentStatus == DialogQuestStatus.InProgress &&
                 QuestManager.Instance != null &&
                 QuestManager.Instance.IsQuestCompleted(currentActiveQuest.questId)))
            {
                return true;
            }
        }

        if (HasActiveTalkToNPCObjective())
        {
            return true;
        }

        return false;
    }

    if (QuestManager.Instance != null)
    {
        var activeQuests = QuestManager.Instance.GetActiveQuests();

        foreach (var playerQuest in activeQuests)
        {
            if (playerQuest.status == QuestStatus.InProgress)
            {
                foreach (var objective in playerQuest.objectives)
                {
                    if (objective.type == QuestType.TalkToNPC &&
                        objective.MatchesTarget(npcName) &&
                        !objective.IsCompleted)
                    {
                        return true;
                    }
                }

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
    }

    currentActiveQuest = GetCurrentActiveQuest();

    if (currentActiveQuest == null)
    {
        return false;
    }

    if (currentStatus == DialogQuestStatus.InProgress && QuestManager.Instance != null)
    {
        PlayerQuest playerQuest = QuestManager.Instance.GetPlayerQuest(currentActiveQuest.questId);
        if (playerQuest != null && playerQuest.objectives != null)
        {
            foreach (var objective in playerQuest.objectives)
            {
                if (objective.type == QuestType.TalkToNPC &&
                    objective.targetId == npcName &&
                    !objective.IsCompleted)
                {
                    return true;
                }
            }
        }
    }

    bool canInteract = currentStatus == DialogQuestStatus.NotStarted ||
                      (currentStatus == DialogQuestStatus.InProgress &&
                       QuestManager.Instance != null &&
                       QuestManager.Instance.IsQuestCompleted(currentActiveQuest.questId));

    if (canInteract &&
        currentStatus == DialogQuestStatus.InProgress &&
        QuestManager.Instance != null &&
        QuestManager.Instance.IsQuestCompleted(currentActiveQuest.questId))
    {
        PlayerQuest playerQuest = QuestManager.Instance.GetPlayerQuest(currentActiveQuest.questId);
        if (playerQuest != null)
        {
            foreach (var objective in playerQuest.objectives)
            {
                if (objective.requiresItemGive &&
                    !string.IsNullOrEmpty(objective.requiredItemId))
                {
                    if (objective.type == QuestType.TalkToNPC && !objective.MatchesTarget(npcName))
                    {
                        continue;
                    }

                    int itemCount = GetTotalItemCount(objective.requiredItemId);

                    if (itemCount < objective.requiredItemAmount)
                    {
                        return false;
                    }
                }
            }

            if (playerQuest.isHiddenObjectiveActive &&
                playerQuest.hiddenObjective != null &&
                playerQuest.hiddenObjective.requiresItemGive &&
                !string.IsNullOrEmpty(playerQuest.hiddenObjective.requiredItemId))
            {
                if (playerQuest.hiddenObjective.type == QuestType.TalkToNPC &&
                    !playerQuest.hiddenObjective.MatchesTarget(npcName))
                {
                }
                else
                {
                    int itemCount = GetTotalItemCount(playerQuest.hiddenObjective.requiredItemId);

                    if (itemCount < playerQuest.hiddenObjective.requiredItemAmount)
                    {
                        return false;
                    }
                }
            }
        }
    }

    return canInteract;
}

public void HandleNPCInteraction()
{
    if (!isDialogueOnlyMode && QuestManager.Instance != null)
    {
        var activeQuests = QuestManager.Instance.GetActiveQuests();

        foreach (var playerQuest in activeQuests)
        {
            if (playerQuest.status == QuestStatus.InProgress)
            {
                for (int i = 0; i < playerQuest.objectives.Count; i++)
                {
                    var objective = playerQuest.objectives[i];

                    if (objective.type == QuestType.TalkToNPC &&
                        objective.MatchesTarget(npcName) &&
                        !objective.IsCompleted)
                    {
                        if (objective.requiresItemGive && !string.IsNullOrEmpty(objective.requiredItemId))
                        {
                            int itemsNeeded = objective.requiredAmount - objective.currentAmount;
                            if (itemsNeeded <= 0) continue;

                            int itemCount = GetTotalItemCount(objective.requiredItemId);
                            int itemsToRemove = Mathf.Min(itemCount, itemsNeeded);
                            if (itemsToRemove <= 0) continue;

                            bool removed = RemoveTotalItems(objective.requiredItemId, itemsToRemove);
                            if (!removed) continue;

                            QuestManager.Instance.UpdateQuestProgress(
                                playerQuest.questId,
                                QuestType.TalkToNPC,
                                npcName,
                                itemsToRemove
                            );
                        }
                        else
                        {
                            QuestManager.Instance.UpdateQuestProgress(
                                playerQuest.questId,
                                QuestType.TalkToNPC,
                                npcName
                            );
                        }
                    }
                }

                if (playerQuest.isHiddenObjectiveActive &&
                    playerQuest.hiddenObjective != null)
                {
                    if (playerQuest.hiddenObjective.type == QuestType.TalkToNPC &&
                        playerQuest.hiddenObjective.MatchesTarget(npcName) &&
                        !playerQuest.hiddenObjective.IsCompleted)
                    {
                        if (playerQuest.hiddenObjective.requiresItemGive &&
                            !string.IsNullOrEmpty(playerQuest.hiddenObjective.requiredItemId))
                        {
                            int itemsNeeded = playerQuest.hiddenObjective.requiredAmount - 
                                            playerQuest.hiddenObjective.currentAmount;
                            if (itemsNeeded <= 0) return;

                            int itemCount = GetTotalItemCount(playerQuest.hiddenObjective.requiredItemId);
                            int itemsToRemove = Mathf.Min(itemCount, itemsNeeded);
                            if (itemsToRemove <= 0) return;

                            bool removed = RemoveTotalItems(playerQuest.hiddenObjective.requiredItemId, itemsToRemove);
                            if (!removed) return;

                            QuestManager.Instance.UpdateQuestProgress(
                                playerQuest.questId,
                                QuestType.TalkToNPC,
                                npcName,
                                itemsToRemove
                            );
                        }
                        else
                        {
                            QuestManager.Instance.UpdateQuestProgress(
                                playerQuest.questId,
                                QuestType.TalkToNPC,
                                npcName
                            );
                        }

                        if (QuestManager.Instance.IsQuestCompleted(playerQuest.questId))
                        {
                            QuestData completedQuestData = QuestManager.Instance.GetQuestData(playerQuest.questId);

                            bool isCorrectTurnInNPC = false;
                            if (completedQuestData != null)
                            {
                                string turnInNPC = string.IsNullOrEmpty(completedQuestData.questTurnInNPC)
                                    ? completedQuestData.questGiverNPC
                                    : completedQuestData.questTurnInNPC;

                                isCorrectTurnInNPC = (turnInNPC == npcName);
                            }

                            if (isCorrectTurnInNPC)
                            {
                                currentActiveQuest = completedQuestData;
                                UpdateQuestMarker();

                                if (completedQuestData.HasCompletionDialogs)
                                {
                                    isDialogActive = true;
                                    UIManager uiManager = UIManager.Instance;
                                    if (uiManager != null)
                                    {
                                        string questIdToTurnIn = playerQuest.questId;
                                        uiManager.ShowMainQuestPanel(
                                            completedQuestData.completionDialogues,
                                            completedQuestData.questIcon,
                                            npcName,
                                            () =>
                                            {
                                                isDialogActive = false;
                                                if (QuestManager.Instance != null)
                                                {
                                                    QuestManager.Instance.TurnInQuest(questIdToTurnIn);
                                                }
                                            }
                                        );
                                    }
                                }
                                else
                                {
                                    QuestManager.Instance.TurnInQuest(playerQuest.questId);
                                }
                                return;
                            }
                        }

                        UpdateQuestMarker();
                    }
                }
            }
        }
    }

    if (!CanInteract())
    {
        return;
    }

    if (isDialogActive)
    {
        return;
    }

    if (isDialogueOnlyMode)
    {
        HandleDialogueOnlyMode();
        return;
    }

    currentActiveQuest = GetCurrentActiveQuest();

    if (currentActiveQuest == null) return;

    TriggerNPCInteractionEvent();

    if (currentStatus == DialogQuestStatus.NotStarted)
    {
        StartQuestDialog();
    }
    else if (currentStatus == DialogQuestStatus.InProgress)
    {
        bool isCompleted = QuestManager.Instance.IsQuestCompleted(currentActiveQuest.questId);

        if (isCompleted)
        {
            StartCompletionDialog();
        }
        else
        {
            StartProgressDialog();
        }
    }
}

private void HandleDialogueOnlyMode()
{
    // Quest-specific diyalogları kontrol et
    var (questDialogues, questId) = GetRelevantQuestDialogues();

    // Sadece quest-specific diyaloglar varsa göster
    if (questDialogues == null || questDialogues.Length == 0)
    {
        return;
    }

    isDialogActive = true;
    currentDialogueQuestId = questId;

    Sprite displaySprite = dialogueOnlyNPCSprite != null ?
        dialogueOnlyNPCSprite :
        (currentActiveQuest != null ? currentActiveQuest.questIcon : null);

    UIManager uiManager = UIManager.Instance;
    if (uiManager != null)
    {
        uiManager.ShowMainQuestPanel(
            questDialogues,
            displaySprite,
            npcName,
            OnDialogueOnlyCompleted
        );
    }
    else
    {
        isDialogActive = false;
        currentDialogueQuestId = "";
    }
}

private void OnDialogueOnlyCompleted()
{
    isDialogActive = false;

    if (QuestManager.Instance != null)
    {
        var activeQuests = QuestManager.Instance.GetActiveQuests();

        foreach (var playerQuest in activeQuests)
        {
            if (playerQuest.status == QuestStatus.InProgress)
            {
                // Eğer currentDialogueQuestId doluysa, sadece o quest'i güncelle
                if (!string.IsNullOrEmpty(currentDialogueQuestId) && 
                    playerQuest.questId != currentDialogueQuestId)
                {
                    continue;
                }
                
                for (int i = 0; i < playerQuest.objectives.Count; i++)
                {
                    var objective = playerQuest.objectives[i];

                    if (objective.type == QuestType.TalkToNPC &&
                        objective.MatchesTarget(npcName) &&
                        !objective.IsCompleted)
                    {
                        if (objective.requiresItemGive && !string.IsNullOrEmpty(objective.requiredItemId))
                        {
                            int itemsNeeded = objective.requiredAmount - objective.currentAmount;
                            if (itemsNeeded <= 0) continue;

                            int itemCount = GetTotalItemCount(objective.requiredItemId);
                            int itemsToRemove = Mathf.Min(itemCount, itemsNeeded);
                            if (itemsToRemove <= 0) continue;

                            bool removed = RemoveTotalItems(objective.requiredItemId, itemsToRemove);
                            if (!removed) continue;

                            QuestManager.Instance.UpdateQuestProgress(
                                playerQuest.questId,
                                QuestType.TalkToNPC,
                                npcName,
                                itemsToRemove
                            );
                        }
                        else
                        {
                            QuestManager.Instance.UpdateQuestProgress(
                                playerQuest.questId,
                                QuestType.TalkToNPC,
                                npcName
                            );
                        }
                    }
                }

                if (playerQuest.isHiddenObjectiveActive &&
                    playerQuest.hiddenObjective != null &&
                    playerQuest.hiddenObjective.type == QuestType.TalkToNPC &&
                    playerQuest.hiddenObjective.MatchesTarget(npcName) &&
                    !playerQuest.hiddenObjective.IsCompleted)
                {
                    if (playerQuest.hiddenObjective.requiresItemGive &&
                        !string.IsNullOrEmpty(playerQuest.hiddenObjective.requiredItemId))
                    {
                        int itemsNeeded = playerQuest.hiddenObjective.requiredAmount - 
                                        playerQuest.hiddenObjective.currentAmount;
                        if (itemsNeeded <= 0) continue;

                        int itemCount = GetTotalItemCount(playerQuest.hiddenObjective.requiredItemId);
                        int itemsToRemove = Mathf.Min(itemCount, itemsNeeded);
                        if (itemsToRemove <= 0) continue;

                        bool removed = RemoveTotalItems(playerQuest.hiddenObjective.requiredItemId, itemsToRemove);
                        if (!removed) continue;

                        QuestManager.Instance.UpdateQuestProgress(
                            playerQuest.questId,
                            QuestType.TalkToNPC,
                            npcName,
                            itemsToRemove
                        );
                    }
                    else
                    {
                        QuestManager.Instance.UpdateQuestProgress(
                            playerQuest.questId,
                            QuestType.TalkToNPC,
                            npcName
                        );
                    }
                }
            }
        }
    }
    
    // Quest ID'yi temizle
    currentDialogueQuestId = "";
    
    UpdateQuestMarker();
}

    private void StartProgressDialog()
    {
        if (currentActiveQuest?.progressDialogues != null && currentActiveQuest.progressDialogues.Length > 0)
        {
            isDialogActive = true;

            UIManager uiManager = UIManager.Instance;
            if (uiManager != null)
            {
                uiManager.ShowMainQuestPanel(
                    currentActiveQuest.progressDialogues,
                    currentActiveQuest.questIcon,
                    npcName,
                    OnProgressDialogCompleted
                );
            }
        }
        else
        {
            OnProgressDialogCompleted();
        }
    }

    private void OnProgressDialogCompleted()
    {
        isDialogActive = false;
    }

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

    private void StartQuestDialog()
    {
        if (currentActiveQuest?.startDialogues == null || currentActiveQuest.startDialogues.Length == 0) return;

        isDialogActive = true;

        UIManager uiManager = UIManager.Instance;
        if (uiManager != null)
        {
            uiManager.ShowMainQuestPanel(
                currentActiveQuest.startDialogues,
                currentActiveQuest.questIcon,
                npcName,
                OnDialogCompleted
            );
        }
    }

    private void StartCompletionDialog()
    {
        if (currentActiveQuest != null && currentActiveQuest.HasCompletionDialogs)
        {
            isDialogActive = true;

            UIManager uiManager = UIManager.Instance;
            if (uiManager != null)
            {
                uiManager.ShowMainQuestPanel(
                    currentActiveQuest.completionDialogues,
                    currentActiveQuest.questIcon,
                    npcName,
                    OnCompletionDialogCompleted
                );
            }
        }
        else
        {
            CompleteQuest();
        }
    }

    private void OnDialogCompleted()
    {
        isDialogActive = false;

        if (QuestManager.Instance != null && currentActiveQuest != null)
        {
            bool questStarted = QuestManager.Instance.StartQuest(currentActiveQuest.questId);

            if (questStarted)
            {
                UpdateQuestStatus();
                UpdateQuestMarker();
            }
        }
    }

    private void OnCompletionDialogCompleted()
    {
        isDialogActive = false;
        CompleteQuest();
    }

    private void CompleteQuest()
    {
        if (QuestManager.Instance != null && currentActiveQuest != null)
        {
            QuestManager.Instance.TurnInQuest(currentActiveQuest.questId);
        }
    }

    public QuestData GetAssignedQuest() => currentActiveQuest;
    public bool IsDialogActive => isDialogActive;
    public string NPCName => npcName;
    public float InteractionRange => interactionRange;

    // CombatInitializer için sprite seçimi yapabilmesi için quest durumunu döndür
    public bool ShouldShowCompletedSprite()
    {
        // TalkToNPC objective varsa ve bu NPC için ise, completed sprite göster
        if (HasActiveTalkToNPCObjective())
        {
            return true;
        }

        // Veya current quest completed ise, completed sprite göster
        if (currentActiveQuest != null &&
            currentStatus == DialogQuestStatus.InProgress &&
            QuestManager.Instance != null &&
            QuestManager.Instance.IsQuestCompleted(currentActiveQuest.questId))
        {
            return true;
        }

        return false;
    }

    public bool ShouldShowAvailableSprite()
    {
        // Current quest NotStarted ise, available sprite göster
        if (currentActiveQuest != null && currentStatus == DialogQuestStatus.NotStarted)
        {
            return true;
        }

        return false;
    }

    // CraftInventorySystem'i bul
private CraftInventorySystem FindLocalPlayerCraftInventory()
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject networkObject = player.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.HasInputAuthority)
        {
            return player.GetComponent<CraftInventorySystem>();
        }
    }
    return null;
}

// Hem normal hem craft inventory'den item say
private int GetTotalItemCount(string itemId)
{
    int totalCount = 0;
    
    // Normal inventory'den say
    InventorySystem inventory = FindLocalPlayerInventory();
    if (inventory != null)
    {
        foreach (var slot in inventory.GetAllSlots().Values)
        {
            if (!slot.isEmpty && slot.item != null && slot.item.itemId == itemId)
            {
                totalCount += slot.amount;
            }
        }
    }
    
    // Craft inventory'den say
    CraftInventorySystem craftInventory = FindLocalPlayerCraftInventory();
    if (craftInventory != null)
    {
        foreach (var slot in craftInventory.GetAllCraftSlots().Values)
        {
            if (!slot.isEmpty && slot.item != null && slot.item.itemId == itemId)
            {
                totalCount += slot.amount;
            }
        }
    }
    
    return totalCount;
}

    // Hem normal hem craft inventory'den item sil
    private bool RemoveTotalItems(string itemId, int amount)
    {
        int remainingToRemove = amount;

        // Önce normal inventory'den sil
        InventorySystem inventory = FindLocalPlayerInventory();
        if (inventory != null)
        {
            foreach (var slot in inventory.GetAllSlots().Values)
            {
                if (remainingToRemove <= 0) break;

                if (!slot.isEmpty && slot.item != null && slot.item.itemId == itemId)
                {
                    int removeFromSlot = Mathf.Min(slot.amount, remainingToRemove);
                    if (inventory.RemoveItemById(itemId, removeFromSlot))
                    {
                        remainingToRemove -= removeFromSlot;
                    }
                }
            }
        }

        // Sonra craft inventory'den sil
        if (remainingToRemove > 0)
        {
            CraftInventorySystem craftInventory = FindLocalPlayerCraftInventory();
            if (craftInventory != null)
            {
                // CraftInventorySystem'de ConsumeItems metodu var, onu kullan
                if (craftInventory.ConsumeItems(itemId, remainingToRemove))
                {
                    remainingToRemove = 0;
                }
            }
        }

        return remainingToRemove == 0;
    }
private (string[] dialogues, string questId) GetRelevantQuestDialogues()
{
    if (QuestManager.Instance == null) 
    {
        return (null, "");
    }
    
    var activeQuests = QuestManager.Instance.GetActiveQuests();
    
    foreach (var playerQuest in activeQuests)
    {
        
        if (playerQuest.status != QuestStatus.InProgress)
        {
            continue;
        }
            
        foreach (var objective in playerQuest.objectives)
        {
            
            if (objective.type == QuestType.TalkToNPC)
            {
                bool matches = objective.MatchesTarget(npcName);
                
                if (matches && !objective.IsCompleted)
                {
                    // ✅ YENİ: Target'a özel diyalogları al (alternatif target'lar da desteklenir)
                    string[] targetDialogues = objective.GetDialoguesForTarget(npcName);
                    bool hasDialogues = targetDialogues != null && targetDialogues.Length > 0;

                    if (hasDialogues)
                    {
                        return (targetDialogues, playerQuest.questId);
                    }
                    else
                    {
                    }
                }
                else
                {
                }
            }
        }
        
        if (playerQuest.isHiddenObjectiveActive &&
            playerQuest.hiddenObjective != null &&
            playerQuest.hiddenObjective.type == QuestType.TalkToNPC &&
            playerQuest.hiddenObjective.MatchesTarget(npcName) &&
            !playerQuest.hiddenObjective.IsCompleted)
        {
            // ✅ YENİ: Target'a özel diyalogları al (alternatif target'lar da desteklenir)
            string[] targetDialogues = playerQuest.hiddenObjective.GetDialoguesForTarget(npcName);
            if (targetDialogues != null && targetDialogues.Length > 0)
            {
                return (targetDialogues, playerQuest.questId);
            }
        }
    }
    
    return (null, "");
}
}