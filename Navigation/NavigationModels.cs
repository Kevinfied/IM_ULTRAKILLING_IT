using System;
using UnityEngine;

namespace IMULTRAKILLINGIT.Navigation;

internal enum NavigationState
{
    Disabled,
    Initializing,
    Stabilizing,
    SelectingObjective,
    FollowingRoute,
    Exploring,
    Recovering,
    SuspendedForCombat,
    Failed
}

internal enum NavigationObjectiveType
{
    Door,
    Checkpoint,
    Exit,
    Exploration
}

internal abstract class NavigationObjective
{
    protected NavigationObjective(Component source, NavigationObjectiveType type, int priority, string description)
    {
        Source = source;
        Type = type;
        Priority = priority;
        Description = description;
    }

    public int Id => Source.GetInstanceID();
    public Component Source { get; }
    public NavigationObjectiveType Type { get; }
    public int Priority { get; }
    public string Description { get; }
    public Vector3 Position => Source.transform.position;
    public abstract bool IsAvailable { get; }
    public abstract bool IsCompleted { get; }
}

internal sealed class DoorObjective : NavigationObjective
{
    public DoorObjective(Door door) : base(door, NavigationObjectiveType.Door, 10, $"Door {door.name}") => Door = door;
    public Door Door { get; }
    public override bool IsAvailable => Door != null && Door.isActiveAndEnabled && !Door.locked;
    public override bool IsCompleted => false;
}

internal sealed class CheckpointObjective : NavigationObjective
{
    public CheckpointObjective(CheckPoint checkpoint)
        : base(checkpoint, NavigationObjectiveType.Checkpoint, 40, $"Checkpoint {checkpoint.name}") => Checkpoint = checkpoint;
    public CheckPoint Checkpoint { get; }
    public override bool IsAvailable => Checkpoint != null && Checkpoint.isActiveAndEnabled && !Checkpoint.forceOff;
    public override bool IsCompleted => Checkpoint == null || Checkpoint.activated;
}

internal sealed class ExitObjective : NavigationObjective
{
    public ExitObjective(FinalPit exit) : base(exit, NavigationObjectiveType.Exit, 30, $"Level exit {exit.name}") => Exit = exit;
    public FinalPit Exit { get; }
    public override bool IsAvailable => Exit != null && Exit.isActiveAndEnabled;
    public override bool IsCompleted => false;
}

internal sealed class ExplorationObjective : NavigationObjective
{
    public ExplorationObjective(Transform marker, Vector3 position)
        : base(marker, NavigationObjectiveType.Exploration, 0, "Reachable unexplored frontier") => Target = position;
    public Vector3 Target { get; }
    public override bool IsAvailable => true;
    public override bool IsCompleted => false;
}

internal sealed class NavigationRoute
{
    public NavigationRoute(Vector3 goal, Vector3[] corners, float cost, Vector3 approachDirection)
    {
        Goal = goal;
        Corners = corners;
        Cost = cost;
        ApproachDirection = approachDirection;
    }

    public Vector3 Goal { get; }
    public Vector3[] Corners { get; }
    public float Cost { get; }
    public Vector3 ApproachDirection { get; }
}

internal sealed class NavigationSettings
{
    public NavigationSettings(Func<float> moveSpeed, Func<float> arrivalDistance, Func<float> stuckTimeout,
        Func<float> minimumProgress, Func<float> replanCooldown, Func<float> maximumSafeDrop,
        Func<bool> debugDrawing, Func<bool> verboseLogging)
    {
        MoveSpeed = moveSpeed;
        ArrivalDistance = arrivalDistance;
        StuckTimeout = stuckTimeout;
        MinimumProgress = minimumProgress;
        ReplanCooldown = replanCooldown;
        MaximumSafeDrop = maximumSafeDrop;
        DebugDrawing = debugDrawing;
        VerboseLogging = verboseLogging;
    }

    public Func<float> MoveSpeed { get; }
    public Func<float> ArrivalDistance { get; }
    public Func<float> StuckTimeout { get; }
    public Func<float> MinimumProgress { get; }
    public Func<float> ReplanCooldown { get; }
    public Func<float> MaximumSafeDrop { get; }
    public Func<bool> DebugDrawing { get; }
    public Func<bool> VerboseLogging { get; }
}

internal readonly struct NavigationSnapshot
{
    public NavigationSnapshot(NavigationState state, string objective, string action, string reason,
        Vector3 waypoint, int corner, int cornerCount, int doors, int checkpoints, int exits, int recoveryLevel)
    {
        State = state;
        Objective = objective;
        Action = action;
        Reason = reason;
        Waypoint = waypoint;
        Corner = corner;
        CornerCount = cornerCount;
        Doors = doors;
        Checkpoints = checkpoints;
        Exits = exits;
        RecoveryLevel = recoveryLevel;
    }

    public NavigationState State { get; }
    public string Objective { get; }
    public string Action { get; }
    public string Reason { get; }
    public Vector3 Waypoint { get; }
    public int Corner { get; }
    public int CornerCount { get; }
    public int Doors { get; }
    public int Checkpoints { get; }
    public int Exits { get; }
    public int RecoveryLevel { get; }
}
