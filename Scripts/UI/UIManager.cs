// Path: Assets/Game/Scripts/UIManager.cs

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;

    #region ===== CORE UI MANAGEMENT =====
    
    [Header("Core UI Components")]
    [SerializeField] private PlayerStatsUI playerStatsUI;
    [SerializeField] private PotionUI potionUI;
    [SerializeField] private HeadPreviewManager headPreview;
    [Header("Class Info Panel")]
[SerializeField] public GameObject classPanel;
[SerializeField] private Button classPanelToggleButton;
    [Header("Player Info Display")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI infoPlayerNameText;

    private void Awake()
    {
        Instance = this;
        InitializeCoreUI();
    }

    private void Start()
    {
        StartCoroutine(DelayedStartup());
    }

    private IEnumerator DelayedStartup()
    {
        yield return new WaitForSeconds(0.1f);
        InitializeAllSystems();
        CreateRewardNotificationContainer();
        InitializeHeadPreview();
        StartCoroutine(InitializeExistingPlayerTags());
    }

    private void InitializeCoreUI()
    {
        // Core UI panellerini başlangıç durumuna getir
        if (targetInfoPanel != null) targetInfoPanel.SetActive(false);
        if (questDialogPanel != null) questDialogPanel.SetActive(false);
        if (mainQuestPanel != null) mainQuestPanel.SetActive(false);
    }

private void InitializeAllSystems()
{
    InitializeQuestSystem();
    InitializeTargetSystem();
    InitializeNotificationSystem();
    InitializeClassSelectionSystem();
    InitializeDebugSystem(); // YENİ SATIR
}


    public void Initialize(PlayerStats stats)
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
            StartCoroutine(DelayedInitialize(stats));
            return;
        }
        DoInitialize(stats);
    }

    private IEnumerator DelayedInitialize(PlayerStats stats)
    {
        yield return new WaitForSeconds(0.1f);
        DoInitialize(stats);
    }

private void DoInitialize(PlayerStats stats)
{
    // Player UI sistemlerini initialize et
    InitializePlayerUI(stats);
    InitializePlayerNameTags(stats);
    InitializeInfoPanel(stats);
    // InitializeClassInfoDisplay(stats); // Bu satırı kaldır
    StartCoroutine(InitializeAllUISlots(stats));
}


    private void InitializePlayerUI(PlayerStats stats)
    {
        if (playerStatsUI != null) playerStatsUI.Initialize(stats);
        if (potionUI != null) potionUI.Initialize(stats);

        string playerName = GetSafePlayerNickname(stats);
        if (playerNameText != null) playerNameText.text = playerName;
        if (infoPlayerNameText != null) infoPlayerNameText.text = playerName;
    }

    private void InitializeInfoPanel(PlayerStats stats)
    {
        InfoPanelManager infoPanelManager = FindFirstObjectByType<InfoPanelManager>();
        if (infoPanelManager != null)
        {
            infoPanelManager.Initialize(stats);
        }
    }

    private IEnumerator InitializeAllUISlots(PlayerStats stats)
    {
        yield return new WaitForSeconds(1f);
        
        UISlot[] allSlots = FindObjectsByType<UISlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach (UISlot slot in allSlots)
        {
            if (slot.playerNetworkObject == null) 
            {
                NetworkObject netObj = stats.GetComponent<NetworkObject>();
                slot.playerNetworkObject = netObj;
                slot.inventorySystem = stats.GetComponent<InventorySystem>();
                slot.equipmentSystem = stats.GetComponent<EquipmentSystem>();
            }
        }
    }

    #endregion
    // UIManager.cs'e bu bölümü ekle

#region ===== DEBUG SYSTEM =====

[Header("Debug System")]
[SerializeField] private Button debugResetLevelButton;

private void InitializeDebugSystem()
{
    if (debugResetLevelButton != null)
        debugResetLevelButton.onClick.AddListener(OnDebugResetLevelClicked);
}

private void OnDebugResetLevelClicked()
{
    GameObject localPlayer = FindLocalPlayerObject();
    if (localPlayer != null)
    {
        PlayerStats playerStats = localPlayer.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.DebugResetLevel();
        }
    }
}

#endregion
// UIManager.cs'e bu bölümleri ekle

    #region ===== CLASS SELECTION SYSTEM =====

    [Header("Class Selection System")]
[SerializeField] private GameObject classSelectionPanel;
[SerializeField] private Button warriorButton;
[SerializeField] private Button rangerButton;
[SerializeField] private Button rogueButton;
[SerializeField] private GameObject classSelectionOverlay;
public void ToggleClassPanel()
{
    if (classPanel == null)
    {
        Debug.LogError("[UIManager] classPanel null! Inspector'da atanmamış.");
        return;
    }

    bool isClassPanelActive = classPanel.activeSelf;

    if (!isClassPanelActive)
    {
        // Class panel açılırken InfoPanelManager'ı aç ve sadece ClassPanel'i göster
        if (InfoPanelManager.Instance != null)
        {
            // Chat kontrolü
            if (ChatManager.Instance != null && ChatManager.Instance.IsChatOpen())
            {
                ChatManager.Instance.ForceCloseChat();
            }
            
            // InfoPanel'i aç
            InfoPanelManager.Instance.infoPanel.SetActive(true);
            
            // Tüm panelleri kapat, sadece gerekli olanları aç
            InfoPanelManager.Instance.ShowPanels(
                showInventory: false,
                showItemInfo: false,
                showEquipment: false,
                showStats: false,
                showUpgrade: false,
                showMerchant: false,
                showPreview: false,
                showCraftInventory: false,
                showCraftNPC: false,
                showEquStatPanel: false
            );
        }

        // ClassPanel'i aç
        classPanel.SetActive(true);
        
        // ClassInfoDisplay'i initialize et
        InitializeClassInfoDisplay();
    }
    else
    {
        // Class panel kapatılırken InfoPanel'i de kapat
        classPanel.SetActive(false);
        
        if (InfoPanelManager.Instance != null)
        {
            InfoPanelManager.Instance.CloseInfoPanel();
        }
    }
}
private void InitializeClassInfoDisplay()
{

    // Önce classPanel içinde ara
    ClassInfoDisplay classInfoDisplay = null;
    if (classPanel != null)
    {
        classInfoDisplay = classPanel.GetComponentInChildren<ClassInfoDisplay>(true);
    }

    // classPanel'de bulunamazsa genel arama yap
    if (classInfoDisplay == null)
    {
        classInfoDisplay = FindFirstObjectByType<ClassInfoDisplay>(FindObjectsInactive.Include);
    }

    if (classInfoDisplay != null)
    {

        // Player bulma metodunu daha güvenli yap
        PlayerStats localPlayerStats = GetLocalPlayerStats();
        if (localPlayerStats != null)
        {
            classInfoDisplay.InitializeWithPlayer(localPlayerStats);
        }
        else
        {
            Debug.LogError($"[UIManager.InitializeClassInfoDisplay] Local player stats bulunamadı at {Time.time:F2}s!");
        }
    }
    else
    {
        Debug.LogError($"[UIManager.InitializeClassInfoDisplay] ClassInfoDisplay hiçbir yerde bulunamadı at {Time.time:F2}s!");
    }
}
private PlayerStats GetLocalPlayerStats()
{
    // Önce cached player'ı kontrol et
    GameObject localPlayer = FindLocalPlayerObject();
    if (localPlayer != null)
    {
        PlayerStats stats = localPlayer.GetComponent<PlayerStats>();
        if (stats != null)
        {
            return stats;
        }
    }
    
    // Fusion 2 uyumlu yöntem: Tüm PlayerStats'ları ara
    PlayerStats[] allStats = FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    foreach (PlayerStats stats in allStats)
    {
        if (stats.Object != null && stats.Object.HasInputAuthority)
        {
            return stats;
        }
    }
    
    // Son çare: GameObject tag ile arama
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj != null && netObj.HasInputAuthority)
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                return stats;
            }
        }
    }
    
    return null;
}
private void InitializeClassSelectionSystem()
{
    if (warriorButton != null)
        warriorButton.onClick.AddListener(() => OnClassSelected(ClassType.Warrior));
    if (rangerButton != null)
        rangerButton.onClick.AddListener(() => OnClassSelected(ClassType.Ranger));
    if (rogueButton != null)
        rogueButton.onClick.AddListener(() => OnClassSelected(ClassType.Rogue));
    
    // Class info panel toggle button
    if (classPanelToggleButton != null)
        classPanelToggleButton.onClick.AddListener(ToggleClassPanel);
        
    // Class panel'i başlangıçta kapat
    if (classPanel != null)
        classPanel.SetActive(false);
}

public void ShowClassSelectionPanel()
{
    
    if (classSelectionPanel == null) 
    {
        Debug.LogError("[UIManager] classSelectionPanel null! Inspector'da atanmamış.");
        return;
    }

    classSelectionPanel.SetActive(true);
}
private void OnClassSelected(ClassType selectedClass)
{
    
    // Local player'ı bul
    GameObject localPlayer = FindLocalPlayerObject();
    if (localPlayer != null)
    {
        ClassSystem classSystem = localPlayer.GetComponent<ClassSystem>();
        if (classSystem != null)
        {
            classSystem.SelectClass(selectedClass);
            HideClassSelectionPanel();

            // Skill UI'ı refresh et
            StartCoroutine(RefreshSkillUIAfterClassSelection());
        }

    }

}
private IEnumerator RefreshSkillUIAfterClassSelection()
{
    yield return new WaitForSeconds(0.5f); // Class sistem settle olması için bekle
    
    SkillSlotManager skillSlotManager = FindFirstObjectByType<SkillSlotManager>();
    if (skillSlotManager != null)
    {
        skillSlotManager.RefreshAllSkillIcons();
    }
}

private void HideClassSelectionPanel()
{
    if (classSelectionPanel != null)
    {
        classSelectionPanel.SetActive(false);
    }
    
    if (classSelectionOverlay != null)
    {
        classSelectionOverlay.SetActive(false);
    }
}
private GameObject FindLocalPlayerObject()
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj != null && netObj.HasInputAuthority)
            return player;
    }
    return null;
}

#endregion
    #region ===== QUEST SYSTEM UI =====

    [Header("Quest System")]
    [SerializeField] private QuestTracker questTracker;
    [SerializeField] private GameObject questDialogPanel;
    [SerializeField] private TextMeshProUGUI questTitleText;
    [SerializeField] private TextMeshProUGUI questDescriptionText;
    [SerializeField] private TextMeshProUGUI questObjectivesText;
    [SerializeField] private Image questNPCImage;
    [SerializeField] private Transform rewardsContainer;
    [SerializeField] private GameObject rewardItemPrefab;
    [SerializeField] private Button acceptQuestButton;
    [SerializeField] private Button declineQuestButton;
    [SerializeField] private Button completeQuestButton;

    [Header("Quest Reward Icons")]
    [SerializeField] private Sprite coinIcon;
    [SerializeField] private Sprite xpIcon;
    [SerializeField] private Sprite potionIcon;
    [SerializeField] private Sprite defaultItemIcon;

    private QuestGiver currentQuestGiver;
    private QuestData currentQuestData;

    private void InitializeQuestSystem()
    {
        if (acceptQuestButton != null)
            acceptQuestButton.onClick.AddListener(OnAcceptQuestClicked);
        if (declineQuestButton != null)
            declineQuestButton.onClick.AddListener(OnDeclineQuestClicked);
        if (completeQuestButton != null)
            completeQuestButton.onClick.AddListener(OnCompleteQuestClicked);
    }

    public void ShowQuestDialog(QuestData questData, QuestGiver questGiver)
    {
        if (questDialogPanel == null || questData == null) return;

        currentQuestData = questData;
        currentQuestGiver = questGiver;

        SetupQuestDialog(questData, true);
        questDialogPanel.SetActive(true);
        RefreshQuestTracker();
    }

    public void ShowQuestCompletionDialog(QuestData questData, QuestGiver questGiver)
    {
        if (questDialogPanel == null || questData == null) return;

        currentQuestData = questData;
        currentQuestGiver = questGiver;

        SetupQuestDialog(questData, false);
        questDialogPanel.SetActive(true);
        RefreshQuestTracker();
    }

    private void SetupQuestDialog(QuestData questData, bool isAccepting)
    {
        if (questTitleText != null)
            questTitleText.text = questData.questName;

        if (questDescriptionText != null)
            questDescriptionText.text = isAccepting ? questData.startDialogue : questData.completionDialogue;

        SetupQuestObjectives(questData, isAccepting);
        SetupQuestNPCImage(questData);
        ShowQuestRewards(questData.rewards);
        SetupQuestButtons(isAccepting);
    }

    private void SetupQuestObjectives(QuestData questData, bool isAccepting)
    {
        if (questObjectivesText != null)
        {
            string objectivesText = isAccepting ? "Gereksinimler:\n" : "Tamamlandı:\n";
            foreach (var objective in questData.objectives)
            {
                string targetName = GetTargetName(objective);
                objectivesText += $"• {targetName} x{objective.requiredAmount}\n";
            }
            questObjectivesText.text = objectivesText;
        }
    }

    private void SetupQuestNPCImage(QuestData questData)
    {
        if (questNPCImage != null)
        {
            if (questData.questIcon != null)
            {
                questNPCImage.sprite = questData.questIcon;
                questNPCImage.gameObject.SetActive(true);
            }
            else
            {
                questNPCImage.gameObject.SetActive(false);
            }
        }
    }

    private void SetupQuestButtons(bool isAccepting)
    {
        if (acceptQuestButton != null)
            acceptQuestButton.gameObject.SetActive(isAccepting);
        if (declineQuestButton != null)
            declineQuestButton.gameObject.SetActive(isAccepting);
        if (completeQuestButton != null)
            completeQuestButton.gameObject.SetActive(!isAccepting);
    }

    private void ShowQuestRewards(QuestReward rewards)
    {
        if (rewardsContainer == null) return;
        
        ClearRewardItems();
        
        if (rewards.xpReward > 0)
            CreateRewardItem("XP", rewards.xpReward.ToString(), xpIcon);
        if (rewards.coinReward > 0)
            CreateRewardItem("Coin", rewards.coinReward.ToString(), coinIcon);
        if (rewards.potionReward > 0)
            CreateRewardItem("Potion", $"x{rewards.potionReward}", potionIcon);
        
        ShowItemRewards(rewards.itemRewards);
    }

    private void ClearRewardItems()
    {
        foreach (Transform child in rewardsContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void ShowItemRewards(List<string> itemRewards)
    {
        if (itemRewards != null && itemRewards.Count > 0)
        {
            foreach (string itemId in itemRewards)
            {
                ItemData item = ItemDatabase.Instance.GetItemById(itemId);
                if (item != null)
                {
                    Sprite itemSprite = item.itemIcon != null ? item.itemIcon : defaultItemIcon;
                    CreateRewardItem(item.itemName, "x1", itemSprite);
                }
            }
        }
    }

    private void CreateRewardItem(string name, string amount, Sprite icon)
    {
        if (rewardItemPrefab == null || rewardsContainer == null) return;
            
        GameObject rewardObj = Instantiate(rewardItemPrefab, rewardsContainer);
        
        TextMeshProUGUI nameText = rewardObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null) nameText.text = name;
            
        TextMeshProUGUI amountText = rewardObj.transform.Find("AmountText")?.GetComponent<TextMeshProUGUI>();
        if (amountText != null) amountText.text = amount;
            
        Image iconImage = rewardObj.transform.Find("IconImage")?.GetComponent<Image>();
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);
        }
    }

    private string GetTargetName(QuestObjective objective)
    {
        switch (objective.type)
        {
            case QuestType.KillMonsters:
                return objective.targetId;

            case QuestType.CollectItems:
                ItemData item = ItemDatabase.Instance.GetItemById(objective.targetId);
                return item != null ? item.itemName : objective.targetId;

            case QuestType.ReachLocation:
                if (!string.IsNullOrEmpty(objective.description))
                {
                    if (TryParseLocationCoordinates(objective.targetId, out Vector2 coords))
                        return $"{objective.description} – [{coords.x:F0}, {coords.y:F0}]";
                    return objective.description;
                }
                else if (TryParseLocationCoordinates(objective.targetId, out Vector2 coordinates))
                {
                    return $"Konum: [{coordinates.x:F0}, {coordinates.y:F0}]";
                }
                return "Belirtilen Konuma Git";

            case QuestType.TalkToNPC:
                return !string.IsNullOrEmpty(objective.description) ? objective.description : objective.targetId;

            default:
                return objective.targetId;
        }
    }

    private bool TryParseLocationCoordinates(string locationString, out Vector2 coordinates)
    {
        coordinates = Vector2.zero;
        if (string.IsNullOrEmpty(locationString)) return false;
        
        char[] separators = { ',', ';', '|' };
        
        foreach (char separator in separators)
        {
            string[] parts = locationString.Split(separator);
            if (parts.Length == 2)
            {
                if (float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, 
                                   System.Globalization.CultureInfo.InvariantCulture, out float x) && 
                    float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, 
                                   System.Globalization.CultureInfo.InvariantCulture, out float y))
                {
                    coordinates = new Vector2(x, y);
                    return true;
                }
            }
        }
        return false;
    }

    private void OnAcceptQuestClicked()
    {
        if (currentQuestGiver != null && currentQuestData != null)
        {
            currentQuestGiver.OnQuestAccepted(currentQuestData.questId);
            CloseQuestDialog();
        }
    }

    private void OnDeclineQuestClicked()
    {
        CloseQuestDialog();
    }

    private void OnCompleteQuestClicked()
    {
        if (currentQuestGiver != null && currentQuestData != null)
        {
            currentQuestGiver.OnQuestTurnedIn(currentQuestData.questId);
            CloseQuestDialog();
        }
    }

    private void CloseQuestDialog()
    {
        questDialogPanel.SetActive(false);
        RefreshQuestTracker();
        currentQuestGiver = null;
        currentQuestData = null;
    }

    private void RefreshQuestTracker()
    {
        if (questTracker != null)
        {
            questTracker.RefreshQuestTracker();
        }
    }

    #endregion

    #region ===== TARGET SYSTEM UI =====

    [Header("Target System")]
    [SerializeField] private GameObject targetInfoPanel;
    [SerializeField] private TextMeshProUGUI targetNameText;
    [SerializeField] private TextMeshProUGUI targetHealthText;
    [SerializeField] private Image healthBarImage;

    [Header("Target System Settings")]
    [SerializeField] private float targetUpdateInterval = 0.2f;

    private GameObject currentTargetMonster;
    private GameObject currentPriorityTarget;
    private float nextTargetUpdateTime = 0f;
    private float nextUpdateTime = 0f;
    private float updateInterval = 0.2f;

    // Cache for performance
    private GameObject localPlayerCache;
    private WeaponSystem weaponSystemCache;
    private GameObject[] cachedMonsters;
    private float nextMonsterCacheUpdate;

    private void InitializeTargetSystem()
    {
        if (targetInfoPanel != null)
        {
            targetInfoPanel.SetActive(false);
        }
    }

    public void UpdateTargetInfo(string targetName, float currentHealth, float maxHealth)
    {
        if (targetInfoPanel == null || targetNameText == null || targetHealthText == null) return;

        targetInfoPanel.SetActive(true);

        if (targetNameText != null)
            targetNameText.text = targetName;

        if (targetHealthText != null)
            targetHealthText.text = $"{Mathf.Round(currentHealth)} / {Mathf.Round(maxHealth)}";

        if (healthBarImage != null)
        {
            float healthPercent = maxHealth > 0 ? currentHealth / maxHealth : 0;
            healthBarImage.fillAmount = healthPercent;
        }
    }

    public void SetCurrentTarget(GameObject monster)
    {
        currentTargetMonster = monster;
    }

    public void HideTargetInfo()
    {
        if (targetInfoPanel != null)
        {
            targetInfoPanel.SetActive(false);
            currentTargetMonster = null;
            currentPriorityTarget = null;
        }
    }

private void UpdateTargetPriorities()
{
    if (Runner == null || !Runner.IsRunning) return;

    UpdateLocalPlayerCache();
    if (localPlayerCache == null || weaponSystemCache == null) return;

    NetworkObject localNetObj = localPlayerCache.GetComponent<NetworkObject>();
    if (localNetObj == null || !localNetObj.HasInputAuthority) return;

    // MonsterManager kullanarak hedef bul - Fusion AOI guaranteed
    if (MonsterManager.Instance == null) return;
    
    float attackRange = weaponSystemCache.CurrentWeaponType == PlayerStats.WeaponType.Melee ? 2f : 5f;
    float detectionRange = attackRange * 3f;
    
    // Visible monsters al
    var nearbyMonsters = MonsterManager.Instance.GetMonstersInRadius(localPlayerCache.transform.position, detectionRange);
    
    GameObject nearestTarget = null;
    float minDistance = float.MaxValue;
    
    // En yakın visible target bul
    foreach (MonsterBehaviour monsterBehaviour in nearbyMonsters)
    {
        if (monsterBehaviour == null || monsterBehaviour.IsDead || !monsterBehaviour.IsVisibleToLocalPlayer) 
            continue;
            
        float distance = Vector2.Distance(localPlayerCache.transform.position, monsterBehaviour.transform.position);
        if (distance <= detectionRange && distance < minDistance)
        {
            minDistance = distance;
            nearestTarget = monsterBehaviour.gameObject;
        }
    }
    
    // PVP Check - manuel implementation
    if (nearestTarget == null)
    {
        PVPSystem localPVP = localPlayerCache.GetComponent<PVPSystem>();
        if (localPVP != null && localPVP.CanAttackPlayers())
        {
            nearestTarget = FindNearestPVPTarget(detectionRange);
        }
    }
    
    UpdateTargetDisplay(nearestTarget);
}

private GameObject FindNearestPVPTarget(float maxRange)
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    GameObject nearestPlayer = null;
    float minDistance = float.MaxValue;
    
    foreach (GameObject player in players)
    {
        if (player == localPlayerCache) continue;
        
        // Network validity
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsValid) continue;
        
        // Death check
        DeathSystem deathSystem = player.GetComponent<DeathSystem>();
        if (deathSystem != null && deathSystem.IsDead) continue;
        
        PlayerStats playerStats = player.GetComponent<PlayerStats>();
        if (playerStats == null || playerStats.NetworkCurrentHP <= 0) continue;
        
        // PVP zone check
        PVPSystem targetPVP = player.GetComponent<PVPSystem>();
        if (targetPVP == null || !targetPVP.GetSafePVPStatus()) continue;
        
        float distance = Vector2.Distance(localPlayerCache.transform.position, player.transform.position);
        if (distance <= maxRange && distance < minDistance)
        {
            minDistance = distance;
            nearestPlayer = player;
        }
    }
    
    return nearestPlayer;
}

    private void UpdateLocalPlayerCache()
    {
        if (localPlayerCache == null || !localPlayerCache.activeInHierarchy)
        {
            foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (player == null) continue;

                NetworkObject netObj = player.GetComponent<NetworkObject>();
                if (netObj != null && netObj.HasInputAuthority)
                {
                    localPlayerCache = player;
                    weaponSystemCache = player.GetComponent<WeaponSystem>();
                    break;
                }
            }
        }
    }

private void UpdateTargetDisplay(GameObject nearestTarget)
{
    if (nearestTarget != null && nearestTarget.activeInHierarchy)
    {
        if (currentPriorityTarget == null || currentPriorityTarget != nearestTarget)
        {
            currentPriorityTarget = nearestTarget;
            SetCurrentTarget(nearestTarget);
            
            // Target tipine göre UI güncelle
            if (nearestTarget.CompareTag("Monster"))
            {
                MonsterBehaviour monster = nearestTarget.GetComponent<MonsterBehaviour>();
                if (monster != null && monster.CurrentHealth > 0)
                {
                    UpdateTargetInfo(monster.monsterType, monster.CurrentHealth, monster.MaxHealth);
                }
            }
            else if (nearestTarget.CompareTag("Player"))
            {
                UpdatePlayerTargetInfo(nearestTarget);
            }
            
            if (targetInfoPanel != null)
                targetInfoPanel.SetActive(true);
        }
    }
    else if (currentPriorityTarget != null)
    {
        HideTargetInfo();
        currentPriorityTarget = null;
    }
}




private void UpdatePlayerTargetInfo(GameObject player)
{
    PlayerStats playerStats = player.GetComponent<PlayerStats>();
    if (playerStats == null) return;

    string playerName = playerStats.GetPlayerDisplayName();
    float currentHealth = playerStats.CurrentHP;
    float maxHealth = playerStats.GetNetworkMaxHP();

    // Seviye bilgisi ekle
    int playerLevel = playerStats.NetworkCurrentLevel;
    string displayName = $"{playerName} (Lv.{playerLevel})";

    UpdateTargetInfo(displayName, currentHealth, maxHealth);
}

    #endregion

    #region ===== NOTIFICATION SYSTEM =====

    [Header("Notification System")]
    [SerializeField] private Transform rewardNotificationContainer;
    [SerializeField] private GameObject rewardNotificationPrefab;
    [SerializeField] private float notificationDuration = 2f;

    private Queue<RewardNotificationData> rewardQueue = new Queue<RewardNotificationData>();
    private bool isShowingReward = false;

    [System.Serializable]
    public class RewardNotificationData
    {
        public string rewardText;
        public Sprite rewardIcon;
        public Color rewardColor;
    }

    private void InitializeNotificationSystem()
    {
        CreateRewardNotificationContainer();
    }

    public void ShowQuestRewardNotifications(QuestReward rewards)
    {
        if (rewards == null) return;

        CreateRewardNotificationContainer();
        EnqueueRewardNotifications(rewards);

        if (!isShowingReward)
        {
            StartCoroutine(ProcessRewardQueue());
        }
    }

    private void EnqueueRewardNotifications(QuestReward rewards)
    {
        if (rewards.xpReward > 0)
        {
            rewardQueue.Enqueue(new RewardNotificationData
            {
                rewardText = $"+{rewards.xpReward} XP",
                rewardIcon = xpIcon,
                rewardColor = new Color(0.4f, 0.8f, 1f)
            });
        }

        if (rewards.coinReward > 0)
        {
            rewardQueue.Enqueue(new RewardNotificationData
            {
                rewardText = $"+{rewards.coinReward} Gold",
                rewardIcon = coinIcon,
                rewardColor = Color.yellow
            });
        }

        if (rewards.potionReward > 0)
        {
            rewardQueue.Enqueue(new RewardNotificationData
            {
                rewardText = $"+{rewards.potionReward} Potion",
                rewardIcon = potionIcon,
                rewardColor = new Color(1f, 0.4f, 0.8f)
            });
        }

        EnqueueItemRewards(rewards.itemRewards);
    }

    private void EnqueueItemRewards(List<string> itemRewards)
    {
        if (itemRewards != null && itemRewards.Count > 0)
        {
            foreach (string itemId in itemRewards)
            {
                ItemData item = ItemDatabase.Instance.GetItemById(itemId);
                if (item != null)
                {
                    rewardQueue.Enqueue(new RewardNotificationData
                    {
                        rewardText = $"+1 {item.itemName}",
                        rewardIcon = item.itemIcon != null ? item.itemIcon : defaultItemIcon,
                        rewardColor = Color.green
                    });
                }
            }
        }
    }

    private IEnumerator ProcessRewardQueue()
    {
        isShowingReward = true;
        
        while (rewardQueue.Count > 0)
        {
            var rewardData = rewardQueue.Dequeue();
            yield return StartCoroutine(ShowSingleRewardNotification(rewardData));
            yield return new WaitForSeconds(0.3f);
        }
        
        isShowingReward = false;
    }

    private IEnumerator ShowSingleRewardNotification(RewardNotificationData rewardData)
    {
        if (rewardNotificationContainer == null) yield break;
        
        GameObject notificationObj = CreateRewardNotificationObject(rewardData);
        if (notificationObj == null) yield break;
        
        yield return StartCoroutine(AnimateRewardNotification(notificationObj));
        
        Destroy(notificationObj);
    }

    private IEnumerator AnimateRewardNotification(GameObject notificationObj)
    {
        RectTransform notificationRect = notificationObj.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = notificationObj.GetComponent<CanvasGroup>();
        
        Vector2 startPos = new Vector2(300, 0);
        Vector2 targetPos = Vector2.zero;
        Vector2 endPos = new Vector2(-300, 0);
        
        notificationRect.anchoredPosition = startPos;
        canvasGroup.alpha = 0f;
        
        // Slide in animation
        yield return StartCoroutine(AnimateSlideIn(notificationRect, canvasGroup, startPos, targetPos));
        
        // Wait duration
        yield return new WaitForSeconds(notificationDuration);
        
        // Slide out animation
        yield return StartCoroutine(AnimateSlideOut(notificationRect, canvasGroup, targetPos, endPos));
    }

    private IEnumerator AnimateSlideIn(RectTransform rect, CanvasGroup canvasGroup, Vector2 start, Vector2 target)
    {
        float animDuration = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animDuration;
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            
            rect.anchoredPosition = Vector2.Lerp(start, target, easedT);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, easedT);
            
            yield return null;
        }
        
        rect.anchoredPosition = target;
        canvasGroup.alpha = 1f;
    }

    private IEnumerator AnimateSlideOut(RectTransform rect, CanvasGroup canvasGroup, Vector2 start, Vector2 target)
    {
        float animDuration = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animDuration;
            float easedT = Mathf.Pow(t, 3f);
            
            rect.anchoredPosition = Vector2.Lerp(start, target, easedT);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, easedT);
            
            yield return null;
        }
    }

    private GameObject CreateRewardNotificationObject(RewardNotificationData rewardData)
    {
        if (rewardNotificationContainer == null || rewardNotificationPrefab == null) return null;
        
        GameObject notificationObj = Instantiate(rewardNotificationPrefab, rewardNotificationContainer);
        
        Image iconImage = notificationObj.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImage != null) iconImage.sprite = rewardData.rewardIcon;
        
        TextMeshProUGUI rewardText = notificationObj.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
        if (rewardText != null)
        {
            rewardText.text = rewardData.rewardText;
            rewardText.color = rewardData.rewardColor;
        }
        
        return notificationObj;
    }

    private void CreateRewardNotificationContainer()
    {
        if (rewardNotificationContainer != null) return;

        Canvas mainCanvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (mainCanvas == null) return;

        GameObject containerObj = new GameObject("RewardNotificationContainer");
        containerObj.transform.SetParent(mainCanvas.transform, false);
        rewardNotificationContainer = containerObj.transform;

        RectTransform containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(1f, 1f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.pivot = new Vector2(1f, 1f);
        containerRect.sizeDelta = new Vector2(300, 400);
        containerRect.anchoredPosition = new Vector2(-20, -20);
    }

    public void ShowNotification(string message)
    {
        // TODO: Genel bildirim sistemi için implement edilebilir
    }

    #endregion

    #region ===== PLAYER NAME TAG SYSTEM =====

[Header("Player Name Tags")]
[SerializeField] private float nameTagHeight = 0.75f;
[SerializeField] private float nameTagScale = 0.2f;
[SerializeField] private TMP_FontAsset playerNameFont; // YENİ SATIR

    private Dictionary<int, TextMeshProUGUI> playerNameTags = new Dictionary<int, TextMeshProUGUI>();

    private void InitializePlayerNameTags(PlayerStats stats)
    {
        NetworkObject netObj = stats.GetComponent<NetworkObject>();
        if (netObj != null && IsNetworkObjectValid(netObj))
        {
            string playerName = GetSafePlayerNickname(stats);
            StartCoroutine(CreateNameTagWithDelay(netObj.Id.GetHashCode(), playerName, stats.gameObject));
        }
    }

    private IEnumerator InitializeExistingPlayerTags()
    {
        if (Runner == null || !Runner.IsClient)
        {
            yield return new WaitUntil(() => Runner != null && Runner.IsClient);
        }

        yield return new WaitForSeconds(1f);

        try 
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            if (players != null)
            {
                foreach (GameObject playerObj in players)
                {
                    if (playerObj != null)
                    {
                        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
                        PlayerStats playerStats = playerObj.GetComponent<PlayerStats>();
                        
                        if (netObj != null && playerStats != null && IsNetworkObjectValid(netObj))
                        {
                            string playerName = GetSafePlayerNickname(playerStats);
                            CreateOrUpdateNameTag(netObj.Id.GetHashCode(), playerName, playerObj);
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UIManager] Error in InitializeExistingPlayerTags: {e.Message}");
        }
    }

    public void UpdatePlayerNameTag(int objectId, string playerName, GameObject playerObject)
    {
        CreateOrUpdateNameTag(objectId, playerName, playerObject);
    }

    private IEnumerator CreateNameTagWithDelay(int objectId, string playerName, GameObject playerObject)
    {
        yield return new WaitForSeconds(0.1f);
        CreateOrUpdateNameTag(objectId, playerName, playerObject);
    }

    private void CreateOrUpdateNameTag(int objectId, string playerName, GameObject playerObject)
    {
        try
        {
            if (playerNameTags.TryGetValue(objectId, out TextMeshProUGUI existingText) && existingText != null)
            {
                existingText.text = playerName;
                return;
            }

            if (playerNameTags.ContainsKey(objectId))
            {
                playerNameTags.Remove(objectId);
            }

            CreateNewNameTag(objectId, playerName, playerObject);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error creating name tag for {playerName}: {e.Message}");
        }
    }

private void CreateNewNameTag(int objectId, string playerName, GameObject playerObject)
{
    GameObject canvasObj = new GameObject($"NameCanvas_{objectId}");
    canvasObj.transform.SetParent(playerObject.transform);
    
    Canvas nameCanvas = canvasObj.AddComponent<Canvas>();
    nameCanvas.renderMode = RenderMode.WorldSpace;
    nameCanvas.sortingOrder = 5;
    nameCanvas.sortingLayerName = "Player";

    CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
    scaler.scaleFactor = 1;
    scaler.dynamicPixelsPerUnit = 100;

    GameObject textObj = new GameObject("NameText");
    textObj.transform.SetParent(canvasObj.transform, false);

    TextMeshProUGUI nameText = textObj.AddComponent<TextMeshProUGUI>();
    nameText.alignment = TextAlignmentOptions.Center;
    nameText.fontSize = 3;
    nameText.color = Color.white;
    nameText.text = playerName;
    
    // Font ataması - YENİ SATIRLAR
    if (playerNameFont != null)
    {
        nameText.font = playerNameFont;
    }

    RectTransform textTransform = nameText.GetComponent<RectTransform>();
    textTransform.sizeDelta = new Vector2(100, 30);

    canvasObj.transform.localPosition = Vector3.up * nameTagHeight;
    canvasObj.transform.localScale = Vector3.one * nameTagScale;
    textTransform.localPosition = new Vector3(0, 7, 0);
    
    playerNameTags[objectId] = nameText;
}

    private string GetSafePlayerNickname(PlayerStats playerStats)
    {
        try
        {
            if (playerStats.Object != null && playerStats.Object.IsValid)
            {
                if ((playerStats.Object.HasInputAuthority || playerStats.Object.HasStateAuthority || Runner.IsServer) &&
                    !string.IsNullOrEmpty(playerStats.NetworkPlayerNickname))
                {
                    return playerStats.NetworkPlayerNickname;
                }
                
                if (!string.IsNullOrEmpty(playerStats.NetworkPlayerNickname))
                {
                    return playerStats.NetworkPlayerNickname;
                }
            }
        }
        catch (System.Exception)
        {
            // Network property access error
        }

        if (playerStats.Object != null && playerStats.Object.HasInputAuthority)
        {
            return playerStats.GetPlayerDisplayName();
        }
        
        if (playerStats.Object != null && playerStats.Object.IsValid)
        {
            return $"Player_{playerStats.Object.Id.GetHashCode() % 1000}";
        }
        
        return "Unknown Player";
    }

    private bool IsNetworkObjectValid(NetworkObject netObj)
    {
        try
        {
            if (netObj == null) return false;
            if (!netObj) return false;
            if (!netObj.IsValid) return false;
            if (netObj.gameObject == null) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void CheckAndUpdatePlayerNicknames()
    {
        try
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            
            foreach (GameObject playerObj in players)
            {
                if (playerObj == null) continue;
                
                NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
                PlayerStats playerStats = playerObj.GetComponent<PlayerStats>();
                
                if (netObj != null && playerStats != null && IsNetworkObjectValid(netObj))
                {
                    int objectId = netObj.Id.GetHashCode();
                    string currentNetworkName = GetSafePlayerNickname(playerStats);
                    
                    if (!string.IsNullOrEmpty(currentNetworkName))
                    {
                        if (playerNameTags.TryGetValue(objectId, out TextMeshProUGUI nameTag))
                        {
                            if (nameTag != null && nameTag.text != currentNetworkName)
                            {
                                nameTag.text = currentNetworkName;
                            }
                        }
                        else
                        {
                            CreateOrUpdateNameTag(objectId, currentNetworkName, playerObj);
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UIManager] CheckAndUpdatePlayerNicknames hatası: {e.Message}");
        }
    }

    #endregion

    #region ===== MAIN QUEST SYSTEM =====

    [Header("Main Quest System")]
    [SerializeField] private GameObject mainQuestPanel;
    [SerializeField] private Image mainQuestNPCImage;
    [SerializeField] private TextMeshProUGUI mainQuestNPCNameText;
    [SerializeField] private TextMeshProUGUI mainQuestContentText;
    [SerializeField] private Button mainQuestNextButton;
    [SerializeField] private GameObject mainQuestOverlay;

    [Header("Typewriter Settings")]
    [SerializeField] private float typewriterSpeed = 0.05f;
    [SerializeField] private AudioClip typewriterSound;
    [SerializeField] private bool canSkipTypewriter = true;

    private string[] currentMainQuestTexts;
    private int currentMainQuestIndex = 0;
    private System.Action onMainQuestCompleteCallback;
    private Coroutine typewriterCoroutine;
    private bool isTypewriting = false;
    private bool skipCurrentTypewriter = false;
    private AudioSource audioSource;

    public void ShowMainQuestPanel(string[] mainQuestTexts, Sprite npcSprite, string npcName, System.Action onComplete)
    {
        if (mainQuestPanel == null || mainQuestTexts == null || mainQuestTexts.Length == 0) return;
        
        currentMainQuestTexts = mainQuestTexts;
        currentMainQuestIndex = 0;
        onMainQuestCompleteCallback = onComplete;
        
        CreateOrActivateMainQuestOverlay();
        mainQuestPanel.SetActive(true);
        
        SetupMainQuestNPC(npcSprite, npcName);
        ShowCurrentMainQuest();
        SetupMainQuestButton();
    }

    private void SetupMainQuestNPC(Sprite npcSprite, string npcName)
    {
        if (mainQuestNPCImage != null)
        {
            mainQuestNPCImage.sprite = npcSprite;
            mainQuestNPCImage.gameObject.SetActive(npcSprite != null);
        }
        
        if (mainQuestNPCNameText != null)
        {
            mainQuestNPCNameText.text = npcName;
        }
    }

    private void SetupMainQuestButton()
    {
        if (mainQuestNextButton != null)
        {
            mainQuestNextButton.onClick.RemoveAllListeners();
            mainQuestNextButton.onClick.AddListener(OnMainQuestNextClicked);
        }
    }

    private void ShowCurrentMainQuest()
    {
        if (currentMainQuestTexts == null || currentMainQuestIndex >= currentMainQuestTexts.Length) return;
        
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        
        string textToShow = currentMainQuestTexts[currentMainQuestIndex];
        typewriterCoroutine = StartCoroutine(TypewriterEffect(textToShow));
        
        UpdateMainQuestButtonText();
    }

    private void OnMainQuestNextClicked()
    {
        if (isTypewriting && canSkipTypewriter)
        {
            skipCurrentTypewriter = true;
            return;
        }

        if (!isTypewriting)
        {
            currentMainQuestIndex++;

            if (currentMainQuestIndex >= currentMainQuestTexts.Length)
            {
                if (onMainQuestCompleteCallback != null)
                {
                    onMainQuestCompleteCallback.Invoke();
                }
                HideMainQuestPanel();
            }
            else
            {
                ShowCurrentMainQuest();
            }
        }
    }

    private IEnumerator TypewriterEffect(string textToDisplay)
    {
        isTypewriting = true;
        skipCurrentTypewriter = false;
        
        if (mainQuestContentText == null)
        {
            isTypewriting = false;
            yield break;
        }
        
        PrepareAudioSource();
        mainQuestContentText.text = "";
        
        for (int i = 0; i < textToDisplay.Length; i++)
        {
            if (skipCurrentTypewriter)
            {
                mainQuestContentText.text = textToDisplay;
                break;
            }
            
            mainQuestContentText.text += textToDisplay[i];
            
            PlayTypewriterSound(textToDisplay[i]);
            
            float waitTime = typewriterSpeed;
            if (char.IsPunctuation(textToDisplay[i]))
            {
                waitTime *= 3f;
            }
            
            yield return new WaitForSeconds(waitTime);
        }
        
        isTypewriting = false;
        skipCurrentTypewriter = false;
        UpdateMainQuestButtonText();
    }

    private void PlayTypewriterSound(char character)
    {
        if (!char.IsWhiteSpace(character) && 
            !char.IsPunctuation(character) && 
            typewriterSound != null && 
            audioSource != null)
        {
            audioSource.PlayOneShot(typewriterSound, 0.3f);
        }
    }

    private void PrepareAudioSource()
    {
        if (audioSource == null && typewriterSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            audioSource.volume = 0.5f;
            audioSource.playOnAwake = false;
        }
    }

    private void UpdateMainQuestButtonText()
    {
        if (mainQuestNextButton != null)
        {
            TextMeshProUGUI buttonText = mainQuestNextButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                if (isTypewriting && canSkipTypewriter)
                {
                    buttonText.text = "Atla";
                }
                else
                {
                    buttonText.text = (currentMainQuestIndex >= currentMainQuestTexts.Length - 1) ? "Tamam" : "İleri";
                }
            }
        }
    }

    private void CreateOrActivateMainQuestOverlay()
    {
        if (mainQuestOverlay == null)
        {
            Canvas mainCanvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
            if (mainCanvas == null) return;
            
            mainQuestOverlay = new GameObject("MainQuestOverlay");
            mainQuestOverlay.transform.SetParent(mainCanvas.transform, false);
            
            SetupOverlayTransform();
            SetupOverlayVisuals();
            SetupOverlayCanvas();
            
            mainQuestOverlay.SetActive(false);
        }
        
        mainQuestOverlay.SetActive(true);
        EnsureMainQuestPanelOnTop();
    }

    private void SetupOverlayTransform()
    {
        RectTransform overlayRect = mainQuestOverlay.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
    }

    private void SetupOverlayVisuals()
    {
        Image overlayImage = mainQuestOverlay.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.6f);
    }

    private void SetupOverlayCanvas()
    {
        Canvas overlayCanvas = mainQuestOverlay.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 90;
        
        mainQuestOverlay.AddComponent<GraphicRaycaster>();
    }

    private void EnsureMainQuestPanelOnTop()
    {
        if (mainQuestPanel != null)
        {
            Canvas questCanvas = mainQuestPanel.GetComponent<Canvas>();
            if (questCanvas == null)
            {
                questCanvas = mainQuestPanel.AddComponent<Canvas>();
            }
            questCanvas.overrideSorting = true;
            questCanvas.sortingOrder = 100;
            
            GraphicRaycaster questRaycaster = mainQuestPanel.GetComponent<GraphicRaycaster>();
            if (questRaycaster == null)
            {
                questRaycaster = mainQuestPanel.AddComponent<GraphicRaycaster>();
            }
        }
    }

    public void HideMainQuestPanel()
    {
        StopTypewriter();
        
        if (mainQuestPanel != null)
        {
            mainQuestPanel.SetActive(false);
        }
        
        if (mainQuestOverlay != null)
        {
            mainQuestOverlay.SetActive(false);
        }
        
        CleanupMainQuest();
    }

    private void StopTypewriter()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        
        isTypewriting = false;
        skipCurrentTypewriter = false;
    }

    private void CleanupMainQuest()
    {
        currentMainQuestTexts = null;
        currentMainQuestIndex = 0;
        onMainQuestCompleteCallback = null;
    }

    #endregion

    #region ===== MINIMAP SYSTEM =====

    [Header("Minimap")]
    [SerializeField] private MinimapController minimapController;

    #endregion

    #region ===== UTILITY & HELPER METHODS =====

// UIManager.cs'de InitializeHeadPreview metodunu değiştir:
private void InitializeHeadPreview()
{
    if (headPreview == null)
    {
        var previewObj = new GameObject("HeadPreview");
        previewObj.transform.SetParent(transform, false);
        headPreview = previewObj.AddComponent<HeadPreviewManager>();
        
        // Hemen başlat, delay'siz
        var snapshotManager = previewObj.GetComponent<HeadSnapshotManager>();
        if (snapshotManager == null)
        {
            previewObj.AddComponent<HeadSnapshotManager>();
        }
    }
}

    #endregion

    #region ===== UPDATE LOOP =====

    private void Update()
    {    
        // Target system updates
        if (Time.time >= nextTargetUpdateTime)
        {
            UpdateTargetPriorities();
            nextTargetUpdateTime = Time.time + (targetUpdateInterval * 2f);
        }

        // Player nickname sync (every 3 seconds)
        if (Time.time % 3f < 0.1f)
        {
            CheckAndUpdatePlayerNicknames();
        }
        
        // Target info panel updates
        UpdateTargetInfoPanel();
    }

private void UpdateTargetInfoPanel()
{
    if (targetInfoPanel != null && targetInfoPanel.activeSelf && currentTargetMonster != null)
    {
        if (Time.time >= nextUpdateTime)
        {
            // Monster target
            if (currentTargetMonster.CompareTag("Monster"))
            {
                MonsterBehaviour monster = currentTargetMonster.GetComponent<MonsterBehaviour>();
                if (monster != null)
                {
                    UpdateTargetInfo(monster.monsterType, monster.CurrentHealth, monster.MaxHealth);
                    nextUpdateTime = Time.time + (updateInterval * 3f);
                }
                else
                {
                    HideTargetInfo();
                    currentTargetMonster = null;
                }
            }
            // Player target
            else if (currentTargetMonster.CompareTag("Player"))
            {
                UpdatePlayerTargetInfo(currentTargetMonster);
                nextUpdateTime = Time.time + (updateInterval * 3f);
            }
        }
    }
}

    #endregion
}