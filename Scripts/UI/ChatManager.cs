// Path: Assets/Game/Scripts/ChatManager.cs

using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;
using DuloGames.UI;

// Ana chat yönetim sınıfı
public class ChatManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
public static ChatManager Instance { get; private set; }

    private Dictionary<string, List<PrivateMessage>> privateMessages = new Dictionary<string, List<PrivateMessage>>();
    private Dictionary<string, GameObject> activeChatBars = new Dictionary<string, GameObject>();

[Header("Chat UI References")]
[SerializeField] private GameObject chatPanel; // Ana chat panel (Chat GameObject)
[SerializeField] private Demo_Chat demoChat; // Yeni chat component referansı
[SerializeField] private GameObject inputContainer; // Eski sistem için hala gerekli (private chat için)
[SerializeField] private InputField chatInput; // Eski input field
[SerializeField] private GameObject chatMessagePrefab; // Eski prefab
[SerializeField] private Transform chatContent; // Eski content
[SerializeField] private Button toggleChatButton;
[SerializeField] private Button sendButton; // Artık kullanılmayacak ama eski sistem için kalsın

        private bool isInputVisible = false;
[Networked] public int LastMessageTick { get; set; }
   
    [Header("Settings")]
    [SerializeField] private int maxMessages = 50;
    [SerializeField] private int maxPrivateMessagesPerUser = 100; // Her kullanıcı için max mesaj sayısı

    [Header("Floating Message Settings")]
    [SerializeField] private Vector2 floatingMessageSize = new Vector2(500, 40);
    [SerializeField] private float floatingMessageYOffset = 0f;
    [SerializeField] private float messageDisplayTime = 10f;
    [SerializeField] private float fadeOutDuration = 2f;
    [SerializeField] private Color floatingMessageBgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    [SerializeField] private Color floatingMessageTextColor = Color.white;
    [SerializeField] private int floatingMessageFontSize = 40;
    [SerializeField] private float floatingMessageSpacing = 45f;

    [Header("Private Chat UI")]
[SerializeField] private GameObject privateChatBarPrefab; // Oluşturduğumuz prefab
[SerializeField] private Transform privateChatContainer;  // Özel chat barlarının parent'ı
[SerializeField] private GameObject messageTextPrefab;    // Tek bir mesaj için prefab

private const string PARTY_CHAT = "Party";
public void SendPrivateMessage(string targetPlayer, string message)
{
    // Hedef oyuncu kontrolü
    PlayerRef target = FindPlayerByNickname(targetPlayer);
    if (target == PlayerRef.None)
    {
        ShowNotification($"Oyuncu bulunamadı: {targetPlayer}");
        return;
    }

    RPC_ReceivePrivateMessage(target, GetLocalPlayerName(), message);
    
    // Gönderen kişinin de mesajı görmesi için
    AddPrivateMessage(targetPlayer, GetLocalPlayerName(), message);
    CreateOrUpdateChatBar(targetPlayer);
}
        public class PrivateMessage
    {
        public string SenderName { get; set; }
        public string Content { get; set; }
        public long Timestamp { get; set; }
    }

// Sınıfa eklenecek public metodlar
public bool IsChatOpen()
{
    return chatPanel != null && chatPanel.activeSelf;
}

public void ForceCloseChat()
{
    if (chatPanel != null)
    {
        chatPanel.SetActive(false);
    }
    
    if (inputContainer != null)
    {
        inputContainer.SetActive(false);
    }
    
    isInputVisible = false;
}

public void ForceOpenChat()
{
    if (chatPanel != null)
    {
        chatPanel.SetActive(true);
    }
    
    if (inputContainer != null)
    {
        inputContainer.SetActive(true);
    }
    
    // Demo chat input field'ını da kontrol et
    if (demoChat != null)
    {
        InputField demoInputField = demoChat.GetComponentInChildren<InputField>();
        if (demoInputField != null)
        {
            demoInputField.gameObject.SetActive(true);
            demoInputField.interactable = true;
        }
    }
    
    isInputVisible = true;
}
        private void CreateFloatingMessage(string senderName, string message)
    {
        try
        {
            Canvas mainCanvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
            if (mainCanvas == null) return;

            GameObject notificationObj = new GameObject("FloatingMessage");
            notificationObj.transform.SetParent(mainCanvas.transform, false);

            Image bgImage = notificationObj.AddComponent<Image>();
            bgImage.color = floatingMessageBgColor;

            RectTransform rectTransform = notificationObj.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.sizeDelta = floatingMessageSize;
            rectTransform.anchoredPosition = new Vector2(0, -floatingMessageYOffset);

            GameObject textObj = new GameObject("NotificationText");
            textObj.transform.SetParent(notificationObj.transform, false);

            TextMeshProUGUI notificationText = textObj.AddComponent<TextMeshProUGUI>();
            notificationText.text = $"[{senderName}]: {message}";
            notificationText.fontSize = floatingMessageFontSize;
            notificationText.alignment = TextAlignmentOptions.Center;
            notificationText.color = floatingMessageTextColor;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);

            StartCoroutine(FadeOutNotification(notificationObj));
            ArrangeFloatingMessages();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChatManager] Error showing notification: {e.Message}");
        }
    }
        private IEnumerator FadeOutNotification(GameObject notification)
    {
        yield return new WaitForSeconds(messageDisplayTime - fadeOutDuration);

        CanvasGroup canvasGroup = notification.AddComponent<CanvasGroup>();
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - (elapsed / fadeOutDuration);
            yield return null;
        }

        Destroy(notification);
    }


    private void ArrangeFloatingMessages()
    {
        var messages = transform.GetComponentsInChildren<RectTransform>()
            .Where(r => r.gameObject.name == "FloatingMessage")
            .OrderByDescending(r => r.position.y)
            .ToList();

        for (int i = 0; i < messages.Count; i++)
        {
            Vector3 newPos = messages[i].anchoredPosition;
            newPos.y = -floatingMessageYOffset - (i * floatingMessageSpacing);
            messages[i].anchoredPosition = newPos;
        }
    }
    
    private float lastMessageTime;
    private Dictionary<string, List<ChatMessage>> messageHistory = new Dictionary<string, List<ChatMessage>>();
        [Header("Chat Message Colors")]
    private readonly Color goldMessageColor = new Color(1f, 0.84f, 0f);      // Altın sarısı
    private readonly Color itemPickupColor = new Color(0.56f, 1f, 0.56f);    // Açık yeşil
    private readonly Color errorMessageColor = new Color(1f, 0.4f, 0.4f);    // Açık kırmızı
    private readonly Color partyMessageColor = new Color(0.5f, 1f, 0.5f);    // Party mesajı rengi
    private readonly string GENERAL_CHAT = "General";
    
    private class ChatMessage
    {
        public string SenderID { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }
        public string Channel { get; set; }
        public long Timestamp { get; set; }
    }
private void Awake()
{
    
    // Sadece Instance kontrolü yap, initialization'ı Spawned()'a taşı
    if (Instance != null && Instance != this)
    {
        Destroy(gameObject);
        return;
    }
}
    public override void Spawned()
    {

        if (Instance == null)
        {
            Instance = this;
            privateMessages = new Dictionary<string, List<PrivateMessage>>();
            activeChatBars = new Dictionary<string, GameObject>();
            InitializeChatUI();
        }
    }

private IEnumerator ForceActivateInputField()
{
    yield return new WaitForEndOfFrame();
    yield return new WaitForSeconds(0.5f); // Biraz daha bekle

    if (demoChat != null)
    {
        
        // Tüm çocukları aktif et
        Transform[] allChildren = demoChat.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            if (child.name.Contains("Input") || child.name.Contains("Field"))
            {
                child.gameObject.SetActive(true);
            }
        }
        
        InputField inputField = demoChat.GetComponentInChildren<InputField>(true);
        if (inputField != null)
        {
            inputField.gameObject.SetActive(true);
            inputField.interactable = true;
        }
        else
        {
            Debug.LogError("[ChatManager] Input field still not found in ForceActivateInputField!");
        }
    }
}
    private void Start()
    {

        // Demo chat input field'ını garanti aktif et
        if (demoChat != null)
        {
            InputField inputField = demoChat.GetComponentInChildren<InputField>(true);
            if (inputField != null)
            {
                inputField.gameObject.SetActive(true);
                inputField.interactable = true;
            }
        }

        // Loading step'i tamamla
        //if (LoadingManager.Instance != null)
        //{
        //    LoadingManager.Instance.CompleteStep("ChatManager");
        //}
        //else
        //{
        //     Debug.LogError("[ChatManager] LoadingManager Instance null!");
        // }

    }
public void ShowCraftMaterialPickupMessage(string itemName, int amount)
{
    string message = $"{itemName} x{amount} materyalini topladınız!";
    
    if (demoChat != null)
    {
        string timeStr = DateTime.Now.ToString("HH:mm");
        string formattedMessage = $"[{timeStr}] <color=#87CEEB>{message}</color>";
        demoChat.ReceiveChatMessage(1, formattedMessage);
    }
}
public void ShowCraftMaterialInfoMessage(string itemName, int amount)
{
//
}
public void ShowCraftInventoryFullMessage()
{
//
}
public void ShowPartyAcceptedMessage(string playerName)
    {
        string message = $"{playerName} parti davetinizi kabul etti!";

        if (demoChat != null)
        {
            string timeStr = DateTime.Now.ToString("HH:mm");
            string formattedMessage = $"[{timeStr}] <color=#90EE90>{message}</color>";
            demoChat.ReceiveChatMessage(1, formattedMessage);
        }
    }
// ChatManager.cs'e eklenecek metodlar (eğer ChatManager varsa)

public void ShowCraftSuccessMessage(string message)
{
    //
}

public void ShowCraftFailedMessage(string message)
{
    //
}
public void ShowPartyDeclinedMessage(string playerName)
{
    string message = $"{playerName} parti davetinizi reddetti.";
    
    if (demoChat != null)
    {
        string timeStr = DateTime.Now.ToString("HH:mm");
        string formattedMessage = $"[{timeStr}] <color=#FF6666>{message}</color>";
        demoChat.ReceiveChatMessage(1, formattedMessage);
    }
}
public void ShowGoldPickupMessage(int amount)
{
    string message = $"{amount} gold topladınız!";
    
    if (demoChat != null)
    {
        string timeStr = DateTime.Now.ToString("HH:mm");
        string formattedMessage = $"[{timeStr}] <color=#FFD700>{message}</color>";
        demoChat.ReceiveChatMessage(1, formattedMessage);
    }
}
private void InitializeChatUI()
{
    
    // ÖNCELİKLE PANEL AYARLARINI YAP
    if (chatPanel != null)
    {
        chatPanel.SetActive(true);
    }
        
    // Input container'ı aktif et (deaktif etme!)
    if (inputContainer != null)
    {
        inputContainer.SetActive(true); // False değil True yap
    }
    
    
    if (chatInput != null)
    {
        
        // Input field'ı garanti aktif et
        chatInput.gameObject.SetActive(true);
        chatInput.interactable = true;
        
        
        chatInput.onSubmit.RemoveAllListeners();
        chatInput.onSubmit.AddListener(delegate { 
            SendChatMessage(); 
        });
        
        chatInput.onEndEdit.RemoveAllListeners();
        chatInput.onEndEdit.AddListener(delegate(string text) { 
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SendChatMessage(); 
            }
        });
        
    }
    else
    {
        Debug.LogError("[ChatManager] chatInput is NULL!");
    }
    
    // BUTTON EVENT'İNİ EKLE
    if (sendButton != null)
    {
        sendButton.onClick.RemoveAllListeners();
        sendButton.onClick.AddListener(() => {
            SendChatMessage();
        });
    }
    else
    {
        Debug.LogError("[ChatManager] sendButton is NULL!");
    }
        
    // Toggle button
    if (toggleChatButton != null)
        toggleChatButton.onClick.AddListener(ToggleChat);
    
}
private void Update()
{
    // Test için - manuel Enter kontrolü
    if (Input.GetKeyDown(KeyCode.Return) && chatInput != null && chatInput.isFocused)
    {
        SendChatMessage();
    }
}
private void OnNewChatSend(int tabId, string message)
{
    // Runner kontrolünü değiştir
    if (!Object.HasInputAuthority) return;

    // Spam korumasını geçici olarak kaldır (test için)
    /*if (Runner.Tick - LastMessageTick < (int)(spamProtectionInterval * Runner.TickRate))
    {
        ShowNotification("Lütfen mesaj göndermek için bekleyin.");
        return;
    }*/

    if (string.IsNullOrEmpty(message.Trim())) return;

    // Debug log ekle

    // Sadece genel chat mesajı gönder (tabId = 1 genel chat)
    if (tabId == 1)
    {
        RPC_ReceiveGeneralMessage(GetLocalPlayerName(), message);
        LastMessageTick = Runner.Tick;
    }
}
[Rpc(RpcSources.All, RpcTargets.All)] // InputAuthority değil All yap
private void RPC_ReceiveGeneralMessage(string senderName, string message)
{
    
    // Mesajı geçmişe ekle
    AddMessageToHistory(GENERAL_CHAT, new ChatMessage
    {
        SenderName = senderName,
        Message = message,
        Channel = GENERAL_CHAT,
        Timestamp = GetCurrentTimestamp()
    });

    // Yeni UI'da göster
    if (demoChat != null)
    {
        string timeStr = DateTime.Now.ToString("HH:mm");
        string formattedMessage = $"[{timeStr}] <b><color=white>{senderName}</color></b> <color=#59524bff>said:</color> {message}";
        demoChat.ReceiveChatMessage(1, formattedMessage);
    }

    // Floating mesaj sistemini koru
    CreateFloatingMessage(senderName, message);
    
    // Mesaj limitini kontrol et
    CleanupOldMessages();
}

[Rpc(RpcSources.All, RpcTargets.All)]
private void RPC_ReceivePrivateMessage(PlayerRef target, string senderName, string message)
{
    if (target == Runner.LocalPlayer)
    {
        AddPrivateMessage(senderName, senderName, message);
        CreateOrUpdateChatBar(senderName);
    }
}

    // EKLENEN FUSION CALLBACK'LER:
public void PlayerJoined(PlayerRef player)
{
    NetworkObject playerNetObj = Runner.GetPlayerObject(player);
    if (playerNetObj != null)
    {
        PlayerStats playerStats = playerNetObj.gameObject.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            CreateMessageUI("System", $"{playerStats.GetPlayerDisplayName()} odaya katıldı.", GENERAL_CHAT);
        }
    }
}

public void PlayerLeft(PlayerRef player)
{
    NetworkObject playerNetObj = Runner.GetPlayerObject(player);
    if (playerNetObj != null)
    {
        PlayerStats playerStats = playerNetObj.gameObject.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            CreateMessageUI("System", $"{playerStats.GetPlayerDisplayName()} odadan ayrıldı.", GENERAL_CHAT);
        }
    }
}
private string GetLocalPlayerName()
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj != null && netObj.HasInputAuthority)
        {
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                string playerName = playerStats.GetPlayerDisplayName();
                return playerName;
            }
        }
    }
    
    return "Unknown Player";
}
public void ToggleChat()
{
    // Chat panelinin mevcut durumunu kontrol et
    bool currentState = chatPanel.activeInHierarchy;
    
    // Tersine çevir
    isInputVisible = !currentState;
    
    // Chat panelini toggle et
    if (chatPanel != null)
        chatPanel.SetActive(isInputVisible);
    
    // Demo chat input field'ını da kontrol et
    if (demoChat != null && isInputVisible)
    {
        InputField demoInputField = demoChat.GetComponentInChildren<InputField>();
        if (demoInputField != null)
        {
            demoInputField.gameObject.SetActive(true);
            demoInputField.interactable = true;
        }
    }
        
    // Private chat input container ayrı kontrol et
    if (inputContainer != null)
        inputContainer.SetActive(isInputVisible);
}

public void SendChatMessage()
{
    
    string message = chatInput.text.Trim();
    
    if (string.IsNullOrEmpty(message)) return;

    // Özel mesaj kontrolü
    if (message.StartsWith("/w "))
    {
        HandlePrivateMessage(message);
        chatInput.text = "";
        return;
    }

    // Authority kontrolünü kaldır - herkes mesaj gönderebilir
    RPC_ReceiveGeneralMessage(GetLocalPlayerName(), message);

    chatInput.text = "";
}
private void HandlePrivateMessage(string message)
{
    string trimmedMessage = message.Substring(3).Trim();
    int firstSpaceIndex = trimmedMessage.IndexOf(' ');

    if (firstSpaceIndex == -1)
    {
        ShowNotification("Kullanım: /w oyuncuAdı mesaj");
        return;
    }

    string targetPlayer = trimmedMessage.Substring(0, firstSpaceIndex);
    string messageContent = trimmedMessage.Substring(firstSpaceIndex + 1);

    // Hedef oyuncu kontrolü
    PlayerRef target = FindPlayerByNickname(targetPlayer);
    if (target == PlayerRef.None)
    {
        ShowNotification($"Oyuncu bulunamadı: {targetPlayer}");
        return;
    }

    RPC_ReceivePrivateMessage(target, GetLocalPlayerName(), messageContent);

    // Gönderen kişinin de mesajı görmesi için
    AddPrivateMessage(targetPlayer, GetLocalPlayerName(), messageContent);
    CreateOrUpdateChatBar(targetPlayer);
}


private void AddPrivateMessage(string otherPlayer, string sender, string content)
{


    if (!privateMessages.ContainsKey(otherPlayer))
    {
        privateMessages[otherPlayer] = new List<PrivateMessage>();
    }

    privateMessages[otherPlayer].Add(new PrivateMessage
    {
        SenderName = sender,
        Content = content,
        Timestamp = GetCurrentTimestamp()
    });

    // MEMORY LEAK FIX: Mesaj limitini kontrol et
    if (privateMessages[otherPlayer].Count > maxPrivateMessagesPerUser)
    {
        // En eski mesajları sil (ilk yarısını temizle)
        int removeCount = privateMessages[otherPlayer].Count - maxPrivateMessagesPerUser;
        privateMessages[otherPlayer].RemoveRange(0, removeCount);
        Debug.Log($"[ChatManager] Trimmed {removeCount} old messages from {otherPlayer}");
    }
}


private GameObject CreateOrUpdateChatBar(string otherPlayer)
{
    if (!activeChatBars.ContainsKey(otherPlayer))
    {
        GameObject chatBar = Instantiate(privateChatBarPrefab, privateChatContainer);
        PrivateChatBar barComponent = chatBar.GetComponent<PrivateChatBar>();
        barComponent.Initialize(otherPlayer, this);
        activeChatBars[otherPlayer] = chatBar;
    }

    GameObject existingBar = activeChatBars[otherPlayer];
    PrivateChatBar bar = existingBar.GetComponent<PrivateChatBar>();
    bar.UpdateMessages(privateMessages[otherPlayer]);
    
    return existingBar;
}

private PlayerRef FindPlayerByNickname(string nickname)
{
    foreach (var player in Runner.ActivePlayers)
    {
        NetworkObject playerNetObj = Runner.GetPlayerObject(player);
        if (playerNetObj != null)
        {
            PlayerStats playerStats = playerNetObj.gameObject.GetComponent<PlayerStats>();
            if (playerStats != null && playerStats.GetPlayerDisplayName() == nickname)
                return player;
        }
    }
    return PlayerRef.None;
}

    public void ClosePrivateChat(string otherPlayer)
    {
        if (activeChatBars.TryGetValue(otherPlayer, out GameObject chatBar))
        {
            Destroy(chatBar);
            activeChatBars.Remove(otherPlayer);
            privateMessages.Remove(otherPlayer);
        }
    }


public void ShowInventoryFullMessage()
{
    string message = "Envanter dolu!";
    
    if (demoChat != null)
    {
        string timeStr = DateTime.Now.ToString("HH:mm");
        string formattedMessage = $"[{timeStr}] <color=#FF6666>{message}</color>";
        demoChat.ReceiveChatMessage(1, formattedMessage);
    }
}
    private void CreateMessageUI(string senderName, string message, string channel)
    {
        if (chatMessagePrefab == null || chatContent == null) return;

        GameObject msgObj = Instantiate(chatMessagePrefab, chatContent);
        TextMeshProUGUI msgText = msgObj.GetComponent<TextMeshProUGUI>();
        
        string timeStr = DateTime.Now.ToString("HH:mm");
        string prefix = "";
        Color messageColor = Color.white;

        // Kanal renklerini ayarla
        switch(channel)
        {
            case "Party":
                prefix = "[Parti]";
                messageColor = partyMessageColor;
                break;
            default:
                prefix = channel == GENERAL_CHAT ? "" : $"[{channel}]";
                break;
        }
        
        msgText.text = $"[{timeStr}] {message}";
        msgText.color = messageColor;
    }
public void ShowItemPickupMessage(string itemName)
{
    string message = $"{itemName} eşyasını topladınız!";
    
    if (demoChat != null)
    {
        string timeStr = DateTime.Now.ToString("HH:mm");
        string formattedMessage = $"[{timeStr}] <color=#90EE90>{message}</color>";
        demoChat.ReceiveChatMessage(1, formattedMessage);
    }
}
    private void AddMessageToHistory(string channel, ChatMessage message)
    {
        if (!messageHistory.ContainsKey(channel))
        {
            messageHistory[channel] = new List<ChatMessage>();
        }

        messageHistory[channel].Add(message);
    }

    private void CleanupOldMessages()
    {
        foreach (var channel in messageHistory.Keys)
        {
            if (messageHistory[channel].Count > maxMessages)
            {
                messageHistory[channel].RemoveAt(0);
            }
        }
    }


    private long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private void ShowNotification(string message)
    {
        // TODO: Bildirim sistemi implementasyonu

    }
    public void ReceivePartyMessage(string senderName, string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        AddMessageToHistory(PARTY_CHAT, new ChatMessage
        {
            SenderName = senderName,
            Message = message,
            Channel = PARTY_CHAT,
            Timestamp = GetCurrentTimestamp()
        });

        // Parti mesajını UI'da göster
        CreateMessageUI(senderName, message, PARTY_CHAT);
    }
public void ShowQuestStartMessage(string questName)
{
    string message = $"'{questName}' görevi alındı!";
    
    if (demoChat != null)
    {
        string timeStr = DateTime.Now.ToString("HH:mm");
        string formattedMessage = $"[{timeStr}] <color=#4FC3F7>{message}</color>";
        demoChat.ReceiveChatMessage(1, formattedMessage);
    }
}
// ShowCraftFailedMessage() metodundan sonra ekle

public void ShowUpgradeMaterialNeededMessage(string message)
{
    if (demoChat != null)
    {
        string timeStr = DateTime.Now.ToString("HH:mm");
        string formattedMessage = $"[{timeStr}] <color=#FF6666>[Blacksmith] {message}</color>";
        demoChat.ReceiveChatMessage(1, formattedMessage);
    }
}
public void ShowQuestCompleteMessage(string questName)
{
    string message = $"'{questName}' görevi tamamlandı!";
    
    if (demoChat != null)
    {
        string timeStr = DateTime.Now.ToString("HH:mm");
        string formattedMessage = $"[{timeStr}] <color=#66BB6A>{message}</color>";
        demoChat.ReceiveChatMessage(1, formattedMessage);
    }
}

public void ShowQuestRewardMessage(QuestReward rewards)
{
    List<string> rewardMessages = new List<string>();
    
    if (rewards.xpReward > 0)
        rewardMessages.Add($"{rewards.xpReward} XP");
    
    if (rewards.coinReward > 0)
        rewardMessages.Add($"{rewards.coinReward} Gold");
    
    if (rewards.potionReward > 0)
        rewardMessages.Add($"{rewards.potionReward} Potion");
    
    if (rewards.itemRewards != null && rewards.itemRewards.Count > 0)
    {
        foreach (string itemId in rewards.itemRewards)
        {
            ItemData item = ItemDatabase.Instance.GetItemById(itemId);
            if (item != null)
                rewardMessages.Add($"1x {item.itemName}");
        }
    }
    
    if (rewardMessages.Count > 0)
    {
        string message = "Ödüller: " + string.Join(", ", rewardMessages);
        
        if (demoChat != null)
        {
            string timeStr = DateTime.Now.ToString("HH:mm");
            string formattedMessage = $"[{timeStr}] <color=#FFB74D>{message}</color>";
            demoChat.ReceiveChatMessage(1, formattedMessage);
        }
    }
}
}