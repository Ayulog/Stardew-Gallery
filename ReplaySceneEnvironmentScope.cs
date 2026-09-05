using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;

namespace StardewGallery;

internal sealed class ReplaySceneEnvironmentScope : IDisposable
{
    private readonly string contextId;
    private readonly string originalSeason;
    private readonly int originalTime;
    private readonly WeatherSnapshot originalWeather;
    private bool seasonChanged;
    private bool timeChanged;
    private bool weatherChanged;
    private bool restored;

    private ReplaySceneEnvironmentScope(GameLocation location)
    {
        contextId = location.GetLocationContextId();
        originalSeason = Game1.currentSeason;
        originalTime = Game1.timeOfDay;
        originalWeather = WeatherSnapshot.Capture(Game1.netWorldState.Value.GetWeatherForLocation(contextId));
    }

    internal static ReplaySceneEnvironmentScope Apply(GameLocation location, ReplaySceneEnvironment environment, IMonitor monitor)
    {
        ReplaySceneEnvironmentScope scope = new(location);
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
        List<Exception> errors = [];
        TryRestore(() =>
        {
            if (!seasonChanged)
                return;
            Game1.currentSeason = originalSeason;
            Game1.setGraphicsForSeason();
        }, errors);
        TryRestore(() =>
        {
            if (!timeChanged)
                return;
            Game1.timeOfDay = originalTime;
            RefreshClock();
        }, errors);
        TryRestore(() =>
        {
            if (!weatherChanged)
                return;
            originalWeather.Apply(Game1.netWorldState.Value.GetWeatherForLocation(contextId));
            SyncDefaultWeather(contextId, originalWeather);
            RefreshWeather(originalWeather.IsDebrisWeather);
        }, errors);
        if (errors.Count > 0)
            throw new AggregateException("回放演出环境恢复失败。", errors);
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
        Game1.currentSeason = season;
        Game1.setGraphicsForSeason();
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
