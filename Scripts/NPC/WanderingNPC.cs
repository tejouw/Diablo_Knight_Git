using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using Fusion.Addons.Physics;

public class WanderingNPC : BaseNPC
{
        [Header("Character4D Settings")]
    [SerializeField] private Character4D character4D;
    [Header("UI Settings")]
    [SerializeField] private TMP_FontAsset customFont; // Inspector'dan atanacak font
    [Header("Wandering Settings")]
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float minWanderTime = 1f;
    [SerializeField] private float maxWanderTime = 3f;
    [SerializeField] private float minIdleTime = 2f;
    [SerializeField] private float maxIdleTime = 5f;
    [SerializeField] private float wanderRadius = 3f;
    [SerializeField] private Vector2 boundaryMin;
    [SerializeField] private Vector2 boundaryMax;
    [SerializeField] private Transform spawnPoint;
[Header("Stuck Detection")]
[SerializeField] private float minMovementDistance = 1f; // Minimum hareket mesafesi
private Vector2 wanderingStartPosition; // Wandering başladığında pozisyon
    [Header("Dialogue Settings")]
    [SerializeField] private float speechBubbleTime = 4f;
    [SerializeField] private float dialogTriggerDistance = 5f;
    [SerializeField] private List<string> dialogueTexts = new List<string>();
    [SerializeField] private List<string> rareDialogueTexts = new List<string>();
    [SerializeField] private float rareDialogueChance = 0.05f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
[Networked] public Vector2 NetworkCurrentDirection { get; set; }
[Networked] public int NetworkAnimationState { get; set; }
private CharacterState currentAnimationState = CharacterState.Idle;

private Vector2 lastSyncedDirection = Vector2.zero;
private CharacterState lastSyncedAnimationState = CharacterState.Idle;
    private Vector2 startPosition;
    private Vector2 targetPosition;
    private bool isWandering = false;
    private float nextActionTime = 0f;
    private float nextSpeechTime = 0f;
    private GameObject speechBubble;
    private TextMeshProUGUI speechText;
    private TypingEffect typingEffect = new TypingEffect();
    private static int activeDialogueCount = 0;
    [Header("Obstacle Avoidance")]
[SerializeField] private LayerMask obstacleLayerMask; // Inspector'dan atanacak engel katmanları
[Networked] public bool NetworkIsWandering { get; set; }
[Networked] public Vector2 NetworkTargetPosition { get; set; }
[Networked] public float NetworkNextStateChangeTime { get; set; }


protected override void Awake()
{
    // NetworkRigidbody2D ekle
    NetworkRigidbody2D networkRb = GetComponent<NetworkRigidbody2D>();
    if (networkRb == null)
    {
        networkRb = gameObject.AddComponent<NetworkRigidbody2D>();
    }
    
    rb = networkRb.Rigidbody;
    if (rb == null)
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
    }
    
    // Rigidbody ayarları
    rb.bodyType = RigidbodyType2D.Dynamic;
    rb.freezeRotation = true;
    rb.linearDamping = 5f;
    rb.gravityScale = 0f;
    
    animator = GetComponent<Animator>();
    
    // Başlangıç pozisyonu
    startPosition = transform.position;
    targetPosition = startPosition;
    
    // Layer mask kontrolü - Inspector'dan ayarlanmalı
    if (obstacleLayerMask == 0)
    {
        Debug.LogWarning($"[WanderingNPC] {npcName} - ObstacleLayerMask ayarlanmamış!");
    }
    
    base.Awake();
}
[Rpc(RpcSources.All, RpcTargets.All)]
private void SyncCharacterAppearanceRPC(string characterJson)
{
    if (character4D != null)
    {
        try
        {
            character4D.FromJson(characterJson, silent: true);
            character4D.Initialize();
            character4D.SetDirection(Vector2.down);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WanderingNPC] Karakter senkronizasyon hatası: {e.Message}");
        }
    }
}
private void SyncCharacterAppearance()
{
    if (!Object.HasInputAuthority) return;
    
    string characterJson = character4D.ToJson();
    if (!string.IsNullOrEmpty(characterJson))
    {
        SyncCharacterAppearanceRPC(characterJson);
    }
}
protected override void Start()
{
    base.Start();
    EnsureCharacterInitialized();
    
    // Speech bubble'ı oluştur
    CreateSpeechBubble();
}

protected override void Update()
{
    // base.Update() çağırma - kendi sistemini kullan
    
    // Sadece dialog kontrolü yap
    CheckPlayerProximityForDialog();
    
    // Kendi player distance kontrolü (CombatInitializer'a bildirim yapmadan)
    CheckPlayerDistance();
}




private Vector2 GetCardinalDirection(Vector2 direction)
{
    if (direction.magnitude < 0.1f) return Vector2.down;
    
    // En yakın cardinal direction'ı bul
    if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
    {
        // Yatay hareket daha baskın
        return direction.x > 0 ? Vector2.right : Vector2.left;
    }
    else
    {
        // Dikey hareket daha baskın
        return direction.y > 0 ? Vector2.up : Vector2.down;
    }
}
[Rpc(RpcSources.All, RpcTargets.All)]
private void SyncWanderingStateRPC(bool wanderingState, Vector2 newTargetPos, float actionTime)
{
    isWandering = wanderingState;
    targetPosition = newTargetPos;
    nextActionTime = actionTime;
}

private void SetNewTargetPosition()
{
    Vector2 direction = Random.insideUnitCircle.normalized;
    Vector2 currentPos = transform.position;
    
    Vector2 newTargetPosition = currentPos + direction * Random.Range(2f, wanderRadius);
    
    newTargetPosition.x = Mathf.Clamp(newTargetPosition.x, boundaryMin.x, boundaryMax.x);
    newTargetPosition.y = Mathf.Clamp(newTargetPosition.y, boundaryMin.y, boundaryMax.y);
    
    targetPosition = newTargetPosition;
    
    // Network property'yi güncelle
    if (Object != null && Object.IsValid)
    {
        NetworkTargetPosition = newTargetPosition;
        
        if (Object.HasStateAuthority)
        {
            SyncWanderingStateRPC(NetworkIsWandering, NetworkTargetPosition, NetworkNextStateChangeTime);
        }
    }
}
private void MoveToTargetPosition()
{
    Vector2 currentPosition = transform.position;
    Vector2 direction = (NetworkTargetPosition - currentPosition).normalized;
    float distance = Vector2.Distance(currentPosition, NetworkTargetPosition);
    
    if (distance > 0.1f)
    {
        Vector2 velocity = direction * (moveSpeed * 0.5f);
        
        NetworkRigidbody2D networkRb = GetComponent<NetworkRigidbody2D>();
        if (networkRb != null && networkRb.Rigidbody != null)
        {
            networkRb.Rigidbody.linearVelocity = velocity;
        }
        else
        {
            rb.linearVelocity = velocity;
        }
        
        UpdateAnimationState(direction);
    }
    else
    {
        // Hedefe erken ulaştı, yine de timer bitene kadar bekle
        StopMovement();
        UpdateAnimationState(Vector2.zero);
    }
}


private void StopMovement()
{
    NetworkRigidbody2D networkRb = GetComponent<NetworkRigidbody2D>();
    if (networkRb != null && networkRb.Rigidbody != null)
    {
        networkRb.Rigidbody.linearVelocity = Vector2.zero;
    }
    else if (rb != null)
    {
        rb.linearVelocity = Vector2.zero;
    }
}


private void UpdateAnimationState(Vector2 direction)
{
    // Hareket edip etmediğini belirle
    bool isMoving = direction.magnitude > 0.1f;
    
    CharacterState newAnimationState;
    Vector2 newDirection;
    
    // Character4D yönünü ayarla
    if (character4D != null)
    {
        if (isMoving)
        {
            newDirection = GetCardinalDirection(direction);
            newAnimationState = CharacterState.Walk;
            
            character4D.SetDirection(newDirection);
            character4D.AnimationManager.SetState(CharacterState.Walk);
            currentAnimationState = CharacterState.Walk; // Local state'i güncelle
        }
        else
        {
            newDirection = character4D.Direction; // Mevcut direction'ı koru
            newAnimationState = CharacterState.Idle;
            
            character4D.AnimationManager.SetState(CharacterState.Idle);
            currentAnimationState = CharacterState.Idle; // Local state'i güncelle
        }
        
        // SADECE DEĞİŞİKLİK VARSA network'e sync et
        if (Object.HasStateAuthority && 
            (newDirection != lastSyncedDirection || newAnimationState != lastSyncedAnimationState))
        {
            NetworkCurrentDirection = newDirection;
            NetworkAnimationState = (int)newAnimationState;
            
            lastSyncedDirection = newDirection;
            lastSyncedAnimationState = newAnimationState;
        }
    }
}
public override void Render()
{
    // Client'lar için network state'i kullan
    if (Object != null && Object.IsValid && !Object.HasStateAuthority)
    {
        if (character4D != null)
        {
            // Direction sync - SetDirection metodunu kullan
            if (NetworkCurrentDirection != Vector2.zero && NetworkCurrentDirection != character4D.Direction)
            {
                character4D.SetDirection(NetworkCurrentDirection);
            }
            
            // Animation sync - sadece farklıysa set et
            CharacterState networkState = (CharacterState)NetworkAnimationState;
            if (currentAnimationState != networkState)
            {
                if (character4D.AnimationManager != null)
                {
                    character4D.AnimationManager.SetState(networkState);
                    currentAnimationState = networkState;
                }
            }
        }
    }
}
private void OnCollisionEnter2D(Collision2D collision)
{
    // Collision detection sistemi kaldırıldı
    // Artık sadece hareket mesafesi kontrolü yapıyoruz
}
private void CheckPlayerProximityForDialog()
{
    // NULL KONTROLÜ EKLE
    if (speechBubble == null) return;
    
    bool playerInRange = false;
    GameObject localPlayerObj = null;
    
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj != null && networkObj.HasInputAuthority)
        {
            localPlayerObj = player;
            break;
        }
    }
    
    if (localPlayerObj != null)
    {
        float distance = Vector2.Distance(transform.position, localPlayerObj.transform.position);
        playerInRange = distance <= dialogTriggerDistance;
    }
    
    if (playerInRange && !speechBubble.activeSelf)
    {
        if (Time.time >= nextSpeechTime)
        {
            ShowRandomDialogue();
            nextSpeechTime = Time.time + 0.5f;
        }
    }
    else if (!playerInRange && speechBubble.activeSelf)
    {
        HideSpeechBubble();
    }
}
private void HideSpeechBubble()
{
    if (speechBubble != null && speechBubble.activeSelf)
    {
        // Kapanma animasyonunu başlat
        StartCoroutine(AnimateSpeechBubbleClose());
        WanderingNPC.activeDialogueCount--;
    }
}
private void ShowRandomDialogue()
{
    if (dialogueTexts == null || dialogueTexts.Count == 0)
    {
        Debug.LogError("[WanderingNPC] Dialog metinleri bulunamadı!");
        return;
    }
            
    string selectedText;
    
    // Nadir dialog şansı
    if (rareDialogueTexts != null && rareDialogueTexts.Count > 0 && Random.value < rareDialogueChance)
    {
        selectedText = rareDialogueTexts[Random.Range(0, rareDialogueTexts.Count)];
    }
    else
    {
        selectedText = dialogueTexts[Random.Range(0, dialogueTexts.Count)];
    }
    
    // Doğrudan görüntüle - RPC KULLANMA
    DisplaySpeechBubble(selectedText);
}
// WanderingNPC.cs içine eklenecek yeni metod
private void EnsureCharacterInitialized()
{
    if (character4D == null) 
    {
        character4D = GetComponent<Character4D>();
        if (character4D == null) return;
    }
    
    // Karakter initialize et
    character4D.Initialize();
    
    // Yönü belirle - network objesi olup olmadığına bakmaksızın
    character4D.SetDirection(Vector2.down);
}
    private void CreateSpeechBubble()
    {
        
        try
        {
            // Speech bubble canvas oluştur
            GameObject bubbleObj = new GameObject("SpeechBubble");
            bubbleObj.transform.SetParent(transform);
            
            // SpeechBubble pozisyonunu ayarla
            bubbleObj.transform.localPosition = new Vector3(0, 0, 0);
            
            // Canvas component ekle
            Canvas canvas = bubbleObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            canvas.sortingLayerName = "UI";
            
            // Canvas Scaler ekle
            CanvasScaler scaler = bubbleObj.AddComponent<CanvasScaler>();
            scaler.scaleFactor = 1f;
            scaler.dynamicPixelsPerUnit = 100f;
            
            // Canvas Group ekle (fade animasyonları için)
            CanvasGroup canvasGroup = bubbleObj.AddComponent<CanvasGroup>();
            
            // Ana panel container
            GameObject containerObj = new GameObject("BubbleContainer");
            containerObj.transform.SetParent(bubbleObj.transform, false);
            RectTransform containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(140, 110); // Genişlik ve yükseklik ayarlandı
            containerRect.anchoredPosition = new Vector2(0, 120);
            
            // Bubble background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(containerObj.transform, false);
            
            // X Scale'i 2 yapmak için transform'u ayarla
            bgObj.transform.localScale = new Vector3(2f, 1f, 1f);
            
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.9f);
            
            // Preserve Aspect seçeneğini aktif et
            bgImage.preserveAspect = true;
            
            // Konuşma balonu sprite'ını ayarla
            Sprite bubbleSprite = Resources.Load<Sprite>("UI/SpeechBubble");
            if (bubbleSprite == null)
            {
                bgImage.sprite = Resources.Load<Sprite>("UI/Panel"); // Varsayılan panel sprite
                
                if (bgImage.sprite == null)
                {
                    // Basit beyaz kare arka plan
                    Texture2D texture = new Texture2D(1, 1);
                    texture.SetPixel(0, 0, Color.white);
                    texture.Apply();
                    bgImage.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
                }
            }
            else
            {
                bgImage.sprite = bubbleSprite;
            }
            
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            // İç panel (iç kenar boşluğu için)
            GameObject innerPanel = new GameObject("InnerPanel");
            innerPanel.transform.SetParent(bgObj.transform, false);
            
            RectTransform innerRect = innerPanel.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(15, 15);
            innerRect.offsetMax = new Vector2(-15, -15);
            
            // NPC adı etiketi
            GameObject nameObj = new GameObject("NPCName");
            nameObj.transform.SetParent(innerPanel.transform, false);
            
            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = npcName;
            nameText.fontSize = 16;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = new Color(0.361f, 0.518f, 0.643f); // 5C84A4 renk kodu
            
            // Inspector'dan atanan fontu kullan
            if (customFont != null)
            {
                nameText.font = customFont;
            }
            else
            {
            }
            
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 1);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.sizeDelta = new Vector2(0, 20);
            nameRect.anchoredPosition = new Vector2(0, 20.3f);
            
            // Konuşma metni
            GameObject textObj = new GameObject("DialogText");
            textObj.transform.SetParent(innerPanel.transform, false);
            
            speechText = textObj.AddComponent<TextMeshProUGUI>();
            speechText.fontSize = 14;
            speechText.alignment = TextAlignmentOptions.Center;
            speechText.color = new Color(0.1f, 0.1f, 0.1f);
            speechText.textWrappingMode = TextWrappingModes.Normal;
            
            // Auto Size ayarları
            speechText.enableAutoSizing = true;
            speechText.fontSizeMin = 6f;
            
            // Inspector'dan atanan fontu kullan
            if (customFont != null)
            {
                speechText.font = customFont;
            }
            
            // İlginç bir efekt ekle: Renk geçişi
            speechText.enableVertexGradient = true;
            speechText.colorGradient = new VertexGradient(
                new Color(0.1f, 0.1f, 0.1f),  // Üst sol
                new Color(0.3f, 0.3f, 0.3f),  // Üst sağ
                new Color(0.1f, 0.1f, 0.1f),  // Alt sol
                new Color(0.3f, 0.3f, 0.3f)   // Alt sağ
            );
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(9.128498f, 0);
            textRect.offsetMax = new Vector2(-10.3445f, 0);
            
            // Typing effect'i başlat
            typingEffect.Initialize(speechText, this);
            
            // Ölçeği ayarla
            bubbleObj.transform.localScale = new Vector3(0.02f, 0.02f, 0f);
            
            // İlk başta gizli olsun
            speechBubble = bubbleObj;
            speechBubble.SetActive(false);
            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WanderingNPC] Konuşma balonu oluşturma hatası: {e.Message}\n{e.StackTrace}");
        }
    }
// WanderingNPC sınıfı içine eklenecek TypingEffect sınıfı
[System.Serializable]
private class TypingEffect
{
    private TextMeshProUGUI textComponent;
    private string fullText;
    private float typeSpeed = 0.03f;
    private Coroutine typingCoroutine;
    private MonoBehaviour monoBehaviour;
    
    public void Initialize(TextMeshProUGUI textComponent, MonoBehaviour owner)
    {
        this.textComponent = textComponent;
        this.monoBehaviour = owner;
    }
    
    public void StartTyping(string text)
    {
        if (textComponent == null || monoBehaviour == null) return;
        
        fullText = text;
        
        if (typingCoroutine != null)
            monoBehaviour.StopCoroutine(typingCoroutine);
            
        typingCoroutine = monoBehaviour.StartCoroutine(TypeText());
    }
    
    private IEnumerator TypeText()
    {
        textComponent.text = "";
        
        for (int i = 0; i < fullText.Length; i++)
        {
            textComponent.text += fullText[i];
            
            // Noktalama işaretlerinde biraz daha bekle
            if (fullText[i] == '.' || fullText[i] == ',' || fullText[i] == '!' || fullText[i] == '?')
            {
                yield return new WaitForSeconds(typeSpeed * 4);
            }
            else
            {
                yield return new WaitForSeconds(typeSpeed);
            }
        }
    }
}

public void SetWanderBounds(Vector2 min, Vector2 max)
{
    boundaryMin = min;
    boundaryMax = max;
    
    // Başlangıç konumu sınırlar içinde olsun
    startPosition.x = Mathf.Clamp(startPosition.x, boundaryMin.x, boundaryMax.x);
    startPosition.y = Mathf.Clamp(startPosition.y, boundaryMin.y, boundaryMax.y);
    
    // Yeni bir hedef pozisyon belirle
    SetNewTargetPosition();
}

[Rpc(RpcSources.All, RpcTargets.All)]
public override void SetWanderBoundsRPC(Vector2 min, Vector2 max)
{
    SetWanderBounds(min, max);
}

private void DisplaySpeechBubble(string text)
{
    if (speechBubble == null || speechText == null)
        return;
    
    // Önce balonu aktif et
    speechBubble.SetActive(true);
    
    // TypingEffect kullanarak metni yazdır
    typingEffect.StartTyping(text);
    
    // Açılma animasyonunu başlat
    StartCoroutine(AnimateSpeechBubbleOpen());
    
    // Otomatik kapanma için zamanlayıcı
    CancelInvoke("CloseSpeechBubble");
    Invoke("CloseSpeechBubble", speechBubbleTime);
}
private void CloseSpeechBubble()
{
    if (speechBubble != null && speechBubble.activeInHierarchy)
    {
        StartCoroutine(AnimateSpeechBubbleClose());
    }
}
private IEnumerator AnimateSpeechBubbleOpen()
{
    // Başlangıç ve hedef ölçekler
    Vector3 startScale = new Vector3(0.01f, 0.01f, 0f); // Başlangıçta küçük
    Vector3 targetScale = new Vector3(0.02f, 0.02f, 0f); // Hedef ölçek
    
    // Canvas group
    CanvasGroup canvasGroup = speechBubble.GetComponent<CanvasGroup>();
    if (canvasGroup == null)
    {
        canvasGroup = speechBubble.AddComponent<CanvasGroup>();
    }
    
    // Başlangıç değerleri
    canvasGroup.alpha = 0f;
    speechBubble.transform.localScale = startScale;
    
    // Fade-in ve scale animasyonu
    float duration = 0.3f;
    float elapsed = 0f;
    
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        float smoothT = Mathf.SmoothStep(0, 1, t); // Smooth easing
        
        canvasGroup.alpha = Mathf.Lerp(0f, 1f, smoothT);
        speechBubble.transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
        
        yield return null;
    }
    
    // Final değerleri
    canvasGroup.alpha = 1f;
    speechBubble.transform.localScale = targetScale;
}
private IEnumerator AnimateSpeechBubbleClose()
{
    if (speechBubble == null) yield break;
    
    // Canvas group
    CanvasGroup canvasGroup = speechBubble.GetComponent<CanvasGroup>();
    if (canvasGroup == null)
    {
        canvasGroup = speechBubble.AddComponent<CanvasGroup>();
    }
    
    // Başlangıç değerleri
    float startAlpha = canvasGroup.alpha;
    Vector3 startScale = speechBubble.transform.localScale;
    Vector3 targetScale = new Vector3(0.01f, 0.01f, 0f); // Küçülerek kaybolacak
    
    // Fade-out ve scale animasyonu
    float duration = 0.3f;
    float elapsed = 0f;
    
    while (elapsed < duration && speechBubble != null)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        float smoothT = Mathf.SmoothStep(0, 1, t); // Smooth easing
        
        canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, smoothT);
        speechBubble.transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
        
        yield return null;
    }
    
    // Animasyon bittikten sonra deaktif et
    if (speechBubble != null)
    {
        speechBubble.SetActive(false);
    }
    
    if (WanderingNPC.activeDialogueCount > 0)
    {
        WanderingNPC.activeDialogueCount--;
    }
}
public override void OpenInteractionPanel()
{
    // Önce questgiver kontrolü yap (bu baseNPC'de yapılacak)
    base.OpenInteractionPanel();
    
    // WanderingNPC'nin özel işlevleri yok, sadece konuşma balonları gösteriyor
    // Bu nedenle başka bir şey yapmaya gerek yok
}

protected override void OnInteractionButtonClicked()
{
    // Bu NPC türünde etkileşim butonu işlevi devre dışı
}
protected override void CheckPlayerDistance()
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    bool wasInRange = isPlayerInRange;
    isPlayerInRange = false;

    foreach (GameObject player in players)
    {
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj != null && networkObj.HasInputAuthority)
        {
            localPlayerObject = networkObj;
            float distance = Vector2.Distance(transform.position, player.transform.position);
            isPlayerInRange = distance <= interactionRange;
            break;
        }
    }
    
    // CombatInitializer'a bildirim YAPMA - WanderingNPC kendi sistemini kullanıyor
    // BaseNPC'deki CombatInitializer.SetNearbyNPC(this) çağrısını kaldır
}
    public override void FixedUpdateNetwork()
    {
        // SERVER AUTHORITY KONTROLÜ
        if (!Object.HasStateAuthority)
        {
            return;
        }

        // Timer sistemi
        if (Time.time >= NetworkNextStateChangeTime)
        {
            if (NetworkIsWandering)
            {
                // Wandering bitti, hareket mesafesini kontrol et
                CheckMovementAndStop();
            }
            else
            {
                // Yeni wandering başlat
                StartNewWandering();
            }
        }

        // Hareket mantığı
        if (NetworkIsWandering)
        {
            MoveToTargetPosition();
        }
    }
    private void CheckMovementAndStop()
    {
        Vector2 currentPos = transform.position;
        float actualMovement = Vector2.Distance(wanderingStartPosition, currentPos);

        // Çok az hareket ettiyse stuck olmuş demektir
        if (actualMovement < minMovementDistance)
        {

            SetNewTargetPositionAfterStuck();
        }


        // Wandering'i durdur
        NetworkIsWandering = false;
        StopMovement();
        UpdateAnimationState(Vector2.zero);
        NetworkNextStateChangeTime = Time.time + Random.Range(minIdleTime, maxIdleTime);
    }
private void SetNewTargetPositionAfterStuck()
{
    Vector2 currentPos = transform.position;
    Vector2 stuckDirection = (NetworkTargetPosition - currentPos).normalized;
    
    // Stuck olduğu yönün tersine git
    Vector2 oppositeDirection = -stuckDirection;
    
    // Eğer tam ters uygun değilse, 90 derece sağ veya sol dene
    Vector2 newDirection = oppositeDirection;
    Vector2 testTarget = currentPos + newDirection * Random.Range(2f, wanderRadius);
    
    // Sınır kontrolü, eğer ters yön sınır dışına çıkarsa alternatif bul
    if (testTarget.x < boundaryMin.x || testTarget.x > boundaryMax.x ||
        testTarget.y < boundaryMin.y || testTarget.y > boundaryMax.y)
    {
        // 90 derece sağa çevir
        newDirection = new Vector2(-stuckDirection.y, stuckDirection.x);
        testTarget = currentPos + newDirection * Random.Range(2f, wanderRadius);
        
        // Hala sınır dışındaysa 90 derece sola çevir
        if (testTarget.x < boundaryMin.x || testTarget.x > boundaryMax.x ||
            testTarget.y < boundaryMin.y || testTarget.y > boundaryMax.y)
        {
            newDirection = new Vector2(stuckDirection.y, -stuckDirection.x);
        }
    }
    
    Vector2 newTargetPosition = currentPos + newDirection * Random.Range(2f, wanderRadius);
    
    newTargetPosition.x = Mathf.Clamp(newTargetPosition.x, boundaryMin.x, boundaryMax.x);
    newTargetPosition.y = Mathf.Clamp(newTargetPosition.y, boundaryMin.y, boundaryMax.y);
    
    // HEMEN NETWORK TARGET'I GÜNCELLE
    NetworkTargetPosition = newTargetPosition;
    targetPosition = newTargetPosition;
    
    if (Object.HasStateAuthority)
    {
        SyncWanderingStateRPC(NetworkIsWandering, NetworkTargetPosition, NetworkNextStateChangeTime);
    }
    
}
private void StartNewWandering()
{
    NetworkIsWandering = true;
    wanderingStartPosition = transform.position; // Başlangıç pozisyonunu kaydet
    
    SetNewTargetPosition();
    NetworkNextStateChangeTime = Time.time + Random.Range(minWanderTime, maxWanderTime);
}

    public override void Spawned()
    {
        base.Spawned();


        // Network spawn olduğunda timer sistemi başlat
        if (Object.HasStateAuthority)
        {
            EnsureCharacterInitialized();

            // ŞİMDİ NETWORK PROPERTY'LERİ SET ET
            NetworkIsWandering = false;
            NetworkNextStateChangeTime = Time.time + 1f;
            NetworkTargetPosition = targetPosition; // Awake'te hesaplanan değeri kullan


            // Karakter görünümünü senkronize et
            if (character4D != null)
            {
                StartCoroutine(DelayedSyncCharacterAppearance());
            }
        }
    }

private IEnumerator DelayedSyncCharacterAppearance()
{
    yield return new WaitForSeconds(1f);
    SyncCharacterAppearance();
}

    // Hareket sınırlarını düzenleme (Editörde görselleştirme)
    private void OnDrawGizmosSelected()
    {
        // Hareket alanını çiz
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3((boundaryMin.x + boundaryMax.x) / 2, (boundaryMin.y + boundaryMax.y) / 2, 0),
            new Vector3(boundaryMax.x - boundaryMin.x, boundaryMax.y - boundaryMin.y, 0)
        );
        
        // Dialog tetikleme mesafesini çiz
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, dialogTriggerDistance);
    }
}