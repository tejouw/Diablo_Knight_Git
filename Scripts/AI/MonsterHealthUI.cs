using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

public class MonsterHealthUI : NetworkBehaviour
{
    [Header("UI Settings")]
[SerializeField] private float canvasYOffset = 4f;
[SerializeField] private float canvasScale = 0.05f;
[SerializeField] private float nameTextSize = 14f;
[SerializeField] private float healthTextSize = 10f;
[SerializeField] private float sliderWidth = 100f;
[SerializeField] private float sliderHeight = 10f;
    [Header("UI Components")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject healthBarPanel;
    
    private MonsterBehaviour monsterBehaviour;
    private Camera mainCamera;
    private Transform cameraTransform;
    
    // Performance
    private float lastUpdateTime = 0f;
    private const float UPDATE_INTERVAL = 0.1f;
    
    private void Awake()
    {
        monsterBehaviour = GetComponent<MonsterBehaviour>();
        SetupCanvas();
        
        // Başlangıçta gizle
        if (healthBarPanel != null)
            healthBarPanel.SetActive(false);
    }
    
private void SetupCanvas()
{
    // World Space Canvas yoksa oluştur
    if (worldCanvas == null)
    {
        GameObject canvasObj = new GameObject("HealthBarCanvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = new Vector3(0, canvasYOffset, 0);
        
        worldCanvas = canvasObj.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.sortingOrder = 100;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Canvas boyutu
        RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2, 1);
        canvasRect.localScale = Vector3.one * canvasScale;
        
        CreateHealthBarUI();
    }
}
    
private void CreateHealthBarUI()
{
    // Panel
    GameObject panel = new GameObject("HealthBarPanel");
    panel.transform.SetParent(worldCanvas.transform, false);
    healthBarPanel = panel;
    
    RectTransform panelRect = panel.AddComponent<RectTransform>();
    panelRect.anchoredPosition = Vector2.zero;
    panelRect.sizeDelta = new Vector2(120, 40);
    
    // Name Text
    GameObject nameObj = new GameObject("NameText");
    nameObj.transform.SetParent(panel.transform, false);
    nameText = nameObj.AddComponent<TextMeshProUGUI>();
    RectTransform nameRect = nameText.rectTransform;
    nameRect.anchoredPosition = new Vector2(0, 15);
    nameRect.sizeDelta = new Vector2(200, 20);
    nameText.alignment = TextAlignmentOptions.Center;
    nameText.fontSize = nameTextSize;
    
    // Slider
    GameObject sliderObj = new GameObject("HealthSlider");
    sliderObj.transform.SetParent(panel.transform, false);

    // Slider component ekle
    healthSlider = sliderObj.AddComponent<Slider>();

    // RectTransform al
    RectTransform sliderRect = healthSlider.GetComponent<RectTransform>();
    sliderRect.sizeDelta = new Vector2(sliderWidth, sliderHeight);
    sliderRect.anchoredPosition = Vector2.zero;
    
    // Slider Background
    GameObject bgObj = new GameObject("Background");
    bgObj.transform.SetParent(sliderObj.transform, false);
    Image bgImage = bgObj.AddComponent<Image>();
    bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    RectTransform bgRect = bgImage.rectTransform;
    bgRect.anchorMin = Vector2.zero;
    bgRect.anchorMax = Vector2.one;
    bgRect.sizeDelta = Vector2.zero;
    bgRect.anchoredPosition = Vector2.zero;
    
    // Slider Fill
    GameObject fillAreaObj = new GameObject("Fill Area");
    fillAreaObj.transform.SetParent(sliderObj.transform, false);
    RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
    fillAreaRect.anchorMin = Vector2.zero;
    fillAreaRect.anchorMax = Vector2.one;
    fillAreaRect.sizeDelta = Vector2.zero;
    fillAreaRect.anchoredPosition = Vector2.zero;
    
    GameObject fillObj = new GameObject("Fill");
    fillObj.transform.SetParent(fillAreaObj.transform, false);
    Image fillImage = fillObj.AddComponent<Image>();
    fillImage.color = Color.green;
    RectTransform fillRect = fillImage.rectTransform;
    fillRect.anchorMin = new Vector2(0, 0);
    fillRect.anchorMax = new Vector2(1, 1);
    fillRect.sizeDelta = Vector2.zero;
    fillRect.anchoredPosition = Vector2.zero;
    
    healthSlider.fillRect = fillRect;
    healthSlider.targetGraphic = fillImage;
    
    // Health Text
    GameObject textObj = new GameObject("HealthText");
    textObj.transform.SetParent(panel.transform, false);
    healthText = textObj.AddComponent<TextMeshProUGUI>();
    RectTransform textRect = healthText.rectTransform;
    textRect.anchoredPosition = new Vector2(0, -15);
    textRect.sizeDelta = new Vector2(100, 15);
    healthText.alignment = TextAlignmentOptions.Center;
    healthText.fontSize = healthTextSize;
    healthText.color = Color.yellow;
}
    
public override void Spawned()
{
    if (Runner.IsClient)
    {
        // Camera bul
        mainCamera = Camera.main;
        if (mainCamera != null)
            cameraTransform = mainCamera.transform;
        
        // Monster bilgilerini ayarla
        UpdateMonsterInfo();
    }
}
    
    private void UpdateMonsterInfo()
    {
        if (monsterBehaviour == null) return;
        
        // Monster ismini ayarla
        if (nameText != null)
        {
            string prefix = monsterBehaviour.RarityPrefix;
            string name = monsterBehaviour.MonsterType;
            int level = monsterBehaviour.NetworkCoreData.MonsterLevel;
            nameText.text = $"{prefix}{name} Lv.{level}";
            nameText.color = GetRarityColor(monsterBehaviour.Rarity);
        }
        
        // İlk health değerini ayarla
        UpdateHealthDisplay();
    }
    
public override void Render()
{
    // Sadece client'da çalış
    if (!Runner.IsClient) return;
    
    // Monster ölü mü?
    if (monsterBehaviour.IsDead)
    {
        if (healthBarPanel != null && healthBarPanel.activeSelf)
            healthBarPanel.SetActive(false);
        return;
    }
    
    // Health bar'ı göster
    if (healthBarPanel != null && !healthBarPanel.activeSelf)
    {
        healthBarPanel.SetActive(true);
        UpdateMonsterInfo();
    }
    
    // Throttled update
    if (Time.time - lastUpdateTime >= UPDATE_INTERVAL)
    {
        UpdateHealthDisplay();
        lastUpdateTime = Time.time;
    }
}
    
    private void UpdateHealthDisplay()
    {
        if (monsterBehaviour == null) return;
        
        float currentHealth = monsterBehaviour.NetworkCoreData.Health;
        float maxHealth = monsterBehaviour.NetworkCoreData.MaxHealth;
        float healthPercent = maxHealth > 0 ? currentHealth / maxHealth : 0;
        
        // Slider güncelle
        if (healthSlider != null)
        {
            healthSlider.value = healthPercent;
            
            // Renk değiştir
            Image fillImage = healthSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = healthPercent > 0.6f ? Color.green :
                                 healthPercent > 0.3f ? Color.yellow : Color.red;
            }
        }
        
        // Text güncelle
        if (healthText != null)
        {
            healthText.text = $"{Mathf.Round(currentHealth)}/{Mathf.Round(maxHealth)}";
        }
    }
    
    private Color GetRarityColor(MonsterRarity rarity)
    {
        return rarity switch
        {
            MonsterRarity.Magic => new Color(0.5f, 0.8f, 1f),
            MonsterRarity.Rare => new Color(1f, 0.8f, 0.2f),
            _ => Color.white
        };
    }
}