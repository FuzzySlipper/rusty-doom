namespace LoadingBay.Game;

/// <summary>Live pickup lifecycle over one canonical pickup entity.</summary>
internal sealed class LoadingBayPickupStateComponent
{
    internal LoadingBayPickupStateComponent(LoadingBayPickupLifecycle lifecycle, string cause, ulong tick, ulong triggerRevision)
    {
        Lifecycle = lifecycle;
        Cause = cause;
        Tick = tick;
        TriggerRevision = triggerRevision;
    }

    internal LoadingBayPickupLifecycle Lifecycle { get; set; }
    internal string Cause { get; set; }
    internal ulong Tick { get; set; }
    internal ulong TriggerRevision { get; set; }
}

/// <summary>Live door progression over one canonical door entity.</summary>
internal sealed class LoadingBayDoorStateComponent
{
    internal LoadingBayDoorStateComponent(LoadingBayDoorState state, ulong dueStep)
    {
        State = state;
        DueStep = dueStep;
    }

    internal LoadingBayDoorState State { get; set; }
    internal ulong DueStep { get; set; }
}

/// <summary>Live floor progression over one canonical floor entity.</summary>
internal sealed class LoadingBayFloorStateComponent
{
    internal LoadingBayFloorStateComponent(LoadingBayFloorState state, ulong dueStep)
    {
        State = state;
        DueStep = dueStep;
    }

    internal LoadingBayFloorState State { get; set; }
    internal ulong DueStep { get; set; }
}

/// <summary>Live lift progression over one canonical lift entity.</summary>
internal sealed class LoadingBayLiftStateComponent
{
    internal LoadingBayLiftStateComponent(LoadingBayLiftState state, ulong dueStep)
    {
        State = state;
        DueStep = dueStep;
    }

    internal LoadingBayLiftState State { get; set; }
    internal ulong DueStep { get; set; }
}

/// <summary>Live barrel damage over one canonical barrel entity.</summary>
internal sealed class LoadingBayBarrelStateComponent
{
    internal LoadingBayBarrelStateComponent(int health, bool exploded)
    {
        Health = health;
        Exploded = exploded;
    }

    internal int Health { get; set; }
    internal bool Exploded { get; set; }
}

/// <summary>Live hazard cooldown over one canonical hazard entity.</summary>
internal sealed class LoadingBayHazardStateComponent
{
    internal LoadingBayHazardStateComponent(ulong readyAtStep)
    {
        ReadyAtStep = readyAtStep;
    }

    internal ulong ReadyAtStep { get; set; }
}
