using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMULTRAKILLINGIT.Navigation;

internal sealed class NavigationController
{
    private readonly ManualLogSource logger;
    private readonly NavigationSettings settings;
    private readonly WorldNavigationScanner scanner = new();
    private readonly NavMeshRoutePlanner planner = new();
    private readonly ObjectiveSelector selector = new();
    private readonly NavigationMovementController movement;
    private readonly StuckDetector stuck;
    private readonly NavigationRecoveryController recovery = new();
    private readonly HashSet<int> visitedObjectives = new();
    private readonly HashSet<Vector2Int> visitedRegions = new();
    private NavigationObjective? objective;
    private NavigationRoute? route;
    private NavigationState state = NavigationState.Disabled;
    private string action = "Idle";
    private string reason = "Not enabled";
    private int sceneHandle = -1;
    private float nextRegionSample;
    private float nextDynamicCheck;
    private Vector3 lastPosition;

    public NavigationController(ManualLogSource logger, NavigationSettings settings)
    {
        this.logger = logger;
        this.settings = settings;
        movement = new NavigationMovementController(settings, new HazardDetector());
        stuck = new StuckDetector(settings);
    }

    public NavigationState State => state;
    public bool HasControl => state is NavigationState.FollowingRoute or NavigationState.Exploring
        or NavigationState.Recovering;
    public NavigationSnapshot Snapshot => new(state, objective?.Description ?? "none", movement.Action,
        reason, movement.CurrentWaypoint, movement.CornerIndex, movement.CornerCount, scanner.Doors.Length,
        scanner.Checkpoints.Length, scanner.Exits.Length, recovery.Level);

    public void Enable(NewMovement player)
    {
        visitedObjectives.Clear();
        visitedRegions.Clear();
        recovery.Reset();
        movement.Clear("Initializing navigation");
        objective = null;
        route = null;
        sceneHandle = SceneManager.GetActiveScene().handle;
        scanner.Refresh();
        scanner.SeedCompletedProgress(visitedObjectives);
        scanner.MarkNearbyDoors(player.transform.position, 2.5f, visitedObjectives);
        bool navMeshAvailable = planner.ProbeNavMesh();
        lastPosition = player.transform.position;
        nextRegionSample = 0f;
        nextDynamicCheck = 0f;
        Transition(player.standing ? NavigationState.SelectingObjective : NavigationState.Stabilizing,
            player.standing ? "Inspecting current progression state" : "Enabled mid-air; waiting for landing");
        logger.LogInfo($"[NAV] Scan: {scanner.Doors.Length} doors, {scanner.Checkpoints.Length} checkpoints, "
                       + $"{scanner.Exits.Length} exits, {scanner.ArenaCount} arenas, {scanner.ElevatorCount} elevators, "
                       + $"{scanner.SwitchCount} switches; NavMesh vertices={planner.LastNavMeshVertexCount}");
        if (!navMeshAvailable) logger.LogWarning("[NAV] No runtime NavMesh data found; safe navigation will wait.");
    }

    public void Disable(string why = "Disabled")
    {
        movement.Clear("Idle");
        objective = null;
        route = null;
        Transition(NavigationState.Disabled, why);
    }

    public void ResetForRestart()
    {
        movement.Clear("Waiting for restarted scene");
        objective = null;
        route = null;
        sceneHandle = -1;
        Transition(NavigationState.Initializing, "Level restart requested");
    }

    public void Update(NewMovement player, bool combatActive)
    {
        if (state == NavigationState.Disabled) return;
        if (state == NavigationState.Initializing || SceneManager.GetActiveScene().handle != sceneHandle)
        {
            Enable(player);
            return;
        }
        if (player.dead || player.levelOver)
        {
            movement.Clear("Player unavailable");
            Transition(NavigationState.Failed, "Player dead or level complete");
            return;
        }
        if (combatActive)
        {
            SuspendForCombat();
            return;
        }
        if (state == NavigationState.SuspendedForCombat)
        {
            scanner.Refresh();
            objective = null;
            route = null;
            movement.Clear("Replanning after combat");
            Transition(player.standing ? NavigationState.SelectingObjective : NavigationState.Stabilizing,
                "Combat ended; replanning from current position");
        }

        RecordCurrentRegion(player);
        if (state == NavigationState.Stabilizing)
        {
            action = "Preserve air state and land";
            if (player.standing)
                Transition(NavigationState.SelectingObjective, "Landed; inspect current progression state");
            return;
        }
        if (state == NavigationState.Recovering)
        {
            if (!recovery.Active)
                Transition(NavigationState.SelectingObjective, "Recovery maneuver complete; replan");
            return;
        }
        if (state == NavigationState.Failed)
        {
            if (Time.unscaledTime >= nextDynamicCheck)
                Transition(NavigationState.SelectingObjective, "Retrying world inspection after navigation failure");
            return;
        }
        if (state == NavigationState.SelectingObjective)
        {
            SelectObjective(player);
            return;
        }
        if (state is not (NavigationState.FollowingRoute or NavigationState.Exploring)) return;

        if (Vector3.Distance(lastPosition, player.transform.position) > 15f)
        {
            Replan("Player displaced far from route");
            lastPosition = player.transform.position;
            return;
        }
        lastPosition = player.transform.position;

        if (objective == null || route == null)
        {
            Replan("Route state missing");
            return;
        }
        if (objective.IsCompleted)
        {
            CompleteObjective(player, "Objective completed");
            return;
        }
        if (!objective.IsAvailable)
        {
            Replan("Objective became unavailable");
            return;
        }
        if (objective is DoorObjective doorObjective)
        {
            float crossing = Vector3.Dot(player.transform.position - doorObjective.Position,
                route.ApproachDirection);
            if (DecisionLogic.HasCrossedDoor(crossing))
            {
                visitedObjectives.Add(objective.Id);
                scanner.MarkNearbyDoors(player.transform.position, 2.5f, visitedObjectives);
                CompleteObjective(player, "Crossed doorway");
                return;
            }
            if (doorObjective.Door.open || doorObjective.Door.isFullyOpened)
            {
                Vector3 beyond = doorObjective.Position + route.ApproachDirection * 5f;
                if (planner.TryPlanTo(player.transform.position, player.transform.up, beyond, out NavigationRoute through))
                    SetRoute(player, objective, through, NavigationState.FollowingRoute, "Door opened; path through it");
            }
        }

        if (movement.RouteReached)
        {
            if (objective.Type == NavigationObjectiveType.Exploration)
            {
                visitedRegions.Add(NavMeshRoutePlanner.ToRegion(route.Goal));
                CompleteObjective(player, "Exploration frontier reached");
            }
            else
                action = "At objective; waiting for its trigger/state change";
        }

        if (stuck.Check(player.transform.position, movement.CurrentWaypoint))
        {
            string blocked = string.IsNullOrEmpty(movement.BlockedReason)
                ? $"No route progress ({stuck.LastProgress:0.0}m)"
                : movement.BlockedReason;
            BeginRecovery(player, blocked);
        }

        movement.DrawDebug();
    }

    public void FixedTick(NewMovement player)
    {
        if (state == NavigationState.Recovering && recovery.Active)
            recovery.FixedTick(player, settings.MoveSpeed());
        else if (state is NavigationState.FollowingRoute or NavigationState.Exploring)
            movement.FixedTick(player, objective);
    }

    private void SelectObjective(NewMovement player)
    {
        scanner.Refresh();
        if (selector.TrySelect(player.transform.position, player.transform.up, scanner.Objectives, visitedObjectives,
                recovery.Failures, planner, out NavigationObjective selected,
                out NavigationRoute selectedRoute))
        {
            SetRoute(player, selected, selectedRoute, NavigationState.FollowingRoute,
                $"Selected {selected.Description}, cost {selectedRoute.Cost:0.0}");
            return;
        }
        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, player.transform.up).normalized;
        if (planner.TryPlanFrontier(player.transform.position, player.transform.up, forward, visitedRegions,
                out NavigationRoute frontier))
        {
            var exploration = new ExplorationObjective(player.transform, frontier.Goal);
            SetRoute(player, exploration, frontier, NavigationState.Exploring,
                $"No semantic objective reachable; exploring frontier cost {frontier.Cost:0.0}");
            return;
        }
        movement.Clear("No safe route");
        nextDynamicCheck = Time.unscaledTime + 2f;
        Transition(NavigationState.Failed, "No complete NavMesh route or unexplored frontier found");
    }

    private void SetRoute(NewMovement player, NavigationObjective nextObjective, NavigationRoute nextRoute,
        NavigationState nextState, string why)
    {
        objective = nextObjective;
        route = nextRoute;
        movement.SetRoute(nextRoute);
        stuck.Reset(player.transform.position, movement.CurrentWaypoint);
        lastPosition = player.transform.position;
        nextDynamicCheck = Time.unscaledTime + Mathf.Max(0.25f, settings.ReplanCooldown());
        Transition(nextState, why);
        if (settings.VerboseLogging())
            logger.LogInfo($"[NAV] Objective selected: {nextObjective.Description}; "
                           + $"route={nextRoute.Corners.Length} corners cost={nextRoute.Cost:0.0}");
    }

    private void CompleteObjective(NewMovement player, string why)
    {
        if (objective != null && objective.Type != NavigationObjectiveType.Exploration)
            visitedObjectives.Add(objective.Id);
        objective = null;
        route = null;
        movement.Clear("Selecting next objective");
        Transition(NavigationState.SelectingObjective, why);
        scanner.Refresh();
        lastPosition = player.transform.position;
    }

    private void BeginRecovery(NewMovement player, string why)
    {
        int objectiveId = objective?.Id ?? 0;
        recovery.Begin(player, objectiveId);
        movement.Clear("Recovery maneuver");
        route = null;
        Transition(NavigationState.Recovering, $"{why}; recovery level {recovery.Level}");
        logger.LogWarning($"[NAV] Recovery {recovery.Level}: {why}");
    }

    private void Replan(string why)
    {
        objective = null;
        route = null;
        movement.Clear("Replanning");
        Transition(NavigationState.SelectingObjective, why);
    }

    private void SuspendForCombat()
    {
        if (state == NavigationState.SuspendedForCombat) return;
        objective = null;
        route = null;
        movement.Clear("Combat owns movement");
        Transition(NavigationState.SuspendedForCombat, "Enemy requires attention");
    }

    private void RecordCurrentRegion(NewMovement player)
    {
        if (Time.unscaledTime < nextRegionSample) return;
        nextRegionSample = Time.unscaledTime + 1f;
        visitedRegions.Add(NavMeshRoutePlanner.ToRegion(player.transform.position));
    }

    private void Transition(NavigationState next, string why)
    {
        if (state == next && reason == why) return;
        state = next;
        reason = why;
        action = next.ToString();
        if (settings.VerboseLogging()) logger.LogInfo($"[NAV] {state}: {reason}");
    }
}
