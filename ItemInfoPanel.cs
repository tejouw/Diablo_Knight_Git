// Path: Assets/Game/Scripts/ItemInfoPanel.cs

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System.Collections.Generic;


public class ItemInfoPanel : MonoBehaviour
{
[Header("UI References")]
[SerializeField] public GameObject infoPanel;
[SerializeField] private Image itemImage;
[SerializeField] private TextMeshProUGUI itemNameText;
[SerializeField] private TextMeshProUGUI itemDescriptionText;
[SerializeField] private TextMeshProUGUI itemStatsText;
[SerializeField] private TextMeshProUGUI armorOrAttackPowerText;
[SerializeField] private TextMeshProUGUI itemEffectiveLevelText; // YENİ

    [SerializeField] private Button deleteButton;
[Header("Materials Mode")]
    private List<CraftRecipe> materialRecipes = new List<CraftRecipe>();

    [SerializeField] private Button equipUnequipButton; // Unity Inspector'dan atanacak
    [SerializeField] private TextMeshProUGUI buttonText; // Button'ın text componenti

    private UISlot currentSlot;
    private InventorySystem inventorySystem;
    private EquipmentSystem equipmentSystem;
    [Header("Button Text References")]
[SerializeField] private TextMeshProUGUI equipButtonText;
    [SerializeField] private TextMeshProUGUI deleteButtonText;
private string originalEquipButtonText = "Kuşan";
private string originalDeleteButtonText = "Sil";

        [Header("Merchant UI")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private TextMeshProUGUI priceText;
    private bool isMerchantMode = false;

    [Header("Default Panel State")]
    [SerializeField] private bool showEmptyPanelOnStart = false; // Inspector'dan kontrol edilebilir
    [SerializeField] private Sprite defaultItemImage; // Boş durum için default icon

public void SetMerchantMode(bool isActive)
{
    isMerchantMode = isActive;
    
    // Merchant modu kapatılırken, ayrı buy/sell butonlarını gizle
    if (!isActive)
    {
        if (buyButton != null) buyButton.gameObject.SetActive(false);
        if (sellButton != null) sellButton.gameObject.SetActive(false);
        if (priceText != null) priceText.gameObject.SetActive(false);
        
        // Button text'lerini normale döndür
        RestoreOriginalButtonTexts();
    }
}

    private void Awake()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        // Button listenerları ekle
        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteButtonClicked);
        if (buyButton != null)
            buyButton.onClick.AddListener(OnBuyButtonClicked);
        if (sellButton != null)
            sellButton.onClick.AddListener(OnSellButtonClicked);
    }

[Header("Craft UI")]
[SerializeField] private Button craftButton;
[SerializeField] private GameObject ingredientsContainer;
[SerializeField] private GameObject ingredientPrefab;
private bool isCraftMode = false;
private CraftRecipe currentRecipe;


public void SetCraftMode(bool isActive)
{
    isCraftMode = isActive;
    
    if (!isActive)
    {
        if (craftButton != null) craftButton.gameObject.SetActive(false);
        ClearIngredientsDisplay();
        currentRecipe = null;
    }
}

public void ShowRecipeInfo(CraftRecipe recipe)
{
    Debug.Log($"[ItemInfoPanel] ShowRecipeInfo called! isCraftMode={isCraftMode}");

    if (recipe == null || recipe.resultItem == null)
    {
        Debug.LogError("[ItemInfoPanel] Recipe or resultItem is NULL!");
        return;
    }
    Debug.Log($"[ItemInfoPanel] Recipe: {recipe.resultItem.itemName}");

    currentRecipe = recipe;

    if (infoPanel != null)
    {
        infoPanel.SetActive(true);
    }

    UpdateItemDisplay(recipe.resultItem);

    // Craft modunda olduğumuzu göster
    if (isCraftMode)
    {
        Debug.Log("[ItemInfoPanel] isCraftMode is TRUE, calling ShowCraftInterface...");
        ShowCraftInterface(recipe);
    }
    else
    {
        Debug.LogWarning("[ItemInfoPanel] isCraftMode is FALSE! Craft button will NOT be shown.");
    }
}
public void SetMaterialsMode(bool isActive)
{
    
    if (!isActive)
    {
        // Materials mode kapatılırken temizle
        materialRecipes.Clear();
        ClearIngredientsDisplay();
    }
}

public void ShowMaterialInfo(UISlot slot)
{
    
    if (slot == null || slot.GetItemInfo() == null) 
    {
        Debug.LogError("[ItemInfoPanel] Slot or item info is null!");
        return;
    }
    
    ItemData materialItem = slot.GetItemInfo();
    
    if (!materialItem.IsCraftItem())
    {
        return;
    }

    
    if (infoPanel != null)
    {
        infoPanel.SetActive(true);
    }
    
    UpdateItemDisplay(materialItem);
    
    if (RecipeManager.Instance != null)
    {
        materialRecipes = RecipeManager.Instance.GetRecipesUsingItem(materialItem.itemId);
        
        CraftNPCUIManager craftNPCUIManager = FindFirstObjectByType<CraftNPCUIManager>();
        if (craftNPCUIManager != null)
        {
            craftNPCUIManager.ShowMaterialRecipes(materialRecipes, materialItem.itemName);
        }
        ClearIngredientsDisplay();
    }
    
    if (equipUnequipButton != null) equipUnequipButton.gameObject.SetActive(false);
    if (deleteButton != null) deleteButton.gameObject.SetActive(false);
    if (buyButton != null) buyButton.gameObject.SetActive(false);
    if (sellButton != null) sellButton.gameObject.SetActive(false);
    if (craftButton != null) craftButton.gameObject.SetActive(false);
}

public void ShowRecipeInfoOnly(CraftRecipe recipe)
{
    
    if (recipe == null || recipe.resultItem == null) return;
    
    // Recipe result item bilgilerini göster
    UpdateItemDisplay(recipe.resultItem);
    
    // Recipe detaylarını ingredients container'da göster - ama craft button olmadan
    DisplayIngredientsInfoOnly(recipe);
    
    // Craft butonu kapalı kal (sadece bilgi)
    if (craftButton != null) craftButton.gameObject.SetActive(false);
    
    // Diğer butonlar da kapalı kal
    if (equipUnequipButton != null) equipUnequipButton.gameObject.SetActive(false);
    if (deleteButton != null) deleteButton.gameObject.SetActive(false);
    if (buyButton != null) buyButton.gameObject.SetActive(false);
    if (sellButton != null) sellButton.gameObject.SetActive(false);
}

// Yeni metod - sadece bilgi gösterme için
private void DisplayIngredientsInfoOnly(CraftRecipe recipe)
{
    
    ClearIngredientsDisplay();
    
    if (ingredientsContainer == null)
    {
        Debug.LogError("[ItemInfoPanel] ingredientsContainer is NULL!");
        return;
    }
    
    if (ingredientPrefab == null)
    {
        Debug.LogError("[ItemInfoPanel] ingredientPrefab is NULL!");
        return;
    }
  
    foreach (var ingredient in recipe.ingredients)
    {
        CreateIngredientDisplayInfoOnly(ingredient);
    }
}

// Sadece bilgi gösterme için ingredient display
private void CreateIngredientDisplayInfoOnly(CraftIngredient ingredient)
{
    GameObject ingredientObj = Instantiate(ingredientPrefab, ingredientsContainer.transform);
    
    if (ingredientObj == null)
    {
        Debug.LogError("[ItemInfoPanel] Failed to instantiate ingredient prefab!");
        return;
    }
    
    // UI bileşenlerini bul ve ayarla
    Transform iconTransform = ingredientObj.transform.Find("Icon");
    Transform nameTransform = ingredientObj.transform.Find("Name");
    Transform amountTransform = ingredientObj.transform.Find("Amount");
    
    Image icon = iconTransform?.GetComponent<Image>();
    TextMeshProUGUI nameText = nameTransform?.GetComponent<TextMeshProUGUI>();
    TextMeshProUGUI amountText = amountTransform?.GetComponent<TextMeshProUGUI>();
    
    // Icon ayarla
    if (icon != null)
    {
        switch (ingredient.type)
        {
            case IngredientType.Item:
                if (ingredient.item != null) 
                {
                    icon.sprite = ingredient.item.itemIcon;
                }
                break;
            case IngredientType.Gold:
                icon.sprite = Resources.Load<Sprite>("Items/LargeGoldBag");
                break;
            case IngredientType.Potion:
                icon.sprite = Resources.Load<Sprite>("Items/HealthPotion");
                break;
        }
    }
    
    if (nameText != null)
    {
        nameText.text = ingredient.GetDisplayName();
        nameText.color = Color.white; // Sabit beyaz renk
    }
    
    // Miktar ayarla (sadece gereken miktarı göster)
    if (amountText != null)
    {
        amountText.text = ingredient.amount.ToString();
        amountText.color = Color.white; // Sabit beyaz renk
    }
}

private void ShowCraftInterface(CraftRecipe recipe)
{
    Debug.Log($"[ItemInfoPanel] ShowCraftInterface called! isCraftMode={isCraftMode}");

    if (!isCraftMode)
    {
        Debug.LogWarning("[ItemInfoPanel] isCraftMode is FALSE! Returning early.");
        return;
    }

    // Craft button'u göster
    if (craftButton != null)
    {
        craftButton.gameObject.SetActive(true);
        craftButton.onClick.RemoveAllListeners();
        craftButton.onClick.AddListener(() => OnCraftButtonClicked(recipe));

        // Button'u enable/disable et
        bool canCraft = CanCraftRecipe(recipe);
        craftButton.interactable = canCraft;
        Debug.Log($"[ItemInfoPanel] CraftButton setup complete! interactable={canCraft}, recipe={recipe?.resultItem?.itemName}");
    }
    else
    {
        Debug.LogError("[ItemInfoPanel] craftButton is NULL!");
    }
    DisplayIngredients(recipe);

    if (equipUnequipButton != null) equipUnequipButton.gameObject.SetActive(false);
    if (deleteButton != null) deleteButton.gameObject.SetActive(false);
    if (buyButton != null) buyButton.gameObject.SetActive(false);
    if (sellButton != null) sellButton.gameObject.SetActive(false);
}

private bool CanCraftRecipe(CraftRecipe recipe)
{
    GameObject localPlayer = FindLocalPlayer();
    if (localPlayer == null) return false;
    
    PlayerStats playerStats = localPlayer.GetComponent<PlayerStats>();
    CraftInventorySystem craftInventory = localPlayer.GetComponent<CraftInventorySystem>();
    InventorySystem inventory = localPlayer.GetComponent<InventorySystem>();
    
    if (playerStats == null || craftInventory == null || inventory == null) return false;
    
    if (!inventory.HasEmptySlot()) return false;
    
    return recipe.CanCraft(craftInventory, playerStats);
}

private void DisplayIngredients(CraftRecipe recipe)
{
    
    ClearIngredientsDisplay();
    
    foreach (var ingredient in recipe.ingredients)
    {
        CreateIngredientDisplay(ingredient);
    }
}

private void CreateIngredientDisplay(CraftIngredient ingredient)
{
    GameObject ingredientObj = Instantiate(ingredientPrefab, ingredientsContainer.transform);
    
    if (ingredientObj == null)
    {
        return;
    }
    
    Transform iconTransform = ingredientObj.transform.Find("Icon");
    Transform nameTransform = ingredientObj.transform.Find("Name");
    Transform amountTransform = ingredientObj.transform.Find("Amount");
    
    Image icon = iconTransform?.GetComponent<Image>();
    TextMeshProUGUI nameText = nameTransform?.GetComponent<TextMeshProUGUI>();
    TextMeshProUGUI amountText = amountTransform?.GetComponent<TextMeshProUGUI>();
    
    if (icon != null)
    {
        switch (ingredient.type)
        {
            case IngredientType.Item:
                if (ingredient.item != null) 
                {
                    icon.sprite = ingredient.item.itemIcon;
                }
                break;
            case IngredientType.Gold:
                icon.sprite = Resources.Load<Sprite>("Items/LargeGoldBag");
                break;
            case IngredientType.Potion:
                icon.sprite = Resources.Load<Sprite>("Items/HealthPotion");
                break;
        }
    }
    if (nameText != null)
    {
        nameText.text = ingredient.GetDisplayName();
        
        bool hasEnough = HasEnoughIngredient(ingredient);
        nameText.color = hasEnough ? Color.white : Color.red;
    }
    
    // Miktar ayarla
    if (amountText != null)
    {
        int currentAmount = GetCurrentIngredientAmount(ingredient);
        amountText.text = $"{currentAmount}/{ingredient.amount}";
        amountText.color = currentAmount >= ingredient.amount ? Color.green : Color.red;
    }
}

private bool HasEnoughIngredient(CraftIngredient ingredient)
{
    GameObject localPlayer = FindLocalPlayer();
    if (localPlayer == null) return false;
    
    PlayerStats playerStats = localPlayer.GetComponent<PlayerStats>();
    CraftInventorySystem craftInventory = localPlayer.GetComponent<CraftInventorySystem>();
    
    switch (ingredient.type)
    {
        case IngredientType.Item:
            return craftInventory != null && craftInventory.HasItem(ingredient.item.itemId, ingredient.amount);
        case IngredientType.Gold:
            return playerStats != null && playerStats.Coins >= ingredient.amount;
        case IngredientType.Potion:
            return playerStats != null && playerStats.PotionCount >= ingredient.amount;
        default:
            return false;
    }
}

private int GetCurrentIngredientAmount(CraftIngredient ingredient)
{
    GameObject localPlayer = FindLocalPlayer();
    if (localPlayer == null) return 0;
    
    PlayerStats playerStats = localPlayer.GetComponent<PlayerStats>();
    CraftInventorySystem craftInventory = localPlayer.GetComponent<CraftInventorySystem>();
    
    switch (ingredient.type)
    {
        case IngredientType.Item:
            if (craftInventory == null) return 0;
            int totalAmount = 0;
            var allSlots = craftInventory.GetAllCraftSlots();
            foreach (var slot in allSlots.Values)
            {
                if (!slot.isEmpty && slot.item != null && slot.item.itemId == ingredient.item.itemId)
                {
                    totalAmount += slot.amount;
                }
            }
            return totalAmount;
            
        case IngredientType.Gold:
            return playerStats != null ? playerStats.Coins : 0;
            
        case IngredientType.Potion:
            return playerStats != null ? playerStats.PotionCount : 0;
            
        default:
            return 0;
    }
}

private void ClearIngredientsDisplay()
{
    if (ingredientsContainer == null) return;
    
    foreach (Transform child in ingredientsContainer.transform)
    {
        Destroy(child.gameObject);
    }
}

private GameObject FindLocalPlayer()
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj != null && networkObj.HasInputAuthority)
        {
            return player;
        }
    }
    return null;
}

private void OnCraftButtonClicked(CraftRecipe recipe)
{
    Debug.Log($"[ItemInfoPanel] OnCraftButtonClicked called!");

    if (recipe == null)
    {
        Debug.LogError("[ItemInfoPanel] Recipe is NULL!");
        return;
    }
    Debug.Log($"[ItemInfoPanel] Recipe: {recipe.resultItem?.itemName ?? "NULL"}, resultAmount: {recipe.resultAmount}");

    GameObject localPlayer = FindLocalPlayer();
    if (localPlayer == null)
    {
        Debug.LogError("[ItemInfoPanel] LocalPlayer is NULL!");
        return;
    }
    Debug.Log($"[ItemInfoPanel] LocalPlayer found: {localPlayer.name}");

    CraftSystem craftSystem = localPlayer.GetComponent<CraftSystem>();
    if (craftSystem != null)
    {
        Debug.Log("[ItemInfoPanel] CraftSystem found, calling RequestCraft...");
        craftSystem.RequestCraft(recipe);
    }
    else
    {
        Debug.LogError("[ItemInfoPanel] CraftSystem component NOT FOUND on player!");
    }
}
    public void ClosePanel()
    {
        if (infoPanel != null)
        {
            // Panel'i temiz state'e getir
            ShowCleanInitialState();

            // Merchant mode'u sıfırla
            isMerchantMode = false;

            // Craft mode'u sıfırla
            isCraftMode = false;
            currentRecipe = null;

            // Materials mode'u sıfırla
            materialRecipes.Clear();

            // Ayrı buy/sell butonlarını sıfırla
            if (buyButton != null) buyButton.gameObject.SetActive(false);
            if (sellButton != null) sellButton.gameObject.SetActive(false);
            if (priceText != null) priceText.gameObject.SetActive(false);

            // Craft butonunu kesinlikle sıfırla
            if (craftButton != null)
            {
                craftButton.gameObject.SetActive(false);
                craftButton.onClick.RemoveAllListeners();
            }
            ClearIngredientsDisplay();

            // Button text'lerini normale döndür
            RestoreOriginalButtonTexts();

            // Equip/unequip butonunu sıfırla
            if (equipUnequipButton != null)
            {
                equipUnequipButton.gameObject.SetActive(false);
                equipUnequipButton.onClick.RemoveAllListeners();
            }

            // Delete butonunu sıfırla
            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(false);
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(OnDeleteButtonClicked);
            }

            currentSlot = null;
        }
    }

private void OnBuyButtonClicked()
{
    if (currentSlot == null || !currentSlot.isMerchantSlot) return;

    ItemData itemToBuy = currentSlot.GetItemInfo();
    if (itemToBuy == null) return;

    // Merchant'ı bul
    MerchantNPC merchant = FindFirstObjectByType<MerchantNPC>();
    if (merchant == null) return;

    // Fusion local player'ı bul
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj != null && networkObj.HasInputAuthority)
        {
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            InventorySystem inventorySystem = player.GetComponent<InventorySystem>();

            if (playerStats != null && inventorySystem != null)
            {
                // Yeterli gold var mı kontrol et
                if (playerStats.Coins >= itemToBuy.buyPrice)
                {
                    // Inventory'de yer var mı kontrol et
                    if (inventorySystem.HasEmptySlot())
                    {
                        // Gold'u düş ve itemi ekle
                        playerStats.AddCoins(-itemToBuy.buyPrice);
                        
                        // Yeni bir kopya oluştur
                        ItemData newItem = Instantiate(itemToBuy);
                        inventorySystem.TryAddItem(newItem);

                        // YENİ - Quest progress güncelle
                        if (QuestManager.Instance != null)
                        {
                            QuestManager.Instance.UpdateQuestProgress("", QuestType.BuyFromMerchant, itemToBuy.itemId);
                        }

                        ClosePanel();
                    }
                }
            }
            break;
        }
    }
}
private void OnSellButtonClicked()
{
    if (currentSlot == null || currentSlot.isMerchantSlot) return;

    ItemData itemToSell = currentSlot.GetItemInfo();
    if (itemToSell == null) return;

    // Fusion local player'ı bul
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj != null && networkObj.HasInputAuthority)
        {
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            InventorySystem inventorySystem = player.GetComponent<InventorySystem>();

            if (playerStats != null && inventorySystem != null)
            {
                // Satış işlemini gerçekleştir
                playerStats.AddCoins(itemToSell.sellPrice);
                
                // Inventory'den kaldır
                Vector2Int pos = new Vector2Int(
                    currentSlot.slotIndex % InventorySystem.INVENTORY_COLS,
                    currentSlot.slotIndex / InventorySystem.INVENTORY_COLS
                );
                inventorySystem.RemoveItem(pos);

                ClosePanel();
            }
            break;
        }
    }
}
    private void Start()
    {
        if (!showEmptyPanelOnStart)
        {
            // Panel'i tamamen gizle
            if (infoPanel != null)
            {
                infoPanel.SetActive(false);
            }
        }
        else
        {
            // Temiz bir başlangıç state'i göster
            ShowCleanInitialState();
        }

        FindSystems();
    }
private void ShowCleanInitialState()
{
    if (infoPanel != null)
    {
        infoPanel.SetActive(true);
    }

    if (itemImage != null)
    {
        if (defaultItemImage != null)
        {
            itemImage.sprite = defaultItemImage;
            itemImage.color = new Color(1f, 1f, 1f, 1f);
        }
        else
        {
            itemImage.sprite = null;
            itemImage.color = new Color(0f, 0f, 0f, 0f);
        }
    }

    if (itemNameText != null)
    {
        itemNameText.text = "";
    }

    if (itemDescriptionText != null)
    {
        itemDescriptionText.text = "";
    }

    if (itemStatsText != null)
    {
        itemStatsText.text = "";
    }

    if (armorOrAttackPowerText != null)
    {
        armorOrAttackPowerText.gameObject.SetActive(false);
    }
    
    // YENİ - effectiveLevelText'i de gizle
    if (itemEffectiveLevelText != null)
    {
        itemEffectiveLevelText.gameObject.SetActive(false);
    }

    if (equipUnequipButton != null) equipUnequipButton.gameObject.SetActive(false);
    if (deleteButton != null) deleteButton.gameObject.SetActive(false);
    if (buyButton != null) buyButton.gameObject.SetActive(false);
    if (sellButton != null) sellButton.gameObject.SetActive(false);
    if (craftButton != null) craftButton.gameObject.SetActive(false);
    if (priceText != null) priceText.gameObject.SetActive(false);
    
    ShowMerchantButtons(false);
}

private void FindSystems()
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj != null && networkObj.HasInputAuthority)
        {
            inventorySystem = player.GetComponent<InventorySystem>();
            equipmentSystem = player.GetComponent<EquipmentSystem>();
            break;
        }
    }
}
public void ShowItemInfo(UISlot slot)
{
    if (slot == null) return;

    if (inventorySystem == null || equipmentSystem == null)
    {
        FindSystems();
    }

    ItemData itemData = slot.GetItemInfo();
    if (itemData == null) return;

    currentSlot = slot;

    if (infoPanel != null)
    {
        infoPanel.SetActive(true);
    }

    UpdateItemDisplay(itemData);

    // Quest item kontrolü
    if (itemData.IsQuestItem())
    {
        ShowQuestItemInfo(slot, itemData);
        return;
    }

    // Collectible kontrolü
    if (itemData.IsCollectible())
    {
        ShowCollectibleInfo(slot, itemData);
        return;
    }

    // Craft item kontrolü
    if (itemData.IsCraftItem())
    {
        ShowCraftItemInfo(slot, itemData);
        return;
    }

    MerchantNPC merchant = FindFirstObjectByType<MerchantNPC>();
    bool isMerchantOpen = merchant != null && merchant.IsMerchantPanelOpen && merchant.isPlayerInRange;

    BlacksmithNPC blacksmith = FindFirstObjectByType<BlacksmithNPC>();
    bool isUpgradePanelOpen = blacksmith != null && blacksmith.IsUpgradePanelOpen;

    if (isMerchantOpen)
    {
        isMerchantMode = true;
        UpdateButtonsForMerchantMode(slot);

        if (buyButton != null) buyButton.gameObject.SetActive(false);
        if (sellButton != null) sellButton.gameObject.SetActive(false);
        if (priceText != null) priceText.gameObject.SetActive(true);
    }
    else if (isUpgradePanelOpen)
    {
        isMerchantMode = false;
        UpdateButtonsForBlacksmithMode(slot, blacksmith);
        
        if (buyButton != null) buyButton.gameObject.SetActive(false);
        if (sellButton != null) sellButton.gameObject.SetActive(false);
        if (priceText != null) priceText.gameObject.SetActive(false);
    }
    else
    {
        isMerchantMode = false;
        UpdateButtonsForNormalMode(slot);

        if (buyButton != null) buyButton.gameObject.SetActive(false);
        if (sellButton != null) sellButton.gameObject.SetActive(false);
        if (priceText != null) priceText.gameObject.SetActive(false);
    }
}
private void UpdateButtonsForBlacksmithMode(UISlot slot, BlacksmithNPC blacksmith)
{
    if (slot.isUpgradeSlot)
    {
        // Upgrade slot'taki item için - sadece "Al" butonu
        if (equipUnequipButton != null) equipUnequipButton.gameObject.SetActive(false);
        
        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(true);
            if (deleteButtonText != null)
            {
                deleteButtonText.text = "Al";
            }
            
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => HandleRetrieveFromAnvilClick(slot, blacksmith));
        }
    }
    else if (!slot.isEquipmentSlot && !slot.isMerchantSlot)
    {
        // Inventory slot'taki item için - "Anvil" butonu
        if (equipUnequipButton != null)
        {
            equipUnequipButton.gameObject.SetActive(true);
            if (equipButtonText != null)
            {
                equipButtonText.text = "Örs";
            }
            
            equipUnequipButton.onClick.RemoveAllListeners();
            equipUnequipButton.onClick.AddListener(() => HandleAnvilButtonClick(slot, blacksmith));
        }
        
        if (deleteButton != null) deleteButton.gameObject.SetActive(false);
    }
    else
    {
        // Equipment slot veya diğer durumlar - butonları gizle
        if (equipUnequipButton != null) equipUnequipButton.gameObject.SetActive(false);
        if (deleteButton != null) deleteButton.gameObject.SetActive(false);
    }
    
    if (craftButton != null) craftButton.gameObject.SetActive(false);
}
private void HandleAnvilButtonClick(UISlot slot, BlacksmithNPC blacksmith)
{
    if (slot == null || slot.isUpgradeSlot) return;
    
    ItemData itemToMove = slot.GetItemInfo();
    if (itemToMove == null) return;
    
    UISlot upgradeSlot = blacksmith.GetUpgradeSlot();
    if (upgradeSlot == null || upgradeSlot.GetItemInfo() != null) return;
    
    if (inventorySystem == null || equipmentSystem == null)
    {
        FindSystems();
        if (inventorySystem == null) return;
    }
    
    ItemData itemCopy = Instantiate(itemToMove);
    itemCopy.upgradeLevel = itemToMove.upgradeLevel;
    
    if (slot.isEquipmentSlot)
    {
        if (!equipmentSystem.UnequipItem(slot.equipmentSlotType, slot.slotIndex))
        {
            return;
        }
    }
    else
    {
        Vector2Int invPos = new Vector2Int(
            slot.slotIndex % InventorySystem.INVENTORY_COLS,
            slot.slotIndex / InventorySystem.INVENTORY_COLS
        );
        if (!inventorySystem.RemoveItem(invPos))
        {
            return;
        }
    }
    
    upgradeSlot.UpdateSlot(itemCopy);
    blacksmith.UpdateUpgradeInfo(itemCopy);
    slot.ClearSlot();
    
    ShowItemInfo(upgradeSlot);
}
private void HandleRetrieveFromAnvilClick(UISlot slot, BlacksmithNPC blacksmith)
{
    if (!slot.isUpgradeSlot) return;
    
    ItemData itemToRetrieve = slot.GetItemInfo();
    if (itemToRetrieve == null) return;
    
    if (inventorySystem == null)
    {
        FindSystems();
        if (inventorySystem == null) return;
    }
    
    ItemData itemCopy = Instantiate(itemToRetrieve);
    itemCopy.upgradeLevel = itemToRetrieve.upgradeLevel;
    
    if (inventorySystem.TryAddItem(itemCopy))
    {
        slot.ClearSlot();
        blacksmith.UpdateUpgradeInfo(null);
        ClosePanel();
    }
}
private void ShowCollectibleInfo(UISlot slot, ItemData collectibleItem)
{
    MerchantNPC merchant = FindFirstObjectByType<MerchantNPC>();
    bool isMerchantOpen = merchant != null && merchant.IsMerchantPanelOpen && merchant.isPlayerInRange;
    
    if (isMerchantOpen)
    {
        isMerchantMode = true;
        UpdateButtonsForMerchantMode(slot);
        
        if (buyButton != null) buyButton.gameObject.SetActive(false);
        if (sellButton != null) sellButton.gameObject.SetActive(false);
        if (priceText != null) priceText.gameObject.SetActive(true);
    }
    else
    {
        // Normal modda sadece delete butonu
        if (equipUnequipButton != null) equipUnequipButton.gameObject.SetActive(false);
        if (deleteButton != null) deleteButton.gameObject.SetActive(true);
        if (buyButton != null) buyButton.gameObject.SetActive(false);
        if (sellButton != null) sellButton.gameObject.SetActive(false);
        if (priceText != null) priceText.gameObject.SetActive(false);
    }
    
    if (craftButton != null) craftButton.gameObject.SetActive(false);
    
    // Armor/Attack Power text'i gizle
    if (armorOrAttackPowerText != null)
    {
        armorOrAttackPowerText.gameObject.SetActive(false);
    }
    
    // Stats kısmını temizle - sadece description göster
    if (itemStatsText != null)
    {
        itemStatsText.text = "";
    }
    
    ClearIngredientsDisplay();
}
private void ShowQuestItemInfo(UISlot slot, ItemData questItem)
{
    // Quest item'lar için TÜM butonları gizle
    if (equipUnequipButton != null) equipUnequipButton.gameObject.SetActive(false);
    if (deleteButton != null) deleteButton.gameObject.SetActive(false);
    if (buyButton != null) buyButton.gameObject.SetActive(false);
    if (sellButton != null) sellButton.gameObject.SetActive(false);
    if (priceText != null) priceText.gameObject.SetActive(false);
    if (craftButton != null) craftButton.gameObject.SetActive(false);
    
    // Armor/Attack Power text'i gizle (quest itemlarda yok)
    if (armorOrAttackPowerText != null)
    {
        armorOrAttackPowerText.gameObject.SetActive(false);
    }
    
    ClearIngredientsDisplay();
    
    if (itemStatsText != null)
    {
        itemStatsText.text = "<color=#FFD700>Görev Eşyası</color>\n<color=#CCCCCC>Bu eşya görevler için kullanılır ve satılamaz veya kuşanılamaz.</color>";
    }
}
private void ShowCraftItemInfo(UISlot slot, ItemData craftItem)
{
    // Craft item'lar için tüm button'ları gizle
    if (equipUnequipButton != null) equipUnequipButton.gameObject.SetActive(false);
    if (deleteButton != null) deleteButton.gameObject.SetActive(false);
    if (buyButton != null) buyButton.gameObject.SetActive(false);
    if (sellButton != null) sellButton.gameObject.SetActive(false);
    if (priceText != null) priceText.gameObject.SetActive(false);
    if (craftButton != null) craftButton.gameObject.SetActive(false);
    
    // DEĞIŞIKLIK: Armor/Attack Power text'i gizle (craft itemlarda yok)
    if (armorOrAttackPowerText != null)
    {
        armorOrAttackPowerText.gameObject.SetActive(false);
    }
    
    // Ingredient container'ı temizle
    ClearIngredientsDisplay();
}
private void UpdateButtonsForMerchantMode(UISlot slot)
{
    bool isInMerchantPanel = slot.isMerchantSlot;
    
    if (equipUnequipButton != null)
    {
        equipUnequipButton.gameObject.SetActive(isInMerchantPanel);
        if (isInMerchantPanel && equipButtonText != null)
        {
            equipButtonText.text = "Satın Al";
        }
        
        equipUnequipButton.onClick.RemoveAllListeners();
        if (isInMerchantPanel)
        {
            equipUnequipButton.onClick.AddListener(() => HandleMerchantBuy());
        }
    }
    
    if (deleteButton != null)
    {
        deleteButton.gameObject.SetActive(!isInMerchantPanel);
        if (!isInMerchantPanel && deleteButtonText != null)
        {
            deleteButtonText.text = "Sat";
        }
        
        deleteButton.onClick.RemoveAllListeners();
        if (!isInMerchantPanel)
        {
            deleteButton.onClick.AddListener(() => HandleMerchantSell());
        }
    }
    
    if (priceText != null)
    {
        ItemData itemData = slot.GetItemInfo();
        if (itemData != null)
        {
            priceText.text = isInMerchantPanel ?
                $"Alış Fiyatı: {itemData.buyPrice} Altın" :
                $"Satış Fiyatı: {itemData.sellPrice} Altın";
        }
    }
}

private void UpdateButtonsForNormalMode(UISlot slot)
{
    if (slot == null) return;
    
    ItemData itemData = slot.GetItemInfo();
    if (itemData != null && itemData.IsCraftItem())
    {
        
        if (equipUnequipButton != null) equipUnequipButton.gameObject.SetActive(false);
        if (deleteButton != null) deleteButton.gameObject.SetActive(false);
        if (craftButton != null) craftButton.gameObject.SetActive(false);
        return;
    }
    
    if (equipUnequipButton != null)
    {
        bool showEquipButton = !slot.isMerchantSlot;
        equipUnequipButton.gameObject.SetActive(showEquipButton);
        
        if (showEquipButton)
        {
            // Text'i normale döndür
            if (equipButtonText != null)
            {
                if (slot.isEquipmentSlot)
                {
                    equipButtonText.text = "Çıkar";
                }
                else
                {
                    equipButtonText.text = "Kuşan";
                }
            }
            
            // Click event'ini normale döndür
            equipUnequipButton.onClick.RemoveAllListeners();
            if (slot.isEquipmentSlot)
            {
                equipUnequipButton.onClick.AddListener(() => HandleUnequipButtonClick(null));
            }
            else
            {
                equipUnequipButton.onClick.AddListener(() => HandleEquipButtonClick(null));
            }
        }
    }

    if (deleteButton != null)
    {
        bool showDeleteButton = !slot.isMerchantSlot;
        deleteButton.gameObject.SetActive(showDeleteButton);
        
        if (showDeleteButton)
        {
            if (deleteButtonText != null)
            {
                deleteButtonText.text = originalDeleteButtonText;
            }
            
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteButtonClicked);
        }
    }
    
    if (craftButton != null) craftButton.gameObject.SetActive(false);
}

private void RestoreOriginalButtonTexts()
{
    if (equipButtonText != null)
    {
        equipButtonText.text = originalEquipButtonText;
    }
    if (deleteButtonText != null)
    {
        deleteButtonText.text = originalDeleteButtonText;
    }
}

private void HandleMerchantBuy()
{
    if (currentSlot == null || !currentSlot.isMerchantSlot) return;

    ItemData itemToBuy = currentSlot.GetItemInfo();
    if (itemToBuy == null) return;

    OnBuyButtonClicked();
}

private void HandleMerchantSell()
{
    if (currentSlot == null || currentSlot.isMerchantSlot) return;

    ItemData itemToSell = currentSlot.GetItemInfo();
    if (itemToSell == null) return;

    OnSellButtonClicked();
}
private void UpdateItemDisplay(ItemData itemData)
{
    if (itemImage != null) 
        itemImage.sprite = itemData.itemIcon;

    if (itemNameText != null)
    {
        string rarityColor;
        
        if (itemData.IsQuestItem())
        {
            rarityColor = "#FFD700";
        }
        else if (itemData.IsCollectible())
        {
            rarityColor = "#99CCFF";
        }
        else
        {
            rarityColor = itemData.Rarity switch
            {
                GameItemRarity.Magic => "#0080FF",
                GameItemRarity.Rare => "#CC33CC",
                _ => "#FFFFFF"
            };
        }

        string upgradeText = itemData.upgradeLevel > 1 ? $" <color=#FFD700>+{itemData.upgradeLevel}</color>" : "";
        itemNameText.text = $"<color={rarityColor}>{itemData._baseItemName}</color>{upgradeText}";
    }

    if (itemDescriptionText != null) 
        itemDescriptionText.text = $"<color=#A0A0A0>{itemData.description}</color>";
    
    if (armorOrAttackPowerText != null)
    {
        if (itemData.IsQuestItem() || itemData.IsCollectible())
        {
            armorOrAttackPowerText.gameObject.SetActive(false);
        }
        else if (itemData.IsArmorItem())
        {
            armorOrAttackPowerText.gameObject.SetActive(true);
            armorOrAttackPowerText.text = $"<color=#99CCFF>Zırh: {itemData.DisplayArmorValue:F1}</color>";
        }
        else if (itemData.IsWeaponItem())
        {
            armorOrAttackPowerText.gameObject.SetActive(true);
            armorOrAttackPowerText.text = $"<color=#FF9966>Saldırı Gücü: {itemData.DisplayAttackPower:F1}</color>";
        }
        else
        {
            armorOrAttackPowerText.gameObject.SetActive(false);
        }
    }
    
    // YENİ - Effective Level Text
    if (itemEffectiveLevelText != null)
    {
        if (itemData.IsEquippableItem())
        {
            itemEffectiveLevelText.text = $"<color=#FFCC66>Etkin Seviye: {itemData.effectiveLevel}</color>";
            itemEffectiveLevelText.gameObject.SetActive(true);
        }
        else
        {
            itemEffectiveLevelText.gameObject.SetActive(false);
        }
    }

    if (itemStatsText != null)
    {
        if (itemData.IsQuestItem())
        {
            ShowQuestItemInfo(currentSlot, itemData);
        }
        else if (itemData.IsCollectible())
        {
            ShowCollectibleInfo(currentSlot, itemData);
        }
        else if (itemData.IsCraftItem())
        {
            ShowCraftItemDropInfo(itemData);
        }
        else
        {
            ShowNormalItemStats(itemData);
        }
    }
}

private void ShowCraftItemDropInfo(ItemData craftItem)
{
    string dropInfoText = "";
    
    if (CraftDropManager.Instance != null && CraftDropManager.Instance.HasDropInfo(craftItem.itemId))
    {
        var dropInfoList = CraftDropManager.Instance.GetDropInfoForCraftItem(craftItem.itemId);
        
        dropInfoText = "<color=#FFD700>Düşüren:</color>\n"; // Altın renk başlık
        
        foreach (var dropInfo in dropInfoList)
        {
            // Monster adı ve drop şansı
            dropInfoText += $"<color=#FF9966>{dropInfo.monsterName}</color> <color=#99FF99>({dropInfo.dropChance:F0}%)</color>\n";
            
            // Lokasyon bilgisi - spawn area type'a göre
            dropInfoText += "<color=#99CCFF>Konumlar:</color> ";
            
            switch (dropInfo.spawnAreaType)
            {
                case SpawnAreaType.AreaBased:
                    // Area based için area isimlerini göster
                    if (dropInfo.areaNames != null && dropInfo.areaNames.Count > 0)
                    {
                        for (int i = 0; i < dropInfo.areaNames.Count; i++)
                        {
                            dropInfoText += $"<color=#CCCCCC>{dropInfo.areaNames[i]}</color>";
                            
                            if (i < dropInfo.areaNames.Count - 1)
                            {
                                dropInfoText += ", ";
                            }
                        }
                    }
                    else
                    {
                        // Fallback olarak koordinatları göster
                        for (int i = 0; i < dropInfo.spawnLocations.Count; i++)
                        {
                            var location = dropInfo.spawnLocations[i];
                            dropInfoText += $"<color=#CCCCCC>({location.x:F1}, {location.y:F1})</color>";
                            
                            if (i < dropInfo.spawnLocations.Count - 1)
                            {
                                dropInfoText += ", ";
                            }
                        }
                    }
                    break;
                    
                case SpawnAreaType.PointRadius:
                default:
                    // Point radius için koordinatları göster
                    for (int i = 0; i < dropInfo.spawnLocations.Count; i++)
                    {
                        var location = dropInfo.spawnLocations[i];
                        dropInfoText += $"<color=#CCCCCC>({location.x:F1}, {location.y:F1})</color>";
                        
                        if (i < dropInfo.spawnLocations.Count - 1)
                        {
                            dropInfoText += ", ";
                        }
                    }
                    break;
            }
            
            dropInfoText += "\n\n";
        }
    }
    else
    {
        dropInfoText = "<color=#FF6B6B>Düşme bilgisi mevcut değil</color>";
    }
    
    itemStatsText.text = dropInfoText;
}

private void ShowNormalItemStats(ItemData itemData)
{
    string statsText = "";
    foreach (var stat in itemData.stats)
    {
        string statColor = GetStatColor(stat.type);
        string statName = GetStatNameTurkish(stat.type);
        string valueText = FormatStatValue(stat.type, stat.value);
        statsText += $"<color={statColor}>{statName}: {valueText}</color>\n";
    }

    // Effective Level KALDIRILDI - artık ayrı component'te

    if (itemData.requiredLevel > 1)
    {
        statsText += $"\n<color=#FF6B6B>Gerekli Seviye: {itemData.requiredLevel}</color>";
    }

    itemStatsText.text = statsText;
}
private string GetStatColor(StatType type)
{
    return type switch
    {
        StatType.Health => "#FF6B6B",         // Kırmızımsı
        StatType.PhysicalDamage => "#FF9966", // Turuncu
        StatType.Armor => "#99CCFF",          // Açık mavi
        StatType.AttackSpeed => "#FFCC66",    // Sarımsı
        StatType.CriticalChance => "#FF9966", // Turuncu
        StatType.CriticalMultiplier => "#FF9966", // Turuncu
        StatType.Range => "#99FF99",          // Açık yeşil
        StatType.MoveSpeed => "#99FF99",      // Açık yeşil
        StatType.HealthRegen => "#FF9999",    // Açık kırmızı
        _ => "#FFFFFF"                        // Beyaz
    };
}

private string GetStatNameTurkish(StatType type)
{
    return type switch
    {
        StatType.Health => "Can",
        StatType.PhysicalDamage => "Fiziksel Hasar",
        StatType.Armor => "Zırh",
        StatType.AttackSpeed => "Saldırı Hızı",
        StatType.CriticalChance => "Kritik Şansı",
        StatType.CriticalMultiplier => "Kritik Çarpanı",
        StatType.Range => "Menzil",
        StatType.MoveSpeed => "Hareket Hızı",
        StatType.HealthRegen => "Can Yenilenmesi",
        StatType.ProjectileSpeed => "Mermi Hızı",
        StatType.LifeSteal => "Can Çalma",
        StatType.ArmorPenetration => "Zırh Delme",
        StatType.Evasion => "Kaçınma",
        StatType.GoldFind => "Altın Bulma",
        StatType.ItemRarity => "Eşya Nadir Bulma",
        StatType.DamageVsElites => "Elit Hasarı",
        _ => type.ToString()
    };
}

private string FormatStatValue(StatType type, float value)
{
    bool showAsPercentage = type is StatType.CriticalChance 
                                   or StatType.CriticalMultiplier 
                                   or StatType.AttackSpeed;

    return showAsPercentage 
        ? $"+{value:F1}%" 
        : $"+{value:F1}";
}

public void ShowMerchantButtons(bool show)
{
    MerchantNPC merchant = FindFirstObjectByType<MerchantNPC>();
    if (merchant == null || !merchant.IsMerchantPanelOpen || !merchant.isPlayerInRange)
    {
        show = false; // Merchant panel kapalıysa veya oyuncu etkileşim alanında değilse butonları gösterme
    }
    
    isMerchantMode = show;
    
    if (buyButton != null) 
    {
        bool shouldShow = show && (currentSlot != null && currentSlot.isMerchantSlot);
        buyButton.gameObject.SetActive(shouldShow);
    }
    if (sellButton != null)
    {
        bool shouldShow = show && (currentSlot != null && !currentSlot.isMerchantSlot);
        sellButton.gameObject.SetActive(shouldShow);
    }
    if (priceText != null)
    {
        priceText.gameObject.SetActive(show);
    }
}

private void HandleEquipButtonClick(UISlot uiSlot) 
{
    if (inventorySystem == null || equipmentSystem == null)
    {
        FindSystems();
        if (inventorySystem == null || equipmentSystem == null) return;
    }
    
    UISlot targetSlot = currentSlot;
    if (targetSlot == null) return;
    
    ItemData itemData = targetSlot.GetItemInfo();
    if (itemData == null) return;

    Vector2Int invPos = new Vector2Int(
        targetSlot.slotIndex % InventorySystem.INVENTORY_COLS,
        targetSlot.slotIndex / InventorySystem.INVENTORY_COLS
    );

    var inventorySlot = inventorySystem.GetSlot(invPos);
    if (inventorySlot == null || inventorySlot.isEmpty) return;

    if (equipmentSystem.EquipItem(inventorySlot.item))
    {
        inventorySystem.RemoveItem(invPos);
        ClosePanel();
    }
}

private void HandleUnequipButtonClick(UISlot slot)
{
    if (inventorySystem == null || equipmentSystem == null)
    {
        FindSystems();
        if (inventorySystem == null || equipmentSystem == null) return;
    }
    UISlot targetSlot = currentSlot;
    if (targetSlot == null) return;
    
    ItemData itemData = targetSlot.GetItemInfo();
    if (itemData == null) return;

    if (inventorySystem.HasEmptySlot())
    {
        if (inventorySystem.TryAddItem(itemData))
        {
            equipmentSystem.UnequipItem(targetSlot.equipmentSlotType, targetSlot.slotIndex);
            ClosePanel();
        }
    }
}

private void OnDeleteButtonClicked()
{
    if (currentSlot == null) return;
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    GameObject localPlayer = null;
    
    foreach (GameObject player in players)
    {
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj != null && networkObj.HasInputAuthority)
        {
            localPlayer = player;
            break;
        }
    }
    if (localPlayer == null) return;
    InventorySystem playerInventory = localPlayer.GetComponent<InventorySystem>();
    EquipmentSystem playerEquipment = localPlayer.GetComponent<EquipmentSystem>();
    
    if (playerInventory == null) return;

    if (currentSlot.isEquipmentSlot)
    {
        playerEquipment?.UnequipItem(currentSlot.equipmentSlotType, currentSlot.slotIndex);
    }
    else
    {
        Vector2Int pos = new Vector2Int(
            currentSlot.slotIndex % InventorySystem.INVENTORY_COLS,
            currentSlot.slotIndex / InventorySystem.INVENTORY_COLS
        );
        playerInventory.RemoveItem(pos);
    }

    ClosePanel();
}

}