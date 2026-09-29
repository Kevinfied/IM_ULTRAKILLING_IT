namespace IMULTRAKILLINGIT;

public readonly struct TargetFacts
{
    public TargetFacts(float distance, bool boss, bool countsAsKill, bool invulnerable)
    {
        Distance = distance;
        Boss = boss;
        CountsAsKill = countsAsKill;
        Invulnerable = invulnerable;
    }

    public float Distance { get; }
    public bool Boss { get; }
    public bool CountsAsKill { get; }
    public bool Invulnerable { get; }
}

public static class DecisionLogic
{
    public static float ScoreTarget(TargetFacts target, float survival, float time, float kills, float style)
    {
        if (target.Invulnerable) return float.NegativeInfinity;
        float distance = target.Distance < 0f ? 0f : target.Distance;
        float proximity = 1f / (1f + distance);
        return proximity * (time * 100f + survival * 25f)
               + (target.CountsAsKill ? kills * 10f : 0f)
               + (target.Boss ? style * 4f : 0f);
    }

    public static bool PRrankStillPossible(float elapsedSeconds, float sRankSeconds, int restarts) =>
        restarts == 0 && (sRankSeconds <= 0f || elapsedSeconds <= sRankSeconds);

    public static float ScoreNavigationGoal(float pathLength, int priority, bool available, bool completed,
        bool visited, bool reachable, int failures) =>
        !available || completed || visited || !reachable
            ? float.NegativeInfinity
            : priority * 100f - pathLength - failures * 250f;

    public static bool HasCrossedDoor(float signedDistance) => signedDistance > 0.75f;

    public static bool IsStuck(float routeProgress, float displacement, float elapsed, float timeout,
        float minimumProgress) =>
        elapsed >= timeout && routeProgress < minimumProgress && displacement < minimumProgress;
}
