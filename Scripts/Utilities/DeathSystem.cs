using UnityEngine;
using System.Collections;
using Fusion;
using Assets.HeroEditor4D.Common.Scripts.Enums;

public class DeathSystem : NetworkBehaviour
{
    [Header("Ölüm Sistemi Ayarları")]
    [SerializeField] private float respawnDelay = 3f;
    
    [Networked] public bool IsDead { get; set; }
    [Networked] public float DeathTime { get; set; }
    [Networked] public bool ShouldRespawn { get; set; }
    [Networked] public bool CanRespawn { get; set; } // Yeni property
    private PlayerStats playerStats;
    private NetworkCharacterController networkController;
    private Vector3 spawnPosition = Vector3.zero;
    private bool deathProcessed = false;

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        networkController = GetComponent<NetworkCharacterController>();
        spawnPosition = transform.position;
    }

public override void FixedUpdateNetwork()
{
    // Server tarafında respawn timer kontrolü - SADECE CanRespawn'ı aktif eder
    if (Runner.IsServer && IsDead && !CanRespawn)
    {
        if (Runner.SimulationTime >= DeathTime + respawnDelay)
        {
            CanRespawn = true; // ShouldRespawn yerine CanRespawn
        }
    }
    
    // Otomatik respawn kaldırıldı - sadece manual trigger ile respawn
    if (ShouldRespawn && IsDead)
    {
        ProcessRespawn();
    }
}
// DeathSystem.cs - Bu metodları ekle
public bool GetSafeDeathStatus()
{
    if (!Object || !Object.IsValid)
    {
        return false; // Object geçersizse ölü değil kabul et
    }

    try
    {
        return IsDead;
    }
    catch (System.Exception)
    {
        return false; // Hata durumunda ölü değil kabul et
    }
}

public bool IsSafeToCheckDeath()
{
    return Object != null && Object.IsValid && Runner != null;
}
// Yeni metod - Manual respawn trigger
[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
public void RequestRespawnRPC()
{
    if (!Runner.IsServer) return;
    
    string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";
    
    // Sadece respawn edilebiliyorsa ve ölüyse respawn et
    if (CanRespawn && IsDead)
    {
        ShouldRespawn = true;
    }
    else
    {
        Debug.LogWarning($"[DeathSystem-Server-{playerName}] Cannot respawn - CanRespawn: {CanRespawn}, IsDead: {IsDead}");
    }
}

// Public metod - UI'dan çağrılacak
public void TriggerManualRespawn()
{
    if (Object.HasInputAuthority && CanRespawn && IsDead)
    {
        RequestRespawnRPC();
    }
}


public void OnDeath()
{
    string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";
    
    // Server death state'i set eder
    if (Object.HasStateAuthority)
    {
        IsDead = true;
        DeathTime = (float)Runner.SimulationTime;
        ShouldRespawn = false;
        CanRespawn = false;
        
        // TÜM CLIENTLERE death durumunu bildir
        NotifyDeathToAllClientsRPC();
    }
    else if (Object.HasInputAuthority)
    {
        // Client detected death, server'a bildir
        RequestDeathRPC();
    }
}

// YENİ - Server'a death isteği
[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void RequestDeathRPC()
{
    if (!Runner.IsServer) return;
    
    IsDead = true;
    DeathTime = (float)Runner.SimulationTime;
    ShouldRespawn = false;
    CanRespawn = false;
    
    // Tüm clientlere bildir
    NotifyDeathToAllClientsRPC();
}

// YENİ - Tüm clientlere death bildirimi
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void NotifyDeathToAllClientsRPC()
{
    // Tüm clientlerde death process'i çalıştır
    ProcessDeath();
}

private void ProcessDeath()
{
    if (deathProcessed) return;
    deathProcessed = true;
    
    string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";
    
    // Character4D death animasyonu
    var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
    if (character4D != null)
    {
        // NetworkCharacterState KALDIRILDI - AnimationManager direkt kullan
        if (character4D.AnimationManager != null)
        {
            character4D.AnimationManager.Die();
        }
    }
    
    // Kontrolleri kapat
    DisableAllControls();
    
    // UI göster (sadece local player)
    if (Object.HasInputAuthority && DeathUI.Instance != null)
    {
        DeathUI.Instance.ShowDeathUI(respawnDelay, this);
    }
}

private void ProcessRespawn()
{
    if (!deathProcessed) return;
    
    string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";
    
    // Server state'i sıfırlar VE HP'yi restore eder
    if (Runner.IsServer)
    {
        IsDead = false;
        ShouldRespawn = false;
        CanRespawn = false;
        DeathTime = 0;
        
        // SERVER'DA HP RESTORE ET
        if (playerStats != null)
        {
            float targetHP = playerStats.GetNetworkMaxHP() * 0.5f;
            playerStats.SetHealthOnServer(targetHP);
        }
        
        // TÜM CLIENT'LARA RESPAWN TAMAMLANDI BILDIR
        NotifyRespawnCompleteRPC();
    }
    
}


[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void NotifyRespawnCompleteRPC()
{
    string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";
    
    // Reset flags (tüm client'larda)
    deathProcessed = false;
    
    // Position reset (tüm client'larda)
    Vector3 oldPos = transform.position;
    transform.position = spawnPosition;
    
    // Kontrolleri aç (tüm client'larda)
    EnableAllControls();
    
    // UI kapat (sadece input authority sahibi client'da)
    if (Object.HasInputAuthority && DeathUI.Instance != null)
    {
        DeathUI.Instance.ForceHideDeathUI();
    }
    
    // Character state (tüm client'larda)
    var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
    if (character4D != null)
    {
        // NetworkCharacterState KALDIRILDI - AnimationManager direkt kullan
        if (character4D.AnimationManager != null)
        {
            character4D.AnimationManager.SetState(CharacterState.Idle);
        }
    }
    
    // PlayerStats event'ini tetikle
    if (playerStats != null)
    {
        playerStats.TriggerReviveEvent();
    }
}

private void DisableAllControls()
{
    // Network movement'ı durdur
    if (networkController != null)
    {
        networkController.SetMovementEnabled(false);
    }
    
    // Physics'i durdur
    var collider = GetComponent<Collider2D>();
    if (collider != null)
        collider.enabled = false;
    
    var rb = GetComponent<Rigidbody2D>();
    if (rb != null)
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
    }
    
    // PlayerController'ı da bilgilendir
    var playerController = GetComponent<PlayerController>();
    if (playerController != null)
    {
        // PlayerController'da ek flag set edebiliriz
    }
}

private void EnableAllControls()
{
    // Network movement'ı aç
    if (networkController != null)
    {
        networkController.SetMovementEnabled(true);
    }
    
    // Physics'i aç
    var collider = GetComponent<Collider2D>();
    if (collider != null)
        collider.enabled = true;
    
    var rb = GetComponent<Rigidbody2D>();
    if (rb != null)
        rb.bodyType = RigidbodyType2D.Dynamic;
    }


    public void SetSpawnPosition(Vector3 newSpawnPos)
    {
        spawnPosition = newSpawnPos;
    }
}