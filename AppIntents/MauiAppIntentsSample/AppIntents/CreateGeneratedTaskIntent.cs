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
public sealed class CreateGeneratedTaskIntent : IAppIntentHandler<CreateGeneratedTaskIntent.Request>
{
    private readonly ITaskService taskService;

    public CreateGeneratedTaskIntent(ITaskService taskService)
    {
        this.taskService = taskService;
    }

    public sealed record Request(
        [property: IntentParameter("Title")] string Title,
        [property: IntentParameter("Estimated Minutes", IsOptional = true)] int? EstimatedMinutes);

    public Task<AppIntentResponse> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Task.FromResult(AppIntentResponse.Failed("A task title is required."));
        }

        var task = taskService.Create(
            request.Title.Trim(),
            TaskPriorityLevel.Medium,
            TaskCategoryType.Personal,
            estimatedMinutes: request.EstimatedMinutes,
            notes: "Created by the generated MAUI App Intents bridge.");

        return Task.FromResult(AppIntentResponse.Succeeded(
            $"Created '{task.Title}' with the generated App Intents bridge."));
    }
}
