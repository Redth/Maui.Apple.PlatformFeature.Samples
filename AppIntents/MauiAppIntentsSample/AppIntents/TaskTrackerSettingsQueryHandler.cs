using Maui.AppIntents;
using MauiAppIntentsSample.Models;

namespace MauiAppIntentsSample.AppIntents;

/// <summary>
/// Resolves the single <see cref="TaskTrackerSettings"/> instance. For a unique entity the generated
/// Swift query only calls the "unique" operation, which the runtime maps to the first suggested
/// entity, but all interface methods return the singleton so the handler also works if referenced
/// through other query paths.
/// </summary>
[AppEntityQueryHandler(typeof(TaskTrackerSettings))]
public sealed class TaskTrackerSettingsQueryHandler : IAppEntityQueryHandler<TaskTrackerSettings>
{
    private static readonly TaskTrackerSettings Current = new();

    public Task<IReadOnlyList<TaskTrackerSettings>> GetEntitiesAsync(
        IReadOnlyList<string> identifiers,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TaskTrackerSettings>>(new[] { Current });
    }

    public Task<IReadOnlyList<TaskTrackerSettings>> SearchEntitiesAsync(
        string query,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TaskTrackerSettings>>(new[] { Current });
    }

    public Task<IReadOnlyList<TaskTrackerSettings>> SuggestedEntitiesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TaskTrackerSettings>>(new[] { Current });
    }
}
