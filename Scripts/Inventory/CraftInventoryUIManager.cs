// Path: Assets/Game/Scripts/CraftInventoryUIManager.cs

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine.UI;
using TMPro;

public class CraftInventoryUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject craftInventoryPanel;
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private GameObject slotPrefab; // 0_Frame prefab'ı
    
    [Header("Settings")]
    [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.4f, 0.2f, 1f);
    
    // Dinamik slot yönetimi
    private Dictionary<string, UISlot> craftSlotsByItemId = new Dictionary<string, UISlot>();
    private List<GameObject> allSlotObjects = new List<GameObject>();
    
    private CraftInventorySystem playerCraftInventorySystem;

    private void Awake()
    {
        if (craftInventoryPanel == null)
        {
            craftInventoryPanel = gameObject;
        }

        // SlotsContainer referansını bul
        if (slotsContainer == null)
        {
            slotsContainer = transform.Find("SlotsContainer");
            if (slotsContainer == null)
            {
                Debug.LogError("[CraftInventoryUIManager] SlotsContainer not found!");
                return;
            }
        }

        // Prefab referansını kontrol et
        if (slotPrefab == null)
        {
            Debug.LogError("[CraftInventoryUIManager] Slot prefab not assigned!");
            return;
        }
    }

    private void Start()
    {
        StartCoroutine(FindPlayerCraftInventory());
    }

    private IEnumerator FindPlayerCraftInventory()
    {
        while (true)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject player in players)
            {
                NetworkObject no = player.GetComponent<NetworkObject>();
                if (no != null && no.HasInputAuthority)
                {
                    playerCraftInventorySystem = player.GetComponent<CraftInventorySystem>();
                    if (playerCraftInventorySystem != null)
                    {
                        SubscribeToEvents();
                        CreateSlotsFromDatabase();
                        yield break;
                    }
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void CreateSlotsFromDatabase()
    {
        if (ItemDatabase.Instance == null)
        {
            Debug.LogError("[CraftInventoryUIManager] ItemDatabase.Instance is null!");
            return;
        }

        // Önceki slotları temizle
        ClearAllSlots();

        // ItemDatabase'den tüm craft itemları al
        List<ItemData> allCraftItems = ItemDatabase.Instance.GetAllCraftItems();

        // Her craft item için slot oluştur
        for (int i = 0; i < allCraftItems.Count; i++)
        {
            ItemData craftItem = allCraftItems[i];
            if (craftItem != null && !string.IsNullOrEmpty(craftItem.itemId))
            {
                CreateSlotForItem(craftItem, i);
            }
        }

        // İlk refresh'i yap
        RefreshAllCraftSlots();
    }

    private void CreateSlotForItem(ItemData craftItem, int index)
    {
        // Prefab'dan slot oluştur
        GameObject slotObject = Instantiate(slotPrefab, slotsContainer);
        slotObject.name = $"CraftSlot_{index}_{craftItem.itemId}";
        
        // Objenin aktif olduğundan emin ol
        slotObject.SetActive(true);
        
        // Slot yapısını bul ve ayarla
        Transform frameTransform = slotObject.transform; // 0_Frame level
        
        if (frameTransform.childCount == 0)
        {
            Debug.LogError($"[CraftInventoryUIManager] No children in frameTransform!");
            return;
        }
        
        Transform backgroundTransform = frameTransform.GetChild(0); // 0 level (background)
        backgroundTransform.gameObject.SetActive(true);
        
        if (backgroundTransform.childCount == 0)
        {
            Debug.LogError($"[CraftInventoryUIManager] No children in backgroundTransform!");
            return;
        }
        
        Transform slotTransform = backgroundTransform.GetChild(0); // CraftSlot_X level
        slotTransform.gameObject.SetActive(true);
        
        // UISlot component'ini bul ve ayarla
        UISlot uiSlot = slotTransform.GetComponent<UISlot>();
        if (uiSlot == null)
        {
            Debug.LogError($"[CraftInventoryUIManager] UISlot component not found on {slotTransform.name}");
            Destroy(slotObject);
            return;
        }

        // UISlot'un itemIcon'unu kontrol et ve ayarla
        if (uiSlot.itemIcon == null)
        {
            Image iconImage = slotTransform.GetComponent<Image>();
            if (iconImage != null)
            {
                uiSlot.itemIcon = iconImage;
            }
            else
            {
                Debug.LogError($"[CraftInventoryUIManager] No Image component found for itemIcon on {slotTransform.name}");
            }
        }

        // Amount text'ini bul ve ata
        if (uiSlot.amountText == null)
        {
            TextMeshProUGUI amountText = slotTransform.GetComponentInChildren<TextMeshProUGUI>();
            if (amountText != null)
            {
                uiSlot.amountText = amountText;
            }
        }

        // UISlot ayarları
        uiSlot.slotIndex = index;
        uiSlot.isEquipmentSlot = false;
        uiSlot.isMerchantSlot = false;
        uiSlot.isUpgradeSlot = false;

        // Background image'ı UISlot'a ata (rarity için)
        Image backgroundImage = backgroundTransform.GetComponent<Image>();
        if (backgroundImage != null)
        {
            uiSlot.slotBackgroundImage = backgroundImage;
        }

        // Dictionary'e ekle
        craftSlotsByItemId[craftItem.itemId] = uiSlot;
        allSlotObjects.Add(slotObject);
    }

    private void ClearAllSlots()
    {
        // Mevcut slotları temizle
        foreach (GameObject slotObj in allSlotObjects)
        {
            if (slotObj != null)
            {
                Destroy(slotObj);
            }
        }
        
        allSlotObjects.Clear();
        craftSlotsByItemId.Clear();
    }

    private void SubscribeToEvents()
    {
        if (playerCraftInventorySystem != null)
        {
            playerCraftInventorySystem.OnCraftItemAdded += HandleCraftItemAdded;
            playerCraftInventorySystem.OnCraftItemRemoved += HandleCraftItemRemoved;
            playerCraftInventorySystem.OnCraftSlotUpdated += HandleCraftSlotUpdated;
        }
    }

    private void HandleCraftItemAdded(CraftInventorySlot slot)
    {
        if (slot?.item != null)
        {
            UpdateSlotAmount(slot.item.itemId);
        }
    }

    private void HandleCraftItemRemoved(CraftInventorySlot slot)
    {
        if (slot?.item != null)
        {
            UpdateSlotAmount(slot.item.itemId);
        }
    }

    private void HandleCraftSlotUpdated(CraftInventorySlot slot)
    {
        if (slot?.item != null)
        {
            UpdateSlotAmount(slot.item.itemId);
        }
        else
        {
            // Slot boşaldıysa tüm slotları yenile
            RefreshAllCraftSlots();
        }
    }

    private void UpdateSlotAmount(string itemId)
    {
        if (craftSlotsByItemId.TryGetValue(itemId, out UISlot uiSlot))
        {
            ItemData itemData = ItemDatabase.Instance.GetItemById(itemId);
            if (itemData != null)
            {
                int playerAmount = GetPlayerCraftItemAmount(itemId);
                uiSlot.UpdateSlot(itemData, playerAmount);
                
                // Transparency ayarla
                SetSlotTransparency(uiSlot, playerAmount);
                
                if (playerAmount > 0)
                {
                    uiSlot.UpdateBackground(itemData.Rarity);
                }
            }
        }
    }

    private void RefreshAllCraftSlots()
    {
        if (playerCraftInventorySystem == null) return;

        foreach (var kvp in craftSlotsByItemId)
        {
            string itemId = kvp.Key;
            UISlot uiSlot = kvp.Value;
            
            ItemData itemData = ItemDatabase.Instance.GetItemById(itemId);
            if (itemData != null)
            {
                int playerAmount = GetPlayerCraftItemAmount(itemId);
                uiSlot.UpdateSlot(itemData, playerAmount);
                
                // Transparency ayarla
                SetSlotTransparency(uiSlot, playerAmount);
                
                if (playerAmount > 0)
                {
                    uiSlot.UpdateBackground(itemData.Rarity);
                }
            }
        }
    }

    private int GetPlayerCraftItemAmount(string itemId)
    {
        if (playerCraftInventorySystem == null) return 0;

        int totalAmount = 0;
        var allCraftSlots = playerCraftInventorySystem.GetAllCraftSlots();
        
        foreach (var slot in allCraftSlots.Values)
        {
            if (!slot.isEmpty && slot.item != null && slot.item.itemId == itemId)
            {
                totalAmount += slot.amount;
            }
        }
        
        return totalAmount;
    }

    private void SetSlotTransparency(UISlot uiSlot, int amount)
    {
        float alpha = amount > 0 ? 1.0f : 0.1f;
        
        // Item icon (CraftSlot_0'ın image'ı) transparency'sini ayarla
        if (uiSlot.itemIcon != null)
        {
            Color itemColor = uiSlot.itemIcon.color;
            itemColor.a = alpha;
            uiSlot.itemIcon.color = itemColor;
        }
        
        // Background image (0'ın image'ı) transparency'sini ayarla
        if (uiSlot.slotBackgroundImage != null)
        {
            Color bgColor = uiSlot.slotBackgroundImage.color;
            bgColor.a = alpha;
            uiSlot.slotBackgroundImage.color = bgColor;
        }
        
        // Amount text transparency'sini de ayarla
        if (uiSlot.amountText != null)
        {
            Color textColor = uiSlot.amountText.color;
            textColor.a = alpha;
            uiSlot.amountText.color = textColor;
        }
    }

    private void OnDestroy()
    {
        if (playerCraftInventorySystem != null)
        {
            playerCraftInventorySystem.OnCraftItemAdded -= HandleCraftItemAdded;
            playerCraftInventorySystem.OnCraftItemRemoved -= HandleCraftItemRemoved;
            playerCraftInventorySystem.OnCraftSlotUpdated -= HandleCraftSlotUpdated;
        }
    }
}