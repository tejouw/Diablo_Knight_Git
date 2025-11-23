using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Tüm item türleri (fragment, craft item, potion, coin vb.) için bildirim sistemi
/// </summary>
public class FragmentNotificationUI : MonoBehaviour
{
    public static FragmentNotificationUI Instance { get; private set; }

    [Header("Animation Settings")]
    [SerializeField] private float slideDistance = 300f;
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    [Header("Visual Settings")]
    [SerializeField] private Vector2 notificationPosition = new Vector2(150f, -100f);
    [SerializeField] private Vector2 notificationSize = new Vector2(250f, 70f);
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    [SerializeField] private Color textColor = new Color(0.5f, 1f, 0.5f);
    [SerializeField] private float iconSize = 50f;
    [SerializeField] private float cornerRadius = 10f;

    // Inspector’dan ata: Main HUD altında boş bir RectTransform (örn. NotificationsRoot)
[SerializeField] private RectTransform containerParent;

// İstersen bu UI nesnesi sahneler arası kalsın; parent sahneye bağlıysa KAPALI bırak.
[SerializeField] private bool useDontDestroyOnLoad = false;


    private RectTransform notificationContainer;
    private Image backgroundImage;
    private Image itemIcon;
    private TextMeshProUGUI itemText;
    private CanvasGroup canvasGroup;

    private Queue<NotificationData> notificationQueue = new Queue<NotificationData>();
    private bool isShowingNotification = false;

    private class NotificationData
    {
        public string itemName;
        public int amount;
        public Sprite itemSprite;
    }

private void Awake()
{
    if (Instance == null)
    {
        Instance = this;

        if (useDontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        // Inspector’dan parent atanmışsa direkt kur.
        if (notificationContainer == null)
            CreateNotificationUI();
    }
    else
    {
        Destroy(gameObject);
    }
}

private void CreateNotificationUI()
{
    if (notificationContainer != null) return;

    // Öncelik: inspector’dan atanmış parent
    RectTransform parent = containerParent;

    // Parent atanmadıysa emniyet için tek seferlik Canvas ara (fallback).
    if (parent == null)
    {
        Canvas c = FindMainCanvas();
        if (c == null)
        {
            Debug.LogError("[FragmentNotificationUI] Parent bulunamadı (containerParent atanmamış ve Canvas bulunamadı).");
            return;
        }
        parent = c.transform as RectTransform;
    }

    GameObject containerObj = new GameObject("ItemNotificationContainer");
    containerObj.layer = parent.gameObject.layer;
    containerObj.transform.SetParent(parent, false);

    notificationContainer = containerObj.AddComponent<RectTransform>();
    notificationContainer.anchorMin = new Vector2(0f, 1f);
    notificationContainer.anchorMax = new Vector2(0f, 1f);
    notificationContainer.pivot = new Vector2(0f, 1f);
    notificationContainer.anchoredPosition = notificationPosition;
    notificationContainer.sizeDelta = notificationSize;

    canvasGroup = containerObj.AddComponent<CanvasGroup>();
    canvasGroup.alpha = 0f;
    canvasGroup.blocksRaycasts = false; // <- Raycast bloklama kapalı

    CreateBackground();
    CreateIcon();
    CreateText();

    notificationContainer.anchoredPosition =
        new Vector2(notificationPosition.x - slideDistance, notificationPosition.y);
}



    private Canvas FindMainCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.isRootCanvas)
            {
                return canvas;
            }
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }

    private void CreateBackground()
    {
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(notificationContainer, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;

        backgroundImage = bgObj.AddComponent<Image>();
        backgroundImage.color = backgroundColor;
        backgroundImage.sprite = CreateRoundedSprite();
        backgroundImage.type = Image.Type.Sliced;
        
    }

    private void CreateIcon()
    {
        GameObject iconObj = new GameObject("ItemIcon");
        iconObj.transform.SetParent(notificationContainer, false);

        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(10f, 0f);
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);

        itemIcon = iconObj.AddComponent<Image>();
        itemIcon.preserveAspect = true;

    }

private void CreateText()
{
    GameObject textObj = new GameObject("ItemText");
    textObj.transform.SetParent(notificationContainer, false);

    RectTransform textRect = textObj.AddComponent<RectTransform>();
    textRect.anchorMin = new Vector2(0f, 0f);
    textRect.anchorMax = new Vector2(1f, 1f);
    textRect.sizeDelta = Vector2.zero;
    textRect.anchoredPosition = new Vector2(iconSize + 20f, 0f);
    textRect.offsetMin = new Vector2(iconSize + 20f, 5f);
    textRect.offsetMax = new Vector2(-10f, -5f);

    itemText = textObj.AddComponent<TextMeshProUGUI>();
    itemText.fontSize = 22f;
    itemText.color = textColor;
    itemText.alignment = TextAlignmentOptions.Left;
    itemText.fontStyle = FontStyles.Bold;
    itemText.textWrappingMode = TextWrappingModes.NoWrap;
    itemText.overflowMode = TextOverflowModes.Ellipsis;
}


    private Sprite CreateRoundedSprite()
    {
        int size = 64;
        int radius = Mathf.RoundToInt(cornerRadius);
        
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isCorner = false;
                
                if (x < radius && y < radius)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius));
                    isCorner = dist > radius;
                }
                else if (x >= size - radius && y < radius)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size - radius - 1, radius));
                    isCorner = dist > radius;
                }
                else if (x < radius && y >= size - radius)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, size - radius - 1));
                    isCorner = dist > radius;
                }
                else if (x >= size - radius && y >= size - radius)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size - radius - 1, size - radius - 1));
                    isCorner = dist > radius;
                }

                pixels[y * size + x] = isCorner ? Color.clear : Color.white;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

public void ShowFragmentNotification(string itemName, int amount, Sprite itemSprite)
{
    if (string.IsNullOrEmpty(itemName) || itemSprite == null)
        return;

    if (notificationContainer == null)
    {
        // Parent inspector’dan geldiği için deterministik kurulum
        CreateNotificationUI();
        if (notificationContainer == null)
        {
            Debug.LogError("[FragmentNotificationUI] UI oluşturulamadı (parent/null).");
            return;
        }
    }

    if (itemIcon == null || itemText == null)
    {
        CreateIcon();
        CreateText();
    }

    NotificationData data = new NotificationData
    {
        itemName = itemName,
        amount = amount,
        itemSprite = itemSprite
    };

    notificationQueue.Enqueue(data);

    if (!isShowingNotification)
        StartCoroutine(ProcessNotificationQueue());
}


private IEnumerator ProcessNotificationQueue()
{
    // Ek güvenlik kontrolü
    if (notificationContainer == null)
    {
        Debug.LogError("[FragmentNotificationUI] notificationContainer null, queue işlenemiyor");
        notificationQueue.Clear();
        isShowingNotification = false;
        yield break;
    }

    isShowingNotification = true;

    while (notificationQueue.Count > 0)
    {
        NotificationData data = notificationQueue.Dequeue();
        yield return StartCoroutine(ShowNotificationCoroutine(data));
    }

    isShowingNotification = false;
}

    private IEnumerator ShowNotificationCoroutine(NotificationData data)
    {

        itemIcon.sprite = data.itemSprite;
        itemText.text = $"{data.itemName} x{data.amount}";

        Vector2 startPos = new Vector2(notificationPosition.x - slideDistance, notificationPosition.y);
        Vector2 centerPos = notificationPosition;
        Vector2 endPos = new Vector2(notificationPosition.x + slideDistance, notificationPosition.y);

        notificationContainer.anchoredPosition = startPos;
        canvasGroup.alpha = 0f;
        

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            notificationContainer.anchoredPosition = Vector2.Lerp(startPos, centerPos, smoothT);
            canvasGroup.alpha = smoothT;

            yield return null;
        }

        notificationContainer.anchoredPosition = centerPos;
        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            notificationContainer.anchoredPosition = Vector2.Lerp(centerPos, endPos, smoothT);
            canvasGroup.alpha = 1f - smoothT;

            yield return null;
        }

        canvasGroup.alpha = 0f;
    }
}