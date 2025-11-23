# Diablo Knight

![Unity](https://img.shields.io/badge/Unity-2021.3+-black?logo=unity)
![Photon](https://img.shields.io/badge/Photon-Fusion-blue)
![Firebase](https://img.shields.io/badge/Firebase-Realtime%20DB-orange?logo=firebase)
![C#](https://img.shields.io/badge/C%23-10.0-purple?logo=csharp)
![License](https://img.shields.io/badge/License-Private-red)

**Diablo Knight**, Unity ve Photon Fusion kullanılarak geliştirilmiş, çok oyunculu (MMORPG) bir aksiyon RPG oyunudur. Diablo benzeri izometrik görünüm, zengin karakter özelleştirme, skill tabanlı combat sistemi ve Firebase entegrasyonuyla oyuncu verilerini kalıcı olarak saklayan profesyonel bir projedir.

---

## İçindekiler

- [Özellikler](#özellikler)
- [Teknik Özellikler](#teknik-özellikler)
- [Proje Yapısı](#proje-yapısı)
- [Kurulum](#kurulum)
- [Kullanım](#kullanım)
- [Oynanış](#oynanış)
- [Networking Mimarisi](#networking-mimarisi)
- [Geliştirici Notları](#geliştirici-notları)
- [Lisans](#lisans)

---

## Özellikler

### Karakter Sistemi
- **2 Oynanabilir Irk**: Human ve Goblin (her biri için özel spawn noktaları)
- **3 Sınıf**: Warrior, Archer, Rogue
- **Hero Editor 4D** ile detaylı karakter özelleştirme
- **Level ve XP Sistemi** ile karakter gelişimi
- **Stat Sistemi**: Strength, Dexterity, Intelligence, Vitality, Luck
- **Karakter snapshot** ve preview sistemleri

### Combat Sistemi
- **Skill Tabanlı Combat**: 3 ana slot (Utility, Combat, Ultimate)
- Her slot'ta 3 farklı skill barındırabilme
- **Pasif Skill Sistemleri** (2 slot)
- **AoE ve Hedef Bazlı Yetenekler**
- **Buff/Debuff Sistemleri** ile taktiksel derinlik
- **Projektil Mekanikleri** ve fizik tabanlı saldırılar

### Quest Sistemi
- **Multi-objective Quest'ler**
- **Quest zinciri** desteği
- **Hidden objective** mekanikleri
- **Dialog sistemi** ile zengin hikaye anlatımı
- **Firebase persistence** ile quest ilerlemesi kaydedilir
- **Quest pusula** ve marker sistemleri

### Inventory & Crafting
- **Grid bazlı envanter sistemi**
- **Equipment slot'ları** (helmet, armor, weapon, vb.)
- **Crafting recipe sistemi** ile item üretimi
- **Loot drop mekanikleri** ve rastgele item üretimi
- **Item upgrade** ve enhancement sistemleri
- **Tüccar NPC'ler** ile alışveriş

### Multiplayer Features
- **Photon Fusion** ile client-server architecture
- **Real-time multiplayer** (MMO benzeri)
- **Party/Group Sistemi** ile takım oyunu
- **PVP Sistemi** - oyuncu vs oyuncu combat
- **Chat Sistemi** (global ve private)
- **Firebase** ile oyuncu verilerinin kalıcı saklanması

### World & Environment
- **Area bazlı oyun dünyası** - bölgelere ayrılmış harita
- **Portal ve Teleportasyon** sistemleri
- **Bindstone Sistemi** - respawn point belirleme
- **Gathering Mekanikleri** - toplanabilir kaynaklar
- **Dynamic Monster Spawning** - area bazlı canavar spawn
- **NPC Sistemleri** - idle, wandering, merchant, blacksmith

---

## Teknik Özellikler

### Oyun Motoru & Framework
- **Unity 2021.3+** (C# 10.0)
- **Photon Fusion** - Networking
- **Firebase Realtime Database** - Data persistence
- **Firebase Authentication** - Kullanıcı yönetimi
- **Hero Editor 4D** - Karakter sistemi
- **DuloGames UI** - UI framework

### Mimari Yapı
- **Modüler Klasör Yapısı** - kategorilere ayrılmış scriptler
- **NetworkBehaviour** bazlı network senkronizasyonu
- **ScriptableObject** bazlı data sistemi
- **Event-driven Architecture** - loosely coupled sistemler
- **Singleton Pattern** - merkezi yöneticiler için
- **Object Pooling** - performans optimizasyonu

### Network Features
- Client-server architecture
- State senkronizasyonu ([Networked] properties)
- RPC çağrıları ile network event'leri
- Area of Interest sistemi
- Lag compensation mekanikleri

### Server Support
- **Headless Server** build desteği
- **Dedicated Server** modu
- Server/client code ayırımı
- Server optimizasyonları

---

## Proje Yapısı

Proje, modüler ve ölçeklenebilir bir yapıda organize edilmiştir. Tüm scriptler `Scripts/` klasörü altında kategorilere ayrılmıştır:

```
Diablo_Knight_Git/
├── Scripts/
│   ├── Core/              # Temel sistem yöneticileri (Network, Firebase, Loading)
│   ├── Character/         # Karakter yaratma, yönetim ve özelleştirme
│   ├── Player/            # Oyuncu kontrolleri, stats ve davranışlar
│   ├── Combat/            # Savaş sistemleri, silahlar, projektiller
│   ├── Skills/            # Skill sistemi ve tüm skill uygulamaları
│   ├── AI/                # Yapay zeka, canavar davranışları
│   ├── Quest/             # Quest sistemi ve görev mekanikleri
│   ├── Inventory/         # Envanter, crafting, loot sistemleri
│   ├── NPC/               # NPC'ler ve etkileşimler
│   ├── UI/                # Kullanıcı arayüzü elementleri
│   ├── World/             # Dünya sistemleri (area, portal, gathering)
│   ├── Class/             # Sınıf ve ırk sistemleri
│   ├── Party/             # Parti/grup yönetimi
│   ├── PVP/               # PvP mekanikleri
│   ├── Account/           # Hesap yönetimi ve authentication
│   ├── Server/            # Sunucu ve build yönetimi
│   ├── Animation/         # Animasyon sistemleri
│   ├── Camera/            # Kamera kontrolleri
│   └── Utilities/         # Yardımcı araçlar ve utilities
├── PROJECT_SUMMARY.md     # Detaylı proje özeti ve dokümantasyon
└── README.md              # Bu dosya
```

**Detaylı modül açıklamaları için** [PROJECT_SUMMARY.md](PROJECT_SUMMARY.md) dosyasına bakınız.

---

## Kurulum

### Gereksinimler
- Unity 2021.3 veya üzeri
- Photon Fusion SDK
- Firebase Unity SDK
- Hero Editor 4D Asset (Unity Asset Store)
- DuloGames UI Package

### Adımlar

1. **Unity Projesini Klonlayın**
   ```bash
   git clone https://github.com/yourusername/Diablo_Knight_Git.git
   cd Diablo_Knight_Git
   ```

2. **Unity'de Projeyi Açın**
   - Unity Hub'dan "Add" butonuna tıklayın
   - Klonlanan proje klasörünü seçin

3. **Photon Fusion Kurulumu**
   - Photon Dashboard'dan App ID alın
   - Unity'de Photon Settings'e App ID'yi girin

4. **Firebase Kurulumu**
   - Firebase Console'dan yeni proje oluşturun
   - `google-services.json` dosyasını indirip Unity projesine ekleyin
   - Realtime Database ve Authentication'ı etkinleştirin

5. **Asset'leri İçeri Aktarın**
   - Hero Editor 4D'yi Unity Asset Store'dan içeri aktarın
   - DuloGames UI package'ını import edin

6. **Sahneyi Açın**
   - `Scenes/MainGame.unity` sahnesini açın

---

## Kullanım

### Client Olarak Oyun Başlatma

1. Unity Editor'de oyunu çalıştırın (Play butonuna basın)
2. Login ekranında kullanıcı adı/şifre girin veya yeni hesap oluşturun
3. Karakter seçimi yapın veya yeni karakter yaratın
4. Oyun otomatik olarak sunucuya bağlanacaktır

### Dedicated Server Başlatma

1. Build Settings'den "Server Build" seçeneğini seçin
2. "Build and Run" ile sunucu build'i oluşturun
3. Headless modda server başlatılacaktır

### Development Modu

Editor'de test yapmak için:
- `NetworkManager` nesnesindeki ayarları düzenleyin
- Test için `ServerEnvironmentConfig` ScriptableObject'i oluşturun
- Editor'de client ve server'ı aynı anda test edebilirsiniz

---

## Oynanış

### Karakter Yaratma
1. Irk seçimi (Human/Goblin)
2. Sınıf seçimi (Warrior/Archer/Rogue)
3. Görünüm özelleştirme (saç, yüz, renk, vb.)
4. İsim belirleme

### Kontroller (PC)
- **WASD** veya **Ok tuşları**: Hareket
- **1, 2, 3**: Skill kullanımı
- **I**: Envanter
- **C**: Karakter paneli
- **Q**: Quest tracker
- **M**: Minimap
- **Enter**: Chat
- **Mouse Sol Tık**: Hedef seçme / Etkileşim
- **Tab**: Skill swap (slot içinde)

### Kontroller (Mobil)
- **Virtual Joystick**: Hareket
- **Skill Butonları**: Yetenekleri kullan
- **UI Butonları**: Panel açma/kapama

### Skill Sistemi
- Her sınıfın kendine özgü skilleri vardır
- 3 ana skill slot: Utility, Combat, Ultimate
- Her slot'ta 3 skill tutulabilir, aktif olan 1 tanedir
- Tab tuşu ile aktif skill'i değiştirebilirsiniz
- Skill kullanarak XP kazanırsınız ve level atlarsınız

### Quest Yapma
1. NPC'lerin başındaki işaretlere bakın (! = yeni quest, ? = tamamlanmış)
2. NPC ile konuşarak quest'i kabul edin
3. Objective'leri tamamlayın (canavar öldür, item topla, NPC ile konuş)
4. Quest veren NPC'ye geri dönüp ödülü alın

### Crafting
1. Crafting NPC ile konuşun
2. Recipe'leri görüntüleyin
3. Gerekli malzemeleri envanterde bulundurun
4. Craft butonuna basarak item üretin

---

## Networking Mimarisi

### Photon Fusion Kullanımı

Proje **Client-Server** mimarisi kullanır:

- **Server**: Tüm oyun mantığını çalıştırır, authoritative'dir
- **Client**: Input gönderir, görsel ve UI güncellemelerini yapar
- **NetworkBehaviour**: Tüm network objeleri için base class
- **[Networked]**: State senkronizasyonu için property attribute
- **RPC**: Client-server arası event gönderimi

### Firebase Entegrasyonu

- **Authentication**: Oyuncu giriş/kayıt işlemleri
- **Realtime Database**: Karakter verileri, quest ilerlemeleri, inventory
- **Async/Await**: Database operasyonları için non-blocking pattern

### State Senkronizasyonu

Networked property'ler otomatik senkronize edilir:
```csharp
[Networked] public int Health { get; set; }
[Networked] public NetworkString<_32> CharacterName { get; set; }
```

### Optimizasyonlar

- **Area of Interest**: Oyuncular sadece yakınındaki objeleri görür
- **State Authority**: Server'ın oyun mantığı üzerinde tam kontrolü
- **Object Pooling**: Projectile ve efektler için
- **Lag Compensation**: Smooth oyuncu hareketi

---

## Geliştirici Notları

### Yeni Skill Ekleme

1. `Scripts/Skills/` altında `YourSkillExecutor.cs` ve `YourSkillPreview.cs` oluşturun
2. `ISkillExecutor` ve `ISkillPreview` interface'lerini implement edin
3. `SkillDatabase` ScriptableObject'ine skill data'sı ekleyin
4. `SkillExecutorFactory.cs` içinde yeni skill'i register edin

### Yeni Quest Ekleme

1. `QuestData` ScriptableObject oluşturun
2. Objective'leri tanımlayın (Kill, Collect, Talk, GiveItem)
3. Ödülleri belirleyin
4. Quest veren NPC'ye quest'i assign edin

### Yeni Item Ekleme

1. `ItemData` ScriptableObject oluşturun
2. Item type, stats ve sprite'ı tanımlayın
3. `ItemDatabase`'e item'i ekleyin
4. Loot tablolarında kullanın

### Server Build Oluşturma

1. `File > Build Settings`
2. "Server Build" checkbox'ını işaretleyin
3. Platform: PC, Mac & Linux Standalone
4. Target Platform: Linux (veya Windows/Mac)
5. Build ve çalıştırın

### Debugging

- **Network Debug**: `NetworkManager` nesnesinde debug logları etkinleştirin
- **Firebase Debug**: Firebase Console'dan real-time verileri izleyin
- **Profiler**: Unity Profiler ile performans analizi
- **Quest Dev Tools**: `QuestDevTools` componenti ile quest testleri

---

## Katkıda Bulunma

Bu proje şu anda private development aşamasındadır. Katkıda bulunmak isterseniz lütfen iletişime geçin.

---

## Lisans

Bu proje özel bir lisans altındadır. Tüm hakları saklıdır.

---

## İletişim

Proje sahibi: [Your Name]
Email: [your.email@example.com]
GitHub: [https://github.com/yourusername](https://github.com/yourusername)

---

## Teşekkürler

- **Photon Engine** - Harika networking altyapısı için
- **Firebase** - Güvenilir backend servisleri için
- **Hero Editor 4D** - Muhteşem karakter editörü için
- **Unity Technologies** - Güçlü oyun motoru için

---

**Son Güncelleme**: 2025-11-23
**Versiyon**: 1.0
**Durum**: Active Development
