using UnityEngine;

namespace IMULTRAKILLINGIT.Navigation;

internal sealed class HazardDetector
{
    public bool HasSafeGroundAhead(NewMovement player, Vector3 direction, float maximumSafeDrop)
    {
        if (!player.standing || direction.sqrMagnitude < 0.01f) return true;
        Vector3 up = player.transform.up;
        Vector3 origin = player.transform.position + direction.normalized * 1.5f + up * 0.5f;
        return Physics.Raycast(origin, -up, maximumSafeDrop + 0.5f, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
    }

    public bool TryGetObstacle(NewMovement player, Vector3 direction, out RaycastHit hit)
    {
        hit = default;
        CapsuleCollider collider = player.playerCollider;
        if (collider == null || direction.sqrMagnitude < 0.01f) return false;
        Transform transform = collider.transform;
        Vector3 up = player.transform.up;
        Vector3 center = transform.TransformPoint(collider.center);
        Vector3 scale = transform.lossyScale;
        float radius = collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * 0.85f;
        float height = Mathf.Max(collider.height * Mathf.Abs(scale.y), radius * 2f);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 top = center + up * halfSegment;
        Vector3 bottom = center - up * halfSegment;
        RaycastHit[] hits = Physics.CapsuleCastAll(top, bottom, radius, direction.normalized, 1.25f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        foreach (RaycastHit candidate in hits)
        {
            if (candidate.collider == null || candidate.collider.transform.IsChildOf(player.transform)) continue;
            if (candidate.distance >= nearest) continue;
            nearest = candidate.distance;
            hit = candidate;
        }
        return nearest < float.PositiveInfinity;
    }
}
