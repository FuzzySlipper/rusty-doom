using Rusty.Engine.Mechanics;

namespace LoadingBay.Game;

/// <summary>Canonical stat/track identities for Loading Bay vitality.</summary>
internal static class LoadingBayStatIds
{
    internal static readonly StatId HealthMax = StatId.Parse("loading-bay.health-max");
    internal static readonly TrackId Health = TrackId.Parse("loading-bay.health");
    internal static readonly StatId ArmorMax = StatId.Parse("loading-bay.armor-max");
    internal static readonly TrackId Armor = TrackId.Parse("loading-bay.armor");
    internal static readonly StatId VitalityMax = StatId.Parse("loading-bay.vitality-max");
    internal static readonly TrackId Vitality = TrackId.Parse("loading-bay.vitality");
}

/// <summary>
/// Builds the canonical <see cref="StatsComponent"/> for the player from
/// admitted tuning. Tracks share their maximum <see cref="Stat"/> references.
/// Quantization and rounding match the previous standalone-track behavior
/// exactly: quantum 1 with ToZero rounding on tracks.
/// </summary>
internal static class LoadingBayStats
{
    internal static StatsComponent ForPlayer(LoadingBayTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        StatsComponent stats = new();
        Stat healthMax = new(tuning.MaximumHealth, minimum: 0, maximum: tuning.MaximumHealth,
            quantum: 1, integerRounding: MidpointRounding.ToZero);
        stats.AddStat(LoadingBayStatIds.HealthMax, healthMax);
        stats.AddTrack(LoadingBayStatIds.Health, new Track(
            healthMax, tuning.StartingHealth,
            quantum: 1, rounding: MidpointRounding.ToZero, integerRounding: MidpointRounding.ToZero));
        Stat armorMax = new(tuning.MaximumArmor, minimum: 0, maximum: tuning.MaximumArmor,
            quantum: 1, integerRounding: MidpointRounding.ToZero);
        stats.AddStat(LoadingBayStatIds.ArmorMax, armorMax);
        stats.AddTrack(LoadingBayStatIds.Armor, new Track(
            armorMax, tuning.StartingArmor,
            quantum: 1, rounding: MidpointRounding.ToZero, integerRounding: MidpointRounding.ToZero));
        return stats;
    }

    internal static StatsComponent ForEnemy(int maximumHealth)
    {
        if (maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
        StatsComponent stats = new();
        Stat vitalityMax = new(maximumHealth, minimum: 0, maximum: maximumHealth,
            quantum: 1, integerRounding: MidpointRounding.ToZero);
        stats.AddStat(LoadingBayStatIds.VitalityMax, vitalityMax);
        stats.AddTrack(LoadingBayStatIds.Vitality, new Track(
            vitalityMax, maximumHealth,
            quantum: 1, rounding: MidpointRounding.ToZero, integerRounding: MidpointRounding.ToZero));
        return stats;
    }
}

/// <summary>Enemy posture, visibility, and readiness over one canonical enemy entity.</summary>
internal sealed class LoadingBayEnemyStateComponent
{
    internal LoadingBayEnemyStateComponent(LoadingBayEnemyPosture posture, ulong readyAtTick)
    {
        Posture = posture;
        ReadyAtTick = readyAtTick;
    }

    internal LoadingBayEnemyPosture Posture { get; set; }
    internal bool Visible { get; set; }
    internal ulong ReadyAtTick { get; set; }
}
