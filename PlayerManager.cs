using UnityEngine;
using Fusion;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    // Player registry - sadece server manage eder
    private Dictionary<PlayerRef, PlayerData> activePlayers = new Dictionary<PlayerRef, PlayerData>();
    private List<PlayerData> cachedPlayerList = new List<PlayerData>(); // Performance için cache
    private float lastCacheUpdate = 0f;
    private const float CACHE_UPDATE_INTERVAL = 0.1f; // 100ms'de bir güncelle

    // Thread-safety için lock object
    private readonly object _lock = new object();

    [System.Serializable]
    public class PlayerData
    {
        public PlayerRef playerRef;
        public Transform transform;
        public PlayerStats playerStats;
        public Vector2 lastKnownPosition;
        public float lastUpdateTime;

        public PlayerData(PlayerRef pRef, Transform t, PlayerStats stats)
        {
            playerRef = pRef;
            transform = t;
            playerStats = stats;
            lastKnownPosition = t.position;
            lastUpdateTime = Time.time;
        }
    }

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
        }
    }

    private void Start()
    {
        // Initial player scan - her durumda çalışmalı
        InvokeRepeating(nameof(ScanExistingPlayers), 0.5f, 2f); // 0.5 saniye sonra başla, 2 saniyede bir tekrarla
    }
    
    // Player register - spawn'da çağrılır
    public void RegisterPlayer(PlayerRef playerRef, Transform playerTransform, PlayerStats playerStats)
    {
        // Null validation
        if (playerTransform == null || playerStats == null || playerRef == PlayerRef.None)
        {
            Debug.LogError($"[PlayerManager] Cannot register player with null/invalid data: PlayerRef={playerRef}");
            return;
        }

        lock (_lock)
        {
            // Duplicate check - aynı player zaten kayıtlıysa güncelle
            if (activePlayers.ContainsKey(playerRef))
            {
                Debug.LogWarning($"[PlayerManager] Player {playerRef} already registered. Updating data.");
            }

            var playerData = new PlayerData(playerRef, playerTransform, playerStats);
            activePlayers[playerRef] = playerData;

            RefreshCache();
        }
    }

    // Player unregister - despawn'da çağrılır
    public void UnregisterPlayer(PlayerRef playerRef)
    {
        lock (_lock)
        {
            if (activePlayers.Remove(playerRef))
            {
                RefreshCache();
            }
        }
    }
    
    // Monster'lar için optimized player getter
    public List<PlayerData> GetPlayersNear(Vector2 monsterPosition, float maxDistance)
    {
        UpdateCacheIfNeeded();

        List<PlayerData> nearbyPlayers = new List<PlayerData>();
        float maxDistSqr = maxDistance * maxDistance;

        // Defensive copy: iteration sırasında cachedPlayerList değişirse crash olmaz
        List<PlayerData> safeCopy;
        lock (_lock)
        {
            safeCopy = new List<PlayerData>(cachedPlayerList);
        }

        foreach (var player in safeCopy)
        {
            if (player.transform == null || player.playerStats == null || player.playerStats.IsDead) continue;

            float distSqr = (player.transform.position - (Vector3)monsterPosition).sqrMagnitude;
            if (distSqr <= maxDistSqr)
            {
                nearbyPlayers.Add(player);
            }
        }

        return nearbyPlayers;
    }
    
    // Tüm aktif player'ları al (cached)
    public List<PlayerData> GetAllActivePlayers()
    {
        UpdateCacheIfNeeded();

        // Defensive copy: dış sistemler bu listeyi değiştirirse internal state bozulmaz
        lock (_lock)
        {
            return new List<PlayerData>(cachedPlayerList);
        }
    }
    
    private void UpdateCacheIfNeeded()
    {
        if (Time.time - lastCacheUpdate >= CACHE_UPDATE_INTERVAL)
        {
            lock (_lock)
            {
                // Double-check pattern: başka bir thread lock almadan önce update yapmış olabilir
                if (Time.time - lastCacheUpdate >= CACHE_UPDATE_INTERVAL)
                {
                    RefreshCache();
                }
            }
        }
    }
    
    private void RefreshCache()
    {
        // CRITICAL: Bu metod lock içinde çağrılmalı (RegisterPlayer, UnregisterPlayer, UpdateCacheIfNeeded'den)
        // Burada lock kullanmıyoruz çünkü caller zaten lock almış durumda

        cachedPlayerList.Clear();

        foreach (var kvp in activePlayers)
        {
            var playerData = kvp.Value;
            if (playerData.transform != null)
            {
                playerData.lastKnownPosition = playerData.transform.position;
                playerData.lastUpdateTime = Time.time;
                cachedPlayerList.Add(playerData);
            }
        }

        lastCacheUpdate = Time.time;
    }
    
    private void ScanExistingPlayers()
    {
        // Sadece başlangıçta mevcut player'ları tara
        NetworkObject[] allNetObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

        int playersFound = 0;
        foreach (var netObj in allNetObjects)
        {
            if (netObj != null && netObj.CompareTag("Player"))
            {
                PlayerStats stats = netObj.GetComponent<PlayerStats>();
                if (stats != null && netObj.InputAuthority != PlayerRef.None)
                {
                    // Duplicate check: zaten kayıtlıysa skip et
                    bool alreadyRegistered = false;
                    lock (_lock)
                    {
                        alreadyRegistered = activePlayers.ContainsKey(netObj.InputAuthority);
                    }

                    if (!alreadyRegistered)
                    {
                        RegisterPlayer(netObj.InputAuthority, netObj.transform, stats);
                        playersFound++;
                    }
                }
            }
        }
    }
    
    private void FixedUpdate()
    {
        // Passive cleanup - her frame
        // Her saniyede bir dead reference'ları temizle
        if (Time.fixedTime % 1f < 0.02f)
        {
            CleanupDeadReferences();
        }
    }
    
    private void CleanupDeadReferences()
    {
        List<PlayerRef> toRemove = new List<PlayerRef>();

        lock (_lock)
        {
            foreach (var kvp in activePlayers)
            {
                if (kvp.Value.transform == null)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            // Cleanup işlemi de lock içinde yapılmalı
            foreach (var playerRef in toRemove)
            {
                if (activePlayers.Remove(playerRef))
                {
                    // Removed successfully
                }
            }

            // Eğer silme yapıldıysa cache'i güncelle
            if (toRemove.Count > 0)
            {
                RefreshCache();
            }
        }
    }

    private void OnDestroy()
    {
        // MEMORY LEAK FIX: InvokeRepeating cleanup
        CancelInvoke(nameof(ScanExistingPlayers));
    }
}