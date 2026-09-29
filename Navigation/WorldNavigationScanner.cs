using System.Collections.Generic;
using UnityEngine;

namespace IMULTRAKILLINGIT.Navigation;

internal sealed class WorldNavigationScanner
{
    private readonly List<NavigationObjective> objectives = new();

    public IReadOnlyList<NavigationObjective> Objectives => objectives;
    public Door[] Doors { get; private set; } = System.Array.Empty<Door>();
    public CheckPoint[] Checkpoints { get; private set; } = System.Array.Empty<CheckPoint>();
    public FinalPit[] Exits { get; private set; } = System.Array.Empty<FinalPit>();
    public int ArenaCount { get; private set; }
    public int ElevatorCount { get; private set; }
    public int SwitchCount { get; private set; }

    public void Refresh()
    {
        objectives.Clear();
        Doors = Object.FindObjectsOfType<Door>();
        Checkpoints = Object.FindObjectsOfType<CheckPoint>();
        Exits = Object.FindObjectsOfType<FinalPit>();
        ArenaCount = Object.FindObjectsOfType<ActivateArena>().Length;
        ElevatorCount = Object.FindObjectsOfType<Elevator>().Length;
        SwitchCount = Object.FindObjectsOfType<LimboSwitch>().Length;

        foreach (FinalPit exit in Exits)
            if (exit != null && exit.isActiveAndEnabled)
                objectives.Add(new ExitObjective(exit));
        foreach (CheckPoint checkpoint in Checkpoints)
            if (checkpoint != null && checkpoint.isActiveAndEnabled)
                objectives.Add(new CheckpointObjective(checkpoint));
        foreach (Door door in Doors)
            if (door != null && door.isActiveAndEnabled)
                objectives.Add(new DoorObjective(door));
    }

    public void MarkNearbyDoors(Vector3 position, float radius, ISet<int> visited)
    {
        float radiusSquared = radius * radius;
        foreach (Door door in Doors)
            if (door != null && (door.transform.position - position).sqrMagnitude < radiusSquared)
                visited.Add(door.GetInstanceID());
    }

    public void SeedCompletedProgress(ISet<int> visited)
    {
        foreach (CheckPoint checkpoint in Checkpoints)
        {
            if (checkpoint == null || !checkpoint.activated) continue;
            visited.Add(checkpoint.GetInstanceID());
            MarkNearbyDoors(checkpoint.transform.position, 4f, visited);
        }
    }
}
