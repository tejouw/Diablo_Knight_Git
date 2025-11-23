using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class PlayerStatsUI : MonoBehaviour
{

    [Header("Buff/Debuff UI")]
[SerializeField] private Transform buffContainer; // Buff icon'ları için parent
[SerializeField] private GameObject buffIconPrefab; // Buff icon prefab'ı
[SerializeField] private int maxBuffSlots = 6; // Maksimum buff sayısı

    private Dictionary<string, BuffIconController> activeBuffIcons = new Dictionary<string, BuffIconController>();

    [Header("Health UI")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Text healthText;
    [SerializeField] private Text healthPercentText;

    [Header("XP UI")]
    [SerializeField] private Image xpBarFill;
    [SerializeField] private Text xpText;
    [SerializeField] private Text xpPercentText;

    [Header("Level UI")]
    [SerializeField] private Text levelText;

    [Header("Coins UI")]
    [SerializeField] private TextMeshProUGUI coinText;

    [Header("Level Up Notification")]
[SerializeField] private GameObject levelUpPanel;
    [SerializeField] private Text levelUpText;

private void Start()
{
    // Level up panel'ini başlangıçta kapat
    if (levelUpPanel != null)
    {
        levelUpPanel.SetActive(false);
    }
}

    private bool hasLeveledUpBefore = false;

    private PlayerStats playerStats;

    public void Initialize(PlayerStats stats)
    {
        playerStats = stats;

        if (playerStats != null)
        {
            playerStats.OnNetworkHealthChanged += UpdateHealthUI;
            playerStats.OnLevelChanged += OnLevelUp;
            playerStats.OnXPChanged += UpdateXPUI;
            playerStats.OnCoinsChanged += UpdateCoinsUI;

            // Buff event'lerini dinle
            var tempBuffSystem = playerStats.GetComponent<TemporaryBuffSystem>();
            if (tempBuffSystem != null)
            {
                tempBuffSystem.OnBuffStarted += ShowBuffIcon;
                tempBuffSystem.OnBuffEnded += HideBuffIcon;
            }

            UpdateHealthUI(playerStats.CurrentHP);
            UpdateLevelUI(playerStats.CurrentLevel);
            UpdateXPUI(playerStats.CurrentXP);
            UpdateCoinsUI(playerStats.Coins);

            hasLeveledUpBefore = true;
            if (levelUpPanel != null)
            {
                levelUpPanel.SetActive(false);
            }
        }
    }
private void ShowBuffIcon(string buffId, Sprite buffIcon, float duration)
{
    // Zaten varsa güncelle
    if (activeBuffIcons.ContainsKey(buffId))
    {
        activeBuffIcons[buffId].UpdateDuration(duration);
        return;
    }
    
    // Maksimum slot kontrolü
    if (activeBuffIcons.Count >= maxBuffSlots)
    {
        return;
    }
    
    // Yeni buff icon oluştur
    if (buffIconPrefab != null && buffContainer != null)
    {
        GameObject buffObj = Instantiate(buffIconPrefab, buffContainer);
        BuffIconController controller = buffObj.GetComponent<BuffIconController>();
        
        if (controller == null)
        {
            controller = buffObj.AddComponent<BuffIconController>();
        }
        
        controller.Initialize(buffId, buffIcon, duration);
        controller.OnBuffExpired += () => HideBuffIcon(buffId);
        
        activeBuffIcons[buffId] = controller;
        
        // Fade in animasyonu
        StartCoroutine(FadeInBuff(controller));
    }
}

private void HideBuffIcon(string buffId)
{
    if (activeBuffIcons.TryGetValue(buffId, out BuffIconController controller))
    {
        activeBuffIcons.Remove(buffId);
        
        if (controller != null)
        {
            // Fade out animasyonu
            StartCoroutine(FadeOutBuff(controller));
        }
    }
}

private IEnumerator FadeInBuff(BuffIconController controller)
{
    CanvasGroup canvasGroup = controller.GetComponent<CanvasGroup>();
    if (canvasGroup == null)
    {
        canvasGroup = controller.gameObject.AddComponent<CanvasGroup>();
    }
    
    canvasGroup.alpha = 0f;
    controller.transform.localScale = Vector3.zero;
    
    float duration = 0.3f;
    float elapsed = 0f;
    
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / duration;
        
        // Ease out back
        float scale = 1f + 0.3f * Mathf.Sin(progress * Mathf.PI);
        if (progress > 0.7f)
        {
            scale = Mathf.Lerp(scale, 1f, (progress - 0.7f) / 0.3f);
        }
        
        canvasGroup.alpha = progress;
        controller.transform.localScale = Vector3.one * scale;
        
        yield return null;
    }
    
    canvasGroup.alpha = 1f;
    controller.transform.localScale = Vector3.one;
}

private IEnumerator FadeOutBuff(BuffIconController controller)
{
    CanvasGroup canvasGroup = controller.GetComponent<CanvasGroup>();
    if (canvasGroup == null)
    {
        canvasGroup = controller.gameObject.AddComponent<CanvasGroup>();
    }
    
    float duration = 0.2f;
    float elapsed = 0f;
    float startAlpha = canvasGroup.alpha;
    Vector3 startScale = controller.transform.localScale;
    
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / duration;
        
        canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);
        controller.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
        
        yield return null;
    }
    
    if (controller != null && controller.gameObject != null)
    {
        Destroy(controller.gameObject);
    }
}
private void OnLevelUp(int newLevel)
{
    UpdateLevelUI(newLevel);
    
    // İlk seviye set edilmesi notification göstermesin
    if (hasLeveledUpBefore)
    {
        ShowLevelUpNotification(newLevel);
    }
    else
    {
        hasLeveledUpBefore = true;
    }
}

private void UpdateLevelUI(int level)
{
    if (levelText != null)
    {
        levelText.text = $"{level}";
    }
}

public void ShowLevelUpNotification(int level)
{
    if (levelUpPanel == null || levelUpText == null) return;

    // Text'i güncelle
    levelUpText.text = $"Seviye {level}";
    
    // Panel'i aktif et
    levelUpPanel.SetActive(true);
    
    // Animasyonu başlat
    StartCoroutine(LevelUpAnimationCoroutine());
}

private IEnumerator LevelUpAnimationCoroutine()
{
    RectTransform panelRect = levelUpPanel.GetComponent<RectTransform>();
    
    // Başlangıç değerleri
    Vector2 startPos = new Vector2(0, Screen.height);
    Vector2 targetPos = Vector2.zero;
    Vector2 endPos = new Vector2(0, -Screen.height);
    
    Vector3 startScale = Vector3.zero;
    Vector3 targetScale = Vector3.one;
    Vector3 endScale = Vector3.zero;
    
    // Başlangıç konumunu ayarla
    panelRect.anchoredPosition = startPos;
    levelUpPanel.transform.localScale = startScale;
    
    // 1. Fase: Panel yukarıdan gelir ve büyür (0.8 saniye)
    float duration1 = 0.8f;
    float elapsed = 0f;
    
    while (elapsed < duration1)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration1;
        
        // Easing için smooth curve
        float easedT = 1f - Mathf.Pow(1f - t, 3f); // Ease out cubic
        
        panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, easedT);
        levelUpPanel.transform.localScale = Vector3.Lerp(startScale, targetScale, easedT);
        
        yield return null;
    }
    
    // Tam pozisyona yerleştir
    panelRect.anchoredPosition = targetPos;
    levelUpPanel.transform.localScale = targetScale;
    
    // 2. Fase: 2 saniye bekle
    yield return new WaitForSeconds(2f);
    
    // 3. Fase: Panel küçülür ve aşağı gider (0.5 saniye)
    float duration2 = 0.5f;
    elapsed = 0f;
    
    while (elapsed < duration2)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration2;
        
        // Easing için smooth curve
        float easedT = Mathf.Pow(t, 3f); // Ease in cubic
        
        panelRect.anchoredPosition = Vector2.Lerp(targetPos, endPos, easedT);
        levelUpPanel.transform.localScale = Vector3.Lerp(targetScale, endScale, easedT);
        
        yield return null;
    }
    
    // Panel'i deaktif et
    levelUpPanel.SetActive(false);
}

private void OnDestroy()
{
    if (playerStats != null)
    {
        playerStats.OnNetworkHealthChanged -= UpdateHealthUI;
        playerStats.OnLevelChanged -= OnLevelUp;
        playerStats.OnXPChanged -= UpdateXPUI;
        playerStats.OnCoinsChanged -= UpdateCoinsUI;
        
        // Buff event'lerini temizle
        var tempBuffSystem = playerStats.GetComponent<TemporaryBuffSystem>();
        if (tempBuffSystem != null)
        {
            tempBuffSystem.OnBuffStarted -= ShowBuffIcon;
            tempBuffSystem.OnBuffEnded -= HideBuffIcon;
        }
    }
}

    private void UpdateHealthUI(float currentHealth)
    {
        if (healthBarFill != null && healthText != null && playerStats != null)
        {
            float healthPercent = currentHealth / playerStats.MaxHP;
            healthBarFill.fillAmount = healthPercent;
            
            healthText.text = $"{Mathf.FloorToInt(currentHealth)} / {Mathf.FloorToInt(playerStats.MaxHP)}";
            
            if (healthPercentText != null)
            {
                healthPercentText.text = $"{Mathf.FloorToInt(healthPercent * 100)}%";
            }
        }
    }


    private void UpdateXPUI(float currentXP)
    {
        if (xpBarFill != null && xpText != null && playerStats != null)
        {
            float requiredXP = playerStats.GetRequiredXPForNextLevel();
            float xpPercent = currentXP / requiredXP;
            
            xpBarFill.fillAmount = xpPercent;
            xpText.text = $"{Mathf.FloorToInt(currentXP)} / {Mathf.FloorToInt(requiredXP)}";
            
            if (xpPercentText != null)
            {
                xpPercentText.text = $"{Mathf.FloorToInt(xpPercent * 100)}%";
            }
        }
    }

    private void UpdateCoinsUI(int coins)
    {
        if (coinText != null)
        {
            coinText.text = $"Altın: {coins}";
        }
    }
}