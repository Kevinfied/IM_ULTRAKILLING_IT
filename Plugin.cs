using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace IMULTRAKILLINGIT;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    private enum BotState { Disabled, Paused, Explore, Engage, Chase, Recover }

    internal static new ManualLogSource Logger { get; private set; } = null!;
    private ConfigEntry<KeyCode> toggleKey = null!;
    private ConfigEntry<KeyCode> pauseKey = null!;
    private ConfigEntry<KeyCode> restartKey = null!;
    private ConfigEntry<KeyCode> debugKey = null!;
    private ConfigEntry<float> moveSpeed = null!;
    private ConfigEntry<float> attackInterval = null!;
    private ConfigEntry<float> weaponRotationSeconds = null!;
    private ConfigEntry<float> survivalWeight = null!;
    private ConfigEntry<float> timeWeight = null!;
    private ConfigEntry<float> killsWeight = null!;
    private ConfigEntry<float> styleWeight = null!;
    private ConfigEntry<bool> autoRestart = null!;

    private BotState state;
    private bool enabledBot;
    private bool paused;
    private bool showDebug = true;
    private string reason = "Not enabled";
    private string action = "Idle";
    private EnemyIdentifier? target;
    private NewMovement? player;
    private GunControl? guns;
    private StatsManager? stats;
    private Vector3 destination;
    private Vector3 progressOrigin;
    private float nextScan;
    private float nextAttack;
    private float nextJump;
    private float nextWeaponRotation;
    private float nextProgressCheck;
    private float recoveryUntil;
    private int nextSlot;

    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly Dictionary<Type, MethodInfo?> shootMethods = new();

    private void Awake()
    {
        Logger = base.Logger;
        toggleKey = Config.Bind("Controls", "Toggle", KeyCode.F8, "Enable or disable the bot.");
        pauseKey = Config.Bind("Controls", "Pause", KeyCode.F9, "Pause or resume the bot.");
        restartKey = Config.Bind("Controls", "Restart", KeyCode.F10, "Restart the current mission.");
        debugKey = Config.Bind("Controls", "DebugOverlay", KeyCode.F7, "Show or hide the debug overlay.");
        moveSpeed = Config.Bind("Bot", "MoveSpeed", 18f, "Desired horizontal movement speed.");
        attackInterval = Config.Bind("Bot", "MinimumAttackInterval", 0.18f, "Minimum seconds between attack requests.");
        weaponRotationSeconds = Config.Bind("Bot", "WeaponRotationSeconds", 4f, "Seconds between weapon-slot changes.");
        survivalWeight = Config.Bind("Priorities", "Survival", 1.25f, "Preference for keeping distance from nearby threats.");
        timeWeight = Config.Bind("Priorities", "Time", 1f, "Preference for nearby targets.");
        killsWeight = Config.Bind("Priorities", "Kills", 1f, "Preference for targets that count as kills.");
        styleWeight = Config.Bind("Priorities", "Style", 0.65f, "Preference for bosses and fresh weapon use.");
        autoRestart = Config.Bind("PRank", "AutoRestart", false, "Restart when S-time is certainly missed or a restart occurred.");
        Logger.LogInfo("IM ULTRAKILLING IT loaded. F8 toggle, F9 pause, F10 restart, F7 overlay.");
        gameObject.hideFlags = HideFlags.DontSaveInEditor;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey.Value)) SetEnabled(!enabledBot);
        if (Input.GetKeyDown(pauseKey.Value) && enabledBot)
        {
            paused = !paused;
            Transition(paused ? BotState.Paused : BotState.Explore, paused ? "Paused by user" : "Resumed by user");
        }
        if (Input.GetKeyDown(debugKey.Value)) showDebug = !showDebug;
        if (Input.GetKeyDown(restartKey.Value)) RestartMission("Manual restart");
        if (!enabledBot || paused) return;

        try
        {
            RefreshGameObjects();
            if (player == null || player.dead || player.levelOver)
            {
                Transition(BotState.Recover, player == null ? "Waiting for player" : "Player unavailable");
                return;
            }
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.25f;
                target = FindTarget();
            }
            if (target != null && !target.dead)
            {
                destination = target.GetCenter().position;
                AimAt(destination);
                float distance = Vector3.Distance(player.transform.position, destination);
                Transition(distance > 18f ? BotState.Chase : BotState.Engage,
                    distance > 18f ? "Closing on target" : "Target in attack range");
                if (Time.unscaledTime >= nextAttack) Attack();
            }
            else
            {
                target = null;
                destination = player.transform.position + player.transform.forward * 20f;
                Transition(BotState.Explore, "No visible living enemy");
            }
            RotateWeapon();
            CheckProgress();
            CheckPRankFailure();
        }
        catch (Exception exception)
        {
            enabledBot = false;
            Transition(BotState.Disabled, "Disabled after error");
            Logger.LogError(exception);
        }
    }

    private void FixedUpdate()
    {
        if (!enabledBot || paused || player == null || player.dead || state == BotState.Disabled) return;
        Vector3 up = player.transform.up;
        Vector3 toward = Vector3.ProjectOnPlane(destination - player.transform.position, up).normalized;
        if (state == BotState.Recover && Time.unscaledTime < recoveryUntil)
            toward = Quaternion.AngleAxis(65f, up) * player.transform.forward;
        else if (state == BotState.Engage)
            toward = (toward + player.transform.right * Mathf.Sin(Time.time * 2.2f) * 0.75f).normalized;

        Vector3 vertical = Vector3.Project(player.rb.velocity, up);
        Vector3 horizontal = Vector3.ProjectOnPlane(player.rb.velocity, up);
        float speed = state == BotState.Engage ? moveSpeed.Value * 0.7f : moveSpeed.Value;
        player.rb.velocity = Vector3.MoveTowards(horizontal, toward * speed, 55f * Time.fixedDeltaTime) + vertical;
        bool obstacle = Physics.Raycast(player.transform.position + up, toward, 2f, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        if (Time.unscaledTime >= nextJump && (obstacle || destination.y > player.transform.position.y + 2.5f))
        {
            nextJump = Time.unscaledTime + 0.8f;
            player.Jump();
            action = obstacle ? "Jump obstacle" : "Jump toward target";
        }
    }

    private void SetEnabled(bool value)
    {
        enabledBot = value;
        paused = false;
        target = null;
        if (value)
        {
            RefreshGameObjects();
            progressOrigin = player != null ? player.transform.position : Vector3.zero;
            nextProgressCheck = Time.unscaledTime + 2f;
            Transition(BotState.Explore, "Enabled by user");
        }
        else
        {
            Transition(BotState.Disabled, "Disabled by user; player control untouched");
            action = "Idle";
        }
    }

    private void RefreshGameObjects()
    {
        player ??= MonoSingleton<NewMovement>.Instance;
        guns ??= MonoSingleton<GunControl>.Instance;
        stats ??= MonoSingleton<StatsManager>.Instance;
    }

    private EnemyIdentifier? FindTarget()
    {
        if (player == null) return null;
        Camera? camera = player.cc != null ? player.cc.cam : Camera.main;
        if (camera == null) return null;
        EnemyIdentifier? best = null;
        float bestScore = float.NegativeInfinity;
        foreach (EnemyIdentifier enemy in FindObjectsOfType<EnemyIdentifier>())
        {
            if (enemy == null || enemy.dead || !enemy.isActiveAndEnabled || enemy.ignorePlayer) continue;
            Vector3 center = enemy.GetCenter().position;
            float distance = Vector3.Distance(camera.transform.position, center);
            if (distance > 120f || !HasLineOfSight(camera.transform.position, center, enemy)) continue;
            var facts = new TargetFacts(distance, enemy.isBoss, !enemy.dontCountAsKills, enemy.blessed);
            float score = DecisionLogic.ScoreTarget(facts, survivalWeight.Value, timeWeight.Value,
                killsWeight.Value, styleWeight.Value);
            if (score <= bestScore) continue;
            best = enemy;
            bestScore = score;
        }
        return best;
    }

    private static bool HasLineOfSight(Vector3 origin, Vector3 point, EnemyIdentifier enemy)
    {
        if (!Physics.Linecast(origin, point, out RaycastHit hit, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore)) return true;
        return hit.transform.GetComponentInParent<EnemyIdentifier>() == enemy;
    }

    private void AimAt(Vector3 point)
    {
        if (player?.cc == null) return;
        Vector3 direction = point - player.cc.transform.position;
        if (direction.sqrMagnitude < 0.01f) return;
        Vector3 angles = Quaternion.LookRotation(direction.normalized, player.transform.up).eulerAngles;
        float pitch = -NormalizeAngle(angles.x);
        player.cc.ResetCamera(angles.y, Mathf.Clamp(pitch, player.cc.minimumX, player.cc.maximumX));
        player.cc.ApplyRotations(false);
    }

    private static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;

    private void Attack()
    {
        if (guns?.currentWeapon == null) return;
        GameObject weapon = guns.currentWeapon;
        float interval = attackInterval.Value;
        if (TryShoot<Revolver>(weapon, "Shoot", component => new object[] { component.altVersion ? 2 : 1 }))
            interval = Mathf.Max(interval, 0.55f);
        else if (TryShoot<Shotgun>(weapon, "Shoot")) interval = Mathf.Max(interval, 0.9f);
        else if (TryShoot<Nailgun>(weapon, "Shoot")) interval = Mathf.Max(interval, 0.08f);
        else if (TryShoot<RocketLauncher>(weapon, "Shoot")) interval = Mathf.Max(interval, 1.1f);
        else if (MonoSingleton<WeaponCharges>.Instance is { raicharge: >= 5f }
                 && TryShoot<Railcannon>(weapon, "Shoot"))
            interval = Mathf.Max(interval, 5.2f);
        else
        {
            action = "Waiting for known weapon";
            nextAttack = Time.unscaledTime + 0.25f;
            return;
        }
        action = $"Fire {weapon.name}";
        nextAttack = Time.unscaledTime + interval;
    }

    private bool TryShoot<T>(GameObject weapon, string methodName, Func<T, object[]>? arguments = null)
        where T : Component
    {
        T component = weapon.GetComponentInChildren<T>(true);
        if (component == null) return false;
        Type type = typeof(T);
        if (!shootMethods.TryGetValue(type, out MethodInfo? method))
        {
            method = type.GetMethod(methodName, PrivateInstance | BindingFlags.Public);
            shootMethods[type] = method;
        }
        if (method == null) return false;
        method.Invoke(component, arguments?.Invoke(component) ?? Array.Empty<object>());
        return true;
    }

    private void RotateWeapon()
    {
        if (guns == null || Time.unscaledTime < nextWeaponRotation || guns.slots == null || guns.slots.Count == 0)
            return;
        nextWeaponRotation = Time.unscaledTime + Mathf.Max(1f, weaponRotationSeconds.Value);
        for (int checkedSlots = 0; checkedSlots < guns.slots.Count; checkedSlots++)
        {
            nextSlot = (nextSlot + 1) % guns.slots.Count;
            if (guns.slots[nextSlot] == null || guns.slots[nextSlot].Count == 0) continue;
            guns.SwitchWeapon(nextSlot, null, true, false, false);
            action = $"Switch to slot {nextSlot + 1}";
            return;
        }
    }

    private void CheckProgress()
    {
        if (player == null || Time.unscaledTime < nextProgressCheck) return;
        float progress = Vector3.Distance(progressOrigin, player.transform.position);
        progressOrigin = player.transform.position;
        nextProgressCheck = Time.unscaledTime + 2f;
        if (progress >= 1f || state == BotState.Engage) return;
        recoveryUntil = Time.unscaledTime + 1.5f;
        nextJump = 0f;
        Transition(BotState.Recover, $"Stuck: only {progress:0.0}m progress");
    }

    private void CheckPRankFailure()
    {
        if (!autoRestart.Value || stats == null || stats.timeRanks == null || stats.timeRanks.Length == 0) return;
        float sTime = stats.timeRanks[stats.timeRanks.Length - 1];
        if (!DecisionLogic.PRrankStillPossible(stats.seconds, sTime, stats.restarts))
            RestartMission(stats.restarts > 0 ? "P-rank impossible: restart recorded" : "P-rank impossible: S-time exceeded");
    }

    private void RestartMission(string why)
    {
        Logger.LogWarning($"Restarting: {why}");
        reason = why;
        target = null;
        OptionsManager? options = OptionsManager.Instance;
        if (options != null) options.RestartMission();
        else Logger.LogWarning("Restart requested before OptionsManager was available.");
    }

    private void Transition(BotState next, string why)
    {
        if (state == next && reason == why) return;
        state = next;
        reason = why;
        if (showDebug) Logger.LogDebug($"{state}: {reason}");
    }

    private void OnGUI()
    {
        if (!showDebug) return;
        GUI.Box(new Rect(12, 12, 430, 190), "IM ULTRAKILLING IT");
        GUILayout.BeginArea(new Rect(24, 40, 405, 155));
        GUILayout.Label($"State: {state} | F8 toggle | F9 pause | F10 restart | F7 overlay");
        GUILayout.Label($"Target: {(target != null ? target.FullName : "none")}");
        GUILayout.Label($"Action: {action}");
        GUILayout.Label($"Destination: {destination.x:0.0}, {destination.y:0.0}, {destination.z:0.0}");
        if (stats != null)
            GUILayout.Label($"Time: {stats.seconds:0.0}/{Last(stats.timeRanks)}s  Kills: {stats.kills}/{Last(stats.killRanks)}  Style: {stats.stylePoints}/{Last(stats.styleRanks)}");
        GUILayout.Label($"Reason: {reason}");
        GUILayout.EndArea();
    }

    private static int Last(int[]? values) => values is { Length: > 0 } ? values[values.Length - 1] : 0;
}
