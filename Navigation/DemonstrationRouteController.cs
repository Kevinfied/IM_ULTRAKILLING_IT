using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMULTRAKILLINGIT.Navigation;

internal sealed class DemonstrationRouteController
{
    [Serializable]
    private sealed class RouteData
    {
        public string scene = string.Empty;
        public List<RoutePoint> points = new();
    }

    [Serializable]
    private sealed class RoutePoint
    {
        public Vector3 position;
        public float yaw;
        public float pitch;
        public bool jump;
        public bool fire;
    }

    private readonly ManualLogSource logger;
    private readonly Func<float> moveSpeed;
    private RouteData? route;
    private int sceneHandle = -1;
    private int completedSceneHandle = -1;
    private int pointIndex;
    private float nextSample;
    private bool suspended;

    public DemonstrationRouteController(ManualLogSource logger, Func<float> moveSpeed)
    {
        this.logger = logger;
        this.moveSpeed = moveSpeed;
    }

    public bool Recording { get; private set; }
    public bool PlaybackActive { get; private set; }
    public bool FireRequested { get; private set; }
    public string Status { get; private set; } = "No recorded route active";
    public int PointIndex => pointIndex;
    public int PointCount => route?.points.Count ?? 0;
    public Vector3 CurrentWaypoint => PlaybackActive && route != null && pointIndex < route.points.Count
        ? route.points[pointIndex].position
        : Vector3.zero;

    public void ToggleRecording(NewMovement player)
    {
        if (Recording)
        {
            StopRecording();
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        route = new RouteData { scene = scene.name };
        sceneHandle = scene.handle;
        Recording = true;
        PlaybackActive = false;
        nextSample = 0f;
        AddPoint(player, false, false);
        Status = $"Recording {scene.name}";
        logger.LogInfo($"[ROUTE] Recording started for {scene.name}. Press F2 to save.");
    }

    public void RecordFrame(NewMovement player)
    {
        if (!Recording || route == null) return;
        if (SceneManager.GetActiveScene().handle != sceneHandle)
        {
            StopRecording();
            return;
        }
        bool jump = Input.GetKeyDown(KeyCode.Space);
        bool fire = Input.GetMouseButton(0);
        Vector3 previous = route.points[route.points.Count - 1].position;
        if (!jump && !fire && Time.unscaledTime < nextSample
            && (player.transform.position - previous).sqrMagnitude < 2.25f) return;
        AddPoint(player, jump, fire);
    }

    public void StopRecording()
    {
        if (!Recording || route == null) return;
        Recording = false;
        string path = RoutePath(route.scene);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonUtility.ToJson(route, true));
        Status = $"Saved {route.points.Count} route points";
        logger.LogInfo($"[ROUTE] Saved {route.points.Count} points to {path}");
    }

    public void ResetPlayback()
    {
        PlaybackActive = false;
        FireRequested = false;
        suspended = false;
        sceneHandle = -1;
        completedSceneHandle = -1;
        pointIndex = 0;
    }

    public bool EnsurePlayback(NewMovement player)
    {
        if (Recording) return false;
        Scene scene = SceneManager.GetActiveScene();
        if (PlaybackActive && scene.handle == sceneHandle) return true;
        if (scene.handle == completedSceneHandle) return false;
        string path = RoutePath(scene.name);
        if (!File.Exists(path)) return false;
        RouteData? loaded = JsonUtility.FromJson<RouteData>(File.ReadAllText(path));
        if (loaded?.points == null || loaded.points.Count < 2) return false;
        route = loaded;
        sceneHandle = scene.handle;
        pointIndex = Vector3.Distance(player.transform.position, loaded.points[0].position) <= 10f
            ? 0
            : FindNearestPoint(player.transform.position, 0, loaded.points.Count);
        PlaybackActive = true;
        suspended = false;
        Status = $"Replaying {scene.name}";
        logger.LogInfo($"[ROUTE] Replaying {loaded.points.Count} recorded points for {scene.name}.");
        return true;
    }

    public void SetSuspended(bool value, NewMovement player)
    {
        if (!PlaybackActive || suspended == value) return;
        suspended = value;
        if (!value && route != null)
            pointIndex = FindNearestPoint(player.transform.position, pointIndex,
                Mathf.Min(route.points.Count, pointIndex + 100));
        Status = value ? "Recorded route paused for combat" : "Resuming recorded route";
    }

    public bool ConsumeFireRequest()
    {
        bool result = FireRequested;
        FireRequested = false;
        return result;
    }

    public void FixedTick(NewMovement player)
    {
        if (!PlaybackActive || suspended || route == null) return;
        Vector3 up = player.transform.up;
        while (pointIndex < route.points.Count
               && Vector3.ProjectOnPlane(route.points[pointIndex].position - player.transform.position, up).sqrMagnitude
               <= 2.25f)
        {
            RoutePoint reached = route.points[pointIndex++];
            if (reached.jump && player.standing) player.Jump();
            if (reached.fire) FireRequested = true;
        }
        if (pointIndex >= route.points.Count)
        {
            PlaybackActive = false;
            completedSceneHandle = sceneHandle;
            Status = "Recorded route complete; using live navigation";
            return;
        }

        RoutePoint point = route.points[pointIndex];
        ApplyLook(player, point);
        Vector3 direction = Vector3.ProjectOnPlane(point.position - player.transform.position, up).normalized;
        Vector3 vertical = Vector3.Project(player.rb.velocity, up);
        Vector3 horizontal = Vector3.ProjectOnPlane(player.rb.velocity, up);
        player.rb.velocity = Vector3.MoveTowards(horizontal, direction * moveSpeed(),
            500f * Time.fixedDeltaTime) + vertical;
        Status = $"Following recorded point {pointIndex + 1}/{route.points.Count}";
    }

    private void AddPoint(NewMovement player, bool jump, bool fire)
    {
        Transform look = player.cc != null ? player.cc.transform : player.transform;
        Vector3 angles = look.eulerAngles;
        route!.points.Add(new RoutePoint
        {
            position = player.transform.position,
            yaw = angles.y,
            pitch = -NormalizeAngle(angles.x),
            jump = jump,
            fire = fire
        });
        nextSample = Time.unscaledTime + 0.1f;
        Status = $"Recording point {route.points.Count}";
    }

    private int FindNearestPoint(Vector3 position, int start, int end)
    {
        int nearest = start;
        float nearestDistance = float.PositiveInfinity;
        for (int i = start; i < end; i++)
        {
            float distance = (route!.points[i].position - position).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = i;
        }
        return nearest;
    }

    private static void ApplyLook(NewMovement player, RoutePoint point)
    {
        if (player.cc == null) return;
        player.cc.ResetCamera(point.yaw, Mathf.Clamp(point.pitch, player.cc.minimumX, player.cc.maximumX));
        player.cc.ApplyRotations(false);
    }

    private static string RoutePath(string scene)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) scene = scene.Replace(invalid, '_');
        return Path.Combine(Paths.ConfigPath, "IMULTRAKILLINGIT", "routes", scene + ".json");
    }

    private static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;
}
