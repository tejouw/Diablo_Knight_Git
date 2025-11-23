using UnityEngine;
using System.Collections.Generic;
using Fusion;
using System.Linq;
using System;
using Firebase.Database;
using System.Collections;

public enum QuestStatus
{
    NotStarted,
    InProgress,
    Completed,
    TurnedIn
}

[Serializable]
public class PlayerQuest
{
    public string questId;
    public QuestStatus status;
    public List<QuestObjective> objectives = new List<QuestObjective>();
    public QuestObjective hiddenObjective;
    public bool isHiddenObjectiveActive = false;

    public PlayerQuest() { }

    public PlayerQuest(QuestData questData)
    {
        questId = questData.questId;
        status = QuestStatus.InProgress;

        foreach (var objective in questData.objectives)
        {
            var newObjective = new QuestObjective
            {
                type = objective.type,
                targetId = objective.targetId,
                requiredAmount = objective.requiredAmount,
                currentAmount = 0,
                description = objective.description,
                useCompass = objective.useCompass,
                compassCoordinates = objective.compassCoordinates,
                alternativeTargetIds = objective.alternativeTargetIds,
                requiresItemGive = objective.requiresItemGive,
                requiredItemId = objective.requiredItemId,
                requiredItemAmount = objective.requiredItemAmount,
                objectiveDialogues = objective.objectiveDialogues,
                alternativeTargetDialogues = objective.alternativeTargetDialogues,
                minUpgradeLevel = objective.minUpgradeLevel
            };
            objectives.Add(newObjective);
        }

        if (questData.hiddenObjective != null)
        {
            hiddenObjective = new QuestObjective
            {
                type = questData.hiddenObjective.type,
                targetId = questData.hiddenObjective.targetId,
                requiredAmount = questData.hiddenObjective.requiredAmount,
                currentAmount = 0,
                description = questData.hiddenObjective.description,
                useCompass = questData.hiddenObjective.useCompass,
                compassCoordinates = questData.hiddenObjective.compassCoordinates,
                alternativeTargetIds = questData.hiddenObjective.alternativeTargetIds,
                requiresItemGive = questData.hiddenObjective.requiresItemGive,
                requiredItemId = questData.hiddenObjective.requiredItemId,
                requiredItemAmount = questData.hiddenObjective.requiredItemAmount,
                objectiveDialogues = questData.hiddenObjective.objectiveDialogues,
                alternativeTargetDialogues = questData.hiddenObjective.alternativeTargetDialogues,
                minUpgradeLevel = questData.hiddenObjective.minUpgradeLevel
            };
            isHiddenObjectiveActive = false;
        }
    }
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;
    [SerializeField] private List<QuestData> availableQuests = new List<QuestData>();
    private Dictionary<string, PlayerQuest> playerQuests = new Dictionary<string, PlayerQuest>();
    private Dictionary<string, QuestData> questDatabase = new Dictionary<string, QuestData>();
    private PlayerStats playerStats;
    private bool introQuestStarted = false;
    private string previousSceneName = "";
    private bool isInitializing = false;
    private bool isNetworkReady = false;
    private EquipmentSystem currentListeningEquipment = null;

    // New components for improved persistence and state management
    private QuestSystem.QuestPersistence questPersistence;
    private QuestSystem.QuestStateManager questStateManager;
    private QuestSystem.QuestSaveQueue questSaveQueue;

    public event Action<string> OnQuestStarted;
    public event Action<string> OnQuestCompleted;
    public event Action<string> OnQuestUpdated;
    public event Action<string> OnQuestTurnedIn;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize new components
        questPersistence = gameObject.AddComponent<QuestSystem.QuestPersistence>();
        questStateManager = gameObject.AddComponent<QuestSystem.QuestStateManager>();
        questSaveQueue = gameObject.AddComponent<QuestSystem.QuestSaveQueue>();

        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        InitializeQuestDatabase();
        previousSceneName = currentSceneName;
    }

    private void Start()
    {
        BaseNPC.OnNPCInteraction += HandleNPCInteraction;
        StartCoroutine(ListenToEquipmentEvents());
        if (LoadingManager.Instance != null) LoadingManager.Instance.CompleteStep("QuestManager");
        StartCoroutine(BackgroundInitialization());
        StartCoroutine(CheckForActiveLocationQuests());
        if (QuestManager.Instance != null) QuestManager.Instance.OnQuestTurnedIn += OnQuestCompleted;
    }

    private void OnDestroy()
    {
        // CRITICAL: Force save before destruction to prevent data loss
        if (questSaveQueue != null)
        {
            bool saveSuccess = questSaveQueue.ForceSaveBlocking();
        }

        BaseNPC.OnNPCInteraction -= HandleNPCInteraction;
        CleanupEquipmentEvents();
        if (NetworkManager.Instance != null) NetworkManager.Instance.OnNetworkReady -= OnNetworkReady;
    }

    private void OnApplicationQuit()
    {
        // CRITICAL: Force save on application quit
        if (questSaveQueue != null)
        {
            bool saveSuccess = questSaveQueue.ForceSaveBlocking();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // MOBILE: Save when app goes to background (Android/iOS)
        if (pauseStatus && questSaveQueue != null)
        {
            bool saveSuccess = questSaveQueue.ForceSaveBlocking();
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshNetworkManagerConnection();
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (NetworkManager.Instance != null) NetworkManager.Instance.OnNetworkReady -= OnNetworkReady;
    }

    private void InitializeQuestDatabase()
    {
        questDatabase.Clear();
        if (availableQuests == null || availableQuests.Count == 0) return;
        foreach (var quest in availableQuests)
        {
            if (quest == null || string.IsNullOrEmpty(quest.questId)) continue;
            if (!questDatabase.ContainsKey(quest.questId)) questDatabase.Add(quest.questId, quest);
        }
    }

    private bool IsInGameScene()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainGame";
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        string currentSceneName = scene.name;
        if (currentSceneName == "MainGame" && previousSceneName != "MainGame")
        {
            StartCoroutine(RefreshNetworkConnectionAfterSceneLoad());
        }
        previousSceneName = currentSceneName;
    }

    private IEnumerator RefreshNetworkConnectionAfterSceneLoad()
    {
        yield return new WaitForSeconds(1f);
        RefreshNetworkManagerConnection();
        int attempts = 0;
        while (attempts < 40)
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.Runner != null)
            {
                if (NetworkManager.Instance.Runner.IsRunning || NetworkManager.Instance.Runner.IsStarting || !NetworkManager.Instance.Runner.IsShutdown)
                {
                    CheckNetworkStatusForInit();
                    yield break;
                }
            }
            attempts++;
            yield return new WaitForSeconds(0.5f);
        }
        CheckNetworkStatusForInit();
    }

    public bool HasCompletedQuestForNPC(string npcId)
    {
        foreach (var questPair in playerQuests)
        {
            if (questPair.Value.status == QuestStatus.Completed && questDatabase.TryGetValue(questPair.Key, out QuestData questData))
            {
                if (questData.questTurnInNPC == npcId) return true;
            }
        }
        return false;
    }

    public bool HasInProgressQuestFromNPC(string npcId)
    {
        foreach (var questPair in playerQuests)
        {
            if (questPair.Value.status == QuestStatus.InProgress && questDatabase.TryGetValue(questPair.Key, out QuestData questData))
            {
                if (questData.questGiverNPC == npcId) return true;
            }
        }
        return false;
    }

    public QuestData GetInProgressQuestFromNPC(string npcId)
    {
        foreach (var questPair in playerQuests)
        {
            if (questPair.Value.status == QuestStatus.InProgress && questDatabase.TryGetValue(questPair.Key, out QuestData questData) && questData.questGiverNPC == npcId)
            {
                return questData;
            }
        }
        return null;
    }

    private IEnumerator CheckForActiveLocationQuests()
    {
        float lastLogTime = 0f;
        while (true)
        {
            yield return new WaitForSeconds(1f);
            bool shouldLog = Time.time - lastLogTime >= 1f;
            if (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null || !NetworkManager.Instance.Runner.IsConnectedToServer)
            {
                if (shouldLog) lastLogTime = Time.time;
                continue;
            }
            if (playerQuests != null && QuestCompass.Instance != null)
            {
                GameObject localPlayer = null;
                foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
                {
                    NetworkObject networkObject = player.GetComponent<NetworkObject>();
                    if (networkObject != null && networkObject.HasInputAuthority)
                    {
                        localPlayer = player;
                        break;
                    }
                }
                if (localPlayer == null)
                {
                    if (shouldLog) lastLogTime = Time.time;
                    continue;
                }
                Vector2 playerPosition = localPlayer.transform.position;
                bool hasActiveCompassQuest = false;
                Vector2 closestTargetLocation = Vector2.zero;
                float closestDistance = float.MaxValue;
                foreach (var questPair in playerQuests)
                {
                    PlayerQuest playerQuest = questPair.Value;
                    if (playerQuest.status != QuestStatus.InProgress) continue;
                    foreach (var objective in playerQuest.objectives)
                    {
                        if (objective.IsCompleted) continue;
                        if (objective.useCompass && !string.IsNullOrEmpty(objective.compassCoordinates))
                        {
                            if (TryParseLocation(objective.compassCoordinates, out Vector2 targetLocation))
                            {
                                float distance = Vector2.Distance(playerPosition, targetLocation);
                                if (distance < closestDistance)
                                {
                                    closestDistance = distance;
                                    closestTargetLocation = targetLocation;
                                    hasActiveCompassQuest = true;
                                }
                            }
                        }
                    }
                    if (playerQuest.isHiddenObjectiveActive && playerQuest.hiddenObjective != null && !playerQuest.hiddenObjective.IsCompleted)
                    {
                        if (playerQuest.hiddenObjective.useCompass && !string.IsNullOrEmpty(playerQuest.hiddenObjective.compassCoordinates))
                        {
                            if (TryParseLocation(playerQuest.hiddenObjective.compassCoordinates, out Vector2 targetLocation))
                            {
                                float distance = Vector2.Distance(playerPosition, targetLocation);
                                if (distance < closestDistance)
                                {
                                    closestDistance = distance;
                                    closestTargetLocation = targetLocation;
                                    hasActiveCompassQuest = true;
                                }
                            }
                        }
                    }
                }
                if (shouldLog) lastLogTime = Time.time;
                if (hasActiveCompassQuest && closestDistance > 6f) QuestCompass.Instance.ShowCompass(closestTargetLocation);
                else QuestCompass.Instance.HideCompass();
            }
        }
    }

    private void CheckNetworkStatusForInit()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.Runner != null && NetworkManager.Instance.Runner.IsConnectedToServer && IsInGameScene())
        {
            bool hasPlayer = false;
            foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
            {
                NetworkObject networkObject = player.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.HasInputAuthority)
                {
                    hasPlayer = true;
                    break;
                }
            }
            if (hasPlayer) StartCoroutine(InitializePlayerStatsWithRetry());
        }
    }

    private string GetPlayerNickname()
    {
        if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.playerNickname)) return NetworkManager.Instance.playerNickname;
        if (playerStats != null)
        {
            string displayName = playerStats.GetPlayerDisplayName();
            if (!string.IsNullOrEmpty(displayName) && displayName != "Unknown Player") return displayName;
        }
        string nickname = PlayerPrefs.GetString("Nickname", "");
        if (!string.IsNullOrEmpty(nickname)) return nickname;
        return "Player";
    }

    // Player'ın ırkını al
    private PlayerRace GetPlayerRace()
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null)
            return PlayerRace.Human; // Fallback

        // Local player'ı bul
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.HasInputAuthority)
            {
                // RaceManager'dan player'ın ırkını al
                if (RaceManager.Instance != null && RaceManager.Instance.TryGetPlayerRace(networkObject.InputAuthority, out PlayerRace race))
                {
                    return race;
                }
                break;
            }
        }

        // Fallback: PlayerPrefs'ten al
        string nickname = GetPlayerNickname();
        string raceKey = "PlayerRace_" + nickname;
        if (PlayerPrefs.HasKey(raceKey))
        {
            string raceString = PlayerPrefs.GetString(raceKey);
            if (System.Enum.TryParse<PlayerRace>(raceString, out PlayerRace race))
                return race;
        }

        return PlayerRace.Human; // Default fallback
    }

    private void OnNetworkReady()
    {
        if (isInitializing || (playerStats != null && isNetworkReady)) return;
        StartCoroutine(QuickPlayerInitialization());
    }

    private IEnumerator QuickPlayerInitialization()
    {
        isInitializing = true;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
            {
                NetworkObject networkObject = player.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.HasInputAuthority)
                {
                    playerStats = player.GetComponent<PlayerStats>();
                    if (playerStats != null)
                    {
                        isNetworkReady = true;
                        StartCoroutine(LoadQuestDataBackground());
                        isInitializing = false;
                        yield break;
                    }
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
        isInitializing = false;
    }

    private IEnumerator LoadQuestDataBackground()
    {
        yield return new WaitForSeconds(0.5f);

        // Begin loading state
        questStateManager.BeginLoading();

        string nickname = GetPlayerNickname();
        if (string.IsNullOrEmpty(nickname))
        {
            questStateManager.SetError("Player nickname not found");
            yield break;
        }

        bool questLoadingComplete = false;
        bool questLoadingSuccess = false;

        StartCoroutine(LoadPlayerQuestsCoroutineWithCallback((success) =>
        {
            questLoadingComplete = true;
            questLoadingSuccess = success;
        }));

        float timeout = 5f;
        float elapsed = 0f;
        while (!questLoadingComplete && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        // Ensure dictionary initialized
        if (playerQuests == null)
        {
            playerQuests = new Dictionary<string, PlayerQuest>();
        }

        // Initialize save queue now that we have nickname and quests loaded
        if (questSaveQueue != null && questPersistence != null)
        {
            questSaveQueue.Initialize(questPersistence, playerQuests, nickname);
        }

        yield return new WaitForEndOfFrame();

        // Check inventory for quest items
        foreach (var questPair in playerQuests)
        {
            if (questPair.Value.status == QuestStatus.InProgress)
            {
                CheckInventoryForQuestItems(questPair.Key);
                CheckEquipmentForQuests(questPair.Key);
            }
        }

        UpdateQuestMarkersForNPCs();
        RefreshQuestTrackerUI();

        // Mark as ready
        if (questLoadingSuccess || questLoadingComplete)
        {
            questStateManager.SetReady();
        }
        else
        {
            questStateManager.SetError("Quest loading failed or timed out");
        }

        // Start intro quest for new players
        bool isNewPlayer = (playerQuests == null || playerQuests.Count == 0);
        if (isNewPlayer && !introQuestStarted)
        {
            yield return new WaitForSeconds(1f);
            if (NetworkManager.Instance != null && NetworkManager.Instance.Runner != null &&
                NetworkManager.Instance.Runner.IsRunning && UIManager.Instance != null)
            {
                StartIntroQuestForNewPlayer();
            }
        }
    }

    private IEnumerator LoadPlayerQuestsCoroutineWithCallback(System.Action<bool> onComplete)
    {
        bool isComplete = false;
        bool isSuccess = false;
        LoadPlayerQuestsAsync().ContinueWith(task =>
        {
            isComplete = true;
            isSuccess = !task.IsFaulted;
            if (task.IsFaulted && playerQuests == null) playerQuests = new Dictionary<string, PlayerQuest>();
        }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => isComplete);
        onComplete?.Invoke(isSuccess);
    }

    private async System.Threading.Tasks.Task LoadPlayerQuestsAsync()
    {
        try
        {
            if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
            {
                return;
            }

            string nickname = GetPlayerNickname();
            if (string.IsNullOrEmpty(nickname))
            {
                return;
            }

            // Timeout protection
            var timeoutTask = System.Threading.Tasks.Task.Delay(3000);
            var loadTask = LoadFirebaseQuests(nickname);
            var completedTask = await System.Threading.Tasks.Task.WhenAny(loadTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                return;
            }

            await loadTask;
        }
        catch (System.Exception)
        {
        }
    }

    private async System.Threading.Tasks.Task LoadFirebaseQuests(string nickname)
    {
        // FIXED: Use QuestPersistence with MERGE strategy instead of REPLACE
        // This prevents wiping out locally started quests during load

        // Initialize playerQuests dictionary if null (first load only)
        if (playerQuests == null)
        {
            playerQuests = new Dictionary<string, PlayerQuest>();
        }

        // Get dirty quest IDs from save queue (these have unsaved local changes)
        HashSet<string> dirtyQuests = new HashSet<string>();
        if (questSaveQueue != null)
        {
            foreach (var questId in playerQuests.Keys)
            {
                if (questSaveQueue.IsDirty(questId))
                {
                    dirtyQuests.Add(questId);
                }
            }
        }

        // Load and MERGE (not replace!) with existing data
        bool success = await questPersistence.LoadQuestsAsync(nickname, playerQuests, dirtyQuests);

    }

    public bool IsNetworkReady()
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null) return false;
        return isNetworkReady && playerQuests != null && NetworkManager.Instance.Runner.IsConnectedToServer;
    }

    private void CheckInventoryForQuestItems(string questId)
    {
        if (!playerQuests.TryGetValue(questId, out PlayerQuest playerQuest) || playerQuest.status != QuestStatus.InProgress) return;
        InventorySystem inventory = null;
        CraftInventorySystem craftInventory = null;
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
            {
                inventory = player.GetComponent<InventorySystem>();
                craftInventory = player.GetComponent<CraftInventorySystem>();
                break;
            }
        }
        if (inventory == null) return;
        bool progressUpdated = false;
        foreach (var objective in playerQuest.objectives)
        {
            if (objective.type == QuestType.CollectItems && !objective.IsCompleted)
            {
                int itemCount = 0;
                foreach (var slot in inventory.GetAllSlots().Values)
                {
                    if (!slot.isEmpty && slot.item != null && slot.item.itemId == objective.targetId) itemCount += slot.amount;
                }
                if (craftInventory != null)
                {
                    foreach (var slot in craftInventory.GetAllCraftSlots().Values)
                    {
                        if (!slot.isEmpty && slot.item != null && slot.item.itemId == objective.targetId) itemCount += slot.amount;
                    }
                }
                if (itemCount > 0)
                {
                    int newProgress = Mathf.Min(itemCount, objective.requiredAmount);
                    if (newProgress > objective.currentAmount)
                    {
                        objective.currentAmount = newProgress;
                        progressUpdated = true;
                    }
                }
            }
        }
        if (playerQuest.isHiddenObjectiveActive && playerQuest.hiddenObjective != null && playerQuest.hiddenObjective.type == QuestType.CollectItems && !playerQuest.hiddenObjective.IsCompleted)
        {
            int itemCount = 0;
            foreach (var slot in inventory.GetAllSlots().Values)
            {
                if (!slot.isEmpty && slot.item != null && slot.item.itemId == playerQuest.hiddenObjective.targetId) itemCount += slot.amount;
            }
            if (craftInventory != null)
            {
                foreach (var slot in craftInventory.GetAllCraftSlots().Values)
                {
                    if (!slot.isEmpty && slot.item != null && slot.item.itemId == playerQuest.hiddenObjective.targetId) itemCount += slot.amount;
                }
            }
            if (itemCount > 0)
            {
                int newProgress = Mathf.Min(itemCount, playerQuest.hiddenObjective.requiredAmount);
                if (newProgress > playerQuest.hiddenObjective.currentAmount)
                {
                    playerQuest.hiddenObjective.currentAmount = newProgress;
                    progressUpdated = true;
                }
            }
        }
        if (progressUpdated)
        {
            OnQuestUpdated?.Invoke(questId);
            CheckQuestCompletion();
        }
    }

    private IEnumerator InitializePlayerStatsWithRetry()
    {
        try
        {
            int maxAttempts = 10;
            int attemptCount = 0;
            bool playerFound = false;
            while (attemptCount < maxAttempts && !playerFound)
            {
                attemptCount++;
                foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
                {
                    NetworkObject networkObject = player.GetComponent<NetworkObject>();
                    if (networkObject != null && networkObject.HasInputAuthority)
                    {
                        playerStats = player.GetComponent<PlayerStats>();
                        if (playerStats == null) break;
                        playerFound = true;
                        string nickname = GetPlayerNickname();
                        if (!string.IsNullOrEmpty(nickname)) LoadPlayerQuests();
                        break;
                    }
                }
                if (!playerFound)
                {
                    foreach (NetworkObject netObj in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
                    {
                        if (netObj.HasInputAuthority)
                        {
                            playerStats = netObj.GetComponent<PlayerStats>();
                            if (playerStats != null)
                            {
                                playerFound = true;
                                string nickname = GetPlayerNickname();
                                if (!string.IsNullOrEmpty(nickname)) LoadPlayerQuests();
                                break;
                            }
                        }
                    }
                }
                if (!playerFound && attemptCount < maxAttempts) yield return new WaitForSeconds(1f);
            }
            if (playerFound && LoadingManager.Instance != null) LoadingManager.Instance.CompleteStep("QuestManager");
        }
        finally
        {
            isInitializing = false;
        }
    }

    public void CheckDialogQuestAutoCompletion(string questId)
    {
        if (!playerQuests.TryGetValue(questId, out PlayerQuest playerQuest) || !questDatabase.TryGetValue(questId, out QuestData questData)) return;
        if (questData.isDialogQuest && playerQuest.status == QuestStatus.Completed && !questData.HasCompletionDialogs) TurnInQuest(questId);
    }

    public async void ResetAllQuests()
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null) return;
        try
        {
            playerQuests.Clear();
            if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
            {
                string nickname = GetPlayerNickname();
                if (!string.IsNullOrEmpty(nickname))
                {
                    var database = FirebaseManager.Instance.GetDatabase();
                    if (database != null)
                    {
                        DatabaseReference questRef = database.GetReference("players").Child(nickname).Child("quests");
                        await questRef.RemoveValueAsync();
                    }
                }
            }
            UpdateQuestMarkersForNPCs();
            RefreshQuestTrackerUI();
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Tüm questler sıfırlandı!");
        }
        catch (System.Exception) { }
    }

    private void RefreshQuestTrackerUI()
    {
        foreach (QuestTracker tracker in FindObjectsByType<QuestTracker>(FindObjectsSortMode.None))
        {
            if (tracker != null) tracker.RefreshQuestTracker();
        }
        OnQuestUpdated?.Invoke("refresh_all_quests");
    }

    private void HandleNPCInteraction(string npcId, int playerRefId)
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null) return;
        bool isLocalPlayer = false;
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.HasInputAuthority)
            {
                isLocalPlayer = true;
                break;
            }
        }
        if (!isLocalPlayer) return;
        foreach (var questPair in playerQuests)
        {
            if (questPair.Value.status == QuestStatus.InProgress) UpdateQuestProgress(questPair.Key, QuestType.TalkToNPC, npcId);
        }
    }

    private void LoadPlayerQuests()
    {
        StartCoroutine(LoadPlayerQuestsCoroutine());
    }

    private IEnumerator LoadPlayerQuestsCoroutine()
    {
        bool isComplete = false;
        LoadPlayerQuestsAsync().ContinueWith(task => { isComplete = true; }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => isComplete);
    }

    private PlayerQuest ParsePlayerQuestFromDict(Dictionary<string, object> questDict)
    {
        if (questDict == null) return null;
        try
        {
            PlayerQuest playerQuest = new PlayerQuest();
            if (questDict.TryGetValue("status", out object statusObj)) playerQuest.status = (QuestStatus)System.Convert.ToInt32(statusObj);
            if (questDict.TryGetValue("isHiddenObjectiveActive", out object hiddenActiveObj)) playerQuest.isHiddenObjectiveActive = System.Convert.ToBoolean(hiddenActiveObj);
            if (questDict.TryGetValue("objectives", out object objectivesObj))
            {
                if (objectivesObj is List<object> objectivesList)
                {
                    foreach (var objItem in objectivesList)
                    {
                        if (objItem is Dictionary<string, object> objectiveDict)
                        {
                            QuestObjective objective = new QuestObjective();
                            if (objectiveDict.TryGetValue("type", out object typeObj)) objective.type = (QuestType)System.Convert.ToInt32(typeObj);
                            if (objectiveDict.TryGetValue("targetId", out object targetIdObj)) objective.targetId = targetIdObj?.ToString() ?? "";
                            if (objectiveDict.TryGetValue("alternativeTargetIds", out object altTargetsObj) && altTargetsObj is List<object> altTargetsList) objective.alternativeTargetIds = altTargetsList.Select(t => t?.ToString() ?? "").ToArray();
                            if (objectiveDict.TryGetValue("requiredAmount", out object requiredAmountObj)) objective.requiredAmount = System.Convert.ToInt32(requiredAmountObj);
                            if (objectiveDict.TryGetValue("currentAmount", out object currentAmountObj)) objective.currentAmount = System.Convert.ToInt32(currentAmountObj);
                            if (objectiveDict.TryGetValue("description", out object descriptionObj)) objective.description = descriptionObj?.ToString() ?? "";
                            if (objectiveDict.TryGetValue("useCompass", out object useCompassObj)) objective.useCompass = System.Convert.ToBoolean(useCompassObj);
                            if (objectiveDict.TryGetValue("compassCoordinates", out object compassCoordsObj)) objective.compassCoordinates = compassCoordsObj?.ToString() ?? "";
                            if (objectiveDict.TryGetValue("requiresItemGive", out object requiresItemGiveObj)) objective.requiresItemGive = System.Convert.ToBoolean(requiresItemGiveObj);
                            if (objectiveDict.TryGetValue("requiredItemId", out object requiredItemIdObj)) objective.requiredItemId = requiredItemIdObj?.ToString() ?? "";
                            if (objectiveDict.TryGetValue("requiredItemAmount", out object requiredItemAmountObj)) objective.requiredItemAmount = System.Convert.ToInt32(requiredItemAmountObj);
                            else objective.requiredItemAmount = 1;
                            if (objectiveDict.TryGetValue("objectiveDialogues", out object objDialoguesObj) && objDialoguesObj is List<object> dialoguesList) objective.objectiveDialogues = dialoguesList.Select(d => d?.ToString() ?? "").ToArray();
                            if (objectiveDict.TryGetValue("minUpgradeLevel", out object minUpgradeLevelObj)) objective.minUpgradeLevel = System.Convert.ToInt32(minUpgradeLevelObj);
                            else objective.minUpgradeLevel = 0;
                            playerQuest.objectives.Add(objective);
                        }
                    }
                }
            }
            if (questDict.TryGetValue("hiddenObjective", out object hiddenObjData) && hiddenObjData is Dictionary<string, object> hiddenDict)
            {
                QuestObjective hiddenObjective = new QuestObjective();
                if (hiddenDict.TryGetValue("type", out object typeObj)) hiddenObjective.type = (QuestType)System.Convert.ToInt32(typeObj);
                if (hiddenDict.TryGetValue("targetId", out object targetIdObj)) hiddenObjective.targetId = targetIdObj?.ToString() ?? "";
                if (hiddenDict.TryGetValue("alternativeTargetIds", out object altTargetsObj) && altTargetsObj is List<object> altTargetsList) hiddenObjective.alternativeTargetIds = altTargetsList.Select(t => t?.ToString() ?? "").ToArray();
                if (hiddenDict.TryGetValue("requiredAmount", out object requiredAmountObj)) hiddenObjective.requiredAmount = System.Convert.ToInt32(requiredAmountObj);
                if (hiddenDict.TryGetValue("currentAmount", out object currentAmountObj)) hiddenObjective.currentAmount = System.Convert.ToInt32(currentAmountObj);
                if (hiddenDict.TryGetValue("description", out object descriptionObj)) hiddenObjective.description = descriptionObj?.ToString() ?? "";
                if (hiddenDict.TryGetValue("useCompass", out object useCompassObj)) hiddenObjective.useCompass = System.Convert.ToBoolean(useCompassObj);
                if (hiddenDict.TryGetValue("compassCoordinates", out object compassCoordsObj)) hiddenObjective.compassCoordinates = compassCoordsObj?.ToString() ?? "";
                if (hiddenDict.TryGetValue("requiresItemGive", out object requiresItemGiveObj)) hiddenObjective.requiresItemGive = System.Convert.ToBoolean(requiresItemGiveObj);
                if (hiddenDict.TryGetValue("requiredItemId", out object requiredItemIdObj)) hiddenObjective.requiredItemId = requiredItemIdObj?.ToString() ?? "";
                if (hiddenDict.TryGetValue("requiredItemAmount", out object requiredItemAmountObj)) hiddenObjective.requiredItemAmount = System.Convert.ToInt32(requiredItemAmountObj);
                else hiddenObjective.requiredItemAmount = 1;
                if (hiddenDict.TryGetValue("objectiveDialogues", out object hiddenDialoguesObj) && hiddenDialoguesObj is List<object> hiddenDialoguesList) hiddenObjective.objectiveDialogues = hiddenDialoguesList.Select(d => d?.ToString() ?? "").ToArray();
                if (hiddenDict.TryGetValue("minUpgradeLevel", out object hiddenMinUpgradeLevelObj)) hiddenObjective.minUpgradeLevel = System.Convert.ToInt32(hiddenMinUpgradeLevelObj);
                else hiddenObjective.minUpgradeLevel = 0;
                playerQuest.hiddenObjective = hiddenObjective;
            }
            return playerQuest;
        }
        catch (System.Exception) { return null; }
    }

    private void StartIntroQuestForNewPlayer()
    {
        // Player'ın ırkını al
        PlayerRace playerRace = GetPlayerRace();

        // Irka göre intro quest ID'sini belirle
        string introQuestId = "";
        switch (playerRace)
        {
            case PlayerRace.Human:
                introQuestId = "intro_quest_human";
                break;
            case PlayerRace.Goblin:
                introQuestId = "intro_quest_goblin";
                break;
            default:
                introQuestId = "intro_quest"; // Fallback eski sistem için
                break;
        }

        // Irka özel intro quest yoksa, genel intro quest'i dene
        if (!questDatabase.ContainsKey(introQuestId))
        {
            introQuestId = "intro_quest"; // Fallback
        }

        if (questDatabase.TryGetValue(introQuestId, out QuestData introQuest) && introQuest.isDialogQuest)
        {
            if (StartQuest(introQuestId)) StartCoroutine(ShowIntroQuestDialogWithDelay(introQuest));
        }
    }

    private IEnumerator ShowIntroQuestDialogWithDelay(QuestData questData)
    {
        yield return new WaitForSeconds(1f);
        if (UIManager.Instance != null && questData.startDialogues != null && questData.startDialogues.Length > 0)
        {
            UIManager.Instance.ShowMainQuestPanel(questData.startDialogues, questData.questIcon, "Başkılavuz Leren", () => { });
        }
    }

    /// <summary>
    /// DEPRECATED: This method has been replaced by QuestSaveQueue system.
    /// Use questSaveQueue.QueueSave() instead for throttled, reliable saves.
    /// Kept for backward compatibility - now delegates to QuestSaveQueue.
    /// </summary>
    [System.Obsolete("Use questSaveQueue.QueueSave() instead", false)]
    public async void SavePlayerQuests()
    {
        // DEPRECATED: Replaced by QuestPersistence + QuestSaveQueue
        // This method is no longer used internally - all calls have been replaced with QueueSave()
        // Delegating to new system for backward compatibility

        if (questSaveQueue != null)
        {
            // Force async save of all dirty quests
            await questSaveQueue.ForceSaveAsync();
        }
        else
        {
        }
    }

    public bool HasAvailableQuestFromNPC(string npcId)
    {
        PlayerRace playerRace = GetPlayerRace();

        foreach (var quest in questDatabase.Values)
        {
            if (quest.questGiverNPC != npcId) continue;

            // Irk kontrolü - quest bu ırk için uygun mu?
            if (!quest.IsAvailableForRace(playerRace)) continue;

            if (!string.IsNullOrEmpty(quest.previousQuestId) && !IsQuestTurnedIn(quest.previousQuestId)) continue;
            if (IsQuestStarted(quest.questId) || IsQuestTurnedIn(quest.questId)) continue;
            return true;
        }
        return false;
    }

    public QuestData GetAvailableQuestFromNPC(string npcId)
    {
        PlayerRace playerRace = GetPlayerRace();

        foreach (var quest in questDatabase.Values)
        {
            // Irk kontrolü - quest bu ırk için uygun mu?
            if (!quest.IsAvailableForRace(playerRace)) continue;

            if (quest.questGiverNPC == npcId && (string.IsNullOrEmpty(quest.previousQuestId) || IsQuestTurnedIn(quest.previousQuestId)) && !IsQuestStarted(quest.questId) && !IsQuestTurnedIn(quest.questId)) return quest;
        }
        return null;
    }

    public QuestData GetCompletedQuestForNPC(string npcId)
    {
        foreach (var questPair in playerQuests)
        {
            if (questPair.Value.status == QuestStatus.Completed && questDatabase.TryGetValue(questPair.Key, out QuestData questData) && questData.questTurnInNPC == npcId) return questData;
        }
        return null;
    }

    public bool StartQuest(string questId)
    {
        // Initialization guard: Prevent operations before quest system is ready
        if (!questStateManager.IsReady)
        {
            questStateManager.ExecuteWhenReady(() => StartQuest(questId));
            return false;
        }

        if (IsQuestStarted(questId) || IsQuestTurnedIn(questId)) return false;
        if (!questDatabase.TryGetValue(questId, out QuestData questData)) return false;
        if (!string.IsNullOrEmpty(questData.previousQuestId) && !IsQuestTurnedIn(questData.previousQuestId)) return false;
        PlayerQuest playerQuest = new PlayerQuest(questData);
        playerQuests[questId] = playerQuest;
        CheckInventoryForQuestItems(questId);
        CheckEquipmentForQuests(questId);
        if (ChatManager.Instance != null) ChatManager.Instance.ShowQuestStartMessage(questData.questName);
        OnQuestStarted?.Invoke(questId);

        // REPLACED: SavePlayerQuests() -> QueueSave (critical: quest started)
        if (questSaveQueue != null)
            questSaveQueue.QueueSave(questId, isCritical: true);

        return true;
    }

    public void UpdateQuestProgress(string ignoredQuestId, QuestType type, string targetId, int amount = 1)
    {
        // Initialization guard: Prevent operations before quest system is ready
        if (!questStateManager.IsReady)
        {
            return;
        }

        bool anyUpdated = false;
        HashSet<string> updatedQuestIds = new HashSet<string>(); // Track which quests were updated
        foreach (var questPair in playerQuests)
        {
            string questId = questPair.Key;
            PlayerQuest playerQuest = questPair.Value;
            if (playerQuest.status != QuestStatus.InProgress) continue;
            foreach (var objective in playerQuest.objectives)
            {
                if (objective.type == type)
                {
                    bool matches = false;
                    if (type == QuestType.BindToBindstone || type == QuestType.PickupEquipment) matches = true;
                    else if (type == QuestType.ReachLocation) matches = objective.MatchesTarget(targetId);
                    else if (type == QuestType.BuyFromMerchant) matches = (string.IsNullOrEmpty(objective.targetId) || objective.targetId.ToLower() == "any") ? true : objective.MatchesTarget(targetId);
                    else matches = objective.MatchesTarget(targetId);
                    if (matches)
                    {
                        bool wasUpdated = false;
                        switch (type)
                        {
                            case QuestType.KillMonsters:
                                if (objective.currentAmount < objective.requiredAmount) { objective.currentAmount++; wasUpdated = true; }
                                break;
                            case QuestType.CollectItems:
                                int newAmount = Mathf.Min(objective.currentAmount + amount, objective.requiredAmount);
                                if (newAmount != objective.currentAmount) { objective.currentAmount = newAmount; wasUpdated = true; }
                                break;
                            case QuestType.TalkToNPC:
                                if (objective.requiresItemGive && !string.IsNullOrEmpty(objective.requiredItemId))
                                {
                                    int newTalkAmount = Mathf.Min(objective.currentAmount + amount, objective.requiredAmount);
                                    if (newTalkAmount != objective.currentAmount) { objective.currentAmount = newTalkAmount; wasUpdated = true; }
                                }
                                else if (objective.currentAmount == 0) { objective.currentAmount = 1; wasUpdated = true; }
                                break;
                            case QuestType.ReachLocation:
                            case QuestType.BindToBindstone:
                                if (objective.currentAmount == 0) { objective.currentAmount = 1; wasUpdated = true; }
                                break;
                            case QuestType.PickupEquipment:
                                if (objective.currentAmount < objective.requiredAmount) { objective.currentAmount++; wasUpdated = true; }
                                break;
                            case QuestType.BuyFromMerchant:
                                if (objective.currentAmount < objective.requiredAmount) { objective.currentAmount++; wasUpdated = true; }
                                break;
                        }
                        if (wasUpdated)
                        {
                            anyUpdated = true;
                            updatedQuestIds.Add(questId);
                            OnQuestUpdated?.Invoke(questId);
                        }
                    }
                }
            }
            if (playerQuest.isHiddenObjectiveActive && playerQuest.hiddenObjective != null && playerQuest.hiddenObjective.type == type)
            {
                bool matches = false;
                if (type == QuestType.BindToBindstone || type == QuestType.PickupEquipment) matches = true;
                else if (type == QuestType.ReachLocation) matches = playerQuest.hiddenObjective.MatchesTarget(targetId);
                else matches = playerQuest.hiddenObjective.MatchesTarget(targetId);
                if (matches)
                {
                    bool wasUpdated = false;
                    switch (type)
                    {
                        case QuestType.KillMonsters:
                            if (playerQuest.hiddenObjective.currentAmount < playerQuest.hiddenObjective.requiredAmount) { playerQuest.hiddenObjective.currentAmount++; wasUpdated = true; }
                            break;
                        case QuestType.CollectItems:
                            int newAmount = Mathf.Min(playerQuest.hiddenObjective.currentAmount + amount, playerQuest.hiddenObjective.requiredAmount);
                            if (newAmount != playerQuest.hiddenObjective.currentAmount) { playerQuest.hiddenObjective.currentAmount = newAmount; wasUpdated = true; }
                            break;
                        case QuestType.TalkToNPC:
                            if (playerQuest.hiddenObjective.requiresItemGive && !string.IsNullOrEmpty(playerQuest.hiddenObjective.requiredItemId))
                            {
                                int newTalkAmount = Mathf.Min(playerQuest.hiddenObjective.currentAmount + amount, playerQuest.hiddenObjective.requiredAmount);
                                if (newTalkAmount != playerQuest.hiddenObjective.currentAmount) { playerQuest.hiddenObjective.currentAmount = newTalkAmount; wasUpdated = true; }
                            }
                            else if (playerQuest.hiddenObjective.currentAmount == 0) { playerQuest.hiddenObjective.currentAmount = 1; wasUpdated = true; }
                            break;
                        case QuestType.ReachLocation:
                        case QuestType.BindToBindstone:
                            if (playerQuest.hiddenObjective.currentAmount == 0) { playerQuest.hiddenObjective.currentAmount = 1; wasUpdated = true; }
                            break;
                        case QuestType.PickupEquipment:
                            if (playerQuest.hiddenObjective.currentAmount < playerQuest.hiddenObjective.requiredAmount) { playerQuest.hiddenObjective.currentAmount++; wasUpdated = true; }
                            break;
                    }
                    if (wasUpdated)
                    {
                        anyUpdated = true;
                        updatedQuestIds.Add(questId);
                        OnQuestUpdated?.Invoke(questId);
                    }
                }
            }
        }
        if (anyUpdated)
        {
            CheckQuestCompletion();

            // REPLACED: SavePlayerQuests() -> QueueSave for each updated quest (non-critical: batched)
            if (questSaveQueue != null)
            {
                foreach (string questId in updatedQuestIds)
                {
                    questSaveQueue.QueueSave(questId, isCritical: false); // Batched every 3s
                }
            }
        }
    }

    public void HandleNPCInteractionDirect(string npcId, int playerRefId)
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null) return;
        bool isLocalPlayer = false;
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.HasInputAuthority)
            {
                isLocalPlayer = true;
                break;
            }
        }
        if (!isLocalPlayer) return;
        foreach (var questPair in playerQuests)
        {
            if (questPair.Value.status == QuestStatus.InProgress) UpdateQuestProgress(questPair.Key, QuestType.TalkToNPC, npcId);
        }
    }

    private void CheckQuestCompletion()
    {
        List<string> completedQuestIds = new List<string>();
        bool anyHiddenActivated = false;
        foreach (var questPair in playerQuests)
        {
            string questId = questPair.Key;
            PlayerQuest playerQuest = questPair.Value;
            if (playerQuest.status == QuestStatus.InProgress)
            {
                bool allMainObjectivesCompleted = true;
                foreach (var objective in playerQuest.objectives)
                {
                    if (!objective.IsCompleted)
                    {
                        allMainObjectivesCompleted = false;
                        break;
                    }
                }
                if (allMainObjectivesCompleted && playerQuest.hiddenObjective != null && !playerQuest.isHiddenObjectiveActive)
                {
                    playerQuest.isHiddenObjectiveActive = true;
                    anyHiddenActivated = true;
                    OnQuestUpdated?.Invoke(questId);

                    // REPLACED: SavePlayerQuests() -> QueueSave (critical: hidden objective activated)
                    if (questSaveQueue != null)
                        questSaveQueue.QueueSave(questId, isCritical: true);
                }
                bool questFullyCompleted = allMainObjectivesCompleted;
                if (playerQuest.hiddenObjective != null) questFullyCompleted = allMainObjectivesCompleted && playerQuest.isHiddenObjectiveActive && playerQuest.hiddenObjective.IsCompleted;
                if (questFullyCompleted)
                {
                    playerQuest.status = QuestStatus.Completed;
                    completedQuestIds.Add(questId);
                    CheckDialogQuestAutoCompletion(questId);
                }
            }
        }
        foreach (var questId in completedQuestIds)
        {
            OnQuestCompleted?.Invoke(questId);
        }

        // REPLACED: SavePlayerQuests() -> QueueSave for each completed quest (critical)
        if (completedQuestIds.Count > 0 && questSaveQueue != null)
        {
            foreach (string questId in completedQuestIds)
            {
                questSaveQueue.QueueSave(questId, isCritical: true); // Quest completion is critical
            }
        }

        if (anyHiddenActivated) UpdateQuestMarkersForNPCs();
    }

    public bool TurnInQuest(string questId)
    {
        // Initialization guard: Prevent operations before quest system is ready
        if (!questStateManager.IsReady)
        {
            return false;
        }

        if (!playerQuests.TryGetValue(questId, out PlayerQuest playerQuest) || playerQuest.status != QuestStatus.Completed) return false;
        if (!questDatabase.TryGetValue(questId, out QuestData questData)) return false;
        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.ShowQuestCompleteMessage(questData.questName);
            ChatManager.Instance.ShowQuestRewardMessage(questData.rewards);
        }
        if (UIManager.Instance != null) UIManager.Instance.ShowQuestRewardNotifications(questData.rewards);
        GiveQuestRewards(questData.rewards);
        playerQuest.status = QuestStatus.TurnedIn;
        if (!string.IsNullOrEmpty(questData.nextQuestId)) StartChainQuest(questData.nextQuestId, questData.questName);
        else UpdateQuestMarkersForNPCs();
        OnQuestTurnedIn?.Invoke(questId);

        // REPLACED: SavePlayerQuests() -> QueueSave (critical: quest turned in)
        if (questSaveQueue != null)
            questSaveQueue.QueueSave(questId, isCritical: true);

        return true;
    }

    private void StartChainQuest(string nextQuestId, string completedQuestName)
    {
        if (!IsQuestAvailable(nextQuestId))
        {
            UpdateQuestMarkersForNPCs();
            return;
        }
        bool questStarted = StartQuest(nextQuestId);
        if (questStarted)
        {
            if (UIManager.Instance != null)
            {
                QuestData nextQuestData = GetQuestData(nextQuestId);
                if (nextQuestData != null) UIManager.Instance.ShowNotification($"Yeni Görev: {nextQuestData.questName}");
            }
        }
        else UpdateQuestMarkersForNPCs();
    }

    private void GiveQuestRewards(QuestReward rewards)
    {
        if (playerStats == null) return;
        if (rewards.xpReward > 0) playerStats.GainXP(rewards.xpReward);
        if (rewards.coinReward > 0) playerStats.AddCoins(rewards.coinReward);
        if (rewards.itemRewards != null && rewards.itemRewards.Count > 0)
        {
            InventorySystem inventory = playerStats.GetComponent<InventorySystem>();
            if (inventory != null)
            {
                foreach (string itemId in rewards.itemRewards)
                {
                    ItemData item = ItemDatabase.Instance.GetItemById(itemId);
                    if (item != null) inventory.TryAddItem(item);
                }
            }
        }
        if (rewards.potionReward > 0) playerStats.AddPotion(rewards.potionReward);
    }

    public void HandleMonsterDeath(string monsterType, Vector3 position, PlayerRef killer)
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null) return;
        bool isLocalPlayerKiller = false;
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.HasInputAuthority && networkObject.InputAuthority == killer)
            {
                isLocalPlayerKiller = true;
                break;
            }
        }
        if (!isLocalPlayerKiller) return;
        foreach (var questPair in playerQuests)
        {
            if (questPair.Value.status == QuestStatus.InProgress) UpdateQuestProgress(questPair.Key, QuestType.KillMonsters, monsterType);
        }
    }

    public void UpdateQuestMarkersForNPCs()
    {
        foreach (var questGiver in UnityEngine.Object.FindObjectsByType<QuestGiver>(FindObjectsSortMode.None)) questGiver.UpdateQuestMarker();
        foreach (var dialogQuestGiver in UnityEngine.Object.FindObjectsByType<DialogQuestGiver>(FindObjectsSortMode.None))
        {
            dialogQuestGiver.UpdateQuestStatus();
            dialogQuestGiver.UpdateQuestMarker();
        }
    }

    public void CheckLocationReached(Vector2 playerPosition)
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null || playerQuests == null || !isNetworkReady) return;
        foreach (var questPair in playerQuests)
        {
            string questId = questPair.Key;
            PlayerQuest playerQuest = questPair.Value;
            if (playerQuest.status == QuestStatus.InProgress && playerQuest.objectives != null)
            {
                foreach (var objective in playerQuest.objectives)
                {
                    if (objective == null) continue;
                    if (objective.type == QuestType.ReachLocation && !objective.IsCompleted)
                    {
                        if (TryParseLocation(objective.targetId, out Vector2 targetLocation))
                        {
                            float distance = Vector2.Distance(playerPosition, targetLocation);
                            if (distance <= 8f) UpdateQuestProgress(questId, QuestType.ReachLocation, objective.targetId);
                        }
                    }
                }
            }
        }
    }

    private bool TryParseLocation(string locationString, out Vector2 location)
    {
        location = Vector2.zero;
        if (string.IsNullOrEmpty(locationString)) return false;
        char[] separators = { ',', ';', '|' };
        foreach (char separator in separators)
        {
            string[] parts = locationString.Split(separator);
            if (parts.Length == 2)
            {
                if (float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
                {
                    location = new Vector2(x, y);
                    return true;
                }
            }
        }
        return false;
    }

    public bool IsQuestAvailable(string questId)
    {
        if (!questDatabase.TryGetValue(questId, out QuestData questData)) return false;

        // Irk kontrolü - quest bu ırk için uygun mu?
        PlayerRace playerRace = GetPlayerRace();
        if (!questData.IsAvailableForRace(playerRace)) return false;

        if (IsQuestStarted(questId) || IsQuestTurnedIn(questId)) return false;
        if (!string.IsNullOrEmpty(questData.previousQuestId) && !IsQuestTurnedIn(questData.previousQuestId)) return false;
        return true;
    }

    public bool IsQuestStarted(string questId)
    {
        return playerQuests.TryGetValue(questId, out PlayerQuest playerQuest) && (playerQuest.status == QuestStatus.InProgress || playerQuest.status == QuestStatus.Completed);
    }

    public bool IsQuestCompleted(string questId)
    {
        return playerQuests.TryGetValue(questId, out PlayerQuest playerQuest) && playerQuest.status == QuestStatus.Completed;
    }

    public bool IsQuestTurnedIn(string questId)
    {
        return playerQuests.TryGetValue(questId, out PlayerQuest playerQuest) && playerQuest.status == QuestStatus.TurnedIn;
    }

    public QuestData GetQuestData(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;
        if (questDatabase.TryGetValue(questId, out QuestData questData)) return questData;
        return null;
    }

    public PlayerQuest GetPlayerQuest(string questId)
    {
        if (playerQuests.TryGetValue(questId, out PlayerQuest playerQuest)) return playerQuest;
        return null;
    }

    public List<PlayerQuest> GetActiveQuests()
    {
        return playerQuests.Values.Where(q => !string.IsNullOrEmpty(q.questId) && (q.status == QuestStatus.InProgress || q.status == QuestStatus.Completed)).ToList();
    }

    public List<PlayerQuest> GetCompletedQuests()
    {
        return playerQuests.Values.Where(q => q.status == QuestStatus.Completed).ToList();
    }

    public void ForceCompleteQuestChain(string targetQuestId)
    {
        if (!questDatabase.TryGetValue(targetQuestId, out QuestData targetQuestData)) return;
        if (!string.IsNullOrEmpty(targetQuestData.previousQuestId))
        {
            CompleteQuestChainRecursive(targetQuestData.previousQuestId);

            // REPLACED: SavePlayerQuests() -> Async save of all dirty quests
            // Note: StartQuest() below will also trigger a save, so we don't need immediate save here
            // The recursive completion will be picked up by the save queue
        }
        StartQuest(targetQuestId); // This will trigger save via QueueSave in StartQuest()
    }

    private void CompleteQuestChainRecursive(string questId)
    {
        if (!questDatabase.TryGetValue(questId, out QuestData questData)) return;
        if (!string.IsNullOrEmpty(questData.previousQuestId)) CompleteQuestChainRecursive(questData.previousQuestId);
        if (!IsQuestTurnedIn(questId))
        {
            if (!playerQuests.ContainsKey(questId))
            {
                PlayerQuest playerQuest = new PlayerQuest(questData);
                playerQuests[questId] = playerQuest;
            }
            if (playerQuests.TryGetValue(questId, out PlayerQuest quest))
            {
                foreach (var objective in quest.objectives) objective.currentAmount = objective.requiredAmount;
                if (quest.hiddenObjective != null)
                {
                    quest.isHiddenObjectiveActive = true;
                    quest.hiddenObjective.currentAmount = quest.hiddenObjective.requiredAmount;
                }
                quest.status = QuestStatus.TurnedIn;

                // ADDED: Save quest after force completion
                if (questSaveQueue != null)
                    questSaveQueue.QueueSave(questId, isCritical: true);
            }
        }
    }

    private void RefreshNetworkManagerConnection()
    {
        if (NetworkManager.Instance != null) NetworkManager.Instance.OnNetworkReady -= OnNetworkReady;
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnNetworkReady += OnNetworkReady;
            if (NetworkManager.Instance.Runner != null && NetworkManager.Instance.Runner.IsRunning) OnNetworkReady();
        }
        else
        {
            NetworkManager networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager != null)
            {
                NetworkManager.Instance = networkManager;
                NetworkManager.Instance.OnNetworkReady += OnNetworkReady;
            }
        }
    }

    private IEnumerator BackgroundInitialization()
    {
        yield return new WaitForSeconds(1f);
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnNetworkReady += OnNetworkReady;
            if (NetworkManager.Instance.Runner != null && NetworkManager.Instance.Runner.IsRunning) OnNetworkReady();
        }
    }

    private IEnumerator ListenToEquipmentEvents()
    {
        while (true)
        {
            foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
            {
                NetworkObject netObj = player.GetComponent<NetworkObject>();
                if (netObj != null && netObj.HasInputAuthority)
                {
                    EquipmentSystem equipmentSystem = player.GetComponent<EquipmentSystem>();
                    if (equipmentSystem != null && currentListeningEquipment != equipmentSystem)
                    {
                        CleanupEquipmentEvents();
                        currentListeningEquipment = equipmentSystem;
                        equipmentSystem.OnItemEquipped += OnPlayerItemEquipped;
                        equipmentSystem.OnItemUnequipped += OnPlayerItemUnequipped;
                        yield break;
                    }
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void CleanupEquipmentEvents()
    {
        if (currentListeningEquipment != null)
        {
            currentListeningEquipment.OnItemEquipped -= OnPlayerItemEquipped;
            currentListeningEquipment.OnItemUnequipped -= OnPlayerItemUnequipped;
            currentListeningEquipment = null;
        }
    }

    private void OnPlayerItemEquipped(ItemData item, EquipmentSlotType slotType, int slotIndex)
    {
        CheckEquipItemsProgress();
    }

    private void OnPlayerItemUnequipped(ItemData item, EquipmentSlotType slotType, int slotIndex)
    {
        CheckEquipItemsProgress();
    }

    private void CheckEquipItemsProgress()
    {
        if (playerQuests == null) return;
        bool anyUpdated = false;
        HashSet<string> updatedQuestIds = new HashSet<string>(); // Track which quests were updated
        foreach (var questPair in playerQuests)
        {
            string questId = questPair.Key;
            PlayerQuest playerQuest = questPair.Value;
            if (playerQuest.status != QuestStatus.InProgress) continue;
            foreach (var objective in playerQuest.objectives)
            {
                if (objective.type == QuestType.EquipItems && !objective.IsCompleted)
                {
                    int equippedCount = GetTotalEquippedItemsCount();
                    int newProgress = Mathf.Min(equippedCount, objective.requiredAmount);
                    if (newProgress != objective.currentAmount)
                    {
                        objective.currentAmount = newProgress;
                        anyUpdated = true;
                        updatedQuestIds.Add(questId);
                        OnQuestUpdated?.Invoke(questId);
                    }
                }
                if (objective.type == QuestType.EquipUpgradedItems && !objective.IsCompleted)
                {
                    int upgradedCount = GetEquippedItemsWithMinUpgradeLevel(objective.minUpgradeLevel);
                    int newProgress = Mathf.Min(upgradedCount, objective.requiredAmount);
                    if (newProgress != objective.currentAmount)
                    {
                        objective.currentAmount = newProgress;
                        anyUpdated = true;
                        updatedQuestIds.Add(questId);
                        OnQuestUpdated?.Invoke(questId);
                    }
                }
            }
            if (playerQuest.isHiddenObjectiveActive && playerQuest.hiddenObjective != null && !playerQuest.hiddenObjective.IsCompleted)
            {
                if (playerQuest.hiddenObjective.type == QuestType.EquipItems)
                {
                    int equippedCount = GetTotalEquippedItemsCount();
                    int newProgress = Mathf.Min(equippedCount, playerQuest.hiddenObjective.requiredAmount);
                    if (newProgress != playerQuest.hiddenObjective.currentAmount)
                    {
                        playerQuest.hiddenObjective.currentAmount = newProgress;
                        anyUpdated = true;
                        updatedQuestIds.Add(questId);
                        OnQuestUpdated?.Invoke(questId);
                    }
                }
                if (playerQuest.hiddenObjective.type == QuestType.EquipUpgradedItems)
                {
                    int upgradedCount = GetEquippedItemsWithMinUpgradeLevel(playerQuest.hiddenObjective.minUpgradeLevel);
                    int newProgress = Mathf.Min(upgradedCount, playerQuest.hiddenObjective.requiredAmount);
                    if (newProgress != playerQuest.hiddenObjective.currentAmount)
                    {
                        playerQuest.hiddenObjective.currentAmount = newProgress;
                        anyUpdated = true;
                        updatedQuestIds.Add(questId);
                        OnQuestUpdated?.Invoke(questId);
                    }
                }
            }
        }
        if (anyUpdated)
        {
            CheckQuestCompletion();

            // REPLACED: SavePlayerQuests() -> QueueSave for each updated quest (non-critical: batched)
            if (questSaveQueue != null)
            {
                foreach (string questId in updatedQuestIds)
                {
                    questSaveQueue.QueueSave(questId, isCritical: false); // Batched every 3s
                }
            }
        }
    }

    private int GetTotalEquippedItemsCount()
    {
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
            {
                EquipmentSystem equipmentSystem = player.GetComponent<EquipmentSystem>();
                if (equipmentSystem != null)
                {
                    int count = 0;
                    var allEquipped = equipmentSystem.GetAllEquippedItems();
                    foreach (var slotItems in allEquipped.Values) count += slotItems.Count;
                    return count;
                }
            }
        }
        return 0;
    }

    private void CheckEquipmentForQuests(string questId)
    {
        if (!playerQuests.TryGetValue(questId, out PlayerQuest playerQuest) || playerQuest.status != QuestStatus.InProgress) return;
        int equippedCount = GetTotalEquippedItemsCount();
        bool progressUpdated = false;
        foreach (var objective in playerQuest.objectives)
        {
            if (objective.type == QuestType.EquipItems && !objective.IsCompleted)
            {
                int newProgress = Mathf.Min(equippedCount, objective.requiredAmount);
                if (newProgress > objective.currentAmount)
                {
                    objective.currentAmount = newProgress;
                    progressUpdated = true;
                }
            }
            if (objective.type == QuestType.EquipUpgradedItems && !objective.IsCompleted)
            {
                int upgradedCount = GetEquippedItemsWithMinUpgradeLevel(objective.minUpgradeLevel);
                int newProgress = Mathf.Min(upgradedCount, objective.requiredAmount);
                if (newProgress > objective.currentAmount)
                {
                    objective.currentAmount = newProgress;
                    progressUpdated = true;
                }
            }
        }
        if (playerQuest.isHiddenObjectiveActive && playerQuest.hiddenObjective != null && !playerQuest.hiddenObjective.IsCompleted)
        {
            if (playerQuest.hiddenObjective.type == QuestType.EquipItems)
            {
                int newProgress = Mathf.Min(equippedCount, playerQuest.hiddenObjective.requiredAmount);
                if (newProgress > playerQuest.hiddenObjective.currentAmount)
                {
                    playerQuest.hiddenObjective.currentAmount = newProgress;
                    progressUpdated = true;
                }
            }
            if (playerQuest.hiddenObjective.type == QuestType.EquipUpgradedItems)
            {
                int upgradedCount = GetEquippedItemsWithMinUpgradeLevel(playerQuest.hiddenObjective.minUpgradeLevel);
                int newProgress = Mathf.Min(upgradedCount, playerQuest.hiddenObjective.requiredAmount);
                if (newProgress > playerQuest.hiddenObjective.currentAmount)
                {
                    playerQuest.hiddenObjective.currentAmount = newProgress;
                    progressUpdated = true;
                }
            }
        }
        if (progressUpdated)
        {
            OnQuestUpdated?.Invoke(questId);
            CheckQuestCompletion();
        }
    }

    private int GetEquippedItemsWithMinUpgradeLevel(int minUpgradeLevel)
    {
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
            {
                EquipmentSystem equipmentSystem = player.GetComponent<EquipmentSystem>();
                if (equipmentSystem != null)
                {
                    int count = 0;
                    var allEquipped = equipmentSystem.GetAllEquippedItems();
                    foreach (var slotItems in allEquipped.Values)
                    {
                        foreach (var item in slotItems)
                        {
                            if (item != null && item.upgradeLevel >= minUpgradeLevel) count++;
                        }
                    }
                    return count;
                }
            }
        }
        return 0;
    }
}
