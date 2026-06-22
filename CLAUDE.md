# Proje CLAUDE.md

## Proje Genel Bilgi
- **Tür:** 2D Top-down, Vampire Survivors tarzı
- **Engine:** Unity 6
- **Platform Hedef:** Mobile
- **Geliştirici:** Unity'ye yeni başlıyor — öğrenme süreci öncelikli

## Mevcut Sistem Durumu
- ✅ Player — 8 yönlü hareket, joystick kontrol
- ✅ Düşmanlar — temel AI var, player'a yaklaşıyor
- ✅ Lazer atan düşman tipi
- ✅ Sağlık sistemi — hasar alınca renk solar, can bitince ölüm
- ✅ Tilemap — background oluşturuldu
- 🔄 Geliştirme devam ediyor (Agile, feature'lar netleşiyor)

## Mevcut Sprint
<!-- Her oturumda buraya o anki görevi yaz, bitince güncelle -->
- Aktif görev: —

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
- Object pooling ileride şart olacak (düşman ve mermi spawn için)
- Her sistem birbirinden bağımsız component olarak tasarlanmalı
- Magic number kullanma — balance için SerializeField tercih et

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
