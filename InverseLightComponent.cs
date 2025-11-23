using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class InverseLightComponent : MonoBehaviour, IInverseLight
{
    private Light2D cachedLight;
    private bool isRegistered = false;
    private Coroutine registrationRetryCoroutine;
    
    private void Awake()
    {
        cachedLight = GetComponent<Light2D>();
    }
    
    private void Start()
    {
        RegisterToController();
    }
    
    private void OnDestroy()
    {
        UnregisterFromController();
        
        if (registrationRetryCoroutine != null)
        {
            StopCoroutine(registrationRetryCoroutine);
        }
    }
    
    public Light2D GetLight()
    {
        return cachedLight;
    }
    
    public void RegisterToController()
    {
        if (isRegistered) return;
        
        InverseLightController controller = FindFirstObjectByType<InverseLightController>();
        if (controller != null)
        {
            controller.RegisterLight(this);
            isRegistered = true;
            
            if (registrationRetryCoroutine != null)
            {
                StopCoroutine(registrationRetryCoroutine);
                registrationRetryCoroutine = null;
            }
        }
        else
        {
            // Controller bulunamazsa retry yap
            if (registrationRetryCoroutine == null)
            {
                registrationRetryCoroutine = StartCoroutine(RetryRegistration());
            }
        }
    }
    
    private IEnumerator RetryRegistration()
    {
        float retryTime = 0f;
        const float maxRetryTime = 5f;
        
        while (retryTime < maxRetryTime && !isRegistered)
        {
            yield return new WaitForSeconds(0.5f);
            retryTime += 0.5f;
            
            InverseLightController controller = FindFirstObjectByType<InverseLightController>();
            if (controller != null)
            {
                controller.RegisterLight(this);
                isRegistered = true;
                break;
            }
        }
        
        registrationRetryCoroutine = null;
    }
    
    public void UnregisterFromController()
    {
        if (!isRegistered) return;
        
        InverseLightController controller = FindFirstObjectByType<InverseLightController>();
        if (controller != null)
        {
            controller.UnregisterLight(this);
        }
        
        isRegistered = false;
    }
}