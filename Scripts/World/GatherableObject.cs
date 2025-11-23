using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public class GatherableObject : NetworkBehaviour
{
    #region Inspector Settings
    [Header("Gatherable Settings")]
    [SerializeField] private string itemId = ""; // Inspector'dan atanacak
    [SerializeField] private int itemAmount = 1;
    [SerializeField] private float interactionRange = 2f;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private GameObject glowEffect;
    #endregion

    #region Network Properties
    [Networked] public NetworkBool IsAvailable { get; set; }
    [Networked] public NetworkId CurrentGathererId { get; set; }
    [Networked] public TickTimer GatheringTimer { get; set; }
    [Networked] public Vector3 SpawnPosition { get; set; }
    #endregion

    #region Private Fields
    private GatheringSpawner spawner;
    private string assignedPointName;
    private const float GATHERING_DURATION = 2f; // 2 saniye toplama süresi
    private Dictionary<NetworkId, float> activeGatherers = new Dictionary<NetworkId, float>();
    #endregion

    #region Initialization
    public void Initialize(GatheringSpawner spawnerRef, string pointName, Vector3 position)
    {
        spawner = spawnerRef;
        assignedPointName = pointName;
        IsAvailable = true;
        CurrentGathererId = default;
        SpawnPosition = position;

        // CRITICAL: Pozisyonu hemen uygula
        transform.position = position;
    }

    public override void Spawned()
    {
        base.Spawned();

        IsAvailable = true;
        CurrentGathererId = default;

        // CRITICAL: NetworkTransform devre dışı bırak (pozisyonu kendimiz yönetiyoruz)
        NetworkTransform netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null)
        {
            netTransform.enabled = false;
        }

        // Networked pozisyonu uygula
        if (SpawnPosition != Vector3.zero)
        {
            transform.position = SpawnPosition;
        }
        else
        {
        }

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (glowEffect != null)
            glowEffect.SetActive(IsAvailable);
    }
    #endregion

    #region Fusion Lifecycle
    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer) return;

        // Aktif gathering varsa timer'ı kontrol et
        if (CurrentGathererId != default && !GatheringTimer.ExpiredOrNotRunning(Runner))
        {
            // Gathering devam ediyor, player hala yakında mı kontrol et
            if (!ValidateGathererProximity(CurrentGathererId))
            {
                // Player uzaklaştı, iptal et
                CancelGathering(CurrentGathererId);
            }
        }
        else if (CurrentGathererId != default && GatheringTimer.ExpiredOrNotRunning(Runner))
        {
            // Gathering tamamlandı
            CompleteGathering(CurrentGathererId);
        }
    }
    #endregion

    #region Server Methods (Called from PlayerController RPC)
    /// <summary>
    /// Server-only: Start gathering process. Called directly from PlayerController's server RPC.
    /// </summary>
    public void ServerStartGathering(NetworkId playerId, PlayerRef playerRef)
    {
        if (!Runner.IsServer)
        {
            Debug.LogError("[GatherableObject] ServerStartGathering called on client!");
            return;
        }

        // Validation
        if (!IsAvailable)
        {
            RPC_NotifyGatheringFailed(playerRef, "Bu kaynak zaten toplanıyor.");
            return;
        }

        if (CurrentGathererId != default)
        {
            RPC_NotifyGatheringFailed(playerRef, "Başka bir oyuncu topluyor.");
            return;
        }

        // Player proximity check
        if (!ValidateGathererProximity(playerId))
        {
            RPC_NotifyGatheringFailed(playerRef, "Kaynağa çok uzaksınız.");
            return;
        }

        // Gathering başlat
        CurrentGathererId = playerId;
        GatheringTimer = TickTimer.CreateFromSeconds(Runner, GATHERING_DURATION);


        // Tüm clientlara bildir
        RPC_NotifyGatheringStarted(playerId);
    }

    /// <summary>
    /// Server-only: Cancel gathering process. Called directly from PlayerController's server RPC.
    /// </summary>
    public void ServerCancelGathering(NetworkId playerId)
    {
        if (!Runner.IsServer) return;

        if (CurrentGathererId == playerId)
        {
            CancelGathering(playerId);
        }
    }
    #endregion

    #region Client RPCs
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyGatheringStarted(NetworkId gathererId)
    {
        // VFX/SFX başlat (opsiyonel)
        if (glowEffect != null)
            glowEffect.SetActive(true);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyGatheringCompleted(NetworkId gathererId, bool success)
    {
        if (success)
        {
            // Başarılı toplama VFX
            if (spriteRenderer != null)
            {
                Color col = spriteRenderer.color;
                col.a = 0.3f;
                spriteRenderer.color = col;
            }

            if (glowEffect != null)
                glowEffect.SetActive(false);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyGatheringCancelled(NetworkId gathererId)
    {
        // İptal VFX (opsiyonel)
        if (glowEffect != null)
            glowEffect.SetActive(IsAvailable);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_NotifyGatheringFailed(PlayerRef player, string reason)
    {
        // UI notification gösterebilirsin
    }
    #endregion

    #region Server Logic
    private void CompleteGathering(NetworkId gathererId)
    {
        if (!Runner.IsServer) return;


        // Player'ı bul
        NetworkObject gathererObj = Runner.FindObject(gathererId);
        if (gathererObj == null)
        {
            Debug.LogError($"[GatherableObject] Gatherer NetworkObject not found: {gathererId}");
            CancelGathering(gathererId);
            return;
        }


        // Inventory'ye item ekle
        InventorySystem inventory = gathererObj.GetComponent<InventorySystem>();
        if (inventory != null && !string.IsNullOrEmpty(itemId))
        {

            if (ItemDatabase.Instance == null)
            {
                Debug.LogError($"[GatherableObject] ItemDatabase.Instance is NULL!");
                CancelGathering(gathererId);
                return;
            }

            ItemData itemData = ItemDatabase.Instance.GetItemById(itemId);
            if (itemData != null)
            {
                bool added = inventory.TryAddItem(itemData, itemAmount, false);

                if (added)
                {
                    // Başarılı, objeyi despawn et
                    RPC_NotifyGatheringCompleted(gathererId, true);
                    DespawnGatherable();
                }
                else
                {
                    // Envanter dolu
                    RPC_NotifyGatheringFailed(gathererObj.InputAuthority, "Envanter dolu!");
                    CancelGathering(gathererId);
                }
            }
            else
            {
                Debug.LogError($"[GatherableObject] ItemData not found for ID: {itemId}");
                CancelGathering(gathererId);
            }
        }
        else
        {
            Debug.LogError($"[GatherableObject] InventorySystem: {(inventory != null ? "Found" : "NULL")}, ItemId: '{itemId}'");
            CancelGathering(gathererId);
        }
    }

    private void CancelGathering(NetworkId gathererId)
    {
        if (!Runner.IsServer) return;

        CurrentGathererId = default;
        GatheringTimer = TickTimer.None;

        RPC_NotifyGatheringCancelled(gathererId);
    }

    private void DespawnGatherable()
    {
        if (!Runner.IsServer) return;

        IsAvailable = false;
        CurrentGathererId = default;

        // Spawner'a bildir
        if (spawner != null)
        {
            spawner.MarkAsGathered(Object.Id, assignedPointName);
            spawner.UnregisterGatherable(Object.Id);
        }

        // Despawn
        Runner.Despawn(Object);
    }

    private bool ValidateGathererProximity(NetworkId gathererId)
    {
        if (!Runner.IsServer) return false;

        NetworkObject gathererObj = Runner.FindObject(gathererId);
        if (gathererObj == null) return false;

        float distance = Vector2.Distance(transform.position, gathererObj.transform.position);
        return distance <= interactionRange;
    }
    #endregion

    #region Public API
    public bool CanBeGathered()
    {
        return IsAvailable && CurrentGathererId == default;
    }

    public float GetGatheringProgress()
    {
        if (CurrentGathererId == default || GatheringTimer.IsRunning == false)
            return 0f;

        float elapsed = GATHERING_DURATION - (float)GatheringTimer.RemainingTime(Runner);
        return Mathf.Clamp01(elapsed / GATHERING_DURATION);
    }

    public bool IsBeingGatheredBy(NetworkId playerId)
    {
        return CurrentGathererId == playerId;
    }

    public string GetItemId() => itemId;
    public float GetInteractionRange() => interactionRange;
    #endregion

    #region Proximity Detection (Called by CombatInitializer)
    private void Update()
    {
        // FIXED: Her client kendi local player'ını kontrol etmeli
        // HasInputAuthority gatherable için değil, local player için check edilmeli!

        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer == null) return;

        float distance = Vector2.Distance(transform.position, localPlayer.transform.position);

        if (distance <= interactionRange && IsAvailable)
        {
            // Notify CombatInitializer
            if (CombatInitializer.Instance != null)
            {
                CombatInitializer.Instance.SetNearbyGatherable(this);
            }
        }
        else
        {
            // Remove from CombatInitializer
            if (CombatInitializer.Instance != null)
            {
                CombatInitializer.Instance.RemoveNearbyGatherable(this);
            }
        }
    }

    private GameObject FindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
                return player;
        }
        return null;
    }

    private void OnDestroy()
    {
        // Cleanup
        if (CombatInitializer.Instance != null)
        {
            CombatInitializer.Instance.RemoveNearbyGatherable(this);
        }
    }
    #endregion

    #region Editor Utilities
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
    #endregion
}
