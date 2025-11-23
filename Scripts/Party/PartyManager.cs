// Path: Assets/Game/Scripts/PartyManager.cs

using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class PartyManager : NetworkBehaviour
{
    public static PartyManager Instance { get; private set; }

    [Networked, Capacity(25)] public NetworkArray<PartyInfo> Parties => default;
    [Networked, Capacity(100)] public NetworkArray<PlayerRef> PartyMembers => default; // 4*25 parti için
    [Networked] public int NextPartyId { get; set; } = 1;

    private Dictionary<PlayerRef, PendingInvite> pendingInvites = new Dictionary<PlayerRef, PendingInvite>();
    private const float INVITE_TIMEOUT = 30f; // 30 saniye
    private const int MAX_PARTY_SIZE = 4;

    public struct PartyInfo : INetworkStruct
    {
        public int PartyId;
        public PlayerRef Leader;
        public int StartIndex; // PartyMembers array'indeki başlangıç indeksi
        public int MemberCount;
        public bool IsActive;
    }

    public struct PendingInvite
    {
        public PlayerRef Inviter;
        public PlayerRef Target;
        public string InviterName;
        public int InviterLevel;
        public float TimeStamp;
    }

    public override void Spawned()
    {
        if (Runner.IsServer)
        {
            Instance = this;
            
            // Tüm parti slotlarını temizle
            for (int i = 0; i < Parties.Length; i++)
            {
                Parties.Set(i, new PartyInfo { PartyId = -1, IsActive = false });
            }
            
            // Tüm member slotlarını temizle
            for (int i = 0; i < PartyMembers.Length; i++)
            {
                PartyMembers.Set(i, PlayerRef.None);
            }
        }
        else
        {
            Instance = this;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer) return;

        // Timeout olan davetleri temizle
        CleanupExpiredInvites();
    }

    #region Server RPC Handlers

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RequestPartyInviteRPC(PlayerRef target, RpcInfo info = default)
    {
        if (!Runner.IsServer) return;

        PlayerRef inviter = info.Source;

        // Validasyon kontrolleri
        if (!IsValidInviteRequest(inviter, target, out string errorMsg))
        {
            NotifyInviteErrorRPC(inviter, errorMsg);
            return;
        }

        // Davetçinin bilgilerini al
        PlayerStats inviterStats = GetPlayerStats(inviter);
        if (inviterStats == null)
        {
            NotifyInviteErrorRPC(inviter, "Davetçi bilgileri alınamadı");
            return;
        }

        // Bekleyen davet oluştur
        var invite = new PendingInvite
        {
            Inviter = inviter,
            Target = target,
            InviterName = inviterStats.GetPlayerDisplayName(),
            InviterLevel = inviterStats.CurrentLevel,
            TimeStamp = Runner.SimulationTime
        };

        pendingInvites[target] = invite;

        // Hedefe davet gönder
        NotifyPartyInviteRPC(target, inviter, invite.InviterName, invite.InviterLevel);
        
    }

[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
public void RespondToInviteRPC(PlayerRef inviter, bool accept, RpcInfo info = default)
{
    if (!Runner.IsServer) return;

    PlayerRef responder = info.Source;

    // Bekleyen daveti kontrol et
    if (!pendingInvites.TryGetValue(responder, out PendingInvite invite) || invite.Inviter != inviter)
    {
        Debug.LogWarning($"[PartyManager] Geçersiz davet yanıtı: {responder}");
        return;
    }

    // Daveti temizle
    pendingInvites.Remove(responder);

    // Yanıtçının bilgilerini al
    PlayerStats responderStats = GetPlayerStats(responder);
    if (responderStats == null) return;

    if (accept)
    {
        // Davetçinin parti durumunu kontrol et
        int inviterPartyId = GetPlayerPartyId(inviter);
        
        if (inviterPartyId == -1)
        {
            // Yeni parti oluştur
            if (CreateParty(inviter, responder))
            {
                NotifyInviteAcceptedRPC(inviter, responderStats.GetPlayerDisplayName());
            }
            else
            {
                NotifyInviteErrorRPC(inviter, "Parti oluşturulamadı");
            }
        }
        else
        {
            // Mevcut partiye ekle
            if (AddPlayerToExistingParty(responder, inviterPartyId))
            {
                NotifyInviteAcceptedRPC(inviter, responderStats.GetPlayerDisplayName());
            }
            else
            {
                NotifyInviteErrorRPC(inviter, "Partiye eklenemedi");
            }
        }
    }
    else
    {
        NotifyInviteDeclinedRPC(inviter, responderStats.GetPlayerDisplayName());
    }
}

private bool AddPlayerToExistingParty(PlayerRef newMember, int partyId)
{
    int partySlot = FindPartySlot(partyId);
    if (partySlot == -1)
    {
        Debug.LogError($"[PartyManager] Parti bulunamadı: {partyId}");
        return false;
    }

    var partyInfo = Parties[partySlot];
    
    // Parti dolu mu kontrol et
    if (partyInfo.MemberCount >= MAX_PARTY_SIZE)
    {
        Debug.LogError($"[PartyManager] Parti dolu: {partyId}");
        return false;
    }

    // Yeni üyeyi ekle
    int newMemberIndex = partyInfo.StartIndex + partyInfo.MemberCount;
    PartyMembers.Set(newMemberIndex, newMember);
    
    // Parti bilgisini güncelle
    partyInfo.MemberCount++;
    Parties.Set(partySlot, partyInfo);

    // Oyuncunun parti ID'sini güncelle
    UpdatePlayerPartyId(newMember, partyId);

    // Güncel üye listesini al
    List<PlayerRef> allMembers = new List<PlayerRef>();
    for (int i = partyInfo.StartIndex; i < partyInfo.StartIndex + partyInfo.MemberCount; i++)
    {
        if (PartyMembers[i] != PlayerRef.None)
        {
            allMembers.Add(PartyMembers[i]);
        }
    }

    // Tüm clientlara bildir
    NotifyPartyUpdateRPC(partyId, allMembers.ToArray(), partyInfo.Leader);

    return true;
}

[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
public void RequestLeavePartyRPC(RpcInfo info = default)
{
    if (!Runner.IsServer) return;

    PlayerRef player = info.Source;
    
    // DETAYLI LOG EKLE
    
    PlayerStats playerStats = GetPlayerStats(player);
    int playerStatsPartyId = playerStats?.CurrentPartyId ?? -1;
    int partyManagerPartyId = GetPlayerPartyIdFromManager(player);
    
    
    int partyId = partyManagerPartyId; // PartyManager'ın kendi verilerini kullan
    
    if (partyId == -1)
    {
        Debug.LogWarning($"[PartyManager] Partide olmayan oyuncu ayrılmaya çalışıyor: {player}");
        return;
    }

    RemovePlayerFromParty(player, partyId);
}

// YENİ METOD EKLE
private int GetPlayerPartyIdFromManager(PlayerRef player)
{
    for (int i = 0; i < Parties.Length; i++)
    {
        var party = Parties[i];
        if (!party.IsActive) continue;
        
        // Bu partinin üyelerini kontrol et
        for (int j = party.StartIndex; j < party.StartIndex + party.MemberCount; j++)
        {
            if (PartyMembers[j] == player)
            {
                return party.PartyId;
            }
        }
    }
    
    return -1;
}

    #endregion

    #region Client RPC Notifications

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void NotifyPartyInviteRPC([RpcTarget] PlayerRef target, PlayerRef inviter, string inviterName, int inviterLevel)
    {
        if (target == Runner.LocalPlayer)
        {
            
            // UI'a bildir
            if (PartyUIManager.Instance != null)
            {
                PartyUIManager.Instance.ShowPartyRequest(inviter, inviterName, inviterLevel);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void NotifyPartyUpdateRPC(int partyId, PlayerRef[] members, PlayerRef leader)
    {
        
        // Local player partide mi kontrol et
        bool localPlayerInParty = members.Contains(Runner.LocalPlayer);
        
        if (localPlayerInParty)
        {
            // Local player'ın parti ID'sini güncelle
            PlayerStats localStats = GetLocalPlayerStats();
            if (localStats != null)
            {
                localStats.UpdatePartyId(partyId);
            }
        }

        // UI'ı güncelle
        if (PartyUIManager.Instance != null)
        {
            PartyUIManager.Instance.OnPartyUpdated(partyId, members, leader, localPlayerInParty);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void NotifyPartyDisbandedRPC(int partyId)
    {
        
        // Local player'ın parti ID'sini temizle
        PlayerStats localStats = GetLocalPlayerStats();
        if (localStats != null && localStats.CurrentPartyId == partyId)
        {
            localStats.UpdatePartyId(-1);
        }

        // UI'ı güncelle
        if (PartyUIManager.Instance != null)
        {
            PartyUIManager.Instance.OnPartyDisbanded();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void NotifyInviteAcceptedRPC([RpcTarget] PlayerRef inviter, string responderName)
    {
        if (inviter == Runner.LocalPlayer)
        {
            
            if (ChatManager.Instance != null)
            {
                ChatManager.Instance.ShowPartyAcceptedMessage(responderName);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void NotifyInviteDeclinedRPC([RpcTarget] PlayerRef inviter, string responderName)
    {
        if (inviter == Runner.LocalPlayer)
        {
            
            if (ChatManager.Instance != null)
            {
                ChatManager.Instance.ShowPartyDeclinedMessage(responderName);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void NotifyInviteErrorRPC([RpcTarget] PlayerRef target, string errorMessage)
    {
        if (target == Runner.LocalPlayer)
        {
            Debug.LogWarning($"[PartyManager] Davet hatası: {errorMessage}");
            
        //    if (ChatManager.Instance != null)
        //    {
        //        ChatManager.Instance.ShowSystemMessage(errorMessage);
        //    }
        }
    }

    #endregion

    #region Server Logic

    private bool CreateParty(PlayerRef leader, PlayerRef member)
    {
        // Boş parti slotu bul
        int partySlot = FindEmptyPartySlot();
        if (partySlot == -1)
        {
            Debug.LogError("[PartyManager] Boş parti slotu bulunamadı");
            return false;
        }

        // Boş member slotları bul
        int memberStartIndex = FindEmptyMemberSlots(2);
        if (memberStartIndex == -1)
        {
            Debug.LogError("[PartyManager] Yeterli member slotu bulunamadı");
            return false;
        }

        int partyId = NextPartyId++;

        // Parti bilgisini kaydet
        var partyInfo = new PartyInfo
        {
            PartyId = partyId,
            Leader = leader,
            StartIndex = memberStartIndex,
            MemberCount = 2,
            IsActive = true
        };
        Parties.Set(partySlot, partyInfo);

        // Üyeleri kaydet
        PartyMembers.Set(memberStartIndex, leader);
        PartyMembers.Set(memberStartIndex + 1, member);

        // Oyuncuların parti ID'lerini güncelle
        UpdatePlayerPartyId(leader, partyId);
        UpdatePlayerPartyId(member, partyId);

        // Tüm clientlara bildir
        PlayerRef[] members = { leader, member };
        NotifyPartyUpdateRPC(partyId, members, leader);

        return true;
    }

    private void RemovePlayerFromParty(PlayerRef player, int partyId)
    {
        int partySlot = FindPartySlot(partyId);
        if (partySlot == -1) return;

        var partyInfo = Parties[partySlot];
        
        // Oyuncuyu member listesinden çıkar
        List<PlayerRef> remainingMembers = new List<PlayerRef>();
        for (int i = partyInfo.StartIndex; i < partyInfo.StartIndex + partyInfo.MemberCount; i++)
        {
            if (PartyMembers[i] != player && PartyMembers[i] != PlayerRef.None)
            {
                remainingMembers.Add(PartyMembers[i]);
            }
        }

        // Eski slotları temizle
        for (int i = partyInfo.StartIndex; i < partyInfo.StartIndex + partyInfo.MemberCount; i++)
        {
            PartyMembers.Set(i, PlayerRef.None);
        }

        // Oyuncunun parti ID'sini temizle
        UpdatePlayerPartyId(player, -1);

        if (remainingMembers.Count <= 1)
        {
            // Parti dağılıyor
            if (remainingMembers.Count == 1)
            {
                UpdatePlayerPartyId(remainingMembers[0], -1);
            }
            
            // Parti slotunu temizle
            Parties.Set(partySlot, new PartyInfo { PartyId = -1, IsActive = false });
            
            NotifyPartyDisbandedRPC(partyId);
        }
        else
        {
            // Parti devam ediyor
            PlayerRef newLeader = (partyInfo.Leader == player) ? remainingMembers[0] : partyInfo.Leader;
            
            // Yeni member slotlarını ayarla
            for (int i = 0; i < remainingMembers.Count; i++)
            {
                PartyMembers.Set(partyInfo.StartIndex + i, remainingMembers[i]);
            }

            // Parti bilgisini güncelle
            partyInfo.Leader = newLeader;
            partyInfo.MemberCount = remainingMembers.Count;
            Parties.Set(partySlot, partyInfo);

            NotifyPartyUpdateRPC(partyId, remainingMembers.ToArray(), newLeader);
        }
    }

private bool IsValidInviteRequest(PlayerRef inviter, PlayerRef target, out string errorMsg)
{
    errorMsg = "";

    // Kendine davet gönderemez
    if (inviter == target)
    {
        errorMsg = "Kendine parti daveti gönderemezsin";
        return false;
    }

    // Davetçinin parti durumunu kontrol et
    int inviterPartyId = GetPlayerPartyId(inviter);
    
    if (inviterPartyId != -1)
    {
        // Davetçi partide - parti lideri mi ve parti dolu mu kontrol et
        PlayerRef partyLeader = GetPartyLeader(inviterPartyId);
        var partyMembers = GetPartyMembers(inviterPartyId);
        
        if (inviter != partyLeader)
        {
            errorMsg = "Sadece parti lideri davet gönderebilir";
            return false;
        }
        
        if (partyMembers.Count >= MAX_PARTY_SIZE)
        {
            errorMsg = "Parti dolu";
            return false;
        }
        
    }

    // Hedef zaten partide mi?
    if (GetPlayerPartyId(target) != -1)
    {
        errorMsg = "Bu oyuncu zaten bir partide";
        return false;
    }

    // Bekleyen davet var mı?
    if (pendingInvites.ContainsKey(target))
    {
        errorMsg = "Bu oyuncuda bekleyen bir davet var";
        return false;
    }

    return true;
}

    private void CleanupExpiredInvites()
    {
        var expiredInvites = pendingInvites.Where(kvp => Runner.SimulationTime - kvp.Value.TimeStamp > INVITE_TIMEOUT).ToList();
        
        foreach (var expired in expiredInvites)
        {
            pendingInvites.Remove(expired.Key);
        }
    }

    #endregion

    #region Helper Methods

    private int FindEmptyPartySlot()
    {
        for (int i = 0; i < Parties.Length; i++)
        {
            if (!Parties[i].IsActive)
            {
                return i;
            }
        }
        return -1;
    }
public bool IsPartyLeader(PlayerRef player, int partyId)
{
    PlayerRef leader = GetPartyLeader(partyId);
    return leader == player;
}

public bool IsPartyFull(int partyId)
{
    var members = GetPartyMembers(partyId);
    return members.Count >= MAX_PARTY_SIZE;
}
    private int FindEmptyMemberSlots(int requiredSlots)
    {
        int consecutiveEmpty = 0;
        int startIndex = -1;

        for (int i = 0; i < PartyMembers.Length; i++)
        {
            if (PartyMembers[i] == PlayerRef.None)
            {
                if (consecutiveEmpty == 0)
                {
                    startIndex = i;
                }
                consecutiveEmpty++;

                if (consecutiveEmpty >= requiredSlots)
                {
                    return startIndex;
                }
            }
            else
            {
                consecutiveEmpty = 0;
                startIndex = -1;
            }
        }

        return -1;
    }

    private int FindPartySlot(int partyId)
    {
        for (int i = 0; i < Parties.Length; i++)
        {
            if (Parties[i].IsActive && Parties[i].PartyId == partyId)
            {
                return i;
            }
        }
        return -1;
    }

private int GetPlayerPartyId(PlayerRef player)
{
    PlayerStats playerStats = GetPlayerStats(player);
    int statsResult = playerStats?.CurrentPartyId ?? -1;
    int managerResult = GetPlayerPartyIdFromManager(player);
    
    // DETAYLI LOG
    
    return managerResult; // PartyManager'ın verilerini kullan
}


    private void UpdatePlayerPartyId(PlayerRef player, int partyId)
    {
        PlayerStats playerStats = GetPlayerStats(player);
        if (playerStats != null)
        {
            playerStats.UpdatePartyId(partyId);
        }
    }

private PlayerStats GetPlayerStats(PlayerRef player)
{
    NetworkObject[] allPlayers = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
    
    
    foreach (NetworkObject netObj in allPlayers)
    {
        if (netObj != null && netObj.IsValid && netObj.InputAuthority == player)
        {
            PlayerStats stats = netObj.GetComponent<PlayerStats>();
            return stats;
        }
    }
    
    return null;
}
    private PlayerStats GetLocalPlayerStats()
    {
        NetworkObject[] allPlayers = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        
        foreach (NetworkObject netObj in allPlayers)
        {
            if (netObj != null && netObj.IsValid && netObj.HasInputAuthority)
            {
                return netObj.GetComponent<PlayerStats>();
            }
        }
        
        return null;
    }

    #endregion

    #region Public API

    public bool IsPlayerInParty(PlayerRef player)
    {
        return GetPlayerPartyId(player) != -1;
    }

    public List<PlayerRef> GetPartyMembers(int partyId)
    {
        List<PlayerRef> members = new List<PlayerRef>();
        
        int partySlot = FindPartySlot(partyId);
        if (partySlot == -1) return members;

        var partyInfo = Parties[partySlot];
        for (int i = partyInfo.StartIndex; i < partyInfo.StartIndex + partyInfo.MemberCount; i++)
        {
            if (PartyMembers[i] != PlayerRef.None)
            {
                members.Add(PartyMembers[i]);
            }
        }

        return members;
    }

    public PlayerRef GetPartyLeader(int partyId)
    {
        int partySlot = FindPartySlot(partyId);
        return partySlot != -1 ? Parties[partySlot].Leader : PlayerRef.None;
    }

    #endregion
}