using System.Collections.Generic;
using UnityEngine;

namespace IMULTRAKILLINGIT.Navigation;

internal sealed class NavigationRecoveryController
{
    private readonly Dictionary<int, int> failures = new();
    private Vector3 direction;
    private float until;

    public IReadOnlyDictionary<int, int> Failures => failures;
    public int Level { get; private set; }
    public bool Active => Time.unscaledTime < until;

    public void Reset()
    {
        failures.Clear();
        Level = 0;
        until = 0f;
    }

    public void Begin(NewMovement player, int objectiveId)
    {
        failures.TryGetValue(objectiveId, out int count);
        count++;
        failures[objectiveId] = count;
        Level = Mathf.Clamp(count, 1, 3);
        float angle = Level switch { 1 => 65f, 2 => -80f, _ => 140f };
        direction = Quaternion.AngleAxis(angle, player.transform.up) * -player.transform.forward;
        until = Time.unscaledTime + 0.8f + Level * 0.2f;
    }

    public bool ShouldAbandon(int objectiveId) => failures.TryGetValue(objectiveId, out int count) && count >= 3;

    public void FixedTick(NewMovement player, float speed)
    {
        Vector3 up = player.transform.up;
        Vector3 vertical = Vector3.Project(player.rb.velocity, up);
        Vector3 horizontal = Vector3.ProjectOnPlane(player.rb.velocity, up);
        player.rb.velocity = Vector3.MoveTowards(horizontal, direction * speed * 0.55f,
            80f * Time.fixedDeltaTime) + vertical;
    }
}
