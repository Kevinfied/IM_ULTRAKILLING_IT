using System;
using UnityEngine;

namespace IMULTRAKILLINGIT.Navigation;

internal sealed class NavigationMovementController
{
    private readonly NavigationSettings settings;
    private readonly HazardDetector hazards;
    private NavigationRoute? route;
    private int cornerIndex;

    public NavigationMovementController(NavigationSettings settings, HazardDetector hazards)
    {
        this.settings = settings;
        this.hazards = hazards;
    }

    public bool RouteReached { get; private set; }
    public string Action { get; private set; } = "Idle";
    public string BlockedReason { get; private set; } = string.Empty;
    public int CornerIndex => cornerIndex;
    public int CornerCount => route?.Corners.Length ?? 0;
    public Vector3 CurrentWaypoint => route == null || route.Corners.Length == 0
        ? Vector3.zero
        : route.Corners[Mathf.Clamp(cornerIndex, 0, route.Corners.Length - 1)];
    public NavigationRoute? Route => route;

    public void SetRoute(NavigationRoute next)
    {
        route = next;
        cornerIndex = next.Corners.Length > 1 ? 1 : 0;
        RouteReached = false;
        BlockedReason = string.Empty;
        Action = "Following grounded route";
    }

    public void Clear(string action = "Idle")
    {
        route = null;
        cornerIndex = 0;
        RouteReached = false;
        BlockedReason = string.Empty;
        Action = action;
    }

    public void FixedTick(NewMovement player, NavigationObjective? objective)
    {
        if (route == null || route.Corners.Length == 0) return;
        AdvanceWaypoint(player);
        Vector3 steeringPoint = CurrentWaypoint;
        if (RouteReached && TryGetCrossingTarget(objective, route, out Vector3 crossingTarget))
        {
            RouteReached = false;
            steeringPoint = crossingTarget;
            Action = "Driving through progression trigger";
        }
        else if (RouteReached)
        {
            Decelerate(player);
            return;
        }

        Vector3 up = player.transform.up;
        Vector3 direction = Vector3.ProjectOnPlane(steeringPoint - player.transform.position, up).normalized;
        if (direction.sqrMagnitude < 0.01f) return;
        SmoothLook(player, steeringPoint + up * 0.8f);

        if (!hazards.HasSafeGroundAhead(player, direction, settings.MaximumSafeDrop()))
        {
            BlockedReason = "Unsafe drop ahead";
            Action = "Holding for hazard";
            Decelerate(player);
            return;
        }

        BlockedReason = string.Empty;
        Action = player.standing ? "Run to waypoint" : "Air steer to waypoint";
        Vector3 vertical = Vector3.Project(player.rb.velocity, up);
        Vector3 horizontal = Vector3.ProjectOnPlane(player.rb.velocity, up);
        float speed = settings.MoveSpeed() * (player.standing ? 1f : 0.65f);
        player.rb.velocity = Vector3.MoveTowards(horizontal, direction * speed, 200f * Time.fixedDeltaTime) + vertical;
    }

    public void DrawDebug()
    {
        if (!settings.DebugDrawing() || route == null) return;
        for (int i = 1; i < route.Corners.Length; i++)
            Debug.DrawLine(route.Corners[i - 1] + Vector3.up * 0.15f,
                route.Corners[i] + Vector3.up * 0.15f, i < cornerIndex ? Color.gray : Color.cyan);
        if (!RouteReached) Debug.DrawRay(CurrentWaypoint, Vector3.up * 1.5f, Color.yellow);
        Debug.DrawRay(route.Goal, Vector3.up * 2f, Color.green);
    }

    private void AdvanceWaypoint(NewMovement player)
    {
        float arrivalSquared = settings.ArrivalDistance() * settings.ArrivalDistance();
        Vector3 up = player.transform.up;
        while (cornerIndex < route!.Corners.Length - 1
               && Vector3.ProjectOnPlane(route.Corners[cornerIndex] - player.transform.position, up).sqrMagnitude
               <= arrivalSquared)
            cornerIndex++;
        if (cornerIndex == route.Corners.Length - 1
            && Vector3.ProjectOnPlane(route.Corners[cornerIndex] - player.transform.position, up).sqrMagnitude
            <= arrivalSquared)
        {
            RouteReached = true;
            Action = "Route endpoint reached";
        }
    }

    private static bool TryGetCrossingTarget(NavigationObjective? objective, NavigationRoute route,
        out Vector3 target)
    {
        target = default;
        if (objective is DoorObjective door)
            target = door.Position + route.ApproachDirection * 3f;
        else if (objective is CheckpointObjective checkpoint)
            target = checkpoint.Position + route.ApproachDirection * 2f;
        else
            return false;
        return true;
    }

    private static void Decelerate(NewMovement player)
    {
        Vector3 up = player.transform.up;
        Vector3 vertical = Vector3.Project(player.rb.velocity, up);
        Vector3 horizontal = Vector3.ProjectOnPlane(player.rb.velocity, up);
        player.rb.velocity = Vector3.MoveTowards(horizontal, Vector3.zero, 80f * Time.fixedDeltaTime) + vertical;
    }

    private static void SmoothLook(NewMovement player, Vector3 point)
    {
        if (player.cc == null) return;
        Vector3 direction = point - player.cc.transform.position;
        if (direction.sqrMagnitude < 0.01f) return;
        Vector3 target = Quaternion.LookRotation(direction.normalized, player.transform.up).eulerAngles;
        Vector3 current = player.cc.transform.eulerAngles;
        float turn = 360f * Time.fixedDeltaTime;
        float yaw = Mathf.MoveTowardsAngle(current.y, target.y, turn);
        float currentPitch = -NormalizeAngle(current.x);
        float targetPitch = -NormalizeAngle(target.x);
        float pitch = Mathf.MoveTowards(currentPitch, targetPitch, turn);
        player.cc.ResetCamera(yaw, Mathf.Clamp(pitch, player.cc.minimumX, player.cc.maximumX));
        player.cc.ApplyRotations(false);
    }

    private static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;
}
