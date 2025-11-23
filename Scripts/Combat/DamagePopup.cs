// Path: Assets/Game/Scripts/DamagePopup.cs

using UnityEngine;
using TMPro;
using Fusion;

public class DamagePopup : NetworkBehaviour
{
    [SerializeField] private TextMeshPro damageText;
    [SerializeField] private float floatSpeed = 1f;
    [SerializeField] private float fadeSpeed = 1f;

    public enum DamageType
    {
        Normal,
        Critical,
        Received,
        Miss,
        SkillDamage
    }

    private static readonly Color normalColor = Color.white;        
    private static readonly Color criticalColor = Color.yellow;     
    private static readonly Color receivedColor = Color.red;  
    private static readonly Color missColor = Color.gray;
    private static readonly Color skillDamageColor = new Color(1f, 0.5f, 0f); // Turuncu - skill damage için

    private float alpha = 1f;

    private void Awake()
    {
        // DamageText component'ini oluştur
        GameObject textObj = new GameObject("DamageText");
        textObj.transform.SetParent(transform, false);
        damageText = textObj.AddComponent<TextMeshPro>();
        
        // TextMeshPro ayarları
        damageText.fontSize = 6;
        damageText.alignment = TextAlignmentOptions.Center;
        
        // Renderer üzerinden sorting ayarla
        Renderer textRenderer = damageText.GetComponent<Renderer>();
        if (textRenderer != null)
        {
            textRenderer.sortingOrder = 15;
        }
    }

    public void Setup(float damageAmount, DamageType type)
    {
        if (damageText == null)
        {
            Destroy(gameObject);
            return;
        }
        
        // Miss durumunda özel text
        if (type == DamageType.Miss)
        {
            damageText.text = "ISKALADI";
        }
        else
        {
            damageText.text = Mathf.Round(damageAmount).ToString();
        }
        
        // Renk ayarları
        damageText.color = type switch
        {
            DamageType.Normal => normalColor,
            DamageType.Critical => criticalColor,
            DamageType.Received => receivedColor,
            DamageType.Miss => missColor,
            DamageType.SkillDamage => skillDamageColor,
            _ => normalColor
        };

        // Scale ayarları - Skill damage 2 katı büyük
        float scale = type switch
        {
            DamageType.Critical => 2.5f,
            DamageType.SkillDamage => 3.0f,  // Skill damage 2 katı büyük
            DamageType.Miss => 1.7f,
            _ => 1.5f
        };
        
        transform.localScale = Vector3.one * scale;

        float randomX = UnityEngine.Random.Range(-1f, 1f);
        transform.position += new Vector3(randomX, 0.5f, 0f);
        
        // Miss ve Skill damage için biraz daha uzun süre göster
        float destroyTime = (type == DamageType.Miss || type == DamageType.SkillDamage) ? 2.5f : 2f;
        Destroy(gameObject, destroyTime);
    }

    private void Update()
    {
        if (damageText == null)
        {
            Destroy(gameObject);
            return;
        }
        
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        alpha -= fadeSpeed * Time.deltaTime;
        damageText.color = new Color(damageText.color.r, damageText.color.g, damageText.color.b, alpha);
    }

    public static void Create(Vector3 position, float damage, DamageType type)
    {
        GameObject damagePopupObj = new GameObject("DamagePopup");
        damagePopupObj.transform.position = position;
        
        DamagePopup damagePopup = damagePopupObj.AddComponent<DamagePopup>();
        damagePopup.Setup(damage, type);
    }
}