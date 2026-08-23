/// <summary>
/// Oyundaki tum ses efekti turleri. SfxManager bu id'leri klip(ler)e esler; kod SfxManager.Play(SfxId.X)
/// ile calar. Yeni ses eklemek: buraya bir id ekle + SfxManager entries dizisine klibini ata + ilgili
/// yerden Play cagir.
/// </summary>
public enum SfxId
{
    // ---- Aksiyon ----
    PlayerHit,      // oyuncunun pence vurusu (her swing)
    LaserFire,      // elite dusman lazeri ateslenince
    BoomerangThrow, // bumerang firlatilinca
    BurstShot,      // burst shooter her mermi
    EnemyDeath,     // dusman olum animasyonu
    Dash,           // oyuncu dash

    // ---- Toplama ----
    CoinCollect,    // core (coin) toplandi
    FoodCollect,    // ultFood (yemek) toplandi

    // ---- Oyuncu ----
    PlayerHurt,     // oyuncu hasar aldi

    // ---- Ultimate ----
    Ultimate,       // ulti aktive edildi

    // ---- Combo ----
    RankUp,         // combo rank yukseldi
    SRankLoop,      // S-rank'ta kalindigi surece donen loop (ayri kaynak)

    // ---- UI / Ortam ----
    ButtonClick,    // menu butonu
    BombWarning,    // bomba dusme uyarisi
    BombExplosion,  // bomba patlamasi

    // ---- Muzik (ayri kaynak) ----
    // NOT: Yeni id'ler HEP sona eklenmeli — ortaya eklemek sahnede kayitli enum index'lerini kaydirir.
    GameplayMusic,  // oyun sahnesinde arkada donen muzik (loop)

    UltimateImpact  // ulti beyaz flash / patlama ani (Ultimate = aktivasyon; bu = impact)
}
