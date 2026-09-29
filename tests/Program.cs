using IMULTRAKILLINGIT;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var near = new TargetFacts(5f, false, true, false);
var far = new TargetFacts(40f, false, true, false);
Check(DecisionLogic.ScoreTarget(near, 1f, 1f, 1f, 1f) > DecisionLogic.ScoreTarget(far, 1f, 1f, 1f, 1f),
    "A nearby equivalent target should win.");
Check(float.IsNegativeInfinity(DecisionLogic.ScoreTarget(new TargetFacts(1f, true, true, true), 1f, 1f, 1f, 1f)),
    "Invulnerable targets should be ignored.");
Check(DecisionLogic.PRrankStillPossible(59f, 60f, 0), "A clean run below S-time should remain viable.");
Check(!DecisionLogic.PRrankStillPossible(61f, 60f, 0), "A run beyond S-time should fail.");
Check(!DecisionLogic.PRrankStillPossible(10f, 60f, 1), "A restarted run cannot P-rank.");
Check(DecisionLogic.ScoreNavigationGoal(10f, 10, true, false, false, true, 0) >
      DecisionLogic.ScoreNavigationGoal(30f, 10, true, false, false, true, 0),
    "The shortest equivalent objective route should win.");
Check(DecisionLogic.ScoreNavigationGoal(30f, 20, true, false, false, true, 0) >
      DecisionLogic.ScoreNavigationGoal(10f, 10, true, false, false, true, 0),
    "Progression priority should beat a small distance advantage.");
Check(float.IsNegativeInfinity(DecisionLogic.ScoreNavigationGoal(1f, 10, false, false, false, true, 0)),
    "Unavailable objectives should not be selected.");
Check(float.IsNegativeInfinity(DecisionLogic.ScoreNavigationGoal(1f, 10, true, false, false, false, 0)),
    "Objectives without a complete path should not be selected.");
Check(!float.IsNegativeInfinity(DecisionLogic.ScoreNavigationGoal(10f, 10, true, false, false, true, 3)),
    "Repeated local failures should penalize an objective without permanently deleting it.");
Check(!DecisionLogic.HasCrossedDoor(0.5f), "Approaching a door should not count as crossing it.");
Check(DecisionLogic.HasCrossedDoor(1f), "Moving beyond a door should count as crossing it.");
Check(!DecisionLogic.IsStuck(0.1f, 0.1f, 1f, 2f, 0.5f), "A brief pause should not count as stuck.");
Check(DecisionLogic.IsStuck(0.1f, 0.1f, 2.5f, 2f, 0.5f), "No progress over the timeout should count as stuck.");
Console.WriteLine("Decision logic checks passed.");
