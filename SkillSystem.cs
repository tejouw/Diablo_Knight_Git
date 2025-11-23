using UnityEngine;
using Fusion;
using System.Collections.Generic;


public class SkillSystem : NetworkBehaviour
{
    // SkillSystem.cs - Network Properties kısmını düzelt
    [Header("Skill Sets - Each slot has 3 skills")]
    [Networked, Capacity(3)] public NetworkArray<NetworkString<_32>> NetworkUtilitySkillIds { get; }
    [Networked, Capacity(3)] public NetworkArray<NetworkString<_32>> NetworkCombatSkillIds { get; }
    [Networked, Capacity(3)] public NetworkArray<NetworkString<_32>> NetworkUltimateSkillIds { get; }

    [Networked] public int NetworkUtilityActiveIndex { get; set; }
    [Networked] public int NetworkCombatActiveIndex { get; set; }
    [Networked] public int NetworkUltimateActiveIndex { get; set; }

    [Header("Passive Slots")]
    [Networked] public string NetworkPassive1Id { get; set; }
    [Networked] public string NetworkPassive2Id { get; set; }


    // Local skill instances
    private Dictionary<string, SkillInstance> skillInstances = new Dictionary<string, SkillInstance>();
    private Dictionary<SkillSlot, string> equippedSkills = new Dictionary<SkillSlot, string>();
    private Dictionary<PassiveSkillSlot, string> equippedPassives = new Dictionary<PassiveSkillSlot, string>();

    private PlayerStats playerStats;
    private ClassSystem classSystem;
    private const int SKILL_XP_PER_USE = 10;

    // Events
    public event System.Action<SkillSlot, SkillInstance> OnSkillEquipped;
    public event System.Action<string, int> OnSkillLevelUp;
    public event System.Action<PassiveSkillSlot, SkillInstance> OnPassiveEquipped;
    public override void Spawned()
    {
        playerStats = GetComponent<PlayerStats>();
        classSystem = GetComponent<ClassSystem>();

        if (Object == null || !Object.IsValid)
        {
            return;
        }

        if (Object.HasInputAuthority)
        {
            StartCoroutine(InitializeSkillSystemDelayed());

            if (classSystem != null)
            {
                classSystem.OnClassChanged += OnClassChanged;
            }

            // Level up event'ine subscribe ol
            if (playerStats != null)
            {
                playerStats.OnLevelChanged += OnPlayerLevelUp;
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecuteColossusStanceRPC(Vector3 position, Vector3[] enemyPositions, Vector3[] allyPositions, int enemyCount)
    {
        var executor = SkillExecutorFactory.GetExecutor("colossus_stance") as ColossusStanceExecutor;
        if (executor != null)
        {
            executor.ExecuteVFX(gameObject, position, enemyPositions, allyPositions, enemyCount);
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecuteSeismicRuptureRPC(Vector3 position, Vector2 direction, int targetCount, Vector3[] targetPositions)
    {

        var executor = SkillExecutorFactory.GetExecutor("seismic_rupture") as SeismicRuptureExecutor;
        if (executor != null)
        {
            executor.ExecuteVFX(gameObject, position, direction, targetCount, targetPositions);
        }
        else
        {
            Debug.LogError("[VFX-DEBUG] SeismicRuptureExecutor not found!");
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecutePiercingThrustRPC(Vector3 startPos, Vector3 endPos, Vector2 direction, int targetCount, bool hasSpeedBuff)
    {
        var executor = SkillExecutorFactory.GetExecutor("piercing_thrust") as PiercingThrustExecutor;
        if (executor != null)
        {
            executor.ExecuteVFX(gameObject, startPos, endPos, direction, targetCount, hasSpeedBuff);
        }
    }
    public void RotateSkillInSlot(SkillSlot slot, int newIndex)
    {
        if (!Object.HasInputAuthority || newIndex < 0 || newIndex >= 3) return;

        RequestSkillRotationRPC(slot, newIndex);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestSkillRotationRPC(SkillSlot slot, int newIndex)
    {
        if (!Runner.IsServer) return;

        switch (slot)
        {
            case SkillSlot.Skill1:
                NetworkUtilityActiveIndex = newIndex;
                break;
            case SkillSlot.Skill2:
                NetworkCombatActiveIndex = newIndex;
                break;
            case SkillSlot.Skill3:
                NetworkUltimateActiveIndex = newIndex;
                break;
        }

        SyncSkillRotationRPC(slot, newIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SyncSkillRotationRPC(SkillSlot slot, int newIndex)
    {
        var skillId = GetEquippedSkillId(slot);
        var instance = GetSkillInstance(skillId);

        OnSkillEquipped?.Invoke(slot, instance);

        if (Object.HasInputAuthority)
        {
            SaveSkillData();
        }
    }

    // Yeni metod ekle - initialization'ı geciktir
    private System.Collections.IEnumerator InitializeSkillSystemDelayed()
    {
        // Network object'in tam olarak hazır olmasını bekle
        yield return new WaitForSeconds(0.5f);

        LoadSkillData();
    }

    public void UseSkill1()
    {
        UseSkillInSlot(SkillSlot.Skill1);
    }

    public void UseSkill2()
    {
        UseSkillInSlot(SkillSlot.Skill2);
    }

    public void UseSkill3()
    {
        UseSkillInSlot(SkillSlot.Skill3);
    }

private void UseSkillInSlot(SkillSlot slot)
{
    if (Object == null || !Object.IsValid || !Object.HasInputAuthority)
    {
        return;
    }

    // YENÄ° - Death kontrolÃ¼
    var deathSystem = GetComponent<DeathSystem>();
    if (deathSystem != null && deathSystem.GetSafeDeathStatus())
    {
        return;
    }

    // Class kontrolÃ¼ ekle
    if (classSystem == null || classSystem.NetworkPlayerClass == ClassType.None)
    {
        return;
    }

    string skillId = GetEquippedSkillId(slot);

    if (string.IsNullOrEmpty(skillId))
    {
        return;
    }

    var instance = GetSkillInstance(skillId);
    if (instance == null)
    {
        return;
    }

    bool isOnCooldown = instance.IsOnCooldown(Time.time);

    if (isOnCooldown)
    {
        float remaining = instance.GetRemainingCooldown(Time.time);
        return;
    }

    // *** YENÄ°: Immediate client execution ***
    ExecuteSkillImmediateClient(skillId, instance);

    // Parallel server request
    RequestUseSkillRPC(skillId, slot);
    
    // YENÄ°: Client UI iÃ§in cooldown'u set et (RPC sonrasÄ±nda)
    instance.lastUsedTime = Time.time;
}
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void ShowSkillDamagePopupRPC(Vector3 position, float damage, int damageType)
{
    DamagePopup.Create(position + Vector3.up, damage, (DamagePopup.DamageType)damageType);
}
private void ExecuteSkillImmediateClient(string skillId, SkillInstance instance)
{
    var executor = SkillExecutorFactory.GetExecutor(skillId);
    if (executor == null) return;

    // ÖNCELİKLE lastUsedTime'ı kaydet
    float originalLastUsedTime = instance.lastUsedTime;

    // Client-side immediate execution (only VFX + Animation)
    if (executor is BlindingShotExecutor blindingExecutor)
    {
        blindingExecutor.ExecuteClientImmediate(gameObject, instance);
    }
    else if (executor is PiercingArrowExecutor piercingExecutor)
    {
        piercingExecutor.ExecuteClientImmediate(gameObject, instance);
    }
    else if (executor is CleaveStrikeExecutor cleaveExecutor)
    {
        cleaveExecutor.ExecuteClientImmediate(gameObject, instance);
    }
    else if (executor is RainOfArrowsExecutor rainOfArrowsExecutor)
    {
        rainOfArrowsExecutor.ExecuteClientImmediate(gameObject, instance);
    }
    else
    {
        // Generic executor için fallback - SADECE VFX/Animation
        executor.Execute(gameObject, instance);
    }
    
    // SONRASINDA lastUsedTime'ı geri restore et
    instance.lastUsedTime = originalLastUsedTime;
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void NotifySkillFailureRPC(string errorMessage)
{
    if (Object.HasInputAuthority)
    {
        // UI'da error göster (opsiyonel)
    }
}
[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void RequestUseSkillRPC(string skillId, SkillSlot slot)
{
    if (!Runner.IsServer)
    {
        Debug.LogError("[SkillSystem] RequestUseSkillRPC called but not on server!");
        return;
    }

    var instance = GetSkillInstance(skillId);
    if (instance == null)
    {
        Debug.LogError($"[SkillSystem-SERVER] No skill instance found for ID: {skillId}");
        return;
    }

    if (instance.IsOnCooldown(Time.time))
    {
        // Client'a error gönder
        NotifySkillFailureRPC("Skill on cooldown");
        return;
    }

    var executor = SkillExecutorFactory.GetExecutor(skillId);

        if (executor != null)
        {
            // COMBAT SKILLS
            if (executor is CleaveStrikeExecutor cleaveExecutor)
            {
                cleaveExecutor.ExecuteOnServer(gameObject, instance, this);
            }
            else if (executor is PiercingThrustExecutor piercingExecutor)
            {
                piercingExecutor.ExecuteOnServer(gameObject, instance, this);
            }
            else if (executor is GuardedSlamExecutor guardedSlamExecutor)
            {
                guardedSlamExecutor.ExecuteOnServer(gameObject, instance, this);
            }
            // YENİ - BlindingShot executor
            else if (executor is BlindingShotExecutor blindingShotExecutor)
            {
                blindingShotExecutor.ExecuteOnServer(gameObject, instance, this);
            }
            // UTILITY SKILLS
            else if (executor is BattleRoarExecutor battleRoarExecutor)
            {
                battleRoarExecutor.ExecuteOnServer(gameObject, instance, this);
            }
            // ULTIMATE SKILLS
            else if (executor is SeismicRuptureExecutor seismicExecutor)
            {
                seismicExecutor.ExecuteOnServer(gameObject, instance, this);
            }
            else if (executor is RainOfArrowsExecutor rainOfArrowsExecutor)
            {
                rainOfArrowsExecutor.ExecuteOnServer(gameObject, instance, this);
            }
            else if (executor is PiercingArrowExecutor piercingArrowExecutor)
            {
                piercingArrowExecutor.ExecuteOnServer(gameObject, instance, this);
            }
            else if (executor is EvasiveRollExecutor evasiveRollExecutor)
            {
                evasiveRollExecutor.ExecuteOnServer(gameObject, instance, this);
            }
            else if (executor is ColossusStanceExecutor colossusExecutor)
            {
                colossusExecutor.ExecuteOnServer(gameObject, instance, this);
            }


            else
            {
                instance.lastUsedTime = Time.time;
                instance.AddXP(SKILL_XP_PER_USE);

                if (instance.CanLevelUp())
                {
                    instance.AddXP(0);
                    OnSkillLevelUp?.Invoke(skillId, instance.currentLevel);
                }

                ExecuteSkillEffectRPC(skillId, slot);
            }
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecuteEvasiveRollRPC(Vector3 startPos, Vector3 endPos, Vector2 direction)
    {
        var executor = SkillExecutorFactory.GetExecutor("evasive_roll") as EvasiveRollExecutor;
        if (executor != null)
        {
            executor.ExecuteVFX(gameObject, startPos, endPos, direction);
        }
    }
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void ExecutePiercingArrowHitEffectsRPC(Vector3 startPos, Vector2 direction, Vector3[] targetPositions, bool hasAttackSpeedBuff)
{
    var executor = SkillExecutorFactory.GetExecutor("piercing_arrow") as PiercingArrowExecutor;
    if (executor != null)
    {
        executor.ExecuteHitEffectsVFX(gameObject, startPos, direction, targetPositions, hasAttackSpeedBuff);
    }
}
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecutePiercingArrowRPC(Vector3 startPos, Vector2 direction, int targetCount, bool hasAttackSpeedBuff)
    {
        var executor = SkillExecutorFactory.GetExecutor("piercing_arrow") as PiercingArrowExecutor;
        if (executor != null)
        {
            executor.ExecuteVFX(gameObject, startPos, direction, targetCount, hasAttackSpeedBuff);
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecuteRainOfArrowsRPC(Vector3 aoeCenter, Vector3[] arrowPositions, Vector2 direction)
    {
        var executor = SkillExecutorFactory.GetExecutor("rain_of_arrows") as RainOfArrowsExecutor;
        if (executor != null)
        {
            executor.ExecuteVFX(gameObject, aoeCenter, arrowPositions, direction);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecuteArrowImpactRPC(Vector3 position, int arrowIndex)
    {
        var executor = SkillExecutorFactory.GetExecutor("rain_of_arrows") as RainOfArrowsExecutor;
        if (executor != null)
        {
            executor.ExecuteArrowImpactVFX(position, arrowIndex);
        }
    }
    // BlindingShot RPC metodları
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecuteBlindingShotRPC(Vector3 startPos, Vector2 direction, bool hitTarget, Vector3 targetPos)
    {
        var executor = SkillExecutorFactory.GetExecutor("blinding_shot") as BlindingShotExecutor;
        if (executor != null)
        {
            executor.ExecuteVFX(gameObject, startPos, direction, hitTarget, targetPos);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecuteBlindingShotMissRPC(Vector3 startPos, Vector2 direction)
    {
        var executor = SkillExecutorFactory.GetExecutor("blinding_shot") as BlindingShotExecutor;
        if (executor != null)
        {
            executor.ExecuteMissVFX(gameObject, startPos, direction);
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecuteBattleRoarRPC(Vector3 position, Vector3[] allyPositions, Vector3[] enemyPositions)
    {
        var executor = SkillExecutorFactory.GetExecutor("battle_roar") as BattleRoarExecutor;
        if (executor != null)
        {
            executor.ExecuteVFX(gameObject, position, allyPositions, enemyPositions);
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecuteGuardedSlamRPC(Vector3 position, Vector2 direction, bool hitTarget, Vector3 targetPosition)
    {
        var executor = SkillExecutorFactory.GetExecutor("guarded_slam") as GuardedSlamExecutor;
        if (executor != null)
        {
            executor.ExecuteVFX(gameObject, position, direction, hitTarget, targetPosition);
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ExecuteSkillEffectRPC(string skillId, SkillSlot slot)
    {

        var executor = SkillExecutorFactory.GetExecutor(skillId);
        if (executor == null)
        {
            Debug.LogError($"[SkillSystem] No executor found for {skillId}");
            return;
        }

        var instance = GetSkillInstance(skillId);
        if (instance == null)
        {
            Debug.LogError($"[SkillSystem] No skill instance found for {skillId}");
            return;
        }

        // Sadece VFX execution yap, server execution yapma
        if (executor is CleaveStrikeExecutor cleaveExecutor)
        {
            // CleaveStrike için sadece VFX - server execution zaten yapıldı
        }
        else
        {
            // Diğer skill'ler için normal execution (eski sistem)
            if (!executor.CanExecute(gameObject, instance))
            {
                return;
            }

            executor.Execute(gameObject, instance);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RequestSkillExecutionRPC(string skillId, string skillInstanceId)
    {

        if (!Runner.IsServer)
        {
            Debug.LogError("[SkillSystem] RequestSkillExecutionRPC called but not on server!");
            return;
        }

        var executor = SkillExecutorFactory.GetExecutor(skillId);
        if (executor == null)
        {
            Debug.LogError($"[SkillSystem-SERVER] No executor found for skill: {skillId}");
            return;
        }

        var instance = GetSkillInstance(skillInstanceId);
        if (instance == null)
        {
            Debug.LogError($"[SkillSystem-SERVER] No skill instance found: {skillInstanceId}");
            return;
        }

        // ✅ SERVER-SIDE COOLDOWN CHECK
        if (instance.lastUsedTime > 0 && instance.IsOnCooldown(Time.time))
        {
            return;
        }


        // Server-side execution
        if (executor is CleaveStrikeExecutor cleaveExecutor)
        {
            cleaveExecutor.ExecuteOnServer(gameObject, instance, this);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ExecuteSkillVFXRPC(string skillId, Vector3 position, Vector2 direction, int targetCount)
    {
        var executor = SkillExecutorFactory.GetExecutor(skillId);
        if (executor == null) return;

        // VFX execution
        if (executor is CleaveStrikeExecutor cleaveExecutor)
        {
            cleaveExecutor.ExecuteVFX(gameObject, position, direction, targetCount);
        }
    }

    private void Update()
    {
        if (Object == null || !Object.IsValid || !Object.HasInputAuthority) return;

        if (Input.GetKeyDown(KeyCode.F1))
        {
            DebugPrintSkillStatus();
        }

    }

    private void DebugPrintSkillStatus()
    {

        foreach (var kvp in skillInstances)
        {
            var instance = kvp.Value;
        }

    }

    public void EquipSkill(string skillId, SkillSlot slot)
    {

        if (Object == null || !Object.IsValid || !Object.HasInputAuthority)
        {
            Debug.LogError("[SkillSystem] Object validation failed in EquipSkill!");
            return;
        }


        var skillData = SkillDatabase.Instance?.GetSkillById(skillId);
        if (skillData == null)
        {

            return;
        }


        // Slot tipine uygun mu kontrol et
        SkillType expectedType = slot switch
        {
            SkillSlot.Skill1 => SkillType.Utility,
            SkillSlot.Skill2 => SkillType.Combat,
            SkillSlot.Skill3 => SkillType.Ultimate,
            _ => SkillType.Combat
        };


        if (skillData.skillType != expectedType)
        {
            return;
        }

        RequestEquipSkillRPC(skillId, slot);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestEquipSkillRPC(string skillId, SkillSlot slot)
    {
        if (!Runner.IsServer)
        {
            return;
        }

        switch (slot)
        {
            case SkillSlot.Skill1:
                NetworkUtilitySkillIds.Set(NetworkUtilityActiveIndex, skillId);
                break;
            case SkillSlot.Skill2:
                NetworkCombatSkillIds.Set(NetworkCombatActiveIndex, skillId);
                break;
            case SkillSlot.Skill3:
                NetworkUltimateSkillIds.Set(NetworkUltimateActiveIndex, skillId);
                break;
        }

        SyncSkillEquipRPC(skillId, slot);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SyncSkillEquipRPC(string skillId, SkillSlot slot)
    {

        equippedSkills[slot] = skillId;

        var instance = GetOrCreateSkillInstance(skillId);

        OnSkillEquipped?.Invoke(slot, instance);

        if (Object.HasInputAuthority)
        {
            SaveSkillData();
        }
    }


    public void EquipPassiveSkill(string skillId, PassiveSkillSlot slot)
    {
        if (!Object.HasInputAuthority) return;

        var skillData = SkillDatabase.Instance?.GetSkillById(skillId);
        if (skillData == null || !skillData.isPassiveSkill) return;

        RequestEquipPassiveRPC(skillId, slot);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestEquipPassiveRPC(string skillId, PassiveSkillSlot slot)
    {
        if (!Runner.IsServer) return;

        switch (slot)
        {
            case PassiveSkillSlot.Passive1:
                NetworkPassive1Id = skillId;
                break;
            case PassiveSkillSlot.Passive2:
                NetworkPassive2Id = skillId;
                break;
        }

        SyncPassiveEquipRPC(skillId, slot);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SyncPassiveEquipRPC(string skillId, PassiveSkillSlot slot)
    {
        equippedPassives[slot] = skillId;

        var instance = GetOrCreateSkillInstance(skillId);
        OnPassiveEquipped?.Invoke(slot, instance);

        if (Object.HasInputAuthority)
        {
            SaveSkillData();
        }
    }

    private SkillInstance GetOrCreateSkillInstance(string skillId)
    {
        string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";

        if (!skillInstances.TryGetValue(skillId, out var instance))
        {
            instance = new SkillInstance(skillId);
            instance.skillData = SkillDatabase.Instance?.GetSkillById(skillId);

            if (instance.skillData == null)
            {
                return null;
            }

            skillInstances[skillId] = instance;
        }

        return instance;
    }

    public SkillInstance GetSkillInstance(string skillId)
    {

        bool found = skillInstances.TryGetValue(skillId, out var instance);

        return found ? instance : null;
    }

// GetEquippedSkillId metodunu güvenli hale getir
public string GetEquippedSkillId(SkillSlot slot)
{
    // Network object ready kontrolü
    if (Object == null || !Object.IsValid)
        return "";
    
    try
    {
        string result = slot switch
        {
            SkillSlot.Skill1 => NetworkUtilitySkillIds[NetworkUtilityActiveIndex].ToString(),
            SkillSlot.Skill2 => NetworkCombatSkillIds[NetworkCombatActiveIndex].ToString(),
            SkillSlot.Skill3 => NetworkUltimateSkillIds[NetworkUltimateActiveIndex].ToString(),
            _ => ""
        };

        return string.IsNullOrEmpty(result) ? "" : result;
    }
    catch
    {
        return "";
    }
}
    public string[] GetSkillSetForSlot(SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 => new string[]
            {
            NetworkUtilitySkillIds[0].ToString(),
            NetworkUtilitySkillIds[1].ToString(),
            NetworkUtilitySkillIds[2].ToString()
            },
            SkillSlot.Skill2 => new string[]
            {
            NetworkCombatSkillIds[0].ToString(),
            NetworkCombatSkillIds[1].ToString(),
            NetworkCombatSkillIds[2].ToString()
            },
            SkillSlot.Skill3 => new string[]
            {
            NetworkUltimateSkillIds[0].ToString(),
            NetworkUltimateSkillIds[1].ToString(),
            NetworkUltimateSkillIds[2].ToString()
            },
            _ => new string[3]
        };
    }
    public int GetActiveIndexForSlot(SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 => NetworkUtilityActiveIndex,
            SkillSlot.Skill2 => NetworkCombatActiveIndex,
            SkillSlot.Skill3 => NetworkUltimateActiveIndex,
            _ => 0
        };
    }
public void EquipSkillToSet(string skillId, SkillSlot slot, int setIndex)
{
    if (!IsNetworkReady() || setIndex < 0 || setIndex >= 3)
        return;

    var skillData = SkillDatabase.Instance?.GetSkillById(skillId);
    if (skillData == null)
        return;

    SkillType expectedType = slot switch
    {
        SkillSlot.Skill1 => SkillType.Utility,
        SkillSlot.Skill2 => SkillType.Combat,
        SkillSlot.Skill3 => SkillType.Ultimate,
        _ => SkillType.Combat
    };

    if (skillData.skillType != expectedType)
        return;

    // RPC gönder
    RequestEquipSkillToSetRPC(skillId, slot, setIndex);
    
    // Retry mechanism'i sadece önemli durumlarda çalıştır
    string currentSkillId = GetEquippedSkillId(slot);
    if (string.IsNullOrEmpty(currentSkillId))
    {
        // Retry check - 4 saniye bekle (daha uzun)
        StartCoroutine(SkillEquipRetryCheck(skillId, slot, setIndex, 4f));
    }
}

// Retry check metodunu daha sessiz yap
private System.Collections.IEnumerator SkillEquipRetryCheck(string skillId, SkillSlot slot, int setIndex, float delay)
{
    yield return new WaitForSeconds(delay);
    
    // Hala equip edilmemişse retry
    string currentSkillId = GetEquippedSkillId(slot);
    if (string.IsNullOrEmpty(currentSkillId))
    {
        // Sadece 2. retry'da log at (3. deneme)
        if (delay > 3.5f && Application.isEditor)
        {
            string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";
        }
        
        // Tekrar dene
        RequestEquipSkillToSetRPC(skillId, slot, setIndex);
        
        // Son çare fallback - 2 saniye daha bekle
        StartCoroutine(SkillEquipFinalFallback(skillId, slot, setIndex, 2f));
    }
}

// Final fallback - local execution
private System.Collections.IEnumerator SkillEquipFinalFallback(string skillId, SkillSlot slot, int setIndex, float delay)
{
    yield return new WaitForSeconds(delay);
    
    string currentSkillId = GetEquippedSkillId(slot);
    if (string.IsNullOrEmpty(currentSkillId))
    {
        string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";
        
        // Network array'i direkt set et
        switch (slot)
        {
            case SkillSlot.Skill1:
                NetworkUtilitySkillIds.Set(setIndex, skillId);
                break;
            case SkillSlot.Skill2:
                NetworkCombatSkillIds.Set(setIndex, skillId);
                break;
            case SkillSlot.Skill3:
                NetworkUltimateSkillIds.Set(setIndex, skillId);
                break;
        }
        
        // Local state'i de güncelle
        var instance = GetOrCreateSkillInstance(skillId);
        int activeIndex = GetActiveIndexForSlot(slot);
        
        if (setIndex == activeIndex && instance != null)
        {
            OnSkillEquipped?.Invoke(slot, instance);
        }
        
        SaveSkillData();
    }
}
// Load sırasında network state'i de restore et
private void LoadSkillData()
{
    if (!Object.HasInputAuthority) return;
    
    string nickname = playerStats?.GetPlayerDisplayName() ?? "Player";
    string skillKey = $"SkillData_{nickname}";

    if (PlayerPrefs.HasKey(skillKey))
    {
        string skillJson = PlayerPrefs.GetString(skillKey);
        // JSON loading logic implementation...
        
        // YENI: Network state'i de restore et
        if (Object.HasInputAuthority)
        {
            StartCoroutine(RestoreNetworkStateAfterLoad());
        }
    }
}

// Network state'i restore etmek için yeni metod
private System.Collections.IEnumerator RestoreNetworkStateAfterLoad()
{
    yield return new WaitForSeconds(1f); // Network hazır olmasını bekle
    
    // Local state'den network state'i restore et
    foreach (var kvp in equippedSkills)
    {
        SkillSlot slot = kvp.Key;
        string skillId = kvp.Value;
        
        if (!string.IsNullOrEmpty(skillId))
        {
            // Force network sync
            RequestEquipSkillToSetRPC(skillId, slot, 0);
        }
    }
}
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestEquipSkillToSetRPC(string skillId, SkillSlot slot, int setIndex)
    {
        string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";

        if (!Runner.IsServer)
        {
            return;
        }


        switch (slot)
        {
            case SkillSlot.Skill1:
                NetworkUtilitySkillIds.Set(setIndex, skillId);
                break;
            case SkillSlot.Skill2:
                NetworkCombatSkillIds.Set(setIndex, skillId);
                break;
            case SkillSlot.Skill3:
                NetworkUltimateSkillIds.Set(setIndex, skillId);
                break;
        }

        SyncSkillSetUpdateRPC(skillId, slot, setIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SyncSkillSetUpdateRPC(string skillId, SkillSlot slot, int setIndex)
    {
        string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";


        var instance = GetOrCreateSkillInstance(skillId);

        if (instance == null)
        {
            return;
        }


        // Eğer bu aktif index ise event fire et
        int activeIndex = GetActiveIndexForSlot(slot);

        if (setIndex == activeIndex)
        {
            OnSkillEquipped?.Invoke(slot, instance);
        }

        if (Object.HasInputAuthority)
        {
            SaveSkillData();
        }
    }
    public List<SkillData> GetAvailableSkills(SkillType skillType)
    {
        if (SkillDatabase.Instance == null || playerStats == null)
            return new List<SkillData>();

        ClassType playerClass = classSystem?.NetworkPlayerClass ?? ClassType.None;
        return SkillDatabase.Instance.GetAvailableSkills(playerClass, skillType, playerStats.CurrentLevel);
    }
private bool IsNetworkReady()
{
    if (Object == null || !Object.IsValid || !Object.HasInputAuthority)
        return false;
        
    // Network runner hazır mı?
    if (Runner == null || !Runner.IsRunning)
        return false;
        
    // Input authority geçerli mi?
    if (Object.InputAuthority == PlayerRef.None)
        return false;
        
    return true;
}
    public void OnClassChanged(ClassType newClass)
    {
        string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";

        // Sadece InputAuthority sahibi client skill equip işlemlerini yapmalı
        if (!IsNetworkReady())
        {
            return;
        }

        if (newClass == ClassType.None)
        {
            ClearAllSkills();
        }
        else
        {
            // PlayerStats'ın RecalculateStats metodunu çağır
            if (playerStats != null)
            {
                playerStats.RecalculateStats();
            }

            // Skill equip işlemini geciktir - Network array'lerin hazır olmasını bekle
            StartCoroutine(DelayedSkillEquipWithNetworkCheck(newClass));
        }

        // UI'ı refresh et
        RefreshSkillUI();
    }
private System.Collections.IEnumerator DelayedSkillEquipWithNetworkCheck(ClassType classType)
{
    string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";

    // Network array'lerin ready olmasını bekle - daha agresif kontrol
    float waitTime = 0f;
    bool arraysReady = false;
    
    while (waitTime < 5f && !arraysReady)
    {
        // Network object kontrolü
        if (IsNetworkReady())
        {
            // Network array'ler ready mi kontrol et - daha sıkı kontrol
            bool hasAnySkill = !string.IsNullOrEmpty(NetworkUtilitySkillIds[0].ToString()) ||
                              !string.IsNullOrEmpty(NetworkCombatSkillIds[0].ToString()) ||
                              !string.IsNullOrEmpty(NetworkUltimateSkillIds[0].ToString());
            
            // PlayerStats ready mi ve network stable mı?
            bool playerReady = playerStats != null && playerStats.CurrentLevel > 0;
            bool networkStable = Runner != null && Runner.IsRunning && Runner.Tick > 30; // 30 tick bekle
            
            if (playerReady && networkStable)
            {
                if (hasAnySkill)
                {
                    // Zaten skill'ler var, sadece UI refresh yap
                    RefreshSkillUI();
                    yield break;
                }
                else
                {
                    // Network hazır, skill'leri equip et
                    arraysReady = true;
                    break;
                }
            }
        }

        yield return new WaitForSeconds(0.2f);
        waitTime += 0.2f;
    }

    if (arraysReady)
    {
        EquipClassSkills(classType);
    }
    else if (Application.isEditor) // Sadece editor'da log
    {
        EquipClassSkills(classType); // Fallback
    }
}
// DelayedSkillEquip metodunu değiştir - daha uzun bekle
private System.Collections.IEnumerator DelayedSkillEquip(ClassType classType)
{
    string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";

    // Network array'lerin ready olmasını bekle
    float waitTime = 0f;
    while (waitTime < 8f) // 5'ten 8'e çıkar
    {
        // Network object kontrolü
        if (Object != null && Object.IsValid && Object.HasInputAuthority)
        {
            // Network array'ler ready mi kontrol et
            bool arraysReady = !string.IsNullOrEmpty(NetworkUtilitySkillIds[0].ToString()) ||
                              !string.IsNullOrEmpty(NetworkCombatSkillIds[0].ToString()) ||
                              !string.IsNullOrEmpty(NetworkUltimateSkillIds[0].ToString());
            
            if (arraysReady)
            {
                // Zaten skill'ler var, sadece UI refresh yap
                RefreshSkillUI();
                yield break;
            }
            
            // PlayerStats ready mi?
            if (playerStats != null && playerStats.CurrentLevel > 0)
            {
                EquipClassSkills(classType);
                yield break;
            }
        }

        yield return new WaitForSeconds(0.3f); // 0.2'den 0.3'e çıkar
        waitTime += 0.3f;
    }

    if (waitTime >= 8f)
    {
        // Timeout durumunda sessizce equip et
        EquipClassSkills(classType);
    }
}
    private void EquipClassSkills(ClassType classType)
    {
        if (SkillDatabase.Instance == null || playerStats == null)
        {
            return;
        }

        string playerName = playerStats.GetPlayerDisplayName();

        // Her skill tipi için available skill'leri al ve equip et
        EquipSkillsForType(SkillType.Utility, SkillSlot.Skill1);
        EquipSkillsForType(SkillType.Combat, SkillSlot.Skill2);
        EquipSkillsForType(SkillType.Ultimate, SkillSlot.Skill3);
    }

    private void EquipSkillsForType(SkillType skillType, SkillSlot slot)
    {
        if (SkillDatabase.Instance == null || playerStats == null) return;

        string playerName = playerStats.GetPlayerDisplayName();
        ClassType currentClass = classSystem?.NetworkPlayerClass ?? ClassType.None;


        var availableSkills = SkillDatabase.Instance.GetAvailableSkills(currentClass, skillType, playerStats.CurrentLevel);


        // Mevcut equip edilmiş skill'leri al
        string[] currentSkillSet = GetSkillSetForSlot(slot);


        // Available skill'leri slot'lara equip et
        for (int i = 0; i < availableSkills.Count && i < 3; i++)
        {
            var skill = availableSkills[i];
            if (skill != null)
            {

                // Bu skill zaten equip edilmiş mi kontrol et
                bool alreadyEquipped = false;
                for (int j = 0; j < 3; j++)
                {
                    if (currentSkillSet[j] == skill.skillId)
                    {
                        alreadyEquipped = true;
                        break;
                    }
                }

                if (!alreadyEquipped)
                {
                    EquipSkillToSet(skill.skillId, slot, i);
                }
            }
        }
    }
    public void OnPlayerLevelUp(int newLevel)
    {
        if (!Object.HasInputAuthority) return;

        ClassType currentClass = classSystem?.NetworkPlayerClass ?? ClassType.None;
        if (currentClass != ClassType.None)
        {
            // Level up olduğunda yeni skill'leri kontrol et ve equip et
            EquipClassSkills(currentClass);
        }
    }
    public void RefreshSkillUI()
    {
        // SkillSlotManager'ı bul ve refresh et
        SkillSlotManager skillSlotManager = FindFirstObjectByType<SkillSlotManager>();
        if (skillSlotManager != null)
        {
            skillSlotManager.RefreshAllSkillIcons();
        }
    }

    private void ClearAllSkills()
    {
        if (Object.HasInputAuthority)
        {
            // Client'dan normal RPC gönder
            RequestClearSkillsRPC();
        }
        else if (Object.HasStateAuthority)
        {
            // Server'da direkt temizle
            ClearSkillsDirectly();
        }
    }
    public void ClearSkillsDirectly()
    {
        if (!Runner.IsServer) return;

        // TÃ¼m skill set'leri temizle
        for (int i = 0; i < 3; i++)
        {
            NetworkUtilitySkillIds.Set(i, "");
            NetworkCombatSkillIds.Set(i, "");
            NetworkUltimateSkillIds.Set(i, "");
        }

        // Active index'leri sÄ±fÄ±rla
        NetworkUtilityActiveIndex = 0;
        NetworkCombatActiveIndex = 0;
        NetworkUltimateActiveIndex = 0;

        // Tüm client'lara sync et
        SyncClearSkillsRPC();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestClearSkillsRPC()
    {
        if (!Runner.IsServer) return;

        // Tüm skill set'leri temizle
        for (int i = 0; i < 3; i++)
        {
            NetworkUtilitySkillIds.Set(i, "");
            NetworkCombatSkillIds.Set(i, "");
            NetworkUltimateSkillIds.Set(i, "");
        }

        // Active index'leri sıfırla
        NetworkUtilityActiveIndex = 0;
        NetworkCombatActiveIndex = 0;
        NetworkUltimateActiveIndex = 0;

        SyncClearSkillsRPC();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SyncClearSkillsRPC()
    {
        equippedSkills.Clear();
        equippedPassives.Clear();

        if (Object.HasInputAuthority)
        {
            SaveSkillData();
        }
    }

    private void SaveSkillData()
    {
        string nickname = playerStats?.GetPlayerDisplayName() ?? "Player";
        string skillKey = $"SkillData_{nickname}";

        // JSON saving logic - gelecekte implement edilecek
        PlayerPrefs.Save();
    }
    [ContextMenu("Debug Network State")]
public void DebugNetworkState()
{
    string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";


    // Current network arrays durumu
}

    private void OnDestroy()
    {
        // MEMORY LEAK FIX: Event unsubscription
        if (classSystem != null)
        {
            classSystem.OnClassChanged -= OnClassChanged;
        }

        if (playerStats != null)
        {
            playerStats.OnLevelChanged -= OnPlayerLevelUp;
        }

        // Event cleanup
        OnSkillEquipped = null;
        OnSkillLevelUp = null;
        OnPassiveEquipped = null;

        // Coroutine cleanup
        StopAllCoroutines();
    }
}
