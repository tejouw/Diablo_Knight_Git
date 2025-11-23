using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;

#region Data Structures
[System.Serializable]
public class GatheringSpawnPoint
{
    public string pointName = "Gathering Point";
    public Vector2 position;
    public float spawnRadius = 2f;
    public GameObject gatherablePrefab;
    public int maxGatherablesInPoint = 3;
    public float respawnTime = 60f; // Toplanan obje kaç saniye sonra respawn olsun

    [System.NonSerialized] public float lastSpawnTime = 0f;
    [System.NonSerialized] public bool isInitialSpawnComplete = false;

    public bool IsPositionInRange(Vector2 targetPosition)
    {
        return Vector2.Distance(position, targetPosition) <= spawnRadius;
    }
}

[System.Serializable]
public class GatherableRegistryEntry
{
    public NetworkId gatherableId;
    public string pointName;
    public Vector2 position;
    public float spawnTime;
    public bool isBeingGathered;

    public GatherableRegistryEntry(NetworkId id, string point, Vector2 pos)
    {
        gatherableId = id;
        pointName = point;
        position = pos;
        spawnTime = Time.time;
        isBeingGathered = false;
    }
}
#endregion

public class GatheringSpawner : NetworkBehaviour
{
    #region Network Properties
    [Networked] public bool IsInitialized { get; set; }
    [Networked] public int TotalGatherablesSpawned { get; set; }
    [Networked] public TickTimer SpawnCheckTimer { get; set; }
    #endregion

    #region Gatherable Registry
    private Dictionary<NetworkId, GatherableRegistryEntry> gatherableRegistry = new Dictionary<NetworkId, GatherableRegistryEntry>();
    private Dictionary<string, List<NetworkId>> gatherablesByPoint = new Dictionary<string, List<NetworkId>>();

    public void RegisterGatherable(NetworkId gatherableId, string pointName, Vector2 position)
    {
        if (!Runner.IsServer) return;

        var entry = new GatherableRegistryEntry(gatherableId, pointName, position);
        gatherableRegistry[gatherableId] = entry;

        if (!gatherablesByPoint.ContainsKey(pointName))
            gatherablesByPoint[pointName] = new List<NetworkId>();

        gatherablesByPoint[pointName].Add(gatherableId);
    }

    public void UnregisterGatherable(NetworkId gatherableId)
    {
        if (!Runner.IsServer) return;

        if (gatherableRegistry.TryGetValue(gatherableId, out var entry))
        {
            if (gatherablesByPoint.ContainsKey(entry.pointName))
            {
                gatherablesByPoint[entry.pointName].Remove(gatherableId);
                if (gatherablesByPoint[entry.pointName].Count == 0)
                    gatherablesByPoint.Remove(entry.pointName);
            }
            gatherableRegistry.Remove(gatherableId);
        }
    }

    private int GetGatherableCountInPoint(string pointName)
    {
        if (!Runner.IsServer) return 0;
        if (!gatherablesByPoint.ContainsKey(pointName))
            return 0;
        return gatherablesByPoint[pointName].Count;
    }

    public void MarkAsGathered(NetworkId gatherableId, string pointName)
    {
        if (!Runner.IsServer) return;

        if (gatherableRegistry.ContainsKey(gatherableId))
        {
            gatherableRegistry[gatherableId].isBeingGathered = true;
        }

        // Respawn için spawn point'i güncelle
        var spawnPoint = gatheringSpawnPoints.FirstOrDefault(sp => sp.pointName == pointName);
        if (spawnPoint != null)
        {
            spawnPoint.lastSpawnTime = (float)Runner.SimulationTime;
        }
    }
    #endregion

    #region Configuration
    [SerializeField] private List<GatheringSpawnPoint> gatheringSpawnPoints = new List<GatheringSpawnPoint>();
    [SerializeField] private float playerSafeDistance = 3f;
    [SerializeField] private float gatherableSpacingDistance = 1.5f;
    private Transform serverHierarchyContainer;
    #endregion

    #region Fusion Lifecycle
    public override void Spawned()
    {
        if (Runner.IsServer)
            InitializeServerSpawner();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer || !IsInitialized) return;

        if (SpawnCheckTimer.ExpiredOrNotRunning(Runner))
        {
            SpawnCheckTimer = TickTimer.CreateFromSeconds(Runner, 1f); // Her saniye kontrol
            PerformServerSpawnCheck();
        }
    }
    #endregion

    #region Initialization
    private void InitializeServerSpawner()
    {
        if (!Runner.IsServer) return;

        InitializeServerHierarchy();
        IsInitialized = true;
        SpawnCheckTimer = TickTimer.CreateFromSeconds(Runner, 0f);
    }

    private void InitializeServerHierarchy()
    {
        if (!Runner.IsServer) return;

        GameObject existingContainer = GameObject.Find("Gatherables");
        if (existingContainer != null)
            serverHierarchyContainer = existingContainer.transform;
        else
        {
            GameObject newContainer = new GameObject("Gatherables");
            serverHierarchyContainer = newContainer.transform;
        }
    }
    #endregion

    #region Main Spawn Logic
    private void PerformServerSpawnCheck()
    {
        if (!Runner.IsServer) return;

        foreach (var spawnPoint in gatheringSpawnPoints)
        {
            if (spawnPoint == null || spawnPoint.gatherablePrefab == null)
                continue;

            ProcessSpawnPoint(spawnPoint);
        }
    }

    private void ProcessSpawnPoint(GatheringSpawnPoint spawnPoint)
    {
        if (!Runner.IsServer) return;

        int currentCount = GetGatherableCountInPoint(spawnPoint.pointName);
        int maxCount = spawnPoint.maxGatherablesInPoint;

        if (currentCount >= maxCount)
        {
            spawnPoint.isInitialSpawnComplete = true;
            return;
        }

        // Initial spawn ise hemen spawn et
        if (!spawnPoint.isInitialSpawnComplete)
        {
            SpawnGatherable(spawnPoint);
            return;
        }

        // Respawn kontrolü
        float timeSinceLastSpawn = (float)Runner.SimulationTime - spawnPoint.lastSpawnTime;
        if (timeSinceLastSpawn >= spawnPoint.respawnTime)
        {
            SpawnGatherable(spawnPoint);
        }
    }

    private bool SpawnGatherable(GatheringSpawnPoint spawnPoint)
    {
        if (!Runner.IsServer) return false;

        Vector2 spawnPosition = FindValidSpawnPosition(spawnPoint);
        if (spawnPosition == Vector2.zero)
            return false;

        try
        {
            NetworkObject prefabNetObj = spawnPoint.gatherablePrefab.GetComponent<NetworkObject>();
            if (prefabNetObj == null)
                return false;

            Vector3 spawnPos3D = new Vector3(spawnPosition.x, spawnPosition.y, 0f);
            NetworkObject gatherable = Runner.Spawn(prefabNetObj, spawnPos3D, Quaternion.identity);

            if (gatherable != null)
            {
                // GatherableObject component'ini ayarla (pozisyon parametresi ile)
                GatherableObject gatherableObj = gatherable.GetComponent<GatherableObject>();
                if (gatherableObj != null)
                {
                    gatherableObj.Initialize(this, spawnPoint.pointName, spawnPos3D);
                }


                // Hierarchy'de organize et
                if (serverHierarchyContainer != null)
                {
                    Transform pointContainer = GetOrCreatePointContainer(spawnPoint.pointName);
                    gatherable.transform.SetParent(pointContainer, true); // worldPositionStays = true
                }

                RegisterGatherable(gatherable.Id, spawnPoint.pointName, spawnPosition);
                spawnPoint.lastSpawnTime = (float)Runner.SimulationTime;
                spawnPoint.isInitialSpawnComplete = true;
                TotalGatherablesSpawned++;

                return true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GatheringSpawner] Spawn error: {e.Message}");
        }

        return false;
    }

    private Transform GetOrCreatePointContainer(string pointName)
    {
        if (!Runner.IsServer) return null;

        foreach (Transform child in serverHierarchyContainer)
        {
            if (child.name == pointName)
                return child;
        }

        GameObject pointObj = new GameObject(pointName);
        pointObj.transform.SetParent(serverHierarchyContainer);
        return pointObj.transform;
    }
    #endregion

    #region Position Validation
    private Vector2 FindValidSpawnPosition(GatheringSpawnPoint spawnPoint)
    {
        const int maxAttempts = 10;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 candidatePosition = GetRandomPositionInCircle(spawnPoint.position, spawnPoint.spawnRadius);
            if (IsPositionValidForSpawn(candidatePosition))
                return candidatePosition;
        }
        return Vector2.zero;
    }

    private Vector2 GetRandomPositionInCircle(Vector2 center, float radius)
    {
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float randomRadius = Random.Range(0.2f, 0.9f) * radius;
        return center + new Vector2(Mathf.Cos(randomAngle) * randomRadius, Mathf.Sin(randomAngle) * randomRadius);
    }

    private bool IsPositionValidForSpawn(Vector2 position)
    {
        return IsPositionSafeFromPlayers(position) &&
               !IsPositionTooCloseToOtherGatherables(position);
    }

    private bool IsPositionSafeFromPlayers(Vector2 position)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        return players.All(player => player == null || Vector2.Distance(player.transform.position, position) >= playerSafeDistance);
    }

    private bool IsPositionTooCloseToOtherGatherables(Vector2 position)
    {
        Collider2D[] existingGatherables = Physics2D.OverlapCircleAll(position, gatherableSpacingDistance);
        foreach (var col in existingGatherables)
        {
            if (col.GetComponent<GatherableObject>() != null)
                return true;
        }
        return false;
    }
    #endregion

    #region Public API
    public int GetTotalGatherablesSpawned() { return TotalGatherablesSpawned; }
    public bool IsSpawnerInitialized() { return IsInitialized; }

    public string GetGatherablesInfo()
    {
        if (Runner.IsServer)
        {
            int totalActive = gatherableRegistry.Count;
            int totalMax = gatheringSpawnPoints.Sum(sp => sp.maxGatherablesInPoint);
            return $"Gatherables: {totalActive}/{totalMax}";
        }
        return "Client Mode";
    }
    #endregion

    #region Editor Utilities
    private void OnDrawGizmosSelected()
    {
        if (gatheringSpawnPoints == null) return;

        foreach (var spawnPoint in gatheringSpawnPoints)
        {
            if (spawnPoint == null) continue;

            // Spawn radius'u çiz
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(spawnPoint.position, spawnPoint.spawnRadius);

            // Spawn point center
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(spawnPoint.position, 0.2f);
        }
    }
    #endregion
}
