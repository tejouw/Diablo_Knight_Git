// Path: Assets/Game/Scripts/PartyUIManager.cs

using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using TMPro;

public class PartyUIManager : MonoBehaviour
{
    public static PartyUIManager Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject nearbyPlayersPanel;
    [SerializeField] private GameObject partyRequestPanel;
    [SerializeField] private GameObject partyMembersPanel;

    [Header("UI Components - Nearby Players")]
    [SerializeField] private Transform nearbyPlayersContent;
    [SerializeField] private GameObject nearbyPlayerItemPrefab;

    [Header("UI Components - Party Request")]
    [SerializeField] private TextMeshProUGUI requestPlayerNameText;
    [SerializeField] private TextMeshProUGUI requestPlayerLevelText;
    [SerializeField] private UnityEngine.UI.Button acceptButton;
    [SerializeField] private UnityEngine.UI.Button declineButton;

    [Header("UI Components - Party Members")]
    [SerializeField] private Transform partyMembersContent;
    [SerializeField] private GameObject partyMemberItemPrefab;
    [SerializeField] private UnityEngine.UI.Button leavePartyButton;

    private PlayerRef pendingInviterPlayer;
    private Dictionary<PlayerRef, GameObject> nearbyPlayerItems = new Dictionary<PlayerRef, GameObject>();
    private Dictionary<PlayerRef, GameObject> partyMemberItems = new Dictionary<PlayerRef, GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (nearbyPlayersPanel != null) nearbyPlayersPanel.SetActive(false);
        if (partyRequestPanel != null) partyRequestPanel.SetActive(false);
        if (partyMembersPanel != null) partyMembersPanel.SetActive(false);

        if (acceptButton != null)
            acceptButton.onClick.AddListener(() => RespondToInvite(true));
        
        if (declineButton != null)
            declineButton.onClick.AddListener(() => RespondToInvite(false));

        if (leavePartyButton != null)
            leavePartyButton.onClick.AddListener(LeaveParty);
    }

    public void ShowNearbyPlayers(Dictionary<PlayerRef, PlayerStats> nearbyPlayers)
    {
        if (nearbyPlayersPanel == null) return;

        nearbyPlayersPanel.SetActive(true);

        // Mevcut itemları temizle
        foreach (var item in nearbyPlayerItems.Values)
        {
            if (item != null) Destroy(item);
        }
        nearbyPlayerItems.Clear();

        // Yeni itemları oluştur
        foreach (var kvp in nearbyPlayers)
        {
            CreateNearbyPlayerItem(kvp.Key, kvp.Value);
        }
    }

    public void HideNearbyPlayers()
    {
        if (nearbyPlayersPanel != null)
            nearbyPlayersPanel.SetActive(false);
    }

    public void ShowPartyRequest(PlayerRef inviterPlayer, string inviterNick, int inviterLevel)
    {
        if (partyRequestPanel == null) return;

        pendingInviterPlayer = inviterPlayer;
        partyRequestPanel.SetActive(true);

        if (requestPlayerNameText != null)
            requestPlayerNameText.text = inviterNick;
        
        if (requestPlayerLevelText != null)
            requestPlayerLevelText.text = $"Seviye {inviterLevel}";

    }

    // PartyManager'dan çağrılacak callback metodları
public void OnPartyUpdated(int partyId, PlayerRef[] members, PlayerRef leader, bool localPlayerInParty)
{
    
    if (localPlayerInParty)
    {
        ShowPartyMembers(members, leader);
        
        // Eğer local player lider ise ve parti dolu değilse, nearby players panelini kapatma
        bool isLocalPlayerLeader = (leader == GetLocalPlayer());
        bool isPartyFull = (members.Length >= 4);
        
        
        if (!isLocalPlayerLeader || isPartyFull)
        {
            HideNearbyPlayers();
        }
        // Lider ise ve parti dolu değilse nearby players açık kalacak
    }
    else
    {
        HidePartyMembers();
    }
}

// Yeni helper metod ekle
private PlayerRef GetLocalPlayer()
{
    NetworkObject[] allNetworkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
    foreach (NetworkObject netObj in allNetworkObjects)
    {
        if (netObj != null && netObj.IsValid && netObj.HasInputAuthority)
        {
            return netObj.InputAuthority;
        }
    }
    return PlayerRef.None;
}

    public void OnPartyDisbanded()
    {
        HidePartyMembers();
    }

    private void CreateNearbyPlayerItem(PlayerRef playerRef, PlayerStats playerStats)
    {
        if (nearbyPlayerItemPrefab == null || nearbyPlayersContent == null) return;

        GameObject item = Instantiate(nearbyPlayerItemPrefab, nearbyPlayersContent);
        nearbyPlayerItems[playerRef] = item;

        // Item componentlarını ayarla
        NearbyPlayerItem itemComponent = item.GetComponent<NearbyPlayerItem>();
        if (itemComponent != null)
        {
            itemComponent.Setup(playerRef, playerStats.GetPlayerDisplayName(), playerStats.CurrentLevel, this);
        }
    }

    private void ShowPartyMembers(PlayerRef[] members, PlayerRef leader)
    {
        
        if (partyMembersPanel == null) 
        {
            Debug.LogError($"[PartyUIManager] partyMembersPanel null!");
            return;
        }

        partyMembersPanel.SetActive(true);

        // Mevcut itemları temizle
        foreach (var item in partyMemberItems.Values)
        {
            if (item != null) Destroy(item);
        }
        partyMemberItems.Clear();

        // Parti üyelerini oluştur
        foreach (PlayerRef member in members)
        {
            CreatePartyMemberItem(member, member == leader);
        }
    }

    private void HidePartyMembers()
    {
        if (partyMembersPanel != null)
            partyMembersPanel.SetActive(false);
    }

private void CreatePartyMemberItem(PlayerRef playerRef, bool isLeader)
{
    if (partyMemberItemPrefab == null || partyMembersContent == null) return;

    // Player stats'ını bul
    PlayerStats targetPlayerStats = GetPlayerStats(playerRef);
    if (targetPlayerStats == null) return;

    GameObject item = Instantiate(partyMemberItemPrefab, partyMembersContent);
    partyMemberItems[playerRef] = item;

    // Item componentlarını ayarla
    PartyMemberItem itemComponent = item.GetComponent<PartyMemberItem>();
    if (itemComponent != null)
    {
        string displayName = targetPlayerStats.GetPlayerDisplayName();
        if (isLeader)
        {
            displayName += " (Lider)";
        }
        
        itemComponent.Setup(playerRef, displayName, targetPlayerStats.NetworkCurrentLevel);
    }
}

    private PlayerStats GetPlayerStats(PlayerRef playerRef)
    {
        NetworkObject[] allPlayers = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

        foreach (NetworkObject netObj in allPlayers)
        {
            if (netObj != null && netObj.IsValid && netObj.InputAuthority == playerRef)
            {
                return netObj.GetComponent<PlayerStats>();
            }
        }

        return null;
    }

    private void RespondToInvite(bool accept)
    {
        
        if (PartyManager.Instance != null)
        {
            PartyManager.Instance.RespondToInviteRPC(pendingInviterPlayer, accept);
        }
        else
        {
            Debug.LogError($"[PartyUIManager] PartyManager.Instance null!");
        }

        partyRequestPanel.SetActive(false);
    }

    private void LeaveParty()
    {
        if (PartyManager.Instance != null)
        {
            PartyManager.Instance.RequestLeavePartyRPC();
        }
        else
        {
            Debug.LogError($"[PartyUIManager] PartyManager.Instance null!");
        }
    }

    public void OnInviteButtonPressed(PlayerRef targetPlayer)
    {
        // Local player'ın ProximityDetector'ını bul
        ProximityDetector localProximityDetector = null;
        
        NetworkObject[] allNetworkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        foreach (NetworkObject netObj in allNetworkObjects)
        {
            if (netObj != null && netObj.IsValid && netObj.HasInputAuthority)
            {
                localProximityDetector = netObj.GetComponent<ProximityDetector>();
                if (localProximityDetector != null)
                {
                    break;
                }
            }
        }
        
        if (localProximityDetector != null)
        {
            localProximityDetector.SendInviteToPlayer(targetPlayer);
        }
        else
        {
            Debug.LogError($"[PartyUIManager] Local ProximityDetector bulunamadı!");
        }
    }
}