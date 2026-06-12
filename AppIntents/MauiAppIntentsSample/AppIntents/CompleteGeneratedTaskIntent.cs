using Maui.AppIntents;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.AppIntents;

[AppIntent("CompleteGeneratedTaskIntent",
    Title = "Complete Generated Task",
    Description = "Marks a selected task complete through the generated MAUI App Intents bridge",
    RequiresConfirmation = true,
    ConfirmationDialog = "Mark this task as complete?",
    ConfirmationActionName = AppIntentConfirmationAction.Set)]
[AppShortcut("Complete a generated task in ${applicationName}",
    ShortTitle = "Complete Task",
    SystemImageName = "checkmark.circle")]
public sealed class CompleteGeneratedTaskIntent : IAppIntentHandler<CompleteGeneratedTaskIntent.Request>
{
    private readonly ITaskService taskService;

    public CompleteGeneratedTaskIntent(ITaskService taskService)
    {
        this.taskService = taskService;
    }

    public sealed record Request(
        [property: IntentParameter("Task")] AppEntityReference<TaskItem> Task);

    public Task<AppIntentResponse> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Task.Id))
        {
            return Task.FromResult(AppIntentResponse.Failed("Choose a task to complete."));
        }

        var task = taskService.GetById(request.Task.Id);
        if (task is null)
        {
            return Task.FromResult(AppIntentResponse.Failed(
                AppIntentErrorCategory.EntityNotFound,
                "The selected task could not be found."));
        }

        if (!taskService.Complete(task.Id))
        {
            return Task.FromResult(AppIntentResponse.Failed("The selected task could not be completed."));
        }

        return Task.FromResult(AppIntentResponse.Succeeded($"Completed '{task.Title}'."));
    }
}
