namespace Meridian.Server;

/// <summary>
/// Verhalten der Monster aus monsters.json: Zielwahl nach Aggressivität und Karma, Flucht, Gift
/// (monster.kod, brain.kod, poison.kod; docs/monsters-original.md).
/// </summary>
public sealed partial class World
{
    /// <summary>brain.kod: AI_FIGHT_AGGRESSIVE/HYPERAGGRESSIVE hassen jeden Spieler; AI_FIGHT_KARMA_AGGRESSIVE nur
    /// Spieler, deren Karma das entgegengesetzte Vorzeichen hat (Hass |Karma| × 25 / 100 > 0).</summary>
    private static bool Hates(MonsterKind kind, Avatar a)
    {
        if (kind.Aggressive)
            return true;
        if (!kind.KarmaAggressive)
            return false;
        int karma = a.State.Karma;
        return karma != 0 && kind.Karma != 0 && Math.Sign(karma) != Math.Sign(kind.Karma) && Math.Abs(karma) * 25 / 100 > 0;
    }

    /// <summary>Fluchtpunkt: drei Felder vom Ziel weg (brain.kod: ohne Verbündete einfach weglaufen).</summary>
    private static (int row, int col) FleePoint(Monster m, Avatar from)
    {
        double dr = m.Row - from.Row, dc = m.Col - from.Col;
        double len = Math.Max(1, Math.Sqrt(dr * dr + dc * dc));
        int row = (int)Math.Clamp(m.Row + dr / len * 3 * 64, 0, ushort.MaxValue);
        int col = (int)Math.Clamp(m.Col + dc / len * 3 * 64, 0, ushort.MaxValue);
        return (row, col);
    }

    /// <summary>poison.kod MakePoisoned / player.kod AddPoison: sofort 20 Ausdauer weniger; alle 10 s
    /// lossRate/1000 Lebenspunkte (nie unter 1) bis zum Ende der Dauer. Eine neue Vergiftung verstärkt die alte.</summary>
    private void Poison(Avatar a, PoisonAttack poison, DateTime now)
    {
        if (ResistsPoison(a))
            return;
        a.State.Vigor = Math.Max(0, a.State.Vigor - 20);
        bool fresh = !a.Poisoned;
        a.PoisonLossRate += poison.LossRate;
        a.PoisonUntil = now + poison.Duration > a.PoisonUntil ? now + poison.Duration : a.PoisonUntil;
        if (fresh)
            a.NextPoisonTick = now.AddSeconds(10);
        Tell(a, fresh ? "Du bist vergiftet worden!" : "Das Gift in deinen Adern wird stärker.");
    }

    private void TickPoison(Avatar a, DateTime now)
    {
        if (!a.Poisoned || now < a.NextPoisonTick)
            return;
        if (now >= a.PoisonUntil || a.Hp <= 0)
        {
            CurePoison(a);
            return;
        }
        a.NextPoisonTick = now.AddSeconds(10);
        int loss = a.PoisonLossRate + a.PoisonRemainder;
        a.PoisonRemainder = loss % 1000;
        if (loss / 1000 > 0 && a.Hp > 1)
        {
            a.Hp = Math.Max(1, a.Hp - loss / 1000);
            Tell(a, "Das Gift brennt in deinen Adern.");
            a.Session.SendHealth();
        }
    }

    /// <summary>Vergiftung aufheben (Ablauf, Heiltrank, Spruch „Gift heilen“).</summary>
    public void CurePoison(Avatar a)
    {
        if (!a.Poisoned)
            return;
        a.PoisonLossRate = 0;
        a.PoisonRemainder = 0;
        Tell(a, "Das Gift hat deinen Körper verlassen.");
    }
}
