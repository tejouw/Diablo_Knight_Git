using UnityEngine;
using Fusion;

[System.Serializable]
public class NPCSpawnData
{
    public string npcPrefabName;      // Resources klasöründeki prefab adı
    public Vector3 spawnPosition;     // Spawn pozisyonu
}

public class NPCSpawnerManager : NetworkBehaviour
{
    [Header("NPC Spawn List")]
    [SerializeField] private NPCSpawnData[] npcSpawnList;
    
    [Header("Settings")]
    [SerializeField] private bool deactivateAfterSpawn = true; // Spawn sonrası kendini deaktif et
    
    private bool hasSpawned = false;
    
    public override void Spawned()
    {
        if (!Runner.IsServer) return;
        if (hasSpawned) return;
        
        SpawnAllNPCs();
    }
    
    private void Start()
    {
        // Network olmadan da çalışabilmek için
        StartCoroutine(WaitForServerAndSpawn());
    }
    
    private System.Collections.IEnumerator WaitForServerAndSpawn()
    {
        // Server olana kadar bekle
        while (NetworkManager.Instance == null || 
               NetworkManager.Instance.Runner == null || 
               !NetworkManager.Instance.Runner.IsServer)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        // Bir tick bekle
        yield return new WaitForSeconds(0.1f);
        
        if (!hasSpawned)
        {
            SpawnAllNPCs();
        }
    }
    
    private void SpawnAllNPCs()
    {
        int successCount = 0;
        
        foreach (var npcData in npcSpawnList)
        {
            if (SpawnNPC(npcData))
            {
                successCount++;
            }
        }
        
        hasSpawned = true;
        
        // Spawn işlemi bittikten sonra bu manager'ı deaktif et
        if (deactivateAfterSpawn)
        {
            StartCoroutine(DeactivateAfterFrame());
        }
    }
    
    private System.Collections.IEnumerator DeactivateAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        gameObject.SetActive(false);
    }
    
    private bool SpawnNPC(NPCSpawnData npcData)
    {
        if (string.IsNullOrEmpty(npcData.npcPrefabName)) 
        {
            Debug.LogWarning("[NPCSpawnerManager] Empty NPC prefab name");
            return false;
        }
        
        GameObject npcPrefab = Resources.Load<GameObject>("NPCs/" + npcData.npcPrefabName);

        if (npcPrefab == null)
        {
            Debug.LogError($"[NPCSpawnerManager] NPC prefab not found: {npcData.npcPrefabName}");
            return false;
        }
        
        NetworkObject networkPrefab = npcPrefab.GetComponent<NetworkObject>();
        if (networkPrefab == null)
        {
            Debug.LogError($"[NPCSpawnerManager] NPC prefab doesn't have NetworkObject: {npcData.npcPrefabName}");
            return false;
        }
        
        try
        {
            NetworkObject spawnedNPC = NetworkManager.Instance.Runner.Spawn(
                networkPrefab, 
                npcData.spawnPosition, 
                Quaternion.identity
            );
            
            if (spawnedNPC != null)
            {
                return true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NPCSpawnerManager] Exception spawning {npcData.npcPrefabName}: {e.Message}");
        }
        
        return false;
    }
}