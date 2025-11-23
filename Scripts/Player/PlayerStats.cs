using UnityEngine;
using Fusion;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Firebase.Database;
using System.Collections;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Linq;

public class PlayerStats : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField] public PlayerStatsData statsData;
    [Header("Visual Effects")]
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color damageFlashColor = Color.red;

    private DeathSystem deathSystem;
    private WeaponSystem weaponSystem;
    private Character4D character4D;
    private ClassSystem classSystem;
    private Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
    private Coroutine flashCoroutine;
    public bool isInitialized = false;
    private float lastSaveTime = 0f;
    private const float SAVE_INTERVAL = 5f;
    private string customPlayerName = "";
    public bool hasReceivedDefaultEquipment = false;
    private PlayerRef cachedInputAuthority;

    [Networked] public float NetworkCurrentHP { get; set; } = -1f;
    [Networked] public float NetworkCurrentXP { get; set; }
    [Networked] public int NetworkCurrentLevel { get; set; }
    [Networked] public int NetworkCoins { get; set; }
    [Networked] public float NetworkBaseDamage { get; set; }
    [Networked] public float NetworkBaseArmor { get; set; }
    [Networked] public float NetworkBaseCriticalChance { get; set; }
    [Networked] public float NetworkMoveSpeed { get; set; }
    [Networked] public float NetworkBaseAttackSpeed { get; set; }
    [Networked] public int NetworkCurrentWeaponType { get; set; }
    [Networked] public string NetworkPlayerNickname { get; set; }
    [Networked] public int CurrentPartyId { get; set; } = -1;
    [Networked] public bool NetworkIsFlashing { get; set; }
    [Networked] public float NetworkFlashStartTime { get; set; }
    [Networked] public int NetworkPotionCount { get; set; }
    [Networked] public int NetworkPotionLevel { get; set; } = 1;

    private float lastKnownNetworkHP = -1f;
    private int lastKnownPotionCount = -1;
    private float _currentXP;
    public int _currentLevel;
    private int _coins;
    private float _baseDamage;
    private float _baseArmor;
    private float _baseCriticalChance;
    private float _baseCriticalMultiplier = 1.5f;
    private float _baseAttackSpeed = 1f;
    private float _moveSpeed;
    private float _weaponDamageMultiplier = 1f;
    private float _weaponAttackSpeedModifier = 1f;
    private float _weaponCriticalModifier = 1f;
    private float _itemDamageBonus;
    private float _itemCriticalChanceBonus;
    private float _itemCriticalMultiplierBonus;
    private Dictionary<StatType, float> equipmentStats = new Dictionary<StatType, float>();
    private float partyXpBonus = 0f;
    private float partyBuffMultiplier = 1f;
    public float PartyBuffMultiplier => partyBuffMultiplier;

    public enum WeaponType { Melee, Ranged }
    private WeaponType currentWeaponType = WeaponType.Melee;

    public int PotionCount => (!Object || !Object.IsValid) ? 0 : Object.HasInputAuthority ? NetworkPotionCount : NetworkPotionCount;
    public int PotionLevel => (!Object || !Object.IsValid) ? 1 : Object.HasInputAuthority ? NetworkPotionLevel : NetworkPotionLevel;

    public event Action<float> OnHealthChanged;
    public event Action<float> OnNetworkHealthChanged;
    public event Action OnPlayerHit;
    public event Action OnPlayerDeath;
    public event Action OnPlayerRevive;
    public event Action<int> OnLevelChanged;
    public event Action<float> OnXPChanged;
    public event Action<int> OnCoinsChanged;
    public event Action<WeaponType> OnWeaponChanged;
    public event System.Action<int> OnPotionCountChanged;

    private float lastSaveRequest = 0f;
    private const float SAVE_DEBOUNCE_TIME = 2f;
    private bool hasPendingSave = false;

    private async Task RequestSave(bool immediate = false)
    {
        if (!Object.HasInputAuthority) return;
        float currentTime = Time.time;
        if (immediate)
        {
            try
            {
                await SaveStats();
                hasPendingSave = false;
                lastSaveRequest = currentTime;
            }
            catch (System.Exception) { }
            return;
        }
        hasPendingSave = true;
        if (currentTime - lastSaveRequest >= SAVE_DEBOUNCE_TIME)
        {
            lastSaveRequest = currentTime;
            try
            {
                await SaveStats();
                hasPendingSave = false;
            }
            catch (System.Exception) { }
        }
    }

    public float CurrentAccuracy
    {
        get
        {
            float baseAccuracy = 1f;
            var buffSystem = GetComponent<TemporaryBuffSystem>();
            if (buffSystem != null) baseAccuracy *= buffSystem.GetCurrentAccuracyMultiplier();
            return Mathf.Clamp(baseAccuracy, 0f, 1f);
        }
    }

    public bool RollAccuracyCheck()
    {
        return UnityEngine.Random.value <= CurrentAccuracy;
    }

    public float CurrentHP
    {
        get
        {
            if (NetworkCurrentHP < 0) return MaxHP;
            return NetworkCurrentHP;
        }
    }

    public void SetHealthOnServer(float newHP)
    {
        if (!Object.HasStateAuthority) return;
        float clampedHP = Mathf.Clamp(newHP, 0, MaxHP);
        NetworkCurrentHP = clampedHP;
        OnHealthChanged?.Invoke(clampedHP);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestInitializeHPRPC(float requestedHP)
    {
        if (!Runner.IsServer) return;
        NetworkCurrentHP = MaxHP;
    }

    public float MaxHP
    {
        get
        {
            float baseHP = statsData.hp.GetValueAtLevel(CurrentLevel);
            if (equipmentStats.TryGetValue(StatType.Health, out float healthBonus)) baseHP += healthBonus;
            if (classSystem != null)
            {
                ClassType currentClass = classSystem.NetworkPlayerClass;
                if (currentClass == ClassType.Warrior && CurrentLevel >= 5) baseHP += statsData.hp.GetValueAtLevel(CurrentLevel) * 0.20f;
            }
            return baseHP;
        }
    }

    public int CurrentLevel
    {
        get
        {
            return !Object.HasInputAuthority ? NetworkCurrentLevel : _currentLevel;
        }
        set
        {
            _currentLevel = Mathf.Clamp(value, 1, statsData.maxLevel);
            if (Object && Object.HasInputAuthority) SyncLevelToServerRPC(_currentLevel);
            RecalculateStats();
        }
    }

    public float CurrentXP
    {
        get => _currentXP;
        set
        {
            _currentXP = Mathf.Max(0, value);
            if (Object && Object.HasInputAuthority) NetworkCurrentXP = _currentXP;
            OnXPChanged?.Invoke(_currentXP);
            CheckLevelUp();
        }
    }

    public int Coins
    {
        get
        {
            // Server (StateAuthority) NetworkCoins'den okur, Client (InputAuthority) local'den okur
            if (Object && Object.HasStateAuthority && !Object.HasInputAuthority)
            {
                return NetworkCoins;
            }
            return _coins;
        }
        set
        {
            int oldValue = _coins;
            _coins = Mathf.Max(0, value);

            bool canWriteNetwork = Object && Object.HasInputAuthority;
            if (canWriteNetwork)
            {
                NetworkCoins = _coins;
            }
            else
            {
            }

            OnCoinsChanged?.Invoke(_coins);
            if (oldValue != _coins)
            {
                #pragma warning disable CS4014
                RequestSave();
                #pragma warning restore CS4014
            }
        }
    }

    public float BaseDamage => _baseDamage;
    public float BaseArmor => _baseArmor;
    public float BaseCriticalChance => _baseCriticalChance;
    public float MoveSpeed
    {
        get
        {
            float baseSpeed = _moveSpeed;
            var buffSystem = GetComponent<TemporaryBuffSystem>();
            if (buffSystem != null)
            {
                baseSpeed *= buffSystem.GetCurrentSpeedMultiplier();
                baseSpeed *= buffSystem.GetCurrentSlowMultiplier();
            }
            return baseSpeed;
        }
    }
    public float FinalDamage => CalculateFinalDamage();
    public float FinalAttackSpeed => CalculateFinalAttackSpeed();
    public float FinalCriticalChance => CalculateFinalCriticalChance();

    public void ConsumePotionForCraft()
    {
        if (!Object.HasInputAuthority) return;
        if (NetworkPotionCount > 0)
        {
            NetworkPotionCount--;
            OnPotionCountChanged?.Invoke(NetworkPotionCount);
            RequestSyncPotionToServerRPC(NetworkPotionCount, NetworkPotionLevel);
        }
    }

    public float TotalHealthRegen
    {
        get
        {
            float totalRegen = 0f;
            if (equipmentStats.TryGetValue(StatType.HealthRegen, out float itemRegen)) totalRegen += itemRegen;
            if (classSystem != null && classSystem.NetworkPlayerClass == ClassType.Warrior && CurrentLevel >= 30) totalRegen += MaxHP * 0.01f;
            return totalRegen;
        }
    }

    public bool IsDead
    {
        get
        {
            var deathSystem = GetComponent<DeathSystem>();
            return deathSystem != null ? deathSystem.GetSafeDeathStatus() : false;
        }
    }

    private void Awake()
    {
        try
        {
            statsData = Resources.Load<PlayerStatsData>("PlayerStatsData");
            if (statsData == null) return;
            deathSystem = GetComponent<DeathSystem>();
            weaponSystem = GetComponent<WeaponSystem>();
            character4D = GetComponent<Character4D>();
            classSystem = GetComponent<ClassSystem>();
            StoreOriginalColors();
            SetupWeaponSystem();
            currentWeaponType = WeaponType.Melee;
        }
        catch (System.Exception) { }
    }

    public override void Spawned()
    {
        cachedInputAuthority = Object.InputAuthority;
        if (!Object.HasInputAuthority)
        {
            isInitialized = true;
            lastKnownNetworkHP = MaxHP;
            lastKnownPotionCount = NetworkPotionCount;
            return;
        }
        lastKnownPotionCount = NetworkPotionCount;
        if (LocalPlayerManager.Instance != null) LocalPlayerManager.Instance.SetLocalPlayer(this);
        if (classSystem != null)
        {
            classSystem.OnClassChanged += OnPlayerClassChanged;
            ClassType currentClass = classSystem.NetworkPlayerClass;
            if (currentClass != ClassType.None) OnPlayerClassChanged(currentClass);
        }
        if (PlayerManager.Instance != null) PlayerManager.Instance.RegisterPlayer(Object.InputAuthority, transform, this);
        RequestInitializeHPRPC(MaxHP);
        StartCoroutine(InitializeAsync());
        StartCoroutine(DelayedNicknameSync());
    }



    private void OnPlayerClassChanged(ClassType newClass)
    {
        if (!Object.IsValid) return;
        RecalculateStats();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && isInitialized)
        {
            if (NetworkCurrentHP < MaxHP && NetworkCurrentHP > 0)
            {
                float totalRegen = 0f;
                if (equipmentStats.TryGetValue(StatType.HealthRegen, out float itemHealthRegen)) totalRegen += itemHealthRegen;
                if (classSystem != null && classSystem.NetworkPlayerClass == ClassType.Warrior && CurrentLevel >= 30) totalRegen += MaxHP * 0.01f;
                if (totalRegen > 0f)
                {
                    float regenAmount = totalRegen * Runner.DeltaTime;
                    NetworkCurrentHP = Mathf.Min(MaxHP, NetworkCurrentHP + regenAmount);
                }
            }
        }
        if (Object.HasInputAuthority && isInitialized)
        {
            if (Time.time >= lastSaveTime + (SAVE_INTERVAL * 3f))
            {
                lastSaveTime = Time.time;
                #pragma warning disable CS4014
                RequestSave();
                #pragma warning restore CS4014
            }
            if (hasPendingSave && Time.time - lastSaveRequest >= SAVE_DEBOUNCE_TIME)
            {
                #pragma warning disable CS4014
                RequestSave();
                #pragma warning restore CS4014
            }
        }
    }

    public void RefreshOriginalColors()
    {
        if (!Object.HasInputAuthority || flashCoroutine != null) return;
        originalColors.Clear();
        StoreOriginalColors();
    }

    public override void Render()
    {
        if (Mathf.Abs(lastKnownNetworkHP - NetworkCurrentHP) > 0.1f)
        {
            lastKnownNetworkHP = NetworkCurrentHP;
            OnNetworkHealthChanged?.Invoke(NetworkCurrentHP);
        }
        if (lastKnownPotionCount != NetworkPotionCount)
        {
            lastKnownPotionCount = NetworkPotionCount;
            OnPotionCountChanged?.Invoke(NetworkPotionCount);
        }
        if (NetworkIsFlashing && flashCoroutine == null) StartFlashEffect();
        else if (!NetworkIsFlashing && flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
            RestoreOriginalColors();
        }
    }

    private IEnumerator InitializeAsync()
    {
        yield return new WaitForEndOfFrame();
        InitializeStatsAsync();
        SetupWeaponSystem();
    }

    private async void InitializeStatsAsync()
    {
        await InitializeStats();
    }

    private async Task InitializeStats()
    {
        // PROFESSIONAL MMORPG PATTERN: Data zaten session'da hazır durumda
        // Firebase loading Character Select/Lobby'de yapılmış olmalı

        try
        {
            if (Object == null || !Object.IsValid)
            {
                return;
            }

            GameObject gameUI = GameObject.Find("GameUI");
            if (gameUI != null && !gameUI.activeInHierarchy) gameUI.SetActive(true);

            // CRITICAL: Session validation - data hazır olmalı
            if (PlayerDataSession.Instance == null || !PlayerDataSession.Instance.IsDataReady)
            {


                string errorMessage = "Oyuncu verisi hazır değil.\nLütfen karakter seçim ekranına dönün.";
                ShowConnectionError(errorMessage);

                // Kick player - data olmadan spawn etmemeli
                if (Object.HasInputAuthority)
                {

                    // Disconnect and return to login
                    StartCoroutine(DisconnectAndReturnToLogin("Oyuncu verisi yüklenemedi"));
                }

                return;
            }

            // Data session'dan yükle (instant, no blocking!)
            await LoadPlayerDataFromSession();
            RecalculateStats();

            if (InfoPanelManager.Instance != null) InfoPanelManager.Instance.Initialize(this);

        }
        catch (Exception)
        {
            // CRITICAL: Exception during stats initialization

            string errorMessage = "Oyuncu verileri yüklenirken hata oluştu.\nLütfen oyunu yeniden başlatın.";
            ShowConnectionError(errorMessage);
        }
    }

    private void ShowConnectionError(string message)
    {
        // Log critical error prominently// TODO: In production, you should show a UI popup here
        // For example, you could create a simple error panel or use a notification system
        // Example implementation:
        // if (UIManager.Instance != null)
        // {
        //     UIManager.Instance.ShowErrorPopup(message);
        // }

        // For now, the error is logged to Unity console and will be visible in device logs
        // Players experiencing this should restart the game
    }

    /// <summary>
    /// Disconnect player gracefully and return to login screen
    /// </summary>
    private IEnumerator DisconnectAndReturnToLogin(string reason)
    {

        // Show error UI if available
        if (UIManager.Instance != null)
        {
            // TODO: Show error popup
            // UIManager.Instance.ShowErrorPopup(reason);
        }

        // Wait a moment for error to be visible
        yield return new WaitForSeconds(1f);

        // Shutdown Photon runner if available
        if (Runner != null && Runner.IsRunning)
        {
            Runner.Shutdown();
            yield return new WaitForSeconds(0.5f);
        }

        // Clear session data
        if (PlayerDataSession.Instance != null)
        {
            PlayerDataSession.Instance.ClearSession();
        }

        // Return to login scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("Login");
    }

    private IEnumerator DelayedNicknameSync()
    {
        yield return new WaitForSeconds(0.5f);
        try
        {
            string nickname = GetPlayerDisplayName();
            if (!string.IsNullOrEmpty(nickname) && Object != null && Object.IsValid) SyncPlayerNicknameRPC(nickname);
        }
        catch (System.Exception) { }
    }

    private IEnumerator NotifyUIAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        GameObject gameUI = GameObject.Find("GameUI");
        if (gameUI == null || !gameUI.activeInHierarchy)
        {
            if (gameUI != null)
            {
                gameUI.SetActive(true);
                yield return new WaitForSeconds(0.2f);
            }
        }
        if (UIManager.Instance != null) UIManager.Instance.Initialize(this);
        else
        {
            UIManager uiManager = FindFirstObjectByType<UIManager>();
            if (uiManager != null) uiManager.Initialize(this);
        }
        if (InfoPanelManager.Instance != null) InfoPanelManager.Instance.Initialize(this);
    }

    private void SetupWeaponSystem()
    {
        if (weaponSystem != null) weaponSystem.OnWeaponChanged += HandleWeaponChanged;
    }

    private void SetDefaultStats()
    {
        CurrentLevel = 1;
        CurrentXP = 0;
        Coins = 0;
        if (Object && Object.HasInputAuthority)
        {
            float targetHP = statsData.hp.GetValueAtLevel(1);
            RequestInitializeHPRPC(targetHP);
        }
        RecalculateStats();
    }

    public void RecalculateStats()
    {
        if (classSystem == null) classSystem = GetComponent<ClassSystem>();
        _baseDamage = statsData.physicalDamage.GetValueAtLevel(CurrentLevel);
        _baseArmor = statsData.armor.GetValueAtLevel(CurrentLevel);
        _baseCriticalChance = statsData.criticalChance.GetValueAtLevel(CurrentLevel);
        _moveSpeed = statsData.moveSpeed.GetValueAtLevel(CurrentLevel);
        _baseAttackSpeed = statsData.attackSpeed.GetValueAtLevel(CurrentLevel);
        _baseCriticalMultiplier = 1.5f;
        if (equipmentStats.TryGetValue(StatType.PhysicalDamage, out float damage)) _baseDamage += damage;
        if (equipmentStats.TryGetValue(StatType.Armor, out float armor)) _baseArmor += armor;
        if (equipmentStats.TryGetValue(StatType.CriticalChance, out float critChance)) _baseCriticalChance += critChance;
        if (equipmentStats.TryGetValue(StatType.MoveSpeed, out float moveSpeed)) _moveSpeed += moveSpeed;
        if (equipmentStats.TryGetValue(StatType.AttackSpeed, out float attackSpeed)) _baseAttackSpeed += attackSpeed;
        if (equipmentStats.TryGetValue(StatType.CriticalMultiplier, out float critMultiplier)) _baseCriticalMultiplier += critMultiplier;
        ApplyClassPassiveBonuses();
        if (Object.HasInputAuthority) SyncAllStatsRPC(_baseDamage, _baseArmor, _baseCriticalChance, _moveSpeed, _baseAttackSpeed);
    }

    private void ApplyClassPassiveBonuses()
    {
        if (classSystem == null) return;
        ClassType currentClass = classSystem.NetworkPlayerClass;
        int currentLevel = CurrentLevel;
        switch (currentClass)
        {
            case ClassType.Warrior:
                int armorBonus = currentLevel * 2;
                _baseArmor += armorBonus;
                ApplyWarriorMilestones(currentLevel);
                break;
            case ClassType.Ranger:
                int critBonus = currentLevel * 2;
                _baseCriticalChance += critBonus;
                ApplyRangerMilestones(currentLevel);
                break;
            case ClassType.Rogue:
                float attackSpeedBonus = Mathf.Pow(currentLevel, 0.2f);
                _baseAttackSpeed += attackSpeedBonus;
                ApplyRogueMilestones(currentLevel);
                break;
            case ClassType.None:
            default:
                break;
        }
    }

    private void ApplyWarriorMilestones(int level)
    {
        if (level >= 10) _baseArmor += 50;
        if (level >= 20) _baseDamage *= 1.15f;
    }

    private void ApplyRangerMilestones(int level)
    {
        if (level >= 10)
        {
            if (equipmentStats.ContainsKey(StatType.ProjectileSpeed)) equipmentStats[StatType.ProjectileSpeed] += 40f;
            else equipmentStats[StatType.ProjectileSpeed] = 40f;
        }
        if (level >= 20) _baseCriticalChance += 10f;
        if (level >= 30) _baseAttackSpeed *= 1.20f;
    }

    private void ApplyRogueMilestones(int level)
    {
        if (level >= 5) _moveSpeed *= 1.25f;
        if (level >= 10) _baseAttackSpeed *= 1.15f;
        if (level >= 20) _baseCriticalMultiplier += 0.25f;
        if (level >= 30) _baseAttackSpeed *= 1.25f;
    }

    private void CheckLevelUp()
    {
        if (!Object.HasInputAuthority) return;
        float requiredXP = statsData.xpRequirement.GetValueAtLevel(CurrentLevel);
        bool leveledUp = false;
        while (CurrentXP >= requiredXP && CurrentLevel < statsData.maxLevel)
        {
            CurrentXP -= requiredXP;
            CurrentLevel++;
            RequestSetHPRPC(MaxHP);
            requiredXP = statsData.xpRequirement.GetValueAtLevel(CurrentLevel);
            leveledUp = true;
        }
        if (leveledUp)
        {
            OnLevelChanged?.Invoke(CurrentLevel);
            #pragma warning disable CS4014
            RequestSave(immediate: true);
            #pragma warning restore CS4014
            if (Object != null && Object.HasInputAuthority) SyncLevelToServerRPC(CurrentLevel);
            if (CurrentLevel == 2) StartCoroutine(DelayedClassSelection());
            var skillSystem = GetComponent<SkillSystem>();
            if (skillSystem != null) skillSystem.OnPlayerLevelUp(CurrentLevel);
        }
    }

    private System.Collections.IEnumerator DelayedClassSelection()
    {
        yield return new WaitForSeconds(0.5f);
        var classSystem = GetComponent<ClassSystem>();
        if (classSystem != null && classSystem.CanSelectClass())
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowClassSelectionPanel();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RequestResetLevelRPC()
    {
        if (!Runner.IsServer) return;
        NetworkCurrentLevel = 1;
        NetworkCurrentXP = 0;
        NetworkCurrentHP = MaxHP;
        NetworkCoins = 0;
        _currentLevel = 1;
        _currentXP = 0;
        _coins = 0;
        RecalculateStats();
        var classSystem = GetComponent<ClassSystem>();
        if (classSystem != null) classSystem.ForceResetClass();
        SyncResetLevelRPC(true);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SyncResetLevelRPC(bool resetClass = false)
    {
        _currentLevel = NetworkCurrentLevel;
        _currentXP = NetworkCurrentXP;
        _coins = NetworkCoins;
        OnLevelChanged?.Invoke(NetworkCurrentLevel);
        OnXPChanged?.Invoke(NetworkCurrentXP);
        OnCoinsChanged?.Invoke(NetworkCoins);
        RecalculateStats();
        if (resetClass)
        {
            var classSystem = GetComponent<ClassSystem>();
            if (classSystem != null)
            {
                if (Runner.IsServer) classSystem.NetworkPlayerClass = ClassType.None;
                classSystem.TriggerClassChangedEvent(ClassType.None);
                if (Object.HasInputAuthority)
                {
                    string nickname = GetPlayerDisplayName();
                    string classKey = $"PlayerClass_{nickname}";
                    PlayerPrefs.DeleteKey(classKey);
                    PlayerPrefs.Save();
                    SaveClassAfterDelay(classSystem);
                }
                var skillSystem = GetComponent<SkillSystem>();
                if (skillSystem != null)
                {
                    if (Object.HasInputAuthority) skillSystem.OnClassChanged(ClassType.None);
                    else if (Runner.IsServer) skillSystem.ClearSkillsDirectly();
                }
            }
        }
    }

    private async void SaveClassAfterDelay(ClassSystem classSystem)
    {
        await Task.Delay(100);
        string nickname = GetPlayerDisplayName();
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
        {
            try { await FirebaseManager.Instance.SavePlayerClass(nickname, ClassType.None); }
            catch (Exception) { }
        }
    }

    public void DebugResetLevel()
    {
        if (!Object.HasInputAuthority) return;
        RequestResetLevelRPC();
    }

    public void GainXP(float amount)
    {
        if (!Object.HasInputAuthority) return;
        float bonusXP = amount * (1f + partyXpBonus);
        float finalXP = bonusXP * statsData.xpGainMultiplier.GetValueAtLevel(CurrentLevel);
        CurrentXP += finalXP;
        #pragma warning disable CS4014
        RequestSave();
        #pragma warning restore CS4014
    }

    public void AddCoins(int amount)
    {
        if (!Object.HasInputAuthority) return;
        int finalAmount = Mathf.FloorToInt(amount * statsData.coinGainMultiplier.GetValueAtLevel(CurrentLevel));
        Coins += finalAmount;
        #pragma warning disable CS4014
        RequestSave();
        #pragma warning restore CS4014
    }

    public float GetRequiredXPForNextLevel()
    {
        return statsData.xpRequirement.GetValueAtLevel(CurrentLevel);
    }

    public float GetNetworkMaxHP()
    {
        return statsData.hp.GetValueAtLevel(NetworkCurrentLevel);
    }

    private float CalculateFinalDamage()
    {
        float finalDamage = (_baseDamage + _itemDamageBonus) * _weaponDamageMultiplier;
        var buffSystem = GetComponent<TemporaryBuffSystem>();
        if (buffSystem != null) finalDamage *= buffSystem.GetCurrentDamageDebuffMultiplier();
        return finalDamage;
    }

    private float CalculateFinalAttackSpeed()
    {
        float baseAttackSpeed = _baseAttackSpeed * _weaponAttackSpeedModifier;
        var buffSystem = GetComponent<TemporaryBuffSystem>();
        if (buffSystem != null) baseAttackSpeed *= buffSystem.GetCurrentAttackSpeedMultiplier();
        return baseAttackSpeed;
    }

    private float CalculateFinalCriticalChance()
    {
        return (_baseCriticalChance + _itemCriticalChanceBonus) * _weaponCriticalModifier;
    }

    public void TakeDamage(float damage, bool isPVPDamage = false)
    {
        if (!Object.HasStateAuthority) return;
        var deathSystem = GetComponent<DeathSystem>();
        if (deathSystem != null && deathSystem.IsDead) return;
        var tempBuff = GetComponent<TemporaryBuffSystem>();
        if (tempBuff != null && tempBuff.IsInvulnerable()) return;
        if (tempBuff != null) damage *= tempBuff.GetCurrentDamageReductionMultiplier();
        float damageReduction = _baseArmor / (100f + _baseArmor);
        float finalDamage = damage * (1f - damageReduction);
        NetworkCurrentHP = Mathf.Max(0, NetworkCurrentHP - finalDamage);
        OnPlayerHit?.Invoke();
        if (!isPVPDamage)
        {
            TriggerFlashEffectFromServer();
            var playerController = GetComponent<PlayerController>();
            if (playerController != null) playerController.TriggerHitAnimationFromServer();
        }
        if (NetworkCurrentHP <= 0 && deathSystem != null && !deathSystem.IsDead)
        {
            OnPlayerDeath?.Invoke();
            try { TriggerDeathRPC(); }
            catch (System.Exception) { }
        }
    }

    public float GetProjectileSpeedMultiplier()
    {
        float baseMultiplier = 1f;
        if (equipmentStats.TryGetValue(StatType.ProjectileSpeed, out float projectileSpeedBonus)) baseMultiplier += projectileSpeedBonus / 100f;
        return baseMultiplier;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void TriggerDeathRPC()
    {
        DeathSystem deathSystem = GetComponent<DeathSystem>();
        if (deathSystem != null) deathSystem.OnDeath();
    }

    private void StartFlashEffect()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
            RestoreOriginalColors();
        }
        if (originalColors.Count == 0 || !IsCurrentActiveCharacterInCache())
        {
            originalColors.Clear();
            StoreOriginalColors();
        }
        flashCoroutine = StartCoroutine(FlashEffectCoroutine());
    }

    private IEnumerator FlashEffectCoroutine()
    {
        ApplyFlashColor();
        yield return new WaitForSeconds(flashDuration);
        RestoreOriginalColors();
        yield return new WaitForEndOfFrame();
        if (originalColors.Count > 0)
        {
            bool anyFlashColorRemaining = false;
            foreach (var kvp in originalColors)
            {
                if (kvp.Key != null && kvp.Key.enabled &&
                    Mathf.Approximately(kvp.Key.color.r, damageFlashColor.r) &&
                    Mathf.Approximately(kvp.Key.color.g, damageFlashColor.g) &&
                    Mathf.Approximately(kvp.Key.color.b, damageFlashColor.b))
                {
                    anyFlashColorRemaining = true;
                    break;
                }
            }
            if (anyFlashColorRemaining) RestoreOriginalColors();
        }
        if (Runner != null && Runner.IsServer) NetworkIsFlashing = false;
        flashCoroutine = null;
    }

    private void ApplyFlashColor()
    {
        if (originalColors == null || originalColors.Count == 0) return;
        foreach (var kvp in originalColors)
        {
            if (kvp.Key != null && kvp.Key.enabled) kvp.Key.color = damageFlashColor;
        }
    }

    private void RestoreOriginalColors()
    {
        if (originalColors == null || originalColors.Count == 0) return;
        foreach (var kvp in originalColors)
        {
            if (kvp.Key != null && kvp.Key.enabled) kvp.Key.color = kvp.Value;
        }
        if (character4D != null) StartCoroutine(RestoreMaterialsAfterFlash());
    }

    public void RestoreHealth(float amount)
    {
        if (!Object.HasStateAuthority) return;
        float newHP = Mathf.Min(MaxHP, NetworkCurrentHP + amount);
        NetworkCurrentHP = newHP;
        OnHealthChanged?.Invoke(NetworkCurrentHP);
    }

    public float ApplyDamageVariation(float damage)
    {
        float variation = UnityEngine.Random.Range(0.8f, 1.2f);
        return damage * variation;
    }

    public float GetDamageAmount(bool isCritical = false)
    {
        float damage = FinalDamage;
        var buffSystem = GetComponent<TemporaryBuffSystem>();
        if (buffSystem != null) damage *= buffSystem.GetCurrentDamageDebuffMultiplier();
        if (isCritical) damage *= (_baseCriticalMultiplier + _itemCriticalMultiplierBonus);
        return ApplyDamageVariation(damage);
    }

    public async Task LoseXP(float amount)
    {
        if (!Object.HasInputAuthority) return;
        CurrentXP = Mathf.Max(0, CurrentXP - amount);
        await RequestSave(immediate: true);
    }

    private void HandleWeaponChanged(WeaponType newWeaponType)
    {
        currentWeaponType = newWeaponType;
        UpdateWeaponModifiers();
        if (Object.HasInputAuthority)
        {
            SyncWeaponTypeRPC((int)currentWeaponType);
            OnWeaponChanged?.Invoke(currentWeaponType);
        }
    }

    private void UpdateWeaponModifiers()
    {
        switch (currentWeaponType)
        {
            case WeaponType.Melee:
                _weaponDamageMultiplier = 1.2f;
                _weaponAttackSpeedModifier = 1.0f;
                _weaponCriticalModifier = 1.5f;
                break;
            case WeaponType.Ranged:
                _weaponDamageMultiplier = 0.8f;
                _weaponAttackSpeedModifier = 0.7f;
                _weaponCriticalModifier = 1.8f;
                break;
        }
        RecalculateStats();
    }

    public void UsePotion()
    {
        if (!Object.HasInputAuthority || NetworkPotionCount <= 0) return;
        RequestUsePotionRPC();
        #pragma warning disable CS4014
        RequestSave();
        #pragma warning restore CS4014
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestUsePotionRPC()
    {
        if (!Runner.IsServer || NetworkPotionCount <= 0) return;
        float baseHeal = NetworkPotionLevel * 10;
        float percentHeal = MaxHP * 0.1f;
        float totalHeal = baseHeal + percentHeal;
        RestoreHealth(totalHeal);
        NetworkPotionCount--;
        ShowHealingVFXRPC();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ShowHealingVFXRPC()
    {
        GameObject healingPrefab = Resources.Load<GameObject>("VFX/Healing");
        if (healingPrefab != null)
        {
            GameObject vfx = Instantiate(healingPrefab, transform.position, Quaternion.identity);
            vfx.transform.SetParent(transform);
            Destroy(vfx, 2f);
        }
    }

    public void AddPotion(int count = 1, bool isLevelUpgrade = false)
    {
        if (!Object.HasInputAuthority) return;
        if (isLevelUpgrade) NetworkPotionLevel = count;
        else NetworkPotionCount += count;
        OnPotionCountChanged?.Invoke(NetworkPotionCount);
        RequestSyncPotionToServerRPC(NetworkPotionCount, NetworkPotionLevel);
    }

    public void TriggerReviveEvent()
    {
        OnPlayerRevive?.Invoke();
    }

    private void UpdatePotionUI()
    {
        OnPotionCountChanged?.Invoke(NetworkPotionCount);
    }

    public void Revive()
    {
        if (!Object.HasInputAuthority) return;
        float targetHP = MaxHP * 0.5f;
        RequestSetHPRPC(targetHP);
        OnPlayerRevive?.Invoke();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestSetHPRPC(float newHP)
    {
        if (Runner.IsServer)
        {
            NetworkCurrentHP = newHP;
            OnHealthChanged?.Invoke(NetworkCurrentHP);
        }
    }

    private bool IsCurrentActiveCharacterInCache()
    {
        if (character4D == null || character4D.Active == null || originalColors.Count == 0) return false;
        Character activeCharacter = character4D.Active;
        SpriteRenderer[] currentRenderers = activeCharacter.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var cachedRenderer in originalColors.Keys)
        {
            if (cachedRenderer != null && System.Array.Exists(currentRenderers, r => r == cachedRenderer)) return true;
        }
        return false;
    }

    private void StoreOriginalColors()
    {
        originalColors.Clear();
        if (character4D != null && character4D.Active != null)
        {
            Character activeCharacter = character4D.Active;
            SpriteRenderer[] childRenderers = activeCharacter.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var renderer in childRenderers)
            {
                if (renderer != null && !originalColors.ContainsKey(renderer)) originalColors[renderer] = renderer.color;
            }
        }
    }

    public void TriggerFlashEffectFromServer()
    {
        if (!Runner.IsServer) return;
        if (Object != null && Object.IsValid)
        {
            var tempBuff = GetComponent<TemporaryBuffSystem>();
            if (tempBuff != null && tempBuff.Object != null && tempBuff.Object.IsValid)
            {
                try { if (tempBuff.IsInvulnerable()) return; }
                catch { }
            }
        }
        NetworkIsFlashing = true;
        NetworkFlashStartTime = (float)Runner.SimulationTime;
        TriggerFlashEffectRPC();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void TriggerFlashEffectRPC()
    {
        StartFlashEffect();
    }

    private IEnumerator RestoreMaterialsAfterFlash()
    {
        yield return new WaitForEndOfFrame();
        if (character4D != null)
        {
            Character[] characters = { character4D.Front, character4D.Back, character4D.Left, character4D.Right };
            foreach (var character in characters)
            {
                if (character != null)
                {
                    try
                    {
                        var renderers = character.ArmorRenderers.ToList();
                        renderers.Add(character.HairRenderer);
                        renderers.Add(character.PrimaryWeaponRenderer);
                        renderers.Add(character.SecondaryWeaponRenderer);
                        foreach (var renderer in renderers)
                        {
                            if (renderer != null) renderer.sharedMaterial = renderer.color == Color.white ? character.DefaultMaterial : character.EquipmentPaintMaterial;
                        }
                    }
                    catch (System.Exception) { }
                }
            }
        }
    }

    public void UpdateEquipmentStats(Dictionary<StatType, float> newStats)
    {
        if (!Object.HasInputAuthority) return;
        float oldMaxHP = MaxHP;
        equipmentStats = newStats;
        RecalculateStats();
        float newMaxHP = MaxHP;
        bool maxHPChanged = Mathf.Abs(oldMaxHP - newMaxHP) > 0.1f;
        bool needsHPSync = NetworkCurrentHP < 0 || NetworkCurrentHP > newMaxHP;
        if ((maxHPChanged || needsHPSync) && newMaxHP > 0)
        {
            if (NetworkCurrentHP > 0 && oldMaxHP > 0)
            {
                float currentHP = Mathf.Min(NetworkCurrentHP, oldMaxHP);
                float healthPercentage = currentHP / oldMaxHP;
                float newHP = healthPercentage * newMaxHP;
                RequestSetHPRPC(newHP);
            }
            else RequestSetHPRPC(newMaxHP);
        }
        SyncEquipmentStatsRPC(EquipmentStatsToByte(equipmentStats));
    }

    private byte[] EquipmentStatsToByte(Dictionary<StatType, float> stats)
    {
        using (var ms = new System.IO.MemoryStream())
        {
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                writer.Write(stats.Count);
                foreach (var stat in stats)
                {
                    writer.Write((int)stat.Key);
                    writer.Write(stat.Value);
                }
            }
            return ms.ToArray();
        }
    }

    private Dictionary<StatType, float> ByteToEquipmentStats(byte[] data)
    {
        var stats = new Dictionary<StatType, float>();
        using (var ms = new System.IO.MemoryStream(data))
        {
            using (var reader = new System.IO.BinaryReader(ms))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    StatType type = (StatType)reader.ReadInt32();
                    float value = reader.ReadSingle();
                    stats[type] = value;
                }
            }
        }
        return stats;
    }

    private void SetDefaultEquipment()
    {
        if (!Object.HasInputAuthority) return;
        try
        {
            var equipSystem = GetComponent<EquipmentSystem>();
            if (equipSystem == null || ItemDatabase.Instance == null) return;
            string defaultMeleeWeaponId = "MeleeWeapon2H.Baguette";
            string defaultRangedWeaponId = "Bow.BattleBow";
            ItemData meleeWeapon = ItemDatabase.Instance.GetItemById(defaultMeleeWeaponId);
            if (meleeWeapon != null)
            {
                ItemData meleeWeaponCopy = meleeWeapon.CreateCopy();
                equipSystem.EquipDefaultItem(meleeWeaponCopy);
            }
            ItemData rangedWeapon = ItemDatabase.Instance.GetItemById(defaultRangedWeaponId);
            if (rangedWeapon != null)
            {
                ItemData rangedWeaponCopy = rangedWeapon.CreateCopy();
                equipSystem.EquipDefaultItem(rangedWeaponCopy);
            }
        }
        catch (Exception) { }
    }

    private void MarkDefaultEquipmentGiven()
    {
        hasReceivedDefaultEquipment = true;
    }

    public void NotifyNewPlayerOfEquipmentRPC()
    {
        if (Object == null || !Object.IsValid) return;
        var playerStats = GetComponent<PlayerStats>();
        if (playerStats != null) StartCoroutine(playerStats.BroadcastEquipmentStateOnJoin());
    }

    public IEnumerator BroadcastEquipmentStateOnJoin()
    {
        yield return new WaitForSeconds(0.5f);
        var equipSystem = GetComponent<EquipmentSystem>();
        if (equipSystem != null)
        {
            equipSystem.BroadcastCurrentEquipment();
            yield return new WaitForSeconds(0.2f);
            equipSystem.RefreshEquipmentVisuals();
        }
    }

    public void UpdatePartyId(int partyId)
    {
        CurrentPartyId = partyId;
        if (partyId != -1) UpdatePartyBonuses(0.2f);
        else UpdatePartyBonuses(0f);
    }

    public void UpdatePartyBonuses(float xpBonus)
    {
        partyXpBonus = xpBonus;
        RecalculateStats();
    }

    public bool IsInParty() => CurrentPartyId != -1;
    public int GetPartyId() => CurrentPartyId;

    public async Task SaveStats()
    {
        if (!Object.HasInputAuthority || !FirebaseManager.Instance.IsReady) return;
        try
        {
            string nickname = GetPlayerDisplayName();
            if (string.IsNullOrEmpty(nickname) || nickname == "Player") return;
            var equipSystem = GetComponent<EquipmentSystem>();
            var invSystem = GetComponent<InventorySystem>();
            var craftSystem = GetComponent<CraftInventorySystem>();
            var classSystem = GetComponent<ClassSystem>();
            var equipmentData = equipSystem?.GetEquipmentData() ?? new Dictionary<string, object>();
            var inventoryData = invSystem?.GetInventoryData() ?? new Dictionary<string, object>();
            var craftInventoryData = craftSystem?.GetCraftInventoryData() ?? new Dictionary<string, object>();
            var statsData = new Dictionary<string, object>
            {
                { "stats", new Dictionary<string, object>
                    {
                        { "level", CurrentLevel },
                        { "xp", CurrentXP },
                        { "maxHP", MaxHP },
                        { "lastUpdated", ServerValue.Timestamp },
                        { "moveSpeed", MoveSpeed },
                        { "criticalChance", BaseCriticalChance },
                        { "armor", BaseArmor },
                        { "attackPower", BaseDamage },
                        { "coins", Coins },
                        { "potionCount", NetworkPotionCount },
                        { "potionLevel", NetworkPotionLevel },
                        { "hasReceivedDefaultEquipment", hasReceivedDefaultEquipment }
                    }
                },
                { "equipment", equipmentData },
                { "inventory", inventoryData },
                { "craftInventory", craftInventoryData },
                { "classType", classSystem?.NetworkPlayerClass.ToString() ?? "None" }
            };
            await FirebaseManager.Instance.SaveUserData(this, statsData);
        }
        catch (Exception) { throw; }
    }

    /// <summary>
    /// PROFESSIONAL MMORPG PATTERN: Load from session cache (instant, no Firebase blocking)
    /// </summary>
    private async Task LoadPlayerDataFromSession()
    {
        try
        {
            // Get cached stats from session
            var savedStats = PlayerDataSession.Instance.GetCachedStats();

            if (savedStats == null)
            {
                throw new Exception("Session cache is null - critical error");
            }

            string nickname = GetPlayerDisplayName();

            // savedStats.Count > 0 means existing player, Count == 0 means new player
            if (savedStats.Count > 0)
            {
                // Existing player - load their data
                LoadStatsFromDictionary(savedStats);

                bool hasEquipment = false;
                if (savedStats.ContainsKey("equipment"))
                {
                    var equipSystem = GetComponent<EquipmentSystem>();
                    if (equipSystem != null)
                    {
                        var equipData = savedStats["equipment"] as Dictionary<string, object>;
                        if (equipData != null)
                        {
                            hasEquipment = equipData.Count > 0;
                            if (hasEquipment) LoadEquipmentData(equipData, equipSystem);
                        }
                    }
                }

                if (!hasEquipment)
                {
                    var equipSystem = GetComponent<EquipmentSystem>();
                    if (equipSystem != null) StartCoroutine(ClearVisualsAfterDelay(equipSystem));
                }

                bool hasReceivedDefaultEquipment = Convert.ToBoolean(savedStats.GetValueOrDefault("hasReceivedDefaultEquipment", false));
                if (!hasReceivedDefaultEquipment)
                {
                    SetDefaultEquipment();
                    MarkDefaultEquipmentGiven();
                }

                UpdatePotionUI();
            }
            else
            {
                // NEW PLAYER - This is the ONLY case where SetDefaultStats should be called
                SetDefaultStats();
                SetDefaultEquipment();
                MarkDefaultEquipmentGiven();
            }

            RecalculateStats();
            isInitialized = true;
            StartCoroutine(NotifyUIAfterDelay());
            await SaveStats();
        }
        catch (Exception)
        {
            // CRITICAL: Do NOT use SetDefaultStats here - it would cause data loss

            // Rethrow the exception so InitializeStats() can handle it
            throw;
        }
    }

    /// <summary>
    /// DEPRECATED: Old Firebase direct load method
    /// Kept for backwards compatibility but should not be used in spawn flow
    /// </summary>
    private void LoadStatsFromDictionary(Dictionary<string, object> data)
    {
        try
        {
            int loadedLevel = Convert.ToInt32(data.GetValueOrDefault("level", 1));
            CurrentLevel = loadedLevel;
            if (Object && Object.HasInputAuthority) RequestInitializeHPRPC(MaxHP);
            int loadedPotionCount = Convert.ToInt32(data.GetValueOrDefault("potionCount", 0));
            int loadedPotionLevel = Convert.ToInt32(data.GetValueOrDefault("potionLevel", 1));
            NetworkPotionCount = loadedPotionCount;
            NetworkPotionLevel = loadedPotionLevel;
            if (Object && Object.HasInputAuthority) RequestSyncPotionToServerRPC(loadedPotionCount, loadedPotionLevel);
            hasReceivedDefaultEquipment = Convert.ToBoolean(data.GetValueOrDefault("hasReceivedDefaultEquipment", false));
            CurrentXP = Convert.ToSingle(data.GetValueOrDefault("xp", 0f));
            Coins = Convert.ToInt32(data.GetValueOrDefault("coins", 0));
            _baseDamage = Convert.ToSingle(data.GetValueOrDefault("baseDamage", statsData.physicalDamage.baseValue));
            _baseArmor = Convert.ToSingle(data.GetValueOrDefault("baseArmor", statsData.armor.baseValue));
            _baseCriticalChance = Convert.ToSingle(data.GetValueOrDefault("baseCriticalChance", statsData.criticalChance.baseValue));
            _baseAttackSpeed = Convert.ToSingle(data.GetValueOrDefault("baseAttackSpeed", statsData.attackSpeed.baseValue));
            _moveSpeed = Convert.ToSingle(data.GetValueOrDefault("moveSpeed", statsData.moveSpeed.baseValue));
            if (data.ContainsKey("classType"))
            {
                string classString = data["classType"].ToString();
                if (System.Enum.TryParse<ClassType>(classString, out ClassType loadedClass))
                {
                    var classSystem = GetComponent<ClassSystem>();
                    if (classSystem != null && loadedClass != ClassType.None) StartCoroutine(LoadClassWithDelay(classSystem, loadedClass));
                }
            }
            if (data.ContainsKey("craftInventory"))
            {
                var craftSystem = GetComponent<CraftInventorySystem>();
                if (craftSystem != null)
                {
                    var craftDataObj = data["craftInventory"];
                    if (craftDataObj is Dictionary<string, object> craftData) StartCoroutine(LoadCraftInventoryWhenReady(craftSystem, craftData));
                }
            }
            if (data.ContainsKey("inventory"))
            {
                var invSystem = GetComponent<InventorySystem>();
                if (invSystem != null)
                {
                    var inventoryDataObj = data["inventory"];
                    if (inventoryDataObj is Dictionary<string, object> inventoryData) StartCoroutine(LoadInventoryWhenReady(invSystem, inventoryData));
                }
            }
            if (data.ContainsKey("equipment"))
            {
                var equipSystem = GetComponent<EquipmentSystem>();
                if (equipSystem != null)
                {
                    var equipDataObj = data["equipment"];
                    if (equipDataObj is Dictionary<string, object> equipData)
                    {
                        equipSystem.LoadEquipmentData(equipData);
                        StartCoroutine(RefreshEquipmentVisualsWithDelay(equipSystem));
                    }
                }
            }
        }
        catch (Exception) { throw new System.Exception("Critical stats loading failure"); }
    }

    private IEnumerator LoadClassWithDelay(ClassSystem classSystem, ClassType classType)
    {
        yield return new WaitForSeconds(1f);
        if (classSystem != null && Object.HasInputAuthority) classSystem.SelectClass(classType);
    }

    private IEnumerator LoadCraftInventoryWhenReady(CraftInventorySystem craftSystem, Dictionary<string, object> craftData)
    {
        int attempts = 0;
        while (ItemDatabase.Instance == null && attempts < 50)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
        if (ItemDatabase.Instance != null) craftSystem.LoadCraftInventoryData(craftData);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestSyncPotionToServerRPC(int count, int level)
    {
        if (!Runner.IsServer) return;
        NetworkPotionCount = count;
        NetworkPotionLevel = level;
    }

    private IEnumerator LoadInventoryWhenReady(InventorySystem invSystem, Dictionary<string, object> inventoryData)
    {
        int attempts = 0;
        while (ItemDatabase.Instance == null && attempts < 50)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
        if (ItemDatabase.Instance != null) invSystem.LoadInventoryData(inventoryData);
    }

    private IEnumerator ClearVisualsAfterDelay(EquipmentSystem equipSystem)
    {
        yield return new WaitForSeconds(0.5f);
        equipSystem.ClearEmptySlotVisuals();
    }

    private IEnumerator RefreshEquipmentVisualsWithDelay(EquipmentSystem equipSystem)
    {
        yield return new WaitForSeconds(0.2f);
        equipSystem.RefreshEquipmentVisuals();
        var character4D = GetComponent<Character4D>();
        if (character4D != null) character4D.Initialize();
        yield return new WaitForSeconds(0.2f);
        StoreOriginalColors();
    }

    private void LoadEquipmentData(Dictionary<string, object> equipData, EquipmentSystem equipSystem)
    {
        if (equipData.TryGetValue("MeleeWeapon2HId", out object meleeId) && meleeId is string MeleeWeapon2HId)
        {
            var item = ItemDatabase.Instance.GetItemById(MeleeWeapon2HId);
            if (item != null) equipSystem.EquipItem(item);
        }
        if (equipData.TryGetValue("CompositeWeaponId", out object rangedId) && rangedId is string CompositeWeaponId)
        {
            var item = ItemDatabase.Instance.GetItemById(CompositeWeaponId);
            if (item != null) equipSystem.EquipItem(item);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void SyncLevelToServerRPC(int newLevel)
    {
        if (!Runner.IsServer) return;
        NetworkCurrentLevel = newLevel;
        _currentLevel = newLevel;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void SyncPlayerNicknameRPC(string nickname)
    {
        try
        {
            NetworkPlayerNickname = nickname;
            if (UIManager.Instance != null)
            {
                NetworkObject netObj = GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsValid) UIManager.Instance.UpdatePlayerNameTag(netObj.Id.GetHashCode(), nickname, gameObject);
            }
        }
        catch (System.Exception) { }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void SyncNicknameToSpecificPlayerRPC(string nickname)
    {
        try
        {
            NetworkPlayerNickname = nickname;
            if (UIManager.Instance != null)
            {
                NetworkObject netObj = GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsValid) UIManager.Instance.UpdatePlayerNameTag(netObj.Id.GetHashCode(), nickname, gameObject);
            }
        }
        catch (System.Exception) { }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void SyncAllStatsRPC(float damage, float armor, float critChance, float moveSpeed, float attackSpeed)
    {
        _baseDamage = damage;
        _baseArmor = armor;
        _baseCriticalChance = critChance;
        _moveSpeed = moveSpeed;
        _baseAttackSpeed = attackSpeed;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void SyncWeaponTypeRPC(int weaponType)
    {
        currentWeaponType = (WeaponType)weaponType;
        if (weaponSystem != null) weaponSystem.OnWeaponTypeChanged(currentWeaponType);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void SyncEquipmentStatsRPC(byte[] statsData)
    {
        equipmentStats = ByteToEquipmentStats(statsData);
        RecalculateStats();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void ResetPotionStatsRPC()
    {
        NetworkPotionCount = 0;
        NetworkPotionLevel = 1;
        OnPotionCountChanged?.Invoke(NetworkPotionCount);
    }

    public void SetPlayerDisplayName(string name)
    {
        customPlayerName = name;
    }

    public string GetPlayerDisplayName()
    {
        if (!string.IsNullOrEmpty(NetworkPlayerNickname)) return NetworkPlayerNickname;
        if (!string.IsNullOrEmpty(customPlayerName)) return customPlayerName;
        if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.playerNickname)) return NetworkManager.Instance.playerNickname;
        string nickname = PlayerPrefs.GetString("PlayerNickname", "");
        if (!string.IsNullOrEmpty(nickname)) return nickname;
        return "Player";
    }

    private void OnDestroy()
    {
                if (PlayerManager.Instance != null) PlayerManager.Instance.UnregisterPlayer(cachedInputAuthority);

        // MEMORY LEAK FIX: Event cleanup
        OnHealthChanged = null;
        OnNetworkHealthChanged = null;
        OnPlayerHit = null;
        OnPlayerDeath = null;
        OnPlayerRevive = null;
        OnLevelChanged = null;
        OnXPChanged = null;
        OnCoinsChanged = null;
        OnWeaponChanged = null;
        OnPotionCountChanged = null;

        // Coroutine cleanup
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        StopAllCoroutines();
    }

}
