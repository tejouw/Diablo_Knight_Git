// Path: Assets/Game/Scripts/InfoPanelManager.cs

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Fusion;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using System.Collections.Generic;

public class InfoPanelManager : MonoBehaviour
{
public static InfoPanelManager Instance { get; private set; }
[SerializeField] public GameObject infoPanel;
[SerializeField] private Button toggleButton;
[SerializeField] private Button closeButton;
[SerializeField] private GameObject craftNPCPanel;
[SerializeField] private CraftNPCUIManager craftNPCUIManager;
private EquipmentSystem equipmentSystem;
private bool wasChatOpenBeforeInfoPanel = false;
[SerializeField] private GameObject inventoryPanel;
[SerializeField] private GameObject itemInfoPanel;
[SerializeField] private GameObject equipmentPanel;
[SerializeField] private GameObject statsPanel;
[SerializeField] private GameObject upgradePanel;
[SerializeField] private GameObject previewAnimation;
[SerializeField] private GameObject merchantPanel;
[SerializeField] private GameObject craftInventoryPanel; // YENİ
[SerializeField] private MerchantPanel merchantPanelScript;
    [Header("Materials Bag")]
[SerializeField] private Button materialsBagButton;
public void ShowPanels(bool showInventory = false, bool showItemInfo = false, 
                     bool showEquipment = false, bool showStats = false, 
                     bool showUpgrade = false, bool showMerchant = false,
                     bool showPreview = false, bool showCraftInventory = false,
                     bool showCraftNPC = false, bool showEquStatPanel = false)
{
    // YENI: Upgrade panel kapanacaksa ve şu an açıksa, item'ı inventory'ye döndür
    if (!showUpgrade && upgradePanel != null && upgradePanel.activeSelf)
    {
        ReturnUpgradeSlotItemToInventory();
    }
    
    // Önce panelleri göster/gizle
    if (inventoryPanel != null) inventoryPanel.SetActive(showInventory);
    if (equipmentPanel != null) equipmentPanel.SetActive(showEquipment);
    if (statsPanel != null) statsPanel.SetActive(showStats);
    if (upgradePanel != null) upgradePanel.SetActive(showUpgrade);
    if (merchantPanel != null) merchantPanel.SetActive(showMerchant);
    if (craftInventoryPanel != null) craftInventoryPanel.SetActive(showCraftInventory);
    if (craftNPCPanel != null) craftNPCPanel.SetActive(showCraftNPC);
    
    // ItemInfoPanel'i sonra ayarla
    ItemInfoPanel itemInfoPanel = transform.GetComponentInChildren<ItemInfoPanel>(true);
    if (itemInfoPanel != null)
    {
        itemInfoPanel.gameObject.SetActive(showItemInfo);
        
        if (showItemInfo)
        {
            // Mode'ları ayarla
            itemInfoPanel.SetMerchantMode(showMerchant);
            itemInfoPanel.SetCraftMode(showCraftNPC);
            
            // Temiz state'e getir
            if (!showMerchant && !showCraftNPC)
            {
                itemInfoPanel.ClosePanel();
            }
        }
    }
    
    // Equ_StatPanel'i ayrı kontrol et
    Transform equStatPanel = transform.Find("Equ_StatPanel");
    if (equStatPanel != null)
    {
        equStatPanel.gameObject.SetActive(showEquStatPanel);
    }
}
// InfoPanelManager.cs'e bu alanları ekle

[Header("Stats Panel References")]
[SerializeField] private TextMeshProUGUI moveSpeedText;
[SerializeField] private TextMeshProUGUI critChanceText;
[SerializeField] private TextMeshProUGUI armorText;
[SerializeField] private TextMeshProUGUI attackPowerText; 
[SerializeField] private TextMeshProUGUI attackSpeedText;
[SerializeField] private TextMeshProUGUI playerClassText; // YENİ SATIR

private ClassSystem classSystem; // YENİ ALAN
    private PlayerStats playerStats;
public void ShowCraftNPCPanels()
{
    if (infoPanel != null)
    {
        infoPanel.SetActive(true);
        
        // ClassPanel'i kapat
        if (UIManager.Instance != null && UIManager.Instance.classPanel != null)
        {
            UIManager.Instance.classPanel.SetActive(false);
        }

        // CraftNPC UI Manager'ı initialize et
        if (craftNPCUIManager != null)
        {
            CraftNPC currentCraftNPC = FindFirstObjectByType<CraftNPC>();
            if (currentCraftNPC != null)
            {
                craftNPCUIManager.Initialize(currentCraftNPC);
            }
        }        
        
        ShowPanels(
            showInventory: false, 
            showItemInfo: true,  
            showEquipment: false, 
            showStats: false, 
            showUpgrade: false,
            showMerchant: false,
            showPreview: false,
            showCraftInventory: true,
            showCraftNPC: true
        );
    }
}
public void ShowMerchantPanels()
{
    if (infoPanel != null)
    {
        infoPanel.SetActive(true);
        
        // ClassPanel'i kapat
        if (UIManager.Instance != null && UIManager.Instance.classPanel != null)
        {
            UIManager.Instance.classPanel.SetActive(false);
        }

        if(merchantPanelScript != null)
        {
            // Mevcut merchant'ı bul
            MerchantNPC currentMerchant = FindFirstObjectByType<MerchantNPC>();
            if(currentMerchant != null)
            {
                merchantPanelScript.Initialize(currentMerchant);
            }

        }        
        ShowPanels(
            showInventory: true, 
            showItemInfo: true,  
            showEquipment: false, 
            showStats: false, 
            showUpgrade: false,
            showMerchant: true,
            showPreview: false
        );
    }
}
public void Initialize(PlayerStats stats)
{
    playerStats = stats;
    string playerName = stats?.GetPlayerDisplayName() ?? "Bilinmiyor";
    
    
    // ClassSystem referansını al
    if (stats != null)
    {
        classSystem = stats.GetComponent<ClassSystem>();
        
        // Class değişiklik event'ine subscribe ol
        if (classSystem != null)
        {
            classSystem.OnClassChanged += OnPlayerClassChanged;
            
            // Mevcut class'ı hemen göster
            ClassType currentClass = classSystem.NetworkPlayerClass;
            OnPlayerClassChanged(currentClass);
        }
        else
        {
            Debug.LogError($"[InfoPanel-{playerName}] ClassSystem component NOT FOUND!");
        }
    }
    else
    {
        Debug.LogError("[InfoPanel] PlayerStats null!");
    }
    


}
private void OnPlayerClassChanged(ClassType newClass)
{
    string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";
    
    
    // UI'ı hemen güncelle
    UpdateClassDisplay();
    
    // PlayerStats'e RecalculateStats çağırması için bildir
    if (playerStats != null)
    {
        playerStats.RecalculateStats();
    }
    else
    {
        Debug.LogError($"[InfoPanel-{playerName}] PlayerStats NULL!");
    }
}
private void UpdateClassDisplay()
{
    string playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";
    
    if (playerClassText != null && classSystem != null)
    {
        ClassType currentClass = classSystem.NetworkPlayerClass;
        string classDisplayName = currentClass switch
        {
            ClassType.None => "Sınıf Yok",
            ClassType.Warrior => "Savaşçı",
            ClassType.Ranger => "Okçu",
            ClassType.Rogue => "Haydut",
            _ => "Bilinmiyor"
        };

        playerClassText.text = $"Sınıf: {classDisplayName}";
    }
    else
    {
        if (playerClassText == null)
            Debug.LogError($"[InfoPanel-{playerName}] playerClassText null!");
        if (classSystem == null)
            Debug.LogError($"[InfoPanel-{playerName}] classSystem null!");
    }
}

[Header("Character Preview")]
[SerializeField] private Transform characterPreviewHolder;



private GameObject FindLocalPlayer()
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject networkObject = player.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.HasInputAuthority)
        {
            return player;
        }
    }
    return null;
}
private void Start()
{
    Instance = this;
    if (infoPanel != null)
    {
        infoPanel.SetActive(false);
    }

    if (toggleButton != null)
        toggleButton.onClick.AddListener(ToggleInfoPanel);
    if (materialsBagButton != null)
    {
        materialsBagButton.onClick.RemoveAllListeners();
        materialsBagButton.onClick.AddListener(ToggleMaterialsBag);
    }
    else
    {
        Debug.LogError("[InfoPanelManager] Materials bag button referansı bulunamadı!");
    }

    if (closeButton != null)
        closeButton.onClick.AddListener(CloseInfoPanel);

    // YENİ EKLEME: Character snapshot sistem entegrasyonu
    InitializeCharacterSnapshot();

    if (playerStats == null)
    {
        GameObject player = FindLocalPlayer();
        if (player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }
    }
}

    private void InitializeCharacterSnapshot()
    {
        CharacterSnapshotSystem snapshotSystem = CharacterSnapshotSystem.Instance;

        if (snapshotSystem == null)
        {
            Debug.Log("[InfoPanelManager] Creating new CharacterSnapshotSystem instance");
            GameObject snapshotObj = new GameObject("CharacterSnapshotSystem");
            snapshotSystem = snapshotObj.AddComponent<CharacterSnapshotSystem>();
            DontDestroyOnLoad(snapshotObj); // Scene değişimlerinde korunması için
        }
        else
        {
            Debug.Log("[InfoPanelManager] Using existing CharacterSnapshotSystem instance");
        }

        if (characterPreviewHolder != null)
        {
            RawImage previewImage = characterPreviewHolder.GetComponentInChildren<RawImage>();

            if (previewImage == null)
            {
                Debug.Log("[InfoPanelManager] Creating RawImage for character preview");
                GameObject imageObj = new GameObject("CharacterPreview");
                imageObj.transform.SetParent(characterPreviewHolder, false);

                previewImage = imageObj.AddComponent<RawImage>();
                previewImage.raycastTarget = false;

                RectTransform rectTransform = previewImage.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(450, 450);
                rectTransform.anchoredPosition = Vector2.zero;

                // Başlangıçta boş texture ayarla
                previewImage.color = Color.white;
            }
            else
            {
                Debug.Log("[InfoPanelManager] Found existing RawImage for character preview");
            }

            // Snapshot system'e preview image'i ata
            if (snapshotSystem != null)
            {
                snapshotSystem.SetPreviewImage(previewImage);
                Debug.Log("[InfoPanelManager] Preview image assigned to snapshot system");
            }
            else
            {
                Debug.LogError("[InfoPanelManager] Snapshot system is null after initialization!");
            }
        }
        else
        {
            Debug.LogError("[InfoPanelManager] characterPreviewHolder is null! Check Inspector references!");
        }
    }

private bool wasCraftOpenBeforeInfoPanel = false;
    public void ToggleMaterialsBag()
    {
        if (infoPanel != null)
        {
            bool isInfoPanelOpen = infoPanel.activeSelf;
            bool isCraftPanelOpen = craftInventoryPanel != null && craftInventoryPanel.activeSelf;

            // InfoPanel açık VE CraftInventoryPanel açıksa - kapat
            if (isInfoPanelOpen && isCraftPanelOpen)
            {
                CloseInfoPanel();
            }
            // InfoPanel kapalı VEYA CraftInventoryPanel kapalıysa - aç
            else
            {
                if (ChatManager.Instance != null)
                {
                    wasCraftOpenBeforeInfoPanel = ChatManager.Instance.IsChatOpen();
                    if (wasCraftOpenBeforeInfoPanel)
                    {
                        ChatManager.Instance.ForceCloseChat();
                    }
                }

                infoPanel.SetActive(true);

                // ClassPanel'i kapat
                if (UIManager.Instance != null && UIManager.Instance.classPanel != null)
                {
                    UIManager.Instance.classPanel.SetActive(false);
                }

                // CraftNPCUIManager'ı materials mode ile initialize et
                if (craftNPCUIManager != null)
                {
                    craftNPCUIManager.InitializeForMaterialsMode();
                }

                // Sadece CraftInventoryPanel'i aç, diğer panelleri kapat
                ShowPanels(
                    showInventory: false,
                    showItemInfo: true,
                    showEquipment: false,
                    showStats: false,
                    showUpgrade: false,
                    showMerchant: false,
                    showPreview: false,
                    showCraftInventory: true,
                    showCraftNPC: true,
                    showEquStatPanel: true  // Materials bag için Equ_StatPanel açık
                );
            }
        }
    }
private void ReturnUpgradeSlotItemToInventory()
{
    BlacksmithNPC blacksmith = FindFirstObjectByType<BlacksmithNPC>();
    if (blacksmith != null && blacksmith.IsUpgradePanelOpen)
    {
        UISlot upgradeSlot = blacksmith.GetUpgradeSlot();
        if (upgradeSlot != null)
        {
            ItemData slotItem = upgradeSlot.GetItemInfo();
            if (slotItem != null)
            {
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                foreach (GameObject player in players)
                {
                    NetworkObject networkObj = player.GetComponent<NetworkObject>();
                    if (networkObj != null && networkObj.HasInputAuthority)
                    {
                        InventorySystem inventory = player.GetComponent<InventorySystem>();
                        if (inventory != null)
                        {
                            inventory.TryAddItem(slotItem);
                            upgradeSlot.ClearSlot();
                            blacksmith.UpdateUpgradeInfo(null);
                        }
                        break;
                    }
                }
            }
        }
    }
}
// ToggleInfoPanel metoduna ekle
public void ToggleInfoPanel()
{
    if (infoPanel != null)
    {
        bool newState = !infoPanel.activeSelf;
        
        if (newState) // Panel açılıyorsa
        {
            if (ChatManager.Instance != null)
            {
                wasChatOpenBeforeInfoPanel = ChatManager.Instance.IsChatOpen();
                if (wasChatOpenBeforeInfoPanel)
                {
                    ChatManager.Instance.ForceCloseChat();
                }
            }
            
            infoPanel.SetActive(true);
            
            MerchantNPC merchantNPC = FindFirstObjectByType<MerchantNPC>();
            if (merchantNPC != null)
            {
                merchantNPC.CloseMerchantPanel();
            }
            
            if (UIManager.Instance != null && UIManager.Instance.classPanel != null)
            {
                UIManager.Instance.classPanel.SetActive(false);
            }
            
            // YENI: Snapshot'ı refresh et
            RefreshCharacterSnapshot();
            
            ShowPanels(
                showInventory: true, 
                showItemInfo: true, 
                showEquipment: true, 
                showStats: true,
                showUpgrade: false, 
                showMerchant: false,
                showCraftInventory: false,
                showEquStatPanel: true
            );
        }
        else
        {
            infoPanel.SetActive(false);
            
            if (wasChatOpenBeforeInfoPanel && ChatManager.Instance != null)
            {
                ChatManager.Instance.ForceOpenChat();
            }
            wasChatOpenBeforeInfoPanel = false;
        }
    }
}

// YENI metod ekle
private void RefreshCharacterSnapshot()
{
    if (CharacterSnapshotSystem.Instance != null && characterPreviewHolder != null)
    {
        Texture2D snapshot = CharacterSnapshotSystem.Instance.GetCurrentSnapshot();
        if (snapshot != null)
        {
            RawImage previewImage = characterPreviewHolder.GetComponentInChildren<RawImage>();
            if (previewImage != null)
            {
                previewImage.texture = snapshot;
                
                // Alpha'yı 1 yap
                CanvasGroup canvasGroup = previewImage.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
            }
        }
        else
        {
            // Snapshot henüz alınmamışsa, al
            CharacterSnapshotSystem.Instance.RefreshSnapshot();
        }
    }
}
public void ShowBlacksmithPanels()
{
    if (infoPanel != null)
    {
        gameObject.SetActive(true);
        infoPanel.SetActive(true);
        
        // ClassPanel'i kapat
        if (UIManager.Instance != null && UIManager.Instance.classPanel != null)
        {
            UIManager.Instance.classPanel.SetActive(false);
        }
        
        // Inventory, Equ_StatPanel ve upgrade panelleri açık, StatsPanel kapalı
        ShowPanels(
            showInventory: true, 
            showItemInfo: true,  
            showEquipment: false, 
            showStats: false,           // StatsPanel kapalı
            showUpgrade: true,
            showPreview: false,
            showMerchant: false,
            showEquStatPanel: true      // Equ_StatPanel açık
        );
    }
}
public void CloseAllPanels()
{
    // Önce tüm panelleri gösterme
    ShowPanels(false, false, false, false, false, false, false); 
    
    // ItemInfoPanel'i açıkça merchant modundan çıkar
    ItemInfoPanel itemInfoPanel = GetComponentInChildren<ItemInfoPanel>(true);
    if (itemInfoPanel != null)
    {
        itemInfoPanel.SetMerchantMode(false);
        itemInfoPanel.ClosePanel();
    }
    
    // InfoPanel'i kapat
    if (infoPanel != null)
    {
        infoPanel.SetActive(false);
    }
}
    
private void Update()
{
    if (infoPanel != null && infoPanel.activeSelf)
    {
        UpdateStatsDisplay();

    }
}
private void UpdateStatsDisplay()
{
    if (playerStats == null) return;

    // Saniyede bir log (throttle)
    if (Time.time % 1f < 0.1f)
    {
        string playerName = playerStats.GetPlayerDisplayName();
    }

    if (moveSpeedText != null) moveSpeedText.text = $"Hareket Hızı: {playerStats.MoveSpeed:F0}";
    if (critChanceText != null) critChanceText.text = $"Kritik Şansı: {playerStats.FinalCriticalChance:F0}%";
    if (armorText != null) armorText.text = $"Zırh: {playerStats.BaseArmor:F0}";
    if (attackSpeedText != null) attackSpeedText.text = $"Saldırı Hızı: {playerStats.FinalAttackSpeed:F0}";
    if (attackPowerText != null) attackPowerText.text = $"Hasar: {Mathf.FloorToInt(playerStats.FinalDamage)}";
}

public void CloseInfoPanel()
{
    // Panel açıksa ItemInfoPanel'i sıfırla
    ItemInfoPanel itemInfoPanel = transform.GetComponentInChildren<ItemInfoPanel>(true);
    if (itemInfoPanel != null)
    {
        itemInfoPanel.SetMerchantMode(false);
        itemInfoPanel.ClosePanel();
    }

    // BlacksmithNPC kontrolü - upgrade paneli açıksa item'ı geri döndür
    BlacksmithNPC blacksmith = FindFirstObjectByType<BlacksmithNPC>();
    if (blacksmith != null && blacksmith.IsUpgradePanelOpen)
    {
        blacksmith.CloseUpgradePanel(); // Yeni public metodu çağır
    }

    // ClassPanel'i de kapat ve UIManager'a bildir
    if (UIManager.Instance != null && UIManager.Instance.classPanel != null)
    {
        UIManager.Instance.classPanel.SetActive(false);
    }

    // Chat önceden açıktıysa tekrar aç
    if (wasChatOpenBeforeInfoPanel && ChatManager.Instance != null)
    {
        ChatManager.Instance.ForceOpenChat();
    }
    wasChatOpenBeforeInfoPanel = false; // Reset et
    
    // Craft için chat kontrolü de reset et
    if (wasCraftOpenBeforeInfoPanel && ChatManager.Instance != null)
    {
        ChatManager.Instance.ForceOpenChat();
    }
    wasCraftOpenBeforeInfoPanel = false; // Reset et

    if (infoPanel != null)
    {
        infoPanel.SetActive(false);
    }
}
private void OnDestroy()
{
    if (classSystem != null)
    {
        classSystem.OnClassChanged -= OnPlayerClassChanged;
    }
    if (toggleButton != null)
        toggleButton.onClick.RemoveListener(ToggleInfoPanel);

    if (closeButton != null)
        closeButton.onClick.RemoveListener(CloseInfoPanel);
        
    if (Instance == this)
    {
        Instance = null;
    }
}
}