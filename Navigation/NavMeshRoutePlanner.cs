using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace IMULTRAKILLINGIT.Navigation;

internal sealed class NavMeshRoutePlanner
{
    private const float SampleRadius = 4f;
    private const float FrontierCellSize = 6f;

    public int LastNavMeshVertexCount { get; private set; }

    public bool ProbeNavMesh()
    {
        LastNavMeshVertexCount = NavMesh.CalculateTriangulation().vertices.Length;
        return LastNavMeshVertexCount > 0;
    }

    public bool TryPlan(Vector3 start, Vector3 up, NavigationObjective objective, out NavigationRoute route)
    {
        Vector3 goal = objective.Position;
        if (!TryCalculate(start, goal, out Vector3[] corners, out float cost, objective is DoorObjective))
        {
            route = null!;
            return false;
        }

        Vector3 approach = HorizontalDirection(corners.Length > 1 ? corners[corners.Length - 2] : start,
            goal, up);
        if (objective is DoorObjective doorObjective && (doorObjective.Door.open || doorObjective.Door.isFullyOpened))
        {
            Vector3 beyond = goal + approach * 3f;
            if (TryCalculate(start, beyond, out Vector3[] throughCorners, out float throughCost))
            {
                goal = beyond;
                corners = throughCorners;
                cost = throughCost;
            }
        }

        route = new NavigationRoute(goal, corners, cost, approach);
        return true;
    }

    public bool TryPlanTo(Vector3 start, Vector3 up, Vector3 goal, out NavigationRoute route)
    {
        if (!TryCalculate(start, goal, out Vector3[] corners, out float cost))
        {
            route = null!;
            return false;
        }
        Vector3 approach = HorizontalDirection(corners.Length > 1 ? corners[corners.Length - 2] : start,
            goal, up);
        route = new NavigationRoute(goal, corners, cost, approach);
        return true;
    }

    public bool TryPlanFrontier(Vector3 start, Vector3 up, Vector3 forward, ISet<Vector2Int> visitedRegions,
        out NavigationRoute route)
    {
        float bestScore = float.NegativeInfinity;
        route = null!;
        foreach (float radius in new[] { 24f, 12f, 6f, 3f })
        foreach (float angle in new[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f })
        {
            Vector3 candidate = start + Quaternion.AngleAxis(angle, up) * forward * radius;
            if (!TryCalculate(start, candidate, out Vector3[] corners, out float cost)) continue;
            Vector3 end = corners[corners.Length - 1];
            bool visited = visitedRegions.Contains(ToRegion(end));
            float score = (visited ? 0f : 1000f) + radius * 4f - cost - Mathf.Abs(angle) * 0.25f;
            if (score <= bestScore) continue;
            bestScore = score;
            route = new NavigationRoute(end, corners, cost,
                HorizontalDirection(corners.Length > 1 ? corners[corners.Length - 2] : start, end, up));
        }
        return route != null;
    }

    public static Vector2Int ToRegion(Vector3 position) =>
        new(Mathf.RoundToInt(position.x / FrontierCellSize), Mathf.RoundToInt(position.z / FrontierCellSize));

    private static bool TryCalculate(Vector3 startPosition, Vector3 goalPosition, out Vector3[] corners,
        out float cost, bool allowPartial = false)
    {
        corners = Array.Empty<Vector3>();
        cost = float.PositiveInfinity;
        if (!NavMesh.SamplePosition(startPosition, out NavMeshHit start, SampleRadius, NavMesh.AllAreas)
            || !NavMesh.SamplePosition(goalPosition, out NavMeshHit end, SampleRadius, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
            || path.corners.Length < 2
            || path.status != NavMeshPathStatus.PathComplete && !allowPartial) return false;
        corners = path.corners;
        cost = 0f;
        for (int i = 1; i < corners.Length; i++) cost += Vector3.Distance(corners[i - 1], corners[i]);
        return true;
    }

    private static Vector3 HorizontalDirection(Vector3 from, Vector3 to, Vector3 up)
    {
        Vector3 direction = Vector3.ProjectOnPlane(to - from, up).normalized;
        return direction.sqrMagnitude > 0.01f ? direction : Vector3.forward;
    }
}
