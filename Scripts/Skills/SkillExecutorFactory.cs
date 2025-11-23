using System.Collections.Generic;
using UnityEngine;

public static class SkillExecutorFactory
{
    private static Dictionary<string, ISkillExecutor> executors = new Dictionary<string, ISkillExecutor>();


    static SkillExecutorFactory()
    {
        RegisterExecutor(new CleaveStrikeExecutor());
        RegisterExecutor(new PiercingThrustExecutor());
        RegisterExecutor(new GuardedSlamExecutor());
        RegisterExecutor(new BattleRoarExecutor());
        RegisterExecutor(new SeismicRuptureExecutor());
        RegisterExecutor(new BlindingShotExecutor());
        RegisterExecutor(new RainOfArrowsExecutor());
        RegisterExecutor(new PiercingArrowExecutor());
        RegisterExecutor(new EvasiveRollExecutor());
        RegisterExecutor(new ColossusStanceExecutor());
}
    
    public static void RegisterExecutor(ISkillExecutor executor)
    {
        executors[executor.SkillId] = executor;
    }
    
    public static ISkillExecutor GetExecutor(string skillId)
    {
        bool found = executors.TryGetValue(skillId, out var executor);
        return executor;
    }
}