using System.Text.Json;
using StardewGallery;

internal static class ModDirectoryDiscoveryChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
        string temporary = Path.Combine(Path.GetTempPath(), "StardewGallery-DirectoryChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string game = Path.Combine(temporary, "game");
            string root = Path.Combine(game, "Mods");
            string own = Path.Combine(root, "Grouping", "Gallery");
            Directory.CreateDirectory(own);
            string custom = Path.Combine(temporary, "custom", "Installed mods");
            string customOwn = Path.Combine(custom, "Grouping", "Gallery");
            Check(ModDirectoryDiscovery.ResolveRoot(game, own, new[] { "smapi" }, null) == root, "Default directory supports nested gallery installations");
            Check(ModDirectoryDiscovery.ResolveRoot(game, customOwn, new[] { "smapi", "--mods-path", custom }, root) == custom, "Command line overrides environment and accepts absolute custom directory");
            Check(ModDirectoryDiscovery.ResolveRoot(game, customOwn, new[] { "smapi", "--mods-path", root, "--mods-path", custom }, null) == custom, "Last explicit mods-path wins as in SMAPI");
            Check(ModDirectoryDiscovery.ResolveRoot(game, customOwn, new[] { "smapi" }, custom) == custom, "Custom environment root is honored");
            Check(ModDirectoryDiscovery.ResolveRoot(game, customOwn, new[] { "smapi", "--mods-path", " " }, custom) == custom, "Empty command argument falls back to environment as in SMAPI");
            string relative = Path.GetRelativePath(game, custom);
            Check(ModDirectoryDiscovery.ResolveRoot(game, customOwn, new[] { "smapi", "--mods-path", relative }, null) == custom, "Relative custom directory is based on game directory");
            Check(ModDirectoryDiscovery.ResolveRoot(game, customOwn, new[] { "smapi" }, null) is null, "No ancestor guessing when expected root does not contain gallery");
            Check(ModDirectoryDiscovery.ResolveRoot(game, root + "Elsewhere/Gallery", new[] { "smapi" }, null) is null, "Containment checks directory boundaries");
            Check(ModDirectoryDiscovery.ResolveRoot(game, own, new[] { "smapi", "--mods-path" }, custom) is null, "Missing argument fails closed");
            Check(ModDirectoryDiscovery.ResolveRoot(game, own, new[] { "smapi", "--mods-path", Path.GetPathRoot(root)! }, null) is null, "Never scan a whole volume");
            Check(ModDirectoryDiscovery.ResolveRoot("relative", own, new[] { "smapi" }, null) is null, "Current working directory is never used to guess game path");
            Check(ModDirectoryDiscovery.ResolveRoot(game, root, new[] { "smapi" }, null) is null, "Mods root itself cannot be mistaken for gallery installation");

            var loaded = new[]
            {
                new ModDirectoryIdentity("Fixture.CP", "1.0.0", "Pathoschild.ContentPatcher", null),
                new ModDirectoryIdentity("Fixture.Code", "2.3.0", null, "Fixture.dll"),
                new ModDirectoryIdentity("Fixture.Disabled", "1.0.0", "Pathoschild.ContentPatcher", null),
                new ModDirectoryIdentity("Fixture.Duplicate", "1.0.0", "Pathoschild.ContentPatcher", null),
                new ModDirectoryIdentity("Fixture.HiddenNested", "1.0.0", "Pathoschild.ContentPatcher", null),
                new ModDirectoryIdentity("Fixture.WrongKind", "1.0.0", "Pathoschild.ContentPatcher", null),
                new ModDirectoryIdentity("Fixture.WrongVersion", "1.0.0", "Pathoschild.ContentPatcher", null),
                new ModDirectoryIdentity("Fixture.Normalized", "1.0.0", "Pathoschild.ContentPatcher", null),
            };
            string Manifest(string folder, string id, string version = "1.0.0", string? packFor = "Pathoschild.ContentPatcher", string? entryDll = null)
            {
                string directory = Path.Combine(root, folder);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new
                { UniqueID = id, Version = version, ContentPackFor = packFor is null ? null : new { UniqueID = packFor }, EntryDll = entryDll }));
                return Path.GetFullPath(directory);
            }
            string cp = Manifest("Deep/Group/CP", "Fixture.CP", "1.0");
            string code = Manifest("Deep/Group/Code", "Fixture.Code", "2.3", null, "Fixture.dll");
            Manifest("NotLoaded", "Fixture.NotLoaded");
            Manifest(".Disabled/Pack", "Fixture.Disabled");
            Manifest("Deep/Group/CP/user-data/Backup", "Fixture.HiddenNested");
            Manifest("DuplicateA", "Fixture.Duplicate");
            Manifest("DuplicateB", "Fixture.Duplicate", "2.0.0");
            Manifest("WrongKind", "Fixture.WrongKind", packFor: "Other.Framework");
            Manifest("WrongVersion", "Fixture.WrongVersion", "3.0.0");
            string invalid = Path.Combine(root, "Broken");
            Directory.CreateDirectory(invalid);
            File.WriteAllText(Path.Combine(invalid, "manifest.json"), "{invalid");
            Manifest("Broken/Child", "Fixture.HiddenNested");
            string normalized = Manifest("Normalized", "Fixture.Normalized");
            File.WriteAllText(Path.Combine(normalized, "manifest.json"), "{normalized-json-fixture}");
            ModDirectoryDiscoveryResult result = ModDirectoryDiscovery.Discover(root, loaded, json => json == "{normalized-json-fixture}"
                ? "{\"UniqueID\":\"Fixture.Normalized\",\"Version\":\"1.0\",\"ContentPackFor\":{\"UniqueID\":\"Pathoschild.ContentPatcher\"}}" : json);
            Check(result.Directories.Count == 3, "Only unambiguous loaded manifests with matching identities are returned");
            Check(result.Directories["fixture.cp"] == cp && result.Directories["Fixture.Code"] == code, "Loaded IDs are case insensitive and nested mod directories are found");
            Check(!result.Directories.ContainsKey("Fixture.NotLoaded"), "An installed but unloaded mod cannot supply provenance");
            Check(!result.Directories.ContainsKey("Fixture.Disabled"), "Dot folders are not scanned");
            Check(!result.Directories.ContainsKey("Fixture.HiddenNested"), "Descent ends at valid or invalid manifests and cannot enter personal data");
            Check(!result.Directories.ContainsKey("Fixture.Duplicate"), "A duplicate is never guessed even if only one version matches loaded metadata");
            Check(!result.Directories.ContainsKey("Fixture.WrongKind") && !result.Directories.ContainsKey("Fixture.WrongVersion"), "Loaded framework and version identity must agree with disk");
            Check(result.Directories["Fixture.Normalized"] == normalized, "Injected permissive JSON normalizer is applied to manifests");
            Check(result.Issues.Any(issue => issue.Reason.Contains("Multiple installed")) && result.Issues.Any(issue => issue.Reason.Contains("could not be read")), "Duplicate and malformed manifest diagnostics are retained");
            Check(ModDirectoryDiscovery.Discover(Path.GetPathRoot(root)!, loaded).Directories.Count == 0, "Direct discovery also rejects volume roots");
            Check(ModDirectoryDiscovery.Discover("relative", loaded).Directories.Count == 0, "Direct discovery rejects relative roots");
            Check(ModDirectoryDiscovery.Discover(Path.Combine(temporary, "absent"), loaded).Issues.Count == 1, "Missing configured folder gives one diagnostic");
            string outside = Path.Combine(temporary, "outside");
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "manifest.json"), "{\"UniqueID\":\"Fixture.Disabled\",\"Version\":\"1.0.0\",\"ContentPackFor\":{\"UniqueID\":\"Pathoschild.ContentPatcher\"}}");
            string link = Path.Combine(root, "LinkedOutside");
            try
            {
                Directory.CreateSymbolicLink(link, outside);
                try
                {
                    result = ModDirectoryDiscovery.Discover(root, loaded);
                    Check(!result.Directories.ContainsKey("Fixture.Disabled") && result.Issues.Any(issue => issue.Reason.Contains("junction")), "Linked directories never escape the configured root");
                    Check(ModDirectoryDiscovery.Discover(link, loaded).Directories.Count == 0, "A linked root is rejected as well");
                }
                finally { Directory.Delete(link); }
            }
            catch (Exception error) when (error is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
            { Console.WriteLine("Directory symbolic-link check unavailable: " + error.GetType().Name); }
        }
        finally { Directory.Delete(temporary, recursive: true); }
        Console.WriteLine($"Mod directory discovery checks passed: {checks}");
    }
}