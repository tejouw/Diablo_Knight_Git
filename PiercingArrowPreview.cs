using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Collections.Generic;

public class PiercingArrowPreview : BaseSkillPreview
{
    public override string SkillId => "piercing_arrow";
    
    private const float SKILL_RANGE = 8f; // PiercingArrowExecutor ile aynı
    private const float LINE_WIDTH = 1f; // PiercingArrowExecutor ile aynı
    
    private SpriteRenderer lineRenderer;
    private float pulseTimer = 0f;
    private GameObject lineVisual;
    private List<GameObject> activeTargetAuras = new List<GameObject>();
    private GameObject targetAuraPrefab;
    
    protected override void CreatePreviewObject()
    {
        previewObject = new GameObject("PiercingArrow_Preview");
        previewObject.transform.SetParent(transform);
        
        // Target_Aura prefab'ını yükle
        LoadTargetAuraPrefab();
        
        // Line preview oluştur
        CreateLinePreview();
    }
    
    private void LoadTargetAuraPrefab()
    {
        targetAuraPrefab = Resources.Load<GameObject>("VFX/Target_Aura");

    }
    
    private void CreateLinePreview()
    {
        GameObject lineVisual = new GameObject("LineVisual");
        lineVisual.transform.SetParent(previewObject.transform);
        
        SpriteRenderer lineRenderer = lineVisual.AddComponent<SpriteRenderer>();
        lineRenderer.sprite = CreateLineSprite();
        lineRenderer.material = CreateLineMaterial();
        lineRenderer.sortingLayerName = "UI";
        lineRenderer.sortingOrder = 5;
        
        this.lineRenderer = lineRenderer;
        this.lineVisual = lineVisual;
    }
    
    private Sprite CreateLineSprite()
    {
        int width = Mathf.RoundToInt(SKILL_RANGE * 32); // 32 pixel per unit
        int height = Mathf.RoundToInt(LINE_WIDTH * 32);
        
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] colors = new Color[width * height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float centerY = height / 2f;
                float distanceFromCenter = Mathf.Abs(y - centerY);
                float normalizedDistance = distanceFromCenter / (height / 2f);
                
                // Gradient from center to edges
                float alpha = Mathf.Lerp(0.6f, 0.1f, normalizedDistance);
                
                // Edge softness
                if (normalizedDistance > 0.8f)
                {
                    alpha *= Mathf.Lerp(1f, 0f, (normalizedDistance - 0.8f) / 0.2f);
                }
                
                colors[y * width + x] = new Color(0.2f, 0.8f, 0.2f, alpha); // Yeşil renk (piercing arrow için)
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        
        return Sprite.Create(texture, new Rect(0, 0, width, height), 
            new Vector2(0f, 0.5f), 32f); // Left pivot
    }
    
    private Material CreateLineMaterial()
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
        if (previewObject == null || lineRenderer == null) return;

        Vector3 casterPos = caster.transform.position;
        Transform lineTransform = lineRenderer.transform;

        // Position ve rotation ayarla
        SetLinePositionAndRotation(casterPos, direction, lineTransform);

        // Pulse animasyonu
        UpdatePulseAnimation();

        // Target aura'ları
        UpdateTargetAuras(caster, casterPos, direction);
    }
    
    private void SetLinePositionAndRotation(Vector3 casterPos, Vector2 direction, Transform lineTransform)
    {
        // Karakterin pozisyonundan başlat
        lineTransform.position = casterPos;
        
        // Direction'a göre rotation ayarla
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        lineTransform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        
        UpdateSortingOrder(direction);
    }
    
    private void UpdateSortingOrder(Vector2 direction)
    {
        if (lineRenderer == null) return;
        
        // Direction'a göre sorting order ayarla
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            // Sağ veya sol - önde olmalı
            lineRenderer.sortingOrder = 15;
        }
        else
        {
            // Yukarı veya aşağı
            lineRenderer.sortingOrder = direction.y > 0 ? 15 : 5;
        }
    }
    
    private void UpdatePulseAnimation()
    {
        if (lineRenderer == null) return;
        
        pulseTimer += Time.deltaTime * 2f; // Hızlı pulse
        float pulse = Mathf.Lerp(0.7f, 1.0f, (Mathf.Sin(pulseTimer) + 1f) / 2f);
        
        Color color = lineRenderer.color;
        color.a = pulse;
        lineRenderer.color = color;
    }
    
    private void UpdateTargetAuras(GameObject caster, Vector3 origin, Vector2 direction)
    {
        if (targetAuraPrefab == null) return;
        
        // Mevcut target'ları bul
        var currentTargets = FindTargetsInLine(caster, origin, direction);
        
        // Eski aura'ları temizle
        ClearUnusedAuras(currentTargets);
        
        // Yeni target'lar için aura spawn et
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
    
    private List<GameObject> FindTargetsInLine(GameObject caster, Vector3 origin, Vector2 direction)
    {
        var targets = new List<GameObject>();
        Vector3 endPoint = origin + (Vector3)(direction * SKILL_RANGE);

        // Monster'ları bul - Use MonsterManager instead of FindGameObjectsWithTag
        if (MonsterManager.Instance != null)
        {
            var allMonsters = MonsterManager.Instance.GetAllActiveMonsters();

            foreach (var monsterBehaviour in allMonsters)
            {
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                // Line üzerinde mi kontrol et
                if (IsTargetOnLinePath(origin, endPoint, monsterBehaviour.transform.position))
                {
                    // Line of sight kontrolü
                    Vector2 rayDirection = monsterBehaviour.transform.position - origin;
                    float distance = rayDirection.magnitude;
                    RaycastHit2D hit = Physics2D.Raycast(origin, rayDirection.normalized, distance, LayerMask.GetMask("Obstacles"));

                    if (hit.collider == null)
                    {
                        targets.Add(monsterBehaviour.gameObject);
                    }
                }
            }
        }
        
        // PvP player'ları kontrol et
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in allPlayers)
        {
            if (player == caster) continue;
            
            // Valid PvP target kontrolü
            if (!IsValidPvPTarget(caster, player)) continue;
            
            if (IsTargetOnLinePath(origin, endPoint, player.transform.position))
            {
                Vector2 rayDirection = player.transform.position - origin;
                float distance = rayDirection.magnitude;
                RaycastHit2D hit = Physics2D.Raycast(origin, rayDirection.normalized, distance, LayerMask.GetMask("Obstacles"));
                
                if (hit.collider == null)
                {
                    targets.Add(player);
                }
            }
        }
        
        return targets;
    }
    
    private bool IsTargetOnLinePath(Vector3 startPos, Vector3 endPos, Vector3 targetPos)
    {
        // PiercingArrowExecutor'daki aynı logic
        Vector3 lineDirection = (endPos - startPos).normalized;
        float lineLength = Vector3.Distance(startPos, endPos);
        
        Vector3 toTarget = targetPos - startPos;
        float projectionLength = Vector3.Dot(toTarget, lineDirection);
        
        // Check if target is within line segment
        if (projectionLength < 0 || projectionLength > lineLength)
            return false;
        
        // Calculate perpendicular distance
        Vector3 projectionPoint = startPos + lineDirection * projectionLength;
        float perpendicularDistance = Vector3.Distance(targetPos, projectionPoint);
        
        // Hit threshold - PiercingArrowExecutor ile aynı
        return perpendicularDistance <= LINE_WIDTH;
    }
    
// PiercingArrowPreview.cs - Bu metodu değiştir
private bool IsValidPvPTarget(GameObject caster, GameObject target)
{
    var pvpSystem = caster.GetComponent<PVPSystem>();
    var targetPvpSystem = target.GetComponent<PVPSystem>();
    var targetStats = target.GetComponent<PlayerStats>();
    
    if (pvpSystem == null || targetPvpSystem == null || targetStats == null)
    {
        return false;
    }
    
    // Güvenli PVP status kontrolü
    bool casterCanAttack = pvpSystem.CanAttackPlayers();
    bool targetInPVP = targetPvpSystem.GetSafePVPStatus();
    bool targetAlive = !targetStats.IsDead;
    
    return casterCanAttack && targetInPVP && targetAlive;
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