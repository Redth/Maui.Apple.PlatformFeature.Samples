using Maui.AppIntents;
using MauiAppIntentsSample.Models;

namespace MauiAppIntentsSample.AppIntents;

/// <summary>
/// Reads the singleton <see cref="TaskTrackerSettings"/> unique entity. Because it references a
/// unique (iOS 18+) entity, the generated intent is emitted as iOS 18+ and intentionally has no
/// App Shortcut; it is still donatable and runnable from Shortcuts.
/// </summary>
[AppIntent("ShowTaskTrackerSettingsIntent",
    Title = "Show Tracker Settings",
    Description = "Reads the singleton task tracker settings through the generated bridge")]
public sealed class ShowTaskTrackerSettingsIntent : IAppIntentHandler<ShowTaskTrackerSettingsIntent.Request>
{
    public sealed record Request(
        [property: IntentParameter("Settings")] AppEntityReference<TaskTrackerSettings> Settings);

    public Task<AppIntentResponse> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var label = string.IsNullOrWhiteSpace(request.Settings.Display)
            ? "Task Tracker Settings"
            : request.Settings.Display;

        return Task.FromResult(AppIntentResponse.Succeeded($"Loaded settings: {label}."));
    }
}
