using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Collections.Generic;

public class ColossusStancePreview : BaseSkillPreview
{
    public override string SkillId => "colossus_stance";
    
    private const float AOE_RADIUS = 4f; // ColossusStanceExecutor ile aynı
    
    private SpriteRenderer circleRenderer;
    private float pulseTimer = 0f;
    private GameObject circleVisual;
    private List<GameObject> activeEnemyAuras = new List<GameObject>();
    private List<GameObject> activeAllyAuras = new List<GameObject>();
    private GameObject enemyAuraPrefab;
    private GameObject allyAuraPrefab;
    
    protected override void CreatePreviewObject()
    {
        previewObject = new GameObject("ColossusStance_Preview");
        previewObject.transform.SetParent(transform);
        
        // Aura prefab'larını yükle
        LoadAuraPrefabs();
        
        // Circle preview oluştur
        CreateCirclePreview();
    }
    
    private void LoadAuraPrefabs()
    {
        enemyAuraPrefab = Resources.Load<GameObject>("VFX/Target_Aura");
        allyAuraPrefab = Resources.Load<GameObject>("VFX/Target_Aura"); // Aynı prefab, farklı renkler
        
        if (enemyAuraPrefab == null)
        {
            Debug.LogError("[ColossusStancePreview] Target_Aura prefab not found in Resources/VFX/!");
        }
    }
    
    private void CreateCirclePreview()
    {
        GameObject circleVisual = new GameObject("CircleVisual");
        circleVisual.transform.SetParent(previewObject.transform);
        
        SpriteRenderer circleRenderer = circleVisual.AddComponent<SpriteRenderer>();
        circleRenderer.sprite = CreateSmoothCircleSprite();
        circleRenderer.material = CreateSmoothCircleMaterial();
        circleRenderer.sortingLayerName = "UI";
        circleRenderer.sortingOrder = 5;
        
        this.circleRenderer = circleRenderer;
        this.circleVisual = circleVisual;
    }
    
    private Sprite CreateSmoothCircleSprite()
    {
        int resolution = 256;
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        Color[] colors = new Color[resolution * resolution];
        
        Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
        float maxRadius = (resolution / 2f) * 0.9f;
        
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);
                
                if (distance <= maxRadius)
                {
                    float distanceNorm = distance / maxRadius;
                    
                    // Center'dan kenara gradient
                    float distanceAlpha = Mathf.Lerp(0.7f, 0.1f, Mathf.Pow(distanceNorm, 0.3f));
                    
                    // Kenar yumuşaklığı
                    float edgeSoftness = 0.15f;
                    if (distanceNorm > (1f - edgeSoftness))
                    {
                        float edgeAlpha = Mathf.Lerp(1f, 0f, (distanceNorm - (1f - edgeSoftness)) / edgeSoftness);
                        distanceAlpha *= edgeAlpha;
                    }
                    
                    // Colossus için altın rengi (tank/guardian teması)
                    colors[y * resolution + x] = new Color(1f, 0.8f, 0.2f, distanceAlpha); 
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
        
        return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), 
            new Vector2(0.5f, 0.5f), resolution / (AOE_RADIUS * 2f));
    }
    
    private Material CreateSmoothCircleMaterial()
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
        if (previewObject == null || circleRenderer == null) return;

        Vector3 casterPos = caster.transform.position;
        
        // Circle'ı caster pozisyonuna yerleştir (ColossusStance self-centered)
        SetCirclePosition(casterPos);

        // Pulse animasyonu
        UpdatePulseAnimation();

        // Target aura'ları (hem enemy hem ally)
        UpdateTargetAuras(caster, casterPos);
    }
    
    private void SetCirclePosition(Vector3 casterPos)
    {
        Transform circleTransform = circleRenderer.transform;
        circleTransform.position = casterPos;
        
        // Circle için rotation gerekmiyor
        circleRenderer.sortingOrder = 10;
    }
    
    private void UpdatePulseAnimation()
    {
        if (circleRenderer == null) return;
        
        pulseTimer += Time.deltaTime * 1.5f; // Orta hızda pulse
        float pulse = Mathf.Lerp(0.8f, 1.0f, (Mathf.Sin(pulseTimer) + 1f) / 2f);
        
        Color color = circleRenderer.color;
        color.a = pulse;
        circleRenderer.color = color;
    }
    
    private void UpdateTargetAuras(GameObject caster, Vector3 center)
    {
        if (enemyAuraPrefab == null) return;
        
        // SkillTargetingUtils ile allies ve enemies'i bul
        var targetsInRange = SkillTargetingUtils.FindAlliesAndEnemiesInCircle(center, AOE_RADIUS, caster);
        var currentAllies = targetsInRange.allies;
        var currentEnemies = targetsInRange.enemies;
        
        // Eski aura'ları temizle
        ClearUnusedEnemyAuras(currentEnemies);
        ClearUnusedAllyAuras(currentAllies);
        
        // Yeni target'lar için aura spawn et
        SpawnAurasForNewEnemies(currentEnemies);
        SpawnAurasForNewAllies(currentAllies);
    }
    
    private void ClearUnusedEnemyAuras(List<GameObject> currentEnemies)
    {
        for (int i = activeEnemyAuras.Count - 1; i >= 0; i--)
        {
            if (activeEnemyAuras[i] == null)
            {
                activeEnemyAuras.RemoveAt(i);
                continue;
            }
            
            TargetAuraController auraController = activeEnemyAuras[i].GetComponent<TargetAuraController>();
            if (auraController == null || !currentEnemies.Contains(auraController.TargetMonster))
            {
                Destroy(activeEnemyAuras[i]);
                activeEnemyAuras.RemoveAt(i);
            }
        }
    }
    
    private void ClearUnusedAllyAuras(List<GameObject> currentAllies)
    {
        for (int i = activeAllyAuras.Count - 1; i >= 0; i--)
        {
            if (activeAllyAuras[i] == null)
            {
                activeAllyAuras.RemoveAt(i);
                continue;
            }
            
            TargetAuraController auraController = activeAllyAuras[i].GetComponent<TargetAuraController>();
            if (auraController == null || !currentAllies.Contains(auraController.TargetMonster))
            {
                Destroy(activeAllyAuras[i]);
                activeAllyAuras.RemoveAt(i);
            }
        }
    }
    
    private void SpawnAurasForNewEnemies(List<GameObject> currentEnemies)
    {
        foreach (var enemy in currentEnemies)
        {
            // Bu enemy için zaten aura var mı?
            bool hasAura = false;
            foreach (var aura in activeEnemyAuras)
            {
                if (aura != null)
                {
                    TargetAuraController auraController = aura.GetComponent<TargetAuraController>();
                    if (auraController != null && auraController.TargetMonster == enemy)
                    {
                        hasAura = true;
                        break;
                    }
                }
            }
            
            if (!hasAura)
            {
                SpawnEnemyTargetAura(enemy);
            }
        }
    }
    
    private void SpawnAurasForNewAllies(List<GameObject> currentAllies)
    {
        foreach (var ally in currentAllies)
        {
            // Bu ally için zaten aura var mı?
            bool hasAura = false;
            foreach (var aura in activeAllyAuras)
            {
                if (aura != null)
                {
                    TargetAuraController auraController = aura.GetComponent<TargetAuraController>();
                    if (auraController != null && auraController.TargetMonster == ally)
                    {
                        hasAura = true;
                        break;
                    }
                }
            }
            
            if (!hasAura)
            {
                SpawnAllyTargetAura(ally);
            }
        }
    }
    
    private void SpawnEnemyTargetAura(GameObject enemy)
    {
        Vector3 auraPosition = enemy.transform.position;
        GameObject auraObj = Instantiate(enemyAuraPrefab, auraPosition, Quaternion.identity);
        
        // Enemy aura'sını kırmızı yap
        SpriteRenderer[] renderers = auraObj.GetComponentsInChildren<SpriteRenderer>();
        foreach (var renderer in renderers)
        {
            renderer.color = new Color(1f, 0.3f, 0.3f, renderer.color.a); // Kırmızımsı
        }
        
        // Aura controller ekle
        TargetAuraController controller = auraObj.GetComponent<TargetAuraController>();
        if (controller == null)
        {
            controller = auraObj.AddComponent<TargetAuraController>();
        }
        controller.SetTarget(enemy);
        
        activeEnemyAuras.Add(auraObj);
    }
    
    private void SpawnAllyTargetAura(GameObject ally)
    {
        Vector3 auraPosition = ally.transform.position;
        GameObject auraObj = Instantiate(allyAuraPrefab, auraPosition, Quaternion.identity);
        
        // Ally aura'sını mavi/yeşil yap
        SpriteRenderer[] renderers = auraObj.GetComponentsInChildren<SpriteRenderer>();
        foreach (var renderer in renderers)
        {
            renderer.color = new Color(0.3f, 0.8f, 1f, renderer.color.a); // Mavi
        }
        
        // Aura controller ekle
        TargetAuraController controller = auraObj.GetComponent<TargetAuraController>();
        if (controller == null)
        {
            controller = auraObj.AddComponent<TargetAuraController>();
        }
        controller.SetTarget(ally);
        
        activeAllyAuras.Add(auraObj);
    }
    
    public override void HidePreview()
    {
        base.HidePreview();
        
        // Tüm aura'ları temizle
        foreach (var aura in activeEnemyAuras)
        {
            if (aura != null)
            {
                Destroy(aura);
            }
        }
        activeEnemyAuras.Clear();
        
        foreach (var aura in activeAllyAuras)
        {
            if (aura != null)
            {
                Destroy(aura);
            }
        }
        activeAllyAuras.Clear();
    }
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        // Tüm aura'ları temizle
        foreach (var aura in activeEnemyAuras)
        {
            if (aura != null)
            {
                Destroy(aura);
            }
        }
        activeEnemyAuras.Clear();
        
        foreach (var aura in activeAllyAuras)
        {
            if (aura != null)
            {
                Destroy(aura);
            }
        }
        activeAllyAuras.Clear();
    }
}