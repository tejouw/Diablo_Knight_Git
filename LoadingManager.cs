using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance;
    
    [Header("Loading UI")]
    [SerializeField] public GameObject loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI detailText; // YENI: Detay gösterimi
    [SerializeField] private Image loadingIcon;
    
    [Header("Development")]
    [SerializeField] private bool developmentMode = true; // YENI: Development modu
    [SerializeField] private TextMeshProUGUI debugText; // YENI: Debug bilgileri
    
    [Header("Settings")]
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float uiUpdateInterval = 0.5f; // YENI: UI güncelleme sıklığı
    
    // YENI: Detaylı step bilgileri
    [System.Serializable]
    public class LoadingStep
    {
        public string stepName;
        public string displayName;
        public string waitingFor; // Hangi metodu/sistemi bekliyor
        public string responsibleClass; // Hangi sınıf sorumlu
        public bool isCompleted;
        public float startTime;
        public float timeoutDuration;
        
        public LoadingStep(string name, string display, string waiting, string responsible, float timeout = 15f)
        {
            stepName = name;
            displayName = display;
            waitingFor = waiting;
            responsibleClass = responsible;
            isCompleted = false;
            timeoutDuration = timeout;
        }
    }
    
    private Dictionary<string, LoadingStep> loadingSteps = new Dictionary<string, LoadingStep>();
    private List<string> pendingCompletions = new List<string>();
    private List<string> stepOrder = new List<string>(); // YENI: Step sırası
    private int completedSteps = 0;
    private bool isInitialized = false;
    private float loadingStartTime;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void StartLoading()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(true);
            
        loadingStartTime = Time.time;
        InitializeLoadingSteps();
        ProcessPendingCompletions();
        StartCoroutine(RotateLoadingIcon());
        StartCoroutine(CheckTimeout());
        StartCoroutine(UpdateUI()); // YENI: UI güncelleme coroutine
        UpdateProgress();
        
    }
    
private void InitializeLoadingSteps()
{
    loadingSteps.Clear();
    stepOrder.Clear();

    AddStep("FirebaseConnection", "Firebase Bağlantısı", "FirebaseManager.IsReady", "FirebaseManager", 2f);
    AddStep("UserAccount", "Hesap Kontrolü", "FirebaseManager.CreateUserAccount()", "SimpleLoginManager", 3f);
    AddStep("CharacterValidation", "Karakter Doğrulama", "Character & Race Validation", "SimpleLoginManager", 3f);
    AddStep("NetworkConnection", "Sunucu Bağlantısı", "NetworkManager.ConnectToServer()", "NetworkManager", 10f);
    AddStep("PlayerSpawn", "Karakter Oluşturma", "NetworkManager.OnPlayerJoined()", "NetworkManager", 5f);
    AddStep("UITransition", "Arayüz Geçişi", "Login to Game UI", "SimpleLoginManager", 1f);
    AddStep("PlayerInitialization", "Oyuncu Başlatma", "Player Systems Initialize", "NetworkManager", 3f);
    AddStep("NetworkInitialization", "Ağ Başlatma", "Network Systems Ready", "NetworkManager", 2f);
    AddStep("UIInitialization", "Arayüz Hazırlama", "UIManager.Initialize()", "UIManager", 2f);
    AddStep("CombatUIReady", "Savaş Arayüzü", "CombatInitializer.InitializeCombatButtons()", "CombatInitializer", 3f); // Timeout 5f -> 3f, sıra değişti
    AddStep("CharacterLoading", "Karakter Verisi", "CharacterDataManager.LoadCharacterData()", "CharacterLoader", 3f);
    AddStep("QuestManager", "Görev Sistemi", "QuestManager.Initialize()", "QuestManager", 2f);
    AddStep("SystemsReady", "Sistem Hazırlığı", "All Network Systems Ready", "Various", 2f);

    completedSteps = 0;
    isInitialized = true;
}
    
    private void AddStep(string name, string display, string waiting, string responsible, float timeout = 15f)
    {
        var step = new LoadingStep(name, display, waiting, responsible, timeout);
        step.startTime = Time.time;
        loadingSteps[name] = step;
        stepOrder.Add(name);
    }
    
    private void ProcessPendingCompletions()
    {
        if (developmentMode && pendingCompletions.Count > 0)
            
        foreach (string stepName in pendingCompletions)
        {
            CompleteStepInternal(stepName);
        }
        
        pendingCompletions.Clear();
    }
    
    public void CompleteStep(string stepName)
    {
        if (developmentMode)
            
        if (!isInitialized)
        {
            if (!pendingCompletions.Contains(stepName))
            {
                pendingCompletions.Add(stepName);
            }
            return;
        }
        
        CompleteStepInternal(stepName);
    }
    
    private void CompleteStepInternal(string stepName)
    {
        if (!loadingSteps.ContainsKey(stepName))
        {
            Debug.LogError($"[LoadingManager] Bilinmeyen step: {stepName}");
            return;
        }
        
        var step = loadingSteps[stepName];
        if (step.isCompleted)
        {
            if (developmentMode)
            return;
        }
        
        step.isCompleted = true;
        completedSteps++;
        
        float completionTime = Time.time - step.startTime;
        if (developmentMode)
        
        UpdateProgress();
        
        if (completedSteps >= loadingSteps.Count)
        {
            StartCoroutine(FinishLoading());
        }
    }
    
    // YENI: Firebase connection check
    public void CheckFirebaseConnection()
    {
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
        {
            CompleteStep("FirebaseConnection");
        }
        else
        {
            StartCoroutine(WaitForFirebase());
        }
    }
    
private IEnumerator WaitForFirebase()
{
    float waitTime = 0f;
    while (waitTime < 3f) // 10f -> 3f
    {
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
        {
            CompleteStep("FirebaseConnection");
            yield break;
        }
        yield return new WaitForSeconds(0.05f); // 0.1f -> 0.05f (daha sık kontrol)
        waitTime += 0.05f;
    }
    
    // Timeout durumunda da geç
    CompleteStep("FirebaseConnection");
}
    
    // YENI: UI güncelleme coroutine
    private IEnumerator UpdateUI()
    {
        while (loadingPanel != null && loadingPanel.activeInHierarchy && completedSteps < loadingSteps.Count)
        {
            UpdateProgress();
            yield return new WaitForSeconds(uiUpdateInterval);
        }
    }
    
    public void UpdateProgress()
    {
        if (!isInitialized) return;
        
        float progress = (float)completedSteps / loadingSteps.Count;
        if (progressBar != null)
        {
            progressBar.DOValue(progress, 0.3f);
        }
        
        if (loadingText != null)
        {
            loadingText.text = GetCurrentLoadingText();
        }
        
        if (detailText != null)
        {
            detailText.text = GetDetailText();
        }
        
        if (developmentMode && debugText != null)
        {
            debugText.text = GetDebugText();
        }
    }
    
    private string GetCurrentLoadingText()
    {
        if (!isInitialized) return "Başlatılıyor...";
        
        // İlk tamamlanmamış step'i bul
        foreach (string stepName in stepOrder)
        {
            var step = loadingSteps[stepName];
            if (!step.isCompleted)
            {
                return step.displayName;
            }
        }
        
        return "Tamamlanıyor...";
    }
    
    private string GetDetailText()
    {
        if (!isInitialized) return "";
        
        // İlk tamamlanmamış step'in detayını göster
        foreach (string stepName in stepOrder)
        {
            var step = loadingSteps[stepName];
            if (!step.isCompleted)
            {
                float waitingTime = Time.time - step.startTime;
                return $"Bekliyor: {step.waitingFor}\nBekleme süresi: {waitingTime:F1}s";
            }
        }
        
        return "Hazır!";
    }
    
    private string GetDebugText()
    {
        if (!isInitialized) return "";
        
        string debug = $"=== LOADING DEBUG ===\n";
        debug += $"Toplam süre: {Time.time - loadingStartTime:F1}s\n";
        debug += $"Tamamlanan: {completedSteps}/{loadingSteps.Count}\n\n";
        
        foreach (string stepName in stepOrder)
        {
            var step = loadingSteps[stepName];
            string status = step.isCompleted ? "+" : "-";
            float elapsed = Time.time - step.startTime;
            
            debug += $"{status} {step.stepName}\n";
            if (!step.isCompleted)
            {
                debug += $"   Sorumlu: {step.responsibleClass}\n";
                debug += $"   Bekliyor: {step.waitingFor}\n";
                debug += $"   Süre: {elapsed:F1}s / {step.timeoutDuration:F0}s\n";
            }
            else
            {
                debug += $"   Tamamlandı: {elapsed:F1}s\n";
            }
            debug += "\n";
        }
        
        return debug;
    }
    
    private IEnumerator RotateLoadingIcon()
    {
        while (loadingPanel != null && loadingPanel.activeInHierarchy)
        {
            if (loadingIcon != null)
            {
                loadingIcon.transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
            }
            yield return null;
        }
    }
    
    private IEnumerator CheckTimeout()
    {
        while (completedSteps < loadingSteps.Count && isInitialized)
        {
            yield return new WaitForSeconds(1f);
            
            if (!isInitialized) continue;
            
            float currentTime = Time.time;
            List<string> timeoutSteps = new List<string>();
            
            foreach (var kvp in loadingSteps)
            {
                var step = kvp.Value;
                if (!step.isCompleted && currentTime - step.startTime > step.timeoutDuration)
                {
                    timeoutSteps.Add(kvp.Key);

                }
            }
            
            foreach (string stepName in timeoutSteps)
            {
                CompleteStepInternal(stepName);
            }
        }
    }
    
    public void CancelLoading()
    {
        if (developmentMode)
            
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
        
        StopAllCoroutines();
        
        loadingSteps.Clear();
        stepOrder.Clear();
        pendingCompletions.Clear();
        completedSteps = 0;
        isInitialized = false;
    }
    
    private IEnumerator FinishLoading()
    {
        if (developmentMode)
        {
            float totalTime = Time.time - loadingStartTime;
        }
        
        if (loadingText != null)
            loadingText.text = "Hazır!";
            
        yield return new WaitForSeconds(0.5f);
        
        CanvasGroup canvasGroup = loadingPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = loadingPanel.AddComponent<CanvasGroup>();
        }
        
        canvasGroup.DOFade(0f, 1f).OnComplete(() => {
            loadingPanel.SetActive(false);
            canvasGroup.alpha = 1f;
        });
    }
    
    // YENI: Manuel step tracking metodları
    public void LogWaitingFor(string stepName, string additionalInfo = "")
    {
        if (!developmentMode) return;
        
        if (loadingSteps.ContainsKey(stepName))
        {
            var step = loadingSteps[stepName];
            if (!step.isCompleted)
            {
                float waitTime = Time.time - step.startTime;
            }
        }
    }
    
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}