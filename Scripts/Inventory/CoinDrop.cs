using UnityEngine;
using System.Collections;
using Fusion;
using TMPro;

public class CoinDrop : NetworkBehaviour
{
    [Header("Coin Settings")]
    [SerializeField] private float autoCollectDelay = 2f;
    [SerializeField] private SpriteRenderer coinRenderer;

    // Sprite'ları Resources'dan yükleyeceğiz
    private Sprite smallGoldSprite;
    private Sprite mediumGoldSprite;
    private Sprite largeGoldSprite;

    [Networked] public int CoinAmount { get; set; }
    [Networked] public int OwnerActorNumber { get; set; }
    [Networked, Capacity(8)] public NetworkArray<PlayerRef> AuthorizedPlayers => default;
    [Networked] public PlayerRef NetworkOwnerPlayer { get; set; }  // int yerine PlayerRef
    [Header("Text Settings")]
    [SerializeField] private TMP_FontAsset goldFont;
    [SerializeField] private float textSize = 6f;
    [SerializeField] private Color goldTextColor = Color.yellow;

    private TextMeshPro coinText;
    private bool isCollected = false;

    private void Awake()
    {
        if (coinRenderer == null)
        {
            coinRenderer = GetComponent<SpriteRenderer>();
        }

        // Sprite'ları yükle
        LoadSprites();
    }

    public void SetAuthorizedPlayersOnSpawn(PlayerRef[] recipients)
    {
        if (!Object.HasStateAuthority) return;
        
        // Spawn anında authorized players'ı ayarla
        for (int i = 0; i < recipients.Length && i < AuthorizedPlayers.Length; i++)
        {
            AuthorizedPlayers.Set(i, recipients[i]);
        }
        
        // Interest ayarını hemen yap
        SetCoinInterest(recipients);
    }

    private void SetCoinInterest(PlayerRef[] recipients)
    {
        if (!Object.HasStateAuthority) return;
        
        // Sadece authorized player'lar için interest set et
        foreach (var recipient in recipients)
        {
            if (recipient != PlayerRef.None)
            {
                Object.SetPlayerAlwaysInterested(recipient, true);
            }
        }
    }

    private void LoadSprites()
    {
        smallGoldSprite = Resources.Load<Sprite>("Items/SmallGoldBag");
        mediumGoldSprite = Resources.Load<Sprite>("Items/MediumGoldBag");
        largeGoldSprite = Resources.Load<Sprite>("Items/LargeGoldBag");
    }

public override void Spawned()
{
    // Auto-collect coroutine'ini SADECE PLAYER CLIENT'LARDA başlat
    // Dedicated server'da çalışmasın
    if (Runner.LocalPlayer != PlayerRef.None)
    {
        StartCoroutine(AutoCollectCoroutine());
    }
    
    // Eğer Initialize henüz çağrılmamışsa bekle
    if (CoinAmount == 0 && coinText == null)
    {
        StartCoroutine(WaitForInitialize());
    }
}

    // Yeni metod ekle
    private IEnumerator WaitForInitialize()
    {
        float timeout = 2f;
        float elapsed = 0f;
        
        while (CoinAmount == 0 && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (CoinAmount > 0 && coinText == null)
        {
            UpdateVisuals();
        }
    }

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void InitializeCoinRPC(int amount, PlayerRef ownerPlayer, PlayerRef[] recipients, Vector2 correctPosition)
{
    
    // Pozisyonu doğru pozisyona set et
    transform.position = correctPosition;
    
    CoinAmount = amount;
    NetworkOwnerPlayer = ownerPlayer;  // PlayerRef olarak kaydet
    
    // Authorized players'ı set et
    for (int i = 0; i < recipients.Length && i < AuthorizedPlayers.Length; i++)
    {
        AuthorizedPlayers.Set(i, recipients[i]);
    }
    
    // Visuals'ları güncelle
    UpdateVisuals();
}

    private void UpdateVisuals()
    {
        
        // Sadece authorized player'larda görüntüle
        if (IsAuthorizedToSeeVisuals())
        {
            SetCoinSprite(CoinAmount);
            SetCoinScale(CoinAmount);
            CreateCoinText();
            
            // Renderer'ı aktif et
            if (coinRenderer != null)
            {
                coinRenderer.enabled = true;
            }
        }
        else
        {
            // Authorized değilse gizle
            if (coinRenderer != null)
            {
                coinRenderer.enabled = false;
            }
        }
    }

    private bool IsAuthorizedToSeeVisuals()
    {
        if (Runner == null || !Runner.IsRunning) 
            return false;
        
        // AuthorizedPlayers listesini kontrol et
        for (int i = 0; i < AuthorizedPlayers.Length; i++)
        {
            if (AuthorizedPlayers[i] == Runner.LocalPlayer)
                return true;
        }
        
        return false;
    }

public bool CanCollectCoin()
{
    if (Runner == null || !Runner.IsRunning)
    {
        return false;
    }

    // Dedicated server'da coin collect edilmemeli
    if (Runner.LocalPlayer == PlayerRef.None)
    {
        return false;
    }

    // Check if local player is in authorized list
    for (int i = 0; i < AuthorizedPlayers.Length; i++)
    {
        if (AuthorizedPlayers[i] == Runner.LocalPlayer)
        {
            return true;
        }
    }
    
    return false;
}

    private void SetCoinSprite(int amount)
    {
        if (coinRenderer == null) return;

        Sprite targetSprite = smallGoldSprite;

        if (amount < 100)
        {
            targetSprite = smallGoldSprite;
        }
        else if (amount < 500)
        {
            targetSprite = mediumGoldSprite ?? smallGoldSprite;
        }
        else
        {
            targetSprite = largeGoldSprite ?? smallGoldSprite;
        }

        coinRenderer.sprite = targetSprite;
        coinRenderer.color = Color.yellow;
    }

    private void SetCoinScale(int amount)
    {
        float scale = 1f;
        if (amount >= 500) scale = 1.5f;
        else if (amount >= 100) scale = 2f;

        transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void CreateCoinText()
    {
        try
        {
            if (coinText != null) return;

            // CoinAmount kontrolü ekle
            if (CoinAmount <= 0)
            {
                return;
            }


            GameObject textObj = new GameObject("CoinText");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = new Vector2(0, 1.1f);

            // Arkaplan objesi oluştur
            GameObject bgObj = new GameObject("TextBackground");
            bgObj.transform.SetParent(textObj.transform);
            bgObj.transform.localPosition = Vector3.zero;

            SpriteRenderer bgRenderer = bgObj.AddComponent<SpriteRenderer>();
            Texture2D gradientTexture = CreateGradientTexture();
            Sprite gradientSprite = Sprite.Create(gradientTexture,
                                                 new Rect(0, 0, gradientTexture.width, gradientTexture.height),
                                                 new Vector2(0.5f, 0.5f));

            bgRenderer.sprite = gradientSprite;
            bgRenderer.color = new Color(0, 0, 0, 0.5f);
            bgRenderer.sortingLayerName = "-25";
            bgRenderer.sortingOrder = 0;
            bgObj.transform.localScale = new Vector3(1f, 0.6f, 1f);

            coinText = textObj.AddComponent<TextMeshPro>();
            coinText.fontSize = textSize;
            coinText.alignment = TextAlignmentOptions.Center;
            coinText.text = $"{CoinAmount} Gold";
            coinText.color = goldTextColor;
            coinText.font = goldFont;

            // TextMeshPro için renderer'ı al ve sorting layer'ı ayarla
            Renderer textRenderer = coinText.GetComponent<Renderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingLayerName = "Default";
                textRenderer.sortingOrder = 5;
            }

            textObj.AddComponent<LookAtCamera>();


        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CoinDrop] Error creating coin text: {e.Message}");
        }
    }

    private Texture2D CreateGradientTexture()
    {
        int width = 256;
        int height = 64;
        Texture2D texture = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float centerX = width / 2.0f;
                float distanceFromCenter = Mathf.Abs(x - centerX);
                float maxDistance = width / 2.0f;
                float normalizedDistance = distanceFromCenter / maxDistance;
                float alpha = Mathf.Clamp01(1.0f - (normalizedDistance * normalizedDistance));

                Color pixelColor = new Color(0, 0, 0, alpha * 0.7f);
                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return texture;
    }

private IEnumerator AutoCollectCoroutine()
{
    yield return new WaitForSeconds(autoCollectDelay);


    // Sadece owner client'ta ve henüz collect edilmemişse
    if (!isCollected && CanCollectCoin())
    {
        CollectCoin();
    }
}

private void CollectCoin()
{
    if (isCollected) 
    {
        return;
    }
    
    // Sadece owner collect edebilir
    if (!CanCollectCoin()) 
    {
        return;
    }
    
    
    // Client ise server'a collection isteği gönder
    if (!Object.HasStateAuthority)
    {
        RPC_RequestCoinCollection();
    }
    else
    {
        // Host ise direkt işle
        isCollected = true;
        RPC_StartCoinAnimation();
        Runner.Despawn(Object);
    }
}

[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
private void RPC_RequestCoinCollection()
{
    
    if (Object.HasStateAuthority && !isCollected)
    {
        isCollected = true;

        // Tüm client'lara animation başlatma emri gönder
        RPC_StartCoinAnimation();

        // RPC'nin gitmesi için kısa bekleme sonra despawn
        StartCoroutine(DelayedDespawn());
    }

}

    private IEnumerator DelayedDespawn()
    {
        // RPC'nin client'lara ulaşması için kısa bekleme
        yield return new WaitForSeconds(0.1f);
        
        if (Object.HasStateAuthority)
        {
            Runner.Despawn(Object);
        }
    }

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_StartCoinAnimation()
{

    if (Runner.LocalPlayer == NetworkOwnerPlayer)  // Düzeltildi
    {
        
        if (CoinEffectManager.Instance != null)
        {
            CoinEffectManager.Instance.PlayCoinEffect(transform.position, CoinAmount);
        }
        else
        {
            // Fallback: direkt coin ekle
            AddCoinDirectly();
        }
    }

}

private void AddCoinDirectly()
{

    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    foreach (GameObject player in players)
    {
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj != null && netObj.HasInputAuthority)
        {
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.AddCoins(CoinAmount);

                // Coin notification göster
                if (FragmentNotificationUI.Instance != null && coinRenderer != null)
                {
                    FragmentNotificationUI.Instance.ShowFragmentNotification(
                        "Gold",
                        CoinAmount,
                        coinRenderer.sprite
                    );
                }

                return;
            }
        }
    }
}
}