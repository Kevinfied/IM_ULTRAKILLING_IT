using UnityEngine;
using UnityEngine.AI;

namespace IMULTRAKILLINGIT.Navigation;

internal sealed class HazardDetector
{
    public bool HasSafeGroundAhead(NewMovement player, Vector3 direction, float maximumSafeDrop)
    {
        if (!player.standing || direction.sqrMagnitude < 0.01f) return true;
        Vector3 up = player.transform.up;
        Vector3 lookAhead = player.transform.position + direction.normalized * 0.9f;
        if (NavMesh.SamplePosition(lookAhead + up * 0.5f, out NavMeshHit navHit,
                maximumSafeDrop + 0.75f, NavMesh.AllAreas))
        {
            Vector3 offset = navHit.position - lookAhead;
            float sideways = Vector3.ProjectOnPlane(offset, up).magnitude;
            float drop = -Vector3.Dot(offset, up);
            if (sideways <= 1.25f && drop <= maximumSafeDrop) return true;
        }
        Vector3 origin = lookAhead + up * 0.5f;
        return Physics.Raycast(origin, -up, maximumSafeDrop + 0.5f, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
    }

}
