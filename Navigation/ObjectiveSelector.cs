using System.Collections.Generic;
using UnityEngine;

namespace IMULTRAKILLINGIT.Navigation;

internal sealed class ObjectiveSelector
{
    public bool TrySelect(Vector3 playerPosition, Vector3 up, IReadOnlyList<NavigationObjective> objectives,
        ISet<int> visited, ISet<int> rejected, IReadOnlyDictionary<int, int> failures, NavMeshRoutePlanner planner,
        out NavigationObjective objective, out NavigationRoute route)
    {
        objective = null!;
        route = null!;
        float bestScore = float.NegativeInfinity;
        foreach (NavigationObjective candidate in objectives)
        {
            if (!candidate.IsAvailable || candidate.IsCompleted || visited.Contains(candidate.Id)
                || rejected.Contains(candidate.Id)) continue;
            bool reachable = planner.TryPlan(playerPosition, up, candidate, out NavigationRoute candidateRoute);
            failures.TryGetValue(candidate.Id, out int failureCount);
            float score = DecisionLogic.ScoreNavigationGoal(reachable ? candidateRoute.Cost : float.PositiveInfinity,
                candidate.Priority, candidate.IsAvailable, candidate.IsCompleted, visited.Contains(candidate.Id),
                reachable, failureCount);
            if (score <= bestScore) continue;
            objective = candidate;
            route = candidateRoute;
            bestScore = score;
        }
        return objective != null;
    }
}
