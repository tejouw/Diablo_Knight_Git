//YENI
// Path: Assets/Game/Scripts/NetworkCharacterController.cs

using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;
using System.Collections;

public class NetworkCharacterController : NetworkBehaviour
{
    #region CORE COMPONENTS & CONFIGURATION
    private NetworkRigidbody2D networkRigidbody;
    private PlayerStats playerStats;
    
    [Header("Bot Settings")]
    public bool isBot = false; // Bot flag'i ekle
    [Header("Teleport Settings")]
[Networked] public bool IsTeleporting { get; set; }
[Networked] private float TeleportStartTime { get; set; }
[Networked] private Vector2 TeleportTargetPosition { get; set; }
private const float TELEPORT_DISABLE_DURATION = 0.2f; // Teleport sonrasÄ± 1 saniye input disable
    private bool movementEnabled = true;
    private bool remoteIsMoving = false;
    #endregion

    #region NETWORK PROPERTIES
    [Networked] public bool NetworkMovementEnabled { get; set; }
    [Networked] public Vector2 NetworkVelocity { get; set; }
    #endregion

    #region PROPERTIES
    public bool IsMovementEnabled => movementEnabled;
    public bool RemoteMovementState => remoteIsMoving;
    #endregion

    #region MOVEMENT SYSTEM
public void SetMovementEnabled(bool enabled)
{
    movementEnabled = enabled;
    
    // Sadece State Authority networked property'yi deÄŸiÅŸtirebilir
    if (Object.HasStateAuthority)
    {
        NetworkMovementEnabled = enabled;
    }
    else if (Object.HasInputAuthority)
    {
        // Client'dan server'a istek gÃ¶nder
        SetMovementEnabledRPC(enabled);
    }
}

[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void SetMovementEnabledRPC(bool enabled)
{
    NetworkMovementEnabled = enabled;
}

    public void ForceMoveToPosition(Vector2 position)
    {
        if (!Object.HasInputAuthority) return;

        StartCoroutine(TeleportSequence(position));
    }

    private IEnumerator TeleportSequence(Vector2 position)
    {
        SetMovementEnabled(false);

        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }

        // NetworkRigidbody2D ile teleport
        if (networkRigidbody != null)
        {
            networkRigidbody.Teleport(position, transform.rotation);
            NetworkVelocity = Vector2.zero;
        }

        yield return new WaitForSeconds(0.1f);

        if (playerCollider != null)
        {
            playerCollider.enabled = true;
        }

        SetMovementEnabled(true);
    }
    #endregion
// Mevcut ForceMoveToPosition metodunu kaldÄ±r, yerine ÅŸunu ekle:
[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
public void RequestTeleportRPC(Vector2 position)
{
    if (!Runner.IsServer) return;
    
    // Teleport state'ini set et
    IsTeleporting = true;
    TeleportStartTime = (float)Runner.SimulationTime;
    TeleportTargetPosition = position;
    
    StartCoroutine(ServerTeleportSequence(position));
}

private IEnumerator ServerTeleportSequence(Vector2 position)
{
    NetworkMovementEnabled = false;

    NetworkVelocity = Vector2.zero;
    if (networkRigidbody != null && networkRigidbody.Rigidbody != null)
    {
        networkRigidbody.Rigidbody.linearVelocity = Vector2.zero;
        networkRigidbody.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
    }

    Collider2D playerCollider = GetComponent<Collider2D>();
    if (playerCollider != null)
    {
        playerCollider.enabled = false;
    }

    yield return new WaitForFixedUpdate();
    yield return new WaitForFixedUpdate();

    transform.position = new Vector3(position.x, position.y, transform.position.z);

    NetworkTransform netTransform = GetComponent<NetworkTransform>();
    if (netTransform != null)
    {
        netTransform.Teleport(transform.position, transform.rotation);
    }

    // DEĞİŞTİR: 0.3s -> 0.1s
    yield return new WaitForSeconds(0.1f);

    if (networkRigidbody != null && networkRigidbody.Rigidbody != null)
    {
        networkRigidbody.Rigidbody.bodyType = RigidbodyType2D.Dynamic;
        networkRigidbody.Rigidbody.linearVelocity = Vector2.zero;
    }

    if (playerCollider != null)
    {
        playerCollider.enabled = true;
    }

    yield return new WaitForFixedUpdate();

    NotifyTeleportCompleteRPC(position);
}
// DEĞİŞTİR - Reflection kaldır
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void NotifyTeleportCompleteRPC(Vector2 finalPosition)
    {

    // Tüm client'larda pozisyonu zorla set et
    transform.position = new Vector3(finalPosition.x, finalPosition.y, transform.position.z);

    // Teleport cleanup
    if (networkRigidbody != null && networkRigidbody.Rigidbody != null)
    {
        networkRigidbody.Rigidbody.linearVelocity = Vector2.zero;
    }

    // PlayerController'a bildir
    PlayerController playerController = GetComponent<PlayerController>();
    if (playerController != null)
    {
        playerController.OnTeleportComplete();
        
        // DEĞİŞTİ: Reflection yerine direkt çağrı
        if (Object.HasStateAuthority)
        {
            playerController.SpawnCompletionPortal(finalPosition);
        }
    }

    // Delayed movement enable
    StartCoroutine(DelayedMovementEnable());
}
private IEnumerator DelayedMovementEnable()
{
    // DEĞİŞTİR: 1s -> 0.2s
    yield return new WaitForSeconds(TELEPORT_DISABLE_DURATION);
    
    if (Runner.IsServer)
    {
        NetworkMovementEnabled = true;
        IsTeleporting = false;
    }
}


    #region NETWORK SYNCHRONIZATION
    
    private float lastDebugTime = 0f;
public override void FixedUpdateNetwork()
{
    // Debug için timer
    if (Time.time - lastDebugTime >= 1f)
    {
        lastDebugTime = Time.time;
    }

    if (isBot)
    {
        // Bot hareket sistemi - BotController'dan velocity alınacak
        return;
    }
    
    // ✅ CRITICAL FIX: Server (StateAuthority) animation state'i günceller
    if (GetInput<PlayerNetworkInput>(out var input))
    {
        // ✅ NetworkMoveInput'u SERVER set eder (animation sync için)
        var playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.NetworkMoveInput = input.MovementInput;
        }

        if (input.MovementInput.magnitude > 0.1f)
        {
            Vector2 normalizedInput = input.MovementInput.normalized;
            float currentSpeed = playerStats?.MoveSpeed ?? 5f;

            Vector2 velocity = normalizedInput * currentSpeed;
            NetworkVelocity = velocity;

            // NetworkRigidbody2D ile velocity set et
            if (networkRigidbody != null && networkRigidbody.Rigidbody != null)
            {
                networkRigidbody.Rigidbody.linearVelocity = velocity;
            }
        }
        else
        {
            NetworkVelocity = Vector2.zero;
            if (networkRigidbody != null && networkRigidbody.Rigidbody != null)
            {
                networkRigidbody.Rigidbody.linearVelocity = Vector2.zero;
            }
        }
    }
}
// RPC olmadan server'ın direkt teleport başlatması için
// ServerInitiateTeleport metodunun BAŞINA ekle
public void ServerInitiateTeleport(Vector2 position)
{
    if (!Object.HasStateAuthority) return;
    
    
    IsTeleporting = true;
    TeleportStartTime = (float)Runner.SimulationTime;
    TeleportTargetPosition = position;
    
    StartCoroutine(ServerTeleportSequence(position));
}
public override void Spawned()
{
    if (Object.HasInputAuthority)
    {
        movementEnabled = true;
        RequestEnableMovementRPC();
        
        // EKLE: Client prediction için player physics'ini aktif tut
        if (networkRigidbody?.Rigidbody != null)
        {
            networkRigidbody.Rigidbody.simulated = true; // Player physics client'da açık
        }
    }
    else if (Object.HasStateAuthority)
    {
        NetworkMovementEnabled = true;
    }

    // NetworkRigidbody2D ayarları (değişiklik yok)
    if (networkRigidbody?.Rigidbody != null)
    {
        networkRigidbody.Rigidbody.bodyType = RigidbodyType2D.Dynamic;
        networkRigidbody.Rigidbody.freezeRotation = true;
        networkRigidbody.Rigidbody.linearDamping = 5f;
        networkRigidbody.Rigidbody.gravityScale = 0f;
    }
}
[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void RequestEnableMovementRPC()
{
    NetworkMovementEnabled = true;
}
    #endregion

    #region UNITY LIFECYCLE
private void Awake()
{
    networkRigidbody = GetComponent<NetworkRigidbody2D>();
    playerStats = GetComponent<PlayerStats>();

    if (networkRigidbody == null)
    {
        return;
    }
    // Rigidbody2D ayarlarÄ±
    if (networkRigidbody.Rigidbody != null)
    {
        networkRigidbody.Rigidbody.bodyType = RigidbodyType2D.Dynamic;
        networkRigidbody.Rigidbody.freezeRotation = true;
        networkRigidbody.Rigidbody.linearDamping = 5f;
        networkRigidbody.Rigidbody.gravityScale = 0f;
        
    }

}
    #endregion
}