// Path: Assets/Game/Scripts/BotManager.cs

using UnityEngine;
using UnityEngine.UI;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.Common;

public class BotManager : MonoBehaviour // NetworkBehaviour değil, MonoBehaviour
{
    public static BotManager Instance;
    
    [Header("Bot Settings")]
    [SerializeField] private Button createBotButton;
    [SerializeField] private int maxBots = 10;
    [SerializeField] private float createBotDelay = 2f;
    [SerializeField] private string[] botPrefixNames = { "Bot_", "NPC_", "Player_" };

    [Header("Bot Behavior Settings")]
    [SerializeField] private float botMoveSpeed = 2f; 
    [SerializeField] private float botDetectionRange = 8f;
    [SerializeField] private float botAttackRange = 2f;
    [SerializeField] private float botPatrolRadius = 5f;
    [SerializeField] private float botLeashRange = 15f;
    [SerializeField] private float botPatrolWaitTime = 3f;
    [SerializeField] private float botFleeHealthPercent = 0.3f;
    [SerializeField] private float botAttackDelay = 1.5f;
    [SerializeField] private bool botIsAggressive = true;
    [SerializeField] private bool botIsPatrolling = true;
    
    [Header("Default Character")]
    [SerializeField] private TextAsset defaultCharacterJson;
    
    private List<NetworkObject> activeBots = new List<NetworkObject>();
    private bool isCreatingBot = false;
    private NetworkRunner runner; // Runner referansı
    
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
    
    private void Start()
    {
        StartCoroutine(DelayedStart());
    }

private IEnumerator DelayedStart()
{
    
    float waitTime = 0;
    int attemptCount = 0;
    
    while (waitTime < 30f) // 30 saniye timeout
    {
        attemptCount++;
        
        // NetworkManager kontrolü
        if (NetworkManager.Instance == null)
        {
            yield return new WaitForSeconds(1f); // 1 saniye bekle
            waitTime += 1f;
            continue;
        }
        
        // Runner kontrolü
        if (NetworkManager.Instance.Runner == null)
        {
            yield return new WaitForSeconds(1f);
            waitTime += 1f;
            continue;
        }
        
        // GameMode kontrolü
        runner = NetworkManager.Instance.Runner;
        if (runner.GameMode == GameMode.Host)
        {
            break;
        }
        else
        {
            yield return new WaitForSeconds(1f);
            waitTime += 1f;
            continue;
        }
    }
    
    // Final kontrol
    if (waitTime >= 30f)
    {
        Debug.LogError($"[BotManager] 30 saniye timeout! Son durum - NetworkManager: {(NetworkManager.Instance != null)}, Runner: {(NetworkManager.Instance?.Runner != null)}, GameMode: {NetworkManager.Instance?.Runner?.GameMode}");
        
        // Yine de button state'i kontrol et (Client olarak bağlanmış olabilir)
        if (NetworkManager.Instance?.Runner != null)
        {
            runner = NetworkManager.Instance.Runner;
        }
    }
    
    yield return new WaitForSeconds(0.5f);
    SetButtonState();
}

private void SetButtonState()
{
    
    if (createBotButton == null) 
    {
        Debug.LogError("[BotManager] createBotButton NULL! Inspector'da assign edilmemiş.");
        return;
    }
    
    // SADECE HOST için button aktif
    bool shouldBeActive = runner != null && runner.GameMode == GameMode.Host;
    
    if (shouldBeActive)
    {
        createBotButton.onClick.RemoveAllListeners();
        createBotButton.onClick.AddListener(CreateBotRequest);
        createBotButton.gameObject.SetActive(true);
    }
    else
    {
        createBotButton.gameObject.SetActive(false);
    }
}
public void CreateBotRequest()
{
    
    if (runner == null || runner.GameMode != GameMode.Host)
    {
        Debug.LogError("[BotManager] CreateBotRequest REJECTED - Runner null: " + (runner == null) + ", GameMode: " + runner?.GameMode);
        return;
    }
            
    if (isCreatingBot)
    {
        Debug.LogWarning("[BotManager] CreateBotRequest REJECTED - Zaten bot oluşturuluyor");
        return;
    }
            
    if (activeBots.Count >= maxBots)
    {
        Debug.LogWarning("[BotManager] CreateBotRequest REJECTED - Max bot sayısına ulaşıldı: " + activeBots.Count + "/" + maxBots);
        return;
    }
    
    StartCoroutine(CreateBotCoroutine());
}

    
    private IEnumerator CreateBotCoroutine()
    {
        isCreatingBot = true;
        
        // Rastgele bot adı oluştur
        string botPrefix = botPrefixNames[Random.Range(0, botPrefixNames.Length)];
        string botName = botPrefix + Random.Range(1000, 9999);
        
        // Default karakter JSONını kullan veya oyuncunun kendi karakter JSONını al
        string characterJson = GetDefaultCharacterJson();
        if (string.IsNullOrEmpty(characterJson))
        {
            Debug.LogError("[BotManager] No default character JSON found!");
            isCreatingBot = false;
            yield break;
        }
        
        // Bot verilerini Firebase'e kaydet
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
        {
            // Karakter verilerini kaydet
            yield return StartCoroutine(SaveCharacterDataCoroutine(botName, characterJson));
            
            // Kullanıcı verisini PlayerPrefs'e kaydet
            PlayerPrefs.SetString("CharacterData_" + botName, characterJson);
            PlayerPrefs.Save();
        }
        else
        {
            // Sadece PlayerPrefs'e kaydet
            PlayerPrefs.SetString("CharacterData_" + botName, characterJson);
            PlayerPrefs.Save();
        }
        
        // Spawning için biraz bekleyelim
        yield return new WaitForSeconds(createBotDelay);
        
        // Botu spawn et
        SpawnBot(botName, characterJson);
        
        isCreatingBot = false;
    }
    
private string GetDefaultCharacterJson()
{
    // Önce TextAsset'ten karakter verisini kullan
    if (defaultCharacterJson != null)
    {
        return defaultCharacterJson.text;
    }
    
    // Sabit ve doğru formatta bir JSON string döndür
    return @"{""Body"":""Basic.HumanMale"",""Ears"":""Basic.HumanMale"",""Hair"":""Basic.Casual8"",""Beard"":"""",""Helmet"":"""",""Armor"":""Basic.ChainmailArmor"",""PrimaryWeapon"":""Basic.Melee2H.Katana"",""SecondaryWeapon"":"""",""Cape"":"""",""Backpack"":"""",""Shield"":"""",""Bow"":"""",""WeaponType"":""Melee2H"",""Expression"":""Default"",""Expression.Default.Eyebrows"":""Basic.Eyebrows1"",""Expression.Default.Eyes"":""Basic.Eyes1"",""Expression.Default.EyesColor"":""#0000FFFF"",""Expression.Default.Mouth"":""Basic.Mouth1"",""Expression.Angry.Eyebrows"":""Basic.Eyebrows3"",""Expression.Angry.Eyes"":""Basic.Eyes1"",""Expression.Angry.EyesColor"":""#0000FFFF"",""Expression.Angry.Mouth"":""Basic.Mouth3"",""Expression.Dead.Eyebrows"":""Basic.Eyebrows4"",""Expression.Dead.Eyes"":""Basic.Eyes4"",""Expression.Dead.EyesColor"":""#0000FFFF"",""Expression.Dead.Mouth"":""Basic.Mouth4"",""HideEars"":""False"",""CropHair"":""False"",""Makeup"":"""",""Mask"":"""",""Earrings"":""""}";
}
    
    private IEnumerator SaveCharacterDataCoroutine(string botName, string characterJson)
    {
        bool isDone = false;
        string errorMessage = null;
        
        FirebaseManager.Instance.SaveCharacterData(botName, characterJson)
            .ContinueWith(task => {
                isDone = true;
                if (task.IsFaulted)
                {
                    errorMessage = task.Exception?.Message;
                    Debug.LogError("[BotManager] Firebase save task faulted: " + errorMessage);
                }
                else
                {
                }
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        
        // İşlem tamamlanana kadar bekle
        while (!isDone)
        {
            yield return null;
        }
        
        if (!string.IsNullOrEmpty(errorMessage))
        {
            Debug.LogError($"[BotManager] Bot karakteri kaydedilemedi: {errorMessage}");
        }
        else
        {
        }
    }
    
// DEĞİŞEN METODLAR:

private void SpawnBot(string botName, string characterJson)
{
    
    if (runner == null || runner.GameMode != GameMode.Host)
    {
        Debug.LogError("[BotManager] SpawnBot REJECTED - Runner null: " + (runner == null) + ", GameMode: " + runner?.GameMode);
        return;
    }
    
    // Rasgele ırk seç
    PlayerRace randomRace = (PlayerRace)Random.Range(0, System.Enum.GetValues(typeof(PlayerRace)).Length);
    
    // NetworkManager'dan doğru prefab'ı al
    NetworkObject playerPrefab = GetBotPrefab(randomRace);
    if (playerPrefab == null)
    {
        Debug.LogError("[BotManager] " + randomRace + " için prefab bulunamadı!");
        return;
    }
    
    Vector2 spawnPos = CalculateRandomSpawnPosition();
    
    try
    {
        NetworkObject botObj = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity);
        
        if (botObj == null)
        {
            Debug.LogError("[BotManager] Runner.Spawn null döndü!");
            return;
        }
        
        
        PlayerSetup playerSetup = botObj.GetComponent<PlayerSetup>();
        if (playerSetup != null)
        {
            playerSetup.SetBotFlagRPC(true);
        }
        else
        {
            Debug.LogError("[BotManager] PlayerSetup component bulunamadı!");
        }
        
        StartCoroutine(ApplyBotAppearanceCoroutine(botObj.gameObject, botName, characterJson));
        activeBots.Add(botObj);
    }
    catch (System.Exception e)
    {
        Debug.LogError("[BotManager] SpawnBot hatası: " + e.Message + "\n" + e.StackTrace);
    }
}

// Yeni metod ekle
private NetworkObject GetBotPrefab(PlayerRace race)
{
    if (NetworkManager.Instance == null)
    {
        Debug.LogError("[BotManager] NetworkManager.Instance null!");
        return null;
    }
    
    // NetworkManager'ın GetPlayerPrefab metodunu kullan
    NetworkObject prefab = null;
    
    switch (race)
    {
        case PlayerRace.Human:
            // NetworkManager'dan human prefab'ı al
            prefab = GetHumanPrefabFromNetworkManager();
            break;
        case PlayerRace.Goblin:
            // NetworkManager'dan goblin prefab'ı al
            prefab = GetGoblinPrefabFromNetworkManager();
            break;
        default:
            Debug.LogWarning("[BotManager] Bilinmeyen ırk: " + race + ", Human kullanılıyor");
            prefab = GetHumanPrefabFromNetworkManager();
            break;
    }
    
    if (prefab == null)
    {
        Debug.LogError("[BotManager] " + race + " prefab'ı NetworkManager'da bulunamadı!");
    }
    
    return prefab;
}

// NetworkManager'dan prefab'ları almak için helper metodlar
private NetworkObject GetHumanPrefabFromNetworkManager()
{
    // Reflection ile private field'a erişim (geçici çözüm)
    var field = typeof(NetworkManager).GetField("humanPlayerPrefab", 
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    
    if (field != null)
    {
        return field.GetValue(NetworkManager.Instance) as NetworkObject;
    }
    
    Debug.LogError("[BotManager] humanPlayerPrefab field bulunamadı!");
    return null;
}

private NetworkObject GetGoblinPrefabFromNetworkManager()
{
    // Reflection ile private field'a erişim (geçici çözüm)
    var field = typeof(NetworkManager).GetField("goblinPlayerPrefab", 
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    
    if (field != null)
    {
        return field.GetValue(NetworkManager.Instance) as NetworkObject;
    }
    
    Debug.LogError("[BotManager] goblinPlayerPrefab field bulunamadı!");
    return null;
}
    
private IEnumerator ApplyBotAppearanceCoroutine(GameObject botObj, string botName, string characterJson)
{
    yield return new WaitForSeconds(0.5f);
    
    if (botObj == null)
    {
        Debug.LogError("[BotManager] Bot object is null");
        yield break;
    }
    
    Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D character4D = null;
    NetworkObject networkObj = null;
    BotController botController = null;
    
    try
    {
        character4D = botObj.GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
        networkObj = botObj.GetComponent<NetworkObject>();
        botController = botObj.GetComponent<BotController>();
    }
    catch (System.Exception e)
    {
        Debug.LogError("[BotManager] Error getting components: " + e.Message);
        yield break;
    }
    
    if (character4D == null)
    {
        Debug.LogError("[BotManager] Character4D component not found on bot object");
        yield break;
    }
    
    try
    {
        character4D.Initialize();
        character4D.WeaponType = Assets.HeroEditor4D.Common.Scripts.Enums.WeaponType.Melee2H;
        character4D.SetDirection(Vector2.down);
        
        // Vücut, saç, zırh ve silahı rasgele ayarla
        try 
        {
            // Rastgele vücut seçimi
            if (character4D.SpriteCollection.Body != null && character4D.SpriteCollection.Body.Count > 0)
            {
                int randomBodyIndex = UnityEngine.Random.Range(0, character4D.SpriteCollection.Body.Count);
                var randomBody = character4D.SpriteCollection.Body[randomBodyIndex];
                if (randomBody != null)
                {
                    character4D.Body = randomBody.Sprites;
                }
            }
            
            // Rastgele kulak seçimi
            if (character4D.SpriteCollection.Ears != null && character4D.SpriteCollection.Ears.Count > 0)
            {
                int randomEarsIndex = UnityEngine.Random.Range(0, character4D.SpriteCollection.Ears.Count);
                var randomEars = character4D.SpriteCollection.Ears[randomEarsIndex];
                if (randomEars != null)
                {
                    character4D.Ears = randomEars.Sprites;
                }
            }
            
            // Rastgele saç seçimi
            if (character4D.SpriteCollection.Hair != null && character4D.SpriteCollection.Hair.Count > 0)
            {
                int randomHairIndex = UnityEngine.Random.Range(0, character4D.SpriteCollection.Hair.Count);
                var randomHair = character4D.SpriteCollection.Hair[randomHairIndex];
                if (randomHair != null)
                {
                    character4D.Hair = randomHair.Sprites;
                }
            }
            
            
            // Rastgele zırh seçimi
            if (character4D.SpriteCollection.Armor != null && character4D.SpriteCollection.Armor.Count > 0)
            {
                int randomArmorIndex = UnityEngine.Random.Range(0, character4D.SpriteCollection.Armor.Count);
                var randomArmor = character4D.SpriteCollection.Armor[randomArmorIndex];
                if (randomArmor != null)
                {
                    character4D.Armor = randomArmor.Sprites;
                }
            }
            
            // Rastgele iki elli silah seçimi
            if (character4D.SpriteCollection.MeleeWeapon2H != null && character4D.SpriteCollection.MeleeWeapon2H.Count > 0)
            {
                int randomWeaponIndex = UnityEngine.Random.Range(0, character4D.SpriteCollection.MeleeWeapon2H.Count);
                var randomWeapon = character4D.SpriteCollection.MeleeWeapon2H[randomWeaponIndex];
                if (randomWeapon != null)
                {
                    character4D.PrimaryWeapon = randomWeapon.Sprite;
                    
                    // ShortSword hatası gelmemesi için kalkan ve diğer silah tiplerini temizle
                    character4D.Shield = new List<Sprite>();
                    character4D.SecondaryWeapon = null;
                }
            }
            if (character4D.SpriteCollection.Mask != null && character4D.SpriteCollection.Mask.Count > 0)

    // Maske kullanılsın mı - %50 şans
if (character4D.SpriteCollection.Armor != null && character4D.SpriteCollection.Armor.Count > 0)
{
    // Kask giyilsin mi - %50 şans
    if (UnityEngine.Random.value > 0.5f)
    {
        int randomHelmetIndex = UnityEngine.Random.Range(0, character4D.SpriteCollection.Armor.Count);
        var randomHelmet = character4D.SpriteCollection.Armor[randomHelmetIndex];
        if (randomHelmet != null)
        {
        }
    }
    else
    {
        character4D.Helmet = null;
    }
}

// Rastgele maske seçimi - tamamen ayrı bir seçim olarak
if (character4D.SpriteCollection.Mask != null && character4D.SpriteCollection.Mask.Count > 0)
{
    // Maske kullanılsın mı - %30 şans
    if (UnityEngine.Random.value > 0.7f)
    {
        int randomMaskIndex = UnityEngine.Random.Range(0, character4D.SpriteCollection.Mask.Count);
        var randomMask = character4D.SpriteCollection.Mask[randomMaskIndex];
        if (randomMask != null && character4D.Parts != null && character4D.Parts.Count > 0)
        {
            
            // Tüm parçalardaki maske rendererları için
            foreach (var part in character4D.Parts)
            {
                if (part != null && part.MaskRenderer != null)
                {
                }
            }
        }
    }
    else
    {
        // Maskeyi temizle
        foreach (var part in character4D.Parts)
        {
            if (part != null)
            {
                part.Mask = null;
            }
        }
    }
}

            // Rastgele göz rengi
            if (character4D.Parts != null && character4D.Parts.Count > 0)
            {
                // Rastgele bir renk oluştur
                Color randomEyeColor = new Color(
                    UnityEngine.Random.value,  // R
                    UnityEngine.Random.value,  // G
                    UnityEngine.Random.value,  // B
                    1f                         // A
                );
                
                
                foreach (var part in character4D.Parts)
                {
                    if (part != null && part.Expressions != null && part.Expressions.Count > 0)
                    {
                        foreach (var expression in part.Expressions)
                        {
                            if (expression != null && expression.Name != "Dead")
                            {
                                expression.EyesColor = randomEyeColor;
                            }
                        }
                    }
                }
            }
            
            // Saç ve vücut için rastgele bir renk tonu
            if (character4D.Parts != null)
            {
                // Saç için rastgele renk
                Color hairColor = new Color(
                    UnityEngine.Random.Range(0.1f, 0.9f),
                    UnityEngine.Random.Range(0.1f, 0.9f),
                    UnityEngine.Random.Range(0.1f, 0.9f),
                    1f
                );
                
                // Ten rengi için daha doğal bir renk aralığı
                Color skinColor = new Color(
                    UnityEngine.Random.Range(0.5f, 0.95f),  // daha çok kırmızı tonu
                    UnityEngine.Random.Range(0.4f, 0.85f),  // daha az yeşil
                    UnityEngine.Random.Range(0.3f, 0.8f),   // daha az mavi
                    1f
                );
                
                
                foreach (var part in character4D.Parts)
                {
                    if (part != null)
                    {
                        // Saç rengi ayarla
                        if (part.HairRenderer != null)
                            part.HairRenderer.color = hairColor;
                        
                        // Ten rengi ayarla
                        if (part.BodyRenderers != null)
                        {
                            foreach (var bodyRenderer in part.BodyRenderers)
                            {
                                if (bodyRenderer != null)
                                    bodyRenderer.color = skinColor;
                            }
                        }
                        
                        // Kulak rengi ayarla (ten rengiyle aynı)
                        if (part.EarsRenderers != null)
                        {
                            foreach (var earRenderer in part.EarsRenderers)
                            {
                                if (earRenderer != null)
                                    earRenderer.color = skinColor;
                            }
                        }
                        
                        // Baş rengi ayarla (ten rengiyle aynı)
                        if (part.HeadRenderer != null)
                            part.HeadRenderer.color = skinColor;
                    }
                }
            }
            
            // Tüm değişiklikleri uygulamak için tekrar initialize et
            character4D.Initialize();
            
        }
        catch (System.Exception e)
        {
            Debug.LogError("[BotManager] Error setting random character appearance: " + e.Message);
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError("[BotManager] Error initializing character: " + e.Message);
    }
    
    // Bot davranış ayarlarını yapılandır
if (botController != null)
{
             
    botController.ConfigureBehaviorSettings(
        botMoveSpeed,
        botDetectionRange,
        botAttackRange,
        botPatrolRadius,
        botLeashRange,
        botPatrolWaitTime,
        botFleeHealthPercent,
        botAttackDelay,
        botIsAggressive,
        botIsPatrolling
    );
}

    // Diğer istemcilere görünümü ilet
    if (networkObj != null)
    {
        try
        {
            
            // Önce tüm kritik bileşenleri kontrol edelim
            foreach (var part in character4D.Parts)
            {
                if (part.Shield != null && part.Shield.Count > 0)
                {
                    part.Shield = new List<Sprite>();
                }
                
                // ShortSword referanslarını temizle
                if (part.WeaponType != Assets.HeroEditor4D.Common.Scripts.Enums.WeaponType.Melee2H)
                {
                    part.WeaponType = Assets.HeroEditor4D.Common.Scripts.Enums.WeaponType.Melee2H;
                }
            }
            
            // Animator'e silah tipini bildir
            if (character4D.Animator != null)
            {
                character4D.Animator.SetInteger("WeaponType", (int)Assets.HeroEditor4D.Common.Scripts.Enums.WeaponType.Melee2H);
            }
            
            // Temizleme sonrası tekrar initialize et
            character4D.Initialize();
            
            // Şimdi senkronize et
            string currentJson = character4D.ToJson();
            if (!string.IsNullOrEmpty(currentJson))
            {
                SyncCharacterRPC(currentJson);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[BotManager] Error syncing character via RPC: " + e.Message);
        }
    }
}
    
    private Vector2 CalculateRandomSpawnPosition()
    {
        float spawnRadius = 5f;
        float randomAngle = Random.Range(0f, 360f);
        
        return new Vector2(
            Mathf.Cos(randomAngle * Mathf.Deg2Rad) * spawnRadius,
            Mathf.Sin(randomAngle * Mathf.Deg2Rad) * spawnRadius
        );
    }
    
public void RemoveBot(NetworkObject botNetworkObj)
{
    if (runner == null || runner.GameMode != GameMode.Host)
    {
        Debug.LogWarning("[BotManager] RemoveBot REJECTED - Host değil");
        return;
    }
            
    if (activeBots.Contains(botNetworkObj))
    {
        activeBots.Remove(botNetworkObj);
        runner.Despawn(botNetworkObj);
    }
}

public void SyncCharacterRPC(string characterJson)
{
    var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
    if (character4D != null)
    {
        try
        {
            character4D.FromJson(characterJson, silent: true);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[BotManager] Error applying character from JSON: " + e.Message);
        }
    }
}


}