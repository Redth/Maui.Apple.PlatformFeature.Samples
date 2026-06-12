using Maui.AppIntents;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.AppIntents;

[AppIntent("ListGeneratedTasksIntent",
    Title = "List Generated Tasks",
    Description = "Returns matching tasks through the generated MAUI App Intents bridge")]
[AppShortcut("List generated tasks in ${applicationName}",
    ShortTitle = "List Tasks",
    SystemImageName = "list.bullet")]
public sealed class ListGeneratedTasksIntent : IAppIntentHandler<ListGeneratedTasksIntent.Request, List<AppEntityReference<TaskItem>>>
{
    private readonly ITaskService taskService;

    public ListGeneratedTasksIntent(ITaskService taskService)
    {
        this.taskService = taskService;
    }

    public sealed record Request(
        [property: IntentParameter("Category", IsOptional = true)] TaskCategoryType? Category,
        [property: IntentParameter("Priority", IsOptional = true)] TaskPriorityLevel? Priority,
        [property: IntentParameter("Show Completed", IsOptional = true)] bool? ShowCompleted);

    public Task<AppIntentResponse<List<AppEntityReference<TaskItem>>>> HandleAsync(
        Request request,
        CancellationToken cancellationToken)
    {
        var tasks = taskService
            .GetFiltered(request.Category, request.Priority, request.ShowCompleted ?? false)
            .Select(ToReference)
            .ToList();

        return Task.FromResult(AppIntentResponse<List<AppEntityReference<TaskItem>>>.Succeeded(
            tasks,
            $"Found {tasks.Count} generated task{(tasks.Count == 1 ? "" : "s")}."));
    }

    private static AppEntityReference<TaskItem> ToReference(TaskItem task)
    {
        return new AppEntityReference<TaskItem>
        {
            Id = task.Id,
            Display = task.Title,
            Subtitle = task.Notes
        };
    }
}
