// Path: Assets/Game/Scripts/CombatInitializer.cs

using UnityEngine;
using UnityEngine.UI;
using Fusion;
using System.Collections;
using UnityEngine.EventSystems; 
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class CombatInitializer : NetworkBehaviour
{
    private static CombatInitializer instance;
    public static CombatInitializer Instance => instance;

    [Header("Weapon Status UI")]
    [SerializeField] private TextMeshProUGUI weaponStatusText;
    private PlayerStats.WeaponType currentWeaponType = PlayerStats.WeaponType.Melee;

    [Header("Skill System")]
    private SkillSystem skillSystem;

    [Header("Teleport Button")]
    [SerializeField] private Button teleportButton;
    [SerializeField] private Sprite teleportLockedSprite;
    [SerializeField] private Sprite teleportUnlockedSprite;
    [SerializeField] private GameObject channellingPanel;
    [SerializeField] private UnityEngine.UI.Slider channellingSlider;
    private Image teleportButtonImage;
    private PlayerController playerController;

    private string GetTeleportUnlockQuestId()
    {
        string nickname = PlayerPrefs.GetString("PlayerNickname", "");
        if (!string.IsNullOrEmpty(nickname) && RaceManager.Instance != null)
        {
            PlayerRace race = RaceManager.Instance.GetPlayerRace(nickname);
            return race == PlayerRace.Goblin ? "teleport_unlock_quest_goblin" : "teleport_unlock_quest_human";
        }
        return "teleport_unlock_quest_human"; // Default to human
    }
    private bool isShowingChannelling = false;

[Header("Combat UI References")]
[SerializeField] private Button weaponSwitchButton;
[SerializeField] private Button autoAttackButton;
[SerializeField] private Button autoAttackButtonBackup;
[SerializeField] private Button UtilitySkillButton;
[SerializeField] private Button CombatSkillButton;
[SerializeField] private Button UltimateSkillButton;

    [Header("Bindstone Interaction")]
    [SerializeField] private Sprite bindstoneSprite;
    private BindstoneInteraction currentNearbyBindstone;

    [Header("Gatherable Interaction")]
    [SerializeField] private Sprite gatherableSprite;
    private GatherableObject currentNearbyGatherable;
    public bool isNearGatherable = false;

    [Header("Skill Selection System")]
    [SerializeField] private SkillSelectionPanel skillSelectionPanel;
    [SerializeField] private float dragThreshold = 50f;
    [SerializeField] private float holdTimeThreshold = 0.3f;
    private bool isInSkillSelectionMode = false;
    private Vector2 touchStartPosition;
    private float touchStartTime;
    private bool isDragging = false;
    private SkillSlot currentSkillSlot = SkillSlot.Skill1;

    [Header("Weapon Switch Sprites")]
    [SerializeField] private Sprite meleeWeaponSprite;
    [SerializeField] private Sprite rangedWeaponSprite;
    private Image weaponSwitchButtonImage;

[Header("Attack Button Sprites")]
[SerializeField] private Sprite meleeAttackSprite;
[SerializeField] private Sprite rangedAttackSprite;
[SerializeField] private Sprite pickupSprite;
private Image autoAttackButtonImage;
private Image autoAttackButtonBackupImage;

[Header("Button States")]
public bool isNearItems = false;
public bool isNearNPC = false;
private WeaponSystem weaponSystem;
public bool isInitialized = false;
private bool isButtonInitialized = false;
private enum ButtonMode
{
    Attack,
    Pickup,
    Gatherable,
    Bindstone,
    SingleNPC,
    DualNPC
}
private ButtonMode currentButtonMode = ButtonMode.Attack;
private bool isBackupButtonActive = false;

[Header("Item Pickup Panel")]
[SerializeField] private GameObject itemInfoPanel;
[SerializeField] private Image itemPreviewImage;
[SerializeField] private TextMeshProUGUI itemNameText;
[SerializeField] private TextMeshProUGUI itemDescriptionText;
[SerializeField] private TextMeshProUGUI itemStatsText;
[SerializeField] private TextMeshProUGUI itemArmorAttackText;
[SerializeField] private TextMeshProUGUI itemEffectiveLevelText;

    [Header("NPC Type Sprites")]
    [SerializeField] private Sprite merchantSprite;
    [SerializeField] private Sprite blacksmithSprite;
    [SerializeField] private Sprite craftSprite;

    [Header("NPC Interaction")]
    [SerializeField] private Sprite defaultNpcSprite;
    [SerializeField] private Sprite questAvailableSprite;
    [SerializeField] private Sprite questCompletedSprite;
    private BaseNPC currentNearbyNPC;
    private DialogQuestGiver currentNearbyDialogNPC;

    [Header("Initialization Settings")]
    [SerializeField] private float retryInterval = 0.2f;
    [SerializeField] private int maxRetryAttempts = 20;
    private int currentRetryAttempt = 0;
    private Coroutine initializeCoroutine;

    [Header("Item Pickup")]
    private Button pickupButton;
    public List<DroppedLoot> nearbyItems = new List<DroppedLoot>();
    private DroppedLoot nearestItem;

    [Header("Button Transition Settings")]
    private Coroutine buttonTransitionCoroutine;
    private Sprite currentTargetSprite;

private void Awake()
{
    if (instance == null)
    {
        instance = this;
    }
    if (pickupButton != null)
    {
        pickupButton.gameObject.SetActive(false);
    }
    if (itemInfoPanel != null)
    {
        itemInfoPanel.SetActive(false);
    }
    
    // Backup button'u başta deaktif yap
    if (autoAttackButtonBackup != null)
    {
        autoAttackButtonBackup.gameObject.SetActive(false);
    }
    
    // YENİ SATIRLAR
    isButtonInitialized = false;
    currentButtonMode = ButtonMode.Attack;  // Explicit set
    
    nearbyItems = new List<DroppedLoot>();
    nearestItem = null;
}

public void SetNearbyNPC(BaseNPC npc)
{

    // Zaten aynı NPC ve range içindeyse buton durumunu güncelle
    if (currentNearbyNPC == npc && isNearNPC)
    {
        UpdateAutoAttackButtonState();
        return;
    }

    // DialogQuestGiver set edilmişse ve aynı GameObject'teyse, NPC referansını güncelle ve buton durumunu güncelle
    if (currentNearbyDialogNPC != null && npc != null &&
        currentNearbyDialogNPC.gameObject == npc.gameObject)
    {
        currentNearbyNPC = npc;
        isNearNPC = true;
        UpdateAutoAttackButtonState();
        return;
    }

    currentNearbyNPC = npc;
    currentNearbyDialogNPC = null;
    isNearNPC = true;
    UpdateAutoAttackButtonState();
}

public void SetNearbyDialogNPC(DialogQuestGiver dialogNPC)
{

    // Zaten aynı DialogNPC ve range içindeyse buton durumunu güncelle
    if (currentNearbyDialogNPC == dialogNPC && isNearNPC)
    {
        UpdateAutoAttackButtonState();
        return;
    }

    // BaseNPC set edilmişse ve aynı GameObject'teyse, Dialog referansını güncelle ve buton durumunu güncelle
    if (currentNearbyNPC != null && dialogNPC != null &&
        currentNearbyNPC.gameObject == dialogNPC.gameObject)
    {
        currentNearbyDialogNPC = dialogNPC;
        currentNearbyNPC = null;
        isNearNPC = true;
        UpdateAutoAttackButtonState();
        return;
    }

    currentNearbyDialogNPC = dialogNPC;
    currentNearbyNPC = null;
    isNearNPC = true;
    UpdateAutoAttackButtonState();
}

    public void RemoveNearbyNPC(BaseNPC npc)
    {
        if (currentNearbyNPC == npc)
        {
            currentNearbyNPC = null;
            isNearNPC = false;
            UpdateAutoAttackButtonState();
        }
    }

    public void RemoveNearbyDialogNPC(DialogQuestGiver dialogNPC)
    {
        if (currentNearbyDialogNPC == dialogNPC)
        {
            currentNearbyDialogNPC = null;
            isNearNPC = false;
            UpdateAutoAttackButtonState();
        }
    }

    public void SetNearbyGatherable(GatherableObject gatherable)
    {
        if (currentNearbyGatherable == gatherable && isNearGatherable)
        {
            return;
        }

        currentNearbyGatherable = gatherable;
        isNearGatherable = true;
        UpdateAutoAttackButtonState();
    }

    public void RemoveNearbyGatherable(GatherableObject gatherable)
    {
        if (currentNearbyGatherable == gatherable)
        {
            currentNearbyGatherable = null;
            isNearGatherable = false;
            UpdateAutoAttackButtonState();
        }
    }

    public void SetNearbyItem(DroppedLoot item)
    {
        if (item == null)
        {
            nearbyItems.RemoveAll(x => x == null);
            if (nearbyItems.Count == 0)
            {
                nearestItem = null;
                isNearItems = false;
                UpdateAutoAttackButtonState();
                if (itemInfoPanel != null)
                    itemInfoPanel.SetActive(false);
            }
            else
            {
                UpdateNearestItem();
                UpdateItemInfoPanel();
            }
            return;
        }
        if (!nearbyItems.Contains(item))
        {
            nearbyItems.Add(item);
            isNearItems = true;
            UpdateNearestItem();
            UpdateAutoAttackButtonState();
            UpdateItemInfoPanel();
        }
    }

public void UpdateAutoAttackButtonState(bool forceUpdate = false)
{
    if (autoAttackButton == null)
    {
        return;
    }

    // Ana button image'ını al
    if (autoAttackButtonImage == null)
    {
        Transform mainIconTransform = autoAttackButton.transform.Find("MainIcon");
        if (mainIconTransform != null)
        {
            autoAttackButtonImage = mainIconTransform.GetComponent<Image>();
        }
        else
        {
            return;
        }
    }

    // Backup button image'ını al
    if (autoAttackButtonBackupImage == null && autoAttackButtonBackup != null)
    {
        Transform backupIconTransform = autoAttackButtonBackup.transform.Find("MainIcon");
        if (backupIconTransform != null)
        {
            autoAttackButtonBackupImage = backupIconTransform.GetComponent<Image>();
        }
    }

    // Yeni durumu belirle
    ButtonMode newMode = DetermineButtonMode();

    // İLK INITIALIZATION İSE HER ZAMAN ÇALIŞTIR
    if (!isButtonInitialized)
    {
        currentButtonMode = newMode;
        ApplyButtonMode(newMode);
        isButtonInitialized = true;
        return;
    }

    // Force update isteniyorsa veya mod değiştiyse güncelle
    if (forceUpdate || newMode != currentButtonMode || !IsBackupButtonStateCorrect(newMode))
    {
        currentButtonMode = newMode;
        ApplyButtonMode(newMode);
    }
    else
    {
    }
}

private ButtonMode DetermineButtonMode()
{
    // Öncelik 1: Item pickup
    if (isNearItems && nearestItem != null && nearestItem.HasItems())
    {
        return ButtonMode.Pickup;
    }

    // Öncelik 2: Gatherable
    if (isNearGatherable && currentNearbyGatherable != null && currentNearbyGatherable.CanBeGathered())
    {
        return ButtonMode.Gatherable;
    }

    // Öncelik 3: Bindstone
    if (currentNearbyBindstone != null)
    {
        return ButtonMode.Bindstone;
    }
    
    // Öncelik 3: NPC Etkileşimi
    if (isNearNPC && (currentNearbyNPC != null || currentNearbyDialogNPC != null))
    {
        if (CheckNPCHasMultipleInteractions())
        {
            return ButtonMode.DualNPC;
        }
        else
        {
            return ButtonMode.SingleNPC;
        }
    }
    
    // Öncelik 4: Normal Saldırı
    return ButtonMode.Attack;
}

private bool IsBackupButtonStateCorrect(ButtonMode mode)
{
    if (autoAttackButtonBackup == null) return true;
    
    bool shouldBeActive = (mode == ButtonMode.DualNPC);
    bool isActive = autoAttackButtonBackup.gameObject.activeSelf;
    
    return shouldBeActive == isActive;
}

private void ApplyButtonMode(ButtonMode mode)
{
    switch (mode)
    {
        case ButtonMode.Pickup:
            if (pickupSprite != null)
            {
                SetSingleButtonMode(pickupSprite, () => OnPickupButtonClicked());
            }
            break;

        case ButtonMode.Gatherable:
            if (gatherableSprite != null)
            {
                SetSingleButtonMode(gatherableSprite, () => OnGatherableButtonClicked());
            }
            break;

        case ButtonMode.Bindstone:
            SetSingleButtonMode(bindstoneSprite, OnBindstoneInteractionClicked);
            break;
            
        case ButtonMode.DualNPC:
            HandleMultipleInteractionButtons();
            break;
            
        case ButtonMode.SingleNPC:
            HandleSingleInteractionButton();
            break;
            
        case ButtonMode.Attack:
            // weaponSystem yerine currentWeaponType kullan
            Sprite attackSprite = currentWeaponType == PlayerStats.WeaponType.Melee
                ? meleeAttackSprite
                : rangedAttackSprite;
            SetSingleButtonMode(attackSprite, null);
            SetupAttackEvents();
            break;
    }
}
private bool CheckNPCHasMultipleInteractions()
{
    // DialogQuestGiver component'i varsa + Meslek component'i varsa = Çift button
    if (currentNearbyDialogNPC != null)
    {
        // DialogQuestGiver zaten var, şimdi meslek var mı bak
        MerchantNPC merchant = currentNearbyDialogNPC.GetComponent<MerchantNPC>();
        if (merchant != null) return true;
        
        BaseNPC baseNPC = currentNearbyDialogNPC.GetComponent<BaseNPC>();
        if (baseNPC != null)
        {
            NPCType npcType = baseNPC.NPCType;
            if (npcType == NPCType.Blacksmith || npcType == NPCType.Craft)
            {
                return true;
            }
        }
    }
    
    // Normal NPC üzerinde hem QuestGiver hem de Meslek var mı
    if (currentNearbyNPC != null)
    {
        // QuestGiver component'i var mı kontrol et (quest durumuna BAKMA)
        QuestGiver questGiver = currentNearbyNPC.GetComponent<QuestGiver>();
        DialogQuestGiver dialogQuestGiver = currentNearbyNPC.GetComponent<DialogQuestGiver>();
        
        bool hasQuestComponent = (questGiver != null || dialogQuestGiver != null);
        
        if (hasQuestComponent)
        {
            NPCType npcType = currentNearbyNPC.NPCType;
            if (npcType == NPCType.Merchant || npcType == NPCType.Blacksmith || npcType == NPCType.Craft)
            {
                return true;
            }
        }
    }
    
    return false;
}

private void HandleMultipleInteractionButtons()
{
    // Ana button: Quest sprite (smooth transition ile)
    Sprite questSprite = GetQuestSprite();
    if (questSprite != null && autoAttackButtonImage != null)
    {
        // Sadece sprite farklıysa smooth transition yap
        if (autoAttackButtonImage.sprite != questSprite)
        {
            currentTargetSprite = questSprite;
            if (buttonTransitionCoroutine != null)
                StopCoroutine(buttonTransitionCoroutine);
            buttonTransitionCoroutine = StartCoroutine(SmoothSpriteTransition(questSprite));
        }
    }
    
    ClearAllButtonEvents();
    autoAttackButton.onClick.AddListener(OnQuestInteractionClicked);
    
    // Backup button: Meslek sprite (anlık aç ve ayarla)
    if (autoAttackButtonBackup != null && !isBackupButtonActive)
    {
        Sprite professionSprite = GetProfessionSprite();
        
        if (autoAttackButtonBackupImage == null)
        {
            Transform backupIconTransform = autoAttackButtonBackup.transform.Find("MainIcon");
            if (backupIconTransform != null)
            {
                autoAttackButtonBackupImage = backupIconTransform.GetComponent<Image>();
            }
        }
        
        if (professionSprite != null && autoAttackButtonBackupImage != null)
        {
            // Backup button anlık açılır (smooth geçiş yok çünkü zaten yoktan var oluyor)
            autoAttackButtonBackup.gameObject.SetActive(true);
            autoAttackButtonBackupImage.sprite = professionSprite;
            autoAttackButtonBackupImage.color = Color.white;
            isBackupButtonActive = true;
            
            ClearBackupButtonEvents();
            autoAttackButtonBackup.onClick.AddListener(OnProfessionInteractionClicked);
        }
    }
}

private void HandleSingleInteractionButton()
{

    // Backup button'u deaktif yap
    if (autoAttackButtonBackup != null && isBackupButtonActive)
    {
        autoAttackButtonBackup.gameObject.SetActive(false);
        isBackupButtonActive = false;
    }

    // Ana button: NPC sprite
    Sprite targetSprite = GetNPCTargetSprite();

    // Sadece sprite gerçekten değiştiyse güncelle
    if (targetSprite != currentTargetSprite && autoAttackButtonImage != null)
    {
        currentTargetSprite = targetSprite;

        if (buttonTransitionCoroutine != null)
            StopCoroutine(buttonTransitionCoroutine);
        buttonTransitionCoroutine = StartCoroutine(SmoothSpriteTransition(targetSprite));

        ClearAllButtonEvents();
        autoAttackButton.onClick.AddListener(OnNPCInteractionClicked);
    }
    else if (autoAttackButton != null)
    {
        // Sprite aynı ama event farklı olabilir, sadece eventi güncelle
        ClearAllButtonEvents();
        autoAttackButton.onClick.AddListener(OnNPCInteractionClicked);
    }
}

private Sprite GetQuestSprite()
{
    // DialogQuestGiver'dan quest sprite al
    if (currentNearbyDialogNPC != null)
    {
        if (currentNearbyDialogNPC.ShouldShowCompletedSprite())
        {
            return questCompletedSprite;
        }
        else if (currentNearbyDialogNPC.ShouldShowAvailableSprite())
        {
            return questAvailableSprite;
        }
    }
    
    // Normal QuestGiver'dan quest sprite al
    if (currentNearbyNPC != null)
    {
        QuestGiver questGiver = currentNearbyNPC.GetComponent<QuestGiver>();
        if (questGiver != null && QuestManager.Instance != null)
        {
            if (QuestManager.Instance.HasAvailableQuestFromNPC(currentNearbyNPC.NPCName))
            {
                return questAvailableSprite;
            }
            else if (QuestManager.Instance.HasCompletedQuestForNPC(currentNearbyNPC.NPCName))
            {
                return questCompletedSprite;
            }
        }
    }
    
    return questAvailableSprite;
}

private Sprite GetProfessionSprite()
{
    // DialogQuestGiver üzerinden
    if (currentNearbyDialogNPC != null)
    {
        BaseNPC baseNPC = currentNearbyDialogNPC.GetComponent<BaseNPC>();
        if (baseNPC != null)
        {
            return GetSpriteByNPCType(baseNPC.NPCType);
        }
    }
    
    // Normal NPC üzerinden
    if (currentNearbyNPC != null)
    {
        return GetSpriteByNPCType(currentNearbyNPC.NPCType);
    }
    
    return defaultNpcSprite;
}

private void OnQuestInteractionClicked()
{
    // DialogQuestGiver'ı öncelikle kontrol et
    if (currentNearbyDialogNPC != null)
    {
        currentNearbyDialogNPC.HandleNPCInteraction();
        UpdateAutoAttackButtonState();
        return;
    }
    
    // Normal NPC üzerindeki quest componentlerini kontrol et
    if (currentNearbyNPC != null)
    {
        // Önce DialogQuestGiver var mı
        DialogQuestGiver dialogQuestGiver = currentNearbyNPC.GetComponent<DialogQuestGiver>();
        if (dialogQuestGiver != null)
        {
            dialogQuestGiver.HandleNPCInteraction();
            UpdateAutoAttackButtonState();
            return;
        }
        
        // Yoksa QuestGiver var mı
        QuestGiver questGiver = currentNearbyNPC.GetComponent<QuestGiver>();
        if (questGiver != null)
        {
            questGiver.HandleNPCInteraction();
            UpdateAutoAttackButtonState();
            return;
        }
    }
    
    UpdateAutoAttackButtonState();
}

private void OnProfessionInteractionClicked()
{
    // DialogQuestGiver üzerindeki BaseNPC'yi kontrol et
    if (currentNearbyDialogNPC != null)
    {
        BaseNPC baseNPC = currentNearbyDialogNPC.GetComponent<BaseNPC>();
        if (baseNPC != null)
        {
            // Meslek panelini aç (quest paneli değil)
            baseNPC.HandleNPCTypeInteraction();
            return;
        }
    }
    
    // Normal NPC'nin meslek panelini aç
    if (currentNearbyNPC != null)
    {
        currentNearbyNPC.HandleNPCTypeInteraction();
    }
}

private void SetSingleButtonMode(Sprite sprite, System.Action clickAction)
{
    // Backup button'u kapat
    if (autoAttackButtonBackup != null && isBackupButtonActive)
    {
        autoAttackButtonBackup.gameObject.SetActive(false);
        isBackupButtonActive = false;
    }
    
    // Ana button'u ayarla - sadece sprite farklıysa
    if (sprite != currentTargetSprite && autoAttackButtonImage != null)
    {
        currentTargetSprite = sprite;
        
        if (buttonTransitionCoroutine != null)
            StopCoroutine(buttonTransitionCoroutine);
        buttonTransitionCoroutine = StartCoroutine(SmoothSpriteTransition(sprite));
        
        ClearAllButtonEvents();
        if (clickAction != null)
        {
            autoAttackButton.onClick.AddListener(() => clickAction());
        }
    }
}

private void ClearBackupButtonEvents()
{
    if (autoAttackButtonBackup == null) return;
    
    autoAttackButtonBackup.onClick.RemoveAllListeners();
    
    EventTrigger trigger = autoAttackButtonBackup.gameObject.GetComponent<EventTrigger>();
    if (trigger != null)
    {
        trigger.triggers.Clear();
    }
}
private void OnBindstoneInteractionClicked()
{
    if (currentNearbyBindstone != null)
    {
        currentNearbyBindstone.StartBindChannelling();
    }
}

private void OnGatherableButtonClicked()
{
    if (currentNearbyGatherable != null && currentNearbyGatherable.CanBeGathered())
    {
        // PlayerController'a gathering başlat diyoruz
        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            PlayerController playerController = localPlayer.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.StartGatheringChannelling(currentNearbyGatherable);
            }
        }
    }
}

private Sprite GetNPCTargetSprite()
{
    if (currentNearbyBindstone != null)
    {
        return bindstoneSprite;
    }
    
    // Önce DialogQuestGiver kontrolü - öncelik sistemi ile
    if (currentNearbyDialogNPC != null)
    {
        NPCInteractionPriority priority = currentNearbyDialogNPC.GetInteractionPriority();
        
        if (priority == NPCInteractionPriority.High)
        {
            // Quest etkileşimi öncelikli
            if (currentNearbyDialogNPC.ShouldShowCompletedSprite())
            {
                return questCompletedSprite;
            }
            else if (currentNearbyDialogNPC.ShouldShowAvailableSprite())
            {
                return questAvailableSprite;
            }
            return questAvailableSprite;
        }
        else if (priority == NPCInteractionPriority.Low)
        {
            // Quest yok veya tamamlanamaz, merchant kontrolü yap
            if (currentNearbyNPC != null)
            {
                return GetSpriteByNPCType(currentNearbyNPC.NPCType);
            }
            return defaultNpcSprite;
        }
    }
    
    // DialogQuestGiver yoksa veya None priority ise, normal NPC kontrolü
    if (currentNearbyNPC != null)
    {
        QuestGiver questGiver = currentNearbyNPC.GetComponent<QuestGiver>();
        if (questGiver != null)
        {
            if (QuestManager.Instance != null)
            {
                if (QuestManager.Instance.HasAvailableQuestFromNPC(currentNearbyNPC.NPCName))
                {
                    return questAvailableSprite;
                }
                else if (QuestManager.Instance.HasCompletedQuestForNPC(currentNearbyNPC.NPCName))
                {
                    return questCompletedSprite;
                }
                else
                {
                    return GetSpriteByNPCType(currentNearbyNPC.NPCType);
                }
            }
            else
            {
                return GetSpriteByNPCType(currentNearbyNPC.NPCType);
            }
        }
        else
        {
            return GetSpriteByNPCType(currentNearbyNPC.NPCType);
        }
    }
    
    return defaultNpcSprite;
}

    private void SetupAttackEvents()
    {
        if (autoAttackButton == null || weaponSystem == null) return;
        autoAttackButton.onClick.RemoveAllListeners();
        EventTrigger trigger = autoAttackButton.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = autoAttackButton.gameObject.AddComponent<EventTrigger>();
        }
        trigger.triggers.Clear();
        EventTrigger.Entry pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener((_) => {
            if (weaponSystem != null)
                weaponSystem.OnAttackButtonDown();
        });
        trigger.triggers.Add(pointerDown);
        EventTrigger.Entry pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener((_) => {
            if (weaponSystem != null)
                weaponSystem.OnAttackButtonUp();
        });
        trigger.triggers.Add(pointerUp);
    }

private void ClearAllButtonEvents()
{
    if (autoAttackButton == null) return;
    
    if (weaponSystem != null)
    {
        weaponSystem.ForceStopAttack();
    }
    
    autoAttackButton.onClick.RemoveAllListeners();
    EventTrigger trigger = autoAttackButton.gameObject.GetComponent<EventTrigger>();
    if (trigger != null)
    {
        trigger.triggers.Clear();
    }
    
    // YENİ - Backup button'u da temizle
    ClearBackupButtonEvents();
}

    private Sprite GetSpriteByNPCType(NPCType npcType)
    {
        switch(npcType)
        {
            case NPCType.Merchant:
                return merchantSprite;
            case NPCType.Blacksmith:
                return blacksmithSprite;
            case NPCType.Craft:
                return craftSprite;
            case NPCType.DialogQuest:
                return defaultNpcSprite;
            default:
                return defaultNpcSprite;
        }
    }

private void OnNPCInteractionClicked()
{
    // Önce DialogQuestGiver kontrolü - öncelik sistemi ile
    if (currentNearbyDialogNPC != null)
    {
        NPCInteractionPriority priority = currentNearbyDialogNPC.GetInteractionPriority();
        
        if (priority == NPCInteractionPriority.High)
        {
            // Quest etkileşimi
            currentNearbyDialogNPC.HandleNPCInteraction();
            UpdateAutoAttackButtonState();
            return;
        }
        else if (priority == NPCInteractionPriority.Low)
        {
            // Quest etkileşimi yapılamaz, merchant varsa onu aç
            if (currentNearbyNPC != null)
            {
                currentNearbyNPC.OpenInteractionPanel();
                UpdateAutoAttackButtonState();
                return;
            }
        }
    }
    
    // Normal NPC etkileşimi
    if (currentNearbyNPC != null)
    {
        currentNearbyNPC.OpenInteractionPanel();
    }
    
    UpdateAutoAttackButtonState();
}

    private IEnumerator SmoothSpriteTransition(Sprite targetSprite)
    {
        if (autoAttackButtonImage == null || targetSprite == null)
        {
            yield break;
        }
        if (autoAttackButtonImage.sprite == targetSprite)
        {
            yield break;
        }
        Color originalColor = autoAttackButtonImage.color;
        originalColor.a = 1.0f;
        autoAttackButtonImage.color = originalColor;
        float fadeOutTime = 0.15f;
        float fadeInTime = 0.15f;
        float elapsedTime = 0f;
        while (elapsedTime < fadeOutTime)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1.0f, 0f, elapsedTime / fadeOutTime);
            autoAttackButtonImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }
        autoAttackButtonImage.sprite = targetSprite;
        elapsedTime = 0f;
        while (elapsedTime < fadeInTime)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1.0f, elapsedTime / fadeInTime);
            autoAttackButtonImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }
        autoAttackButtonImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1.0f);
    }

    public void UpdateNearestItem()
    {
        nearbyItems.RemoveAll(x => x == null);
        if (nearbyItems.Count == 0)
        {
            nearestItem = null;
            isNearItems = false;
            if (itemInfoPanel != null)
                itemInfoPanel.SetActive(false);
            UpdateAutoAttackButtonState();
            return;
        }
        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer == null) 
        {
            return;
        }
        float minDistance = float.MaxValue;
        DroppedLoot closestItem = null;
        foreach (var item in nearbyItems)
        {
            if (item == null) continue;
            float distance = Vector2.Distance(localPlayer.transform.position, item.transform.position);
            if (distance < minDistance && item.HasItems())
            {
                minDistance = distance;
                closestItem = item;
            }
        }
        nearestItem = closestItem;
        isNearItems = nearestItem != null;
        UpdateAutoAttackButtonState();
    }

    private GameObject FindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var player in players)
        {
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.HasInputAuthority)
                return player;
        }
        return null;
    }

    private void OnPickupButtonClicked()
    {
        foreach (var item in nearbyItems.ToList())
        {
            if (item != null && item.HasItems())
            {
                item.CollectItems();
            }
        }
        nearbyItems.Clear();
        isNearItems = false;
        UpdateAutoAttackButtonState();
    }

    public void RemoveNearbyItem(DroppedLoot item)
    {
        if (item == null) return;
        nearbyItems.RemoveAll(x => x == null);
        if (nearbyItems.Contains(item))
        {
            nearbyItems.Remove(item);
        }
        if (nearbyItems.Count == 0)
        {
            nearestItem = null;
            isNearItems = false;
            if (pickupButton != null)
                pickupButton.gameObject.SetActive(false);
            if (itemInfoPanel != null)
                itemInfoPanel.SetActive(false);
        }
        else
        {
            UpdateNearestItem();
            UpdatePickupButton();
            UpdateItemInfoPanel();
            return;
        }
        UpdateAutoAttackButtonState();
    }

public void UpdateItemInfoPanel()
{
    if (itemInfoPanel == null) return;
    if (nearestItem == null || !nearestItem.HasItems())
    {
        itemInfoPanel.SetActive(false);
        return;
    }
    ItemData itemData = nearestItem.GetDroppedItems().FirstOrDefault();
    if (itemData == null)
    {
        itemInfoPanel.SetActive(false);
        return;
    }
    
    if (itemData.IsCollectible())
    {
        itemInfoPanel.SetActive(false);
        return;
    }

    itemInfoPanel.SetActive(true);
    if (itemPreviewImage != null)
    {
        itemPreviewImage.sprite = itemData.itemIcon;
        itemPreviewImage.enabled = true;
    }
    if (itemNameText != null)
    {
        string rarityPrefix = itemData.Rarity switch
        {
            GameItemRarity.Magic => "<color=#0080FF>",
            GameItemRarity.Rare => "<color=#CC33CC>",
            _ => "<color=#FFFFFF>"
        };
        itemNameText.text = $"{rarityPrefix}{itemData.itemName}</color>";
    }
    if (itemDescriptionText != null)
    {
        itemDescriptionText.text = itemData.description;
    }
    if (itemArmorAttackText != null)
    {
        string armorAttackText = "";
        if (itemData.IsArmorItem() && itemData.armorValue > 0)
        {
            armorAttackText = $"<color=#99CCFF>Zırh: +{itemData.armorValue:F1}</color>";
        }
        else if (itemData.IsWeaponItem() && itemData.attackPower > 0)
        {
            armorAttackText = $"<color=#FF9966>Saldırı Gücü: +{itemData.attackPower:F1}</color>";
        }
        itemArmorAttackText.text = armorAttackText;
        itemArmorAttackText.gameObject.SetActive(!string.IsNullOrEmpty(armorAttackText));
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
}

    private string GetStatColor(StatType type)
    {
        return type switch
        {
            StatType.Health => "#FF6B6B",
            StatType.PhysicalDamage => "#FF9966",
            StatType.Armor => "#99CCFF",
            StatType.AttackSpeed => "#FFCC66",
            StatType.CriticalChance => "#FF9966",
            StatType.CriticalMultiplier => "#FF9966",
            StatType.Range => "#99FF99",
            StatType.MoveSpeed => "#99FF99",
            StatType.HealthRegen => "#FF9999",
            _ => "#FFFFFF"
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

private void Update()
{
    if (isInitialized && weaponSystem == null && Time.time % 1f < 0.1f)
    {
        RetryWeaponSystemConnection();
    }
    
    // Item kontrolü - SADECE durumu değişirse güncelle
    bool hasValidItems = nearbyItems.Count > 0 && nearbyItems.Any(x => x != null && x.HasItems());
    
    if (hasValidItems != isNearItems)
    {
        if (hasValidItems)
        {
            nearbyItems.RemoveAll(x => x == null);
            UpdateNearestItem();
            UpdateItemInfoPanel();
            isNearItems = true;
        }
        else
        {
            nearestItem = null;
            isNearItems = false;
            if (itemInfoPanel != null)
                itemInfoPanel.SetActive(false);
        }
        UpdateAutoAttackButtonState();
    }
    else if (hasValidItems)
    {
        // Item varsa panel güncelle ama button'u GÜNCELLEME
        nearbyItems.RemoveAll(x => x == null);
        UpdateNearestItem();
        UpdateItemInfoPanel();
    }
    
    // NPC mesafe kontrolü - SADECE saniyede bir
    if (Time.frameCount % 30 == 0) // 60 FPS'te saniyede 2 kere
    {
        if (isNearNPC && currentNearbyNPC != null)
        {
            GameObject localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                float distance = Vector2.Distance(localPlayer.transform.position, currentNearbyNPC.transform.position);
                if (distance > currentNearbyNPC.InteractionRange)
                {
                    RemoveNearbyNPC(currentNearbyNPC);
                }
            }
        }
        
        if (isNearNPC && currentNearbyDialogNPC != null)
        {
            GameObject localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                float distance = Vector2.Distance(localPlayer.transform.position, currentNearbyDialogNPC.transform.position);
                if (distance > currentNearbyDialogNPC.InteractionRange)
                {
                    RemoveNearbyDialogNPC(currentNearbyDialogNPC);
                }
            }
        }
    }
    
    if (Time.frameCount % 6 == 0)
    {
        UpdateChannellingUI();
    }
    if (Time.frameCount % 60 == 0)
    {
        CheckTeleportUnlockStatus();
    }
}

    private void UpdateChannellingUI()
    {
        if (playerController == null) return;
        bool isChannelling = playerController.IsCurrentlyChannelling();
        if (isChannelling && !isShowingChannelling)
        {
            if (channellingPanel != null)
            {
                channellingPanel.SetActive(true);
            }
            isShowingChannelling = true;
        }
        else if (!isChannelling && isShowingChannelling)
        {
            if (channellingPanel != null)
            {
                channellingPanel.SetActive(false);
            }
            isShowingChannelling = false;
        }
        if (isChannelling && channellingSlider != null)
        {
            float progress = playerController.GetChannellingProgress();
            channellingSlider.value = progress;
        }
    }

    private void RetryWeaponSystemConnection()
    {
        GameObject localPlayer = FindLocalPlayerWithTimeout();
        if (localPlayer != null)
        {
            WeaponSystem foundWeaponSystem = localPlayer.GetComponent<WeaponSystem>();
            if (foundWeaponSystem != null)
            {
                weaponSystem = foundWeaponSystem;
                InitializeCombatButtons();
            }
        }
    }

    public void UpdatePickupButton()
    {
        nearbyItems.RemoveAll(x => x == null);
        if (pickupButton != null)
        {
            bool hasValidItems = nearbyItems.Any(x => x != null && x.HasItems());
            pickupButton.gameObject.SetActive(hasValidItems);
            if (hasValidItems)
            {
                int itemCount = nearbyItems.Count(x => x != null && x.HasItems());
                var buttonText = pickupButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = itemCount > 1 ? $"Collect All ({itemCount})" : "Collect";
                }
            }
        }
    }

    private void Start()
    {
        SetButtonsActive(false);
        if (pickupButton != null)
        {
            pickupButton.gameObject.SetActive(false);
        }
        if (itemInfoPanel != null)
        {
            itemInfoPanel.SetActive(false);
        }
        if (autoAttackButton != null)
        {
            Transform mainIconTransform = autoAttackButton.transform.Find("MainIcon");
            if (mainIconTransform != null)
            {
                autoAttackButtonImage = mainIconTransform.GetComponent<Image>();
            }
        }
        if (weaponSwitchButton != null)
        {
            Transform iconTransform = weaponSwitchButton.transform.Find("Icon");
            if (iconTransform != null)
            {
                weaponSwitchButtonImage = iconTransform.GetComponent<Image>();
            }
        }
        if (teleportButton != null)
        {
            Transform iconTransform = teleportButton.transform.Find("Icon");
            if (iconTransform != null)
            {
                teleportButtonImage = iconTransform.GetComponent<Image>();
            }
            teleportButton.onClick.AddListener(OnTeleportButtonClicked);
            UpdateTeleportButtonState();
        }
        StartCoroutine(BackupInitializationCheck());
        if (channellingPanel != null)
        {
            channellingPanel.SetActive(false);
        }
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestTurnedIn += OnQuestCompleted;
        }
    }

    private void OnQuestCompleted(string questId)
    {
        string teleportQuestId = GetTeleportUnlockQuestId();
        if (questId == teleportQuestId)
        {
            UpdateTeleportButtonState();
        }
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestTurnedIn -= OnQuestCompleted;
        }
    }

    private IEnumerator BackupInitializationCheck()
    {
        yield return new WaitForSeconds(2f);
        if (!isInitialized)
        {
            FallbackInitialization();
        }
    }

    private void SetButtonsActive(bool active)
    {
        if (weaponSwitchButton)
        {
            weaponSwitchButton.gameObject.SetActive(true);
            weaponSwitchButton.interactable = active;
        }
        if (autoAttackButton)
        {
            autoAttackButton.gameObject.SetActive(true);
            autoAttackButton.interactable = active;
        }
        if (UtilitySkillButton)
        {
            UtilitySkillButton.gameObject.SetActive(true);
            UtilitySkillButton.interactable = active;
        }
        if (CombatSkillButton)
        {
            CombatSkillButton.gameObject.SetActive(true);
            CombatSkillButton.interactable = active;
        }
        if (UltimateSkillButton)
        {
            UltimateSkillButton.gameObject.SetActive(true);
            UltimateSkillButton.interactable = active;
        }
    }

    public override void Spawned()
    {
        base.Spawned();
        if (Object.HasInputAuthority)
        {
            if (initializeCoroutine == null)
            {
                initializeCoroutine = StartCoroutine(InitializeWithRetry());
            }
        }
    }

    private IEnumerator InitializeWithRetry()
    {
        if (isInitialized)
        {
            yield break;
        }
        yield return new WaitForSeconds(0.05f);
        while (currentRetryAttempt < maxRetryAttempts && !isInitialized)
        {
            currentRetryAttempt++;
            bool initSuccess = TryInitializeCombatSystem();
            if (initSuccess)
            {
                break;
            }
            else
            {
                yield return new WaitForSeconds(retryInterval);
            }
        }
        if (!isInitialized)
        {
            FallbackInitialization();
        }
    }

    private bool TryInitializeCombatSystem()
    {
        GameObject localPlayer = FindLocalPlayerWithTimeout();
        if (localPlayer == null)
        {
            return false;
        }
        weaponSystem = localPlayer.GetComponent<WeaponSystem>();
        if (weaponSystem == null)
        {
            return false;
        }
        NetworkObject netObj = localPlayer.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.HasInputAuthority)
        {
            return false;
        }
        InitializeCombatButtons();
        isInitialized = true;
        return true;
    }

    private GameObject FindLocalPlayerWithTimeout()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            if (player == null) continue;
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsValid && networkObject.HasInputAuthority)
            {
                PlayerStats playerStats = player.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    return player;
                }
            }
        }
        return null;
    }

    private void FallbackInitialization()
    {
        SetButtonsActive(true);
        SetupFallbackEvents();
        StartCoroutine(InitializeSkillSystemDelayed());
        isInitialized = true;
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.CompleteStep("CombatUIReady");
        }
    }

    private void SetupFallbackEvents()
    {
        if (weaponSwitchButton != null)
        {
            weaponSwitchButton.onClick.RemoveAllListeners();
            weaponSwitchButton.onClick.AddListener(() => {
                currentWeaponType = currentWeaponType == PlayerStats.WeaponType.Melee
                    ? PlayerStats.WeaponType.Ranged
                    : PlayerStats.WeaponType.Melee;
                UpdateWeaponButtonStates();
                UpdateWeaponStatusText();
                // Force update ile attack button sprite'ını güncelle
                UpdateAutoAttackButtonState(forceUpdate: true);
            });
        }
        if (autoAttackButton != null)
        {
            autoAttackButton.onClick.RemoveAllListeners();
            autoAttackButton.onClick.AddListener(() => {
            });
        }
    }

    private IEnumerator InitializeSkillSystemDelayed()
    {
        float skillWaitTime = 0f;
        float maxSkillWait = 5f;
        while (skillWaitTime < maxSkillWait)
        {
            GameObject localPlayer = FindLocalPlayerWithTimeout();
            if (localPlayer != null)
            {
                skillSystem = localPlayer.GetComponent<SkillSystem>();
                if (skillSystem != null)
                {
                    SetupSkillButtons();
                    break;
                }
            }
            yield return new WaitForSeconds(0.2f);
            skillWaitTime += 0.2f;
        }
    }

    private void InitializeCombatButtons()
    {
        SetButtonsActive(true);
        if (weaponSwitchButton)
        {
            weaponSwitchButton.onClick.RemoveAllListeners();
            weaponSwitchButton.onClick.AddListener(() =>
            {
                PlayerStats.WeaponType newWeaponType = currentWeaponType == PlayerStats.WeaponType.Melee
                    ? PlayerStats.WeaponType.Ranged
                    : PlayerStats.WeaponType.Melee;

                // ÖNCE CombatInitializer'daki currentWeaponType'ı güncelle
                currentWeaponType = newWeaponType;

                // SONRA WeaponSystem'e bildir
                if (weaponSystem != null)
                {
                    weaponSystem.SwitchWeapon(newWeaponType);
                }

                UpdateWeaponButtonStates();
                UpdateWeaponStatusText();
                // Force update ile attack button sprite'ını güncelle
                UpdateAutoAttackButtonState(forceUpdate: true);
            });
        }
        InitializeSkillSystem();
        SetupSkillButtons();
        currentWeaponType = PlayerStats.WeaponType.Melee;
        UpdateWeaponButtonStates();
        UpdateWeaponStatusText();
        UpdateAutoAttackButtonState();
        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            playerController = localPlayer.GetComponent<PlayerController>();
        }
        UpdateTeleportButtonState();
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.CompleteStep("CombatUIReady");
        }
    }

    private void CheckTeleportUnlockStatus()
    {
        if (QuestManager.Instance == null) return;
        string teleportQuestId = GetTeleportUnlockQuestId();
        bool isUnlocked = QuestManager.Instance.IsQuestTurnedIn(teleportQuestId);
        UpdateTeleportButtonState();
    }

    private void UpdateTeleportButtonState()
    {
        if (teleportButton == null || teleportButtonImage == null) return;
        bool isUnlocked = false;
        if (QuestManager.Instance != null)
        {
            string teleportQuestId = GetTeleportUnlockQuestId();
            isUnlocked = QuestManager.Instance.IsQuestTurnedIn(teleportQuestId);
        }
        teleportButton.interactable = isUnlocked;
        if (isUnlocked && teleportUnlockedSprite != null)
        {
            teleportButtonImage.sprite = teleportUnlockedSprite;
            teleportButtonImage.color = Color.white;
        }
        else if (!isUnlocked && teleportLockedSprite != null)
        {
            teleportButtonImage.sprite = teleportLockedSprite;
            teleportButtonImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        }
    }

    private void OnTeleportButtonClicked()
    {
        if (QuestManager.Instance == null) return;
        string teleportQuestId = GetTeleportUnlockQuestId();
        bool isUnlocked = QuestManager.Instance.IsQuestTurnedIn(teleportQuestId);
        if (!isUnlocked) return;
        if (playerController == null)
        {
            GameObject localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                playerController = localPlayer.GetComponent<PlayerController>();
            }
        }
        if (playerController != null)
        {
            playerController.StartTeleportChannelling();
        }
    }

    private void InitializeSkillSystem()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.HasInputAuthority)
            {
                skillSystem = player.GetComponent<SkillSystem>();
                if (skillSystem != null)
                {
                    StartCoroutine(AutoEquipTestSkills());
                    StartCoroutine(RefreshUIAfterSkillEquip());
                }
                break;
            }
        }
    }

    private System.Collections.IEnumerator RefreshUIAfterSkillEquip()
    {
        yield return new WaitForSeconds(0.5f);
        UpdateSkillButtonVisuals();
    }

    private System.Collections.IEnumerator AutoEquipTestSkills()
    {
        while (SkillDatabase.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        while (skillSystem == null || !skillSystem.Object.IsValid || !skillSystem.Object.HasInputAuthority)
        {
            yield return new WaitForSeconds(0.1f);
        }
        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            ClassSystem classSystem = localPlayer.GetComponent<ClassSystem>();
            if (classSystem != null && classSystem.NetworkPlayerClass != ClassType.None)
            {
                yield break;
            }
        }
    }

    private void SetupSkillButtons()
    {
        if (skillSystem == null) 
        {
            return;
        }
        if (skillSelectionPanel != null)
        {
            skillSelectionPanel.Initialize(skillSystem);
            skillSelectionPanel.OnSkillSelected += OnSkillSelectedFromPanel;
        }
        SetupSkillButtonGestures(UtilitySkillButton, SkillSlot.Skill1);
        SetupSkillButtonGestures(CombatSkillButton, SkillSlot.Skill2);
        SetupSkillButtonGestures(UltimateSkillButton, SkillSlot.Skill3);
    }

    private void SetupSkillButtonGestures(Button skillButton, SkillSlot slot)
    {
        if (skillButton == null)
        {
            return;
        }
        EventTrigger trigger = skillButton.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = skillButton.gameObject.AddComponent<EventTrigger>();
        }
        trigger.triggers.Clear();
        EventTrigger.Entry pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener((data) => {
            OnSkillButtonTouchStart(slot, data as PointerEventData);
        });
        trigger.triggers.Add(pointerDown);
        EventTrigger.Entry drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        drag.callback.AddListener((data) => {
            OnSkillButtonDrag(slot, data as PointerEventData);
        });
        trigger.triggers.Add(drag);
        EventTrigger.Entry pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener((data) => {
            OnSkillButtonTouchEnd(slot, data as PointerEventData);
        });
        trigger.triggers.Add(pointerUp);
    }

    private void OnSkillButtonTouchStart(SkillSlot slot, PointerEventData eventData)
    {
        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            ClassSystem classSystem = localPlayer.GetComponent<ClassSystem>();
            if (classSystem == null || classSystem.NetworkPlayerClass == ClassType.None)
            {
                return;
            }
        }
        Button targetButton = slot switch
        {
            SkillSlot.Skill1 => UtilitySkillButton,
            SkillSlot.Skill2 => CombatSkillButton,
            SkillSlot.Skill3 => UltimateSkillButton,
            _ => null
        };
        if (targetButton == null || !targetButton.interactable)
        {
            return;
        }
        if (skillSystem == null)
        {
            return;
        }
        string skillId = skillSystem.GetEquippedSkillId(slot);
        if (string.IsNullOrEmpty(skillId))
        {
            return;
        }
        currentSkillSlot = slot;
        touchStartPosition = eventData.position;
        touchStartTime = Time.time;
        isDragging = false;
        isInSkillSelectionMode = false;
        if (localPlayer != null && SkillPreviewManager.Instance != null)
        {
            SkillPreviewManager.Instance.ShowSkillPreview(skillId, localPlayer);
        }
    }

    private void OnSkillButtonDrag(SkillSlot slot, PointerEventData eventData)
    {
        if (isInSkillSelectionMode) return;
        Button targetButton = slot switch
        {
            SkillSlot.Skill1 => UtilitySkillButton,
            SkillSlot.Skill2 => CombatSkillButton,
            SkillSlot.Skill3 => UltimateSkillButton,
            _ => null
        };
        if (targetButton == null || !targetButton.interactable)
        {
            return;
        }
        Vector2 dragDistance = eventData.position - touchStartPosition;
        float holdTime = Time.time - touchStartTime;
        if (dragDistance.y > dragThreshold && holdTime > holdTimeThreshold)
        {
            isDragging = true;
            EnterSkillSelectionMode(slot, eventData.position);
        }
    }

    private void OnSkillButtonTouchEnd(SkillSlot slot, PointerEventData eventData)
    {
        if (SkillPreviewManager.Instance != null)
        {
            SkillPreviewManager.Instance.HideCurrentPreview();
        }
        if (isInSkillSelectionMode)
        {
            return;
        }
        if (!isDragging)
        {
            UseSkillInSlot(slot);
        }
        ResetTouchState();
    }

    private void EnterSkillSelectionMode(SkillSlot slot, Vector2 screenPosition)
    {
        isInSkillSelectionMode = true;
        if (SkillPreviewManager.Instance != null)
        {
            SkillPreviewManager.Instance.HideCurrentPreview();
        }
        if (skillSelectionPanel != null)
        {
            Button sourceButton = slot switch
            {
                SkillSlot.Skill1 => UtilitySkillButton,
                SkillSlot.Skill2 => CombatSkillButton,
                SkillSlot.Skill3 => UltimateSkillButton,
                _ => null
            };
            if (sourceButton != null)
            {
                Vector3 buttonWorldPos = sourceButton.transform.position;
                skillSelectionPanel.ShowSelectionPanel(slot, buttonWorldPos);
            }
        }
    }

    private void OnSkillSelectedFromPanel(int selectedIndex)
    {
        isInSkillSelectionMode = false;
        ResetTouchState();
        UpdateSkillButtonVisuals();
    }

    private void ResetTouchState()
    {
        isDragging = false;
        isInSkillSelectionMode = false;
        touchStartTime = 0f;
    }

    private void UseSkillInSlot(SkillSlot slot)
    {
        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            ClassSystem classSystem = localPlayer.GetComponent<ClassSystem>();
            if (classSystem == null || classSystem.NetworkPlayerClass == ClassType.None)
            {
                return;
            }
        }
        if (skillSystem == null)
        {
            return;
        }
        string skillId = skillSystem.GetEquippedSkillId(slot);
        if (string.IsNullOrEmpty(skillId))
        {
            return;
        }
        Button targetButton = slot switch
        {
            SkillSlot.Skill1 => UtilitySkillButton,
            SkillSlot.Skill2 => CombatSkillButton,
            SkillSlot.Skill3 => UltimateSkillButton,
            _ => null
        };
        if (targetButton == null || !targetButton.interactable)
        {
            return;
        }
        var skillInstance = skillSystem.GetSkillInstance(skillId);
        if (skillInstance?.skillData == null)
        {
            return;
        }
        switch (slot)
        {
            case SkillSlot.Skill1:
                skillSystem.UseSkill1();
                break;
            case SkillSlot.Skill2:
                skillSystem.UseSkill2();
                break;
            case SkillSlot.Skill3:
                skillSystem.UseSkill3();
                break;
        }
    }

    private void UpdateSkillButtonVisuals()
    {
        UpdateSkillButtonIcon(SkillSlot.Skill1, UtilitySkillButton);
        UpdateSkillButtonIcon(SkillSlot.Skill2, CombatSkillButton);
        UpdateSkillButtonIcon(SkillSlot.Skill3, UltimateSkillButton);
    }

    private void UpdateSkillButtonIcon(SkillSlot slot, Button button)
    {
        if (skillSystem == null || button == null) return;
        string activeSkillId = skillSystem.GetEquippedSkillId(slot);
        if (!string.IsNullOrEmpty(activeSkillId))
        {
            var skillData = SkillDatabase.Instance?.GetSkillById(activeSkillId);
            if (skillData != null)
            {
                Image iconImage = button.transform.Find("Icon")?.GetComponent<Image>();
                if (iconImage != null)
                {
                    iconImage.sprite = skillData.skillIcon;
                }
            }
        }
    }
public void SetNearbyBindstone(BindstoneInteraction bindstone)
{
    currentNearbyBindstone = bindstone;
    UpdateAutoAttackButtonState();
}

public void RemoveNearbyBindstone(BindstoneInteraction bindstone)
{
    if (currentNearbyBindstone == bindstone)
    {
        currentNearbyBindstone = null;
        UpdateAutoAttackButtonState();
    }
}
    public void UpdateWeaponButtonStates()
    {
        if (weaponSwitchButton == null)
        {
            return;
        }
        if (weaponSwitchButtonImage == null)
        {
            Transform iconTransform = weaponSwitchButton.transform.Find("Icon");
            if (iconTransform != null)
            {
                weaponSwitchButtonImage = iconTransform.GetComponent<Image>();
            }
            else
            {
                return;
            }
        }
        if (weaponSwitchButtonImage != null)
        {
            Sprite targetSprite = currentWeaponType == PlayerStats.WeaponType.Melee 
                ? rangedWeaponSprite
                : meleeWeaponSprite;
            weaponSwitchButtonImage.sprite = targetSprite;
            Color normalColor = weaponSwitchButtonImage.color;
            normalColor.a = 1.0f;
            weaponSwitchButtonImage.color = normalColor;
        }
    }

    private void UpdateWeaponStatusText()
    {
        if (weaponStatusText != null)
        {
            string statusText = currentWeaponType switch
            {
                PlayerStats.WeaponType.Melee => "YAKIN SALDIRI AKTIF",
                PlayerStats.WeaponType.Ranged => "UZAK SALDIRI AKTIF",
                _ => "SALDIRI AKTIF"
            };
            weaponStatusText.text = statusText;
        }
    }
}