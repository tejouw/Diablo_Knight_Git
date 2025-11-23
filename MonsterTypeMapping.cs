using System.Collections.Generic;
using UnityEngine;

public static class MonsterTypeMapping
{
    public const byte UNKNOWN_MONSTER_TYPE = 0; // 0 = Unknown/Invalid
    
    private static readonly Dictionary<string, byte> StringToByteMap = new Dictionary<string, byte>
{
    // Normal Monsters (1-38)
    { "Civciv", 1 },
    { "Köy Tavuğu", 2 },
    { "Yaban Ördeği", 3 },
    { "Kaz", 4 },
    { "Larva", 5 },
    { "Eşek Arısı", 6 },
    { "Tavuk", 7 },
    { "Horoz", 8 },
    { "Tavşan", 9 },
    { "Ördek", 10 },
    { "Kızıl Tavuk", 11 },
    { "Domuz", 12 },
    { "Uğru Göz", 13 },
    { "Minik Tavşan", 14 },
    { "Dövüş Horozu", 15 },
    { "Sürüngen", 16 },
    { "Maymun", 17 },
    { "Yarasa", 18 },
    { "Et Böceği", 19 },
    { "Sıçan", 20 },
    { "Hindi", 21 },
    { "Katır", 22 },
    { "Koyun", 23 },
    { "Tırtıl", 24 },
    { "Eşek", 25 },
    { "Koç", 26 },
    { "Keçi", 27 },
    { "Sivrisinek", 28 },
    { "İnek", 29 },
    { "Boğa", 30 },
    { "Bokböceği", 31 },
    { "At", 32 },
    { "Öküz", 33 },
    { "Midilli", 34 },
    { "Zombi", 35 },
    { "Yamyam", 36 },
    { "Kızıl Akrep", 37 },
    { "Aslan", 38 },

    // Elite Monsters (51-100)
    { "Tıknaz Köpek", 51 },
    { "Kasvet Göz", 52 },
    { "Karanlık Tavşan", 53 },
    { "Tıknaz Domuz", 54 },
    { "Mağara Tırtılı", 55 },
    { "Orman Hindisi", 56 },
    { "Dikenli Kirpi", 57 },
    { "Mağara Sıçanı", 58 },
    { "Dağ Horozu", 59 },
    { "Lanetli Sıçan", 60 },
    { "Kristal Kertenkele", 61 },
    { "Çamur Domuzu", 62 },
    { "Diken Kulak Yarasa", 63 },
    { "Gaga Kuşu", 64 },
    { "Dağ Keçisi", 65 },
    { "Yaban Domuzu", 66 },
    { "Kara Domuz", 67 },
    { "Veba Sıçanı", 68 },
    { "Dişli Asalak", 69 },
    { "Lanetli Domuz", 70 },
    { "Demir Dişli Asalak", 71 },
    { "Lav Asalak", 72 },
    { "Parça Kertenkele", 73 },
    { "Mor Bokböceği", 74 },
    { "Gösterişli Asalak", 75 },
    { "Kurt", 76 },
    { "Buz Asalak", 77 },
    { "Yeşil Asalak", 78 },
    { "Kraliyet Domuzu", 79 },
    { "Taş Domuzu", 80 },
    { "Buz Domuzu", 81 },
    { "Panter", 82 },
    { "Yağmacı Domuz", 83 },
    { "Kurt Köpeği", 84 },
    { "Bekçi Köpeği", 85 },
    { "Av Köpeği", 86 },
    { "Lanetli Bekçi Köpeği", 87 },
    { "Lanetli Kurt", 88 },
    { "Ak Kurt", 89 },
    { "Ayı", 90 },
    { "Kar Kurtu", 91 },
    { "Kara Kurt", 92 },
    { "Don Kurt", 93 },
    { "Varg", 94 },
    { "Buz Vargı", 95 },
    { "Lanetli Varg", 96 },
    { "Kış Ayısı", 97 },
    { "Kaplan", 98 },
    { "Kara Ayı", 99 },
    { "Mavi Akrep", 100 },

    // Boss Monsters (101-120)
    { "Sümüklü Kraliçe", 101 },
    { "Sıçan Kralı", 102 },
    { "Böcek Kraliçe", 103 },
    { "Yosun Kraliçe", 104 },
    { "Ezici Dev", 105 },
    { "Dev Zırhlı", 106 },
    { "Üç Başlı Köpek", 107 },
    { "Kanatlı İblis", 108 },
    { "İnsan Yiyen", 109 },
    { "Cezalandırıcı", 110 },
    { "Karabasan Atı", 111 },
    { "Kasap", 112 },
    { "Buz Ogrusu", 113 },
    { "Trol", 114 },
    { "Ogre", 115 },
    { "Kılıçdiş Kaplan", 116 },
    { "Savaş Panteri", 117 },
//Sonradan eklenenler özel
    { "Domuzcuk", 118 },
    { "Aç Civciv", 119 },
    { "Sarı Kız", 120 },
    { "Etkilenmiş Yaban Ördeği", 121 },
    { "Etkilenmiş Kaz", 122 },
    { "Etkilenmiş Sürüngen", 123 },
    { "Lanetli Köpek", 124 },
        // Special/Event (151-200) – şimdilik boş
        // Future Content (201-254) – manuel doldurulur
        // 255 = UNKNOWN / sistem için ayrılmış
    };
    
    private static readonly Dictionary<byte, string> ByteToStringMap = new Dictionary<byte, string>();
    
    static MonsterTypeMapping()
    {
        // Reverse mapping oluştur
        foreach (var kvp in StringToByteMap)
        {
            ByteToStringMap[kvp.Value] = kvp.Key;
        }
        
        // Unknown type için reverse mapping
        ByteToStringMap[UNKNOWN_MONSTER_TYPE] = "Unknown";
        
    }
    
    public static byte GetMonsterTypeByte(string monsterType)
    {
        if (string.IsNullOrEmpty(monsterType))
        {
            return UNKNOWN_MONSTER_TYPE;
        }
            
        if (StringToByteMap.TryGetValue(monsterType, out byte result))
            return result;
            
        // GÜVENLI: Dynamic ekleme yok, sadece warning
        Debug.LogError($"[MonsterTypeMapping] UNKNOWN MONSTER TYPE: '{monsterType}'. " +
                      "This monster type must be added to MonsterTypeMapping.cs manually!");
        
#if UNITY_EDITOR
        // Editor'da daha detaylı bilgi
        Debug.LogError($"[MonsterTypeMapping] Available types: {string.Join(", ", StringToByteMap.Keys)}");
#endif
        
        return UNKNOWN_MONSTER_TYPE;
    }
    
    public static string GetMonsterTypeString(byte monsterTypeByte)
    {
        if (ByteToStringMap.TryGetValue(monsterTypeByte, out string result))
            return result;
            
        Debug.LogError($"[MonsterTypeMapping] Unknown monster type byte: {monsterTypeByte}");
        return "Unknown";
    }
    
    public static bool IsValidMonsterType(string monsterType)
    {
        return !string.IsNullOrEmpty(monsterType) && StringToByteMap.ContainsKey(monsterType);
    }
    
    public static bool IsValidMonsterTypeByte(byte monsterTypeByte)
    {
        return ByteToStringMap.ContainsKey(monsterTypeByte);
    }
    
    // Development için mapping kontrolü
    public static void ValidateMonsterTypes(string[] usedTypes)
    {
#if UNITY_EDITOR
        List<string> missingTypes = new List<string>();
        
        foreach (string type in usedTypes)
        {
            if (!IsValidMonsterType(type))
            {
                missingTypes.Add(type);
            }
        }
        
        if (missingTypes.Count > 0)
        {
            Debug.LogError($"[MonsterTypeMapping] Missing monster types in mapping: {string.Join(", ", missingTypes)}");
        }
        else
        {
        }
#endif
    }
    
    // Debug için mapping listesi
    public static Dictionary<string, byte> GetAllMappings()
    {
        return new Dictionary<string, byte>(StringToByteMap);
    }
    
    // Editor için yeni type ekleme helper'ı
#if UNITY_EDITOR
    public static void LogAddNewMonsterType(string monsterType)
    {
        if (IsValidMonsterType(monsterType)) return;
        
        // Boş slot bul
        for (byte i = 201; i <= 254; i++)
        {
            if (!ByteToStringMap.ContainsKey(i))
            {
                break;
            }
        }
    }
#endif
}