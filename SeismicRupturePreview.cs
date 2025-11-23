using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Collections.Generic;

public class SeismicRupturePreview : BaseSkillPreview
{
    public override string SkillId => "seismic_rupture";
    
    private const float CONE_ANGLE = 120f;
    private const float SKILL_RANGE = 6f;
    
    private SpriteRenderer coneRenderer;
    private float pulseTimer = 0f;
    private GameObject coneVisual;
    private List<GameObject> activeTargetAuras = new List<GameObject>();
    private GameObject targetAuraPrefab;
    
    protected override void CreatePreviewObject()
    {
        previewObject = new GameObject("SeismicRupture_Preview");
        previewObject.transform.SetParent(transform);
        
        // Target_Aura prefab'ını yükle
        LoadTargetAuraPrefab();
        
        // Cone outline oluştur
        CreateConeOutline();
    }
    
    private void LoadTargetAuraPrefab()
    {
        targetAuraPrefab = Resources.Load<GameObject>("VFX/Target_Aura");

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
        this.coneVisual = coneVisual;
    }

    private Sprite CreateSmoothConeSprite()
    {
        int resolution = 256;
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        Color[] colors = new Color[resolution * resolution];
        
        Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
        float maxDistance = (resolution / 2f) * 0.9f;
        float halfAngle = CONE_ANGLE / 2f;
        
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                Vector2 pos = new Vector2(x, y);
                Vector2 dir = pos - center;
                float distance = dir.magnitude;
                
                if (distance < 5f)
                {
                    colors[y * resolution + x] = Color.clear;
                    continue;
                }
                
                float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                bool inCone = Mathf.Abs(angle) <= halfAngle && distance <= maxDistance;
                
                if (inCone)
                {
                    float distanceNorm = distance / maxDistance;
                    float angleNorm = Mathf.Abs(angle) / halfAngle;
                    
                    float distanceAlpha = Mathf.Lerp(0.7f, 0.1f, Mathf.Pow(distanceNorm, 0.5f));
                    
                    float edgeSoftness = 0.2f;
                    float angleAlpha = 1f;
                    if (angleNorm > (1f - edgeSoftness))
                    {
                        angleAlpha = Mathf.Lerp(1f, 0f, (angleNorm - (1f - edgeSoftness)) / edgeSoftness);
                    }
                    
                    float finalAlpha = distanceAlpha * angleAlpha;
                    colors[y * resolution + x] = new Color(0.8f, 0.2f, 0.2f, finalAlpha); // Kırmızımsı ultimate skill rengi
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
            new Vector2(0.5f, 0.5f), resolution / (SKILL_RANGE * 2f));
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

        SetConePositionAndRotation(casterPos, direction, coneTransform);
        UpdatePulseAnimation();
        UpdateTargetAuras(caster, casterPos, direction);
    }

    private void SetConePositionAndRotation(Vector3 casterPos, Vector2 direction, Transform coneTransform)
    {
        Vector3 offsetPosition = casterPos + (Vector3)(direction * 0.5f);
        coneTransform.position = offsetPosition;
        
        float angle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
        coneTransform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        
        UpdateSortingOrder(direction);
    }

    private void UpdateSortingOrder(Vector2 direction)
    {
        if (coneRenderer == null) return;
        
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            coneRenderer.sortingOrder = 15;
        }
        else
        {
            coneRenderer.sortingOrder = direction.y > 0 ? 15 : 5;
        }
    }

    private void UpdatePulseAnimation()
    {
        if (coneRenderer == null) return;
        
        pulseTimer += Time.deltaTime * 1.8f;
        float pulse = Mathf.Lerp(0.7f, 1.0f, (Mathf.Sin(pulseTimer) + 1f) / 2f);
        
        Color color = coneRenderer.color;
        color.a = pulse;
        coneRenderer.color = color;
    }

    private void UpdateTargetAuras(GameObject caster, Vector3 origin, Vector2 direction)
    {
        if (targetAuraPrefab == null) return;
        
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
        var targets = new List<GameObject>();

        // Check monsters - Use MonsterManager instead of FindGameObjectsWithTag
        if (MonsterManager.Instance != null)
        {
            var allMonsters = MonsterManager.Instance.GetAllActiveMonsters();

            foreach (var monsterBehaviour in allMonsters)
            {
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                float distance = Vector2.Distance(origin, monsterBehaviour.transform.position);
                if (distance > SKILL_RANGE) continue;

                Vector2 targetDirection = (monsterBehaviour.transform.position - origin).normalized;
                float angle = Vector2.Angle(direction, targetDirection);

                if (angle <= CONE_ANGLE / 2f)
                {
                    Vector2 rayDirection = monsterBehaviour.transform.position - origin;
                    RaycastHit2D hit = Physics2D.Raycast(origin, rayDirection, distance, LayerMask.GetMask("Obstacles"));

                    if (hit.collider == null)
                    {
                        targets.Add(monsterBehaviour.gameObject);
                    }
                }
            }
        }

        return targets;
    }
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        
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