using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

public class TeleportChannellingUI : MonoBehaviour
{
    private static TeleportChannellingUI _instance;
    public static TeleportChannellingUI Instance => _instance;

    [Header("UI Settings")]
    [SerializeField] private Vector2 panelSize = new Vector2(350, 120);
    [SerializeField] private float panelYOffset = -80f;
    
    [Header("Colors - Fantasy Theme")]
    [SerializeField] private Color panelColor = new Color(0.1f, 0.05f, 0.2f, 0.95f); // Mor-siyah
    [SerializeField] private Color borderColor = new Color(0.4f, 0.2f, 0.6f, 1f); // Mor border
    [SerializeField] private Color sliderFillColor = new Color(0.5f, 0.3f, 1f, 1f); // Parlak mor
    [SerializeField] private Color sliderGlowColor = new Color(0.7f, 0.5f, 1f, 0.8f); // Glow
    [SerializeField] private Color textColor = new Color(1f, 0.9f, 0.7f, 1f); // Altın sarısı
[Header("Scale Settings")]
[SerializeField] private float uiScale = 1f;
    private GameObject uiRoot;
    private CanvasGroup canvasGroup;
    private Slider progressSlider;
    private TextMeshProUGUI progressText;
    private Image sliderFill;
    private Image glowImage;
    
    private Coroutine updateCoroutine;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        CreateUI();
    }

    private void CreateUI()
    {
        // Root Canvas
        uiRoot = new GameObject("TeleportChannellingUI");
        uiRoot.transform.SetParent(transform);
        
        Canvas canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        
        uiRoot.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        uiRoot.AddComponent<GraphicRaycaster>();
        
        canvasGroup = uiRoot.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

// Panel Container
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(uiRoot.transform, false);
        
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = new Vector2(0, panelYOffset);
        
        // EKLE: Initial scale uygula
        panelRect.localScale = Vector3.one * uiScale;

        // Panel Background (arka plan)
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = panelColor;
        panelBg.raycastTarget = false;

        // Border (kenarlık)
        GameObject border = new GameObject("Border");
        border.transform.SetParent(panel.transform, false);
        
        RectTransform borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        
        Outline borderOutline = border.AddComponent<Outline>();
        borderOutline.effectColor = borderColor;
        borderOutline.effectDistance = new Vector2(2, -2);
        
        Image borderImage = border.AddComponent<Image>();
        borderImage.color = Color.clear;
        borderImage.raycastTarget = false;

    GameObject titleObj = new GameObject("Title");
    titleObj.transform.SetParent(panel.transform, false);
    
    RectTransform titleRect = titleObj.AddComponent<RectTransform>();
    titleRect.anchorMin = new Vector2(0, 1);
    titleRect.anchorMax = new Vector2(1, 1);
    titleRect.pivot = new Vector2(0.5f, 1);
    titleRect.sizeDelta = new Vector2(-20, 30);
    titleRect.anchoredPosition = new Vector2(0, -10);
    
    TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
    titleText.text = "Ayaklarının dibinde ince bir iz beliriyor…"; // DEĞİŞTİ
    titleText.fontSize = 18;
    titleText.fontStyle = FontStyles.Bold;
    titleText.alignment = TextAlignmentOptions.Center;
    titleText.color = textColor;
    titleText.raycastTarget = false;

        // Slider Container
        GameObject sliderContainer = new GameObject("SliderContainer");
        sliderContainer.transform.SetParent(panel.transform, false);
        
        RectTransform sliderContainerRect = sliderContainer.AddComponent<RectTransform>();
        sliderContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderContainerRect.pivot = new Vector2(0.5f, 0.5f);
        sliderContainerRect.sizeDelta = new Vector2(300, 30);
        sliderContainerRect.anchoredPosition = new Vector2(0, -10);

        // Glow Effect (arka planda parlama)
        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(sliderContainer.transform, false);
        
        RectTransform glowRect = glowObj.AddComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.sizeDelta = new Vector2(20, 20);
        
        glowImage = glowObj.AddComponent<Image>();
        glowImage.color = sliderGlowColor;
        glowImage.raycastTarget = false;

// Slider
        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(sliderContainer.transform, false);
        
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.sizeDelta = Vector2.zero;
        
        progressSlider = sliderObj.AddComponent<Slider>();
        progressSlider.interactable = false;
        progressSlider.minValue = 0f;
        progressSlider.maxValue = 1f;
        progressSlider.value = 0f;
        progressSlider.direction = Slider.Direction.LeftToRight;
        
        // Slider Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.1f, 0.3f, 0.8f);
        bgImage.raycastTarget = false;

        // Slider Fill Area
        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        
        RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-5, -5);
        fillAreaRect.anchoredPosition = Vector2.zero;

        // Slider Fill
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.zero;
        fillRect.pivot = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
        
        sliderFill = fillObj.AddComponent<Image>();
        sliderFill.color = sliderFillColor;
        sliderFill.raycastTarget = false;

        progressSlider.fillRect = fillRect;
        progressSlider.targetGraphic = bgImage;

        // Progress Text
        GameObject progressTextObj = new GameObject("ProgressText");
        progressTextObj.transform.SetParent(panel.transform, false);
        
        RectTransform progressTextRect = progressTextObj.AddComponent<RectTransform>();
        progressTextRect.anchorMin = new Vector2(0, 0);
        progressTextRect.anchorMax = new Vector2(1, 0);
        progressTextRect.pivot = new Vector2(0.5f, 0);
        progressTextRect.sizeDelta = new Vector2(-20, 25);
        progressTextRect.anchoredPosition = new Vector2(0, 10);
        
        progressText = progressTextObj.AddComponent<TextMeshProUGUI>();
        progressText.text = "0%";
        progressText.fontSize = 16;
        progressText.alignment = TextAlignmentOptions.Center;
        progressText.color = textColor;
        progressText.raycastTarget = false;

        uiRoot.SetActive(false);
    }
public void SetUIScale(float scale)
    {
        uiScale = Mathf.Clamp(scale, 0.5f, 2f);
        
        if (uiRoot != null && uiRoot.transform.childCount > 0)
        {
            Transform panel = uiRoot.transform.GetChild(0);
            if (panel != null)
            {
                panel.localScale = Vector3.one * uiScale;
            }
        }
    }
    public void Show()
    {
        if (uiRoot == null) return;
        
        uiRoot.SetActive(true);
        progressSlider.value = 0f;
        progressText.text = "0%";
        
        // Fade in animation
        canvasGroup.DOKill();
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad);
        
        // Glow pulse animation
        if (glowImage != null)
        {
            glowImage.DOKill();
            glowImage.DOFade(0.3f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }
        
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
    }

    public void Hide()
    {
        if (uiRoot == null) return;
        
        canvasGroup.DOKill();
        canvasGroup.DOFade(0f, 0.2f).OnComplete(() => {
            if (uiRoot != null) uiRoot.SetActive(false);
        });
        
        if (glowImage != null)
        {
            glowImage.DOKill();
        }
        
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
            updateCoroutine = null;
        }
    }

public void UpdateProgress(float progress)
    {
        if (progressSlider == null || progressText == null) return;
        
        progress = Mathf.Clamp01(progress);
        
        progressSlider.value = progress;
        progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
        
        // Fill color pulse near completion
        if (progress > 0.8f && sliderFill != null)
        {
            float pulseIntensity = (progress - 0.8f) / 0.2f;
            Color targetColor = Color.Lerp(sliderFillColor, Color.white, pulseIntensity * 0.3f);
            sliderFill.color = targetColor;
        }
        else if (sliderFill != null)
        {
            sliderFill.color = sliderFillColor;
        }
    }

    public void StartAutoUpdate(System.Func<float> getProgressCallback)
    {
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
        updateCoroutine = StartCoroutine(AutoUpdateCoroutine(getProgressCallback));
    }

    private IEnumerator AutoUpdateCoroutine(System.Func<float> getProgressCallback)
    {
        while (uiRoot != null && uiRoot.activeSelf)
        {
            float progress = getProgressCallback?.Invoke() ?? 0f;
            UpdateProgress(progress);
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (glowImage != null) glowImage.DOKill();
        if (canvasGroup != null) canvasGroup.DOKill();
        
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
    }
}