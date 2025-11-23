using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class PrivateChatBar : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Transform messageContainer;
    [SerializeField] private GameObject messageTextPrefab;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform messagesPanel;
        [SerializeField] private TMP_InputField replyInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private RectTransform replyPanel;

    private string otherPlayerName;
    private ChatManager chatManager;
    private bool isExpanded = false;

    // Sınıfın ilk kurulumu
    public void Initialize(string playerName, ChatManager manager)
    {
        Debug.Log($"Initializing PrivateChatBar for player: {playerName}");
        otherPlayerName = playerName;
        chatManager = manager;
        playerNameText.text = playerName;

        toggleButton.onClick.AddListener(ToggleMessages);
        closeButton.onClick.AddListener(() => chatManager.ClosePrivateChat(otherPlayerName));
        messagesPanel.gameObject.SetActive(false);
        Debug.Log("PrivateChatBar Initialized.");
                sendButton.onClick.AddListener(SendReply);
        replyInput.onSubmit.AddListener((msg) => SendReply());
        
        // Reply panel'i başlangıçta gizli
        replyPanel.gameObject.SetActive(false);
    }
        private void SendReply()
    {
        if (string.IsNullOrEmpty(replyInput.text)) return;

        // ChatManager üzerinden mesajı gönder
        chatManager.SendPrivateMessage(otherPlayerName, replyInput.text);
        
        // Input field'ı temizle
        replyInput.text = "";
    }

    // Yeni mesajları günceller
    public void UpdateMessages(List<ChatManager.PrivateMessage> messages)
    {
        Debug.Log($"Updating messages for {otherPlayerName}, Total messages: {messages.Count}");

        // Mevcut mesajları temizle
        foreach (Transform child in messageContainer)
        {
            Destroy(child.gameObject);
            Debug.Log("Removed old message from container.");
        }

        // Yeni mesajları ekle
        foreach (var message in messages)
        {
            Debug.Log($"Adding message from {message.SenderName}: {message.Content}");
            GameObject msgObj = Instantiate(messageTextPrefab, messageContainer);
            TextMeshProUGUI msgText = msgObj.GetComponent<TextMeshProUGUI>();
            msgText.text = $"{message.SenderName}: {message.Content}";
        }
    }

    // Mesajlar panelini genişletir ya da daraltır
    private void ToggleMessages()
    {
        isExpanded = !isExpanded;
        messagesPanel.gameObject.SetActive(isExpanded);
        replyPanel.gameObject.SetActive(isExpanded); // Reply panel'i de toggle
    }
        // Gelen mesajda input field'a focus olmak için
    public void FocusInputField()
    {
        if (!isExpanded)
        {
            ToggleMessages();
        }
        replyInput.ActivateInputField();
    }
}
