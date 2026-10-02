/// <summary>
/// Spawn aninda "yerel zorluk" (0-1) alabilen dusman. EnemyGenerator, dusmani spawn ederken tipin
/// AÇILISINDAN (unlockMilestone) bu yana gecen sureye gore bir faktor hesaplayip verir. Boylece bir
/// dusman tipi ILK cikinca taban/zayif baslar, zamanla guclenir — global zorluk yuksek olsa bile.
/// Override verilmezse dusman global DifficultyManager.DifficultyFactor'a duser (geriye uyumluluk).
/// </summary>
public interface IDifficultyScaled
{
    /// <summary>Bu dusmanin davranis olceklemesi icin kullanilacak yerel zorluk (0 = taban, 1 = tam).</summary>
    void SetSpawnDifficulty(float factor01);
}
