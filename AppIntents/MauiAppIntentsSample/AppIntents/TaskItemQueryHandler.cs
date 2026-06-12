using Maui.AppIntents;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.AppIntents;

[AppEntityQueryHandler(typeof(TaskItem))]
public sealed class TaskItemQueryHandler : IAppEntityQueryHandler<TaskItem>
{
    private readonly ITaskService taskService;

    public TaskItemQueryHandler(ITaskService taskService)
    {
        this.taskService = taskService;
    }

    public Task<IReadOnlyList<TaskItem>> GetEntitiesAsync(
        IReadOnlyList<string> identifiers,
        CancellationToken cancellationToken)
    {
        var tasks = identifiers
            .Select(taskService.GetById)
            .Where(static task => task is not null)
            .Cast<TaskItem>()
            .ToList();

        return Task.FromResult<IReadOnlyList<TaskItem>>(tasks);
    }

    public Task<IReadOnlyList<TaskItem>> SearchEntitiesAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var tasks = string.IsNullOrWhiteSpace(query)
            ? taskService.GetFiltered(showCompleted: false)
            : taskService.Search(query);

        return Task.FromResult(tasks);
    }

    public Task<IReadOnlyList<TaskItem>> SuggestedEntitiesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(taskService.GetFiltered(showCompleted: false));
    }
}
