using Maui.AppIntents;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.AppIntents;

[AppIntent("CompleteGeneratedTasksIntent",
    Title = "Complete Generated Tasks",
    Description = "Marks multiple selected tasks complete through the generated MAUI App Intents bridge")]
[AppShortcut("Complete generated tasks in ${applicationName}",
    ShortTitle = "Complete Tasks",
    SystemImageName = "checkmark.circle.fill")]
public sealed class CompleteGeneratedTasksIntent : IAppIntentHandler<CompleteGeneratedTasksIntent.Request, int>
{
    private readonly ITaskService taskService;

    public CompleteGeneratedTasksIntent(ITaskService taskService)
    {
        this.taskService = taskService;
    }

    public sealed record Request(
        [property: IntentParameter("Tasks")] List<AppEntityReference<TaskItem>> Tasks);

    public Task<AppIntentResponse<int>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var completed = 0;
        foreach (var task in request.Tasks)
        {
            if (!string.IsNullOrWhiteSpace(task.Id) && taskService.Complete(task.Id))
            {
                completed++;
            }
        }

        return Task.FromResult(AppIntentResponse<int>.Succeeded(
            completed,
            $"Completed {completed} generated task{(completed == 1 ? "" : "s")}."));
    }
}
