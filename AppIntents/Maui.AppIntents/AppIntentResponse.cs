using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Maui.AppIntents;

/// <summary>
/// Maps a failed intent to a typed Swift <c>AppIntentError</c> case (iOS 18+). On earlier OS
/// versions the generated code falls back to a generic failure carrying the error message.
/// </summary>
public enum AppIntentErrorCategory
{
    /// <summary>No typed category; a generic failure with the error message is surfaced.</summary>
    None = 0,

    // AppIntentError.Unrecoverable
    NetworkFailure,
    NotAllowed,
    UnsupportedOnDevice,
    FeatureRestricted,
    EntityNotFound,

    // AppIntentError.UserActionRequired
    NeedsSignIn,
    NeedsAccountSetup,
    NeedsConfirmation,

    // AppIntentError.PermissionRequired
    PermissionSiri,
    PermissionPhotos,
    PermissionContacts,
    PermissionLocation,
    PermissionBluetooth,
    PermissionLocalNetwork,
}

public sealed class AppIntentResponse
{
    public bool Success { get; set; } = true;

    public string? Dialog { get; set; }

    public string? Error { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppIntentErrorCategory ErrorCategory { get; set; } = AppIntentErrorCategory.None;

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

    public static AppIntentResponse Failed(AppIntentErrorCategory category, string error, string? dialog = null)
    {
        return new AppIntentResponse
        {
            Success = false,
            Error = error,
            ErrorCategory = category,
            Dialog = dialog
        };
    }
}

public sealed class AppIntentResponse<TResult>
{
    public bool Success { get; set; } = true;

    public string? Dialog { get; set; }

    public string? Error { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppIntentErrorCategory ErrorCategory { get; set; } = AppIntentErrorCategory.None;

    public TResult? Value { get; set; }

    [JsonExtensionData]
    public IDictionary<string, object>? Extensions { get; set; }

    public static AppIntentResponse<TResult> Succeeded(TResult value, string? dialog = null)
    {
        return new AppIntentResponse<TResult>
        {
            Success = true,
            Dialog = dialog,
            Value = value
        };
    }

    public static AppIntentResponse<TResult> Failed(string error, string? dialog = null)
    {
        return new AppIntentResponse<TResult>
        {
            Success = false,
            Error = error,
            Dialog = dialog
        };
    }

    public static AppIntentResponse<TResult> Failed(AppIntentErrorCategory category, string error, string? dialog = null)
    {
        return new AppIntentResponse<TResult>
        {
            Success = false,
            Error = error,
            ErrorCategory = category,
            Dialog = dialog
        };
    }
}

public interface IAppIntentHandler<in TRequest>
{
    Task<AppIntentResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public interface IAppIntentHandler<in TRequest, TResult>
{
    Task<AppIntentResponse<TResult>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public sealed class AppEntityReference<TEntity>
{
    public string Id { get; set; } = "";

    public string Display { get; set; } = "";

    public string? Subtitle { get; set; }
}

public sealed class AppIntentEntityValue
{
    public string Id { get; set; } = "";

    public string Display { get; set; } = "";

    public string? Subtitle { get; set; }

    public IDictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();
}

public sealed class AppIntentEntityQueryResponse
{
    public IList<AppIntentEntityValue> Entities { get; set; } = new List<AppIntentEntityValue>();
}

public interface IAppEntityQueryHandler<TEntity>
{
    Task<IReadOnlyList<TEntity>> GetEntitiesAsync(IReadOnlyList<string> identifiers, CancellationToken cancellationToken);

    Task<IReadOnlyList<TEntity>> SearchEntitiesAsync(string query, CancellationToken cancellationToken);

    Task<IReadOnlyList<TEntity>> SuggestedEntitiesAsync(CancellationToken cancellationToken);
}
