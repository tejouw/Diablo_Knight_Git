// Path: Assets/Game/Scripts/ProximityDetector.cs

using System.Collections.Generic;
using Fusion;
using UnityEngine;
using System.Collections;

public class ProximityDetector : NetworkBehaviour
{
    [SerializeField] private float detectionRadius = 5f;
    private PartyUIManager partyUIManager;
    private Dictionary<PlayerRef, PlayerStats> nearbyPlayers = new Dictionary<PlayerRef, PlayerStats>();
    
    // Debug için
    private float lastLogTime = 0f;
    private const float LOG_INTERVAL = 1f;

    public override void Spawned()
    {
        // Object null kontrolü
        if (Object == null)
        {
            Debug.LogError($"[ProximityDetector] KRITIK HATA: Spawned'da Object null!");
            return;
        }
        
        if (!Object.HasInputAuthority) 
        {
            return;
        }

        partyUIManager = FindFirstObjectByType<PartyUIManager>();
        
        if (partyUIManager == null)
        {
            Debug.LogError($"[ProximityDetector] PartyUIManager bulunamadı!");
            return;
        }
        
        InvokeRepeating(nameof(CheckNearbyPlayers), 1f, 0.5f);
    }

private void CheckNearbyPlayers()
{
    if (!Object.HasInputAuthority) return;

    PlayerStats localStats = GetComponent<PlayerStats>();
    if (localStats == null) return;

    // Parti durumunu kontrol et
    bool isInParty = localStats.IsInParty();
    bool isPartyLeader = false;
    bool isPartyFull = false;
    
    if (isInParty)
    {
        // Parti liderliğini ve doluluk durumunu kontrol et
        if (PartyManager.Instance != null)
        {
            int partyId = localStats.GetPartyId();
            PlayerRef leader = PartyManager.Instance.GetPartyLeader(partyId);
            var partyMembers = PartyManager.Instance.GetPartyMembers(partyId);
            
            isPartyLeader = (leader == Object.InputAuthority);
            isPartyFull = (partyMembers.Count >= 4); // MAX_PARTY_SIZE
            
        }
        
        // Eğer parti üyesi ama lider değilse veya parti dolu ise proximity'yi gizle
        if (!isPartyLeader || isPartyFull)
        {
            if (partyUIManager != null)
            {
                partyUIManager.HideNearbyPlayers();
            }
            return;
        }
    }

    Dictionary<PlayerRef, PlayerStats> currentNearbyPlayers = new Dictionary<PlayerRef, PlayerStats>();
    bool shouldLog = Time.time - lastLogTime >= LOG_INTERVAL;

    // Tüm oyuncuları kontrol et
    NetworkObject[] allPlayers = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
    
    if (shouldLog)
    {
        lastLogTime = Time.time;
    }
    
    foreach (NetworkObject netObj in allPlayers)
    {
        if (netObj == null || !netObj.IsValid) continue;
        
        PlayerStats otherPlayerStats = netObj.GetComponent<PlayerStats>();
        if (otherPlayerStats == null) continue;
        
        // Kendisini her zaman exclude et
        if (netObj == Object) 
        {
            continue;
        }

        // Eğer lider ise, parti üyelerini de exclude et
        if (isPartyLeader && isInParty)
        {
            if (otherPlayerStats.IsInParty() && otherPlayerStats.GetPartyId() == localStats.GetPartyId())
            {
                continue; // Kendi parti üyelerini gösterme
            }
        }

        // Diğer oyuncu partide mi kontrol et (ama lider için kendi partisi hariç yukarıda kontrol edildi)
        if (!isPartyLeader && otherPlayerStats.IsInParty())
        {
            continue; // Normal üye ise partide olan oyuncuları gösterme
        }

        float distance = Vector2.Distance(transform.position, netObj.transform.position);
        
        if (distance <= detectionRadius)
        {
            currentNearbyPlayers[netObj.InputAuthority] = otherPlayerStats;
        }
    }

    // UI güncelle
    UpdateProximityUI(currentNearbyPlayers);
    nearbyPlayers = currentNearbyPlayers;
}

    private void UpdateProximityUI(Dictionary<PlayerRef, PlayerStats> players)
    {
        if (partyUIManager == null) return;

        if (players.Count > 0)
        {
            partyUIManager.ShowNearbyPlayers(players);
        }
        else
        {
            partyUIManager.HideNearbyPlayers();
        }
    }

    public void SendInviteToPlayer(PlayerRef targetPlayer)
    {
        if (PartyManager.Instance != null)
        {
            PartyManager.Instance.RequestPartyInviteRPC(targetPlayer);
        }
        else
        {
            // PartyManager henüz hazır değil, retry yap
            StartCoroutine(RetryInviteWhenReady(targetPlayer));
        }
    }
private IEnumerator RetryInviteWhenReady(PlayerRef targetPlayer)
{
    float timeout = 5f;
    float elapsed = 0f;
    
    while (PartyManager.Instance == null && elapsed < timeout)
    {
        yield return new WaitForSeconds(0.2f);
        elapsed += 0.2f;
    }
    
    if (PartyManager.Instance != null)
    {
        PartyManager.Instance.RequestPartyInviteRPC(targetPlayer);
    }
    else
    {
        Debug.LogError($"[ProximityDetector] PartyManager timeout after {timeout}s!");
    }
}

    private void OnDestroy()
    {
        CancelInvoke();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
#endif
}