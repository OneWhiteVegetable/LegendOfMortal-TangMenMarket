using XingShangMod.Domain;
using XingShangMod.Market;
using static AuditAssert;

internal sealed class ElasticityTests
{
    public void Register(TestRunner runner)
    {
        runner.Run("elasticity-thresholds", TestElasticity);
        runner.Run("elasticity-weeks-three-through-nine", TestWeeksThreeThroughNine);
        runner.Run("elasticity-inclusive-boundaries-and-reset", TestBoundaries);
    }

    private static void TestElasticity()
    {
        ElasticityState state = new ElasticityState();
        ElasticityStateMachine machine = new ElasticityStateMachine();
        machine.Apply(state, 0.01);
        machine.Apply(state, 0.01);
        ElasticityResult entered = machine.Apply(state, 0.01);
        True(entered.Entered && state.IsActive, "elasticity did not enter");
        Near(0.02, machine.Apply(state, 0.01).Return, "week 4 multiplier");
        Near(2.5, state.Multiplier, "week 5 prepared multiplier");
        Near(0.025, machine.Apply(state, 0.01).Return, "week 5 multiplier");
        Near(3.0, state.Multiplier, "week 6 prepared multiplier");
        Near(0.03, machine.Apply(state, 0.01).Return, "week 6 multiplier");
        machine.Apply(state, 0.01);
        Near(4.0, state.Multiplier, "week 8 prepared multiplier");
        ElasticityResult exited = machine.Apply(state, 0.02);
        True(exited.Exited && !state.IsActive, "elasticity did not exit above five percent");
        Near(0.08, exited.Return, "exit week return");
    }

    private static void TestWeeksThreeThroughNine()
    {
        double[] expectedReturns = { 0.01, 0.01, 0.01, 0.02, 0.025, 0.03, 0.035, 0.04, 0.045 };
        double[] preparedMultipliers = { 1.0, 1.0, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0 };
        ElasticityState state = new ElasticityState();
        ElasticityStateMachine machine = new ElasticityStateMachine();
        for (int i = 0; i < expectedReturns.Length; i++)
        {
            ElasticityResult result = machine.Apply(state, 0.01);
            Near(expectedReturns[i], result.Return, "elasticity return week " + (i + 1));
            Near(preparedMultipliers[i], state.Multiplier, "prepared multiplier week " + (i + 1));
            Equal(i >= 2, state.IsActive, "active flag week " + (i + 1));
            True(!result.Exited, "unexpected exit week " + (i + 1));
        }
        Equal(6, state.ActiveWeekCount, "active week count after overall week 9");
        Equal(3, state.QuietCount, "quiet count after entry");
    }

    private static void TestBoundaries()
    {
        ElasticityState state = new ElasticityState();
        ElasticityStateMachine machine = new ElasticityStateMachine();
        machine.Apply(state, 0.02);
        machine.Apply(state, -0.02);
        ElasticityResult entered = machine.Apply(state, 0.02);
        True(entered.Entered, "inclusive quiet threshold did not enter");
        Near(0.02, entered.Return, "entry week changed base return");

        ElasticityResult fourth = machine.Apply(state, ElasticityStateMachine.FourthWeekThreshold / 2.0);
        Near(ElasticityStateMachine.FourthWeekThreshold, fourth.Return, "fourth week threshold return");
        Near(2.5, state.Multiplier, "inclusive fourth week threshold");
        ElasticityResult fifth = machine.Apply(state, ElasticityStateMachine.FifthWeekThreshold / 2.5);
        Near(ElasticityStateMachine.FifthWeekThreshold, fifth.Return, "fifth week threshold return");
        Near(3.0, state.Multiplier, "inclusive fifth week threshold");

        ElasticityState exitBoundary = ActiveState();
        ElasticityResult atExit = machine.Apply(exitBoundary, ElasticityStateMachine.ExitThreshold / 2.0);
        Near(ElasticityStateMachine.ExitThreshold, atExit.Return, "exit threshold return");
        True(!atExit.Exited && exitBoundary.IsActive, "exact exit threshold exited");

        ElasticityState aboveExit = ActiveState();
        ElasticityResult exited = machine.Apply(aboveExit, 0.0251);
        True(exited.Exited && !aboveExit.IsActive, "above exit threshold stayed active");
        Equal(0, aboveExit.QuietCount, "exit quiet count");
        Equal(0, aboveExit.ActiveWeekCount, "exit active count");
        Near(1.0, aboveExit.Multiplier, "exit multiplier reset");

        ElasticityState negativeExit = ActiveState();
        True(machine.Apply(negativeExit, -0.0251).Exited, "negative exit magnitude ignored");

        ElasticityState interrupted = new ElasticityState();
        machine.Apply(interrupted, 0.02);
        machine.Apply(interrupted, 0.02);
        machine.Apply(interrupted, 0.0201);
        Equal(0, interrupted.QuietCount, "loud week did not reset quiet count");
        True(!interrupted.IsActive, "loud week entered elasticity");
    }

    private static ElasticityState ActiveState()
    {
        return new ElasticityState
        {
            IsActive = true,
            QuietCount = 3,
            Multiplier = 2.0
        };
    }
}
