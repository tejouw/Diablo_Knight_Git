using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Collections.Generic;

public class RainOfArrowsPreview : BaseSkillPreview
{
    public override string SkillId => "rain_of_arrows";
    
    private const float AOE_RADIUS = 4f; // 2 tile çapında
    private const float CAST_RANGE = 6f; // Oyuncunun 6 unit önünde
    
    private SpriteRenderer circleRenderer;
    private float pulseTimer = 0f;
    private GameObject circleVisual;
    private List<GameObject> activeTargetAuras = new List<GameObject>();
    private GameObject targetAuraPrefab;
    
    protected override void CreatePreviewObject()
    {
        previewObject = new GameObject("RainOfArrows_Preview");
        previewObject.transform.SetParent(transform);
        
        // Target_Aura prefab'ını yükle
        LoadTargetAuraPrefab();
        
        // Circle preview oluştur
        CreateCirclePreview();
    }
    
    private void LoadTargetAuraPrefab()
    {
        targetAuraPrefab = Resources.Load<GameObject>("VFX/Target_Aura");

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
        float maxRadius = (resolution / 2f) * 0.9f; // %90'ını kullan, kenar yumuşaklığı için
        
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
                    float distanceAlpha = Mathf.Lerp(0.6f, 0.1f, Mathf.Pow(distanceNorm, 0.3f));
                    
                    // Kenar yumuşaklığı
                    float edgeSoftness = 0.15f;
                    if (distanceNorm > (1f - edgeSoftness))
                    {
                        float edgeAlpha = Mathf.Lerp(1f, 0f, (distanceNorm - (1f - edgeSoftness)) / edgeSoftness);
                        distanceAlpha *= edgeAlpha;
                    }
                    
                    colors[y * resolution + x] = new Color(1f, 0.3f, 0.3f, distanceAlpha); // Kırmızımsı ultimate rengi
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
        
        // AOE center hesapla (oyuncunun önünde)
        Vector3 aoeCenter = CalculateAOECenter(casterPos, direction);
        
        // Circle'ı position'la
        SetCirclePosition(aoeCenter);

        // Pulse animasyonu
        UpdatePulseAnimation();

        // Target aura'ları
        UpdateTargetAuras(caster, aoeCenter);
    }
    
    private Vector3 CalculateAOECenter(Vector3 casterPosition, Vector2 direction)
    {
        return casterPosition + (Vector3)(direction.normalized * CAST_RANGE);
    }
    
    private void SetCirclePosition(Vector3 aoeCenter)
    {
        Transform circleTransform = circleRenderer.transform;
        circleTransform.position = aoeCenter;
        
        // Circle için rotation gerekmiyor, her zaman aynı
        circleRenderer.sortingOrder = 10; // Hep üstte görünsün
    }
    
    private void UpdatePulseAnimation()
    {
        if (circleRenderer == null) return;
        
        pulseTimer += Time.deltaTime * 2f; // Hızlı pulse (ultimate skill)
        float pulse = Mathf.Lerp(0.7f, 1.0f, (Mathf.Sin(pulseTimer) + 1f) / 2f);
        
        Color color = circleRenderer.color;
        color.a = pulse;
        circleRenderer.color = color;
    }
    
    private void UpdateTargetAuras(GameObject caster, Vector3 aoeCenter)
    {
        if (targetAuraPrefab == null) return;
        
        // Mevcut target'ları bul
        var currentTargets = FindTargetsInCircleArea(aoeCenter, AOE_RADIUS, caster);
        
        // Eski aura'ları temizle
        ClearUnusedAuras(currentTargets);
        
        // Yeni target'lar için aura spawn et
        SpawnAurasForNewTargets(currentTargets);
    }
    
    private List<GameObject> FindTargetsInCircleArea(Vector3 center, float radius, GameObject caster)
    {
        var targets = new List<GameObject>();

        // Check monsters - Use MonsterManager instead of FindGameObjectsWithTag
        if (MonsterManager.Instance != null)
        {
            var allMonsters = MonsterManager.Instance.GetAllActiveMonsters();

            foreach (var monsterBehaviour in allMonsters)
            {
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                float distance = Vector2.Distance(center, monsterBehaviour.transform.position);
                if (distance <= radius)
                {
                    targets.Add(monsterBehaviour.gameObject);
                }
            }
        }
        
        // Check players in PvP (RainOfArrowsExecutor'daki IsValidTarget mantığıyla)
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in allPlayers)
        {
            if (player == caster) continue;
            
            // Valid PvP target kontrolü
            if (!IsValidPvPTarget(caster, player)) continue;
            
            float distance = Vector2.Distance(center, player.transform.position);
            if (distance <= radius)
            {
                targets.Add(player);
            }
        }
        
        return targets;
    }
    
private bool IsValidPvPTarget(GameObject caster, GameObject target)
{
    var pvpSystem = caster.GetComponent<PVPSystem>();
    var targetPvpSystem = target.GetComponent<PVPSystem>();
    var targetStats = target.GetComponent<PlayerStats>();
    
    if (pvpSystem != null && targetPvpSystem != null && targetStats != null)
    {
        // Güvenli PVP status kontrolü
        bool casterCanAttack = pvpSystem.CanAttackPlayers();
        bool targetInPVP = targetPvpSystem.GetSafePVPStatus(); // Bu satır değişti
        bool targetAlive = !targetStats.IsDead;
        
        return casterCanAttack && targetInPVP && targetAlive;
    }
    
    return false;
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
        Vector3 auraPosition = target.transform.position;
        GameObject auraObj = Instantiate(targetAuraPrefab, auraPosition, Quaternion.identity);
        
        // Aura controller ekle
        TargetAuraController controller = auraObj.GetComponent<TargetAuraController>();
        if (controller == null)
        {
            controller = auraObj.AddComponent<TargetAuraController>();
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