/// <summary>
/// Oyun akisinin bitip bitmedigini tutan minik global bayrak. Kazanma (WinUI) veya olum (GameOverUI)
/// bunu true yapar; boylece timeScale'i tekrar 1'e cekmeye calisan sistemler (ornek: UltimateCinematic
/// sinema bitisi) oyun bittiginde onu YENIDEN acmaz — oyun donmus kalir. Sahne (yeniden) yuklenince
/// WinUI.Start icinde false'a doner. God manager degil: tek bir durum bayragi.
/// </summary>
public static class GameFlow
{
    /// <summary>Oyun kazanildi ya da kaybedildi mi? True iken kimse timeScale'i 1'e dondurmemeli.</summary>
    public static bool Ended;

    /// <summary>Oyun DURAKLATILDI mi? (PauseMenu set eder.) True iken timeScale'i 1'e cekmeye calisan
    /// sistemler (ulti sinemasi) oyunu YENIDEN baslatmaz; ayrica ulti sinemasi kendi de donar.</summary>
    public static bool Paused;
}
