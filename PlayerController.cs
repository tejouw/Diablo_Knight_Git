// Path: Assets/Game/Scripts/PlayerController.cs
using UnityEngine;
using Fusion;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using DuloGames.UI;
using System.Collections;

public class PlayerController : NetworkBehaviour, IInterestEnter, IInterestExit
{
    [Header("Teleport Channelling")]
    [Networked] private bool IsChannellingTeleport { get; set; }
    [Networked] private TickTimer ChannellingTimer { get; set; }
    [Networked] private Vector2 ChannellingTargetPosition { get; set; }
    private const float CHANNELLING_DURATION = 2f;

    [Header("Teleport Target Settings")]
    [SerializeField] private Vector2 teleportTargetCenter = Vector2.zero;
    [SerializeField] private float teleportSpawnRadius = 5f;

    [Header("Teleport Visuals")]
    [SerializeField] private NetworkObject channellingPortalPrefab;
    [SerializeField] private NetworkObject completionPortalPrefab;
    private NetworkObject currentChannellingPortal;
    private Vector2 lastChannellingPosition;
    private const float POSITION_CHANGE_THRESHOLD = 0.1f;
    private bool isTeleportCooldown = false;

[Header("Channelling UI")]
private TeleportChannellingUI channelingUI;
private BindstoneChannellingUI bindstoneChannelingUI;
private GatheringChannellingUI gatheringChannelingUI;

    [Header("Bindstone Channelling")]
[Networked] private bool IsChannellingBindstone { get; set; }
[Networked] private TickTimer BindstoneChannellingTimer { get; set; }
[Networked] private Vector2 BindstonePosition { get; set; }
private const float BINDSTONE_CHANNELLING_DURATION = 3f;
private Vector2 lastBindstoneChannellingPosition;

    [Header("Gathering Channelling")]
[Networked] private bool IsChannellingGathering { get; set; }
[Networked] private TickTimer GatheringChannellingTimer { get; set; }
[Networked] private NetworkId CurrentGatherableId { get; set; }
private const float GATHERING_CHANNELLING_DURATION = 2f;
private Vector2 lastGatheringChannellingPosition;

    private UIJoystick uiJoystick;
    private Vector2 moveInput;
    private bool isMoving = false;
    private NetworkCharacterController networkController;
    private PlayerStats playerStats;
    private WeaponSystem weaponSystem;
    private Character4D character4D;
    private bool wasMoving = false;
    private bool isDamageTaken = false;
    private Vector2 lastRealMovementInput = Vector2.zero;
    private SkillSystem skillSystem;

    [Networked] public Vector2 NetworkMoveInput { get; set; }
    [Networked] public bool NetworkIsDead { get; set; }
    [Networked] public float LastMovementCheck { get; set; }
    [Networked] public bool LastMovingState { get; set; }
    [Networked] public int NetworkCharacterState { get; set; }

    [Header("AOI Settings")]
    [SerializeField] private float aoiRadius = 25f;
    [SerializeField] private float aoiUpdateInterval = 1f;
    private float lastAOIUpdate = 0f;
    private bool isVisibleToLocalPlayer = false;

    private void Start()
    {
        if (character4D == null)
        {
            character4D = GetComponent<Character4D>();
            if (character4D == null) Debug.LogError("[PlayerController] Character4D reference is missing!");
        }
    }

    public override void Spawned()
    {
        if (Object.HasInputAuthority) isVisibleToLocalPlayer = true;
        if (character4D == null) character4D = GetComponent<Character4D>();
        InterestEnter(Runner.LocalPlayer);
    }

private void Awake()
{
    uiJoystick = FindFirstObjectByType<UIJoystick>();
    weaponSystem = GetComponent<WeaponSystem>();
    playerStats = GetComponent<PlayerStats>();
    networkController = GetComponent<NetworkCharacterController>();
    character4D = GetComponent<Character4D>();
    skillSystem = GetComponent<SkillSystem>();
    if (playerStats != null) playerStats.OnHealthChanged += HandleHealthChanged;
    
    // Teleport UI
    if (TeleportChannellingUI.Instance == null)
    {
        GameObject teleportUIObj = new GameObject("TeleportChannellingUI");
        teleportUIObj.AddComponent<TeleportChannellingUI>();
    }
    channelingUI = TeleportChannellingUI.Instance;
    channelingUI.SetUIScale(0.5f);
    
    // Bindstone UI
    if (BindstoneChannellingUI.Instance == null)
    {
        GameObject bindstoneUIObj = new GameObject("BindstoneChannellingUI");
        bindstoneUIObj.AddComponent<BindstoneChannellingUI>();
    }
    bindstoneChannelingUI = BindstoneChannellingUI.Instance;
    bindstoneChannelingUI.SetUIScale(0.5f);

    // Gathering UI
    if (GatheringChannellingUI.Instance == null)
    {
        GameObject gatheringUIObj = new GameObject("GatheringChannellingUI");
        gatheringUIObj.AddComponent<GatheringChannellingUI>();
    }
    gatheringChannelingUI = GatheringChannellingUI.Instance;
    gatheringChannelingUI.SetUIScale(0.5f);
}

    private void HandleSkillInput()
    {
        if (!Object.HasInputAuthority) return;
        var deathSystem = GetComponent<DeathSystem>();
        if (deathSystem != null && deathSystem.IsDead) return;
        if (skillSystem == null) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) skillSystem.UseSkill1();
        else if (Input.GetKeyDown(KeyCode.Alpha2)) skillSystem.UseSkill2();
        else if (Input.GetKeyDown(KeyCode.Alpha3)) skillSystem.UseSkill3();
    }

    public void OnDamageTaken()
    {
        isDamageTaken = true;
    }

    public void TriggerHitAnimationFromServer()
    {
        if (!Runner.IsServer) return;
        SyncDamageTakenFromServerRPC();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SyncDamageTakenFromServerRPC()
    {
        if (character4D != null) character4D.AnimationManager.Hit();
    }

    private void OnDestroy()
    {
        if (playerStats != null) playerStats.OnHealthChanged -= HandleHealthChanged;
    }

    public void OnTeleportComplete()
    {
        moveInput = Vector2.zero;
        lastRealMovementInput = Vector2.zero;
        isMoving = false;
        isTeleportCooldown = true;
        StartCoroutine(TeleportCooldown());
        if (character4D != null) character4D.AnimationManager.SetState(CharacterState.Idle);
    }

    private IEnumerator TeleportCooldown()
    {
        yield return new WaitForSeconds(0.2f);
        isTeleportCooldown = false;
    }

private void HandleHealthChanged(float currentHealth)
{
    var deathSystem = GetComponent<DeathSystem>();
    if (deathSystem != null && deathSystem.IsDead) return;
    
    // SADECE ANIMASYON KONTROLÜ - NetworkIsDead'i kaldır
    if (currentHealth <= 0)
    {
        // Death sistemi bunu halleder, burada sadece animasyon
        if (character4D != null && Object.HasInputAuthority) 
        {
            character4D.AnimationManager.Die();
        }
    }
    else if (currentHealth > 0)
    {
        if (character4D != null && Object.HasInputAuthority) 
        {
            character4D.AnimationManager.SetState(CharacterState.Idle);
        }
    }
}

    public override void FixedUpdateNetwork()
    {
        if (Runner.IsServer && IsChannellingGathering)
        {
            if (HasPlayerMovedDuringGatheringChannelling())
            {
                CancelGatheringChannelling();
                return;
            }
            if (GatheringChannellingTimer.Expired(Runner))
            {
                CompleteGatheringChannelling();
                return;
            }
        }

        if (Runner.IsServer && IsChannellingBindstone)
        {
            if (HasPlayerMovedDuringBindstoneChannelling())
            {
                // VFX'i durdur
                StopBindstoneChannellingVFX();

                IsChannellingBindstone = false;
                StopDanceAnimation();
                RPC_HideBindstoneChannellingUI();
                return;
            }
            if (BindstoneChannellingTimer.Expired(Runner))
            {
                CompleteBindstoneChannelling();
                return;
            }
        }
        if (Runner.IsServer && IsChannellingTeleport)
        {
            if (HasPlayerMovedDuringChannelling())
            {
                IsChannellingTeleport = false;
                DespawnChannellingPortal();
                StopDanceAnimation();
                RPC_HideChannellingUI();
                return;
            }
            if (ChannellingTimer.Expired(Runner))
            {
                IsChannellingTeleport = false;
                DespawnChannellingPortal();
                StopDanceAnimation();
                RPC_HideChannellingUI();
                SpawnCompletionPortal(lastChannellingPosition);
                NetworkCharacterController netController = GetComponent<NetworkCharacterController>();
                if (netController != null) netController.ServerInitiateTeleport(ChannellingTargetPosition);
                return;
            }
        }
 var deathSystem = GetComponent<DeathSystem>();
    if (!Object.HasInputAuthority || (deathSystem != null && deathSystem.IsDead)) return;
        
        if (IsChannellingTeleport)
        {
            if (moveInput.magnitude > 0.1f) CancelChannelling();
            moveInput = Vector2.zero;
            lastRealMovementInput = Vector2.zero;
            isMoving = false;
            return;
        }
        HandleSkillInput();
        HandleMovementInput();
        UpdateCharacterState();
        if (!IsChannellingTeleport && character4D != null)
        {
            CharacterState currentState = (CharacterState)character4D.AnimationManager.Animator.GetInteger("State");
            if (currentState != CharacterState.Dance && character4D.Front.Expression == "Happy")
            {
                character4D.SetExpression("Default");
            }
        }
        PeriodicMovementCheck();
        if (character4D != null && moveInput.magnitude > 0.1f)
        {
            Vector2 direction = CalculateDirection(moveInput);
            character4D.SetDirection(direction);
            SyncDirectionRPC(direction);
        }
        if (Object.HasInputAuthority && Time.time - lastAOIUpdate >= aoiUpdateInterval)
        {
            UpdatePlayerAOI();
            lastAOIUpdate = Time.time;
        }

if (Object.HasInputAuthority && 
    Time.time - lastLocationCheckTime >= LOCATION_CHECK_INTERVAL &&
    QuestCompass.Instance != null && 
    QuestCompass.Instance.IsActive)
{
    if (QuestManager.Instance != null)
    {
        QuestManager.Instance.CheckLocationReached(transform.position);
    }
    lastLocationCheckTime = Time.time;
}
    }
    private float lastLocationCheckTime = 0f;
private const float LOCATION_CHECK_INTERVAL = 0.5f;
private void StopBindstoneChannellingVFX()
{
    if (!Runner.IsServer) return;
    
    // Tüm bindstone'ları bul ve player'ın channelling yaptığı bindstone'u tespit et
    var bindstones = FindObjectsByType<BindstoneInteraction>(FindObjectsSortMode.None);
    NetworkObject netObj = GetComponent<NetworkObject>();
    
    foreach (var bindstone in bindstones)
    {
        if (bindstone.Object != null && bindstone.Object.IsValid)
        {
            // Eğer bu bindstone bu player için VFX gösteriyorsa durdur
            bindstone.StopChannellingVFX();
        }
    }
}
    private void StartDanceAnimation()
    {
        if (character4D != null && character4D.AnimationManager != null)
        {
            character4D.SetExpression("Default");
            character4D.AnimationManager.SetState(CharacterState.Dance);
            RPC_SyncDanceAnimation(true);
        }
    }

    private IEnumerator ForceDefaultExpressionAfterDelay()
    {
        yield return new WaitForEndOfFrame();
        if (character4D != null) character4D.SetExpression("Default");
        yield return new WaitForSeconds(0.3f);
        if (character4D != null) character4D.SetExpression("Default");
    }

    private void StopDanceAnimation()
    {
        if (character4D != null && character4D.AnimationManager != null)
        {
            character4D.SetExpression("Default");
            character4D.AnimationManager.SetState(CharacterState.Idle);
            StartCoroutine(ForceDefaultExpressionAfterDelay());
            RPC_SyncDanceAnimation(false);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncDanceAnimation(bool isDancing)
    {
        if (character4D != null && character4D.AnimationManager != null)
        {
            if (isDancing)
            {
                character4D.SetExpression("Default");
                character4D.AnimationManager.SetState(CharacterState.Dance);
            }
            else
            {
                character4D.SetExpression("Default");
                character4D.AnimationManager.SetState(CharacterState.Idle);
                StartCoroutine(ForceDefaultExpressionAfterDelay());
            }
        }
    }

    private void UpdatePlayerAOI()
    {
        if (Object.HasInputAuthority) RequestAOIUpdateRPC(transform.position, aoiRadius);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestAOIUpdateRPC(Vector2 position, float radius)
    {
        if (!Runner.IsServer) return;
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.InputAuthority != PlayerRef.None)
        {
            if (Runner.GameMode != GameMode.Shared) Runner.ClearPlayerAreaOfInterest(netObj.InputAuthority);
            Runner.AddPlayerAreaOfInterest(netObj.InputAuthority, position, radius);
        }
    }

    private void HandleMovementInput()
    {
    if (uiJoystick == null) return;
    if (IsChannellingTeleport || IsChannellingBindstone)
    {
        moveInput = Vector2.zero;
        lastRealMovementInput = Vector2.zero;
        isMoving = false;
        return;
    }
        var deathSystem = GetComponent<DeathSystem>();
        if (deathSystem != null && deathSystem.IsDead)
        {
            moveInput = Vector2.zero;
            lastRealMovementInput = Vector2.zero;
            isMoving = false;
            return;
        }
        if (isTeleportCooldown)
        {
            moveInput = Vector2.zero;
            lastRealMovementInput = Vector2.zero;
            isMoving = false;
            return;
        }
        lastRealMovementInput = uiJoystick.JoystickAxis;
        moveInput = lastRealMovementInput;
        isMoving = moveInput.magnitude > 0.1f;
    }

    public bool IsTeleportCooldown() => isTeleportCooldown;
    public Vector2 GetRealMovementInput() => lastRealMovementInput;

    private void PeriodicMovementCheck()
    {
        if (!Object.HasInputAuthority) return;
        if (Runner.SimulationTime - LastMovementCheck >= 1f)
        {
            LastMovementCheck = Runner.SimulationTime;
            if (!isMoving && LastMovingState)
            {
                if (character4D != null) character4D.AnimationManager.SetState(CharacterState.Idle);
                LastMovingState = false;
            }
            else if (isMoving) LastMovingState = true;
        }
    }

    private Vector2 CalculateDirection(Vector2 input)
    {
        float horizontal = input.x;
        float vertical = input.y;
        if (Mathf.Abs(horizontal) > Mathf.Abs(vertical))
            return horizontal > 0 ? Vector2.right : Vector2.left;
        else
            return vertical > 0 ? Vector2.up : Vector2.down;
    }

public override void Render()
{
    if (Object.HasInputAuthority) return;
    if (!isVisibleToLocalPlayer) return;
    
    // GÜNCELLEME: DeathSystem'den death durumunu al
    var deathSystem = GetComponent<DeathSystem>();
    bool isDead = deathSystem != null && deathSystem.IsDead;
    
    if (character4D != null && character4D.AnimationManager != null)
    {
        if (isDead)
        {
            if (character4D.AnimationManager.Animator.GetInteger("State") != (int)CharacterState.Death)
                character4D.AnimationManager.Die();
            return;
        }
        
        bool isRemoteMoving = NetworkMoveInput.magnitude > 0.1f;
        CharacterState currentState = (CharacterState)character4D.AnimationManager.Animator.GetInteger("State");
        if (isRemoteMoving && currentState != CharacterState.Run)
            character4D.AnimationManager.SetState(CharacterState.Run);
        else if (!isRemoteMoving && currentState != CharacterState.Idle)
            character4D.AnimationManager.SetState(CharacterState.Idle);
    }
}

    public void InterestEnter(PlayerRef player)
    {
        if (Runner.LocalPlayer != player || !Runner.GetVisible()) return;
        isVisibleToLocalPlayer = true;
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer != null) renderer.enabled = true;
        }
        var animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = true;
        EnableNameCanvas();
        if (!Object.HasInputAuthority && character4D != null)
        {
            if (character4D.NetworkDirection != Vector2.zero) character4D.SetDirection(character4D.NetworkDirection);
            if (character4D.AnimationManager != null)
            {
                if (NetworkIsDead) character4D.AnimationManager.Die();
                else
                {
                    bool isRemoteMoving = NetworkMoveInput.magnitude > 0.1f;
                    character4D.AnimationManager.SetState(isRemoteMoving ? CharacterState.Run : CharacterState.Idle);
                }
            }
        }
    }

    public void InterestExit(PlayerRef player)
    {
        if (Runner.LocalPlayer != player || !Runner.GetVisible()) return;
        isVisibleToLocalPlayer = false;
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer != null) renderer.enabled = false;
        }
        var animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        DisableNameCanvas();
    }

    private void EnableNameCanvas()
    {
        Transform nameCanvas = transform.Find("NameCanvas_" + Object.Id.GetHashCode());
        if (nameCanvas != null) nameCanvas.gameObject.SetActive(true);
    }

    private void DisableNameCanvas()
    {
        Transform nameCanvas = transform.Find("NameCanvas_" + Object.Id.GetHashCode());
        if (nameCanvas != null) nameCanvas.gameObject.SetActive(false);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        InterestExit(Runner.LocalPlayer);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void SyncDirectionRPC(Vector2 direction)
    {
        if (!Object.HasInputAuthority && character4D != null) character4D.SetDirection(direction);
    }

    private void UpdateCharacterState()
    {
        if (character4D == null) return;
        if (character4D.AnimationManager.Animator.GetInteger("State") == (int)CharacterState.Death) return;
        if (isMoving)
        {
            character4D.AnimationManager.SetState(CharacterState.Run);
            wasMoving = true;
        }
        else if (wasMoving)
        {
            character4D.AnimationManager.SetState(CharacterState.Idle);
            wasMoving = false;
        }
        if (isDamageTaken)
        {
            character4D.AnimationManager.Hit();
            isDamageTaken = false;
        }
    }

    public Vector2 GetMovementInput() => moveInput;

    public void StartTeleportChannelling()
    {
        if (!Object.HasInputAuthority) return;
        Vector2 targetPos = CalculateRandomTeleportPosition();
        RPC_RequestTeleportChannelling(targetPos);
    }

private Vector2 CalculateRandomTeleportPosition()
{
    if (BindstoneManager.Instance != null)
    {
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && BindstoneManager.Instance.TryGetPlayerBindstone(netObj.InputAuthority, out Vector2 bindstonePos))
        {
            Debug.Log($"[TELEPORT] Bindstone bulundu: {bindstonePos}");
            float randomAngle = UnityEngine.Random.Range(0f, 360f);
            float randomDistance = UnityEngine.Random.Range(0f, 5f);
            Vector2 offset = new Vector2(
                Mathf.Cos(randomAngle * Mathf.Deg2Rad) * randomDistance,
                Mathf.Sin(randomAngle * Mathf.Deg2Rad) * randomDistance
            );
            return bindstonePos + offset;
        }
        else
        {
            Debug.Log("[TELEPORT] Bindstone kaydı yok, default kullanılıyor");
        }
    }
    else
    {
        Debug.LogWarning("[TELEPORT] BindstoneManager henüz hazır değil");
    }
    
    float randomAngle2 = UnityEngine.Random.Range(0f, 360f);
    float randomDistance2 = UnityEngine.Random.Range(0f, teleportSpawnRadius);
    Vector2 offset2 = new Vector2(
        Mathf.Cos(randomAngle2 * Mathf.Deg2Rad) * randomDistance2,
        Mathf.Sin(randomAngle2 * Mathf.Deg2Rad) * randomDistance2
    );
    return teleportTargetCenter + offset2;
}

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestTeleportChannelling(Vector2 targetPos)
    {
        if (!Runner.IsServer) return;
        if (IsChannellingTeleport) return;
        IsChannellingTeleport = true;
        ChannellingTargetPosition = targetPos;
        ChannellingTimer = TickTimer.CreateFromSeconds(Runner, CHANNELLING_DURATION);
        lastChannellingPosition = transform.position;
        SpawnChannellingPortal();
        StartDanceAnimation();
        RPC_ShowChannellingUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowChannellingUI()
    {
        if (channelingUI != null && Object.HasInputAuthority)
        {
            channelingUI.Show();
            channelingUI.StartAutoUpdate(() => GetChannellingProgress());
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideChannellingUI()
    {
        if (channelingUI != null && Object.HasInputAuthority) channelingUI.Hide();
    }

    public void CancelChannelling()
    {
        if (!Object.HasInputAuthority) return;
        if (character4D != null) character4D.SetExpression("Default");
        RPC_CancelChannelling();
        StartCoroutine(ForceDefaultExpressionAfterDelay());
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_CancelChannelling()
    {
        if (!Runner.IsServer) return;
        IsChannellingTeleport = false;
        ChannellingTimer = default;
        DespawnChannellingPortal();
        StopDanceAnimation();
        RPC_HideChannellingUI();
    }

    private void SpawnChannellingPortal()
    {
        if (!Runner.IsServer) return;
        if (channellingPortalPrefab == null) return;
        DespawnChannellingPortal();
        Vector3 playerPos = transform.position;
        currentChannellingPortal = Runner.Spawn(channellingPortalPrefab, playerPos, Quaternion.identity);
        if (currentChannellingPortal != null)
        {
            TeleportPortal portalScript = currentChannellingPortal.GetComponent<TeleportPortal>();
            if (portalScript != null)
            {
                portalScript.Initialize(CHANNELLING_DURATION + 0.5f);
                StartCoroutine(VerifyPortalPosition(currentChannellingPortal, playerPos));
            }
        }
    }

    private void DespawnChannellingPortal()
    {
        if (!Runner.IsServer) return;
        if (currentChannellingPortal != null && currentChannellingPortal.IsValid)
        {
            Runner.Despawn(currentChannellingPortal);
            currentChannellingPortal = null;
        }
    }

    public void SpawnCompletionPortal(Vector2 position)
    {
        if (!Runner.IsServer) return;
        if (completionPortalPrefab == null) return;
        Vector3 targetPos = new Vector3(position.x, position.y, 0f);
        NetworkObject completionPortal = Runner.Spawn(completionPortalPrefab, targetPos, Quaternion.identity);
        if (completionPortal != null)
        {
            TeleportPortal portalScript = completionPortal.GetComponent<TeleportPortal>();
            if (portalScript != null)
            {
                portalScript.Initialize(3f);
                StartCoroutine(VerifyPortalPosition(completionPortal, targetPos));
            }
        }
    }

    private IEnumerator VerifyPortalPosition(NetworkObject portal, Vector3 expectedPos)
    {
        yield return new WaitForEndOfFrame();
        if (portal != null && portal.IsValid)
        {
            Vector3 actualPos = portal.transform.position;
            float distance = Vector3.Distance(actualPos, expectedPos);
            if (distance > 0.1f) portal.transform.position = expectedPos;
        }
    }

    private bool HasPlayerMovedDuringChannelling()
    {
        float distanceMoved = Vector2.Distance(lastChannellingPosition, transform.position);
        bool moved = distanceMoved > POSITION_CHANGE_THRESHOLD;
        if (moved)
        {
            if (character4D != null) character4D.SetExpression("Default");
            StartCoroutine(ForceDefaultExpressionAfterDelay());
        }
        return moved;
    }

public bool IsCurrentlyChannelling() => IsChannellingTeleport || IsChannellingBindstone || IsChannellingGathering;

    public float GetChannellingProgress()
    {
        if (!IsChannellingTeleport) return 0f;
        float remaining = ChannellingTimer.RemainingTime(Runner) ?? 0f;
        return 1f - (remaining / CHANNELLING_DURATION);
    }
public void StartBindstoneChannelling(Vector2 bindstonePos, BindstoneInteraction bindstone)
{
    if (!Object.HasInputAuthority) return;
    RPC_RequestBindstoneChannelling(bindstonePos, bindstone.Object.Id);
}

[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void RPC_RequestBindstoneChannelling(Vector2 bindstonePos, NetworkId bindstoneId)
{
    if (!Runner.IsServer) return;
    if (IsChannellingTeleport || IsChannellingBindstone) return;
    
    // Bindstone'u bul
    var bindstoneObj = Runner.FindObject(bindstoneId);
    if (bindstoneObj == null) return;
    
    var bindstoneInteraction = bindstoneObj.GetComponent<BindstoneInteraction>();
    if (bindstoneInteraction == null) return;
    
    IsChannellingBindstone = true;
    BindstonePosition = bindstonePos;
    BindstoneChannellingTimer = TickTimer.CreateFromSeconds(Runner, BINDSTONE_CHANNELLING_DURATION);
    lastBindstoneChannellingPosition = transform.position;
    
    // VFX başlat
    NetworkObject netObj = GetComponent<NetworkObject>();
    if (netObj != null)
    {
        bindstoneInteraction.StartChannellingVFX(netObj.InputAuthority);
    }
    
    StartDanceAnimation();
    RPC_ShowBindstoneChannellingUI();
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_ShowBindstoneChannellingUI()
{
    if (bindstoneChannelingUI != null && Object.HasInputAuthority) // DEĞİŞTİ
    {
        bindstoneChannelingUI.Show(); // DEĞİŞTİ
        bindstoneChannelingUI.StartAutoUpdate(() => GetBindstoneChannellingProgress()); // DEĞİŞTİ
    }
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_HideBindstoneChannellingUI()
{
    if (bindstoneChannelingUI != null && Object.HasInputAuthority) // DEĞİŞTİ
    {
        bindstoneChannelingUI.Hide(); // DEĞİŞTİ
    }
}

private float GetBindstoneChannellingProgress()
{
    if (!IsChannellingBindstone) return 0f;
    float remaining = BindstoneChannellingTimer.RemainingTime(Runner) ?? 0f;
    return 1f - (remaining / BINDSTONE_CHANNELLING_DURATION);
}

    // DEĞİŞTİR: CompleteBindstoneChannelling metodunu
    private void CompleteBindstoneChannelling()
    {
        // VFX'i durdur
        StopBindstoneChannellingVFX();

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null)
        {
            // Bindstone ID'sini al
            var bindstones = FindObjectsByType<BindstoneInteraction>(FindObjectsSortMode.None);
            NetworkId bindstoneId = default(NetworkId);

            foreach (var bindstone in bindstones)
            {
                if (Vector2.Distance(bindstone.transform.position, BindstonePosition) < 0.1f)
                {
                    bindstoneId = bindstone.Object.Id;
                    break;
                }
            }

            // ✅ DEĞİŞTİ: Coroutine yerine direkt kaydet ve RPC çağır
            if (BindstoneManager.Instance != null)
            {
                BindstoneManager.Instance.RegisterPlayerBindstone(netObj.InputAuthority, BindstonePosition, bindstoneId);
                Debug.Log($"[BINDSTONE] ✓ Kayıt başarılı! Pozisyon: {BindstonePosition}, ID: {bindstoneId}");

                // Client'a quest güncellemesi için RPC gönder
                RPC_NotifyBindstoneQuestUpdate();
            }
            else
            {
                Debug.LogError("[BINDSTONE] BindstoneManager.Instance NULL!");
            }
        }

        IsChannellingBindstone = false;
        StopDanceAnimation();
        RPC_HideBindstoneChannellingUI();
    }
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_NotifyBindstoneQuestUpdate()
{
    
    // Sadece bu player'ın client'ında çalışsın
    if (Object.HasInputAuthority)
    {
        Debug.Log($"[BINDSTONE-QUEST] Client tarafında quest güncelleniyor");
        
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.UpdateQuestProgress("", QuestType.BindToBindstone, "any");
        }
    }
}

    private bool HasPlayerMovedDuringBindstoneChannelling()
    {
        float distanceMoved = Vector2.Distance(lastBindstoneChannellingPosition, transform.position);
        bool moved = distanceMoved > POSITION_CHANGE_THRESHOLD;
        if (moved)
        {
            if (character4D != null) character4D.SetExpression("Default");
            StartCoroutine(ForceDefaultExpressionAfterDelay());
        }
        return moved;
    }

    #region Gathering Channelling
    public void StartGatheringChannelling(GatherableObject gatherableObj)
    {
        if (!Object.HasInputAuthority) return;
        if (gatherableObj == null) return;

        RPC_RequestGatheringChannelling(gatherableObj.Object.Id);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestGatheringChannelling(NetworkId gatherableId)
    {
        if (!Runner.IsServer) return;
        if (IsChannellingTeleport || IsChannellingBindstone || IsChannellingGathering) return;

        // Gatherable'ı bul
        NetworkObject gatherableNetObj = Runner.FindObject(gatherableId);
        if (gatherableNetObj == null) return;

        GatherableObject gatherableObj = gatherableNetObj.GetComponent<GatherableObject>();
        if (gatherableObj == null || !gatherableObj.CanBeGathered()) return;

        // Channelling başlat
        IsChannellingGathering = true;
        CurrentGatherableId = gatherableId;
        GatheringChannellingTimer = TickTimer.CreateFromSeconds(Runner, GATHERING_CHANNELLING_DURATION);
        lastGatheringChannellingPosition = transform.position;

        // GatherableObject'e gathering başladığını bildir (Server-to-Server call, RPC değil!)
        NetworkObject playerNetObj = GetComponent<NetworkObject>();
        if (playerNetObj != null)
        {
            gatherableObj.ServerStartGathering(playerNetObj.Id, Object.InputAuthority);
        }

        StartDanceAnimation();
        RPC_ShowGatheringChannellingUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowGatheringChannellingUI()
    {
        if (gatheringChannelingUI != null && Object.HasInputAuthority)
        {
            gatheringChannelingUI.Show();
            gatheringChannelingUI.StartAutoUpdate(() => GetGatheringChannellingProgress());
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideGatheringChannellingUI()
    {
        if (gatheringChannelingUI != null && Object.HasInputAuthority)
        {
            gatheringChannelingUI.Hide();
        }
    }

    private void CompleteGatheringChannelling()
    {
        if (!Runner.IsServer) return;

        // Gathering tamamlandı, GatherableObject completion logic kendi içinde hallediliyor
        IsChannellingGathering = false;
        CurrentGatherableId = default;
        StopDanceAnimation();
        RPC_HideGatheringChannellingUI();
    }

    private void CancelGatheringChannelling()
    {
        if (!Runner.IsServer) return;

        // Gatherable'a cancel isteği gönder (Server-to-Server call, RPC değil!)
        if (CurrentGatherableId != default)
        {
            NetworkObject gatherableNetObj = Runner.FindObject(CurrentGatherableId);
            if (gatherableNetObj != null)
            {
                GatherableObject gatherableObj = gatherableNetObj.GetComponent<GatherableObject>();
                if (gatherableObj != null)
                {
                    NetworkObject playerNetObj = GetComponent<NetworkObject>();
                    if (playerNetObj != null)
                    {
                        gatherableObj.ServerCancelGathering(playerNetObj.Id);
                    }
                }
            }
        }

        IsChannellingGathering = false;
        CurrentGatherableId = default;
        StopDanceAnimation();
        RPC_HideGatheringChannellingUI();
    }

    private bool HasPlayerMovedDuringGatheringChannelling()
    {
        float distanceMoved = Vector2.Distance(lastGatheringChannellingPosition, transform.position);
        bool moved = distanceMoved > POSITION_CHANGE_THRESHOLD;
        if (moved)
        {
            if (character4D != null) character4D.SetExpression("Default");
            StartCoroutine(ForceDefaultExpressionAfterDelay());
        }
        return moved;
    }

    private float GetGatheringChannellingProgress()
    {
        if (!IsChannellingGathering) return 0f;
        float remaining = GatheringChannellingTimer.RemainingTime(Runner) ?? 0f;
        return 1f - (remaining / GATHERING_CHANNELLING_DURATION);
    }
    #endregion
}