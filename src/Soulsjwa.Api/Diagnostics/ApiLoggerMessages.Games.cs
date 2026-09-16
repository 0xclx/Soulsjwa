namespace Soulsjwa.Api.Diagnostics;

public static partial class ApiLoggerMessages
{
    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.PredefinedObjectivesSeeded,
        EventName = DiagnosticsConfig.LogEventNames.PredefinedObjectivesSeeded,
        Level = LogLevel.Information,
        Message = "Seeded predefined objectives: {AddedCount} added out of {DefinitionCount} definitions")]
    public static partial void PredefinedObjectivesSeeded(this ILogger logger, int addedCount, int definitionCount);
}
