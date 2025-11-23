using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class SkillDisplayItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image skillIcon;
    [SerializeField] private TextMeshProUGUI skillName;
    [SerializeField] private TextMeshProUGUI skillDescription;
    [SerializeField] private TextMeshProUGUI requiredLevel;
    [SerializeField] private TextMeshProUGUI cooldown;
    [SerializeField] private TextMeshProUGUI skillProgress;
    
    [Header("Lock State")]
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color unlockedColor = Color.white;
    
    [Header("Skill Type")]
    [SerializeField] private SkillType skillType;
    
    // Cached References - Performance için
    private ClassSystem cachedClassSystem;
    private PlayerStats cachedPlayerStats;
    private SkillSystem cachedSkillSystem;
    private SkillData currentSkillData;
    
    // Performance Optimization
    private bool isInitialized = false;
    private float lastUpdateTime = 0f;
    private const float UPDATE_INTERVAL = 1f; // 1 saniyede bir güncelle
    private int lastKnownLevel = -1;
    private ClassType lastKnownClass = ClassType.None;

    private void Start()
    {
        // Event subscription - LocalPlayerManager sonradan hazır olursa haberdar ol
        if (LocalPlayerManager.Instance != null)
        {
            LocalPlayerManager.Instance.OnLocalPlayerFound -= OnLocalPlayerFound;
            LocalPlayerManager.Instance.OnLocalPlayerFound += OnLocalPlayerFound;
        }

        // İlk kontrol
        TryInitialize();
    }
    private void TryInitialize()
    {
        if (LocalPlayerManager.Instance != null && LocalPlayerManager.Instance.LocalPlayerStats != null)
        {
            InitializeWithPlayer(LocalPlayerManager.Instance.LocalPlayerStats);
        }
    }
private void OnLocalPlayerFound(PlayerStats playerStats)
{
    if (!isInitialized)
    {
        InitializeWithPlayer(playerStats);
    }
}
private void OnEnable()
{
    if (!isInitialized)
    {
        // Panel açıldığında henüz initialize olmamışsa kontrol et
        TryInitialize();
    }
    else
    {
        // Zaten initialize edilmişse sadece refresh et
        RefreshDisplay();
    }
}

    private void InitializeWithPlayer(PlayerStats playerStats)
    {
        if (playerStats == null) return;

        // References'ları cache'le
        cachedPlayerStats = playerStats;
        cachedClassSystem = playerStats.GetComponent<ClassSystem>();
        cachedSkillSystem = playerStats.GetComponent<SkillSystem>();

        if (cachedClassSystem != null)
        {
            // Event subscribe - sadece bir kere
            cachedClassSystem.OnClassChanged += OnClassChanged;
            
            // İlk durumu set et
            OnClassChanged(cachedClassSystem.NetworkPlayerClass);
        }

        if (cachedPlayerStats != null)
        {
            // Level change event'ine subscribe ol
            cachedPlayerStats.OnLevelChanged += OnPlayerLevelChanged;
            lastKnownLevel = cachedPlayerStats.CurrentLevel;
        }

        isInitialized = true;
    }

    private void OnClassChanged(ClassType newClass)
    {
        lastKnownClass = newClass;
        UpdateSkillDisplay(newClass);
    }

    private void OnPlayerLevelChanged(int newLevel)
    {
        lastKnownLevel = newLevel;
        
        // Sadece skill unlock durumu değişebilecekse güncelle
        if (currentSkillData != null)
        {
            bool wasUnlocked = lastKnownLevel >= currentSkillData.requiredLevel;
            bool isNowUnlocked = newLevel >= currentSkillData.requiredLevel;
            
            if (wasUnlocked != isNowUnlocked)
            {
                UpdateSkillDisplay(lastKnownClass);
            }
        }
    }

    private void Update()
    {
        // Performance: Sadece gerektiğinde güncelle
        if (!isInitialized || Time.time - lastUpdateTime < UPDATE_INTERVAL)
            return;

        lastUpdateTime = Time.time;

        // Sadece skill progress'i güncelle (hafif operation)
        if (currentSkillData != null && cachedPlayerStats != null)
        {
            bool isUnlocked = cachedPlayerStats.CurrentLevel >= currentSkillData.requiredLevel;
            UpdateSkillProgress(currentSkillData, isUnlocked);
        }
    }

private void UpdateSkillDisplay(ClassType classType)
{
    if (classType == ClassType.None)
    {
        ClearDisplay();
        return;
    }
    
    if (SkillDatabase.Instance == null || cachedPlayerStats == null)
    {
        ClearDisplay();
        return;
    }
    
    var classData = SkillDatabase.Instance.GetClassData(classType);
    if (classData == null)
    {
        ClearDisplay();
        return;
    }
    
    var allSkills = classData.GetSkillsByType(skillType);
    
    if (allSkills.Count > 0)
    {
        // En düşük seviyeli skill'i bul
        SkillData skillToShow = null;
        foreach (var skill in allSkills)
        {
            if (skillToShow == null || skill.requiredLevel < skillToShow.requiredLevel)
            {
                skillToShow = skill;
            }
        }
        
        if (skillToShow != null)
        {
            currentSkillData = skillToShow;
            DisplaySkill(skillToShow);
        }
    }
    else
    {
        ClearDisplay();
    }
}

    private void DisplaySkill(SkillData skillData)
    {
        if (skillData == null || cachedPlayerStats == null) return;
        
        bool isUnlocked = cachedPlayerStats.CurrentLevel >= skillData.requiredLevel;
        
        // Basic skill info
        if (skillIcon != null)
        {
            skillIcon.sprite = skillData.skillIcon;
            skillIcon.color = isUnlocked ? unlockedColor : lockedColor;
        }
            
        if (skillName != null)
        {
            skillName.text = skillData.skillName;
            skillName.color = isUnlocked ? unlockedColor : lockedColor;
        }
            
        if (skillDescription != null)
        {
            if (isUnlocked)
            {
                skillDescription.text = skillData.description;
                skillDescription.color = unlockedColor;
            }
            else
            {
                skillDescription.text = $"<color=#FF6B6B>LOCKED</color>\n{skillData.description}";
                skillDescription.color = lockedColor;
            }
        }
            
        if (requiredLevel != null)
        {
            if (isUnlocked)
            {
                requiredLevel.text = $"<color=#90EE90>Seviye {skillData.requiredLevel}</color>";
            }
            else
            {
                int levelsNeeded = skillData.requiredLevel - cachedPlayerStats.CurrentLevel;
                requiredLevel.text = $"<color=#FF6B6B>Seviye {skillData.requiredLevel} ({levelsNeeded} daha)</color>";
            }
        }

        if (cooldown != null)
        {
            cooldown.text = $"Bekleme: {skillData.baseCooldown}s";
            cooldown.color = isUnlocked ? unlockedColor : lockedColor;
        }
        
        // Lock overlay
        if (lockOverlay != null)
        {
            lockOverlay.SetActive(!isUnlocked);
        }
        
        // Skill progress
        UpdateSkillProgress(skillData, isUnlocked);
    }

    private void UpdateSkillProgress(SkillData skillData, bool isUnlocked)
    {
        if (skillProgress == null) return;

        if (!isUnlocked)
        {
            skillProgress.text = $"<color=#808080>Seviye {skillData.requiredLevel}'de açılır</color>";
            return;
        }
        
        if (cachedSkillSystem == null)
        {
            skillProgress.text = $"Seviye: 0/{skillData.maxSkillLevel}\n<color=#FFD700>Öğrenilmedi</color>";
            return;
        }

        var skillInstance = cachedSkillSystem.GetSkillInstance(skillData.skillId);

        if (skillInstance != null)
        {
            int currentLevel = skillInstance.currentLevel;
            int currentXP = skillInstance.currentXP;
            int maxLevel = skillData.maxSkillLevel;

            if (currentLevel >= maxLevel)
            {
                skillProgress.text = $"<color=#FFD700>Seviye: {currentLevel}/{maxLevel} (MAX)</color>";
            }
            else
            {
                int xpRequiredForNext = skillData.GetXPRequirement(currentLevel + 1);
                skillProgress.text = $"<color=#87CEEB>Seviye: {currentLevel}/{maxLevel}</color>\n<color=#98FB98>Deneyim: {currentXP}/{xpRequiredForNext}</color>";
            }
        }
        else
        {
            skillProgress.text = $"<color=#87CEEB>Seviye: 0/{skillData.maxSkillLevel}</color>\n<color=#FFD700>Öğrenmeye Hazır</color>";
        }
    }

private void ClearDisplay()
{
    currentSkillData = null;
    
    if (skillIcon != null)
    {
        skillIcon.sprite = null;
        skillIcon.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
    }
        
    if (skillName != null)
    {
        skillName.text = "Class Seç";
        skillName.color = new Color(0.9f, 0.7f, 0.2f);
    }
        
    if (skillDescription != null)
    {
        skillDescription.text = "Level 2'ye ulaş ve bir class seç!\nSkillerin o zaman açılacak.";
        skillDescription.color = new Color(0.8f, 0.8f, 0.8f);
    }
        
    if (requiredLevel != null)
    {
        int currentLevel = cachedPlayerStats != null ? cachedPlayerStats.CurrentLevel : 1;
        if (currentLevel < 2)
        {
            int levelsNeeded = 2 - currentLevel;
            requiredLevel.text = $"<color=#FFD700>{levelsNeeded} level daha!</color>";
        }
        else
        {
            requiredLevel.text = "<color=#90EE90>Class seçmeye hazırsın!</color>";
        }
    }
        
    if (cooldown != null)
        cooldown.text = "";
        
    if (skillProgress != null)
        skillProgress.text = "<color=#87CEEB>Class seçtikten sonra\nskill geliştirebilirsin</color>";
        
    if (lockOverlay != null)
        lockOverlay.SetActive(false);
}

    private void OnDestroy()
    {
        // Event unsubscribe - Memory leak önleme
        if (cachedClassSystem != null)
        {
            cachedClassSystem.OnClassChanged -= OnClassChanged;
        }
        
        if (cachedPlayerStats != null)
        {
            cachedPlayerStats.OnLevelChanged -= OnPlayerLevelChanged;
        }
    }

    // Public method - Dışarıdan manuel refresh için
    public void RefreshDisplay()
    {
        if (isInitialized && lastKnownClass != ClassType.None)
        {
            UpdateSkillDisplay(lastKnownClass);
        }
    }
}