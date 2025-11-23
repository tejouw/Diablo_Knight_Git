using UnityEngine;
using System.Collections.Generic;

public static class AnimationMapping
{
    public const byte NONE_ANIMATION = 0;
    
    private static readonly Dictionary<string, byte> StringToByteMap = new Dictionary<string, byte>
    {
        { "", 0 },           // None/Empty
        { "Idle", 1 },
        { "Walk", 2 },
        { "Run", 3 },
        { "Ready", 4 },
        { "Attack", 5 },
        { "Death", 6 },
        { "Custom", 7 },
        { "Hit", 8 },
        { "Spawn", 9 }
    };
    
    private static readonly Dictionary<byte, string> ByteToStringMap = new Dictionary<byte, string>();
    
    static AnimationMapping()
    {
        // Reverse mapping oluştur
        foreach (var kvp in StringToByteMap)
        {
            ByteToStringMap[kvp.Value] = kvp.Key;
        }
        
    }
    
    public static byte GetAnimationByte(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
        {
            return NONE_ANIMATION;
        }
            
        if (StringToByteMap.TryGetValue(animationName, out byte result))
            return result;
            
        return NONE_ANIMATION;
    }
    
    public static string GetAnimationString(byte animationByte)
    {
        if (ByteToStringMap.TryGetValue(animationByte, out string result))
            return result;
            
        return "";
    }
    
    public static bool IsValidAnimation(string animationName)
    {
        return StringToByteMap.ContainsKey(animationName ?? "");
    }
    
    public static bool IsValidAnimationByte(byte animationByte)
    {
        return ByteToStringMap.ContainsKey(animationByte);
    }
}