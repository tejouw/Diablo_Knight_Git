using UnityEngine;
using TMPro;
using System.Collections;

public class ClassInfoDisplay : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI classNameText;
    public TextMeshProUGUI passiveDescriptionText;
    public TextMeshProUGUI level5BonusText;
    public TextMeshProUGUI level10BonusText;
    public TextMeshProUGUI level20BonusText;
    public TextMeshProUGUI level30BonusText;
    public TextMeshProUGUI classLoreText;
    
    [Header("Class Selection")]
    public ClassType displayedClass = ClassType.Warrior;
    
    // Cached References
    private PlayerStats localPlayerStats;
    private ClassSystem localClassSystem;
    
    // Performance Optimization
    private bool isInitialized = false;
    private ClassType lastKnownClass = ClassType.None;
    private int lastKnownLevel = -1;
    
    // Color Cache - Performance için
    private readonly Color unlockedColor = new Color(0.2f, 1f, 0.2f); // Yeşil - Ulaşıldı
    private readonly Color lockedColor = new Color(0.6f, 0.6f, 0.6f); // Gri - Henüz ulaşılmadı
    private readonly Color goldColor = new Color(0.9f, 0.9f, 0.6f); // Altın rengi
    
    private string cachedWarriorPassive;
    private string cachedRangerPassive;
    private string cachedRoguePassive;
    private string cachedWarriorLore;
    private string cachedRangerLore;
    private string cachedRogueLore;

private void Start()
{
    bool hasInstance = LocalPlayerManager.Instance != null;
    bool hasStats = hasInstance && LocalPlayerManager.Instance.LocalPlayerStats != null;
    
    InitializeStringCache();
    
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
    
    // LOG: Coroutine başladı
    
    while (LocalPlayerManager.Instance == null || LocalPlayerManager.Instance.LocalPlayerStats == null)
    {
        if (elapsed >= timeout)
        {
            UpdateClassInfo();
            yield break;
        }
        
        // LOG: Her 1 saniyede bir durum
        if (elapsed % 1f < 0.2f)
        {
            bool hasInstance = LocalPlayerManager.Instance != null;
            bool hasStats = hasInstance && LocalPlayerManager.Instance.LocalPlayerStats != null;
        }
        
        yield return new WaitForSeconds(0.2f);
        elapsed += 0.2f;
    }
    
    InitializeWithPlayer(LocalPlayerManager.Instance.LocalPlayerStats);
}
private void TryInitialize()
{
    if (LocalPlayerManager.Instance != null && LocalPlayerManager.Instance.LocalPlayerStats != null)
    {
        InitializeWithPlayer(LocalPlayerManager.Instance.LocalPlayerStats);
    }
}

    private void InitializeStringCache()
    {
        // String'leri cache'le - her seferinde yeniden oluşturma
        cachedWarriorPassive = "Seviye başına +2 Zırh. Her adımda daha dayanıklı olursun.";
        cachedRangerPassive = "Seviye başına +2 Critical Chance. Her okun hedefini bulur.";
        cachedRoguePassive = "Seviye başına Attack Speed artışı. Gölgelerde hızla hareket et.";
        
        cachedWarriorLore = "Kalkanını kuşan, kılıcını kaldır: savaşçıların adı, cesaretle anılır.";
        cachedRangerLore = "Yayını ger, nişanını al: avcıların yolu, sessizlikle dolu.";
        cachedRogueLore = "Gölgeden çık, vurup kaçar: hırsızların dansı, ölümle yarışır.";
    }



    private void OnLocalPlayerFound(PlayerStats playerStats)
    {
        InitializeWithPlayer(playerStats);
    }

    public void InitializeWithPlayer(PlayerStats playerStats)
    {
        if (playerStats == null)
        {
            Debug.LogError("[ClassInfoDisplay] PlayerStats is null!");
            return;
        }

        // Önceki event'leri temizle
        UnsubscribeFromEvents();

        localPlayerStats = playerStats;
        localClassSystem = playerStats.GetComponent<ClassSystem>();

        if (localClassSystem != null)
        {
            // Class değişiklik eventine subscribe ol
            localClassSystem.OnClassChanged += OnPlayerClassChanged;

            // Mevcut class'ı al ve göster
            ClassType currentClass = localClassSystem.NetworkPlayerClass;
            displayedClass = currentClass;
            
            // Level event'ine subscribe ol
            if (localPlayerStats != null)
            {
                localPlayerStats.OnLevelChanged += OnPlayerLevelChanged;
                lastKnownLevel = localPlayerStats.CurrentLevel;
            }
            
            UpdateClassInfo();
            isInitialized = true;
        }
        else
        {
            Debug.LogError("[ClassInfoDisplay] ClassSystem component not found!");
        }
    }

    public void SetClass(ClassType classType)
    {
        displayedClass = classType;
        UpdateClassInfo();
    }

    private void OnPlayerClassChanged(ClassType newClass)
    {
        if (lastKnownClass == newClass) return; // Aynı class ise güncelleme
        
        lastKnownClass = newClass;
        displayedClass = newClass;
        
        // Sadece active ise animasyonu başlat
        if (gameObject.activeInHierarchy)
        {
            UpdateClassInfo();
        }
    }

    private void OnPlayerLevelChanged(int newLevel)
    {
        if (lastKnownLevel == newLevel) return; // Aynı level ise güncelleme
        
        lastKnownLevel = newLevel;
        
        // Sadece milestone color'ları güncelle (hafif operation)
        if (gameObject.activeInHierarchy)
        {
            UpdateMilestoneColors();
        }
    }

private void UpdateClassInfo()
{
    switch (displayedClass)
    {
        case ClassType.Warrior:
            SetWarriorInfo();
            break;
        case ClassType.Ranger:
            SetRangerInfo();
            break;
        case ClassType.Rogue:
            SetRogueInfo();
            break;
        case ClassType.None:
        default:
            ClearInfo();
            break;
    }
}

private void OnEnable()
{
    if (!isInitialized)
    {
        // Panel açıldığında henüz initialize olmamışsa kontrol et
        TryInitialize();
    }
    else if (lastKnownClass != ClassType.None)
    {
        // Zaten initialize edilmişse sadece refresh et
        UpdateClassInfo();
    }
}

    // Optimized color method - çok çağrıldığı için cache'lenmiş
    private Color GetMilestoneColor(int level)
    {
        if (localPlayerStats == null) return lockedColor;
        
        int currentLevel = localPlayerStats.CurrentLevel;
        
        return currentLevel >= level ? unlockedColor : lockedColor;
    }

    // Sadece milestone renklerini güncelle - full rebuild yerine
    private void UpdateMilestoneColors()
    {
        switch (displayedClass)
        {
            case ClassType.Warrior:
                UpdateWarriorMilestoneColors();
                break;
            case ClassType.Ranger:
                UpdateRangerMilestoneColors();
                break;
            case ClassType.Rogue:
                UpdateRogueMilestoneColors();
                break;
        }
    }

    private void UpdateWarriorMilestoneColors()
    {
        if (level5BonusText != null)
            level5BonusText.color = GetMilestoneColor(5);
        if (level10BonusText != null)
            level10BonusText.color = GetMilestoneColor(10);
        if (level20BonusText != null)
            level20BonusText.color = GetMilestoneColor(20);
        if (level30BonusText != null)
            level30BonusText.color = GetMilestoneColor(30);
    }

    private void UpdateRangerMilestoneColors()
    {
        if (level5BonusText != null)
            level5BonusText.color = GetMilestoneColor(5);
        if (level10BonusText != null)
            level10BonusText.color = GetMilestoneColor(10);
        if (level20BonusText != null)
            level20BonusText.color = GetMilestoneColor(20);
        if (level30BonusText != null)
            level30BonusText.color = GetMilestoneColor(30);
    }

    private void UpdateRogueMilestoneColors()
    {
        if (level5BonusText != null)
            level5BonusText.color = GetMilestoneColor(5);
        if (level10BonusText != null)
            level10BonusText.color = GetMilestoneColor(10);
        if (level20BonusText != null)
            level20BonusText.color = GetMilestoneColor(20);
        if (level30BonusText != null)
            level30BonusText.color = GetMilestoneColor(30);
    }
    
    private void SetWarriorInfo()
    {
        if (classNameText != null)
        {
            classNameText.text = "Savaşçı";
            classNameText.color = new Color(0.8f, 0.2f, 0.2f); // Kırmızı
        }

        if (passiveDescriptionText != null)
            passiveDescriptionText.text = cachedWarriorPassive;

        if (level5BonusText != null)
        {
            level5BonusText.text = " Seviye 5: Can Bonusu +%20";
            level5BonusText.color = GetMilestoneColor(5);
        }

        if (level10BonusText != null)
        {
            level10BonusText.text = " Seviye 10: Hasar Azaltma %10";
            level10BonusText.color = GetMilestoneColor(10);
        }

        if (level20BonusText != null)
        {
            level20BonusText.text = " Seviye 20: Saldırı Hasarı +%15";
            level20BonusText.color = GetMilestoneColor(20);
        }

        if (level30BonusText != null)
        {
            level30BonusText.text = " Seviye 30: Can Yenileme (saniyede %1)";
            level30BonusText.color = GetMilestoneColor(30);
        }
            
        if (classLoreText != null)
        {
            classLoreText.text = cachedWarriorLore;
            classLoreText.color = goldColor;
        }
    }
    
    private void SetRangerInfo()
    {
        if (classNameText != null)
        {
            classNameText.text = "Okçu";
            classNameText.color = new Color(0.2f, 0.8f, 0.2f); // Yeşil
        }

        if (passiveDescriptionText != null)
            passiveDescriptionText.text = cachedRangerPassive;

        if (level5BonusText != null)
        {
            level5BonusText.text = " Seviye 5: Menzil Artışı +%30";
            level5BonusText.color = GetMilestoneColor(5);
        }

        if (level10BonusText != null)
        {
            level10BonusText.text = " Seviye 10: Mermi Hızı +%40";
            level10BonusText.color = GetMilestoneColor(10);
        }

        if (level20BonusText != null)
        {
            level20BonusText.text = " Seviye 20: Kritik Şansı +%10";
            level20BonusText.color = GetMilestoneColor(20);
        }

        if (level30BonusText != null)
        {
            level30BonusText.text = " Seviye 30: Saldırı Hızı +%20";
            level30BonusText.color = GetMilestoneColor(30);
        }
            
        if (classLoreText != null)
        {
            classLoreText.text = cachedRangerLore;
            classLoreText.color = goldColor;
        }
    }
    
    private void SetRogueInfo()
    {
        if (classNameText != null)
        {
            classNameText.text = "Haydut";
            classNameText.color = new Color(0.6f, 0.2f, 0.8f); // Mor
        }

        if (passiveDescriptionText != null)
            passiveDescriptionText.text = cachedRoguePassive;

        if (level5BonusText != null)
        {
            level5BonusText.text = " Seviye 5: Hareket Hızı +%25";
            level5BonusText.color = GetMilestoneColor(5);
        }

        if (level10BonusText != null)
        {
            level10BonusText.text = " Seviye 10: Saldırı Hızı +%15";
            level10BonusText.color = GetMilestoneColor(10);
        }

        if (level20BonusText != null)
        {
            level20BonusText.text = " Seviye 20: Kritik Hasarı +%25";
            level20BonusText.color = GetMilestoneColor(20);
        }

        if (level30BonusText != null)
        {
            level30BonusText.text = " Seviye 30: Saldırı Hızı +%25 (ek)";
            level30BonusText.color = GetMilestoneColor(30);
        }
            
        if (classLoreText != null)
        {
            classLoreText.text = cachedRogueLore;
            classLoreText.color = goldColor;
        }
    }
    
private void ClearInfo()
{
    if (classNameText != null)
    {
        classNameText.text = "Class Seçilmedi";
        classNameText.color = new Color(0.9f, 0.7f, 0.2f); // Turuncu
    }
        
    if (passiveDescriptionText != null)
        passiveDescriptionText.text = "Level 2'ye ulaştığında class seçebilirsin!";
        
    if (level5BonusText != null)
        level5BonusText.text = "• Warrior: Savunma odaklı";
        
    if (level10BonusText != null)
        level10BonusText.text = "• Ranger: Uzak saldırı uzmanı";
        
    if (level20BonusText != null)
        level20BonusText.text = "• Rogue: Hız ve kritik hasar";
        
    if (level30BonusText != null)
        level30BonusText.text = "";
        
    if (classLoreText != null)
    {
        classLoreText.text = "Hangi yolu seçeceğin senin elinde. Her class farklı bir oyun tarzı sunar.";
        classLoreText.color = goldColor;
    }
}

    private void UnsubscribeFromEvents()
    {
        // Event unsubscribe - Memory leak önleme
        if (localClassSystem != null)
        {
            localClassSystem.OnClassChanged -= OnPlayerClassChanged;
        }
        
        if (localPlayerStats != null)
        {
            localPlayerStats.OnLevelChanged -= OnPlayerLevelChanged;
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

    // Public method - Dışarıdan manuel refresh için
    public void RefreshDisplay()
    {
        if (isInitialized)
        {
            UpdateClassInfo();
        }
    }
}