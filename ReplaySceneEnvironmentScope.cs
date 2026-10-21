using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;

namespace StardewGallery;

internal sealed class ReplaySceneEnvironmentScope : IDisposable
{
    private readonly GameLocation location;
    private readonly string contextId;
    private readonly string originalSeason;
    private readonly Color originalEveningColor;
    private readonly int originalTime;
    private readonly WeatherSnapshot originalWeather;
    private bool seasonChanged;
    private bool timeChanged;
    private bool weatherChanged;
    private bool restored;
    private IMonitor? monitor;

    private ReplaySceneEnvironmentScope(GameLocation location)
    {
        this.location = location;
        contextId = location.GetLocationContextId();
        originalSeason = Game1.currentSeason;
        originalEveningColor = Game1.eveningColor;
        originalTime = Game1.timeOfDay;
        originalWeather = WeatherSnapshot.Capture(Game1.netWorldState.Value.GetWeatherForLocation(contextId));
    }

    internal static ReplaySceneEnvironmentScope Apply(GameLocation location, ReplaySceneEnvironment environment, IMonitor monitor)
    {
        ReplaySceneEnvironmentScope scope = new(location) { monitor = monitor };
        scope.TryApply("季节", () => scope.ApplySeason(environment.Season), monitor);
        scope.TryApply("时间", () => scope.ApplyTime(environment.Time), monitor);
        scope.TryApply("天气", () => scope.ApplyWeather(environment.Weather), monitor);
        return scope;
    }

    internal void Restore()
    {
        if (restored)
            return;
        restored = true;
        // Failures writing the original values are restore failures. Failures refreshing the screen,
        // clock or music afterwards leave the restored values intact and are only logged.
        List<Exception> errors = [];
        TryRestore(() =>
        {
            if (!seasonChanged)
                return;
            Game1.currentSeason = originalSeason;
            Game1.eveningColor = originalEveningColor;
            Refresh("季节画面", () => location.updateSeasonalTileSheets());
        }, errors);
        TryRestore(() =>
        {
            if (!timeChanged)
                return;
            Game1.timeOfDay = originalTime;
            Refresh("时钟", RefreshClock);
        }, errors);
        TryRestore(() =>
        {
            if (!weatherChanged)
                return;
            originalWeather.Apply(Game1.netWorldState.Value.GetWeatherForLocation(contextId));
            SyncDefaultWeather(contextId, originalWeather);
            Refresh("天气画面", () => RefreshWeather(originalWeather.IsDebrisWeather));
        }, errors);
        if (errors.Count > 0)
            throw new AggregateException("回放演出环境恢复失败。", errors);
    }

    private void Refresh(string label, Action refresh)
    {
        try { refresh(); }
        catch (Exception error)
        {
            try { monitor?.Log($"回放演出环境的{label}刷新失败；原始数值已恢复。\n{error}", LogLevel.Warn); }
            catch { }
        }
    }

    public void Dispose()
    {
        try { Restore(); }
        catch { }
    }

    private void ApplySeason(string? season)
    {
        if (string.IsNullOrWhiteSpace(season) || season.Equals(Game1.currentSeason, StringComparison.OrdinalIgnoreCase))
            return;
        seasonChanged = true;
        // Only the scene's appearance changes. Game1.setGraphicsForSeason() would run every location's
        // seasonUpdate, clearing out-of-season fruit, fertilizer, forage and weeds in the real world.
        Game1.currentSeason = season;
        Game1.eveningColor = Game1.season == Season.Winter ? new Color(245, 225, 170) : new Color(255, 255, 0);
        location.updateSeasonalTileSheets();
    }

    private void ApplyTime(int? time)
    {
        if (time is null || time == Game1.timeOfDay)
            return;
        timeChanged = true;
        Game1.timeOfDay = time.Value;
        RefreshClock();
    }

    private void ApplyWeather(string? weatherId)
    {
        if (weatherId is null)
            return;
        LocationWeather weather = Game1.netWorldState.Value.GetWeatherForLocation(contextId);
        WeatherSnapshot target = WeatherSnapshot.For(weatherId);
        if (WeatherSnapshot.Capture(weather) == target)
            return;
        weatherChanged = true;
        target.Apply(weather);
        SyncDefaultWeather(contextId, target);
        RefreshWeather(target.IsDebrisWeather);
    }

    private void TryApply(string label, Action apply, IMonitor monitor)
    {
        try { apply(); }
        catch (Exception error)
        {
            monitor.Log($"回放演出环境的{label}设置失败；事件仍会继续播放。\n{error}", LogLevel.Warn);
        }
    }

    private static void TryRestore(Action restore, List<Exception> errors)
    {
        try { restore(); }
        catch (Exception error) { errors.Add(error); }
    }

    private static void RefreshClock()
        => Game1.UpdateGameClock(new GameTime(Game1.currentGameTime.TotalGameTime, TimeSpan.Zero));

    private static void RefreshWeather(bool debris)
    {
        Game1.debrisWeather.Clear();
        if (debris)
            Game1.populateDebrisWeatherArray();
        Game1.updateWeather(new GameTime(Game1.currentGameTime.TotalGameTime, TimeSpan.Zero));
        Game1.updateWeatherIcon();
        if (Game1.currentLocation is GameLocation current)
            GameLocation.HandleMusicChange(current, current);
    }

    private static void SyncDefaultWeather(string contextId, WeatherSnapshot weather)
    {
        if (!contextId.Equals("Default", StringComparison.OrdinalIgnoreCase))
            return;
        Game1.isRaining = weather.IsRaining;
        Game1.isSnowing = weather.IsSnowing;
        Game1.isLightning = weather.IsLightning;
        Game1.isDebrisWeather = weather.IsDebrisWeather;
        Game1.isGreenRain = weather.IsGreenRain;
    }

    private sealed record WeatherSnapshot(
        string Weather,
        bool IsRaining,
        bool IsSnowing,
        bool IsLightning,
        bool IsDebrisWeather,
        bool IsGreenRain)
    {
        internal static WeatherSnapshot Capture(LocationWeather value)
            => new(value.Weather, value.IsRaining, value.IsSnowing, value.IsLightning, value.IsDebrisWeather, value.IsGreenRain);

        internal static WeatherSnapshot For(string weather)
            => new(
                weather,
                IsRaining: weather is "Rain" or "Storm" or "GreenRain",
                IsSnowing: weather == "Snow",
                IsLightning: weather == "Storm",
                IsDebrisWeather: weather == "Wind",
                IsGreenRain: weather == "GreenRain");

        internal void Apply(LocationWeather value)
        {
            value.Weather = Weather;
            value.IsRaining = IsRaining;
            value.IsSnowing = IsSnowing;
            value.IsLightning = IsLightning;
            value.IsDebrisWeather = IsDebrisWeather;
            value.IsGreenRain = IsGreenRain;
        }
    }
}
