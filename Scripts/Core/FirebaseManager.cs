using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Auth;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance;
    
    [Header("Firebase Settings")]
    [SerializeField] private string databaseURL = "https://diablo-knight-default-rtdb.europe-west1.firebasedatabase.app/";
    
    private FirebaseAuth auth;
    private string deviceId;
    private DatabaseReference dbReference;
    private FirebaseUser currentUser;
    private bool isInitialized = false;
    private Dictionary<string, UserData> userCache = new Dictionary<string, UserData>();
    private FirebaseDatabase database;
    private float lastCacheCleanup = 0f;
    private const float CACHE_CLEANUP_INTERVAL = 600f; // 10 dakika
    private const float CACHE_ENTRY_LIFETIME = 3600f; // 1 saat

    public bool IsReady => isInitialized;

    private class UserData
    {
        public string UserId { get; set; }
        public string ActiveNickname { get; set; }
        public Dictionary<string, object> Stats { get; set; }
        public long LastLogin { get; set; }
        public float CacheTime { get; set; } // Cache'e eklenme zamanı
    }

    #region INITIALIZATION

    private void Awake()
    {
        if (IsServerMode())
        {
            gameObject.SetActive(false);
            return;
        }

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            deviceId = SystemInfo.deviceUniqueIdentifier;
            InitializeFirebase().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    isInitialized = false;
                }
            });
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async Task InitializeFirebase()
    {
        try
        {
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
            {
                isInitialized = false;
                return;
            }

            if (string.IsNullOrEmpty(databaseURL))
            {
                isInitialized = false;
                return;
            }

            FirebaseApp app = FirebaseApp.DefaultInstance;
            if (app == null)
            {
                var options = new AppOptions { DatabaseUrl = new Uri(databaseURL) };
                app = FirebaseApp.Create(options);
            }

            database = FirebaseDatabase.GetInstance(app, databaseURL);
            dbReference = database.RootReference;
            auth = FirebaseAuth.DefaultInstance;

            if (dbReference != null && auth != null)
            {
                isInitialized = true;
            }
            else
            {
                isInitialized = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Init error: {e.Message}");
            isInitialized = false;
        }
    }

    private bool IsServerMode()
    {
        if (Application.isEditor) return false;
        string[] args = System.Environment.GetCommandLineArgs();
        return System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode");
    }

    #endregion

    #region DATABASE ACCESS

    public FirebaseDatabase GetDatabase()
    {
        if (database == null && FirebaseApp.DefaultInstance != null)
        {
            database = FirebaseDatabase.GetInstance(FirebaseApp.DefaultInstance, databaseURL);
        }
        return database;
    }

    public DatabaseReference GetDatabaseReference() => dbReference;

    public bool IsConnected() => isInitialized && database != null && auth != null && dbReference != null;

    #endregion

    #region CHARACTER OPERATIONS

    public async Task SaveCharacterData(string nickname, string characterJson)
    {
        if (!isInitialized || database == null) return;

        try
        {
            var characterData = new Dictionary<string, object>
            {
                { "characterData", characterJson },
                { "lastUpdated", ServerValue.Timestamp }
            };

            await database.RootReference
                .Child("characters")
                .Child(nickname)
                .UpdateChildrenAsync(characterData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Character save error: {e.Message}");
            throw;
        }
    }

    public async Task<string> LoadCharacterData(string nickname)
    {
        if (!isInitialized || database == null) return null;

        try
        {
            var snapshot = await database.RootReference
                .Child("characters")
                .Child(nickname)
                .Child("characterData")
                .GetValueAsync();

            return snapshot.Exists ? snapshot.Value.ToString() : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Character load error: {e.Message}");
            return null;
        }
    }

    public async Task<bool> HasCharacter(string nickname)
    {
        if (!isInitialized || database == null) return false;

        try
        {
            var snapshot = await database.RootReference
                .Child("characters")
                .Child(nickname)
                .Child("characterData")
                .GetValueAsync();

            return snapshot.Exists;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Character check error: {e.Message}");
            return false;
        }
    }

    public async Task SaveCharacterDataByRace(string nickname, PlayerRace race, string characterJson)
    {
        if (!isInitialized || database == null) return;

        try
        {
            var characterData = new Dictionary<string, object>
            {
                { $"characterData_{race}", characterJson },
                { "lastUpdated", ServerValue.Timestamp }
            };

            await database.RootReference
                .Child("characters")
                .Child(nickname)
                .UpdateChildrenAsync(characterData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Character save error: {e.Message}");
            throw;
        }
    }

    public async Task<string> LoadCharacterDataByRace(string nickname, PlayerRace race)
    {
        if (!isInitialized || database == null) return null;

        try
        {
            var snapshot = await database.RootReference
                .Child("characters")
                .Child(nickname)
                .Child($"characterData_{race}")
                .GetValueAsync();

            return snapshot.Exists ? snapshot.Value.ToString() : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Character load error: {e.Message}");
            return null;
        }
    }

    #endregion

    #region CLASS OPERATIONS

    public async Task SavePlayerClass(string nickname, ClassType classType)
    {
        if (!isInitialized || database == null) return;

        try
        {
            var classData = new Dictionary<string, object>
            {
                { "classType", classType.ToString() },
                { "lastUpdated", ServerValue.Timestamp }
            };

            await database.RootReference
                .Child("characters")
                .Child(nickname)
                .UpdateChildrenAsync(classData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Class save error: {e.Message}");
            throw;
        }
    }

    public async Task<ClassType?> LoadPlayerClass(string nickname)
    {
        if (!isInitialized || database == null) return null;

        try
        {
            var snapshot = await database.RootReference
                .Child("characters")
                .Child(nickname)
                .Child("classType")
                .GetValueAsync();

            if (snapshot.Exists && System.Enum.TryParse<ClassType>(snapshot.Value.ToString(), out ClassType classType))
            {
                return classType;
            }
            
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Class load error: {e.Message}");
            return null;
        }
    }

    #endregion

    #region RACE OPERATIONS

    public async Task SavePlayerRace(string nickname, PlayerRace race)
    {
        if (!isInitialized || database == null) return;

        try
        {
            var raceData = new Dictionary<string, object>
            {
                { "raceType", race.ToString() },
                { "lastUpdated", ServerValue.Timestamp }
            };

            await database.RootReference
                .Child("characters")
                .Child(nickname)
                .UpdateChildrenAsync(raceData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Race save error: {e.Message}");
            throw;
        }
    }

    public async Task<PlayerRace?> LoadPlayerRace(string nickname)
    {
        if (!isInitialized || database == null) return null;

        try
        {
            var snapshot = await database.RootReference
                .Child("characters")
                .Child(nickname)
                .Child("raceType")
                .GetValueAsync();

            if (snapshot.Exists && System.Enum.TryParse<PlayerRace>(snapshot.Value.ToString(), out PlayerRace race))
            {
                return race;
            }
            
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Race load error: {e.Message}");
            return null;
        }
    }

    #endregion

    #region USER OPERATIONS

    public async Task<bool> CreateUserAccount(string nickname)
    {
        if (!isInitialized) return false;

        try
        {
            string accountId = await AccountManager.Instance.GetOrCreateAccount();

            var snapshot = await dbReference.Child("nicknames").Child(nickname).GetValueAsync();
            if (snapshot.Exists) return false;
            
            var nicknameData = new Dictionary<string, object>
            {
                { "userId", accountId },
                { "lastLogin", ServerValue.Timestamp }
            };
            
            await dbReference.Child("nicknames").Child(nickname).SetValueAsync(nicknameData);
            
            var characterData = new Dictionary<string, object>
            {
                { "stats", new Dictionary<string, object>
                    {
                        { "level", 1 },
                        { "xp", 0f },
                        { "maxHP", 100f },
                        { "currentHP", 100f },
                        { "createdAt", ServerValue.Timestamp },
                        { "lastUpdated", ServerValue.Timestamp }
                    }
                }
            };
            
            await dbReference.Child("characters").Child(nickname).SetValueAsync(characterData);
            
            var playerData = new Dictionary<string, object>
            {
                { "quests", new Dictionary<string, object>() },
                { "createdAt", ServerValue.Timestamp }
            };
            
            await dbReference.Child("players").Child(nickname).SetValueAsync(playerData);
            
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Account creation error: {e.Message}");
            return false;
        }
    }

    public async Task<Dictionary<string, object>> LoadUserData(string nickname)
    {
        if (!isInitialized || database == null || dbReference == null || string.IsNullOrEmpty(nickname))
        {
            return null;
        }

        try
        {
            var characterSnapshot = await dbReference.Child("characters").Child(nickname).GetValueAsync();
            if (!characterSnapshot.Exists) return null;

            var stats = new Dictionary<string, object>();

            var raceSnapshot = characterSnapshot.Child("raceType");
            if (raceSnapshot.Exists) stats["raceType"] = raceSnapshot.Value;

            var statsSnapshot = characterSnapshot.Child("stats");
            if (statsSnapshot.Exists)
            {
                foreach (var child in statsSnapshot.Children)
                {
                    if (child.Key == "currentHP") continue;
                    stats[child.Key] = child.Value;
                }
            }

            var equipmentSnapshot = characterSnapshot.Child("equipment");
            if (equipmentSnapshot.Exists)
            {
                var equipmentData = new Dictionary<string, object>();
                foreach (var slot in equipmentSnapshot.Children)
                {
                    if (slot.Value != null) equipmentData[slot.Key] = slot.Value;
                }
                stats["equipment"] = equipmentData;
            }

            var inventorySnapshot = characterSnapshot.Child("inventory");
            if (inventorySnapshot.Exists)
            {
                stats["inventory"] = ParseInventoryData(inventorySnapshot);
            }

            var craftInventorySnapshot = characterSnapshot.Child("craftInventory");
            if (craftInventorySnapshot.Exists)
            {
                stats["craftInventory"] = ParseCraftInventoryData(craftInventorySnapshot);
            }

            return stats;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] User data load error: {e.Message}");
            return null;
        }
    }

    public async Task SaveUserData(PlayerStats stats, Dictionary<string, object> completeData = null, string characterJson = null)
    {
        if (!isInitialized) return;

        const int maxRetries = 3;
        int retryCount = 0;
        
        while (retryCount < maxRetries)
        {
            try
            {
                string nickname = stats.GetPlayerDisplayName();
                if (string.IsNullOrEmpty(nickname)) return;

                Dictionary<string, object> characterData = completeData ?? BuildCharacterData(stats);

                if (!string.IsNullOrEmpty(characterJson))
                {
                    characterData["characterData"] = characterJson;
                }

                await dbReference.Child("characters").Child(nickname).UpdateChildrenAsync(characterData);
                return;
            }
            catch (Exception e)
            {
                retryCount++;
                
                if (retryCount >= maxRetries)
                {
                    Debug.LogError($"[FirebaseManager] Save failed: {e.Message}");
                    throw;
                }
                
                await Task.Delay(1000 * retryCount);
            }
        }
    }

    public async Task SaveDeviceToken(string accountId, string deviceToken)
    {
        await dbReference.Child("accounts").Child(accountId).Child("devices")
            .Child(deviceToken).SetValueAsync(ServerValue.Timestamp);
    }

    #endregion

    #region GENERIC DATA OPERATIONS

    public async Task<bool> SaveDataToPath(string path, object data)
    {
        if (!isInitialized || database == null || string.IsNullOrEmpty(path)) return false;

        try
        {
            await database.RootReference.Child(path).SetValueAsync(data);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Data save error: {e.Message}");
            return false;
        }
    }

    public async Task<DataSnapshot> LoadDataFromPath(string path)
    {
        if (!isInitialized || database == null) return null;

        try
        {
            return await database.RootReference.Child(path).GetValueAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Data load error: {e.Message}");
            return null;
        }
    }

    #endregion

    #region HELPER METHODS

    private Dictionary<string, object> ParseInventoryData(DataSnapshot inventorySnapshot)
    {
        var inventoryData = new Dictionary<string, object>();
        
        foreach (var slot in inventorySnapshot.Children)
        {
            if (slot.Value == null) continue;

            try
            {
                var slotData = new Dictionary<string, object>
                {
                    { "itemId", slot.Child("itemId").Value },
                    { "amount", slot.Child("amount").Value }
                };

                if (slot.Child("upgradeLevel").Exists) slotData["upgradeLevel"] = slot.Child("upgradeLevel").Value;
                if (slot.Child("rarity").Exists) slotData["rarity"] = slot.Child("rarity").Value;
                if (slot.Child("armorValue").Exists) slotData["armorValue"] = slot.Child("armorValue").Value;
                if (slot.Child("attackPower").Exists) slotData["attackPower"] = slot.Child("attackPower").Value;

                var selectedStatsSnapshot = slot.Child("selectedStats");
                if (selectedStatsSnapshot.Exists && selectedStatsSnapshot.HasChildren)
                {
                    var selectedStats = new List<Dictionary<string, object>>();
                    foreach (var statSnapshot in selectedStatsSnapshot.Children)
                    {
                        selectedStats.Add(new Dictionary<string, object>
                        {
                            { "type", statSnapshot.Child("type").Value },
                            { "value", statSnapshot.Child("value").Value }
                        });
                    }
                    slotData["selectedStats"] = selectedStats;
                }

                string[] slotParts = slot.Key.Replace("slot_", "").Split('_');
                if (slotParts.Length == 2)
                {
                    slotData["position"] = new Dictionary<string, int>
                    {
                        { "x", int.Parse(slotParts[0]) },
                        { "y", int.Parse(slotParts[1]) }
                    };
                    inventoryData[slot.Key] = slotData;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FirebaseManager] Inventory slot parse error: {e.Message}");
            }
        }
        
        return inventoryData;
    }

    private Dictionary<string, object> ParseCraftInventoryData(DataSnapshot craftInventorySnapshot)
    {
        var craftInventoryData = new Dictionary<string, object>();
        
        foreach (var slot in craftInventorySnapshot.Children)
        {
            if (slot.Value == null) continue;

            try
            {
                var slotData = new Dictionary<string, object>
                {
                    { "itemId", slot.Child("itemId").Value },
                    { "amount", slot.Child("amount").Value }
                };

                if (slot.Child("upgradeLevel").Exists) slotData["upgradeLevel"] = slot.Child("upgradeLevel").Value;
                if (slot.Child("rarity").Exists) slotData["rarity"] = slot.Child("rarity").Value;

                var selectedStatsSnapshot = slot.Child("selectedStats");
                if (selectedStatsSnapshot.Exists && selectedStatsSnapshot.HasChildren)
                {
                    var selectedStats = new List<Dictionary<string, object>>();
                    foreach (var statSnapshot in selectedStatsSnapshot.Children)
                    {
                        selectedStats.Add(new Dictionary<string, object>
                        {
                            { "type", statSnapshot.Child("type").Value },
                            { "value", statSnapshot.Child("value").Value }
                        });
                    }
                    slotData["selectedStats"] = selectedStats;
                }

                string[] slotParts = slot.Key.Replace("craft_slot_", "").Split('_');
                if (slotParts.Length == 2)
                {
                    slotData["position"] = new Dictionary<string, int>
                    {
                        { "x", int.Parse(slotParts[0]) },
                        { "y", int.Parse(slotParts[1]) }
                    };
                    craftInventoryData[slot.Key] = slotData;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FirebaseManager] Craft slot parse error: {e.Message}");
            }
        }
        
        return craftInventoryData;
    }

    private Dictionary<string, object> BuildCharacterData(PlayerStats stats)
    {
        var equipSystem = stats.GetComponent<EquipmentSystem>();
        var invSystem = stats.GetComponent<InventorySystem>();
        var craftSystem = stats.GetComponent<CraftInventorySystem>();

        return new Dictionary<string, object>
        {
            { "stats", new Dictionary<string, object>
                {
                    { "level", stats.CurrentLevel },
                    { "xp", stats.CurrentXP },
                    { "maxHP", stats.MaxHP },
                    { "lastUpdated", ServerValue.Timestamp },
                    { "moveSpeed", stats.MoveSpeed },
                    { "criticalChance", stats.BaseCriticalChance },
                    { "armor", stats.BaseArmor },
                    { "attackPower", stats.BaseDamage },
                    { "coins", stats.Coins },
                    { "potionCount", stats.PotionCount },
                    { "potionLevel", stats.PotionLevel },
                    { "hasReceivedDefaultEquipment", stats.hasReceivedDefaultEquipment }
                }
            },
            { "equipment", equipSystem?.GetEquipmentData() ?? new Dictionary<string, object>() },
            { "inventory", invSystem?.GetInventoryData() ?? new Dictionary<string, object>() },
            { "craftInventory", craftSystem?.GetCraftInventoryData() ?? new Dictionary<string, object>() }
        };
    }

    #endregion

    #region CACHE MANAGEMENT

    private void Update()
    {
        if (!isInitialized) return;

        // Periyodik cache temizliği
        if (Time.time - lastCacheCleanup > CACHE_CLEANUP_INTERVAL)
        {
            CleanupCache();
            lastCacheCleanup = Time.time;
        }
    }

    private void CleanupCache()
    {
        try
        {
            float currentTime = Time.time;
            var keysToRemove = new System.Collections.Generic.List<string>();

            // Eski cache entry'leri bul
            foreach (var kvp in userCache)
            {
                if (currentTime - kvp.Value.CacheTime > CACHE_ENTRY_LIFETIME)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            // Temizle
            foreach (var key in keysToRemove)
            {
                userCache.Remove(key);
            }

            // Eğer cache hala çok büyükse (100+ entry), en eskileri temizle
            if (userCache.Count > 100)
            {
                var oldestEntries = userCache
                    .OrderBy(kvp => kvp.Value.CacheTime)
                    .Take(userCache.Count - 100)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in oldestEntries)
                {
                    userCache.Remove(key);
                }

            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseManager] Cache cleanup error: {e.Message}");
        }
    }

    #endregion
}