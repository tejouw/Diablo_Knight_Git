// Path: Assets/Game/Scripts/CanvasEventCamera.cs

using UnityEngine;

public class CanvasEventCamera : MonoBehaviour
{
    private Canvas canvas;
    
    private void Start()
    {
        canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }
    }
}