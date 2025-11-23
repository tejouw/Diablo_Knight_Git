using UnityEngine;

public static class QuestMarkerIcons
{
    private static Sprite _availableQuestIcon;
    private static Sprite _activeQuestIcon;
    private static Sprite _completedQuestIcon;
    
    public static Sprite AvailableQuestIcon
    {
        get
        {
            if (_availableQuestIcon == null)
                LoadIcons();
            return _availableQuestIcon;
        }
    }
    
    public static Sprite ActiveQuestIcon
    {
        get
        {
            if (_activeQuestIcon == null)
                LoadIcons();
            return _activeQuestIcon;
        }
    }
    
    public static Sprite CompletedQuestIcon
    {
        get
        {
            if (_completedQuestIcon == null)
                LoadIcons();
            return _completedQuestIcon;
        }
    }
    
    private static void LoadIcons()
    {
        _availableQuestIcon = Resources.Load<Sprite>("QuestIcons/quest_available");
        _activeQuestIcon = Resources.Load<Sprite>("QuestIcons/quest_active");
        _completedQuestIcon = Resources.Load<Sprite>("QuestIcons/quest_completed");
        
        // Varsayılan sprite oluştur eğer bulunamazsa
        if (_availableQuestIcon == null)
        {
            Debug.LogWarning("[QuestMarkerIcons] quest_available sprite bulunamadı, varsayılan oluşturuluyor");
            _availableQuestIcon = CreateDefaultSprite(Color.yellow);
        }
        
        if (_activeQuestIcon == null)
        {
            Debug.LogWarning("[QuestMarkerIcons] quest_active sprite bulunamadı, varsayılan oluşturuluyor");
            _activeQuestIcon = CreateDefaultSprite(Color.blue);
        }
        
        if (_completedQuestIcon == null)
        {
            Debug.LogWarning("[QuestMarkerIcons] quest_completed sprite bulunamadı, varsayılan oluşturuluyor");
            _completedQuestIcon = CreateDefaultSprite(Color.green);
        }
    }
    
    private static Sprite CreateDefaultSprite(Color color)
    {
        Texture2D texture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f);
    }
}