// Path: Assets/Game/Scripts/InventoryUIManager.cs

using UnityEngine;
using System.Collections;
using Fusion;

public class InventoryUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] public UISlot[] inventorySlots = new UISlot[24]; // UISlot array'ine çevirdik

    [Header("Settings")]
    [SerializeField] private Color emptySlotColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("🔒 Production Safety Settings")]
    [SerializeField] private bool enablePeriodicSync = true;
    [SerializeField] private float syncCheckInterval = 5f;
    [SerializeField] private bool enableDebugLogs = false;

    private InventorySystem playerInventorySystem;
    private Coroutine syncCheckerCoroutine;

    private void Awake()
    {
        if (inventoryPanel == null)
        {
            inventoryPanel = gameObject;
        }

        // Slot'ları otomatik bul ve UISlot component'i ekle
        if (inventorySlots[0] == null)
        {
            for (int i = 0; i < 24; i++)
            {
                Transform slotTransform = transform.Find($"InventorySlot_{i}");
                if (slotTransform != null)
                {
                    UISlot slot = slotTransform.GetComponent<UISlot>();
                    if (slot == null)
                    {
                        slot = slotTransform.gameObject.AddComponent<UISlot>();
                    }
                    slot.slotIndex = i;
                    slot.isEquipmentSlot = false;
                    inventorySlots[i] = slot;
                }
            }
        }
    }

    private void Start()
    {
        StartCoroutine(FindPlayerInventory());
    }

    private void OnEnable()
    {
        // 🔒 PRODUCTION SAFETY: Panel her açıldığında UI'ı refresh et
        // Bu sayede panel kapalıyken eklenen itemler açıldığında görünür
        if (playerInventorySystem != null)
        {
            if (enableDebugLogs)
                Debug.Log("[InventoryUIManager] Panel opened - refreshing all slots");

            RefreshAllSlots();
        }
    }

private IEnumerator FindPlayerInventory()
{
    while (true)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject no = player.GetComponent<NetworkObject>();
            if (no != null && no.HasInputAuthority)
            {
                playerInventorySystem = player.GetComponent<InventorySystem>();
                if (playerInventorySystem != null)
                {
                    if (enableDebugLogs)
                        Debug.Log("[InventoryUIManager] Player inventory system found, subscribing to events...");

                    SubscribeToEvents();
                    RefreshAllSlots();

                    // 🔒 PRODUCTION SAFETY: Guaranteed refresh after 1 frame to catch any missed events
                    StartCoroutine(GuaranteedRefreshAfterDelay());

                    // 🔒 PRODUCTION SAFETY: Start periodic sync checker
                    if (enablePeriodicSync)
                    {
                        syncCheckerCoroutine = StartCoroutine(PeriodicSyncChecker());
                    }

                    yield break;
                }
            }
        }
        yield return new WaitForSeconds(0.5f);
    }
}

    private void SubscribeToEvents()
    {
        if (playerInventorySystem != null)
        {
            playerInventorySystem.OnItemAdded += HandleItemAdded;
            playerInventorySystem.OnItemRemoved += HandleItemRemoved;
            playerInventorySystem.OnSlotUpdated += HandleSlotUpdated;

            if (enableDebugLogs)
                Debug.Log("[InventoryUIManager] ✅ Event subscriptions completed");
        }
    }

    // 🔒 PRODUCTION SAFETY: Catch any items added during subscription window
    private IEnumerator GuaranteedRefreshAfterDelay()
    {
        yield return null; // Wait 1 frame

        // Only refresh if panel is still active
        if (gameObject.activeInHierarchy && playerInventorySystem != null)
        {
            if (enableDebugLogs)
                Debug.Log("[InventoryUIManager] Performing guaranteed post-subscription refresh...");

            RefreshAllSlots();
        }
    }

    private void HandleItemAdded(InventorySlot slot)
    {
        if (enableDebugLogs)
            Debug.Log($"[InventoryUIManager] HandleItemAdded called for slot ({slot.position.x}, {slot.position.y}), item: {slot.item?.itemId ?? "NULL"}");

        int index = slot.position.y * InventorySystem.INVENTORY_COLS + slot.position.x;
        if (inventorySlots[index] != null)
        {
            inventorySlots[index].UpdateSlot(slot.item, slot.amount);

            // 🔒 PRODUCTION SAFETY: Fallback - verify slot was actually updated (only if panel is active)
            if (slot.item != null && gameObject.activeInHierarchy)
            {
                StartCoroutine(VerifySlotUpdateAfterDelay(index, slot));
            }
        }
        else
        {
            Debug.LogWarning($"[InventoryUIManager] inventorySlots[{index}] is NULL!");
        }
    }

    private void HandleItemRemoved(InventorySlot slot)
    {
        if (enableDebugLogs)
            Debug.Log($"[InventoryUIManager] HandleItemRemoved called for slot ({slot.position.x}, {slot.position.y})");

        int index = slot.position.y * InventorySystem.INVENTORY_COLS + slot.position.x;
        if (inventorySlots[index] != null)
        {
            inventorySlots[index].ClearSlot();
        }
    }

    private void HandleSlotUpdated(InventorySlot slot)
    {
        if (slot == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[InventoryUIManager] HandleSlotUpdated called with NULL slot - refreshing all");

            RefreshAllSlots();
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[InventoryUIManager] HandleSlotUpdated called for slot ({slot.position.x}, {slot.position.y}), isEmpty: {slot.isEmpty}");

        int index = slot.position.y * InventorySystem.INVENTORY_COLS + slot.position.x;
        if (inventorySlots[index] != null)
        {
            if (slot.isEmpty)
            {
                inventorySlots[index].ClearSlot();
            }
            else
            {
                inventorySlots[index].UpdateSlot(slot.item, slot.amount);

                // 🔒 PRODUCTION SAFETY: Fallback verification (only if panel is active)
                if (slot.item != null && gameObject.activeInHierarchy)
                {
                    StartCoroutine(VerifySlotUpdateAfterDelay(index, slot));
                }
            }
        }
        else
        {
            Debug.LogWarning($"[InventoryUIManager] inventorySlots[{index}] is NULL during HandleSlotUpdated!");
        }
    }

    // 🔒 PRODUCTION SAFETY: Verify that UI slot actually updated, retry if failed
    private IEnumerator VerifySlotUpdateAfterDelay(int slotIndex, InventorySlot dataSlot)
    {
        yield return new WaitForSeconds(0.1f);

        if (inventorySlots[slotIndex] != null)
        {
            var uiSlot = inventorySlots[slotIndex];

            // Check if UI slot's sprite matches the expected item
            if (dataSlot.item != null && uiSlot.itemIcon != null)
            {
                if (uiSlot.itemIcon.sprite == null || uiSlot.itemIcon.sprite != dataSlot.item.itemIcon)
                {
                    Debug.LogWarning($"[InventoryUIManager] ⚠️ UI slot {slotIndex} sprite mismatch detected! Forcing re-update. Expected: {dataSlot.item.itemId}");

                    // Force re-update
                    uiSlot.UpdateSlot(dataSlot.item, dataSlot.amount);
                }
            }
        }
    }

// InventoryUIManager.cs içinde RefreshAllSlots metodunu güncelliyoruz:

private void RefreshAllSlots()
{
    if (playerInventorySystem == null) return;

    // Önce tüm slotları temizle
    foreach (var slot in inventorySlots)
    {
        if (slot != null)
        {
            slot.ClearSlot();
        }
    }

    // Mevcut itemları göster
    for (int y = 0; y < InventorySystem.INVENTORY_ROWS; y++)
    {
        for (int x = 0; x < InventorySystem.INVENTORY_COLS; x++)
        {
            var slot = playerInventorySystem.GetSlot(new Vector2Int(x, y));
            if (slot != null && !slot.isEmpty)
            {
                int index = y * InventorySystem.INVENTORY_COLS + x;
                if (inventorySlots[index] != null)
                {
                    // Item'ı slot'a yerleştir
                    inventorySlots[index].UpdateSlot(slot.item, slot.amount);
                    
                    // Background'ı güncelle 
                    if (slot.item != null)
                    {
                        inventorySlots[index].UpdateBackground(slot.item.Rarity);
                    }
                }
            }
        }
    }
}

    // 🔒 PRODUCTION SAFETY: Periodically check if UI matches network state
    private IEnumerator PeriodicSyncChecker()
    {
        yield return new WaitForSeconds(syncCheckInterval); // Wait before first check

        while (true)
        {
            if (playerInventorySystem != null)
            {
                bool mismatchFound = false;

                for (int y = 0; y < InventorySystem.INVENTORY_ROWS; y++)
                {
                    for (int x = 0; x < InventorySystem.INVENTORY_COLS; x++)
                    {
                        var dataSlot = playerInventorySystem.GetSlot(new Vector2Int(x, y));
                        int index = y * InventorySystem.INVENTORY_COLS + x;

                        if (inventorySlots[index] != null && dataSlot != null)
                        {
                            // Check for mismatch
                            if (!dataSlot.isEmpty && inventorySlots[index].itemIcon != null)
                            {
                                // Data has item, but UI sprite is null or wrong
                                if (inventorySlots[index].itemIcon.sprite == null ||
                                    (dataSlot.item != null && inventorySlots[index].itemIcon.sprite != dataSlot.item.itemIcon))
                                {
                                    mismatchFound = true;
                                    Debug.LogWarning($"[InventoryUIManager] 🔄 Periodic sync detected mismatch at slot ({x},{y}). Data item: {dataSlot.item?.itemId ?? "NULL"}");
                                    break;
                                }
                            }
                            else if (dataSlot.isEmpty && inventorySlots[index].itemIcon != null && inventorySlots[index].itemIcon.sprite != null)
                            {
                                // Data is empty but UI shows something
                                mismatchFound = true;
                                Debug.LogWarning($"[InventoryUIManager] 🔄 Periodic sync detected mismatch at slot ({x},{y}). UI shows item but data is empty.");
                                break;
                            }
                        }
                    }

                    if (mismatchFound) break;
                }

                if (mismatchFound)
                {
                    Debug.LogWarning("[InventoryUIManager] 🔄 Mismatch detected - performing corrective RefreshAllSlots()");
                    RefreshAllSlots();
                }
                else if (enableDebugLogs)
                {
                    Debug.Log("[InventoryUIManager] ✅ Periodic sync check passed - UI matches data");
                }
            }

            yield return new WaitForSeconds(syncCheckInterval);
        }
    }

    private void OnDestroy()
    {
        if (playerInventorySystem != null)
        {
            playerInventorySystem.OnItemAdded -= HandleItemAdded;
            playerInventorySystem.OnItemRemoved -= HandleItemRemoved;
            playerInventorySystem.OnSlotUpdated -= HandleSlotUpdated;
        }

        // Stop sync checker
        if (syncCheckerCoroutine != null)
        {
            StopCoroutine(syncCheckerCoroutine);
        }
    }
}