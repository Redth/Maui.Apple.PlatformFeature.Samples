using System.Text;
using Maui.AppIntents;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.AppIntents;

[AppIntent("ImportTaskFileIntent",
    Title = "Import Task From File",
    Description = "Creates a task from a text file passed in through the generated MAUI App Intents bridge",
    SupportedModes = AppIntentExecutionModes.Foreground)]
[AppShortcut("Import a task file into ${applicationName}",
    ShortTitle = "Import Task File",
    SystemImageName = "doc.text")]
public sealed class ImportTaskFileIntent : IAppIntentHandler<ImportTaskFileIntent.Request, AppEntityReference<TaskItem>>
{
    private readonly ITaskService taskService;

    public ImportTaskFileIntent(ITaskService taskService)
    {
        this.taskService = taskService;
    }

    public sealed record Request(
        [property: IntentParameter("File")] AppIntentFile File);

    public Task<AppIntentResponse<AppEntityReference<TaskItem>>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file is null || file.Data.Length == 0)
        {
            return Task.FromResult(AppIntentResponse<AppEntityReference<TaskItem>>.Failed("The imported file was empty."));
        }

        var firstLine = ReadFirstLine(file.Data);
        var title = string.IsNullOrWhiteSpace(firstLine)
            ? System.IO.Path.GetFileNameWithoutExtension(file.FileName)
            : firstLine.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Imported Task";
        }

        var task = taskService.Create(
            title,
            TaskPriorityLevel.Medium,
            TaskCategoryType.Personal,
            notes: $"Imported from '{file.FileName}' ({file.Data.Length} bytes) via the generated App Intents bridge.");

        return Task.FromResult(AppIntentResponse<AppEntityReference<TaskItem>>.Succeeded(
            new AppEntityReference<TaskItem>
            {
                Id = task.Id,
                Display = task.Title,
                Subtitle = task.Notes
            },
            $"Imported '{task.Title}' from {file.FileName}."));
    }

    private static string ReadFirstLine(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        using var reader = new System.IO.StringReader(text);
        return reader.ReadLine() ?? "";
    }
}
