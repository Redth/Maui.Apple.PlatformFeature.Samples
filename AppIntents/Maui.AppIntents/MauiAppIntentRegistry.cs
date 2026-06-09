using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Maui.AppIntents;

public sealed class MauiAppIntentRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly Dictionary<string, Func<string, CancellationToken, Task<AppIntentResponse>>> _handlers =
        new Dictionary<string, Func<string, CancellationToken, Task<AppIntentResponse>>>(StringComparer.Ordinal);

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
