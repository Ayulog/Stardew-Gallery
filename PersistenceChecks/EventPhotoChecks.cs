using System.Text.Json.Nodes;
using StardewGallery;

internal static class EventPhotoChecks
{
    internal static void Run()
    {
        string root = Path.Combine(Environment.CurrentDirectory, "PersistenceChecks", "bin", "photo-checks-" + Guid.NewGuid().ToString("N"));
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
        SaveProfileKey profile = new(10, 20);
        EventIdentity identity = new("Data/Events/Town", "a/b:../event");
        EventPhotoStore NewStore() => new(root, profile, _ => { });
        EventPhotoStore store = NewStore();
        Check(store.Read(identity).Photos.Count == 0, "empty catalog");
        EventPhoto first = store.Add(identity, png, png);
        EventPhoto second = store.Add(identity, png, png);
        Check(store.Read(identity).CoverId == first.Id, "first photo sets cover; next photo preserves selection");
        Check(NewStore().Read(identity).Photos.Count == 2, "photos survive restart");
        Check(NewStore().Read(new EventIdentity("data\\events\\TOWN", identity.EventId)).CoverId == first.Id, "asset case and slashes normalize");
        Check(NewStore().Read(new EventIdentity(identity.AssetName, identity.EventId.ToUpperInvariant())).Photos.Count == 0, "event IDs remain case-sensitive");
        Check(new EventPhotoStore(root, new SaveProfileKey(11,20), _ => { }).Read(identity).Photos.Count == 0, "farm isolation");
        Check(new EventPhotoStore(root, new SaveProfileKey(10,21), _ => { }).Read(identity).Photos.Count == 0, "player isolation");
        Check(store.Read(new EventIdentity("Data/Events/Beach", identity.EventId)).Photos.Count == 0, "same ID in another asset stays separate");
        Check(Path.GetFullPath(store.ImagePath(identity, first.Id)).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar), "event IDs cannot escape root");
        Throws(() => store.ImagePath(identity,"../other"), "invalid filename rejected");
        store.SetCover(identity, second.Id);
        Check(NewStore().Read(identity).CoverId == second.Id, "replace persisted cover");
        store.SetCover(identity, null);
        Check(NewStore().Read(identity).CoverId is null && NewStore().Read(identity).Photos.Count == 2, "default preserves photos");
        EventPhoto third = store.Add(identity,png,png);
        Check(store.Read(identity).CoverId is null, "new capture respects explicit default");
        store.SetCover(identity,second.Id);
        store.Archive(identity,second.Id);
        Check(NewStore().Read(identity).Photos.Count == 2 && NewStore().Read(identity).CoverId is null, "removing cover restores default");
        Check(!File.Exists(store.ImagePath(identity,second.Id)), "removed photo moved from active directory");
        Check(Directory.EnumerateFiles(Path.Combine(store.EventDirectory(identity),"archive"),second.Id+".png",SearchOption.AllDirectories).Any(), "original photo archived");
        Throws(() => store.SetCover(identity,Guid.NewGuid().ToString("N")), "foreign photo rejected");
        File.Move(store.ImagePath(identity,third.Id),store.ImagePath(identity,third.Id)+".missing");
        Throws(() => store.SetCover(identity,third.Id), "missing photo cannot be selected");
        Check(store.Read(identity).CoverId is null, "failed selection preserves mapping");
        Throws(() => store.Add(identity,new byte[24],png), "invalid PNG header rejected");
        byte[] oversized = png.ToArray();
        oversized[16] = 127;
        Throws(() => store.Add(identity,oversized,png), "oversized PNG rejected before writing");
        string index = Path.Combine(store.EventDirectory(identity),"index.json");
        byte[] good = File.ReadAllBytes(index);
        File.WriteAllText(index,"{ broken");
        EventPhotoSet recovered = NewStore().Read(identity);
        Check(!recovered.ReadOnly && recovered.Photos.Count > 0, "corrupt index recovers archived valid index");
        File.WriteAllBytes(index,good);
        JsonNode future = JsonNode.Parse(good)!;
        future["Version"] = 999;
        File.WriteAllText(index,future.ToJsonString());
        EventPhotoStore protectedStore = NewStore();
        Check(protectedStore.Read(identity).ReadOnly, "future schema is read-only without fallback");
        string before = File.ReadAllText(index);
        Throws(() => protectedStore.Add(identity,png,png), "future schema blocks writes");
        Check(File.ReadAllText(index) == before,"future schema untouched");
        EventIdentity corruptIdentity = new("Data/Events/Forest","broken");
        Directory.CreateDirectory(store.EventDirectory(corruptIdentity));
        File.WriteAllText(Path.Combine(store.EventDirectory(corruptIdentity),"index.json"),"null");
        Check(NewStore().Read(corruptIdentity).ReadOnly,"unrecoverable catalog stays read-only");
        var crop = PhotoImageLayout.Crop(1600,1200,265,149);
        Check(crop.Width == 1600 && crop.Height < 1200 && crop.Y > 0,"portraitish source center crops without distortion");
        Check(PhotoImageLayout.Fit(3840,2160,512,288) == (512,288),"thumbnail dimensions bounded");
        Console.WriteLine("Event photo persistence checks passed (artifacts preserved).");
    }
    private static void Check(bool condition,string message) { if(!condition) throw new Exception("Photos: " + message); }
    private static void Throws(Action action,string message)
    {
        try { action(); } catch(Exception error) when(error is ArgumentException or IOException or InvalidDataException or InvalidOperationException) { return; }
        throw new Exception("Photos: expected failure: " + message);
    }
}
