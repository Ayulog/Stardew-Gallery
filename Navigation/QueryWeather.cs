namespace StardewGallery;

internal static class QueryWeather
{
    internal static readonly string[] Standard = ["Sun", "Rain", "Storm", "Snow", "Wind", "GreenRain"];
    internal static string Normalize(string value) => value.ToLowerInvariant() switch
    {
        "sun" or "sunny" => "Sun", "rain" or "rainy" => "Rain", "storm" or "stormy" => "Storm",
        "snow" or "snowy" => "Snow", "wind" or "windy" or "debris" => "Wind", "greenrain" => "GreenRain", _ => value
    };
    internal static IEnumerable<string> Mentioned(ConditionExpression condition) => condition switch
    {
        WeatherCondition weather => [Normalize(weather.WeatherId)],
        NativeQueryCondition query when SafeGameQuery.TryParse(query.Query, out var clauses) => clauses
            .Where(clause => clause.Name == "WEATHER").SelectMany(clause => clause.Arguments.Skip(1)).Select(Normalize),
        _ => []
    };
    internal static bool? Matches(WeatherCondition condition, string selected)
    {
        selected = Normalize(selected);
        bool? raining = selected switch { "Rain" or "Storm" or "GreenRain" => true, "Sun" or "Wind" or "Snow" => false, _ => null };
        return condition.Kind switch { WeatherKind.Rainy => raining, WeatherKind.Sunny => raining is null ? null : !raining.Value,
            _ => Normalize(condition.WeatherId).Equals(selected, StringComparison.Ordinal) };
    }
}
