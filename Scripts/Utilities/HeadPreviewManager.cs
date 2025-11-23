using UnityEngine;
using UnityEngine.UI;

public class HeadPreviewManager : MonoBehaviour 
{
    [Header("UI")]
    [SerializeField] private RawImage previewImage;
    
    private HeadSnapshotManager snapshotManager;

    private void Start()
    {
        // HeadSnapshotManager'ı kullan
        snapshotManager = GetComponent<HeadSnapshotManager>();
        if (snapshotManager == null)
        {
            snapshotManager = gameObject.AddComponent<HeadSnapshotManager>();
        }
    }
}