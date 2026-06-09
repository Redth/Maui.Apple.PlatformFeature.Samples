using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Maui.AppIntents;

public sealed class AppIntentResponse
{
    public bool Success { get; set; } = true;

    public string? Dialog { get; set; }

    public string? Error { get; set; }

    public object? Value { get; set; }

    [JsonExtensionData]
    public IDictionary<string, object>? Extensions { get; set; }

    public static AppIntentResponse Succeeded(string? dialog = null, object? value = null)
    {
        return new AppIntentResponse
        {
            Success = true,
            Dialog = dialog,
            Value = value
        };
    }

    public static AppIntentResponse Failed(string error, string? dialog = null)
    {
        return new AppIntentResponse
        {
            Success = false,
            Error = error,
            Dialog = dialog
        };
    }
}

public interface IAppIntentHandler<in TRequest>
{
    Task<AppIntentResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
