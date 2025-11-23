#if UNITY_SERVER || SERVER_BUILD
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

[DefaultExecutionOrder(-5000)]
public class ServerTileColliderOverride : MonoBehaviour
{
    [Tooltip("Sadece TilemapCollider2D bulunan tilemap'leri işler.")]
    public bool onlyTilemapsWithCollider = true;

    [Tooltip("Grid tipi dikdörtgen collider kullan.")]
    public bool useGridCollider = true;

    private readonly Dictionary<Sprite, Tile> _tileCache = new Dictionary<Sprite, Tile>();

    // Sahne yüklenmeden önce en erken anda kurulum objesini yarat
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        var go = new GameObject("ServerTileColliderOverride");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<ServerTileColliderOverride>();
    }

    private void Awake()
    {
        // 1) Tüm TilemapCollider2D'leri önce kapat (outline üretimine fırsat vermeden)
        var colliders = FindAll<TilemapCollider2D>(includeInactive: true);
        foreach (var col in colliders) col.enabled = false;

        // 2) İşlenecek tilemap'leri bul ve tile'ları runtime kopyalarıyla değiştir
        var tilemaps = FindAll<Tilemap>(includeInactive: true);
        foreach (var tm in tilemaps)
        {
            bool hasCollider = tm.GetComponent<TilemapCollider2D>() != null;
            if (onlyTilemapsWithCollider && !hasCollider) continue;
            ReplaceTilesWithRuntimeCopies(tm);
        }

        // 3) Collider'ları tekrar aç ve değişiklikleri uygula
        foreach (var col in colliders)
        {
            col.enabled = true;
            col.ProcessTilemapChanges();
        }
    }

    private void ReplaceTilesWithRuntimeCopies(Tilemap tm)
    {
        var bounds = tm.cellBounds;
        var positions = bounds.allPositionsWithin;

        foreach (var pos in positions)
        {
            var baseTile = tm.GetTile<TileBase>(pos);
            if (baseTile == null) continue;

            // Klasik Tile dışındakileri (RuleTile vs.) şimdilik atla (gerekirse genişletilir)
            var tile = baseTile as Tile;
            if (tile == null) continue;

            if (tile.colliderType == Tile.ColliderType.None) continue;

            var spr = tile.sprite;
            if (spr == null) continue;

            if (!_tileCache.TryGetValue(spr, out var runtimeTile))
            {
                runtimeTile = ScriptableObject.CreateInstance<Tile>();
                runtimeTile.sprite = spr;
                runtimeTile.color = Color.white;
                runtimeTile.flags = TileFlags.None;
                runtimeTile.colliderType = useGridCollider ? Tile.ColliderType.Grid : Tile.ColliderType.Sprite;
                _tileCache.Add(spr, runtimeTile);
            }

            // Asset'e dokunmadan, sadece runtime'da değiştir
            tm.SetTile(pos, runtimeTile);
        }
    }

    // Unity 2023+ yeni API; eski sürümler için geriye uyumlu yardımcı
    private static T[] FindAll<T>(bool includeInactive) where T : Object
    {
        #if UNITY_2023_1_OR_NEWER
        var inactive = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        return Object.FindObjectsByType<T>(inactive, FindObjectsSortMode.None);
        #else
        return Object.FindObjectsOfType<T>(includeInactive);
        #endif
    }
}
#endif
