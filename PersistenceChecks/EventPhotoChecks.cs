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
        CheckAdoption(root, png);
        CheckBackupCap(root, png);
        var crop = PhotoImageLayout.Crop(1600,1200,265,149);
        Check(crop.Width == 1600 && crop.Height < 1200 && crop.Y > 0,"portraitish source center crops without distortion");
        Check(PhotoImageLayout.Fit(3840,2160,512,288) == (512,288),"thumbnail dimensions bounded");
        Console.WriteLine("Event photo persistence checks passed (artifacts preserved).");
    }
    /// <summary>2.10.0 merges each Trailer_Big copy of a trailer event into the Trailer entry.</summary>
    private static void CheckAdoption(string root, byte[] png)
    {
        SaveProfileKey profile = new(30, 40);
        EventPhotoStore NewStore() => new(root, profile, _ => { });
        EventIdentity current = new("Data/Events/Trailer", "35"), former = new("Data/Events/Trailer_Big", "35");
        EventPhotoStore store = NewStore();
        EventPhoto kept = store.Add(current, png, png);
        EventPhoto moved = store.Add(former, png, png), other = store.Add(former, png, png);
        store.SetCover(current, null);
        store.SetCover(former, other.Id);
        Check(NewStore().Adopt(current, former), "former photos adopted");
        EventPhotoStore reloaded = NewStore();
        EventPhotoSet merged = reloaded.Read(current);
        Check(merged.Photos.Select(photo => photo.Id).SequenceEqual([kept.Id, moved.Id, other.Id]) && merged.CoverId == other.Id,
            "adopted photos follow the current ones and bring the former cover when none is chosen");
        Check(merged.Photos.All(photo => File.Exists(reloaded.ImagePath(current, photo.Id)) && File.Exists(reloaded.ImagePath(current, photo.Id, true))),
            "adopted images are in the current folder");
        Check(File.Exists(reloaded.ImagePath(former, moved.Id)) && reloaded.Read(former).Photos.Count == 0
            && Directory.EnumerateFiles(Path.Combine(reloaded.EventDirectory(former), "archive"), "index-adopted-*.json").Count() == 1,
            "former images stay on disk and its catalog is retired");
        reloaded.Archive(current, moved.Id);
        Check(!NewStore().Adopt(current, former) && NewStore().Read(current).Photos.All(photo => photo.Id != moved.Id),
            "a photo archived after adoption does not return");

        // An adoption interrupted after its commit resumes without duplicates.
        EventIdentity resumedCurrent = new("Data/Events/Trailer", "36"), resumedFormer = new("Data/Events/Trailer_Big", "36");
        EventPhoto shared = store.Add(resumedFormer, png, png);
        Check(store.Adopt(resumedCurrent, resumedFormer), "second event adopted");
        string retired = Directory.EnumerateFiles(Path.Combine(store.EventDirectory(resumedFormer), "archive"), "index-adopted-*.json").Single();
        File.Copy(retired, Path.Combine(store.EventDirectory(resumedFormer), "index.json"));
        Check(NewStore().Adopt(resumedCurrent, resumedFormer) && NewStore().Read(resumedCurrent).Photos.Count(photo => photo.Id == shared.Id) == 1,
            "a repeated adoption keeps one copy of each photo");

        // The game ID keeps surrounding spaces; saved photos stay keyed by the trimmed ID.
        EventIdentity spaced = new("Data/Events/Town", "123 ");
        EventPhoto spacedPhoto = store.Add(new EventIdentity("Data/Events/Town", "123"), png, png);
        Check(NewStore().Read(spaced).Photos.Single().Id == spacedPhoto.Id && NewStore().EventDirectory(spaced) == store.EventDirectory(new("Data/Events/Town", "123")),
            "a trailing-space game ID reads photos saved under the trimmed ID");
    }

    private static void CheckBackupCap(string root, byte[] png)
    {
        EventPhotoStore store = new(root, new SaveProfileKey(50, 60), _ => { });
        EventIdentity identity = new("Data/Events/Town", "backup-cap");
        EventPhoto photo = store.Add(identity, png, png);
        string archive = Path.Combine(store.EventDirectory(identity), "archive");
        Directory.CreateDirectory(archive);
        string adopted = Path.Combine(archive, "index-adopted-20260101000000000000-fixture.json");
        File.WriteAllText(adopted, "{}");
        for (int i = 0; i < EventPhotoStore.MaxIndexBackups + 5; i++) store.SetCover(identity, i % 2 == 0 ? null : photo.Id);
        Check(Directory.EnumerateFiles(archive, "index-*.json").Count(path => path != adopted) == EventPhotoStore.MaxIndexBackups && File.Exists(adopted),
            "catalog backups are capped while a retired adopted catalog is kept");
        store.Archive(identity, photo.Id);
        Check(Directory.EnumerateFiles(archive, photo.Id + ".png", SearchOption.AllDirectories).Any(), "removed photos are never trimmed");
    }

    private static void Check(bool condition,string message) { if(!condition) throw new Exception("Photos: " + message); }
    private static void Throws(Action action,string message)
    {
        try { action(); } catch(Exception error) when(error is ArgumentException or IOException or InvalidDataException or InvalidOperationException) { return; }
        throw new Exception("Photos: expected failure: " + message);
    }
}
