using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Fusion;

public class MerchantNPC : BaseNPC
{
    private Dictionary<Vector2Int, InventorySlot> merchantSlots = new Dictionary<Vector2Int, InventorySlot>();

    private const int MERCHANT_ROWS = 3;
    private const int MERCHANT_COLS = 6;
    private Dictionary<string, int> itemPrices = new Dictionary<string, int>();
    public bool IsInRange => isPlayerInRange;


        private bool isMerchantPanelOpen = false;
    public bool IsMerchantPanelOpen => isMerchantPanelOpen;

        [System.Serializable]
    public class MerchantItem
    {
        public ItemData item;
        public Vector2Int position;
    }
        [Header("Merchant Items")]
    [SerializeField] private List<MerchantItem> defaultItems = new List<MerchantItem>();

    protected override void Start()
    {
        base.Start();
        InitializeMerchantInventory();
        LoadDefaultItems();
    }

private void LoadDefaultItems()
{
    
    foreach(var merchantItem in defaultItems)
    {
        if(merchantItem.item != null)
        {
            
            float manualArmor = merchantItem.item.useManualStats ? merchantItem.item.manualArmorValue : 0f;
            float manualAttack = merchantItem.item.useManualStats ? merchantItem.item.manualAttackPower : 0f;
            
            AddItemToMerchant(merchantItem.item.itemId, merchantItem.item.buyPrice, merchantItem.position, manualArmor, manualAttack);
        }
    }
}

        public InventorySlot GetSlot(Vector2Int position)
    {
        if (merchantSlots.TryGetValue(position, out InventorySlot slot))
        {
            return slot;
        }
        return null;
    }
public bool RemoveItem(string itemId)
{
    // Sınırsız stok sistemi için artık hiçbir item kaldırılmayacak
    // Sadece item'ın mevcut olup olmadığını kontrol et
    var slot = merchantSlots.Values.FirstOrDefault(s => 
        !s.isEmpty && s.item.itemId == itemId);
    
    return slot != null; // Item varsa true döndür ama slotu temizleme
}

    private void InitializeMerchantInventory()
    {
        // Mevcut slotları temizle
        merchantSlots.Clear();

        // Yeni slotları oluştur
        for (int y = 0; y < MERCHANT_ROWS; y++)
        {
            for (int x = 0; x < MERCHANT_COLS; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                merchantSlots[pos] = new InventorySlot(pos);
            }
        }
    }
        public Dictionary<string, int> GetAllPrices()
    {
        return new Dictionary<string, int>(itemPrices);
    }

    public Dictionary<Vector2Int, InventorySlot> GetAllSlots()
    {
        return new Dictionary<Vector2Int, InventorySlot>(merchantSlots);
    }
public bool AddItemToMerchant(string itemId, int price, Vector2Int? targetPosition = null, float manualArmor = 0f, float manualAttack = 0f)
{
    var item = ItemDatabase.Instance.GetItemById(itemId);
    if (item == null) return false;

    InventorySlot targetSlot;

    if (targetPosition.HasValue)
    {
        if (!merchantSlots.TryGetValue(targetPosition.Value, out targetSlot) || !targetSlot.isEmpty)
        {
            return false;
        }
    }
    else
    {
        targetSlot = merchantSlots.Values.FirstOrDefault(slot => slot.isEmpty);
        if (targetSlot == null) return false;
    }

    // Merchant item kopyası oluştur
    ItemData merchantCopy = item.CreateExactCopy();
    merchantCopy.useManualStats = true;
    
    if (manualArmor > 0f)
    {
        merchantCopy.manualArmorValue = manualArmor;
        merchantCopy.armorValue = manualArmor;
    }
    if (manualAttack > 0f)
    {
        merchantCopy.manualAttackPower = manualAttack;
        merchantCopy.attackPower = manualAttack;
    }

    targetSlot.item = merchantCopy;
    targetSlot.amount = 1;
    itemPrices[itemId] = price;
    return true;
}

        public int GetSellPrice(ItemData item)
    {
        return item?.sellPrice ?? 0; // sellPrice kullan
    }

public bool IsItemAvailable(string itemId)
{
    // Sadece slot'ta item var mı kontrol et
    return merchantSlots.Values.Any(slot => 
        !slot.isEmpty && slot.item.itemId == itemId);
}
public void AddItemToMerchant(ItemData item)
{
    if (item != null)
    {
        AddItemToMerchant(item.itemId, item.buyPrice);
    }
}    

private void AddItemToMerchantWithPosition(string itemId, int price, Vector2Int? targetPosition = null, float manualArmor = 0f, float manualAttack = 0f)
{
    var item = ItemDatabase.Instance.GetItemById(itemId);
    if (item == null) return;

    InventorySlot targetSlot;

    if (targetPosition.HasValue)
    {
        if (!merchantSlots.TryGetValue(targetPosition.Value, out targetSlot) || !targetSlot.isEmpty)
        {
            return;
        }
    }
    else
    {
        targetSlot = merchantSlots.Values.FirstOrDefault(slot => slot.isEmpty);
        if (targetSlot == null) return;
    }

    // Merchant item kopyası oluştur
    ItemData merchantCopy = Instantiate(item);
    merchantCopy.useManualStats = true; // DEĞİŞTİ
    
    // Manuel değerler varsa set et
    if (manualArmor > 0f)
    {
        merchantCopy.manualArmorValue = manualArmor;
        merchantCopy.armorValue = manualArmor;
    }
    if (manualAttack > 0f)
    {
        merchantCopy.manualAttackPower = manualAttack;
        merchantCopy.attackPower = manualAttack;
    }
    
    targetSlot.item = merchantCopy;
    targetSlot.amount = 1;
    itemPrices[itemId] = price;
}

// Public API metodlarını DEĞİŞTİR:

public void AddItemToShop(ItemData item, float manualArmor = 0f, float manualAttack = 0f)
{
    AddItemToMerchantWithPosition(item.itemId, item.buyPrice, null, manualArmor, manualAttack);
}

public void AddItemToShop(string itemId, int price, float manualArmor = 0f, float manualAttack = 0f)
{
    AddItemToMerchantWithPosition(itemId, price, null, manualArmor, manualAttack);
}

public void AddItemToShop(string itemId, int price, Vector2Int position, float manualArmor = 0f, float manualAttack = 0f)
{
    AddItemToMerchantWithPosition(itemId, price, position, manualArmor, manualAttack);
}


    public int GetBuyPrice(ItemData item)
    {
        return item?.buyPrice ?? 0; // buyPrice kullan
    }

    public override void OpenInteractionPanel()
    {
        // Önce questgiver kontrolü yap (bu baseNPC'de yapılacak)
        base.OpenInteractionPanel();

        // Eğer quest işlenmezse Merchant panelini aç
        QuestGiver questGiver = GetComponent<QuestGiver>();
        if (questGiver == null || !questGiver.HasActiveQuest())
        {
            HandleNPCTypeInteraction();
        }
    }
public override void HandleNPCTypeInteraction()
{
    GameObject gameUI = GameObject.Find("GameUI");
    if (gameUI != null)
    {
        Transform infoPanelManagerTransform = gameUI.transform.Find("InfoPanelManager");
        if (infoPanelManagerTransform != null)
        {
            InfoPanelManager infoPanelManager = infoPanelManagerTransform.GetComponent<InfoPanelManager>();
            if (infoPanelManager != null)
            {
                isMerchantPanelOpen = true;
                infoPanelManager.ShowMerchantPanels();
                
                // MerchantPanel'i bul ve bu NPC ile initialize et
                MerchantPanel merchantPanel = FindFirstObjectByType<MerchantPanel>();
                if (merchantPanel != null)
                {
                    merchantPanel.Initialize(this);
                }
            }
        }
    }
}
    
protected override void CloseInteractionPanel()
{
    // Panel'i kapat
    isMerchantPanelOpen = false;
    
    // ItemInfoPanel'i temizle ve merchant modunu kapat
    var itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
    if (itemInfoPanel != null)
    {
        itemInfoPanel.SetMerchantMode(false);
        itemInfoPanel.ClosePanel();
    }
    
    base.CloseInteractionPanel();
}
public void CloseMerchantPanel()
{
    isMerchantPanelOpen = false;
    CloseInteractionPanel();
}
protected override void CheckPlayerDistance()
{
    bool wasInRange = isPlayerInRange;
    base.CheckPlayerDistance();

    if (wasInRange && !isPlayerInRange && isMerchantPanelOpen)
    {
        CloseInteractionPanel();
        
        // ItemInfoPanel'i temizle ve merchant modunu kapat
        var itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
        if (itemInfoPanel != null)
        {
            itemInfoPanel.SetMerchantMode(false);  
            itemInfoPanel.ShowMerchantButtons(false);
            itemInfoPanel.ClosePanel();
        }
    }
}

    public int GetItemPrice(string itemId)
    {
        return itemPrices.TryGetValue(itemId, out int price) ? price : 0;
    }
}