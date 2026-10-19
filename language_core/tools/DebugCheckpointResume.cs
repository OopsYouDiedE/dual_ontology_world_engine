using DuelView.Compiled;

// Tool-only loader. Value semantics match DvSession.Restore/ReadValue, including
// exact $dv_rational atoms. It performs no initialization or state edits.
internal static class DebugCheckpointResume
{
    public static int Main(string[] args)
    {
        R.Arguments = args;
        try
        {
            if (args.Length <= 10 || string.IsNullOrWhiteSpace(args[10]))
                throw new InvalidOperationException("A validated binary checkpoint Save is required");
            _ = DVProgram.ResumeDebugCheckpoint(R.ReadEvidenceFile(args[10]));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return 1;
        }
    }

}
public static partial class DVProgram
{
    public static object ResumeDebugCheckpoint(object checkpoint) => F_dvg_debug_resume(checkpoint);
}
