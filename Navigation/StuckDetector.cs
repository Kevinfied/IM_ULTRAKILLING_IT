using UnityEngine;

namespace IMULTRAKILLINGIT.Navigation;

internal sealed class StuckDetector
{
    private readonly NavigationSettings settings;
    private Vector3 samplePosition;
    private Vector3 sampleWaypoint;
    private float sampleDistance;
    private float sampleTime;

    public StuckDetector(NavigationSettings settings) => this.settings = settings;

    public float LastProgress { get; private set; }
    public float Elapsed => Time.unscaledTime - sampleTime;

    public void Reset(Vector3 position, Vector3 waypoint)
    {
        samplePosition = position;
        sampleWaypoint = waypoint;
        sampleDistance = Vector3.Distance(position, waypoint);
        sampleTime = Time.unscaledTime;
        LastProgress = 0f;
    }

    public bool Check(Vector3 position, Vector3 waypoint)
    {
        if ((waypoint - sampleWaypoint).sqrMagnitude > 9f)
        {
            Reset(position, waypoint);
            return false;
        }
        float elapsed = Time.unscaledTime - sampleTime;
        if (elapsed < settings.StuckTimeout()) return false;
        float distance = Vector3.Distance(position, waypoint);
        LastProgress = sampleDistance - distance;
        float displacement = Vector3.Distance(samplePosition, position);
        bool stuck = DecisionLogic.IsStuck(LastProgress, displacement, elapsed, settings.StuckTimeout(),
            settings.MinimumProgress());
        Reset(position, waypoint);
        return stuck;
    }
}
