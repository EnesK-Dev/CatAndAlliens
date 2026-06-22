using UnityEngine;

public class EnemyGenerator : MonoBehaviour
{
    [Header("Generator Ayarlari")]
    [SerializeField] private GameObject enemyPrefab; 
    
    
     private float minSpawnDelay=4; // Unity'den girilecek (Örn: 10)
     private float maxSpawnDelay=30; // Unity'den girilecek (Örn: 30)
    
    // Çizginin toplam uzunluğu (Unity'den elinle büyütebilirsin)
    [SerializeField] private float spawnLineLength = 5f; 

    // Çizginin yatay mı (X) yoksa dikey mi (Y) uzanacağını buradan seçeceksin
    [SerializeField] private bool isVerticalLine = false;

    private float nextSpawnTime;

    void Start()
    {
        // Başlangıçta bir sonraki spawn zamanını rastgele belirliyoruz
        CalculateNextSpawnTime();
    }
    void Update()
    {
        if (Time.time >= nextSpawnTime)
        {
            SpawnEnemyOnLine();
            CalculateNextSpawnTime();
        }
    }

    private void CalculateNextSpawnTime()
    {
        float randomDelay = Random.Range(minSpawnDelay, maxSpawnDelay);
        nextSpawnTime = Time.time + randomDelay;
    }

    private void SpawnEnemyOnLine()
    {
        if (enemyPrefab == null) return;

        // Çizginin merkezine göre sol/sağ veya aşağı/yukarı sınırlarını hesaplıyoruz
        float halfLength = spawnLineLength / 2f;
        float randomOffset = Random.Range(-halfLength, halfLength);

        Vector3 spawnPosition = transform.position;

        if (isVerticalLine)
        {
            // Eğer dikey seçildiyse (Sol veya Sağ sınırlar için), uzaylıyı Y ekseninde rastgele dağıt
            spawnPosition.y += randomOffset;
        }
        else
        {
            // Eğer yatay seçildiyse (Üst veya Alt sınırlar için), uzaylıyı X ekseninde rastgele dağıt
            spawnPosition.x += randomOffset;
        }

        // Uzaylıyı tam o hesaplanan çizgi koordinatında var et
        Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
    }

    // Unity Editöründe o şık kılavuz çizgiyi kesintisiz görmek için
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        float halfLength = spawnLineLength / 2f;

        if (isVerticalLine)
        {
            // Dikey çizgi çizimi
            Gizmos.DrawLine(transform.position + new Vector3(0f, -halfLength, 0f), transform.position + new Vector3(0f, halfLength, 0f));
        }
        else
        {
            // Yatay çizgi çizimi
            Gizmos.DrawLine(transform.position + new Vector3(-halfLength, 0f, 0f), transform.position + new Vector3(halfLength, 0f, 0f));
        }
    }
}