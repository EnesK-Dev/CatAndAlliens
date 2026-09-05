using UnityEngine;

/// <summary>
/// Hit-flash yardimcisi. Custom/SpriteFlash shader'ini kullanan bir SpriteRenderer'da
/// per-renderer MaterialPropertyBlock ile _FlashColor / _FlashAmount surer. Material KOPYALANMAZ
/// (paylasimli material + MPB) — mobil icin ucuz. amount=1 -> dolu flash rengi, amount=0 -> normal.
/// Not: SpriteRenderer.color (tint) ayri bir kanaldir; bu yardimci ona dokunmaz.
/// </summary>
public static class FlashFx
{
    #region Private Fields
    private static MaterialPropertyBlock _mpb;
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int FlashColorID = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    #endregion

    #region Public Methods
    /// <summary>
    /// Dusmanin TINT rengini _Color'a surer (MPB). URP 2D'de SpriteRenderer.color custom shader'a
    /// ulasmadigi icin gorsel renk buradan gelir. Spawn'da ve renk her degistiginde cagrilir.
    /// </summary>
    public static void SetTint(SpriteRenderer sr, Color tint)
    {
        if (sr == null) return;
        _mpb ??= new MaterialPropertyBlock();
        sr.GetPropertyBlock(_mpb);
        _mpb.SetColor(ColorID, tint);
        sr.SetPropertyBlock(_mpb);
    }

    /// <summary>Verilen SpriteRenderer'da flash rengini ve miktarini (0-1) ayarlar (tint'e dokunmaz).</summary>
    public static void Set(SpriteRenderer sr, Color flashColor, float amount)
    {
        if (sr == null) return;
        _mpb ??= new MaterialPropertyBlock();
        sr.GetPropertyBlock(_mpb);
        _mpb.SetColor(FlashColorID, flashColor);
        _mpb.SetFloat(FlashAmountID, amount);
        sr.SetPropertyBlock(_mpb);
    }

    /// <summary>Flash'i tamamen kapatir (amount=0). Olum/temizlik icin.</summary>
    public static void Clear(SpriteRenderer sr) => Set(sr, Color.white, 0f);
    #endregion
}
