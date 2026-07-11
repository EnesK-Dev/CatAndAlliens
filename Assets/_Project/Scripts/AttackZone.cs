using UnityEngine;

public class AttackZone : MonoBehaviour
{
    [Header("Hasar Ayari")]
    // Kedinin bu vuruş alanıyla uzaylıya tek seferde vereceği hasar miktarı
    // Örneğin uzaylının canı 100 ise ve bunu Unity'den 50 yaparsan tam 2 vuruşta ölürler
    [SerializeField] private float attackDamage ; 

    private void OnTriggerEnter2D(Collider2D collision)
    {
        EnemyController enemy = collision.GetComponent<EnemyController>();
        if (enemy != null)
        {
            // Sabit sayı yerine yukarıdaki 'attackDamage' değişkenini gönderiyoruz
            enemy.TakeDamage(attackDamage);
            Debug.Log("Uzaylıya " + attackDamage + " hasar verildi!");
            return;
        }

        // EnemyController değilse burst shooter olabilir — ayrı sınıf, kendi TakeDamage'i var
        BurstShooterEnemy burst = collision.GetComponent<BurstShooterEnemy>();
        if (burst != null)
        {
            burst.TakeDamage(attackDamage);
            Debug.Log("Burst shooter'a " + attackDamage + " hasar verildi!");
        }
    }
}