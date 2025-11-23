using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Collections.Generic;

public class CleaveStrikePreview : BaseSkillPreview
{
    public override string SkillId => "cleave_strike";
    
    private const float CONE_ANGLE = 180f;
    private const float SKILL_RANGE = 8f;
    private const int CONE_SEGMENTS = 20;
        private SpriteRenderer coneRenderer;
    private float pulseTimer = 0f;
    private GameObject coneVisual;
    private LineRenderer[] lineRenderers;
    private List<GameObject> activeTargetAuras = new List<GameObject>();
    private GameObject targetAuraPrefab;
    
protected override void CreatePreviewObject()
{
    
    previewObject = new GameObject("CleaveStrike_Preview");
    previewObject.transform.SetParent(transform);
    
    
    // Target_Aura prefab'ını yükle
    LoadTargetAuraPrefab();
    
    // Cone outline oluştur
    CreateConeOutline();
    
}
    
private void LoadTargetAuraPrefab()
{
    targetAuraPrefab = Resources.Load<GameObject>("VFX/Target_Aura");
    if (targetAuraPrefab == null)
    {
        Debug.LogError("[CleavePreview] Target_Aura prefab not found in Resources/VFX/!");
    }
    else
    {
    }
}
    
private void CreateConeOutline()
{
    GameObject coneVisual = new GameObject("ConeVisual");
    coneVisual.transform.SetParent(previewObject.transform);
    
    SpriteRenderer coneRenderer = coneVisual.AddComponent<SpriteRenderer>();
    coneRenderer.sprite = CreateSmoothConeSprite();
    coneRenderer.material = CreateSmoothConeMaterial();
    coneRenderer.sortingLayerName = "UI";
    coneRenderer.sortingOrder = 5;
    
    this.coneRenderer = coneRenderer;
    this.coneVisual = coneVisual; // Reference sakla
}

private Sprite CreateSmoothConeSprite()
{
    int resolution = 256;
    Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
    Color[] colors = new Color[resolution * resolution];
    
    // DÜZELTME: Pivot center'da olacak, yön kontrolü UpdatePreview'da yapılacak
    Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
    
    // DÜZELTME: Gerçek skill range'i kullan (pixel cinsinden)
    float maxDistance = (resolution / 2f) * 0.9f; // %90'ını kullan, kenar yumuşaklığı için
    float halfAngle = CONE_ANGLE / 2f;
    
    for (int x = 0; x < resolution; x++)
    {
        for (int y = 0; y < resolution; y++)
        {
            Vector2 pos = new Vector2(x, y);
            Vector2 dir = pos - center;
            float distance = dir.magnitude;
            
            if (distance < 5f) // Çok küçük mesafeleri atla
            {
                colors[y * resolution + x] = Color.clear;
                continue;
            }
            
            // DÜZELTME: Yukarı doğru (0,1) referans alarak açı hesapla
            float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            
            bool inCone = Mathf.Abs(angle) <= halfAngle && distance <= maxDistance;
            
            if (inCone)
            {
                float distanceNorm = distance / maxDistance;
                float angleNorm = Mathf.Abs(angle) / halfAngle;
                
                float distanceAlpha = Mathf.Lerp(0.6f, 0.1f, Mathf.Pow(distanceNorm, 0.5f));
                
                // Kenar yumuşaklığı
                float edgeSoftness = 0.2f;
                float angleAlpha = 1f;
                if (angleNorm > (1f - edgeSoftness))
                {
                    angleAlpha = Mathf.Lerp(1f, 0f, (angleNorm - (1f - edgeSoftness)) / edgeSoftness);
                }
                
                float finalAlpha = distanceAlpha * angleAlpha;
                colors[y * resolution + x] = new Color(1f, 0.6f, 0.2f, finalAlpha);
            }
            else
            {
                colors[y * resolution + x] = Color.clear;
            }
        }
    }
    
    texture.SetPixels(colors);
    texture.Apply();
    texture.filterMode = FilterMode.Bilinear;
    
    // DÜZELTME: Center pivot ile oluştur
    return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), 
        new Vector2(0.5f, 0.5f), resolution / (SKILL_RANGE * 2f)); // Doğru scale hesabı
}

private Material CreateSmoothConeMaterial()
{
    Material mat = new Material(Shader.Find("Sprites/Default"));
    mat.color = Color.white;
    mat.SetFloat("_Mode", 3); // Transparent
    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    mat.SetInt("_ZWrite", 0);
    mat.DisableKeyword("_ALPHATEST_ON");
    mat.EnableKeyword("_ALPHABLEND_ON");
    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    mat.renderQueue = 3000;
    return mat;
}

    public override void UpdatePreview(GameObject caster, Vector2 direction)
    {
        if (previewObject == null || coneRenderer == null) return;

        Vector3 casterPos = caster.transform.position;
        Transform coneTransform = coneRenderer.transform;

        // DÜZELTME: Direction'a göre pozisyon ve rotation ayarla
        SetConePositionAndRotation(casterPos, direction, coneTransform);

        // Pulse animasyonu
        UpdatePulseAnimation();

        // Target aura'ları
        UpdateTargetAuras(caster, casterPos, direction);
    }
private void SetConePositionAndRotation(Vector3 casterPos, Vector2 direction, Transform coneTransform)
{
    // Yöne göre offset hesapla - karakterin önünde görünmesi için
    Vector3 offsetPosition = casterPos + (Vector3)(direction * 0.5f);
    coneTransform.position = offsetPosition;
    
    // DÜZELTME: X eksenini negatif yap - Unity'de saat yönünün tersi rotation
    float angle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
    coneTransform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    
    UpdateSortingOrder(direction);
}
private void UpdateSortingOrder(Vector2 direction)
{
    if (coneRenderer == null) return;
    
    // Sağ/sol için önde, yukarı/aşağı için normal
    if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
    {
        // Sağ veya sol - önde olmalı
        coneRenderer.sortingOrder = 15; // Daha yüksek order
    }
    else
    {
        // Yukarı veya aşağı - normal
        coneRenderer.sortingOrder = direction.y > 0 ? 15 : 5; // Yukarı yüksek, aşağı düşük
    }
}

    private void UpdatePulseAnimation()
    {
        if (coneRenderer == null) return;

        pulseTimer += Time.deltaTime * 1.5f;
        float pulse = Mathf.Lerp(0.7f, 1.0f, (Mathf.Sin(pulseTimer) + 1f) / 2f);

        Color color = coneRenderer.color;
        color.a = pulse;
        coneRenderer.color = color;
    }

private void UpdateTargetAuras(GameObject caster, Vector3 origin, Vector2 direction)
{
    if (targetAuraPrefab == null)
    {
        Debug.LogError("[CleavePreview] UpdateTargetAuras - targetAuraPrefab is NULL!");
        return;
    }
    
    var currentTargets = FindTargetsInCone(caster, origin, direction);
    
    ClearUnusedAuras(currentTargets);
    SpawnAurasForNewTargets(currentTargets);
}
    
    private void ClearUnusedAuras(List<GameObject> currentTargets)
    {
        for (int i = activeTargetAuras.Count - 1; i >= 0; i--)
        {
            if (activeTargetAuras[i] == null)
            {
                activeTargetAuras.RemoveAt(i);
                continue;
            }
            
            // Bu aura'nın target'ı hala listede var mı?
            TargetAuraController auraController = activeTargetAuras[i].GetComponent<TargetAuraController>();
            if (auraController == null || !currentTargets.Contains(auraController.TargetMonster))
            {
                Destroy(activeTargetAuras[i]);
                activeTargetAuras.RemoveAt(i);
            }
        }
    }
    
    private void SpawnAurasForNewTargets(List<GameObject> currentTargets)
    {
        foreach (var target in currentTargets)
        {
            // Bu target için zaten aura var mı?
            bool hasAura = false;
            foreach (var aura in activeTargetAuras)
            {
                if (aura != null)
                {
                    TargetAuraController auraController = aura.GetComponent<TargetAuraController>();
                    if (auraController != null && auraController.TargetMonster == target)
                    {
                        hasAura = true;
                        break;
                    }
                }
            }
            
            // Aura yoksa spawn et
            if (!hasAura)
            {
                SpawnTargetAura(target);
            }
        }
    }
    
private void SpawnTargetAura(GameObject target)
{
    if (targetAuraPrefab == null)
    {
        Debug.LogError("[CleavePreview] targetAuraPrefab is NULL!");
        return;
    }
    
    if (target == null)
    {
        Debug.LogError("[CleavePreview] target is NULL!");
        return;
    }
    
    Vector3 auraPosition = target.transform.position;
    GameObject auraObj = Instantiate(targetAuraPrefab, auraPosition, Quaternion.identity);
    
    if (auraObj == null)
    {
        Debug.LogError("[CleavePreview] Aura instantiate FAILED!");
        return;
    }
    
    
    // Sprite renderer kontrolü
    SpriteRenderer[] renderers = auraObj.GetComponentsInChildren<SpriteRenderer>(true);
    
    foreach (var renderer in renderers)
    {
    }
    
    TargetAuraController controller = auraObj.GetComponent<TargetAuraController>();
    if (controller == null)
    {
        controller = auraObj.AddComponent<TargetAuraController>();
    }
    else
    {
    }
    
    controller.SetTarget(target);
    activeTargetAuras.Add(auraObj);
    
}
    
    public override void HidePreview()
    {
        base.HidePreview();
        
        // Tüm Target Aura'ları temizle
        foreach (var aura in activeTargetAuras)
        {
            if (aura != null)
            {
                Destroy(aura);
            }
        }
        activeTargetAuras.Clear();
    }
    
private List<GameObject> FindTargetsInCone(GameObject caster, Vector3 origin, Vector2 direction)
{
    // SkillTargetingUtils kullan - hem monster hem player bulur
    return SkillTargetingUtils.FindTargetsInCone(origin, direction, CONE_ANGLE, SKILL_RANGE, caster);
}
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        // Tüm aura'ları temizle
        foreach (var aura in activeTargetAuras)
        {
            if (aura != null)
            {
                Destroy(aura);
            }
        }
        activeTargetAuras.Clear();
    }
}