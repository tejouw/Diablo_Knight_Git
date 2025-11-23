using UnityEngine;
using Fusion;
using System;

[System.Serializable]
public struct ClassMilestone
{
    public int level;
    public StatType statType;
    public float bonusValue;
    public string description;
}
public class ClassSystem : NetworkBehaviour
{
    [Header("Class Settings")]
    [SerializeField] private int classSelectionLevel = 2;
    
    [Networked] public ClassType NetworkPlayerClass { get; set; } = ClassType.None;
    
    private PlayerStats playerStats;
    private SkillSystem skillSystem;
    
    // Events
    public event System.Action<ClassType> OnClassChanged;
    
public override void Spawned()
{
    playerStats = GetComponent<PlayerStats>();
    skillSystem = GetComponent<SkillSystem>();
    
    if (Object.HasInputAuthority)
    {
        LoadPlayerClass();
    }
}
    
public bool CanSelectClass()
{
    bool playerStatsCheck = playerStats != null;
    bool levelCheck = playerStats != null && playerStats.CurrentLevel >= classSelectionLevel;
    bool classCheck = NetworkPlayerClass == ClassType.None;
    
    string playerName = GetPlayerName();
    bool isServer = Runner?.IsServer ?? false;
    bool hasInputAuth = Object?.HasInputAuthority ?? false;
    
    
    bool result = playerStatsCheck && levelCheck && classCheck;
    return result;
}

public void SelectClass(ClassType classType)
{
    if (!Object.HasInputAuthority || !CanSelectClass()) 
    {
        Debug.LogError($"[ClassSystem-{GetPlayerName()}] SelectClass FAILED - InputAuth: {Object.HasInputAuthority}, CanSelect: {CanSelectClass()}");
        return;
    }
    
    
    // Network property'yi set et ve RPC gönder
    NetworkPlayerClass = classType;
    
    // Server'a da gönder (güvenlik için)
    if (Object.HasInputAuthority)
    {
        RequestClassSelectionRPC(classType);
    }
    
    // Event'i trigger et
    OnClassChanged?.Invoke(classType);
    
    // Skill sistemine bildir
    if (skillSystem != null)
    {
        skillSystem.OnClassChanged(classType);
    }
    
    SavePlayerClass();
    
}
private string GetPlayerName()
{
    return playerStats?.GetPlayerDisplayName() ?? $"Player_{GetHashCode() % 1000}";
}
    
[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void RequestClassSelectionRPC(ClassType classType)
{
    string playerName = GetPlayerName();
    
    if (!Runner.IsServer)
    {
        Debug.LogError($"[ClassSystem-SERVER-{playerName}] RequestClassSelectionRPC FAILED - Not server!");
        return;
    }
    
    
    NetworkPlayerClass = classType;
    
    // Tüm client'lara sync et
    SyncClassSelectionRPC(classType);
    
    // Skill sistemine bildir
    if (skillSystem != null)
    {
        skillSystem.OnClassChanged(classType);
    }
}
    
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncClassSelectionRPC(ClassType classType)
{
    string playerName = GetPlayerName();
    ClassType oldClass = NetworkPlayerClass;
    
    NetworkPlayerClass = classType;
    
    OnClassChanged?.Invoke(classType);
    
    if (Object.HasInputAuthority)
    {
        SavePlayerClass();
    }
}
    
private async void LoadPlayerClass()
{
    string nickname = playerStats?.GetPlayerDisplayName() ?? "Player";
    
    // Önce Firebase'den dene
    if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
    {
        try
        {
            var savedClass = await FirebaseManager.Instance.LoadPlayerClass(nickname);
            
            if (savedClass.HasValue && savedClass.Value != ClassType.None)
            {
                RequestLoadClassRPC(savedClass.Value);
                return;
            }
            else
            {
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ClassSystem-{nickname}] Firebase'den class yükleme hatası: {e.Message}");
        }
    }
    else
    {
        Debug.LogWarning($"[ClassSystem-{nickname}] Firebase not ready");
    }
    
    // Firebase başarısızsa PlayerPrefs'ten dene (fallback)
    string classKey = $"PlayerClass_{nickname}";
    if (PlayerPrefs.HasKey(classKey))
    {
        string classString = PlayerPrefs.GetString(classKey);
        
        if (System.Enum.TryParse<ClassType>(classString, out ClassType savedClass))
        {
            if (savedClass != ClassType.None)
            {
                RequestLoadClassRPC(savedClass);
            }
            else
            {
            }
        }
    }
    else
    {
    }
}

[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void RequestLoadClassRPC(ClassType classType)
{
    if (!Runner.IsServer) return;
    
    NetworkPlayerClass = classType;
    SyncClassLoadRPC(classType);
    
    // Server tarafında OnClassChanged çağırma - Client tarafında yapılacak
}
public void ResetClass()
{
    if (!Object.HasInputAuthority) return;
    
    RequestResetClassRPC();
}
public void ForceResetClass()
{
    if (!Object.HasStateAuthority) return;
    
    string playerName = GetPlayerName();
    
    NetworkPlayerClass = ClassType.None;
    
    // Skill sistemine bildir - Authority kontrolü ile
    if (skillSystem != null)
    {
        if (Object.HasInputAuthority)
        {
            // Client ise normal event
            skillSystem.OnClassChanged(ClassType.None);
        }
        else
        {
            // Server ise direkt skill temizle
            skillSystem.ClearSkillsDirectly();
            skillSystem.RefreshSkillUI();
        }
    }
    
    // Event'i trigger et
    OnClassChanged?.Invoke(ClassType.None);
    
    // PlayerPrefs temizleme sadece InputAuthority'de
    if (Object.HasInputAuthority)
    {
        string nickname = playerStats?.GetPlayerDisplayName() ?? "Player";
        string classKey = $"PlayerClass_{nickname}";
        PlayerPrefs.DeleteKey(classKey);
        PlayerPrefs.Save();
        
        // YENI: Firebase'e de None class'ını kaydet
        SavePlayerClass();
    }
}
[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
public void RequestResetClassRPC()
{
    if (!Runner.IsServer) return;
    
    NetworkPlayerClass = ClassType.None;
    SyncClassResetRPC();
    
    // Skill sistemine bildir
    if (skillSystem != null)
    {
        skillSystem.OnClassChanged(ClassType.None);
    }
}
public void TriggerClassChangedEvent(ClassType classType)
{
    OnClassChanged?.Invoke(classType);
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncClassResetRPC()
{
    NetworkPlayerClass = ClassType.None;
    OnClassChanged?.Invoke(ClassType.None);
    
    if (Object.HasInputAuthority)
    {
        SavePlayerClass();
    }
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncClassLoadRPC(ClassType classType)
{
    NetworkPlayerClass = classType;
    
    OnClassChanged?.Invoke(classType);
    
    // Sadece InputAuthority sahibi client'ta skill equip işlemlerini yap
    if (Object.HasInputAuthority)
    {
        // Skill sistemine bildir - sadece kendi client'ında
        if (skillSystem != null)
        {
            skillSystem.OnClassChanged(classType);
        }
        
        SavePlayerClass();
    }
}

private async void SavePlayerClass()
{
    if (NetworkPlayerClass == ClassType.None) 
    {
        string playerName = GetPlayerName();
    }
    
    string nickname = playerStats?.GetPlayerDisplayName() ?? "Player";
    
    // Firebase'e kaydet
    if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
    {
        try
        {
            await FirebaseManager.Instance.SavePlayerClass(nickname, NetworkPlayerClass);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ClassSystem-{nickname}] Firebase'e class kaydetme hatası: {e.Message}");
        }
    }
    else
    {
        Debug.LogWarning($"[ClassSystem-{nickname}] Firebase not ready, cannot save class");
    }
    
    // PlayerPrefs'e de kaydet (fallback)
    string classKey = $"PlayerClass_{nickname}";
    PlayerPrefs.SetString(classKey, NetworkPlayerClass.ToString());
    PlayerPrefs.Save();
}

    private void OnDestroy()
    {
        // MEMORY LEAK FIX: Event cleanup
        OnClassChanged = null;
    }
}