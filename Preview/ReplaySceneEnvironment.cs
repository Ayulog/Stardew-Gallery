namespace StardewGallery;

internal sealed record ReplaySceneEnvironment(string? Season, int? Time, string? Weather, string? Warning = null);

internal static class ReplaySceneEnvironmentResolver
{
    internal static ReplaySceneEnvironment Resolve(
        IReadOnlyList<ConditionExpression> conditions,
        string currentSeason,
        int currentTime,
        string currentWeather)
    {
        string? season = null;
        int? time = null;
        string? weather = null;
        string? warning = null;

        foreach (ConditionExpression condition in conditions)
        {
            switch (condition)
            {
                case SeasonCondition { Negated: false } value when value.Seasons.Count > 0:
                    season = value.Seasons.FirstOrDefault(candidate => candidate.Equals(currentSeason, StringComparison.OrdinalIgnoreCase))
                        ?? value.Seasons[0];
                    break;
                case TimeCondition { Negated: false } value:
                    bool inside = (value.Min is null || currentTime >= value.Min)
                        && (value.Max is null || currentTime <= value.Max);
                    time = inside ? currentTime : value.Min ?? 600;
                    break;
                case WeatherCondition { Negated: false } value:
                    weather = NormalizeWeather(value.Weather, currentWeather);
                    if (weather is null)
                        warning = $"不支持自定义天气要求：{value.Weather}";
                    break;
            }
        }

        return new ReplaySceneEnvironment(season, time, weather, warning);
    }

    private static string? NormalizeWeather(string required, string current)
        => required.ToLowerInvariant() switch
        {
            "sun" or "sunny" => "Sun",
            "rain" => "Rain",
            "rainy" => current is "Rain" or "Storm" ? current : "Rain",
            "storm" or "stormy" => "Storm",
            "snow" or "snowy" => "Snow",
            "wind" or "windy" => "Wind",
            _ => null
        };
}
