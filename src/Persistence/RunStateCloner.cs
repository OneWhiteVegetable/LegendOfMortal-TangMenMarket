using XingShangMod.Data;

namespace XingShangMod.Persistence
{
    public static class RunStateCloner
    {
        public static RunState Clone(RunState source)
        {
            if (source == null)
                return null;
            return JsonCodec.DeepClone<RunState>(source);
        }
    }
}
