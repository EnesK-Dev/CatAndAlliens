using UnityEngine;

/// <summary>
/// Bir collider'a (dusman/boss) hasar uygulayan tek noktali yardimci. Dusman tiplerini ve boss'u
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

        return false;
    }

    /// <summary>
    /// Hasar + KNOCKBACK: dusmani 'knockDir' yonunde iter (silah vurus hissi). Boss itilmez (sadece hasar).
    /// Dusman disi collider'da false doner. Silah mermileri (yildiz/burst/bumerang) bunu kullanir.
    /// </summary>
    public static bool Apply(Collider2D col, float damage, Vector2 knockDir, float knockSpeed, float knockDuration)
    {
        if (col == null) return false;

        BossController boss = col.GetComponent<BossController>();
        if (boss != null) { boss.TakeDamage(damage); return true; } // boss itilmez

        EnemyController enemy = col.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            // Splitter BOSS (ve soyu) knockback YEMEZ — boss itilmemeli. Normal dusmanlar itilir.
            SplitterEnemy sp = enemy as SplitterEnemy;
            if (sp == null || !sp.IsBossLineage) enemy.ApplyKnockback(knockDir, knockSpeed, knockDuration);
            return true;
        }

        BurstShooterEnemy burst = col.GetComponent<BurstShooterEnemy>();
        if (burst != null) { burst.TakeDamage(damage); burst.ApplyKnockback(knockDir, knockSpeed, knockDuration); return true; }

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
        if (enemy != null)
        {
            // Splitter BOSS soyunu bombardiman oldurmesin (spawn ani havada kalan bombaya kurban gitmesin).
            SplitterEnemy sp = enemy as SplitterEnemy;
            if (sp != null && sp.IsBossLineage) return true; // "islendi" say ama hasar verme
            enemy.NukeKill(damage);
            return true;
        }

        BurstShooterEnemy burst = col.GetComponent<BurstShooterEnemy>();
        if (burst != null) { burst.NukeKill(damage); return true; }

        return false; // boss vb. — nuke hasar vermez
    }
}
