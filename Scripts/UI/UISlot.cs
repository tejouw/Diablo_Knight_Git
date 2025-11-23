// Path: Assets/Game/Scripts/UISlot.cs

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Fusion;

public class UISlot : NetworkBehaviour, IPointerClickHandler
{
    public Image itemIcon;
    public TextMeshProUGUI amountText;
    public bool isEquipmentSlot;
    public EquipmentSlotType equipmentSlotType;
    public int slotIndex;
    public bool isMerchantSlot;
    
    private float lastClickTime;
    private const float doubleClickTime = 0.3f;
    private ItemData currentItem;
    public InventorySystem inventorySystem;
    public EquipmentSystem equipmentSystem;
    public NetworkObject playerNetworkObject;
    private ItemInfoPanel itemInfoPanel;
    public bool isUpgradeSlot; // Yeni özellik

    private Sprite normalBackground;
private Sprite magicBackground;
private Sprite rareBackground;


    [Header("Rarity Backgrounds")]
[SerializeField] public Image slotBackgroundImage; // Slot'un arka plan image'i
private bool isBackgroundInitialized = false;

        [Header("Default Images")]
    [SerializeField] private Sprite defaultImage; // Inventory slotları için genel default image
    [SerializeField] private bool useCustomDefaultImage = false; // Equipment slotlar için custom image kullanılıp kullanılmayacağı

private void InitializeBackground()
{
    if (isBackgroundInitialized) return;


    // Background sprite'larını yükle ve null kontrolü yap
    normalBackground = Resources.Load<Sprite>("ItemBackgrounds/Grey");
    magicBackground = Resources.Load<Sprite>("ItemBackgrounds/Blue");
    rareBackground = Resources.Load<Sprite>("ItemBackgrounds/Purple");


    // Background kontrolü ve oluşturma
    if (slotBackgroundImage == null)
    {
        var bgObject = new GameObject("BackgroundImage");
        bgObject.transform.SetParent(transform);
        slotBackgroundImage = bgObject.AddComponent<Image>();
        
        var rectTransform = slotBackgroundImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.SetAsFirstSibling();
        
        slotBackgroundImage.raycastTarget = false;
        slotBackgroundImage.enabled = false;
    }

    isBackgroundInitialized = true;
}

private void Start()
{
    // İlk olarak null kontrolü yapalım
    if(itemIcon == null)
    {
        itemIcon = GetComponent<Image>();
    }

    if(slotBackgroundImage != null && itemIcon != null)
    {
        slotBackgroundImage.transform.SetSiblingIndex(0);
        itemIcon.transform.SetSiblingIndex(1);
    }

    if (isEquipmentSlot)
    {
        // Equipment slot işlemleri...
    }
    else if (isMerchantSlot)
    {
        // Merchant slot için index 0'dan başlasın
        slotIndex = transform.GetSiblingIndex();
    }
}
public void UpdateBackground(GameItemRarity rarity)
{

    if (!isBackgroundInitialized)
    {
        InitializeBackground();
    }

    if (slotBackgroundImage != null)
    {
        slotBackgroundImage.enabled = true;
        switch (rarity)
        {
            case GameItemRarity.Magic:
                slotBackgroundImage.sprite = magicBackground;
                break;
            case GameItemRarity.Rare:
                slotBackgroundImage.sprite = rareBackground;
                break;
            case GameItemRarity.Normal:  // Explicit olarak Normal case'i ekleyelim
                slotBackgroundImage.sprite = normalBackground;
                break;
            default:
                break;
        }

        // Sprite atandıktan sonra kontrol
    }
}
    public ItemData GetItemInfo()
{
    return currentItem;
}
private void Awake()
{
    if (itemIcon == null)
    {
        itemIcon = GetComponent<Image>();
    }
    // ItemInfoPanel referansını bul
    itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();

    StartCoroutine(FindPlayerComponents());
    ClearSlot();
}

private System.Collections.IEnumerator FindPlayerComponents()
{
    while (true)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject no = player.GetComponent<NetworkObject>();
            if (no != null && no.HasInputAuthority)
            {
                playerNetworkObject = no;
                inventorySystem = player.GetComponent<InventorySystem>();
                equipmentSystem = player.GetComponent<EquipmentSystem>();
                
                if (inventorySystem != null && equipmentSystem != null)
                {
                    yield break;
                }
            }
        }
        yield return new WaitForSeconds(0.5f);
    }
}

public void OnPointerClick(PointerEventData eventData)
{
    
    // NetworkObject kontrolü
    if (playerNetworkObject == null || !playerNetworkObject.HasInputAuthority) 
    {
        Debug.LogWarning($"[UISlot] No input authority or null network object");
        return;
    }

    float timeSinceLastClick = Time.time - lastClickTime;
    
    if (timeSinceLastClick <= doubleClickTime)
    {
        OnDoubleClick();
    }
    else
    {
        if (currentItem != null)
        {
            
            // YENİ: Craft NPC ile etkileşim kontrolü ekle
            bool isCraftNPCActive = IsCraftNPCActive();
            
            // Craft inventory slot ise VE CraftNPC ile etkileşimde DEĞİLSE materials info göster
            if (IsCraftInventorySlot() && currentItem.IsCraftItem() && !isCraftNPCActive)
            {
                
                ItemInfoPanel itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
                if (itemInfoPanel != null)
                {
                    itemInfoPanel.ShowMaterialInfo(this);
                    lastClickTime = Time.time;
                    return;
                }
            }
            
            // ItemInfoPanel referansını dinamik olarak bul
            if (itemInfoPanel == null)
            {
                itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
            }
            
            if (itemInfoPanel != null)
            {
                MerchantNPC merchant = FindFirstObjectByType<MerchantNPC>();
                bool isMerchantActive = merchant != null && merchant.IsMerchantPanelOpen && merchant.isPlayerInRange;
                
                itemInfoPanel.SetMerchantMode(isMerchantActive);
                itemInfoPanel.ShowMerchantButtons(isMerchantActive);
                itemInfoPanel.ShowItemInfo(this);
            }
        }

    }
    
    lastClickTime = Time.time;
}


private Vector2Int GetInventoryPosition()
{
    // Craft inventory için farklı hesaplama
    if (GetComponent<CraftInventoryUIManager>() != null)
    {
        int y = slotIndex / CraftInventorySystem.CRAFT_INVENTORY_COLS;
        int x = slotIndex % CraftInventorySystem.CRAFT_INVENTORY_COLS;
        return new Vector2Int(x, y);
    }
    
    // Normal inventory için mevcut hesaplama
    int normalY = slotIndex / InventorySystem.INVENTORY_COLS;
    int normalX = slotIndex % InventorySystem.INVENTORY_COLS;
    return new Vector2Int(normalX, normalY);
}

public void OnDoubleClick()
{
    // Quest item kontrolü (en üstte)
    if (currentItem != null && currentItem.IsQuestItem())
    {
        return;
    }
    
    // Merchant kontrollerini en başta yap (tek sefer tanımla)
    MerchantNPC merchant = FindFirstObjectByType<MerchantNPC>();
    bool isMerchantOpen = merchant != null && merchant.IsMerchantPanelOpen;
    
    // Collectible item kontrolü
    if (currentItem != null && currentItem.IsCollectible())
    {
        if (isMerchantOpen)
        {
            if (isMerchantSlot)
            {
                HandleMerchantSlotDoubleClick();
            }
            else if (!isEquipmentSlot)
            {
                HandleInventorySlotSell();
            }
        }
        return;
    }
    
BlacksmithNPC blacksmith = FindFirstObjectByType<BlacksmithNPC>();
if (blacksmith != null)
{
    bool isPanelOpen = blacksmith.IsUpgradePanelOpen;
    Debug.Log($"[UISlot] Blacksmith bulundu. Panel açık: {isPanelOpen}");
    
    if (isPanelOpen)
    {
        HandleUpgradeSlotInteraction();
        return;
    }
}

    if (isMerchantOpen)
    {
        if (isMerchantSlot)
        {
            HandleMerchantSlotDoubleClick();
        }
        else if (!isEquipmentSlot)
        {
            HandleInventorySlotSell();
        }
        return;
    }

    bool isCraftNPCActive = IsCraftNPCActive();
    
    if (IsCraftInventorySlot() && currentItem != null && !isCraftNPCActive)
    {
        HandleCraftSlotDoubleClick();
        return;
    }
    else if (IsCraftInventorySlot() && currentItem != null && isCraftNPCActive)
    {
        ItemInfoPanel itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
        if (itemInfoPanel != null)
        {
            itemInfoPanel.ShowItemInfo(this);
        }
        return;
    }

    if (inventorySystem == null || equipmentSystem == null) 
    {
        return;
    }

    if (isEquipmentSlot && currentItem != null)
    {
        if (inventorySystem.HasEmptySlot())
        {
            if (inventorySystem.TryAddItem(currentItem))
            {
                bool unequipped = equipmentSystem.UnequipItem(equipmentSlotType, slotIndex);
                if (unequipped)
                {
                    ClearSlot();
                }
            }
        }
    }
    else if (!isEquipmentSlot && currentItem != null)
    {
        HandleNormalSlotDoubleClick();
    }
}

// YENİ metod - CraftNPC etkileşim durumunu kontrol et
private bool IsCraftNPCActive()
{
    // CraftNPC bul ve etkileşim durumunu kontrol et
    CraftNPC[] craftNPCs = FindObjectsByType<CraftNPC>(FindObjectsSortMode.None);
    
    foreach (var craftNPC in craftNPCs)
    {
        if (craftNPC != null && craftNPC.IsCraftPanelOpen && craftNPC.isPlayerInRange)
        {
            return true;
        }
    }
    
    return false;
}

// YENİ metodlar
private bool IsCraftInventorySlot()
{
    bool isCraft = GetComponentInParent<CraftInventoryUIManager>() != null;
    return isCraft;
}

private void HandleCraftSlotDoubleClick()
{
    
    if (currentItem != null)
    {
        
        ItemInfoPanel itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
        if (itemInfoPanel != null)
        {
            itemInfoPanel.ShowMaterialInfo(this);
        }
    }
}
private void HandleInventorySlotSell()
{
    if (currentItem == null) return;

    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject no = player.GetComponent<NetworkObject>();
        if (no != null && no.HasInputAuthority)
        {
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            InventorySystem inventorySystem = player.GetComponent<InventorySystem>();

            if (playerStats != null && inventorySystem != null)
            {
                // Sadece 1 tane sat
                playerStats.AddCoins(currentItem.sellPrice);
                
                Vector2Int pos = new Vector2Int(
                    slotIndex % InventorySystem.INVENTORY_COLS,
                    slotIndex / InventorySystem.INVENTORY_COLS
                );
                inventorySystem.RemoveItem(pos, 1); // 1 adet kaldır
                
                // Slot hala varsa ve item varsa UI'ı güncelle
                var slot = inventorySystem.GetSlot(pos);
                if (slot != null && !slot.isEmpty)
                {
                    UpdateSlot(slot.item, slot.amount);
                }

                var itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
                if (itemInfoPanel != null)
                {
                    // Slot boşaldıysa panel'i kapat
                    if (slot == null || slot.isEmpty)
                    {
                        itemInfoPanel.ClosePanel();
                    }
                }
            }
            break;
        }
    }
}
private void HandleMerchantSlotDoubleClick()
{
    if (currentItem == null) return;

    // Object. prefixi kaldırıldı
    MerchantNPC merchant = FindFirstObjectByType<MerchantNPC>();
    if (merchant == null) return;

    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject no = player.GetComponent<NetworkObject>();
        if (no != null && no.HasInputAuthority)
        {
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            InventorySystem inventorySystem = player.GetComponent<InventorySystem>();

            if (playerStats != null && inventorySystem != null)
            {
                if (playerStats.Coins >= currentItem.buyPrice)
                {
                    if (inventorySystem.HasEmptySlot())
                    {
                        playerStats.AddCoins(-currentItem.buyPrice);
                        
                        ItemData newItem = Instantiate(currentItem);
                        inventorySystem.TryAddItem(newItem);
                    }
                }
            }
            break;
        }
    }
}


private void HandleUpgradeSlotInteraction()
{
    // Object. prefixi kaldırıldı
    BlacksmithNPC blacksmith = FindFirstObjectByType<BlacksmithNPC>();

    if (blacksmith == null)
    {
        return;
    }

    if (isUpgradeSlot && currentItem != null)
    {
        ItemData itemToAdd = Instantiate(currentItem);
        itemToAdd.upgradeLevel = currentItem.upgradeLevel;

        if (inventorySystem.TryAddItem(itemToAdd))
        {
            ClearSlot();
            blacksmith.UpdateUpgradeInfo(null);
        }
        return;
    }

    if (!isUpgradeSlot && currentItem != null)
    {
        UISlot upgradeSlot = blacksmith.GetUpgradeSlot();
        
        if (upgradeSlot == null || upgradeSlot.currentItem != null)
        {
            return;
        }

        // Item'ın kopyasını oluştur ve upgrade seviyesini koru
        ItemData itemToMove = Instantiate(currentItem);
        itemToMove.upgradeLevel = currentItem.upgradeLevel;

        // Önce mevcut konumdan item'ı kaldır
        if (isEquipmentSlot)
        {
            if (!equipmentSystem.UnequipItem(equipmentSlotType, slotIndex))
            {
                return;
            }
        }
        else
        {
            Vector2Int invPos = GetInventoryPosition();
            if (!inventorySystem.RemoveItem(invPos))
            {
                return;
            }
        }

        upgradeSlot.UpdateSlot(itemToMove);
        blacksmith.UpdateUpgradeInfo(itemToMove);
        ClearSlot();
    }
}

    private void HandleNormalSlotDoubleClick()
    {
        Vector2Int inventoryPos = GetInventoryPosition();
        var slot = inventorySystem.GetSlot(inventoryPos);
        if (slot == null || slot.isEmpty) return;

        // Equipment slotları kontrolü ve equip işlemi
        if (equipmentSystem.EquipItem(currentItem))
        {
            inventorySystem.RemoveItem(inventoryPos);
            ClearSlot();
        }
    }

public void UpdateSlot(ItemData item, int amount = 1)
{
    if (!isBackgroundInitialized)
    {
        InitializeBackground();
    }

    currentItem = item;

    if (item != null)
    {
        // 🔒 PRODUCTION SAFETY: itemIcon component null check
        if (itemIcon == null)
        {
            Debug.LogError($"[UISlot] itemIcon component is NULL! Cannot display item: {item.itemId}");
            return;
        }

        // 🔒 PRODUCTION SAFETY: Sprite null check with fallback
        if (item.itemIcon == null)
        {
            Debug.LogError($"[UISlot] item.itemIcon is NULL for item: {item.itemId}! Using fallback sprite.");

            // Fallback: Use defaultImage if available, otherwise keep current sprite
            if (defaultImage != null)
            {
                itemIcon.sprite = defaultImage;
            }
            else
            {
                // Last resort: Create a placeholder white square
                Debug.LogWarning($"[UISlot] No fallback sprite available for item: {item.itemId}. Slot will appear empty!");
            }
        }
        else
        {
            itemIcon.sprite = item.itemIcon;
        }

        itemIcon.color = Color.white;
        UpdateBackground(item.Rarity);

        if (slotBackgroundImage != null)
        {
            slotBackgroundImage.enabled = true;
            
            switch (item.Rarity)
            {
                case GameItemRarity.Magic:
                    slotBackgroundImage.sprite = magicBackground;
                    break;
                case GameItemRarity.Rare:
                    slotBackgroundImage.sprite = rareBackground;
                    break;
                default:
                    slotBackgroundImage.sprite = normalBackground;
                    break;
            }
        }
        
        string displayName = item.GetDisplayName();
        var nameText = transform.Find("ItemName")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = displayName;
        }

if (amountText != null)
{
    if (IsCraftInventorySlot())
    {
        // Craft inventory'de amount göster - Beyaz renk
        if (amount > 0)
        {
            amountText.text = amount.ToString();
            amountText.color = Color.white;
        }
        else
        {
            amountText.text = "0";
            amountText.color = Color.white;
        }
    }
    else if (item.IsCraftItem()) // YENI: Normal inventory'de craft item kontrolü
    {
        // Craft item'lar için her zaman amount göster (1 dahil)
        if (amount > 0)
        {
            amountText.text = amount.ToString();
            amountText.color = Color.white;
        }
        else
        {
            amountText.text = "0";
            amountText.color = Color.white;
        }
    }
    else
    {
        // Normal inventory mantığı
        if (amount > 1)
        {
            // Amount göster - Beyaz renk
            amountText.text = amount.ToString();
            amountText.color = Color.white;
        }
        else if (amount == 0)
        {
            amountText.text = "0";
            amountText.color = Color.white;
        }
        else
        {
            // Upgrade level göster - Altın sarısı renk
            if (item.upgradeLevel > 1)
            {
                amountText.text = $"+{item.upgradeLevel}";
                amountText.color = new Color(1f, 0.84f, 0f); // Altın sarısı
            }
            else
            {
                amountText.text = "";
            }
        }
    }
}
    }
    else
    {
        if (slotBackgroundImage != null)
        {
            slotBackgroundImage.enabled = false;
        }

        ClearSlot();
    }
}

    public void ClearSlot()
    {
        currentItem = null;

            if (slotBackgroundImage != null)
    {
        slotBackgroundImage.enabled = false;
    }
        
        if (itemIcon != null)
        {
            // Eğer equipment slot ise ve custom default image kullanılıyorsa
            if (isEquipmentSlot && useCustomDefaultImage && defaultImage != null)
            {
                itemIcon.sprite = defaultImage;
                itemIcon.color = new Color(1f, 1f, 1f, 1f); // Yarı saydam
            }
            // Eğer normal inventory slot ise veya custom image kullanılmıyorsa
            else
            {
                itemIcon.sprite = defaultImage; // Eğer null ise otomatik boş sprite gösterecek
                itemIcon.color = new Color(0f, 0f, 0f, 0f);
            }
        }
        
        if (amountText != null)
        {
            amountText.text = "";
        }
    }
}