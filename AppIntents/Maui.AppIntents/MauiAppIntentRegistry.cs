using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Maui.AppIntents;

public sealed class MauiAppIntentRegistry
{
    public const string EntityQueryEntitiesOperation = "entities";
    public const string EntityQueryMatchingOperation = "matching";
    public const string EntityQuerySuggestedOperation = "suggested";
    public const string EntityQueryUniqueOperation = "unique";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly Dictionary<string, Func<string, CancellationToken, Task<AppIntentResponse>>> _handlers =
        new Dictionary<string, Func<string, CancellationToken, Task<AppIntentResponse>>>(StringComparer.Ordinal);

    private readonly Dictionary<string, Func<string, string, CancellationToken, Task<AppIntentEntityQueryResponse>>> _entityQueries =
        new Dictionary<string, Func<string, string, CancellationToken, Task<AppIntentEntityQueryResponse>>>(StringComparer.Ordinal);

    public void Map<TRequest, THandler>(string identifier, IServiceProvider services)
        where THandler : IAppIntentHandler<TRequest>
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Intent identifier cannot be empty.", nameof(identifier));
        }

        _handlers[identifier] = async (payload, cancellationToken) =>
        {
            var request = JsonSerializer.Deserialize<TRequest>(payload, JsonOptions);
            if (request is null)
            {
                return AppIntentResponse.Failed($"Could not deserialize payload for intent '{identifier}'.");
            }

            var handler = services.GetRequiredService<THandler>();
            return await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
        };
    }

    public void Map<TRequest, THandler, TResult>(string identifier, IServiceProvider services)
        where THandler : IAppIntentHandler<TRequest, TResult>
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Intent identifier cannot be empty.", nameof(identifier));
        }

        _handlers[identifier] = async (payload, cancellationToken) =>
        {
            var request = JsonSerializer.Deserialize<TRequest>(payload, JsonOptions);
            if (request is null)
            {
                return AppIntentResponse.Failed($"Could not deserialize payload for intent '{identifier}'.");
            }

            var handler = services.GetRequiredService<THandler>();
            var response = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            return new AppIntentResponse
            {
                Success = response.Success,
                Dialog = response.Dialog,
                Error = response.Error,
                ErrorCategory = response.ErrorCategory,
                Value = response.Value
            };
        };
    }

    public void MapEntity<TEntity, THandler>(
        string identifier,
        IServiceProvider services,
        Func<TEntity, AppIntentEntityValue> projector)
        where THandler : IAppEntityQueryHandler<TEntity>
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Entity identifier cannot be empty.", nameof(identifier));
        }

        if (projector is null)
        {
            throw new ArgumentNullException(nameof(projector));
        }

        _entityQueries[identifier] = async (operation, payload, cancellationToken) =>
        {
            var handler = services.GetRequiredService<THandler>();
            IReadOnlyList<TEntity> entities;
            switch (operation)
            {
                case EntityQueryEntitiesOperation:
                    var identifiersPayload = JsonSerializer.Deserialize<EntityIdentifiersPayload>(payload, JsonOptions);
                    entities = await handler.GetEntitiesAsync(
                        identifiersPayload?.Identifiers ?? Array.Empty<string>(),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case EntityQueryMatchingOperation:
                    var queryPayload = JsonSerializer.Deserialize<EntityMatchingPayload>(payload, JsonOptions);
                    entities = await handler.SearchEntitiesAsync(
                        queryPayload?.Query ?? "",
                        cancellationToken).ConfigureAwait(false);
                    break;
                case EntityQuerySuggestedOperation:
                    entities = await handler.SuggestedEntitiesAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case EntityQueryUniqueOperation:
                    var suggested = await handler.SuggestedEntitiesAsync(cancellationToken).ConfigureAwait(false);
                    entities = suggested.Take(1).ToList();
                    break;
                default:
                    entities = Array.Empty<TEntity>();
                    break;
            }

            return new AppIntentEntityQueryResponse
            {
                Entities = entities.Select(projector).Where(static entity => !string.IsNullOrWhiteSpace(entity.Id)).ToList()
            };
        };
    }

    public async Task<string> DispatchToJsonAsync(string identifier, string payload, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(identifier, out var handler))
        {
            return JsonSerializer.Serialize(
                AppIntentResponse.Failed($"No C# handler is registered for intent '{identifier}'."),
                JsonOptions);
        }

        try
        {
            var response = await handler(payload, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(AppIntentResponse.Failed(ex.Message), JsonOptions);
        }
    }

    public async Task<string> DispatchEntityQueryToJsonAsync(
        string identifier,
        string operation,
        string payload,
        CancellationToken cancellationToken = default)
    {
        if (!_entityQueries.TryGetValue(identifier, out var handler))
        {
            return JsonSerializer.Serialize(new AppIntentEntityQueryResponse(), JsonOptions);
        }

        try
        {
            var response = await handler(operation, payload, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppIntents] Entity query '{identifier}' operation '{operation}' failed: {ex.Message}");
            return JsonSerializer.Serialize(new AppIntentEntityQueryResponse(), JsonOptions);
        }
    }

    internal static void RegisterGenerated(IServiceProvider services, MauiAppIntentRegistry registry)
    {
        var registrationType = Assembly.GetEntryAssembly()?.GetType(
            "Maui.AppIntents.Generated.MauiAppIntentGeneratedRegistration",
            throwOnError: false);

        if (registrationType is null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                registrationType = assembly.GetType(
                    "Maui.AppIntents.Generated.MauiAppIntentGeneratedRegistration",
                    throwOnError: false);

                if (registrationType is not null)
                {
                    break;
                }
            }
        }

        var registerMethod = registrationType?.GetMethod(
            "Register",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(IServiceProvider), typeof(MauiAppIntentRegistry) },
            modifiers: null);

        registerMethod?.Invoke(null, new object[] { services, registry });
    }

    private sealed class EntityIdentifiersPayload
    {
        public string[] Identifiers { get; set; } = Array.Empty<string>();
    }

    private sealed class EntityMatchingPayload
    {
        public string Query { get; set; } = "";
    }
}

public sealed class MauiAppIntentDispatcher
{
    private readonly MauiAppIntentRegistry _registry;

    public MauiAppIntentDispatcher(MauiAppIntentRegistry registry)
    {
        _registry = registry;
    }

    public string Dispatch(string identifier, string payload)
    {
        return _registry.DispatchToJsonAsync(identifier, payload).GetAwaiter().GetResult();
    }

    public string DispatchEntityQuery(string identifier, string operation, string payload)
    {
        return _registry.DispatchEntityQueryToJsonAsync(identifier, operation, payload).GetAwaiter().GetResult();
    }
}

public interface IMauiAppIntentNativeBridge
{
    void SetDispatcher(MauiAppIntentDispatcher dispatcher);
}

public static class MauiAppIntentServiceCollectionExtensions
{
    public static IServiceCollection AddMauiAppIntents(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var registry = new MauiAppIntentRegistry();
            MauiAppIntentRegistry.RegisterGenerated(sp, registry);
            return registry;
        });

        services.AddSingleton<MauiAppIntentDispatcher>();
        return services;
    }
}
