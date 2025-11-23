// Path: Assets/Game/Scripts/BlacksmithNPC.cs

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System.Collections;
using DG.Tweening;
using System.Collections.Generic;

public class BlacksmithNPC : BaseNPC
{

[Header("UI References - Auto Found")]
private GameObject upgradePanel;
private UISlot upgradeSlot;
private TextMeshProUGUI titleText;
private Image itemIcon;
private TextMeshProUGUI costText;
private TextMeshProUGUI materialsText; // YENİ
private TextMeshProUGUI chanceText;
private Button upgradeButton;
private TextMeshProUGUI buttonText;
private CraftInventorySystem craftInventorySystem; // Class seviyesinde field ekle

    [Header("UI States")]
    [SerializeField] private bool showEmptyPanelOnStart = false;
    [SerializeField] private Sprite defaultUpgradeImage;

    private InventorySystem playerInventory;
    public bool IsUpgradePanelOpen => upgradePanel != null && upgradePanel.activeSelf;

protected override void Start()
{
    base.Start();
    FindUpgradeUIComponents();
    
    // CraftInventorySystem referansını bul
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj != null && networkObj.HasInputAuthority)
        {
            craftInventorySystem = player.GetComponent<CraftInventorySystem>();
            break;
        }
    }
        
    if (upgradeSlot != null)
    {
        upgradeSlot.isUpgradeSlot = true;
    }
}

protected override void Awake()
{
    base.Awake();
    npcType = NPCType.Blacksmith; // NPC tipini ayarla
}
private void FindUpgradeUIComponents()
{
    GameObject gameUI = GameObject.Find("GameUI");
    if (gameUI == null)
    {
        Debug.LogError("[BlacksmithNPC] GameUI bulunamadı!");
        return;
    }

    Transform infoPanelManagerTransform = gameUI.transform.Find("InfoPanelManager");
    if (infoPanelManagerTransform == null)
    {
        Debug.LogError("[BlacksmithNPC] InfoPanelManager bulunamadı!");
        return;
    }

    Transform equStatPanel = infoPanelManagerTransform.Find("Equ_StatPanel");
    if (equStatPanel == null)
    {
        Debug.LogError("[BlacksmithNPC] Equ_StatPanel bulunamadı!");
        return;
    }

    Transform equUpgradePanel = equStatPanel.Find("Equ_UpgradePanel");
    if (equUpgradePanel == null)
    {
        Debug.LogError("[BlacksmithNPC] Equ_UpgradePanel bulunamadı!");
        return;
    }

    upgradePanel = equUpgradePanel.Find("UpgradePanel")?.gameObject;
    if (upgradePanel == null)
    {
        Debug.LogError("[BlacksmithNPC] UpgradePanel bulunamadı!");
        return;
    }


    titleText = upgradePanel.transform.Find("ItemUpgradeTitle")?.GetComponent<TextMeshProUGUI>();
    upgradeSlot = upgradePanel.transform.Find("BackgroundUpgradeImage/UpgradeSlot")?.GetComponent<UISlot>();
    
    if (upgradeSlot != null)
    {
        itemIcon = upgradeSlot.transform.Find("UpgradeItemIcon")?.GetComponent<Image>();
        upgradeSlot.isUpgradeSlot = true;
    }
    else
    {
        Debug.LogError("[BlacksmithNPC] UpgradeSlot bulunamadı!");
    }

    costText = upgradePanel.transform.Find("UpgradeCostText")?.GetComponent<TextMeshProUGUI>();
    materialsText = upgradePanel.transform.Find("UpgradeMaterialsText")?.GetComponent<TextMeshProUGUI>();
    chanceText = upgradePanel.transform.Find("UpgradeChanceText")?.GetComponent<TextMeshProUGUI>();
    upgradeButton = upgradePanel.transform.Find("UpgradeButton")?.GetComponent<Button>();

    if (upgradeButton != null)
    {
        buttonText = upgradeButton.transform.Find("UpgradeButtonText")?.GetComponent<TextMeshProUGUI>();
        upgradeButton.onClick.RemoveAllListeners(); // Duplicate listener önleme
        upgradeButton.onClick.AddListener(OnUpgradeButtonClicked);
    }
    else
    {
        Debug.LogError("[BlacksmithNPC] UpgradeButton bulunamadı!");
    }

    bool isValid = ValidateComponents();
    
    if (isValid)
    {
        if (showEmptyPanelOnStart)
        {
            ShowCleanUpgradeState();
        }
        else
        {
            upgradePanel.SetActive(false);
        }
    }
}
private void ShowCleanUpgradeState()
{
    if (!ValidateComponents()) 
    {
        return;
    }

    if (upgradePanel != null)
    {
        upgradePanel.SetActive(true);
    }

    if (itemIcon != null)
    {
        if (defaultUpgradeImage != null)
        {
            itemIcon.sprite = defaultUpgradeImage;
            itemIcon.color = new Color(1f, 1f, 1f, 0.5f);
        }
        else
        {
            itemIcon.color = new Color(0f, 0f, 0f, 0f);
        }
    }

if (titleText != null)
{
    titleText.text = "Ekipmanınızı Geliştirin";
    titleText.fontSize = 24;
    titleText.color = new Color(1f, 0.84f, 0f, 1f);
    titleText.fontStyle = TMPro.FontStyles.Bold;
}

if (costText != null)
{
    costText.text = "Geliştirme Maliyetini Görmek İçin Eşya Yerleştirin";
    costText.fontSize = 20;
    costText.color = new Color(0.4f, 0.8f, 1f, 0.9f);
    costText.fontStyle = TMPro.FontStyles.Normal;
}

if (materialsText != null)
{
    materialsText.text = "Gerekli Malzemeler Burada Görünecek";
    materialsText.fontSize = 18;
    materialsText.color = new Color(1f, 0.6f, 0.2f, 0.9f);
    materialsText.fontStyle = TMPro.FontStyles.Normal;
}

if (chanceText != null)
{
    chanceText.text = "Başarı Oranı Bekliyor";
    chanceText.fontSize = 20;
    chanceText.color = new Color(0.4f, 1f, 0.4f, 0.9f);
    chanceText.fontStyle = TMPro.FontStyles.Normal;
}

if (upgradeButton != null)
{
    upgradeButton.interactable = false;
    
    if (buttonText != null)
    {
        buttonText.text = "Başlamak İçin Eşya Seçin";
        buttonText.fontSize = 22;
        buttonText.color = Color.white;
        buttonText.fontStyle = TMPro.FontStyles.Bold;
    }
}
}

private bool ValidateComponents()
{

    bool isValid = titleText != null &&
           upgradeSlot != null &&
           itemIcon != null &&
           costText != null &&
           materialsText != null &&
           chanceText != null &&
           upgradeButton != null &&
           buttonText != null;

    return isValid;
}
    public UISlot GetUpgradeSlot()
    {
        if (upgradeSlot == null)
        {

        }
        return upgradeSlot;
    }

// Public wrapper metod - InfoPanelManager tarafından çağrılabilir
public void CloseUpgradePanel()
{
    CloseInteractionPanel();
}

// Override metod aynı kalır - access modifier değiştirilmez
protected override void CloseInteractionPanel()
{
    ItemData slotItem = upgradeSlot.GetItemInfo();

    if (slotItem != null && playerInventory != null)
    {
        playerInventory.TryAddItem(slotItem);
        upgradeSlot.ClearSlot();
    }

    InfoPanelManager infoPanelManager = GetInfoPanelManager();
    if (infoPanelManager != null)
    {
        infoPanelManager.ShowPanels(); // Reset all panels to false
        infoPanelManager.CloseInfoPanel();
    }

    if (upgradePanel != null)
    {
        upgradePanel.SetActive(false);
    }
}

private InfoPanelManager GetInfoPanelManager()
{
    // Önce GameUI'ı bul
    GameObject gameUI = GameObject.Find("GameUI");
    if (gameUI != null)
    {
        // InfoPanelManager objesini bul
        Transform InfoPanelManagerTransform = gameUI.transform.Find("InfoPanelManager");
        if (InfoPanelManagerTransform != null)
        {
            InfoPanelManager manager = InfoPanelManagerTransform.GetComponent<InfoPanelManager>();
            return manager;
        }
    }
    
    return null;
}
private void OnUpgradeButtonClicked()
{
    ItemData currentItem = upgradeSlot.GetItemInfo();
    if (currentItem == null)
    {
        return;
    }

    GameObject localPlayer = null;
    PlayerStats playerStats = null;
    foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
    {
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj != null && networkObj.HasInputAuthority)
        {
            localPlayer = player;
            playerStats = player.GetComponent<PlayerStats>();
            break;
        }
    }

    if (playerStats == null)
    {
        return;
    }

    // CraftInventorySystem referansını kontrol et
    if (craftInventorySystem == null)
    {
        craftInventorySystem = localPlayer.GetComponent<CraftInventorySystem>();
        if (craftInventorySystem == null)
        {
            ShowNotification("Craft inventory bulunamadı!");
            return;
        }
    }

    int upgradeCost = currentItem.CalculateUpgradeCost();
    float upgradeChance = currentItem.CalculateUpgradeChance();
    Dictionary<string, int> requiredMaterials = currentItem.GetRequiredUpgradeMaterials();

    // Gold kontrolü
    if (playerStats.Coins < upgradeCost)
    {
        ShowNotification("Yetersiz altın!");
        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.ShowUpgradeMaterialNeededMessage($"{upgradeCost - playerStats.Coins} Gold daha gerekli!");
        }
        return;
    }

    // Malzeme kontrolleri
    foreach (var material in requiredMaterials)
    {
        if (!craftInventorySystem.HasItem(material.Key, material.Value))
        {
            ItemData materialItem = ItemDatabase.Instance.GetItemById(material.Key);
            string materialName = materialItem != null ? materialItem.itemName : material.Key;
            
            // Mevcut miktar
            int currentAmount = 0;
            foreach (var slot in craftInventorySystem.GetAllCraftSlots().Values)
            {
                if (!slot.isEmpty && slot.item != null && slot.item.itemId == material.Key)
                {
                    currentAmount += slot.amount;
                }
            }
            
            int neededAmount = material.Value - currentAmount;
            ShowNotification($"Yetersiz {materialName}!");
            
            if (ChatManager.Instance != null)
            {
                ChatManager.Instance.ShowUpgradeMaterialNeededMessage($"{neededAmount} adet {materialName} daha gerekli!");
            }
            return;
        }
    }

    // Tüm kontroller geçti - malzemeleri tüket
    foreach (var material in requiredMaterials)
    {
        if (!craftInventorySystem.ConsumeItems(material.Key, material.Value))
        {
            ShowNotification("Malzeme tüketimi başarısız!");
            return;
        }
    }

    // Gold tüket
    playerStats.AddCoins(-upgradeCost);

    // Upgrade denemesi
    float randomValue = UnityEngine.Random.Range(0f, 100f);

    if (randomValue <= upgradeChance)
    {
        ItemData upgradedItem = currentItem.CreateUpgradedVersion();
        if (upgradedItem != null)
        {
            ShowNotification($"Geliştirme başarılı! +{upgradedItem.upgradeLevel}");
            upgradeSlot.UpdateSlot(upgradedItem);
            UpdateUpgradeInfo(upgradedItem);
            PlayUpgradeEffect(true);

            var itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
            if (itemInfoPanel != null)
            {
                itemInfoPanel.ShowItemInfo(upgradeSlot);
            }
        }
    }
    else
    {
        ShowNotification("Geliştirme başarısız! Eşya kayboldu!");
        upgradeSlot.ClearSlot();
        UpdateUpgradeInfo(null);
        PlayUpgradeEffect(false);

        var itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
        if (itemInfoPanel != null)
        {
            itemInfoPanel.ClosePanel();
        }
    }
}

private void ShowNotification(string message)
{
    GameObject notificationObj = new GameObject("Notification");
    notificationObj.transform.SetParent(upgradePanel.transform, false);

    RectTransform rect = notificationObj.AddComponent<RectTransform>();
    rect.anchorMin = new Vector2(0.5f, 0.5f);
    rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.sizeDelta = new Vector2(200, 40);
    rect.anchoredPosition = new Vector2(0, -150);

    TextMeshProUGUI text = notificationObj.AddComponent<TextMeshProUGUI>();
    text.text = message;
    text.fontSize = 20;
    text.alignment = TextAlignmentOptions.Center;
    text.color = Color.white;

    // 2 saniye sonra yok et
    StartCoroutine(DestroyAfterDelay(notificationObj, 2f));
}

private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
{
    yield return new WaitForSeconds(delay);
    if (obj != null)
    {
        Destroy(obj);
    }
}
public override void HandleNPCTypeInteraction()
{
    
    // Önce player inventory'yi bul
    if (playerInventory == null)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject networkObj = player.GetComponent<NetworkObject>(); 
            if (networkObj != null && networkObj.HasInputAuthority)
            {
                playerInventory = player.GetComponent<InventorySystem>();
                break;
            }
        }
    }
    
    // UI componentlerini kontrol et - yoksa tekrar bul
    if (!ValidateComponents())
    {
        FindUpgradeUIComponents();
    }
    
    GameObject gameUI = GameObject.Find("GameUI");
    if (gameUI != null)
    {
        Transform infoPanelManagerTransform = gameUI.transform.Find("InfoPanelManager");
        if (infoPanelManagerTransform != null)
        {
            InfoPanelManager infoPanelManager = infoPanelManagerTransform.GetComponent<InfoPanelManager>();
            if (infoPanelManager != null)
            {
                infoPanelManager.ShowBlacksmithPanels();
                
                // Panel açıldıktan sonra bir kez daha kontrol
                if (ValidateComponents())
                {
                    ShowCleanUpgradeState();
                }}}}
}
private void PlayUpgradeEffect(bool success)
{
    // Eğer upgradePanel null ise return
    if (upgradePanel == null) return;

    // GameObject oluştur ve parent'a ekle
    GameObject effectObj = new GameObject("UpgradeEffect", typeof(RectTransform));  // RectTransform ekledik
    effectObj.transform.SetParent(upgradePanel.transform, false);

    // RectTransform'u al ve ayarla
    RectTransform rect = effectObj.GetComponent<RectTransform>();
    rect.anchorMin = new Vector2(0.5f, 0.5f);
    rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.sizeDelta = new Vector2(200, 200); // Efekt boyutu

    // Image component'i ekle ve ayarla
    Image effectImage = effectObj.AddComponent<Image>();
    effectImage.color = success ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);

    // Animasyon sequence'i oluştur
    rect.localScale = Vector3.zero;
    Sequence upgradeSequence = DOTween.Sequence();

    upgradeSequence.Append(rect.DOScale(1.5f, 0.3f).SetEase(Ease.OutBack))
                  .Join(rect.DORotate(new Vector3(0, 0, 360), 0.3f, RotateMode.FastBeyond360))
                  .Join(effectImage.DOFade(0, 0.3f))
                  .Append(rect.DOScale(0, 0.2f))
                  .OnComplete(() => {
                      if (effectObj != null)
                      {
                          Destroy(effectObj);
                      }
                  });
}
    public void UpdateUpgradeInfo(ItemData item)
    {
        if (item != null)
        {
            int cost = item.CalculateUpgradeCost();
            float chance = item.CalculateUpgradeChance();
            Dictionary<string, int> materials = item.GetRequiredUpgradeMaterials();

            if (costText != null)
            {
                costText.text = $"Maliyet: {cost} Altın";
                costText.color = Color.white;
            }

            if (materialsText != null)
            {
                if (materials.Count > 0)
                {
                    List<string> materialTexts = new List<string>();
                    bool allMaterialsAvailable = true;
                    _ = allMaterialsAvailable;

                    foreach (var mat in materials)
                    {
                        ItemData matItem = ItemDatabase.Instance.GetItemById(mat.Key);
                        string matName = matItem != null ? matItem.itemName : mat.Key;

                        // Mevcut miktarı al
                        int currentAmount = GetCurrentMaterialAmount(mat.Key);
                        int requiredAmount = mat.Value;

                        // Renk belirle
                        string colorTag = currentAmount >= requiredAmount ? "<color=#00FF00>" : "<color=#FF0000>";
                        string closeTag = "</color>";

                        materialTexts.Add($"{colorTag}{currentAmount}/{requiredAmount}{closeTag} {matName}");

                        if (currentAmount < requiredAmount)
                        {
                            allMaterialsAvailable = false;
                        }
                    }

                    materialsText.text = "Malzemeler: " + string.Join(", ", materialTexts);
                    materialsText.color = Color.white;
                }
                else
                {
                    materialsText.text = "Malzeme gerektirmez";
                    materialsText.color = new Color(0.7f, 0.7f, 0.7f);
                }
            }

            if (chanceText != null)
            {
                chanceText.text = $"Başarı: %{chance}";
                chanceText.color = Color.white;
            }

            if (upgradeButton != null)
            {
                upgradeButton.interactable = item.CanUpgrade();
                if (buttonText != null)
                {
                    buttonText.text = "Geliştir";
                    buttonText.color = Color.white;
                }
            }

            if (titleText != null)
            {
                titleText.text = item._baseItemName;
                titleText.color = Color.white;
            }
        }
        else
        {
            ShowCleanUpgradeState();
        }
    }
private int GetCurrentMaterialAmount(string itemId)
{
    if (craftInventorySystem == null) return 0;
    
    int totalAmount = 0;
    var allSlots = craftInventorySystem.GetAllCraftSlots();
    
    foreach (var slot in allSlots.Values)
    {
        if (!slot.isEmpty && slot.item != null && slot.item.itemId == itemId)
        {
            totalAmount += slot.amount;
        }
    }
    
    return totalAmount;
}
}