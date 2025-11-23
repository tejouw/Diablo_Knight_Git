// QuestResetButton.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestResetButton : MonoBehaviour
{
    [SerializeField] private Button resetButton;
    [SerializeField] private Vector2 buttonPosition = new Vector2(100, 100);
    [SerializeField] private Vector2 buttonSize = new Vector2(150, 50);
    [SerializeField] private Color buttonColor = new Color(1, 0, 0, 0.8f);
    
    private Canvas targetCanvas;
    
private void Start()
{
    // Eğer Inspector'dan buton atanmışsa, sadece event'ini ayarla
    if (resetButton != null)
    {
        resetButton.onClick.AddListener(ResetAllQuests);
    }
    else
    {
        // Inspector'dan buton atanmamışsa, yeni buton oluştur
        FindCanvas();
        CreateResetButton();
    }
}
    
    private void FindCanvas()
    {
        // Ana oyun UI'ını bul
        GameObject gameUI = GameObject.Find("GameUI");
        if (gameUI != null)
        {
            targetCanvas = gameUI.GetComponent<Canvas>();
            return;
        }
        
        // Ana canvas'ı bul
        targetCanvas = FindFirstObjectByType<Canvas>();
        
        // Hala bulunamadıysa yeni bir canvas oluştur
        if (targetCanvas == null)
        {
            GameObject canvasObj = new GameObject("QuestResetCanvas");
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 100; // En üstte görünsün
            
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }
    }
    
    private void CreateResetButton()
    {
        if (targetCanvas == null) return;
        
        // Buton oluştur
        GameObject buttonObj = new GameObject("QuestResetButton");
        buttonObj.transform.SetParent(targetCanvas.transform, false);
        
        // RectTransform ayarla
        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = buttonSize;
        rectTransform.anchoredPosition = buttonPosition;
        
        // Buton component'i ekle
        resetButton = buttonObj.AddComponent<Button>();
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = buttonColor;
        
        // Buton text'i ekle
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;       

        
        // Click event'ini ekle
        resetButton.onClick.AddListener(ResetAllQuests);
    }
    
    private void ResetAllQuests()
    {
        QuestManager questManager = QuestManager.Instance;
        if (questManager != null)
        {
            questManager.ResetAllQuests();
        }
        else
        {
            Debug.LogError("[QuestResetButton] QuestManager bulunamadı!");
        }
    }
}