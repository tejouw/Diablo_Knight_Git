using UnityEngine;
using System.Collections.Generic;
[CreateAssetMenu(fileName = "New Area", menuName = "Game/Area Data")]
public class AreaData : ScriptableObject
{
    [Header("Area Info")]
    public string areaName;

    [Header("PVP Settings")]
    public bool isPVPEnabled = false;

    [Header("Area Bounds - Corner Method")]
    public Vector2 bottomLeftCorner;   // Sol alt köşe
    public Vector2 topRightCorner;     // Sağ üst köşe

    [Header("Auto-Calculated Corners (Read Only)")]
    [SerializeField] private Vector2 topLeftCorner;    // Sol üst köşe
    [SerializeField] private Vector2 bottomRightCorner; // Sağ alt köşe

    // Otomatik hesaplanan değerler
    public Vector2 AreaCenter => (bottomLeftCorner + topRightCorner) / 2f;
    public Vector2 AreaSize => topRightCorner - bottomLeftCorner;

    // Property'ler kod içinde kullanım için
    public Vector2 TopLeftCorner => topLeftCorner;
    public Vector2 BottomRightCorner => bottomRightCorner;

    // Geriye uyumluluk için eski field'lar (kullanma bunları)
    [HideInInspector] public Vector2 areaCenter;
    [HideInInspector] public Vector2 areaSize;

    private void OnValidate()
    {
        // Sol üst: sol alt'ın x'i + sağ üst'ün y'si
        topLeftCorner = new Vector2(bottomLeftCorner.x, topRightCorner.y);

        // Sağ alt: sağ üst'ün x'i + sol alt'ın y'si  
        bottomRightCorner = new Vector2(topRightCorner.x, bottomLeftCorner.y);
    }

    public bool IsPositionInArea(Vector2 position)
    {
        bool result = position.x >= bottomLeftCorner.x &&
                      position.x <= topRightCorner.x &&
                      position.y >= bottomLeftCorner.y &&
                      position.y <= topRightCorner.y;

        return result;
    }
    // AreaData.cs'nin sonuna ekle:

#region Sub-Area System (3x3 Grid)

public Vector2 GetSubAreaSize()
{
    return new Vector2(AreaSize.x / 3f, AreaSize.y / 3f);
}

public Vector2 GetSubAreaCenter(int subAreaNumber)
{
    if (subAreaNumber < 1 || subAreaNumber > 9) return Vector2.zero;
    
    Vector2 subAreaSize = GetSubAreaSize();
    
    // 3x3 grid koordinatları (1-9 numarası için)
    int row = (subAreaNumber - 1) / 3; // 0, 1, 2
    int col = (subAreaNumber - 1) % 3; // 0, 1, 2
    
    // Sub-area'nın merkezi
    float centerX = bottomLeftCorner.x + (col + 0.5f) * subAreaSize.x;
    float centerY = topRightCorner.y - (row + 0.5f) * subAreaSize.y; // Y ekseni ters
    
    return new Vector2(centerX, centerY);
}

public Vector2 GetSubAreaBottomLeft(int subAreaNumber)
{
    if (subAreaNumber < 1 || subAreaNumber > 9) return Vector2.zero;
    
    Vector2 subAreaSize = GetSubAreaSize();
    Vector2 center = GetSubAreaCenter(subAreaNumber);
    
    return new Vector2(
        center.x - subAreaSize.x * 0.5f,
        center.y - subAreaSize.y * 0.5f
    );
}

public Vector2 GetSubAreaTopRight(int subAreaNumber)
{
    if (subAreaNumber < 1 || subAreaNumber > 9) return Vector2.zero;
    
    Vector2 subAreaSize = GetSubAreaSize();
    Vector2 center = GetSubAreaCenter(subAreaNumber);
    
    return new Vector2(
        center.x + subAreaSize.x * 0.5f,
        center.y + subAreaSize.y * 0.5f
    );
}

public bool IsPositionInSubArea(Vector2 position, int subAreaNumber)
{
    if (subAreaNumber < 1 || subAreaNumber > 9) return false;
    
    Vector2 bottomLeft = GetSubAreaBottomLeft(subAreaNumber);
    Vector2 topRight = GetSubAreaTopRight(subAreaNumber);
    
    return position.x >= bottomLeft.x && 
           position.x <= topRight.x &&
           position.y >= bottomLeft.y && 
           position.y <= topRight.y;
}

public bool IsPositionInAnyOfSubAreas(Vector2 position, List<int> subAreaNumbers)
{
    foreach (int subAreaNumber in subAreaNumbers)
    {
        if (IsPositionInSubArea(position, subAreaNumber))
            return true;
    }
    return false;
}

#endregion
}
