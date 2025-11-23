using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class GlobalLightController : MonoBehaviour
{
    [Header("Light Settings")]
    [SerializeField] private float minIntensity = 0.1f;
    [SerializeField] private float maxIntensity = 1f;
    [SerializeField] private float transitionDuration = 10f;
    [Header("Day/Night System")]
    [SerializeField] private float dayThreshold = 0.5f; // Inspector'da ayarlanabilir
    public static bool IsDay { get; private set; } = false;
    public static bool IsNight => !IsDay;
    private Light2D globalLight;
    private static GlobalLightController instance;
    
    public static GlobalLightController GetInstance()
    {
        return instance;
    }
    private void Awake()
    {
        // Singleton pattern - sadece bir tane olsun
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        StartLightSystem();
    }
    
private void StartLightSystem()
{
    // Bu GameObject'den direkt Light2D componentini al
    globalLight = GetComponent<Light2D>();
    
    if (globalLight == null)
    {
        return;
    }
    
    // Başlangıç intensity'sini minimum değere ayarla
    globalLight.intensity = minIntensity;
    
    // İlk durum güncellemesi
    UpdateDayNightState(minIntensity);
    
    StartCoroutine(LightCycle());
}
    
    // Scene değişimlerinde tekrar başlatmak için
    private void OnEnable()
    {
        if (instance == this && globalLight != null)
        {
            StopAllCoroutines();
            StartCoroutine(LightCycle());
        }
    }
    
    private IEnumerator LightCycle()
    {
        while (true)
        {
            // 0.1'den 1'e 10 saniyede artır
            yield return StartCoroutine(ChangeLightIntensity(minIntensity, maxIntensity, transitionDuration));
            
            // 1'den 0.1'e 10 saniyede azalt
            yield return StartCoroutine(ChangeLightIntensity(maxIntensity, minIntensity, transitionDuration));
        }
    }

    private IEnumerator ChangeLightIntensity(float startIntensity, float targetIntensity, float duration)
    {
        if (globalLight == null) yield break;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            // Smooth lerp ile geçiş
            float currentIntensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            globalLight.intensity = currentIntensity;

            // Gece/gündüz durumunu güncelle
            UpdateDayNightState(currentIntensity);

            yield return null;
        }

        // Son değerin kesin olarak ayarlandığından emin ol
        globalLight.intensity = targetIntensity;
        UpdateDayNightState(targetIntensity);
    }
private void UpdateDayNightState(float currentIntensity)
{
    bool newIsDay = currentIntensity > dayThreshold; // >= yerine > kullan

    if (newIsDay != IsDay)
    {
        IsDay = newIsDay;
    }
}
}