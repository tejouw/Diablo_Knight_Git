using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion;  // Photon.Pun yerine
using DG.Tweening;
using UnityEngine.EventSystems;

    [System.Serializable]
    public class PortalDestination
    {
        public string destinationName;
        public Vector2 position;
    }

    public class Portal : NetworkBehaviour  
    {
    [Header("Portal Settings")]
    [SerializeField] private float interactionRadius = 3f;
    [SerializeField] private Vector2 centerOffset = Vector2.zero;
private GameObject cachedLocalPlayer;
private float lastPlayerSearchTime;
private float lastDistanceCheckTime;
private const float PLAYER_SEARCH_INTERVAL = 1f; // 1 saniyede bir player ara
private const float DISTANCE_CHECK_INTERVAL = 0.1f; // 0.1 saniyede bir mesafe kontrol et
private const float DISTANCE_CHECK_THRESHOLD = 0.5f; // Mesafe değişim eşiği
private Vector2 lastPlayerPosition;
private bool uiCurrentlyShown = false;
        private GameObject portalUI;
        private RectTransform buttonContainer;
        private CanvasGroup canvasGroup;

        [Header("Portal Destinations")]
        public List<PortalDestination> destinations = new List<PortalDestination>();

        private void Start()
        {
            CreateUI();
        }

        private void CreateUI()
        {
            // Ana Canvas oluştur
            portalUI = new GameObject("PortalUI_" + gameObject.name);
            Canvas canvas = portalUI.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // En üstte görüntülenmesi için
            portalUI.AddComponent<CanvasScaler>();
            portalUI.AddComponent<GraphicRaycaster>();
            canvasGroup = portalUI.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            // Arka plan panel
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(portalUI.transform, false);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            
            // Panel pozisyonlaması
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(300, destinations.Count * 70 + 100);
            panelRect.anchoredPosition = new Vector2(0, -100f);
            // Panel scale'i sıfırla (animasyon için)
            panelRect.localScale = Vector3.zero;

            // Başlık
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "TELEPORT POINTS";
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.sizeDelta = new Vector2(0, 50);
            titleRect.anchoredPosition = new Vector2(0, -25);

            // Buton container
            GameObject containerObj = new GameObject("ButtonContainer");
            containerObj.transform.SetParent(panel.transform, false);
            buttonContainer = containerObj.AddComponent<RectTransform>();
            
            // Container pozisyonlaması
            buttonContainer.anchorMin = new Vector2(0, 0);
            buttonContainer.anchorMax = new Vector2(1, 1);
            buttonContainer.pivot = new Vector2(0.5f, 1);
            buttonContainer.offsetMin = new Vector2(20, 20);
            buttonContainer.offsetMax = new Vector2(-20, -70);

            // Butonları oluştur
            for (int i = 0; i < destinations.Count; i++)
            {
                CreateButton(destinations[i], i);
            }

            portalUI.SetActive(false);
        }

        private void CreateButton(PortalDestination destination, int index)
        {
            GameObject buttonObj = new GameObject(destination.destinationName + "Button");
            buttonObj.transform.SetParent(buttonContainer, false);

            Button button = buttonObj.AddComponent<Button>();
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1);

            // Button renkleri
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 1);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1);
            colors.selectedColor = new Color(0.2f, 0.2f, 0.2f, 1);
            button.colors = colors;

            // Button pozisyonlaması
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0, 1);
            buttonRect.anchorMax = new Vector2(1, 1);
            buttonRect.pivot = new Vector2(0.5f, 1);
            buttonRect.sizeDelta = new Vector2(0, 60);
            buttonRect.anchoredPosition = new Vector2(0, -index * 65);

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = destination.destinationName;
            buttonText.fontSize = 24;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            // Hover animasyonları
            button.onClick.AddListener(() => TeleportToDestination(destination.position));
            
            // Hover component'i ekle
            ButtonHoverEffect hoverEffect = buttonObj.AddComponent<ButtonHoverEffect>();
            hoverEffect.Initialize(buttonRect, buttonImage, colors.normalColor, colors.highlightedColor);
        }

private void ShowUI()
{
    if (portalUI == null) return;
    
    portalUI.SetActive(true);
    
    // Panel animasyonu
    RectTransform panelRect = portalUI.transform.GetChild(0).GetComponent<RectTransform>();
    canvasGroup.alpha = 0f;
    panelRect.localScale = Vector3.zero;

    // Fade ve scale animasyonu
    canvasGroup.DOFade(1f, 0.3f);
    panelRect.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
}

private void HideUI()
{
    if (portalUI == null || !portalUI.activeInHierarchy) return;

    RectTransform panelRect = portalUI.transform.GetChild(0).GetComponent<RectTransform>();
    
    // Kapanış animasyonu
    Sequence hideSequence = DOTween.Sequence();
    hideSequence.Append(canvasGroup.DOFade(0f, 0.2f));
    hideSequence.Join(panelRect.DOScale(0f, 0.3f).SetEase(Ease.InBack));
    hideSequence.OnComplete(() => {
        if (portalUI != null) portalUI.SetActive(false);
    });
}

    private void Update()
    {
        if (Runner == null || !Runner.IsRunning) return;

        float currentTime = Time.time;

        // Local player'ı belirli aralıklarla ara veya cache'den al
        if (cachedLocalPlayer == null || currentTime - lastPlayerSearchTime > PLAYER_SEARCH_INTERVAL)
        {
            FindAndCacheLocalPlayer();
            lastPlayerSearchTime = currentTime;
        }

        // Local player yoksa çık
        if (cachedLocalPlayer == null)
        {
            if (uiCurrentlyShown)
            {
                HideUI();
                uiCurrentlyShown = false;
            }
            return;
        }

        // Player'ın aktif olup olmadığını kontrol et
        if (!IsPlayerValid(cachedLocalPlayer))
        {
            cachedLocalPlayer = null;
            if (uiCurrentlyShown)
            {
                HideUI();
                uiCurrentlyShown = false;
            }
            return;
        }

        // Mesafe kontrolünü optimize et
        if (currentTime - lastDistanceCheckTime > DISTANCE_CHECK_INTERVAL)
        {
            CheckPlayerDistance();
            lastDistanceCheckTime = currentTime;
        }
    }
private void FindAndCacheLocalPlayer()
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    
    foreach (GameObject player in players)
    {
        if (IsPlayerValid(player))
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
            {
                cachedLocalPlayer = player;
                lastPlayerPosition = player.transform.position;
                return;
            }
        }
    }
    
    cachedLocalPlayer = null;
}

private bool IsPlayerValid(GameObject player)
{
    if (player == null || !player.activeInHierarchy) return false;
    
    NetworkObject netObj = player.GetComponent<NetworkObject>();
    if (netObj == null || !netObj.IsValid) return false;
    
    return true;
}

private void CheckPlayerDistance()
{
    Vector2 currentPlayerPos = cachedLocalPlayer.transform.position;
    
    // Sadece player önemli ölçüde hareket ettiyse mesafe hesapla
    if (Vector2.Distance(lastPlayerPosition, currentPlayerPos) > DISTANCE_CHECK_THRESHOLD || !uiCurrentlyShown)
    {
        // Portal merkezini offset ile hesapla
        Vector2 portalCenter = (Vector2)transform.position + centerOffset;
        float distance = Vector2.Distance(portalCenter, currentPlayerPos);
        bool shouldShowUI = distance <= interactionRadius;

        if (shouldShowUI != uiCurrentlyShown)
        {
            if (shouldShowUI)
            {
                ShowUI();
                uiCurrentlyShown = true;
            }
            else
            {
                HideUI();
                uiCurrentlyShown = false;
            }
        }
        
        lastPlayerPosition = currentPlayerPos;
    }
}

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 portalCenter = (Vector2)transform.position + centerOffset;
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(portalCenter, 0.2f); // Merkez noktası
    }

private void TeleportToDestination(Vector2 destination)
{
    if (Runner == null || !Runner.IsRunning) return;

    GameObject localPlayer = null;
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    
    foreach (GameObject player in players)
    {
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj != null && netObj.HasInputAuthority)
        {
            localPlayer = player;
            break;
        }
    }

    if (localPlayer != null)
    {
        // ✅ YENİ: Cooldown kontrolü ekle
        PlayerController playerController = localPlayer.GetComponent<PlayerController>();
        if (playerController != null && playerController.IsTeleportCooldown())
        {
            return;
        }
        
        // ✅ YENİ: Channelling kontrolü ekle
        if (playerController != null && playerController.IsCurrentlyChannelling())
        {
            return;
        }
        
        // Önce UI'ı kapat
        HideUI();

        // Kısa bir delay sonra ışınlan
        DOVirtual.DelayedCall(0.3f, () => {
            NetworkCharacterController networkController = localPlayer.GetComponent<NetworkCharacterController>();
            if (networkController != null)
            {
                networkController.RequestTeleportRPC(destination);
            }
        });
    }
}

    }
public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform rectTransform;
    private Image buttonImage;
    private Color normalColor;
    private Color highlightedColor;

    public void Initialize(RectTransform rect, Image image, Color normal, Color highlighted)
    {
        rectTransform = rect;
        buttonImage = image;
        normalColor = normal;
        highlightedColor = highlighted;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        rectTransform.DOScale(1.05f, 0.2f).SetEase(Ease.OutQuad);
        buttonImage.DOColor(highlightedColor, 0.2f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        rectTransform.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
        buttonImage.DOColor(normalColor, 0.2f);
    }

    private void OnDestroy()
    {
        // Tweenleri temizle
        rectTransform?.DOKill();
        buttonImage?.DOKill();
    }
}