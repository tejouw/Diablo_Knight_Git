using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QuestDevTools : MonoBehaviour
{
    [Header("Test Quest")]
    [Tooltip("Test etmek istediğin quest'i buraya ata")]
    [SerializeField] private QuestData targetQuest;
    
    [Header("UI References")]
    [SerializeField] private Button testQuestButton;
    
    [Header("Settings")]
    [SerializeField] private Vector2 buttonPosition = new Vector2(100, -100);
    [SerializeField] private Vector2 buttonSize = new Vector2(200, 60);
    
    private void Start()
    {
        if (testQuestButton != null)
        {
            testQuestButton.onClick.AddListener(OnTestQuestButtonClicked);
        }
        else
        {
            CreateDevButton();
        }
    }
    
    private void CreateDevButton()
    {
        // Canvas bul veya oluştur
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DevToolsCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        
        // Button oluştur
        GameObject buttonObj = new GameObject("QuestTestButton");
        buttonObj.transform.SetParent(canvas.transform, false);
        
        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1, 1);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.pivot = new Vector2(1, 1);
        rectTransform.sizeDelta = buttonSize;
        rectTransform.anchoredPosition = buttonPosition;
        
        testQuestButton = buttonObj.AddComponent<Button>();
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);
        
        // Text ekle
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        
        testQuestButton.onClick.AddListener(OnTestQuestButtonClicked);
    }
    
    
    private void OnTestQuestButtonClicked()
    {
        if (targetQuest == null)
        {
            Debug.LogError("[QuestDevTools] Target quest atanmamış!");
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification("Inspector'dan quest ata!");
            }
            return;
        }
        
        if (QuestManager.Instance == null)
        {
            Debug.LogError("[QuestDevTools] QuestManager bulunamadı!");
            return;
        }
        
        StartCoroutine(TestQuestSequence());
    }
    
    private IEnumerator TestQuestSequence()
    {
        // 1. Reset
        QuestManager.Instance.ResetAllQuests();
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("Questler sıfırlanıyor...");
        }
        
        // Reset'in tamamlanmasını bekle
        yield return new WaitForSeconds(0.5f);
        
        // 2. Chain'i tamamla ve quest'i başlat
        QuestManager.Instance.ForceCompleteQuestChain(targetQuest.questId);
        
        // 3. NPC marker'ları güncelle
        yield return new WaitForSeconds(0.2f);
        QuestManager.Instance.UpdateQuestMarkersForNPCs();
        
        // 4. UI güncelle
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification($"Quest Başlatıldı: {targetQuest.questName}");
        }
    }

}