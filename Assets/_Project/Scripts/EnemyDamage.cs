using UnityEngine;

/// <summary>
/// Bir collider'a (dusman/boss) hasar uygulayan tek noktali yardimci. Dort dusman tipini ve boss'u
/// tanir; kimseye denk gelmezse false doner. Player saldirisi ve silah mermileri buradan gecer (DRY).
/// </summary>
public static class EnemyDamage
{
    /// <summary>Collider bir dusman/boss ise TakeDamage cagirir ve true doner; degilse false.</summary>
    public static bool Apply(Collider2D col, float damage)
    {
        if (col == null) return false;

        BossController boss = col.GetComponent<BossController>();
        if (boss != null) { boss.TakeDamage(damage); return true; }

        EnemyController enemy = col.GetComponent<EnemyController>();
        if (enemy != null) { enemy.TakeDamage(damage); return true; }

        BurstShooterEnemy burst = col.GetComponent<BurstShooterEnemy>();
        if (burst != null) { burst.TakeDamage(damage); return true; }

        BoomerangEnemy boom = col.GetComponent<BoomerangEnemy>();
        if (boom != null) { boom.TakeDamage(damage); return true; }

        return false;
    }

    /// <summary>
    /// NUKE (boss-oncesi bombardiman) hasari: dusmanlari NukeKill ile oldurur (core birakir, ultFood BIRAKMAZ).
    /// Boss'a DEGMEZ (bombardiman boss'u vurmaz). Dusman disi collider'da false doner.
    /// </summary>
    public static bool ApplyNuke(Collider2D col, float damage)
    {
        if (col == null) return false;

        EnemyController enemy = col.GetComponent<EnemyController>();
        if (enemy != null) { enemy.NukeKill(damage); return true; }

        BurstShooterEnemy burst = col.GetComponent<BurstShooterEnemy>();
        if (burst != null) { burst.NukeKill(damage); return true; }

        BoomerangEnemy boom = col.GetComponent<BoomerangEnemy>();
        if (boom != null) { boom.NukeKill(damage); return true; }

        return false; // boss vb. — nuke hasar vermez
    }
}
