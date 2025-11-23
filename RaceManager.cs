using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


public class RaceManager : NetworkBehaviour
{
    public static RaceManager Instance;
    
    [Header("Race Management")]
    [SerializeField] private bool debugLogs = true;
    
    // Network properties
    [Networked] public bool IsReady { get; set; }
    
    private Dictionary<PlayerRef, PlayerRace> playerRaces = new Dictionary<PlayerRef, PlayerRace>();
    private List<PendingRaceData> pendingRaces = new List<PendingRaceData>();
    
    [System.Serializable]
    public class PendingRaceData
    {
        public PlayerRef player;
        public PlayerRace race;
        public float timestamp;
        
        public PendingRaceData(PlayerRef p, PlayerRace r)
        {
            player = p;
            race = r;
            timestamp = Time.time;
        }
    }

public override void Spawned()
{
    
    if (Instance == null)
    {
        Instance = this;
        
        if (Object.HasStateAuthority)
        {
            IsReady = true;
            StartCoroutine(ProcessPendingRaces());
            StartCoroutine(AggressiveRaceCollection()); // YENİ
        }
    }

}

    // Mevcut AggressiveRaceCollection metodunu değiştir - daha sık ve agresif çalışsın
    private IEnumerator AggressiveRaceCollection()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f); // 1f → 0.5f (daha sık)

            if (!Object.HasStateAuthority) continue;

            // Aktif player'ları kontrol et, race'i olmayan varsa al
            foreach (var player in Runner.ActivePlayers)
            {
                if (!playerRaces.ContainsKey(player))
                {
                    RequestPlayerRaceRPC(player);
                }
            }
        }
    }

[Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
public void RequestPlayerRaceRPC(PlayerRef targetPlayer)
{
    // Sadece target player yanıtlasın
    if (Runner.LocalPlayer == targetPlayer)
    {
        string nickname = NetworkManager.Instance?.GetLocalPlayerNickname();
        if (!string.IsNullOrEmpty(nickname))
        {
            try
            {
                // NetworkManager'dan race al
                PlayerRace race = GetPlayerRaceFromPrefsInternal(nickname);
                RegisterPlayerRaceRPC(targetPlayer, (int)race);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"🔴 [RACE] Could not get race for {targetPlayer}: {e.Message}");
            }
        }
    }
}

// RaceManager'a ekle - PlayerPrefs'ten race al
private PlayerRace GetPlayerRaceFromPrefsInternal(string nickname)
{
    string raceKey = "PlayerRace_" + nickname;
    
    if (PlayerPrefs.HasKey(raceKey))
    {
        string raceString = PlayerPrefs.GetString(raceKey);
        if (System.Enum.TryParse<PlayerRace>(raceString, out PlayerRace race))
        {
            return race;
        }
    }
    
    // Default yoksa exception fırlat
    throw new System.Exception($"[RaceManager] No race data found for {nickname}!");
}

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, InvokeLocal = false)]
    public void RegisterPlayerRaceRPC(PlayerRef player, int raceType)
    {
        
        PlayerRace race = (PlayerRace)raceType;
        playerRaces[player] = race;
        
        // NetworkManager'a bildir
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnPlayerRaceReceived(player, race);
        }
        
        // Tüm client'lara race bilgisini sync et
        SyncPlayerRaceToAllRPC(player, raceType);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
    public void SyncPlayerRaceToAllRPC(PlayerRef player, int raceType)
    {
        if (debugLogs)
        
        playerRaces[player] = (PlayerRace)raceType;
    }

    public bool TryGetPlayerRace(PlayerRef player, out PlayerRace race)
    {
        return playerRaces.TryGetValue(player, out race);
    }

// RaceManager.cs - GetPlayerRace metodunu kaldır veya exception fırlat
public PlayerRace GetPlayerRace(string nickname)
{
    string raceKey = "PlayerRace_" + nickname;
    
    if (PlayerPrefs.HasKey(raceKey))
    {
        string raceString = PlayerPrefs.GetString(raceKey);
        if (System.Enum.TryParse<PlayerRace>(raceString, out PlayerRace race))
        {
            return race;
        }
    }
    
    // DEFAULT KALDIR
    throw new System.Exception($"[RaceManager] No race data found for {nickname}! This should never happen.");
}

public void RegisterPlayerRaceWithRetry(PlayerRef player, PlayerRace race)
{
    if (IsReady)
    {
        RegisterPlayerRaceRPC(player, (int)race);
    }
    else
    {
        // Pending listesine ekle
        pendingRaces.Add(new PendingRaceData(player, race));
    }
}

private IEnumerator ProcessPendingRaces()
{
    while (true)
    {
        yield return new WaitForSeconds(0.2f); // 0.5s -> 0.2s (daha sık kontrol)
        
        if (IsReady && pendingRaces.Count > 0)
        {
            for (int i = pendingRaces.Count - 1; i >= 0; i--)
            {
                var pending = pendingRaces[i];
                
                // 30 saniyeden eski pending race'leri temizle (10s -> 30s)
                if (Time.time - pending.timestamp > 30f)
                {
                    pendingRaces.RemoveAt(i);
                    continue;
                }
                
                // Race bilgisini kaydet
                RegisterPlayerRaceRPC(pending.player, (int)pending.race);
                pendingRaces.RemoveAt(i);
            }
        }
    }
}

    public void CleanupPlayerRace(PlayerRef player)
    {
        playerRaces.Remove(player);
        
        // Pending listesinden de temizle
        for (int i = pendingRaces.Count - 1; i >= 0; i--)
        {
            if (pendingRaces[i].player == player)
            {
                pendingRaces.RemoveAt(i);
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Passive cleanup her 5 saniyede bir
        if (Object.HasStateAuthority && Runner.Tick % (Runner.TickRate * 5) == 0)
        {
            CleanupDisconnectedPlayers();
        }
    }

private void CleanupDisconnectedPlayers()
{
    List<PlayerRef> toRemove = new List<PlayerRef>();
    
    foreach (var kvp in playerRaces)
    {
        // Fusion 2'de ActivePlayers.Contains kullan
        if (!Runner.ActivePlayers.Contains(kvp.Key))
        {
            toRemove.Add(kvp.Key);
        }
    }
    
    foreach (var player in toRemove)
    {
        CleanupPlayerRace(player);
        

    }
}
}