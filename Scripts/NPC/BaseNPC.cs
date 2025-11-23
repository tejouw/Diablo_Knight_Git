// Path: Assets/Game/Scripts/BaseNPC.cs

using UnityEngine;
using Fusion;
using UnityEngine.UI;
using TMPro;

// NPC türlerini belirleyen enum
public enum NPCType
{
    Merchant,
    Trainer,
    Blacksmith,
    Wandering,
    DialogQuest,
    Craft        // YENİ: Craft NPC'ler için
}
// NPC'lerin temel davranışlarını içeren abstract class
public abstract class BaseNPC : NetworkBehaviour
{
    [Header("NPC Settings")]
    [SerializeField] protected string npcName;
    [SerializeField] protected NPCType npcType;
    [SerializeField] protected float interactionRange = 4f;
    [Header("UI Position Settings")]
[SerializeField] private Vector3 canvasOffset = new Vector3(0f, 3f, 0f);
[SerializeField] private float canvasUIScale = 0.02f;

    [Header("UI Elements")]
    [SerializeField] protected GameObject npcPanel;
    [SerializeField] protected TextMeshProUGUI npcNameText;
    [Header("UI Elements")]
[SerializeField] protected GameObject npcCanvasPrefab;
    public bool isPlayerInRange;
    protected NetworkObject localPlayerObject;
    protected Canvas npcCanvas;
    public string NPCName => npcName;
    public NPCType NPCType => npcType;
    public static event System.Action<string, int> OnNPCInteraction;
    [Header("Button Sprite Settings")]
public float InteractionRange => interactionRange;

[Rpc(RpcSources.All, RpcTargets.All)]
public virtual void SetWanderBoundsRPC(Vector2 min, Vector2 max)
{
    Debug.Log($"[BaseNPC] SetWanderBoundsRPC called on {npcName}, but not implemented");
}

[Rpc(RpcSources.All, RpcTargets.All)]
protected virtual void InitializeNPCRPC(string name, int type)
{
    npcName = name;
    npcType = (NPCType)type;

    // Server modunda UI işlemleri yapma
    if (!IsServerMode() && npcNameText != null)
    {
        npcNameText.text = npcName;
    }
}

public bool IsPlayerInRange() 
{
    return isPlayerInRange;
}
    public virtual void HandleInteraction()
    {
        if (!IsPlayerInRange())
        {
            return;
        }

        // Quest kontrolü - önce QuestGiver'ı kontrol et
        QuestGiver questGiver = GetComponent<QuestGiver>();
        if (questGiver != null)
        {
            questGiver.HandleNPCInteraction();
            
            // Event'i tetikle
            TriggerInteractionEvent();
            return;
        }

        // QuestGiver yoksa normal NPC panelini aç
        OpenInteractionPanel();
        
        // Normal etkileşimlerde de eventi tetikle
        TriggerInteractionEvent();
    }
private void ApplyCustomCanvasSettings()
{


    // Canvas pozisyonunu ve scale'i ayarla
    if (npcCanvas != null)
    {
        npcCanvas.transform.localPosition = canvasOffset;
        npcCanvas.transform.localScale = new Vector3(canvasUIScale, canvasUIScale, canvasUIScale);
    }
}
public void TriggerInteractionEvent()
{
    if (OnNPCInteraction != null)
    {
        OnNPCInteraction.Invoke(npcName, Runner.LocalPlayer.PlayerId);  // PhotonNetwork.LocalPlayer.ActorNumber yerine
    }
}
public Canvas GetNPCCanvas() 
{
    return npcCanvas;}



public virtual void HandleNPCTypeInteraction()
{
    // Mesleki olmayan NPC'ler için basit konuşma
    if (npcType != NPCType.Merchant && npcType != NPCType.Blacksmith && 
        npcType != NPCType.Trainer && npcType != NPCType.Wandering &&
        npcType != NPCType.Craft)  // YENİ satır
    {
        ShowSimpleMessage();
    }
    else
    {
        // Mesleki NPC'ler kendi HandleNPCTypeInteraction'ını override etsin
        Debug.Log($"[BaseNPC] {npcName} ile etkileşim: {npcType}");
    }
}

private void ShowSimpleMessage()
{
    string[] messages = {
        "Merhaba!",
        "Nasılsın?", 
        "Güzel bir gün.",
        "Dikkatli ol buralarda."
    };
    
    string randomMessage = messages[UnityEngine.Random.Range(0, messages.Length)];
    
    // Basit UI message göster
    if (UIManager.Instance != null)
    {
        UIManager.Instance.ShowNotification(randomMessage);
    }
}

    protected virtual void Awake()
    {
        // Server modunda UI oluşturma - CreateNPCUI içinde de kontrol var ama
        // Awake'de erken kontrol daha performanslı
        if (!IsServerMode())
        {
            CreateNPCUI();
        }
    }

    protected virtual void Start()
    {
        // Server modunda UI işlemleri yapma
        if (!IsServerMode() && npcNameText != null)
        {
            npcNameText.text = npcName;
        }

        // Fusion network kontrolü
        if (Runner != null && Runner.IsRunning)
        {
            InitializeSceneNPC();
        }
    }
public override void Spawned()
{
    // Network spawn olduğunda çalışır
    InitializeSceneNPC();
}

// Yeni metod - sahne NPC'lerini başlatmak için
private void InitializeSceneNPC()
{
    if (string.IsNullOrEmpty(npcName))
    {
        npcName = gameObject.name;
    }
    
    // Network objesi kontrolü
    bool isNetworkObject = Object != null && Object.IsValid;
    
    if (isNetworkObject && Object.HasInputAuthority)
    {
        // Network objesi için RPC çağır
        InitializeNPCRPC(npcName, (int)npcType);
        
        if (npcType == NPCType.Wandering)
        {
            WanderingNPC wanderingNPC = GetComponent<WanderingNPC>();
            if (wanderingNPC != null)
            {
                Vector2 currentPos = transform.position;
                Vector2 wanderMin = currentPos - Vector2.one * 5f;
                Vector2 wanderMax = currentPos + Vector2.one * 5f;
                SetWanderBoundsRPC(wanderMin, wanderMax);
            }
        }
    }
    else if (!isNetworkObject)
    {
        // Sahne objesi için direct initialization
        if (npcNameText != null)
        {
            npcNameText.text = npcName;
        }
    }
}
// Field ekle (en üstte)
private float lastDistanceCheckTime = 0f;
private const float DISTANCE_CHECK_INTERVAL = 0.5f; // Yarım saniyede bir kontrol

protected virtual void Update()
{
    // Yarım saniyede bir kontrol yap
    if (Time.time - lastDistanceCheckTime >= DISTANCE_CHECK_INTERVAL)
    {
        CheckPlayerDistance();
        lastDistanceCheckTime = Time.time;
    }
}

protected virtual void CreateNPCUI()
{
    // Server modunda UI oluşturma
    if (IsServerMode())
    {
        return;
    }

    // Prefab'ı yükle (eğer Inspector'da atanmamışsa)
    if (npcCanvasPrefab == null)
    {
        npcCanvasPrefab = Resources.Load<GameObject>("Prefabs/NPCCanvas");
        if (npcCanvasPrefab == null)
        {
            Debug.LogError($"[BaseNPC] {npcName} - NPCCanvas prefab bulunamadı! Resources/Prefabs/NPCCanvas yolunu kontrol edin.");
            return;
        }
    }

    // Canvas prefab'ını instantiate et
    GameObject canvasInstance = Instantiate(npcCanvasPrefab, transform);
    npcCanvas = canvasInstance.GetComponent<Canvas>();

    if (npcCanvas == null)
    {
        Debug.LogError($"[BaseNPC] {npcName} - Instantiate edilen prefab'da Canvas component bulunamadı!");
        return;
    }

    // Prefab'dan TextMeshProUGUI referansını al
    npcNameText = canvasInstance.GetComponentInChildren<TextMeshProUGUI>();
    if (npcNameText == null)
    {
        Debug.LogError($"[BaseNPC] {npcName} - Canvas prefab'ında TextMeshProUGUI bulunamadı!");
        return;
    }

    // NPC adını ayarla
    npcNameText.text = npcName;

    // Custom ayarları uygula
    ApplyCustomCanvasSettings();
}

protected virtual GameObject CreateInteractionButton(Transform parent)
{
    GameObject buttonObj = new GameObject("InteractionButton");
    buttonObj.transform.SetParent(parent, false);

    // Button component'lerini ekle
    Button button = buttonObj.AddComponent<Button>();
    Image buttonImage = buttonObj.AddComponent<Image>();
    buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

    // Button için RectTransform ayarları
    RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
    buttonRect.sizeDelta = new Vector2(80, 30);
    buttonRect.localPosition = Vector3.up * 0.8f;

    // Button text
    GameObject textObj = new GameObject("ButtonText");
    textObj.transform.SetParent(buttonObj.transform, false);
    
    TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
    buttonText.text = npcName; // NPC adını kullan
    buttonText.fontSize = 20;
    buttonText.alignment = TextAlignmentOptions.Center;
    buttonText.color = Color.white;
    
    RectTransform textRect = textObj.GetComponent<RectTransform>();
    textRect.sizeDelta = new Vector2(80, 30);
    textRect.anchoredPosition = Vector2.zero;

    // Click event'ini ayarla
    button.onClick.AddListener(() => 
    {
        OnInteractionButtonClicked();
    });
    
    return buttonObj;
}

protected virtual void CheckPlayerDistance()
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    bool wasInRange = isPlayerInRange;
    isPlayerInRange = false;

    foreach (GameObject player in players)
    {
        NetworkObject netObj = player.GetComponent<NetworkObject>();  
        if (netObj != null && netObj.HasInputAuthority)  // pView.IsMine yerine
        {
            localPlayerObject = netObj;  // localPlayerView yerine localPlayerObject
            float distance = Vector2.Distance(transform.position, player.transform.position);
            isPlayerInRange = distance <= interactionRange;
            
            if (wasInRange != isPlayerInRange)
            {
                CombatInitializer combatInit = CombatInitializer.Instance;
                if (combatInit != null)
                {
                    if (isPlayerInRange)
                    {
                        combatInit.SetNearbyNPC(this);
                    }
                    else
                    {
                        combatInit.RemoveNearbyNPC(this);
                    }
                }
            }
            break;
        }
    }
}
protected virtual void OnInteractionButtonClicked()
{
    if (!isPlayerInRange || localPlayerObject == null)  // localPlayerView yerine localPlayerObject
    {
        return;
    }
    
    OpenInteractionPanel();
}

// BaseNPC.cs - OpenInteractionPanel metodunu eski haline döndür

public virtual void OpenInteractionPanel()
{
    // Önce QuestGiver bileşenini ara
    QuestGiver questGiver = GetComponent<QuestGiver>();
    
    // Eğer NPC'nin quest vermek gibi bir özelliği varsa ve quest durumu varsa
    if (questGiver != null)
    {
        questGiver.HandleNPCInteraction();
    }
    else
    {
        // Özel NPC davranışlarını uygula - NPC türüne özgü kodlar
        HandleNPCTypeInteraction();
    }
}
    protected virtual void CloseInteractionPanel()
    {
        if (npcPanel != null)
            npcPanel.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        // Etkileşim mesafesini görselleştir
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }

    private bool IsServerMode()
    {
        if (Application.isEditor) return false;

        string[] args = System.Environment.GetCommandLineArgs();
        return System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode");
    }
}