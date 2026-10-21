using System.Diagnostics;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

/// <summary>
/// With debug diagnostics on, logs every five seconds how long frames take while a gallery page is open: the game
/// update (which runs the page's update) and the page's draw, plus garbage collections. Used to diagnose slow input.
/// </summary>
internal sealed class GalleryFrameProbe(IMonitor monitor, Func<bool> enabled)
{
    private static readonly double TicksPerMillisecond = Stopwatch.Frequency / 1000d;
    private long updateStart, drawStart, windowStart;
    private double updateTotal, updateMax, drawTotal, drawMax;
    private int updates, draws, collections;

    internal void BeginUpdate() => updateStart = Open() ? Stopwatch.GetTimestamp() : 0;

    internal void EndUpdate()
    {
        if (updateStart == 0)
        {
            windowStart = 0;
            return;
        }
        Add(ref updateTotal, ref updateMax, updateStart, ref updates);
        updateStart = 0;
        long now = Stopwatch.GetTimestamp();
        if (windowStart == 0)
        {
            Reset(now);
            return;
        }
        if ((now - windowStart) / TicksPerMillisecond < 5000)
            return;
        monitor.Log($"画廊帧耗时（{Game1.activeClickableMenu?.GetType().Name}，{updates} 次更新 / {draws} 次绘制）：" +
            $"更新平均 {Average(updateTotal, updates):0.00} ms、最长 {updateMax:0.00} ms；绘制平均 {Average(drawTotal, draws):0.00} ms、最长 {drawMax:0.00} ms；" +
            $"GC0 {GC.CollectionCount(0) - collections} 次。", LogLevel.Info);
        Reset(now);
    }

    internal void BeginDraw() => drawStart = Open() ? Stopwatch.GetTimestamp() : 0;

    internal void EndDraw()
    {
        if (drawStart != 0)
            Add(ref drawTotal, ref drawMax, drawStart, ref draws);
        drawStart = 0;
    }

    private bool Open() => enabled() && GalleryApplication.OwnsMenu(Game1.activeClickableMenu);

    private static void Add(ref double total, ref double max, long start, ref int count)
    {
        double elapsed = (Stopwatch.GetTimestamp() - start) / TicksPerMillisecond;
        total += elapsed;
        max = Math.Max(max, elapsed);
        count++;
    }

    private static double Average(double total, int count) => count == 0 ? 0 : total / count;

    private void Reset(long now)
    {
        windowStart = now;
        updateTotal = updateMax = drawTotal = drawMax = 0;
        updates = draws = 0;
        collections = GC.CollectionCount(0);
    }
}
