using Maui.AppIntents;

namespace MauiAppIntentsSample.Models;

/// <summary>
/// A singleton settings entity. <c>Unique = true</c> makes the generated Swift type conform to
/// <c>UniqueAppEntity</c> (iOS 18+) with a <c>UniqueAppEntityQuery</c> that resolves the single
/// instance through the bridge instead of a by-id/string query. Any intent that references a unique
/// entity is generated as iOS 18+ and cannot expose an App Shortcut.
/// </summary>
[AppEntity("TaskTrackerSettings", TypeDisplayName = "Tracker Settings", Unique = true)]
public class TaskTrackerSettings
{
    [AppEntityIdentifier]
    public string Id { get; set; } = "settings";

    [AppEntityDisplay]
    public string Title { get; set; } = "Task Tracker Settings";

    [AppEntityProperty("Show Completed By Default")]
    public bool ShowCompletedByDefault { get; set; }

    [AppEntityProperty("Daily Task Goal")]
    public int DailyTaskGoal { get; set; } = 5;
}
