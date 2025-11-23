using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

public class InverseLightController : MonoBehaviour
{
    [Header("Light Settings")]
    [SerializeField] private Light2D[] assignedLights; // Inspector'da hala görünür ama otomatik doldurulacak
    [SerializeField] private float minIntensity = 0.1f;
    [SerializeField] private float maxIntensity = 1f;
    [SerializeField] private float transitionDuration = 10f;
    
    [Header("Global Light Reference")]
    [SerializeField] private Light2D globalLight;
    
    // Registry sistem için
    private List<IInverseLight> registeredLights = new List<IInverseLight>();
    
    private void Start()
    {
        // Eğer global light atanmamışsa otomatik bul
        if (globalLight == null)
        {
            GameObject globalLightObj = GameObject.Find("GlobalLight");
            if (globalLightObj != null)
            {
                globalLight = globalLightObj.GetComponent<Light2D>();
            }
        }
        
        // Registry sistem kullanılıyor, Start'ta bekleme süresi ver
        Invoke(nameof(StartLightSystem), 0.1f);
    }
    
    private void StartLightSystem()
    {
        // Başlangıç değerlerini ayarla
        InitializeLights();
        
        // Light cycle'ı başlat
        StartCoroutine(InverseLightCycle());
    }
    
    public void RegisterLight(IInverseLight light)
    {
        if (!registeredLights.Contains(light))
        {
            registeredLights.Add(light);
            RefreshAssignedLights();
        }
    }
    
    public void UnregisterLight(IInverseLight light)
    {
        if (registeredLights.Remove(light))
        {
            RefreshAssignedLights();
        }
    }
    
    private void RefreshAssignedLights()
    {
        assignedLights = new Light2D[registeredLights.Count];
        for (int i = 0; i < registeredLights.Count; i++)
        {
            assignedLights[i] = registeredLights[i].GetLight();
        }
    }
    
    private void InitializeLights()
    {
        // Başlangıçta gece (global light düşük, assigned lightlar yüksek)
        foreach (Light2D light in assignedLights)
        {
            if (light != null)
            {
                light.intensity = maxIntensity;
            }
        }
    }
    
    private IEnumerator InverseLightCycle()
    {
        while (true)
        {
            // Assigned lightlar 1'den 0.1'e
            yield return StartCoroutine(ChangeLightsIntensity(maxIntensity, minIntensity, transitionDuration));
            
            // Assigned lightlar 0.1'den 1'e
            yield return StartCoroutine(ChangeLightsIntensity(minIntensity, maxIntensity, transitionDuration));
        }
    }
    
    private IEnumerator ChangeLightsIntensity(float startIntensity, float targetIntensity, float duration)
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            // Smooth lerp ile geçiş
            float currentIntensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            
            // Tüm assigned lightlara aynı intensity'yi uygula
            foreach (Light2D light in assignedLights)
            {
                if (light != null)
                {
                    light.intensity = currentIntensity;
                }
            }
            
            yield return null;
        }
        
        // Son değerin kesin olarak ayarlandığından emin ol
        foreach (Light2D light in assignedLights)
        {
            if (light != null)
            {
                light.intensity = targetIntensity;
            }
        }
    }
    
    private void OnValidate()
    {
        if (assignedLights != null)
        {
            // Null referansları temizle
            for (int i = 0; i < assignedLights.Length; i++)
            {
                if (assignedLights[i] == null)
                {
                }
            }
        }
    }
}