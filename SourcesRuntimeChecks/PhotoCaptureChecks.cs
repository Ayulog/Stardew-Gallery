using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

/// <summary>Exercises the built photo capture request lifecycle without graphics, a save or Mod.Entry.</summary>
internal static class PhotoCaptureChecks
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal static void Run(Assembly mod)
    {
        Type photosType = mod.GetType("StardewGallery.GalleryPhotos", true)!;
        Type patchType = mod.GetType("StardewGallery.PhotoCapturePatches", true)!;
        Type requestType = photosType.GetNestedType("CaptureRequest", BindingFlags.NonPublic)!;
        Type identityType = mod.GetType("StardewGallery.EventIdentity", true)!;
        int assertions = 0;
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        IModHelper helper = DispatchProxy.Create<IModHelper, FakeProxy>();
        IMonitor monitor = DispatchProxy.Create<IMonitor, FakeProxy>();
        var notices = new List<string>();
        object photos = Activator.CreateInstance(photosType, Members, null,
            [helper, monitor, (Action<string, bool>)((key, _) => notices.Add(key))], null)!;
        typeof(Game1).GetField("screenFade", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, new StardewValley.BellsAndWhistles.ScreenFade(() => true, () => { }));
        var playing = (Event)FormatterServices.GetUninitializedObject(typeof(Event));
        playing.skippable = true;
        FieldInfo request = photosType.GetField("request", Members)!;
        MethodInfo prefer = patchType.GetMethod("AfterShouldDrawOnBuffer", Members)!;
        // Production passes the drawn screen's GalleryPhotos.Pending (split-screen keeps one GalleryPhotos per screen).
        PropertyInfo pending = photosType.GetProperty("Pending", Members)!;
        patchType.GetField("pending", Members)!.SetValue(null, (Func<bool>)(() => (bool)pending.GetValue(photos)!));
        bool Buffer(bool original) { object?[] args = [original]; prefer.Invoke(null, args); return (bool)args[0]!; }

        Check(!Buffer(false) && Buffer(true), "Without a pending photo the game's own world-buffer choice is kept");
        object identity = Activator.CreateInstance(identityType, Members, null, ["Data/Events/Town", "1"], null)!;
        request.SetValue(photos, Activator.CreateInstance(requestType, Members, null, [null, identity, playing], null));
        Check(Buffer(false), "A pending photo routes the 100% zoom world through Game1.screen for that frame");
        photosType.GetMethod("PrepareCapture", Members)!.Invoke(photos, [playing]);
        Check(!playing.skippable, "The skip button is hidden while the photographed frame is drawn");
        // No graphics device exists here, so the capture itself fails; the skip button must still come back.
        photosType.GetMethod("Capture", Members)!.Invoke(photos, [null, playing]);
        Check(playing.skippable && request.GetValue(photos) is null && !Buffer(false),
            "After capture the skip button returns and normal back-buffer drawing resumes");
        Check(notices.Contains("photo.failed"), "A failed capture is reported instead of saving an image");
        Console.WriteLine($"Photo capture checks passed ({assertions} assertions).");
    }
}
