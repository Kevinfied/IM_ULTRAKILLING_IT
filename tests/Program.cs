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
Console.WriteLine("Decision logic checks passed.");
