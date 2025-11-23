using UnityEngine;
using Fusion;

public class BindstoneInteraction : NetworkBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRadius = 3f;
    [SerializeField] private Vector2 centerOffset = Vector2.zero;
    
[Header("Channelling VFX")]
[SerializeField] private GameObject bindstonevfx; // DEĞİŞTİR - NetworkObject yerine GameObject
[Networked] private PlayerRef CurrentChannellingPlayer { get; set; }
// CurrentChannellingAura satırını SİL
[Header("Glow Effect")]
[SerializeField] private GameObject glowObject;
private bool lastGlowState = false;
    [Header("Spawn Settings")]
    [SerializeField] private float spawnRadius = 5f;
    
    private GameObject cachedLocalPlayer;
    private float lastPlayerSearchTime;
    private const float PLAYER_SEARCH_INTERVAL = 1f;
    private bool isInInteractionRange = false;
    
    private void Update()
    {
        if (Runner == null || !Runner.IsRunning) return;
        
        float currentTime = Time.time;
        
        if (cachedLocalPlayer == null || currentTime - lastPlayerSearchTime > PLAYER_SEARCH_INTERVAL)
        {
            FindAndCacheLocalPlayer();
            lastPlayerSearchTime = currentTime;
        }
        
        if (cachedLocalPlayer == null) return;
        
        CheckInteractionRange();
    }
    public override void Spawned()
{
    base.Spawned();
    
    // Glow referansını bul
    if (glowObject == null)
    {
        Transform glowTransform = transform.Find("Glow");
        if (glowTransform != null)
        {
            glowObject = glowTransform.gameObject;
        }
    }
    
    // Başlangıçta kapat
    if (glowObject != null)
    {
        glowObject.SetActive(false);
        lastGlowState = false;
    }
}
    public override void Render()
    {
        base.Render();

        if (glowObject == null) return;

        // Sadece local player için kontrol et
        bool shouldGlow = IsSelectedByLocalPlayer();

        if (shouldGlow != lastGlowState)
        {
            glowObject.SetActive(shouldGlow);
            lastGlowState = shouldGlow;
        }
    }
private bool IsSelectedByLocalPlayer()
{
    if (BindstoneManager.Instance == null) return false;
    if (Runner == null || !Runner.IsRunning) return false;
    
    PlayerRef localPlayer = Runner.LocalPlayer;
    if (localPlayer == PlayerRef.None) return false;
    
    // Local player'ın seçili bindstone'unu kontrol et
    if (BindstoneManager.Instance.TryGetPlayerSelectedBindstone(localPlayer, out NetworkId selectedId))
    {
        return selectedId == Object.Id;
    }
    
    return false;
}
    private void FindAndCacheLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
            {
                cachedLocalPlayer = player;
                return;
            }
        }
        
        cachedLocalPlayer = null;
    }
    
    private void CheckInteractionRange()
    {
        Vector2 bindstoneCenter = (Vector2)transform.position + centerOffset;
        float distance = Vector2.Distance(bindstoneCenter, cachedLocalPlayer.transform.position);
        bool shouldBeInRange = distance <= interactionRadius;
        
        if (shouldBeInRange != isInInteractionRange)
        {
            isInInteractionRange = shouldBeInRange;
            
            if (isInInteractionRange)
            {
                NotifyPlayerInRange();
            }
            else
            {
                NotifyPlayerOutOfRange();
            }
        }
    }
    
    private void NotifyPlayerInRange()
    {
        if (CombatInitializer.Instance != null)
        {
            CombatInitializer.Instance.SetNearbyBindstone(this);
        }
    }
    
    private void NotifyPlayerOutOfRange()
    {
        if (CombatInitializer.Instance != null)
        {
            CombatInitializer.Instance.RemoveNearbyBindstone(this);
        }
    }
    
public void StartChannellingVFX(PlayerRef player)
{
    if (!Object.HasStateAuthority) return;
    
    StopChannellingVFX();
    CurrentChannellingPlayer = player;
    
    RPC_SetVFXActive(true);
}

public void StopChannellingVFX()
{
    if (!Object.HasStateAuthority) return;
    
    CurrentChannellingPlayer = PlayerRef.None;
    RPC_SetVFXActive(false);
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_SetVFXActive(bool active)
{
    if (bindstonevfx != null)
    {
        bindstonevfx.SetActive(active);
    }
}
// DEĞİŞTİR: Mevcut StartBindChannelling metodu
public void StartBindChannelling()
{
    if (cachedLocalPlayer == null) return;
    
    NetworkObject netObj = cachedLocalPlayer.GetComponent<NetworkObject>();
    if (netObj == null || !netObj.HasInputAuthority) return;
    
    PlayerController playerController = cachedLocalPlayer.GetComponent<PlayerController>();
    if (playerController == null) return;
    
    if (playerController.IsCurrentlyChannelling()) return;
    
    playerController.StartBindstoneChannelling(transform.position, this);
}
    
    public Vector2 GetRandomSpawnPosition()
    {
        float randomAngle = UnityEngine.Random.Range(0f, 360f);
        float randomDistance = UnityEngine.Random.Range(0f, spawnRadius);
        Vector2 offset = new Vector2(
            Mathf.Cos(randomAngle * Mathf.Deg2Rad) * randomDistance,
            Mathf.Sin(randomAngle * Mathf.Deg2Rad) * randomDistance
        );
        return (Vector2)transform.position + offset;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector2 center = (Vector2)transform.position + centerOffset;
        Gizmos.DrawWireSphere(center, interactionRadius);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}