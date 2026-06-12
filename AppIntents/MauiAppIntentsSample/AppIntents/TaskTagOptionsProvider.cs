using Maui.AppIntents;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.AppIntents;

[AppIntentOptionsProvider("taskTags")]
public sealed class TaskTagOptionsProvider : IAppIntentOptionsProvider
{
    private static readonly string[] DefaultTags =
    {
        "Work",
        "Home",
        "Errand",
        "Health",
        "Finance"
    };

    private readonly ITaskService taskService;

    public TaskTagOptionsProvider(ITaskService taskService)
    {
        this.taskService = taskService;
    }

    public Task<IReadOnlyList<AppIntentOption>> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var tags = new List<string>(DefaultTags);

        // Demonstrate live, dynamic options: surface the categories that already
        // have tasks so the suggestion list reflects current app state.
        foreach (var category in taskService.GetAll().Select(task => task.Category).Distinct())
        {
            var label = category.ToString();
            if (!tags.Contains(label, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(label);
            }
        }

        IReadOnlyList<AppIntentOption> options = tags
            .Select(tag => new AppIntentOption(tag))
            .ToList();

        return Task.FromResult(options);
    }
}
