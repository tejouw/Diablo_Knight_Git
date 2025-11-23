using UnityEngine;


public class MerchantPanel : MonoBehaviour
{
        public const int MERCHANT_ROWS = 3;
    public const int MERCHANT_COLS = 6;
        [SerializeField] private UISlot[] merchantSlots = new UISlot[MERCHANT_ROWS * MERCHANT_COLS]; // 3x6 grid

    private MerchantNPC currentMerchant;
    private InventorySystem playerInventory;

    
public void Initialize(MerchantNPC merchant)
{
    currentMerchant = merchant;
    
    if (merchant != null) 
    {
        if (!merchant.IsMerchantPanelOpen)
        {
            return;
        }
    }
    RefreshSlots();
}

private void RefreshSlots()
{
    if (currentMerchant == null)
    {
        return;
    }

    var allSlots = currentMerchant.GetAllSlots();
    Debug.Log($"[MerchantPanel] Refreshing slots for merchant: {currentMerchant.name}");

    // ÖNCE TÜM SLOTLARI TEMİZLE
    for (int i = 0; i < merchantSlots.Length; i++)
    {
        if(merchantSlots[i] != null)
        {
            merchantSlots[i].ClearSlot();
        }
    }
    int displayedCount = 0;
    for (int i = 0; i < merchantSlots.Length; i++)
    {
        if(merchantSlots[i] == null)
        {
            continue;
        }

        int x = i % MERCHANT_COLS;
        int y = i / MERCHANT_COLS;
        Vector2Int position = new Vector2Int(x, y);
        
        var slot = currentMerchant.GetSlot(position);
        if (slot != null && !slot.isEmpty)
        {
            merchantSlots[i].UpdateSlot(slot.item, slot.amount);
            displayedCount++;
        }
    }
}

public void BuyItem(ItemData item)
{
    if (item == null || playerInventory == null) return;

    int buyPrice = item.buyPrice; // buyPrice kullan
    var playerStats = playerInventory.GetComponent<PlayerStats>();

    if (playerStats != null && playerStats.Coins >= buyPrice)
    {
        if (playerInventory.TryAddItem(item))
        {
            playerStats.AddCoins(-buyPrice);
        }
    }
}
    public void SellItem(ItemData item, Vector2Int inventoryPosition)
    {
        if (item == null || playerInventory == null) return;

        int sellPrice = item.sellPrice; // sellPrice kullan
        var playerStats = playerInventory.GetComponent<PlayerStats>();

        if (playerInventory.RemoveItem(inventoryPosition))
        {
            playerStats.AddCoins(sellPrice);
            currentMerchant.AddItemToMerchant(item.itemId, item.buyPrice); // buyPrice kullan
            RefreshSlots();
        }
    }

}