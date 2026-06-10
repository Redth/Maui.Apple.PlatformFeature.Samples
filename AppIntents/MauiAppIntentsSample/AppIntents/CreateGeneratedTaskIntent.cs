using Maui.AppIntents;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.AppIntents;

[AppIntent("CreateGeneratedTaskIntent",
    Title = "Create Generated Task",
    Description = "Creates a task through the generated MAUI App Intents bridge")]
[AppShortcut("Create a generated task in ${applicationName}",
    ShortTitle = "Create Generated Task",
    SystemImageName = "sparkles")]
public sealed class CreateGeneratedTaskIntent : IAppIntentHandler<CreateGeneratedTaskIntent.Request, AppEntityReference<TaskItem>>
{
    private readonly ITaskService taskService;

    public CreateGeneratedTaskIntent(ITaskService taskService)
    {
        this.taskService = taskService;
    }

    public sealed record Request(
        [property: IntentParameter("Title")] string Title,
        [property: IntentParameter("Estimated Minutes", IsOptional = true)] int? EstimatedMinutes,
        [property: IntentParameter("Priority", IsOptional = true)] TaskPriorityLevel? Priority,
        [property: IntentParameter("Category", IsOptional = true)] TaskCategoryType? Category);

    public Task<AppIntentResponse<AppEntityReference<TaskItem>>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[AppIntents] Generated handler invoked: {request.Title}");

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Task.FromResult(AppIntentResponse<AppEntityReference<TaskItem>>.Failed("A task title is required."));
        }

        var task = taskService.Create(
            request.Title.Trim(),
            request.Priority ?? TaskPriorityLevel.Medium,
            request.Category ?? TaskCategoryType.Personal,
            estimatedMinutes: request.EstimatedMinutes,
            notes: "Created by the generated MAUI App Intents bridge.");

        return Task.FromResult(AppIntentResponse<AppEntityReference<TaskItem>>.Succeeded(
            ToReference(task),
            $"Created '{task.Title}' with the generated App Intents bridge."));
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
