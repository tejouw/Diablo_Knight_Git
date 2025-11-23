using UnityEngine;
using System.Collections;
using Fusion;
using TMPro;

public class FragmentDrop : NetworkBehaviour
{
    [Header("Fragment Settings")]
    [SerializeField] private float autoCollectDelay = 0.5f;
    [SerializeField] private SpriteRenderer fragmentRenderer;

    [Networked] public int FragmentAmount { get; set; }
    [Networked, Capacity(8)] public NetworkArray<PlayerRef> AuthorizedPlayers => default;
    [Networked] public PlayerRef NetworkOwnerPlayer { get; set; }
    
    [Header("Text Settings")]
    [SerializeField] private TMP_FontAsset fragmentFont;
    [SerializeField] private float textSize = 6f;
    [SerializeField] private Color fragmentTextColor = new Color(0.5f, 1f, 0.5f); // Açık yeşil

    private TextMeshPro fragmentText;
    private bool isCollected = false;
    private string fragmentItemId;

    private void Awake()
    {
        if (fragmentRenderer == null)
        {
            fragmentRenderer = GetComponent<SpriteRenderer>();
        }
    }

    public void SetAuthorizedPlayersOnSpawn(PlayerRef[] recipients)
    {
        if (!Object.HasStateAuthority) return;
        
        for (int i = 0; i < recipients.Length && i < AuthorizedPlayers.Length; i++)
        {
            AuthorizedPlayers.Set(i, recipients[i]);
        }
        
        SetFragmentInterest(recipients);
    }

    private void SetFragmentInterest(PlayerRef[] recipients)
    {
        if (!Object.HasStateAuthority) return;
        
        foreach (var recipient in recipients)
        {
            if (recipient != PlayerRef.None)
            {
                Object.SetPlayerAlwaysInterested(recipient, true);
            }
        }
    }

public override void Spawned()
{
    
    if (Runner.LocalPlayer != PlayerRef.None)
    {
        StartCoroutine(AutoCollectCoroutine());
    }
    
    if (FragmentAmount == 0 && fragmentText == null)
    {
        StartCoroutine(WaitForInitialize());
    }
}

    private IEnumerator WaitForInitialize()
    {
        float timeout = 2f;
        float elapsed = 0f;
        
        while (FragmentAmount == 0 && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (FragmentAmount > 0 && fragmentText == null)
        {
            UpdateVisuals();
        }
    }

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void InitializeFragmentRPC(string itemId, int amount, PlayerRef ownerPlayer, PlayerRef[] recipients, Vector2 correctPosition)
{
    
    transform.position = correctPosition;
    
    fragmentItemId = itemId;
    FragmentAmount = amount;
    NetworkOwnerPlayer = ownerPlayer;
    
    for (int i = 0; i < recipients.Length && i < AuthorizedPlayers.Length; i++)
    {
        AuthorizedPlayers.Set(i, recipients[i]);
    }
    
    
    UpdateVisuals();
}

    private void UpdateVisuals()
    {
        if (IsAuthorizedToSeeVisuals())
        {
            SetFragmentSprite();
            CreateFragmentText();
            
            if (fragmentRenderer != null)
            {
                fragmentRenderer.enabled = true;
            }
        }
        else
        {
            if (fragmentRenderer != null)
            {
                fragmentRenderer.enabled = false;
            }
        }
    }

    private bool IsAuthorizedToSeeVisuals()
    {
        if (Runner == null || !Runner.IsRunning) 
            return false;
        
        for (int i = 0; i < AuthorizedPlayers.Length; i++)
        {
            if (AuthorizedPlayers[i] == Runner.LocalPlayer)
                return true;
        }
        
        return false;
    }

    public bool CanCollectFragment()
    {
        if (Runner == null || !Runner.IsRunning)
            return false;

        if (Runner.LocalPlayer == PlayerRef.None)
            return false;

        for (int i = 0; i < AuthorizedPlayers.Length; i++)
        {
            if (AuthorizedPlayers[i] == Runner.LocalPlayer)
                return true;
        }
        
        return false;
    }

    private void SetFragmentSprite()
    {
        if (fragmentRenderer == null || string.IsNullOrEmpty(fragmentItemId)) return;

        ItemData fragmentItem = ItemDatabase.Instance?.GetItemById(fragmentItemId);
        if (fragmentItem != null && fragmentItem.itemIcon != null)
        {
            fragmentRenderer.sprite = fragmentItem.itemIcon;
            fragmentRenderer.color = Color.white;
            transform.localScale = new Vector3(0.2f, 0.2f, 1f);
        }
    }

    private void CreateFragmentText()
    {
        if (fragmentText != null) return;
        if (FragmentAmount <= 0 || string.IsNullOrEmpty(fragmentItemId)) return;

        ItemData fragmentItem = ItemDatabase.Instance?.GetItemById(fragmentItemId);
        if (fragmentItem == null) return;

        GameObject textObj = new GameObject("FragmentText");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = new Vector2(0, 4f);

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

        fragmentText = textObj.AddComponent<TextMeshPro>();
        fragmentText.fontSize = textSize;
        fragmentText.alignment = TextAlignmentOptions.Center;
        fragmentText.text = $"{fragmentItem._baseItemName} x{FragmentAmount}";
        fragmentText.color = fragmentTextColor;
        fragmentText.font = fragmentFont;

        Renderer textRenderer = fragmentText.GetComponent<Renderer>();
        if (textRenderer != null)
        {
            textRenderer.sortingLayerName = "Default";
            textRenderer.sortingOrder = 5;
        }

        textObj.AddComponent<LookAtCamera>();
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


    if (!isCollected && CanCollectFragment())
    {
        CollectFragment();
    }
}

private void CollectFragment()
{
    
    if (isCollected || !CanCollectFragment()) return;
    
    if (!Object.HasStateAuthority)
    {
        RPC_RequestFragmentCollection();
    }
    else
    {
        isCollected = true;
        RPC_StartFragmentAnimation();
        Runner.Despawn(Object);
    }
}

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestFragmentCollection()
    {
        if (Object.HasStateAuthority && !isCollected)
        {
            isCollected = true;
            RPC_StartFragmentAnimation();
            StartCoroutine(DelayedDespawn());
        }
    }

    private IEnumerator DelayedDespawn()
    {
        yield return new WaitForSeconds(0.1f);
        
        if (Object.HasStateAuthority)
        {
            Runner.Despawn(Object);
        }
    }
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_StartFragmentAnimation()
{
    
    if (Runner.LocalPlayer == NetworkOwnerPlayer)
    {
        AddFragmentDirectly();
    }

}

private void AddFragmentDirectly()
{
    
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    
    foreach (GameObject player in players)
    {
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj != null && netObj.HasInputAuthority)
        {
            
            CraftInventorySystem craftInventory = player.GetComponent<CraftInventorySystem>();
            if (craftInventory != null && !string.IsNullOrEmpty(fragmentItemId))
            {
                
                ItemData fragmentItem = ItemDatabase.Instance?.GetItemById(fragmentItemId);
                if (fragmentItem != null)
                {
                    
                    // YENİ: Direkt lokal metodu kullan
                    bool success = craftInventory.AddFragmentDirectly(fragmentItem, FragmentAmount);
                }
            }
            return;
        }
    }
    
}
}