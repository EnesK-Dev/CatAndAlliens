# Proje CLAUDE.md

## Proje Genel Bilgi
- **Tür:** 2D Top-down, Vampire Survivors tarzı
- **Engine:** Unity 6
- **Platform Hedef:** Mobile
- **Geliştirici:** Unity'ye yeni başlıyor — öğrenme süreci öncelikli
- **Oyun dili:** Oyun içi TÜM metinler İngilizce (menü, butonlar, başlıklar, UI). Sohbet/açıklama dili Türkçe kalır.

## Mevcut Sistem Durumu
- ✅ Player — 8 yönlü hareket, joystick kontrol, dash
- ✅ Player sağlık sistemi — `TakeDamage(float)`, hasar alınca kırmızı flash, can bitince ölüm → `OnPlayerDied` (static event) + `OnHealthChanged` event
- ✅ Player oto-saldırı (Vampire Survivors tarzı) — `VampireAttackRoutine`, menzildeki en yakın düşmanı pençeyle vurur. **Single-target** (sadece en yakın 1 düşmana `playerDamage`). Kamera shake.
- ✅ Düşmanlar — 2 ayrı tip/sınıf: `EnemyController` (chase + elite/lazer varyantı) ve `BurstShooterEnemy` (mesafe tutup 3'lü seri atış). Her ikisinin ayrı `TakeDamage(float)`'i var.
- ✅ Düşman spawn — `EnemyGenerator`, çizgi üzerinde rastgele aralıkla (pooling yok, doğrudan Instantiate)
- ✅ Vuruş hissiyatı — kırmızı hit flash + floating damage numbers + düşman ölüm animasyonu (bkz. "Vuruş Hissiyatı Sistemi")
- ✅ Tilemap — background oluşturuldu
- ✅ Bomba yağmuru sistemi — `BombRainSystem` + `BombWarning`, object pool tabanlı
- ✅ Ana menü + Game Over — MainMenu scene, fade geçiş (`SceneLoader`), `AudioManager`, `GameOverUI` (kod tarafı bitti)
- 🔄 Geliştirme devam ediyor (Agile, feature'lar netleşiyor)

## Bomba Yağmuru Sistemi
- **`BombRainSystem.cs`** — Sahneye boş bir GameObject'e eklenir. Pool yönetimi, zamanlama ve alan kontrolü burada.
  - `followTarget` → Player transform'u atanırsa bombalar spawn anında player etrafına düşer (patlama noktası sabit kalır, kaçılabilir)
  - Alan dikdörtgen: `areaWidth` / `areaHeight` ayrı ayrı ayarlanır
  - Gizmo: Scene view'da sarı dikdörtgen gösterir
- **`BombWarning.cs`** — BombWarning Prefab'ının root'una eklenir. Uyarı → patlama → pool'a dön döngüsünü yönetir.
  - `warningSpriteRenderer` → WarningVisual child'ının SpriteRenderer'ı
  - `explosionSpriteRenderer` → ExplosionVisual child'ının SpriteRenderer'ı
  - `explosionFrames` → patlama PNG'leri array olarak atanır, `explosionFrameRate` ile hız ayarlanır
- **Prefab yapısı:** `BombWarning (root + BombWarning.cs)` → `WarningVisual (SpriteRenderer)` + `ExplosionVisual (SpriteRenderer)`
- **Bilinen durum:** Pool boşsa `bombWarningPrefab` null demektir — Inspector'dan prefab atanmalı

## Vuruş Hissiyatı Sistemi (Combat Feel)
- **Hit flash** — `EnemyController` + `BurstShooterEnemy`: düşman vurulunca kısa süre kırmızı olup kendi rengine döner (`hitFlashColor`, `hitFlashDuration`). Eski "canı azalınca saydamlaşma" (`minAlphaLimit`/`UpdateVisualAlpha`) KALDIRILDI. Flash coroutine referansı saklanır, üst üste vuruşta durdurulur. Renk `baseColor` (EnemyController, elite ise mor) / `shooterColor` (Burst) üzerinden döner.
- **Damage numbers (floating text)** — düşman `TakeDamage` içinden `DamagePopupManager.Show(pozisyon, hasar)` çağrılır.
  - **`DamageNumber.cs`** — popup prefab davranışı: yukarı süzülür, solar, pop scale, yatay jitter. Alanlar: `label` (world-space `TextMeshPro`, UI DEĞİL), `lifetime`, `floatDistance`, `horizontalJitter`, `fadeStartPercent`, `popScale`. `SetText("{0}", int)` → alloc yok.
  - **`DamagePopupManager.cs`** — object pool + statik `Show(...)`. Sahnede tek obje; `damageNumberPrefab` + `initialPoolSize` atanır. `_instance` Unity `!= null` ile kontrol (fake-null güvenli); sahnede yoksa sessizce hiçbir şey yapmaz.
  - Prefab: boş GameObject + `TextMeshPro` (3D/world-space) + `DamageNumber`. MeshRenderer sorting order düşmanların ÜSTÜNDE olmalı.
- **Düşman ölüm animasyonu** — `Die()` artık anında Destroy ETMEZ: `isDying=true` → `StopAllCoroutines()` → **`animator.enabled=false`** (KRİTİK: yoksa Animator sprite'ı her kare ezer, death frame görünmez) → `rb.simulated=false` + collider kapatılır → `DeathRoutine` `deathFrames`'i `deathFrameRate` hızında oynatıp `Destroy`. `deathFrames` boşsa anında yok olur. `deathFrames`/`deathFrameRate` her iki düşman prefabında atanır.
- **Single-target vuruş** — `player.cs` `VampireAttackRoutine`: overlap dairesindeki TÜM düşmanlar değil, sadece **en yakın 1** vurulur (`GetClosestEnemyCollider` + `hasHitThisSwing`). `ApplyDamage(Collider2D, float)` iki düşman tipini de destekler.
- **⚠️ Kaldırıldı: `AttackZone`** — eskiden `AttackPoint` objesinde çift hasar veriyordu (34), overlap sistemiyle üst üste biniyordu. Component AttackPoint'ten kaldırıldı (`AttackZone.cs` dosyası duruyor, kullanılmıyor). Tek hasar kaynağı = `playerDamage`.
- **⚠️ Unity serialization tuzağı:** Mevcut bir prefab'a yeni `[SerializeField]` eklenince (özellikle `Color`/`float`), Unity C# initializer'ı değil tip default'unu (0 / siyah-saydam) yükleyebilir. Yeni alan ekledikten sonra Inspector'dan değeri TEYİT et.

## Mevcut Sprint
<!-- Her oturumda buraya o anki görevi yaz, bitince güncelle -->
- Aktif görev: **İlerleme / Güçlenme Sistemi (5 FAZLI)** — FAZLAR SIRAYLA, her faz bitince özet + test + onay, sonra sonrakine geç. Mimariye sadık (pooling, static event, Update'te alloc yok, OverlapCircleNonAlloc).

### 🔄 5 Fazlı Plan — durum
- **FAZ 1 — DifficultyManager (çekirdek):** ✅ KOD YAZILDI (`DifficultyManager.cs`), 🔲 Editor kurulumu YAPILMADI (sahneye obje ekleme bekliyor). Süreye bağlı tek merkezi zorluk kaynağı, static erişim (DamagePopupManager pattern'i). `DifficultyFactor` (0-1, AnimationCurve), `ElapsedTime`, `CurrentMilestone`, `OnMilestoneReached` (static event). Inspector: `timeToMaxDifficulty` (480sn), `difficultyCurve`, `milestoneMinutes {0,2,5,8}`, `showDebugOverlay`. Bu faz sadece altyapı, davranış değişmez. Editor: boş GO "DifficultyManager" + script, SampleScene.
- **FAZ 2 — EnemyGenerator'ı bağlama:** 🔲 Spawn interval'i `DifficultyFactor` ile Lerp (min/max arası azalan). Başta SADECE normal enemy; milestone'larda sırayla önce elite(lazer), sonra burst havuza girsin. Enemy seçimi weighted random, zamanla elite/burst ağırlığı artar. `EnemyController`/`BurstShooterEnemy` class'larına DOKUNMA, sadece spawn tarafı. (Not: FAZ 2 subscriber `Start`'ta `CurrentMilestone`'u da sorgulamalı — geç enable olursa geçmiş milestone'ları yakalamak için.)
- **FAZ 3 — Enemy davranışını güçlendirme:** 🔲 `EnemyController` şarj/telegraph + ateş aralığı `DifficultyFactor` ile kısalsın (alt sınırlı, sıfıra inmesin). `BurstShooterEnemy` mermi sayısı 3→4→5→6 kademeli, seri arası bekleme kısalsın. Hareket hızı hafif artabilir (abartma). Hepsi Inspector curve/threshold, hardcode yok.
- **FAZ 4 — Core drop + toplama:** 🔲 Yeni Core prefab (sprite + trigger collider + `CoreItem`), enemy ölünce death routine'e hook'la pool'dan çekilsin. Player'a yakınsa magnet (MoveTowards). Değince toplanır → static `CoreManager` toplam tutar + `OnCoreCountChanged` event. Core'lar POOLED. Enemy tipine göre miktar (normal:1, elite:2-3, burst:2-3), Inspector'dan.
- **FAZ 5 — Core eşiği + Upgrade paneli:** 🔄 DEVAM. 5.1 (CoreManager eşik/`OnThresholdReached`) ✅, 5.2 (player upgrade API + `SetPaused`) ✅, 5.3 (UpgradeCard + UpgradeSelectionUI kod + Editor kurulumu) ✅ **PANEL ÇALIŞIYOR** (core eşiğine ulaşınca panel açılıyor, timeScale=0, kart seç → stat uygula → kapat). **Model: eşik SAYAÇ — core harcanmaz.** Upgrade'ler: Damage 10 / AttackSpeed 0.85 / AttackRange 1 / MaxHealth 20 (4'ten 3'ü rastgele, stack "Lv.N"). Hiyerarşi: `Canvas > UpgradePanel > [Dimmer + CardRow > Card1/2/3]` + ayrı `UpgradeManager`. Kart PREFAB'lı (`Card1.prefab`). **KALAN son rötuş:** kart İÇİ eleman düzeni (Vertical Layout Group — hepsi merkezde birleşik duruyor, kullanıcı yarım bıraktı) + testler (MaxHealth kalp ikonu artışı, firstThreshold'u test 5'ten 50'ye geri al, Canvas Scaler mobil için ScaleWithScreen). Detay: memory `progression-sprint`.

### ✅ Tamamlanan sprint: Vuruş Hissiyatı (Combat Feel) — 2026-07-06
Kırmızı hit flash + floating damage numbers + single-target vuruş + düşman ölüm animasyonu. Çift hasar veren `AttackZone` temizlendi. Detay: yukarıdaki "Vuruş Hissiyatı Sistemi" bölümü.

### ✅ Tamamlanan sprint: Ana Menü + Game Over
- `MainMenu.unity` (PLAY/SETTINGS/CREDITS) kuruldu & çalışıyor. Build listesi: MainMenu(0) + SampleScene(1). Canvas Scaler 1920x1080 YATAY, Match 0.5.
- Scriptler: `SceneLoader` (fade geçiş, `LoadScene`/`ReloadCurrentScene`/`QuitGame`, `Time.unscaledDeltaTime`), `AudioManager` (ses toggle + PlayerPrefs "SoundEnabled"; her scene'de bir tane olmalı), `MainMenuController`, `GameOverUI` ("YOU DIED" panel + Main Menu/Restart, `pauseGameOnDeath`).
- `player.cs`: `OnPlayerDied` (static event) VAR, `DieRoutine` fırlatıyor. `GameOverUI` bu event'i dinliyor.
- **Açık teyitler (Editor işi, geliştiricide):** (1) MainMenu görsel cila (başlık TMP font, buton büyütme PosY 120/0/-120, panel düzeni) yarım kalmış olabilir. (2) SampleScene'de `GameOverUI` + `AudioManager` sahne kurulumu (obje ekleme + referans bağlama) yapıldı mı teyit edilmeli.

> Mimari uyarı (tüm fazlar): "God GameManager" YAPMA — her sistem (Difficulty/Core/Upgrade) kendi tek-sorumluluklu manager'ı olarak, static erişim + event ile gevşek bağlı kalsın.

## Claude'un Rolü — SINIRLAR ÖNEMLİ

### Yapabilecekleri
- C# script yazmak ve düzenlemek
- Hataları tespit edip düzeltmek
- Yazdığı her kod bloğunu Türkçe açıklamak
- Alternatif yaklaşımlar önermek ve farkı açıklamak

### Yapmaması Gerekenler
- GameObject oluşturma, silme, taşıma (geliştirici kendisi yapar)
- Inspector değerlerini ayarlama (geliştirici kendisi yapar)
- Animator Controller ve Animation Tree düzenleme (geliştirici kendisi yapar)
- Asset import veya proje ayarları değiştirme
- Birden fazla dosyayı aynı anda toplu değiştirme — önce sor

## Kod Standartları
- Dil: C# (.NET Standard 2.1, Unity 6 uyumlu)
- Naming: PascalCase → class, method, property | camelCase → local variable, private field
- Her public method'a /// XML summary ekle
- Magic number kullanma, const veya [SerializeField] kullan
- Region kullan: #region Serialized Fields / Private Fields / Unity Callbacks / Public Methods / Private Methods
- Her script'te OnDestroy veya OnDisable içinde cleanup yap

## Mimari Notlar
- Vampire Survivors tarzı: çok sayıda düşman spawn olacak — performans kritik
- Object pooling aktif kullanılıyor (BombRainSystem'de List + Queue pattern)
- Her sistem birbirinden bağımsız component olarak tasarlanmalı
- Magic number kullanma — balance için SerializeField tercih et
- Coroutine içinde `gameObject.SetActive(false)` çağrılacaksa önce coroutine referansını null'la; aksi halde OnDisable coroutine'i keser ve sonraki satırlar çalışmaz

## Performans Kuralları (Mobile)
- Update() içinde heap allocation yapma (new, string concat, LINQ)
- Physics işlemleri sadece FixedUpdate() içinde
- FindObjectOfType kullanma, reference'ları Awake() veya Inspector'dan al
- Coroutine'leri OnDisable/OnDestroy'da durdur

## Açıklama Formatı
Her script veya değişiklik sonrasında şunu ekle:
1. "Bu script ne yapıyor?" — 2-3 cümle özet
2. "Inspector'da ne ayarlanmalı?" — hangi component, hangi değer
3. "Neden bu yaklaşım?" — alternatif varsa kısaca karşılaştır

## İletişim Dili
- Açıklamalar Türkçe
- Terimler İngilizce (collider, rigidbody, animator, coroutine vb.)
