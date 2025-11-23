using UnityEngine;
using UnityEngine.UI;
using Fusion;
using System.Collections;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Collections.Generic;

public class HeadSnapshotManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage previewImage;
    [Header("Performance")]
    [SerializeField] private float maxWaitTime = 5f;
    [SerializeField] private float checkInterval = 0.1f;
    [SerializeField] private float characterInitDelay = 1f; // Character4D.Initialize() için ek bekleme

    private Texture2D headSnapshot;
    private bool isInitialized = false;
    private bool isProcessing = false;
    private float playerInitializedTime = 0f;

    private void Start()
    {
        // Server modunda çalışma
        if (IsServerMode())
        {
            gameObject.SetActive(false);
            return;
        }

        StartCoroutine(TakeSnapshotWhenReady());
    }
private bool IsServerMode()
{
    if (Application.isEditor) return false;
    
    string[] args = System.Environment.GetCommandLineArgs();
    return System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode");
}

    private IEnumerator TakeSnapshotWhenReady()
    {
        if (isProcessing) yield break;
        isProcessing = true;

        float waitTime = 0f;
        GameObject localPlayer = null;

        // Akıllı bekleme - player hazır olana kadar kısa aralıklarla kontrol
        while (localPlayer == null && waitTime < maxWaitTime)
        {
            localPlayer = FindLocalPlayer();

            if (localPlayer != null)
            {
                // Player bulundu, character hazır mı kontrol et
                if (IsCharacterReady(localPlayer))
                {
                    yield return new WaitForEndOfFrame();
                    TakeHeadSnapshot(localPlayer);
                    break;
                }
                else
                {
                    localPlayer = null;
                }
            }
            else
            {
            }

            yield return new WaitForSeconds(checkInterval);
            waitTime += checkInterval;
        }

        if (localPlayer == null)
        {
            yield return new WaitForSeconds(2f);
            isProcessing = false;
            StartCoroutine(TakeSnapshotWhenReady());
        }
        else
        {
            isProcessing = false;
        }
    }

    private bool IsCharacterReady(GameObject player)
    {
        // NetworkObject kontrol
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj == null || !networkObj.IsValid || !networkObj.HasInputAuthority)
        {
            return false;
        }

        // Character4D kontrol
        Character4D character4D = player.GetComponent<Character4D>();
        if (character4D == null || character4D.Front == null)
        {
            return false;
        }

        // PlayerStats kontrol (initialized olmalı)
        PlayerStats playerStats = player.GetComponent<PlayerStats>();
        if (playerStats == null || !playerStats.isInitialized)
        {
            playerInitializedTime = 0f;
            return false;
        }

        // PlayerStats yeni initialize oldu mu? Ek delay gerekiyor (Character4D.Initialize() için)
        if (playerInitializedTime == 0f)
        {
            playerInitializedTime = Time.time;
        }

        float timeSinceInit = Time.time - playerInitializedTime;
        if (timeSinceInit < characterInitDelay)
        {
            return false;
        }

        // Character appearance yüklenmiş mi kontrol
        Transform headAnchor = character4D.Front.transform.Find("UpperBody/HeadAnchor");
        if (headAnchor == null)
        {
            return false;
        }

        // En az bir renderer aktif mi kontrol
        SpriteRenderer[] renderers = headAnchor.GetComponentsInChildren<SpriteRenderer>();
        bool hasActiveRenderer = false;
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.enabled && renderer.sprite != null)
            {
                hasActiveRenderer = true;
                break;
            }
        }
        return hasActiveRenderer;
    }

    private GameObject FindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            NetworkObject networkObj = player.GetComponent<NetworkObject>();
            if (networkObj != null && networkObj.IsValid && networkObj.HasInputAuthority)
            {
                return player;
            }
        }
        return null;
    }

    private void TakeHeadSnapshot(GameObject player)
    {
        Character4D character4D = player.GetComponent<Character4D>();
        if (character4D == null)
        {
            return;
        }

        // Front'u aktif et
        bool frontWasActive = character4D.Front.gameObject.activeSelf;
        character4D.Front.gameObject.SetActive(true);

        Transform headAnchor = character4D.Front.transform.Find("UpperBody/HeadAnchor");
        if (headAnchor == null) 
        {
            if (!frontWasActive) character4D.Front.gameObject.SetActive(false);
            return;
        }

        // Layer ayarlarını kaydet ve değiştir
        var originalLayers = new Dictionary<Transform, int>();
        SetChildrenLayer(headAnchor, LayerMask.NameToLayer("UI"), originalLayers);

        // Camera setup - optimize edilmiş
        GameObject tempCamObj = new GameObject("TempSnapshotCamera");
        Camera tempCam = tempCamObj.AddComponent<Camera>();

        tempCam.clearFlags = CameraClearFlags.SolidColor;
        tempCam.backgroundColor = Color.clear;
        tempCam.orthographic = true;
        tempCam.orthographicSize = 1.7f; // Biraz daha yakın
        tempCam.cullingMask = 1 << LayerMask.NameToLayer("UI");
        tempCam.depth = 100; // En üstte render et

        Vector3 headPosition = headAnchor.position;
        tempCamObj.transform.position = headPosition + Vector3.back * 2f;
        tempCamObj.transform.LookAt(headPosition);

        // Render - daha küçük texture boyutu
        RenderTexture renderTexture = new RenderTexture(256, 256, 16);
        tempCam.targetTexture = renderTexture;
        tempCam.Render();

        // Texture2D'ye çevir
        RenderTexture.active = renderTexture;
        headSnapshot = new Texture2D(256, 256, TextureFormat.RGB24, false);
        headSnapshot.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        headSnapshot.Apply();

        // Cleanup
        RestoreChildrenLayers(originalLayers);
        RenderTexture.active = null;
        renderTexture.Release();
        Destroy(renderTexture);
        Destroy(tempCamObj);

        if (!frontWasActive) character4D.Front.gameObject.SetActive(false);

        // UI'ya uygula
        if (previewImage != null)
        {
            previewImage.texture = headSnapshot;
            isInitialized = true;
        }

    }

    private void SetChildrenLayer(Transform parent, int newLayer, Dictionary<Transform, int> originalLayers)
    {
        foreach (Transform child in parent)
        {
            originalLayers[child] = child.gameObject.layer;
            child.gameObject.layer = newLayer;
            SetChildrenLayer(child, newLayer, originalLayers);
        }
    }

    private void RestoreChildrenLayers(Dictionary<Transform, int> originalLayers)
    {
        foreach (var kvp in originalLayers)
        {
            if (kvp.Key != null)
            {
                kvp.Key.gameObject.layer = kvp.Value;
            }
        }
    }

    // Public metod - dışarıdan tetiklemek için
    public void RefreshSnapshot()
    {
        if (!isProcessing && isInitialized)
        {
            StartCoroutine(TakeSnapshotWhenReady());
        }
    }

    private void OnDestroy()
    {
        if (headSnapshot != null)
        {
            Destroy(headSnapshot);
        }
    }
}