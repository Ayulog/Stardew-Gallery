using System.Runtime.CompilerServices;
using HarmonyLib;

/// <summary>Isolated demonstration; never patches any SMAPI generic method.</summary>
internal static class GenericSharingProbe
{
    private static int calls;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Type ReadType<T>() => typeof(T);
    private static void Observe() => calls++;

    internal static void Run()
    {
        Type beforeObject = ReadType<object>();
        Type beforeDictionary = ReadType<Dictionary<string,string>>();
        Type beforeString = ReadType<string>();
        var harmony = new Harmony("StardewGallery.SourceChecks.GenericDemonstration");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(GenericSharingProbe), nameof(ReadType)).MakeGenericMethod(typeof(object)),
                prefix: new HarmonyMethod(typeof(GenericSharingProbe), nameof(Observe)));
            Type afterObject = ReadType<object>();
            Type afterDictionary = ReadType<Dictionary<string,string>>();
            Type afterString = ReadType<string>();
            Console.WriteLine($"Harmony {typeof(Harmony).Assembly.GetName().Version}; CLR {Environment.Version}");
            Console.WriteLine($"object: {beforeObject} => {afterObject}");
            Console.WriteLine($"dictionary: {beforeDictionary} => {afterDictionary}");
            Console.WriteLine($"string: {beforeString} => {afterString}");
            Console.WriteLine($"Observed calls: {calls}");
            Console.WriteLine(afterDictionary != beforeDictionary || afterString != beforeString
                ? "Generic method type context changed. Closed-reference generic hooks are unsafe on this runtime."
                : "This probe did not reproduce the context change; it does not establish generic SMAPI hook safety.");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }
}
