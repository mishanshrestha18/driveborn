using Driveborn.Core.Model;

namespace Driveborn.Core.Combat;

/// <summary>
/// All damage arithmetic. Kept separate from the engine so it can be reasoned
/// about and unit tested on its own - the match/mismatch cliff is the single
/// most important number in the game and it needs to stay visible.
/// </summary>
public static class CombatMath
{
    /// <summary>
    /// Damage a parser lands on a target. A matched parser pierces armour and
    /// hits for 2.5x; a mismatched one is halved and eats full armour. That cliff
    /// is what makes loadout choice - and therefore knowing your own folders -
    /// the core decision of a run.
    /// </summary>
    public static int Strike(ParserTool parser, Entity target, int playerLevel, out bool matched,
        out bool dormantBonus)
    {
        matched = parser.Matches(target.Class);
        dormantBonus = target is { Temperament: Temperament.Dormant, Awake: false };

        var raw = (parser.BaseDamage + (playerLevel * 2)) *
                  (matched ? GameRules.MatchedParserMultiplier : GameRules.MismatchedParserMultiplier);

        var armour = matched && GameRules.MatchedParserPiercesArmour ? 0 : target.Armour;
        var damage = (int)Math.Round(raw) - armour;

        if (dormantBonus) damage = (int)Math.Round(damage * GameRules.DormantStrikeMultiplier);

        return Math.Max(1, damage);
    }

    /// <summary>Damage an entity deals to the Reader on its action.</summary>
    public static int Retaliate(Entity source) =>
        Math.Max(1, source.Kind == EntityKind.Swarm ? source.Power : source.Power);

    /// <summary>How far an entity can reach. Scribes and Wraiths fight at range.</summary>
    public static int AttackRange(Entity e) => e.Kind switch
    {
        EntityKind.Scribe => 4,
        EntityKind.Wraith => 3,
        EntityKind.Boss => 2,
        _ => 1
    };

    /// <summary>Volumes and bosses are immobile - you fight around them, not away from them.</summary>
    public static bool CanMove(Entity e) =>
        e.Class != ExtensionClass.Volume && e.Kind != EntityKind.Boss && e.Footprint < 4;

    /// <summary>XP awarded for a kill. Scales with bulk and with how awake it was.</summary>
    public static int XpFor(Entity e)
    {
        var baseXp = 6 + (e.MaxHp / 6) + (e.Armour * 3);
        var multiplier = e.Temperament switch
        {
            Temperament.Hot => 1.5,
            Temperament.Dormant => 1.25,
            _ => 1.0
        };
        if (e.Kind == EntityKind.Boss) multiplier *= 2.0;
        return (int)Math.Round(baseXp * multiplier);
    }

    /// <summary>Value of a relic dropped by this entity.</summary>
    public static int LootValue(Entity e, bool fromRift)
    {
        var value = 10 + (e.MaxHp / 4) + (e.Armour * 5);
        if (e.Temperament == Temperament.Dormant) value = (int)(value * 1.6); // archaeology pays
        if (e.Kind == EntityKind.Boss) value *= 3;
        if (fromRift) value = (int)(value * GameRules.RiftLootMultiplier);
        return value;
    }
}
