namespace StardewGallery;

internal sealed record ReplaySceneEnvironment(string? Season, int? Time, string? Weather, string? Warning = null);

/// <summary>
/// Picks the season picture, time and vanilla weather a replay needs from positive requirements: the legacy
/// Season/Time/Weather preconditions and positive GSQ clauses (SEASON, LOCATION_SEASON, SEASON_DAY, TIME, WEATHER).
/// Every requirement narrows the allowed values; the current value is kept when it is allowed, otherwise the first
/// supported allowed value is used. Negated, unsupported or conflicting requirements leave that part unchanged.
/// </summary>
internal static class ReplaySceneEnvironmentResolver
{
    private static readonly string[] VanillaWeather = ["Sun", "Rain", "Storm", "Snow", "Wind", "GreenRain"];
    private const int FirstTime = 600, LastTime = 2600;

    /// <param name="currentSeason">The current season key; null when unknown, so the first allowed season is used.</param>
    /// <param name="eventLocation">The event location name; WEATHER and LOCATION_SEASON for it (or Here/Target) apply.</param>
    internal static ReplaySceneEnvironment Resolve(
        IReadOnlyList<ConditionExpression> conditions,
        int currentTime,
        string currentWeather,
        string? currentSeason = null,
        string? eventLocation = null)
    {
        List<string[]> seasons = [];
        List<string[]> weathers = [];
        (int Min, int Max)? time = null;
        bool seasonConflict = false, timeConflict = false, weatherConflict = false;
        List<string> warnings = [];

        void AddTime(int min, int max)
        {
            time = time is { } prior ? (Math.Max(prior.Min, min), Math.Min(prior.Max, max)) : (min, max);
            timeConflict |= time.Value.Min > time.Value.Max;
        }

        foreach (ConditionExpression condition in conditions)
        {
            switch (condition)
            {
                case SeasonCondition { Negated: false } value when value.Seasons.Count > 0:
                    // The game only ever starts the event in the first listed season.
                    seasons.Add([value.Seasons[0].ToLowerInvariant()]);
                    break;
                case TimeCondition { Negated: false } value:
                    AddTime(value.Min, value.Max);
                    break;
                case WeatherCondition { Negated: false } value:
                    string[]? allowed = LegacyWeather(value.WeatherId);
                    if (allowed is null)
                    {
                        weatherConflict = true;
                        warnings.Add($"不支持自定义天气要求：{value.WeatherId}");
                    }
                    else
                        weathers.Add(allowed);
                    break;
                case NativeQueryCondition { Negated: false } query:
                    // A positive G requires every clause, even when another clause in the same G is random or unlisted.
                    foreach (SafeQueryClause clause in SafeGameQuery.ReadSupportedClauses(query.Query).Where(clause => !clause.Negated))
                    {
                        string[] a = clause.Arguments;
                        switch (clause.Name)
                        {
                            case "SEASON":
                                seasons.Add(a.Select(value => value.ToLowerInvariant()).ToArray());
                                break;
                            case "LOCATION_SEASON" when AtEventLocation(a[0], eventLocation):
                                string[] locationSeasons = a.Skip(1).Select(value => value.ToLowerInvariant()).Where(SafeGameQuery.IsSeason).ToArray();
                                if (locationSeasons.Length == 0)
                                {
                                    seasonConflict = true;
                                    warnings.Add($"不支持自定义季节要求：{string.Join(' ', a.Skip(1))}");
                                }
                                else
                                    seasons.Add(locationSeasons);
                                break;
                            case "SEASON_DAY":
                                seasons.Add(Enumerable.Range(0, a.Length / 2).Select(index => a[index * 2].ToLowerInvariant()).Distinct().ToArray());
                                break;
                            case "TIME":
                                AddTime(SafeGameQuery.ParseInt(a[0]), SafeGameQuery.OptionalMax(a, 1));
                                break;
                            case "WEATHER" when AtEventLocation(a[0], eventLocation):
                                string[] vanilla = a.Skip(1).Select(Canonical).Where(value => value is not null).Cast<string>().ToArray();
                                if (vanilla.Length == 0)
                                {
                                    weatherConflict = true;
                                    warnings.Add($"不支持自定义天气要求：{string.Join(' ', a.Skip(1))}");
                                }
                                else
                                    weathers.Add(vanilla);
                                break;
                        }
                    }
                    break;
            }
        }

        string? season = seasonConflict ? null : Pick(seasons, currentSeason?.ToLowerInvariant());
        if (!seasonConflict && seasons.Count > 0 && season is null)
            warnings.Add("季节要求互相冲突，保持当前季节");
        string? weather = weatherConflict ? null : Pick(weathers, Canonical(currentWeather));
        if (!weatherConflict && weathers.Count > 0 && weather is null)
            warnings.Add("天气要求互相冲突，保持当前天气");
        int? sceneTime = null;
        if (time is { } range)
        {
            int min = Math.Max(range.Min, FirstTime), max = Math.Min(range.Max, LastTime);
            if (timeConflict || min > max)
                warnings.Add("时间要求互相冲突或超出游戏时间，保持当前时间");
            else
                sceneTime = currentTime >= min && currentTime <= max ? currentTime : min;
        }
        return new ReplaySceneEnvironment(season, sceneTime, weather, warnings.Count == 0 ? null : string.Join("；", warnings));
    }

    /// <summary>The current value if every requirement allows it, else the first value of the first requirement allowed by all.</summary>
    private static string? Pick(List<string[]> requirements, string? current)
    {
        if (requirements.Count == 0)
            return null;
        string[] allowed = requirements[0].Where(value => requirements.All(requirement => requirement.Contains(value, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (allowed.Length == 0)
            return null;
        return current is not null && allowed.Contains(current, StringComparer.OrdinalIgnoreCase) ? current : allowed[0];
    }

    /// <summary>GSQ location keys: Here and Target are the event location while its preconditions are checked.</summary>
    private static bool AtEventLocation(string location, string? eventLocation)
        => SafeGameQuery.IsContextLocation(location) || eventLocation is not null && location.Equals(eventLocation, StringComparison.OrdinalIgnoreCase);

    private static string? Canonical(string weather)
        => VanillaWeather.FirstOrDefault(value => value.Equals(weather, StringComparison.OrdinalIgnoreCase));

    /// <summary>Legacy Weather: rainy means any raining weather (Rain first), sunny means not raining (shown as Sun).</summary>
    private static string[]? LegacyWeather(string required)
        => required.ToLowerInvariant() switch
        {
            "sun" or "sunny" => ["Sun"],
            "rain" => ["Rain"],
            "rainy" => ["Rain", "Storm", "GreenRain"],
            "storm" or "stormy" => ["Storm"],
            "snow" or "snowy" => ["Snow"],
            "greenrain" => ["GreenRain"],
            "wind" or "windy" => ["Wind"],
            _ => null
        };
}
