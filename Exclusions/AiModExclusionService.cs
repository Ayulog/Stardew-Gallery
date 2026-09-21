using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StardewGallery;

/// <summary>
/// Session-scoped exclusion rules. Public methods run on the game thread; background work only
/// downloads and parses immutable data, and never invokes the log callbacks or game/SMAPI APIs.
/// </summary>
internal sealed class AiModExclusionService : IDisposable
{
    internal const string SettingsFileName = "ai-mod-exclusion.json";
    internal const string SeedRelativePath = "assets/ai-mod-exclusion.seed.json";
    internal const string DownloadUrl = "https://stardewmodding.wiki.gg/wiki/AI_Mod_Exclusion.json?action=raw";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly HttpClient Client = CreateClient();
    private readonly string modDirectory;
    private readonly Action<string> info;
    private readonly Action<string> warn;
    private readonly Func<CancellationToken, Task<string>> download;
    private readonly TimeSpan downloadTimeout;
    private PendingDownload? pending;
    private long session;
    private bool disposed;

    internal bool Enabled { get; private set; } = true;
    internal AiModExclusionRules Rules { get; private set; } = AiModExclusionRules.Empty;
    internal long Revision { get; private set; }

    internal AiModExclusionService(string modDirectory, Action<string> info, Action<string> warn,
        Func<CancellationToken, Task<string>>? download = null, TimeSpan? timeout = null)
    {
        this.modDirectory = modDirectory;
        this.info = info;
        this.warn = warn;
        this.download = download ?? DownloadAsync;
        downloadTimeout = timeout ?? TimeSpan.FromSeconds(12);
        if (downloadTimeout <= TimeSpan.Zero || downloadTimeout > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    internal bool ShouldExclude(EventOriginMatch? origin) => Enabled && Rules.ShouldExclude(origin);

    /// <summary>Start from the bundled list every time; no previous download is used as a fallback.</summary>
    internal void StartSession()
    {
        if (disposed) return;
        EndSession();
        Enabled = ReadEnabled();
        Rules = LoadSeed();
        Revision++;
        if (!Enabled)
        {
            info("AI mod exclusion is disabled by ai-mod-exclusion.json.");
            return;
        }
        info($"AI mod exclusion: using {Rules.Count} bundled ModId rules while checking the current list.");
        var cancellation = new CancellationTokenSource();
        CancellationToken token = cancellation.Token;
        long generation = session;
        pending = new(generation, cancellation, Task.Run(() => FetchAsync(token)));
    }

    /// <summary>Apply a completed current-session result on the calling game thread.</summary>
    internal bool TryApplyCompleted()
    {
        PendingDownload? request = pending;
        if (request is null || !request.Task.IsCompleted) return false;
        pending = null;
        request.Cancellation.Dispose();
        if (disposed || request.Session != session) return false;
        DownloadResult result = request.Task.GetAwaiter().GetResult();
        if (result.Rules is null)
        {
            warn($"AI mod exclusion: latest list unavailable; keeping the bundled list. {result.Error}");
            return false;
        }
        info($"AI mod exclusion: downloaded {result.Rules.Count} current ModId rules.");
        if (Rules.HasSameRules(result.Rules)) return false;
        Rules = result.Rules;
        Revision++;
        return true;
    }

    /// <summary>Invalidate pending work so a late response cannot affect a different save/session.</summary>
    internal void EndSession()
    {
        session++;
        if (pending is not { } request) return;
        pending = null;
        request.Cancellation.Cancel();
        request.Cancellation.Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;
        EndSession();
        disposed = true;
    }

    private bool ReadEnabled()
    {
        string path = Path.Combine(modDirectory, SettingsFileName);
        try
        {
            if (!File.Exists(path))
            {
                try
                {
                    // Never replace an existing player file, including an invalid or future-schema one.
                    using FileStream created = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    using StreamWriter writer = new(created, new UTF8Encoding(false));
                    writer.Write("{\n  \"Enabled\": true\n}\n");
                    return true;
                }
                catch (IOException) when (File.Exists(path)) { }
            }
            using JsonDocument settings = JsonDocument.Parse(ReadBoundedFile(path, 4096), new JsonDocumentOptions { MaxDepth = 8 });
            if (settings.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException("Settings must be an object containing Enabled.");
            JsonElement enabled = default;
            int fields = 0;
            foreach (JsonProperty property in settings.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("Enabled", StringComparison.Ordinal)) continue;
                enabled = property.Value;
                fields++;
            }
            if (fields != 1 || enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new FormatException("Settings must contain exactly one boolean Enabled value.");
            return enabled.GetBoolean();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or DecoderFallbackException)
        {
            warn($"AI mod exclusion: could not read ai-mod-exclusion.json; using Enabled=true and preserving the file. {Reason(ex)}");
            return true;
        }
    }

    private AiModExclusionRules LoadSeed()
    {
        try
        {
            string path = Path.Combine(modDirectory, SeedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            return AiModExclusionRules.Parse(ReadBoundedFile(path, AiModExclusionRules.MaximumBytes));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or DecoderFallbackException)
        {
            warn($"AI mod exclusion: bundled list is unavailable or invalid; no events are excluded without valid rules. {Reason(ex)}");
            return AiModExclusionRules.Empty;
        }
    }

    private async Task<DownloadResult> FetchAsync(CancellationToken sessionToken)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
        timeoutCancellation.CancelAfter(downloadTimeout);
        Task<string>? transfer = null;
        try
        {
            transfer = download(timeoutCancellation.Token);
            // Also bound a downloader which does not honor cancellation; only this result may be applied.
            string json = await transfer.WaitAsync(downloadTimeout, sessionToken).ConfigureAwait(false);
            sessionToken.ThrowIfCancellationRequested();
            return new(AiModExclusionRules.Parse(json), null);
        }
        catch (OperationCanceledException)
        {
            return new(null, sessionToken.IsCancellationRequested ? "Request cancelled." : "Request timed out.");
        }
        catch (TimeoutException)
        {
            return new(null, "Request timed out.");
        }
        catch (Exception ex)
        {
            return new(null, Reason(ex));
        }
        finally
        {
            timeoutCancellation.Cancel();
            // Observe a possible late failure from a cancellation-insensitive injected/downstream request.
            if (transfer is not null)
                _ = transfer.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            MaxAutomaticRedirections = 3
        };
        return new(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private static async Task<string> DownloadAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, DownloadUrl);
        request.Headers.UserAgent.ParseAdd("StardewGallery");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        using HttpResponseMessage response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > AiModExclusionRules.MaximumBytes)
            throw new InvalidDataException("The downloaded exclusion list exceeds its size limit.");
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        while (true)
        {
            int count = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (buffer.Length + count > AiModExclusionRules.MaximumBytes)
                throw new InvalidDataException("The downloaded exclusion list exceeds its size limit.");
            buffer.Write(chunk, 0, count);
        }
        return StrictUtf8.GetString(buffer.ToArray()).TrimStart('\uFEFF');
    }

    private static string ReadBoundedFile(string path, int maxBytes)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maxBytes) throw new InvalidDataException("The JSON file exceeds its size limit.");
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[4096];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > maxBytes) throw new InvalidDataException("The JSON file exceeds its size limit.");
            buffer.Write(chunk, 0, read);
        }
        return StrictUtf8.GetString(buffer.ToArray()).TrimStart('\uFEFF');
    }

    private static string Reason(Exception exception)
    {
        string message = exception.Message.Replace('\r', ' ').Replace('\n', ' ');
        return message.Length <= 240 ? message : message[..240];
    }

    private sealed record PendingDownload(long Session, CancellationTokenSource Cancellation, Task<DownloadResult> Task);
    private sealed record DownloadResult(AiModExclusionRules? Rules, string? Error);
}
