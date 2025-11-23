using UnityEngine;
using TMPro;
using System.Text;
using System.Collections;

public class PlayerStatsDisplay : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI statsText;
    
    [Header("Update Settings")]
    [SerializeField] private float updateInterval = 2f; // 2 saniyeye çıkarıldı
    
    // Cached References
    private PlayerStats playerStats;
    private ClassSystem classSystem;
    private WeaponSystem weaponSystem;
    private TemporaryBuffSystem buffSystem;
private bool activeEffectsChanged = false;
private string lastActiveEffectsText = "";
    // Performance Optimization
    private float nextUpdateTime;
    private StringBuilder statsBuilder = new StringBuilder(1000); // Pre-allocate capacity
    private bool isInitialized = false;
    private float lastKnownHealthRegen = -1f;

    // Cache için değişken tracking
    private int lastKnownLevel = -1;
    private float lastKnownHP = -1f;
    private float lastKnownMaxHP = -1f;
    private float lastKnownXP = -1f;
    private ClassType lastKnownClass = ClassType.None;
    private int lastKnownCoins = -1;
    private float lastKnownDamage = -1f;
    private float lastKnownArmor = -1f;
    private float lastKnownCritChance = -1f;
    private float lastKnownAttackSpeed = -1f;
    private float lastKnownMoveSpeed = -1f;
    
    // String cache - memory allocation azaltmak için
    private string cachedPlayerName = "";
    private string cachedStatsText = "";

    private void Start()
    {
        // LocalPlayerManager'dan player referansını al
        if (LocalPlayerManager.Instance != null && LocalPlayerManager.Instance.LocalPlayerStats != null)
        {
            InitializeWithPlayer(LocalPlayerManager.Instance.LocalPlayerStats);
        }
        else
        {
            // Event'e subscribe ol
            if (LocalPlayerManager.Instance != null)
            {
                LocalPlayerManager.Instance.OnLocalPlayerFound += OnLocalPlayerFound;
            }
            
            StartCoroutine(WaitForLocalPlayerManager());
        }
    }

    private IEnumerator WaitForLocalPlayerManager()
    {
        float timeout = 5f;
        float elapsed = 0f;
        
        while (LocalPlayerManager.Instance == null || LocalPlayerManager.Instance.LocalPlayerStats == null)
        {
            if (elapsed >= timeout)
            {
                ShowLoadingText();
                yield break;
            }
            
            yield return new WaitForSeconds(0.2f);
            elapsed += 0.2f;
        }
        
        InitializeWithPlayer(LocalPlayerManager.Instance.LocalPlayerStats);
    }

    private void OnLocalPlayerFound(PlayerStats stats)
    {
        InitializeWithPlayer(stats);
    }

    private void Update()
    {
        if (!isInitialized || Time.time < nextUpdateTime || !gameObject.activeInHierarchy)
            return;

        // Sadece değişiklik varsa güncelle
        if (HasStatsChanged())
        {
            UpdateStatsDisplay();
            nextUpdateTime = Time.time + updateInterval;
        }
        else
        {
            // Değişiklik yoksa check interval'ı artır
            nextUpdateTime = Time.time + (updateInterval * 0.5f);
        }
    }

private bool HasStatsChanged()
{
    if (playerStats == null) return false;

    // YENİ: Active effects değişiklik kontrolü
    if (activeEffectsChanged)
    {
        activeEffectsChanged = false;
        return true;
    }

    // Critical stats kontrolü - sadece önemli değişiklikleri yakala
    bool changed = false;
    
    if (lastKnownLevel != playerStats.CurrentLevel)
    {
        lastKnownLevel = playerStats.CurrentLevel;
        changed = true;
    }
    
    if (Mathf.Abs(lastKnownHP - playerStats.CurrentHP) > 1f)
    {
        lastKnownHP = playerStats.CurrentHP;
        changed = true;
    }
    
    if (Mathf.Abs(lastKnownMaxHP - playerStats.MaxHP) > 1f)
    {
        lastKnownMaxHP = playerStats.MaxHP;
        changed = true;
    }
    
    if (Mathf.Abs(lastKnownXP - playerStats.CurrentXP) > 1f)
    {
        lastKnownXP = playerStats.CurrentXP;
        changed = true;
    }
    
    if (classSystem != null && lastKnownClass != classSystem.NetworkPlayerClass)
    {
        lastKnownClass = classSystem.NetworkPlayerClass;
        changed = true;
    }
    
    if (lastKnownCoins != playerStats.Coins)
    {
        lastKnownCoins = playerStats.Coins;
        changed = true;
    }

// HasStatsChanged() metodunda, performance check bölümüne ekle
// Performance: Damage/Armor gibi değerler daha az sıklıkla kontrol edilsin
if (Time.frameCount % 30 == 0) // 30 frame'de bir kontrol et
{
    if (Mathf.Abs(lastKnownDamage - playerStats.FinalDamage) > 0.5f)
    {
        lastKnownDamage = playerStats.FinalDamage;
        changed = true;
    }
    
    if (Mathf.Abs(lastKnownArmor - playerStats.BaseArmor) > 0.5f)
    {
        lastKnownArmor = playerStats.BaseArmor;
        changed = true;
    }
    
    if (Mathf.Abs(lastKnownCritChance - playerStats.FinalCriticalChance) > 0.5f)
    {
        lastKnownCritChance = playerStats.FinalCriticalChance;
        changed = true;
    }
    
    if (Mathf.Abs(lastKnownAttackSpeed - playerStats.FinalAttackSpeed) > 0.1f)
    {
        lastKnownAttackSpeed = playerStats.FinalAttackSpeed;
        changed = true;
    }
    
    if (Mathf.Abs(lastKnownMoveSpeed - playerStats.MoveSpeed) > 1f)
    {
        lastKnownMoveSpeed = playerStats.MoveSpeed;
        changed = true;
    }
    
    // ===== YENİ: Health Regen kontrolü =====
    if (Mathf.Abs(lastKnownHealthRegen - playerStats.TotalHealthRegen) > 0.1f)
    {
        lastKnownHealthRegen = playerStats.TotalHealthRegen;
        changed = true;
    }
}

    return changed;
}

public void InitializeWithPlayer(PlayerStats stats)
{
    if (stats == null) return;

    // Önceki event'leri temizle
    UnsubscribeFromEvents();

    playerStats = stats;
    classSystem = stats.GetComponent<ClassSystem>();
    weaponSystem = stats.GetComponent<WeaponSystem>();
    buffSystem = stats.GetComponent<TemporaryBuffSystem>(); // GÜNCELLENDI

    // Event subscription - performans için sadece kritik event'ler
    if (playerStats != null)
    {
        playerStats.OnLevelChanged += OnLevelChanged;
        playerStats.OnHealthChanged += OnHealthChanged;
        playerStats.OnXPChanged += OnXPChanged;
        playerStats.OnCoinsChanged += OnCoinsChanged;
    }

    if (classSystem != null)
    {
        classSystem.OnClassChanged += OnClassChanged;
    }

    // YENİ: Buff event'lerine subscribe ol
    if (buffSystem != null)
    {
        buffSystem.OnBuffStarted += OnBuffChanged;
        buffSystem.OnBuffEnded += OnBuffChanged;
    }

    // Cache'i initialize et
    cachedPlayerName = playerStats.GetPlayerDisplayName();
    ResetCache();
    
    isInitialized = true;
    UpdateStatsDisplay();
}

    // Yeni metod: Buff değişiklik handler'ı
    private void OnBuffChanged(string buffType, Sprite icon = null, float duration = 0f)
    {
        activeEffectsChanged = true;
        // Immediate update for buffs
        if (Time.time >= nextUpdateTime - (updateInterval * 0.8f))
        {
            UpdateStatsDisplay();
            nextUpdateTime = Time.time + updateInterval;
        }
    }
private void OnBuffChanged(string buffType)
{
    OnBuffChanged(buffType, null, 0f);
}

// ResetCache() metoduna ekle
private void ResetCache()
{
    lastKnownLevel = -1;
    lastKnownHP = -1f;
    lastKnownMaxHP = -1f;
    lastKnownXP = -1f;
    lastKnownClass = ClassType.None;
    lastKnownCoins = -1;
    lastKnownDamage = -1f;
    lastKnownArmor = -1f;
    lastKnownCritChance = -1f;
    lastKnownAttackSpeed = -1f;
    lastKnownMoveSpeed = -1f;
    lastKnownHealthRegen = -1f; // ===== YENİ =====
    cachedStatsText = "";
    lastActiveEffectsText = "";
    activeEffectsChanged = false;
}

    // Event handlers - immediate update için
    private void OnLevelChanged(int newLevel)
    {
        UpdateStatsDisplay();
        nextUpdateTime = Time.time + updateInterval;
    }

    private void OnHealthChanged(float newHP)
    {
        // HP değişiklikleri çok sık olabilir, throttle et
        if (Time.time >= nextUpdateTime - (updateInterval * 0.5f))
        {
            UpdateStatsDisplay();
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    private void OnXPChanged(float newXP)
    {
        UpdateStatsDisplay();
        nextUpdateTime = Time.time + updateInterval;
    }

    private void OnCoinsChanged(int newCoins)
    {
        UpdateStatsDisplay();
        nextUpdateTime = Time.time + updateInterval;
    }

    private void OnClassChanged(ClassType newClass)
    {
        UpdateStatsDisplay();
        nextUpdateTime = Time.time + updateInterval;
    }

    private void UpdateStatsDisplay()
    {
        if (playerStats == null || statsText == null)
        {
            ShowLoadingText();
            return;
        }
        
        // StringBuilder'ı temizle ama capacity'yi koru
        statsBuilder.Clear();
        
        // Player name cache kontrolü
        string currentPlayerName = playerStats.GetPlayerDisplayName();
        if (cachedPlayerName != currentPlayerName)
        {
            cachedPlayerName = currentPlayerName;
        }
        
        // Core Stats - Optimized string building
        BuildCoreStats();
        BuildCombatStats();
        BuildAdvancedStats();
        BuildActiveEffects();
        
        // Text'i bir kerede set et
        string newStatsText = statsBuilder.ToString();
        if (cachedStatsText != newStatsText)
        {
            cachedStatsText = newStatsText;
            statsText.text = cachedStatsText;
        }
    }

    private void BuildCoreStats()
    {
        // Title
        statsBuilder.AppendLine($"<color=#FFD700><b>{cachedPlayerName}</b></color>");
        statsBuilder.AppendLine();

        // Core Stats
        statsBuilder.AppendLine($"<color=#87CEEB><b>••• Temel İstatistikler •••</b></color>");
        statsBuilder.AppendLine($"<color=#90EE90>Seviye:</color> {playerStats.CurrentLevel}");
        statsBuilder.AppendLine($"<color=#FF6B6B>Can:</color> {playerStats.CurrentHP:F0} / {playerStats.MaxHP:F0}");
        statsBuilder.AppendLine($"<color=#87CEFA>Deneyim:</color> {playerStats.CurrentXP:F0} / {playerStats.GetRequiredXPForNextLevel():F0}");

        // Class
        string className = GetClassDisplayName();
        statsBuilder.AppendLine($"<color=#DDA0DD>Sınıf:</color> {className}");
        statsBuilder.AppendLine();
    }

private void BuildCombatStats()
{
    // Combat Stats
    statsBuilder.AppendLine($"<color=#87CEEB><b>••• Savaş İstatistikleri •••</b></color>");
    statsBuilder.AppendLine($"<color=#FF4500>Temel Hasar:</color> {playerStats.BaseDamage:F0}");
    statsBuilder.AppendLine($"<color=#FF6347>Toplam Hasar:</color> {playerStats.FinalDamage:F0}");
    statsBuilder.AppendLine($"<color=#4682B4>Zırh:</color> {playerStats.BaseArmor:F0}");

    // Damage Reduction hesapla
    float damageReduction = playerStats.BaseArmor / (100f + playerStats.BaseArmor) * 100f;
    statsBuilder.AppendLine($"<color=#5F9EA0>Hasar Azaltma:</color> {damageReduction:F1}%");

    // ===== YENİ: Health Regen ekle =====
    float healthRegen = playerStats.TotalHealthRegen;
    if (healthRegen > 0f)
    {
        statsBuilder.AppendLine($"<color=#90EE90>Can Yenileme:</color> {healthRegen:F1}/sn");
    }

    statsBuilder.AppendLine($"<color=#DC143C>Kritik Şansı:</color> {playerStats.FinalCriticalChance:F1}%");

    // Critical Multiplier
    float critMultiplier = GetCriticalMultiplier();
    statsBuilder.AppendLine($"<color=#B22222>Kritik Çarpanı:</color> {critMultiplier:F1}x");

    statsBuilder.AppendLine($"<color=#FF1493>Saldırı Hızı:</color> {playerStats.FinalAttackSpeed:F1}");
    statsBuilder.AppendLine($"<color=#32CD32>İsabet:</color> {(playerStats.CurrentAccuracy * 100):F1}%");
    statsBuilder.AppendLine();
}

    private void BuildAdvancedStats()
    {
        // Advanced Stats
        statsBuilder.AppendLine($"<color=#87CEEB><b>••• Gelişmiş İstatistikler •••</b></color>");
        statsBuilder.AppendLine($"<color=#98FB98>Hareket Hızı:</color> {playerStats.MoveSpeed:F0}");

        // Projectile Speed (Ranger için)
        if (classSystem != null && classSystem.NetworkPlayerClass == ClassType.Ranger)
        {
            float projectileSpeed = playerStats.GetProjectileSpeedMultiplier();
            statsBuilder.AppendLine($"<color=#87CEEB>Mermi Hızı:</color> {(projectileSpeed * 100):F0}%");
        }

        // XP & Coin multipliers
        float xpMultiplier = playerStats.statsData.xpGainMultiplier.GetValueAtLevel(playerStats.CurrentLevel);
        float coinMultiplier = playerStats.statsData.coinGainMultiplier.GetValueAtLevel(playerStats.CurrentLevel);
        statsBuilder.AppendLine($"<color=#FFE4B5>Deneyim Çarpanı:</color> {xpMultiplier:F1}x");
        statsBuilder.AppendLine($"<color=#DAA520>Altın Çarpanı:</color> {coinMultiplier:F1}x");

        // Party bonuses
        if (playerStats.IsInParty())
        {
            statsBuilder.AppendLine($"<color=#9370DB>Parti Deneyim Bonusu:</color> +{(playerStats.PartyBuffMultiplier - 1f) * 100:F0}%");
        }
    }

private void BuildActiveEffects()
{
    if (buffSystem == null) return;
    
    // YENİ: Buff text değişiklik kontrolü için StringBuilder kullan
    var tempBuilder = new StringBuilder(200);
    bool hasActiveEffects = false;
    
    // Performance: Network object valid kontrolü
    if (buffSystem.Object == null || !buffSystem.Object.IsValid) return;
    
    try
    {
        float speedMultiplier = buffSystem.GetCurrentSpeedMultiplier();
        float attackSpeedMultiplier = buffSystem.GetCurrentAttackSpeedMultiplier();
        float damageMultiplier = buffSystem.GetCurrentDamageDebuffMultiplier();
        float damageReductionMultiplier = buffSystem.GetCurrentDamageReductionMultiplier();
        bool isInvulnerable = buffSystem.IsInvulnerable();
        float slowMultiplier = buffSystem.GetCurrentSlowMultiplier();
        float accuracyMultiplier = buffSystem.GetCurrentAccuracyMultiplier();
        
        if (speedMultiplier != 1f || attackSpeedMultiplier != 1f || damageMultiplier != 1f || 
            damageReductionMultiplier != 1f || isInvulnerable || slowMultiplier != 1f || accuracyMultiplier != 1f)
        {
            hasActiveEffects = true;
            
            tempBuilder.AppendLine();
            tempBuilder.AppendLine($"<color=#87CEEB><b>••• Aktif Efektler •••</b></color>");

            // Speed effects
            if (speedMultiplier != 1f)
            {
                string effect = speedMultiplier > 1f ? "Hız Artışı" : "Hız Düşüşü";
                tempBuilder.AppendLine($"<color=#98FB98>{effect}:</color> {(speedMultiplier * 100):F0}%");
            }

            // Attack speed effects
            if (attackSpeedMultiplier != 1f)
            {
                string effect = attackSpeedMultiplier > 1f ? "Saldırı Hızı Artışı" : "Saldırı Yavaşlama";
                tempBuilder.AppendLine($"<color=#FF1493>{effect}:</color> {(attackSpeedMultiplier * 100):F0}%");
            }

            // Damage effects
            if (damageMultiplier != 1f)
            {
                string effect = damageMultiplier > 1f ? "Hasar Artışı" : "Hasar Düşüşü";
                tempBuilder.AppendLine($"<color=#FF4500>{effect}:</color> {(damageMultiplier * 100):F0}%");
            }

            // Damage reduction effects
            if (damageReductionMultiplier != 1f)
            {
                float reduction = (1f - damageReductionMultiplier) * 100f;
                tempBuilder.AppendLine($"<color=#4682B4>Hasar Kalkanı:</color> -{reduction:F0}%");
            }

            // Invulnerability
            if (isInvulnerable)
            {
                tempBuilder.AppendLine($"<color=#FFD700>Ölümsüz</color>");
            }

            // Slow effect
            if (slowMultiplier != 1f)
            {
                tempBuilder.AppendLine($"<color=#708090>Yavaşlama:</color> {(slowMultiplier * 100):F0}%");
            }

            // Accuracy effect
            if (accuracyMultiplier != 1f)
            {
                tempBuilder.AppendLine($"<color=#FFA500>İsabet:</color> {(accuracyMultiplier * 100):F0}%");
            }
        }
    }
    catch (System.Exception)
    {
        // Network exception handling - sessiz fail
        return;
    }
    
    // YENİ: Text değişiklik kontrolü
    string newActiveEffectsText = hasActiveEffects ? tempBuilder.ToString() : "";
    if (lastActiveEffectsText != newActiveEffectsText)
    {
        lastActiveEffectsText = newActiveEffectsText;
        statsBuilder.Append(newActiveEffectsText);
    }
    else if (hasActiveEffects)
    {
        // Değişiklik yoksa cached text'i kullan
        statsBuilder.Append(lastActiveEffectsText);
    }
}
    
    private float GetCriticalMultiplier()
    {
        float baseCritMultiplier = 1.5f; // Default value
        
        // Rogue Level 20 bonus: +25% crit damage
        if (classSystem != null && classSystem.NetworkPlayerClass == ClassType.Rogue && playerStats.CurrentLevel >= 20)
        {
            baseCritMultiplier += 0.25f;
        }
        
        return baseCritMultiplier;
    }
    
    private string GetClassDisplayName()
    {
        if (classSystem == null) return "Sınıf Yok";

        return classSystem.NetworkPlayerClass switch
        {
            ClassType.None => "<color=#808080>Sınıf Yok</color>",
            ClassType.Warrior => "<color=#FF6B6B>Savaşçı</color>",
            ClassType.Ranger => "<color=#90EE90>Okçu</color>",
            ClassType.Rogue => "<color=#DDA0DD>Haydut</color>",
            _ => "Bilinmiyor"
        };
    }

    private void ShowLoadingText()
    {
        if (statsText != null)
        {
            statsText.text = "<color=#87CEEB>Oyuncu verileri yükleniyor...</color>";
        }
    }
    
    public void RefreshDisplay()
    {
        if (isInitialized)
        {
            ResetCache();
            UpdateStatsDisplay();
        }
    }

private void UnsubscribeFromEvents()
{
    // Event unsubscribe - Memory leak önleme
    if (playerStats != null)
    {
        playerStats.OnLevelChanged -= OnLevelChanged;
        playerStats.OnHealthChanged -= OnHealthChanged;
        playerStats.OnXPChanged -= OnXPChanged;
        playerStats.OnCoinsChanged -= OnCoinsChanged;
    }
    
    if (classSystem != null)
    {
        classSystem.OnClassChanged -= OnClassChanged;
    }
    
    // YENİ: Buff event'lerini unsubscribe et
    if (buffSystem != null)
    {
        buffSystem.OnBuffStarted -= OnBuffChanged;
        buffSystem.OnBuffEnded -= OnBuffChanged;
    }
}

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        
        if (LocalPlayerManager.Instance != null)
        {
            LocalPlayerManager.Instance.OnLocalPlayerFound -= OnLocalPlayerFound;
        }
    }

    private void OnEnable()
    {
        if (isInitialized)
        {
            UpdateStatsDisplay();
        }
    }
}