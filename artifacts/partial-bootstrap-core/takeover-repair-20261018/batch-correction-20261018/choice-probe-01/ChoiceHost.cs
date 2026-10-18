using System.Reflection;
using DuelView.Compiled;
public static class ChoiceHost
{
    public static void Main()
    {
        var method = typeof(DVProgram).GetMethod("F_neutral_choice_probe", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new Exception("Generated neutral mechanism probe missing");
        R.json_print(method.Invoke(null, null)!);
    }
}
