# Diablo Knight - Proje Özeti

## Genel Bakış
**Diablo Knight**, Unity oyun motoru kullanılarak geliştirilmiş, Photon Fusion networking altyapısıyla çalışan çok oyunculu bir MMORPG projesidir. Oyun, Diablo benzeri izometrik görünüm ve aksiyon RPG mekanikleriyle tasarlanmıştır.

## Teknik Altyapı

### Ana Teknolojiler
- **Oyun Motoru**: Unity (C#)
- **Networking**: Photon Fusion (Fusion Networking)
- **Veritabase**: Firebase Realtime Database
- **Karakter Sistemi**: Hero Editor 4D Asset
- **UI Framework**: DuloGames UI

### Mimari Yapı
Proje, modüler ve ölçeklenebilir bir yapıda tasarlanmıştır. Tüm scriptler kategorizel olarak organize edilmiştir.

## Klasör Yapısı ve Modüller

### 📁 Scripts/Core
**Temel sistem yöneticileri ve merkezi sistemler**
- `NetworkManager.cs` - Photon Fusion networking yönetimi, oyuncu bağlantıları
- `FirebaseManager.cs` - Firebase veritabanı entegrasyonu ve veri senkronizasyonu
- `LoadingManager.cs` - Sahne yükleme ve geçiş yönetimi
- `LocalPlayerManager.cs` - Lokal oyuncu referanslarının yönetimi

**Sorumluluklar**:
- Network bağlantı yönetimi ve oyuncu spawn işlemleri
- Firebase auth ve database operasyonları
- Sahne geçişleri ve loading screen kontrolü

---

### 📁 Scripts/Character
**Karakter yaratma, özelleştirme ve yönetim sistemleri**
- `Character.cs`, `Character4D.cs` - Ana karakter sınıfları ve 4D karakter sistemi
- `CharacterCreationManager.cs` - Karakter yaratma süreci yönetimi
- `CharacterAppearance.cs` - Görünüm ve kostüm sistemi
- `CharacterSerializer.cs` - Karakter verilerinin serileştirme/deserileştirme
- `CharacterSnapshotSystem.cs` - Karakter snapshot ve preview sistemi
- `CharacterLoader.cs` - Karakter yükleme ve başlatma

**Sorumluluklar**:
- Karakter yaratma akışı (ırk, sınıf, görünüm seçimi)
- Karakter verilerinin Firebase'e kaydedilmesi ve yüklenmesi
- Karakter görünümünün Hero Editor 4D ile yönetimi
- Karakter preview ve snapshot sistemleri

---

### 📁 Scripts/Player
**Oyuncu kontrolleri, istatistikleri ve davranışları**
- `PlayerController.cs` - Ana oyuncu kontrolü, hareket, channelling, teleport
- `PlayerStats.cs` - Oyuncu istatistikleri (HP, Mana, XP, Level, Stats)
- `PlayerStatsDisplay.cs`, `PlayerStatsUI.cs` - İstatistik gösterimi ve UI
- `PlayerManager.cs` - Oyuncu yönetimi ve koordinasyonu
- `PlayerDataSession.cs` - Oyun oturumu veri yönetimi

**Sorumluluklar**:
- Oyuncu hareketi (joystick kontrolü)
- Teleport, bindstone ve gathering channelling sistemleri
- Level, XP ve stat yönetimi
- Oyuncu verilerinin oturum boyunca yönetimi

---

### 📁 Scripts/Combat
**Savaş mekanikleri ve silah sistemleri**
- `CombatInitializer.cs` - Savaş sisteminin başlatılması
- `WeaponSystem.cs` - Silah yönetimi, hasarlar ve saldırı mekanikleri
- `TemporaryBuffSystem.cs` - Geçici buff ve debuff yönetimi
- `DamagePopup.cs` - Hasar gösterimi ve popup efektleri
- `ProjectileBehavior.cs`, `LocalProjectile.cs` - Projektil mekanikleri

**Sorumluluklar**:
- Saldırı hesaplamaları ve hasar uygulaması
- Buff/debuff sistemi ve zamanlayıcıları
- Projektil fizik ve hedef algılama
- Savaş efektleri ve görsel geri bildirimler

---

### 📁 Scripts/Skills
**Yetenek sistemi ve skill uygulamaları**

**Temel Sistem**:
- `SkillSystem.cs` - Ana skill yönetimi, skill ekipmanı
- `SkillDatabase.cs`, `SkillData.cs` - Skill veritabanı ve veri yapıları
- `SkillSlotManager.cs` - Skill slot yönetimi (Utility, Combat, Ultimate)
- `SkillSelectionPanel.cs` - Skill seçim arayüzü
- `SkillTargetingUtils.cs` - Hedefleme ve menzil hesaplamaları
- `ISkillExecutor.cs`, `ISkillPreview.cs` - Skill interface'leri

**Warrior Skills**:
- `BattleRoarExecutor.cs` - AoE buff yeteneği
- `CleaveStrikeExecutor.cs`, `CleaveStrikePreview.cs` - Koni AoE saldırı
- `ColossusStanceExecutor.cs`, `ColossusStancePreview.cs` - Savunma stance
- `GuardedSlamExecutor.cs` - Slam saldırısı
- `SeismicRuptureExecutor.cs`, `SeismicRupturePreview.cs` - Yer yarığı AoE

**Archer Skills**:
- `BlindingShotExecutor.cs`, `BlindingShotPreview.cs` - Kör edici ok
- `PiercingArrowExecutor.cs`, `PiercingArrowPreview.cs` - Delici ok
- `RainOfArrowsExecutor.cs`, `RainOfArrowsPreview.cs` - Ok yağmuru AoE

**Rogue Skills**:
- `EvasiveRollExecutor.cs` - Kaçınma hareketi
- `PiercingThrustExecutor.cs`, `PiercingThrustPreview.cs` - Delici hamle

**Sorumluluklar**:
- Skill aktivasyonu ve cooldown yönetimi
- Skill XP ve level sistemi
- Hedefleme ve AoE hesaplamaları
- Skill preview ve görsel geri bildirimler
- Network senkronizasyonu

---

### 📁 Scripts/AI
**Yapay zeka ve canavar davranışları**
- `MonsterBehaviour.cs` - Ana canavar AI (düşman algılama, saldırı, patrol)
- `MonsterSpawner.cs` - Canavar spawn sistemi ve area yönetimi
- `MonsterManager.cs` - Canavar yönetimi ve koordinasyonu
- `MonsterLootSystem.cs` - Canavar loot tabloları ve drop sistemi
- `MonsterHealthUI.cs`, `MonsterBehaviourUI.cs` - Canavar UI elementleri
- `BotController.cs`, `BotManager.cs` - Bot oyuncu sistemleri

**Sorumluluklar**:
- Canavar AI davranışları (idle, patrol, chase, attack)
- Spawn yönetimi ve area bazlı spawn kontrolleri
- Loot drop mekanikleri ve rastgele item üretimi
- Bot oyuncu simülasyonu

---

### 📁 Scripts/Quest
**Görev sistemi ve quest mekanikleri**
- `QuestManager.cs` - Ana quest yönetimi ve ilerleme takibi
- `QuestGiver.cs`, `DialogQuestGiver.cs` - Quest veren NPC'ler ve dialog sistemi
- `QuestData.cs` - Quest veri yapıları ve objective tanımları
- `QuestTracker.cs` - Aktif quest takip sistemi
- `QuestPersistence.cs`, `QuestSaveQueue.cs` - Quest kaydetme sistemi
- `QuestCompass.cs` - Quest yönlendirme ve pusula sistemi
- `QuestDevTools.cs` - Quest geliştirme araçları

**Sorumluluklar**:
- Quest kabul etme, ilerleme ve tamamlama
- Objective takibi (kill, collect, talk, give item)
- Quest kaydı ve Firebase senkronizasyonu
- Dialog sistemleri ve quest marker'ları
- Ödül dağıtımı

---

### 📁 Scripts/Inventory
**Envanter, eşya ve crafting sistemleri**

**Envanter Sistemi**:
- `InventorySystem.cs`, `InventoryUIManager.cs` - Ana envanter yönetimi
- `EquipmentSystem.cs`, `EquipmentUIManager.cs` - Ekipman ve gear sistemi
- `ItemData.cs`, `ItemDatabase.cs` - Item veri yapıları ve veritabanı
- `ItemInfoPanel.cs` - Item detay gösterimi ve tooltip'ler
- `ItemStatSystem.cs` - Item stat hesaplamaları

**Crafting Sistemi**:
- `CraftSystem.cs` - Ana crafting mekanikleri
- `CraftInventorySystem.cs`, `CraftInventoryUIManager.cs` - Crafting envanteri
- `CraftRecipe.cs`, `RecipeManager.cs` - Tarif sistemi
- `CraftNPC.cs`, `CraftNPCUIManager.cs` - Crafting NPC'leri

**Loot Sistemi**:
- `DroppedLoot.cs`, `DroppedLootUI.cs` - Yere düşen loot yönetimi
- `CoinDrop.cs`, `CoinEffectManager.cs` - Para sistemi
- `FragmentDrop.cs`, `FragmentNotificationUI.cs` - Fragment/material drop sistemi

**Sorumluluklar**:
- Envanter slot yönetimi ve item transfer
- Ekipman giyilmesi ve stat etkileri
- Crafting işlemleri ve tarif kontrolü
- Loot drop ve toplama mekanikleri
- Item serileştirme ve Firebase kaydı

---

### 📁 Scripts/NPC
**NPC sistemleri ve etkileşimler**
- `BaseNPC.cs` - Temel NPC sınıfı
- `IdleNPC.cs`, `WanderingNPC.cs` - NPC davranış tipleri
- `MerchantNPC.cs`, `MerchantPanel.cs` - Tüccar NPC ve alışveriş paneli
- `BlacksmithNPC.cs` - Demirci NPC (upgrade/repair)
- `NPCSpawnerManager.cs` - NPC spawn yönetimi

**Sorumluluklar**:
- NPC etkileşim sistemleri
- Alışveriş mekanikleri
- Item upgrade ve repair işlemleri
- NPC hareket ve idle animasyonları

---

### 📁 Scripts/UI
**Kullanıcı arayüzü yönetimi**

**Ana UI**:
- `UIManager.cs` - Merkezi UI yöneticisi
- `UISlot.cs` - Slot bazlı UI elementleri
- `InfoPanelManager.cs` - Info panel sistemi
- `MinimapController.cs` - Minimap sistemi
- `AreaNotificationUI.cs` - Alan bildirimleri

**Oyuncu UI**:
- `DeathUI.cs` - Ölüm ekranı
- `PotionUI.cs` - İksir kullanım UI'ı
- `BuffIconController.cs` - Buff icon gösterimi

**Sosyal UI**:
- `PartyUIManager.cs`, `PartyMemberItem.cs` - Parti UI'ı
- `PartyAvatarRenderer.cs` - Parti üyesi avatarları
- `ChatManager.cs`, `PrivateChatBar.cs` - Chat sistemi
- `NearbyPlayerItem.cs` - Yakındaki oyuncu listesi

**Channelling UI**:
- `TeleportChannellingUI.cs` - Teleport channelling göstergesi
- `BindstoneChannellingUI.cs` - Bindstone aktivasyon UI'ı
- `GatheringChannellingUI.cs` - Gathering progress bar

**Sorumluluklar**:
- UI panel açma/kapama yönetimi
- Drag & drop işlemleri
- Progress bar ve channelling göstergeleri
- Chat ve sosyal özellikler

---

### 📁 Scripts/World
**Oyun dünyası ve çevre sistemleri**

**Alan Yönetimi**:
- `AreaSystem.cs`, `AreaData.cs` - Oyun alanları ve bölge sistemi
- `Portal.cs`, `TeleportPortal.cs` - Portal ve teleportasyon

**Etkileşim Objeleri**:
- `BindstoneInteraction.cs`, `BindstoneManager.cs` - Bindstone sistemi (spawn point)
- `GatherableObject.cs`, `GatheringSpawner.cs` - Toplanabilir objeler (mining, herbalism)

**Çevre**:
- `DecorationSpawner.cs`, `EditorDecorationSpawner.cs` - Dekorasyon spawn
- `EnvironmentBuildHelper.cs` - Çevre oluşturma araçları
- `DepthFadePrefab.cs` - Derinlik efektleri

**Sorumluluklar**:
- Alan geçişleri ve portal sistemleri
- Gathering mekanikleri
- Bindstone kayıt ve teleport
- Çevre obje yönetimi

---

### 📁 Scripts/Class
**Sınıf ve ırk sistemleri**
- `ClassSystem.cs` - Sınıf mekanikleri (Warrior, Archer, Rogue)
- `ClassData.cs` - Sınıf veri yapıları
- `ClassInfoDisplay.cs` - Sınıf bilgi gösterimi
- `RaceManager.cs`, `RaceSelectionManager.cs` - Irk seçimi ve yönetimi

**Sorumluluklar**:
- Sınıf özellikleri ve pasif yetenekler
- Irk seçimi (Human, Goblin)
- Sınıfa özel stat bonusları

---

### 📁 Scripts/Party
**Grup/Parti sistemi**
- `PartyManager.cs` - Parti oluşturma, davet ve yönetim

**Sorumluluklar**:
- Parti kurma ve üye yönetimi
- XP paylaşımı
- Parti üyesi lokasyon gösterimi

---

### 📁 Scripts/PVP
**Oyuncu vs Oyuncu sistemi**
- `PVPSystem.cs` - PvP mekanikleri ve kuralları

**Sorumluluklar**:
- PvP flag yönetimi
- Oyuncu saldırı mekanikleri
- PvP ödül sistemleri

---

### 📁 Scripts/Account
**Hesap yönetimi**
- `AccountManager.cs` - Kullanıcı hesabı yönetimi
- `SimpleLoginManager.cs` - Giriş/kayıt işlemleri
- `SaveAndExitButton.cs` - Kaydet ve çık fonksiyonu

**Sorumluluklar**:
- Firebase authentication
- Karakter kaydetme/yükleme
- Oturum yönetimi

---

### 📁 Scripts/Server
**Sunucu ve build yönetimi**
- `ServerManager.cs` - Dedicated server yönetimi
- `ServerBuildMenu.cs`, `ServerBuildPreprocessor.cs` - Server build araçları
- `ServerConfigCreator.cs`, `ServerEnvironmentConfig.cs` - Server yapılandırma
- `ServerUIDisabler.cs` - Server modunda UI devre dışı bırakma
- `ServerTileColliderOverride.cs` - Server fizik optimizasyonları

**Sorumluluklar**:
- Headless server build oluşturma
- Server/client ayırımı
- Server optimizasyonları

---

### 📁 Scripts/Animation
**Animasyon sistemi**
- `AnimationManager.cs` - Animasyon kontrolü
- `AnimationMapping.cs` - Animasyon mapping sistemi

**Sorumluluklar**:
- Karakter animasyonlarının kontrolü
- 4 yönlü animasyon sistemi

---

### 📁 Scripts/Camera
**Kamera kontrolleri**
- `SimpleCameraFollow.cs` - Oyuncu takip kamerası
- `CanvasEventCamera.cs` - UI event kamera yönetimi

**Sorumluluklar**:
- Smooth camera follow
- Zoom ve kamera sınırları

---

### 📁 Scripts/Utilities
**Yardımcı araçlar ve utility sınıfları**
- `FusionUtilities.cs` - Fusion networking yardımcıları
- `NetworkMonitor.cs`, `NetworkCharacterController.cs` - Network araçları
- `HitEffectSelfDestruct.cs` - Efekt temizleme
- `TargetAuraController.cs` - Hedef göstergeleri
- `FloatingJoystickManager.cs` - Mobil joystick
- `ProximityDetector.cs` - Yakınlık algılama
- `GlobalLightController.cs`, `InverseLightController.cs` - Işık sistemleri
- `DeathSystem.cs` - Ölüm mekanikleri
- `HeadSnapshotManager.cs`, `HeadPreviewManager.cs` - Karakter başı snapshot

**Sorumluluklar**:
- Genel yardımcı fonksiyonlar
- Efekt yönetimi
- Input sistemi
- Işık ve görsel efektler

---

## Network Mimarisi

### Photon Fusion
- **Client-Server** mimarisi kullanılmaktadır
- **NetworkBehaviour** tabanlı senkronizasyon
- **[Networked]** property'ler ile state yönetimi
- **RPC** çağrıları ile network event'leri

### Firebase Integration
- **Realtime Database** için karakter ve quest verisi
- **Authentication** için kullanıcı giriş/kayıt
- **Async/Await** pattern ile database operasyonları

---

## Oyun Mekanikleri

### Karakter Sistemi
- 2 Irk: Human, Goblin (farklı spawn noktaları)
- 3 Sınıf: Warrior, Archer, Rogue
- Level, XP ve stat sistemi
- Skill tree ve yetenek sistemleri

### Combat Sistemi
- Skill bazlı combat (3 slot: Utility, Combat, Ultimate)
- Her slot'ta 3 skill, aktif olan 1 skill
- Pasif skill slotları
- AoE ve hedef bazlı yetenekler
- Buff/debuff sistemleri

### Quest Sistemi
- Multi-objective quest'ler
- Quest zinciri desteği
- Hidden objective sistemi
- Firebase persistence

### Inventory & Crafting
- Grid bazlı envanter sistemi
- Equipment slot'ları
- Crafting recipe sistemi
- Loot drop mekanikleri

### World Systems
- Area bazlı oyun dünyası
- Portal ve teleportasyon
- Bindstone sistemi (respawn point)
- Gathering mekanikleri

---

## Optimizasyon Stratejileri

### Network
- State senkronizasyonu optimizasyonları
- Area of Interest sistemi
- Lag compensation mekanikleri

### Performance
- Object pooling (projectile, effects)
- Server/client build ayırımı
- Headless server desteği

---

## Geliştirme Notları

### Önemli Bağımlılıklar
- Photon Fusion SDK
- Firebase Unity SDK
- Hero Editor 4D Asset
- DuloGames UI Package

### Kod Standartları
- NetworkBehaviour inheritance network objeleri için
- ScriptableObject bazlı data sistemi
- Event-driven architecture
- Singleton pattern merkezi yöneticiler için

---

## Gelecek Geliştirmeler İçin Notlar

### Eklenebilecek Sistemler
- Guild/clan sistemi
- Auction house
- Pet sistemi
- Mount sistemi
- Dungeon/raid sistemleri
- Achievement sistemi
- Daily quest sistemi
- Seasonal content

### İyileştirme Alanları
- AI pathfinding optimizasyonu
- Anti-cheat sistemleri
- Server-authoritative movement
- Database query optimizasyonları
- UI/UX iyileştirmeleri

---

**Son Güncelleme**: 2025-11-23
**Proje Durumu**: Aktif Geliştirme
