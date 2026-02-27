# C# Templates for iOS Widget Integration

All code below uses placeholder identifiers. Replace these with the user's actual values:
- `{Namespace}` — The MAUI app's root namespace (e.g., `MyApp`)
- `{GroupId}` — The App Group ID (e.g., `group.com.example.myapp`)
- `{UrlScheme}` — The custom URL scheme (e.g., `myapp`)
- `{UrlHost}` — The URL host (typically `widget`)
- `{WidgetKind}` — The widget kind string (e.g., `MyWidget`)

## Table of Contents

1. [Service Layer](#service-layer)
   - WidgetData.cs
   - WidgetConstants.cs
   - IWidgetDataService.cs
   - StubWidgetDataService.cs
   - WidgetDataService.cs (iOS)
2. [App Integration](#app-integration)
   - AppDelegate.cs
   - App.xaml.cs
   - MauiProgram.cs
3. [MainPage Example](#mainpage-example)

---

## Service Layer

### WidgetData.cs

The shared data contract. Both C# and Swift serialize/deserialize this as JSON. Customize the properties for your use case, but keep `Version` and `Extras` for forward compatibility.

```csharp
// Services/WidgetData.cs
using System.Text.Json.Serialization;

namespace {Namespace}.Services;

public record WidgetData
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("counter")]
    public int Counter { get; init; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; init; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("extras")]
    public Dictionary<string, string> Extras { get; init; } = new();
}
```

**Customization notes:**
- Add domain-specific properties (e.g., `Temperature`, `TaskCount`, `Username`)
- Always use `[JsonPropertyName("camelCase")]` — the Swift side uses camelCase by default
- The `Extras` dictionary is a safety valve for ad-hoc data without schema changes
- `Version` lets you handle schema evolution on the Swift side

### WidgetConstants.cs

Single source of truth for all identifiers. The Swift `Settings.swift` must mirror these exactly.

```csharp
// Services/WidgetConstants.cs
namespace {Namespace}.Services;

public static class WidgetConstants
{
    public const string GroupId = "{GroupId}";
    public const string FromAppFile = "widget_data_fromapp.json";
    public const string FromWidgetFile = "widget_data_fromwidget.json";
    public const string UrlScheme = "{UrlScheme}";
    public const string UrlHost = "{UrlHost}";
    public const string WidgetKind = "{WidgetKind}";
}
```

### IWidgetDataService.cs

Platform-agnostic interface. Pages depend on this, not the iOS implementation.

```csharp
// Services/IWidgetDataService.cs
namespace {Namespace}.Services;

public interface IWidgetDataService
{
    void SendDataToWidget(WidgetData data);
    WidgetData? ReadDataFromWidget();
    void ClearWidgetIncomingData();
    void RefreshWidget(string kind = "{WidgetKind}");
}
```

### StubWidgetDataService.cs

Used on non-iOS platforms so the app compiles and runs everywhere.

```csharp
// Services/StubWidgetDataService.cs
namespace {Namespace}.Services;

public class StubWidgetDataService : IWidgetDataService
{
    public void SendDataToWidget(WidgetData data) { }
    public WidgetData? ReadDataFromWidget() => null;
    public void ClearWidgetIncomingData() { }
    public void RefreshWidget(string kind = "{WidgetKind}") { }
}
```

### WidgetDataService.cs (iOS)

The iOS-specific implementation. Lives under `Platforms/iOS/` so it's only compiled for iOS. Uses **file-based I/O** to the App Group shared container for reliable cross-process communication.

```csharp
// Platforms/iOS/WidgetDataService.cs
using System.Text.Json;
using Foundation;
using {Namespace}.Services;

namespace {Namespace}.Platforms.iOS;

public class WidgetDataService : IWidgetDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private string? GetSharedContainerPath()
    {
        var url = NSFileManager.DefaultManager.GetContainerUrl(WidgetConstants.GroupId);
        return url?.Path;
    }

    private string? GetFilePath(string filename)
    {
        var container = GetSharedContainerPath();
        if (container == null) return null;
        return Path.Combine(container, filename);
    }

    public void SendDataToWidget(WidgetData data)
    {
        var path = GetFilePath(WidgetConstants.FromAppFile);
        if (path == null) return;

        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(path, json);
    }

    public WidgetData? ReadDataFromWidget()
    {
        var path = GetFilePath(WidgetConstants.FromWidgetFile);
        if (path == null || !File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<WidgetData>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void ClearWidgetIncomingData()
    {
        var path = GetFilePath(WidgetConstants.FromWidgetFile);
        if (path != null && File.Exists(path))
            File.Delete(path);
    }

    public void RefreshWidget(string kind = "{WidgetKind}")
    {
        try
        {
            var proxy = new WidgetKit.WidgetCenterProxy();
            proxy.ReloadTimeLinesOfKind(kind);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WidgetDataService] ReloadTimeLinesOfKind failed: {ex}");
        }
    }
}
```

**Key details:**
- Uses `NSFileManager.DefaultManager.GetContainerUrl()` to get the shared App Group container directory, then reads/writes JSON files directly. This is more reliable than `NSUserDefaults(suiteName:)` which can resolve to different plist files for the app vs. widget extension processes.
- **Do NOT use `NSUserDefaults` for cross-process communication** — it can fail silently. See troubleshooting.md for details.
- `WidgetKit.WidgetCenterProxy` comes from the `WidgetKit.WidgetCenterProxy` NuGet — it's a thin .NET binding over Apple's WidgetKit framework
- `ReloadTimeLinesOfKind` is a polite request to iOS — the OS decides when to actually refresh. Usually it's immediate, but iOS may throttle if called too frequently
- **Important for simulator builds**: The main app must be re-signed with entitlements after building with `CodesignRequireProvisioningProfile=false`, otherwise `GetContainerUrl()` returns null. See the build instructions in SKILL.md.

---

## App Integration

### AppDelegate.cs

Override `OpenUrl` to intercept deep links from widget taps.

```csharp
// Platforms/iOS/AppDelegate.cs
using Foundation;
using {Namespace}.Services;
using UIKit;

namespace {Namespace};

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
        if (url.Scheme == WidgetConstants.UrlScheme)
        {
            var uri = new Uri(url.AbsoluteString!);
            App.HandleWidgetUrl(uri);
            return true;
        }

        return base.OpenUrl(application, url, options);
    }
}
```

### App.xaml.cs

Handle incoming deep link URLs and app resume events.

```csharp
// App.xaml.cs
using {Namespace}.Services;

namespace {Namespace};

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        window.Resumed += (s, e) =>
        {
            // When app returns from background, tell the active page to check for widget data
            if (window.Page is AppShell { CurrentPage: MainPage mainPage })
            {
                mainPage.OnResumed();
            }
        };

        return window;
    }

    internal static void HandleWidgetUrl(Uri uri)
    {
        if (uri is not { Scheme: WidgetConstants.UrlScheme, Host: WidgetConstants.UrlHost })
            return;

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        // Parse whatever data you encode in the deep link URL
        var counterValue = query["counter"];

        if (!string.IsNullOrEmpty(counterValue) && int.TryParse(counterValue, out var count))
        {
            NotifyMainPageOfIncomingUrl(count);
        }
    }

    private static void NotifyMainPageOfIncomingUrl(int counterValue)
    {
        var app = Current;
        app?.Dispatcher.Dispatch(() =>
        {
            if (app?.Windows?.Count > 0 &&
                app.Windows[0].Page is AppShell { CurrentPage: MainPage mainPage })
            {
                mainPage.OnResumedByUrl(counterValue);
            }
        });
    }
}
```

**Adapt the deep link parsing** to match whatever data you encode in the widget's `widgetURL()`. The pattern is always: parse the URL, extract values, dispatch to the correct page on the main thread.

### MauiProgram.cs

Register the widget data service via dependency injection.

```csharp
// MauiProgram.cs
using Microsoft.Extensions.Logging;
using {Namespace}.Services;

namespace {Namespace};

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if IOS
        builder.Services.AddSingleton<IWidgetDataService, Platforms.iOS.WidgetDataService>();
#else
        builder.Services.AddSingleton<IWidgetDataService, StubWidgetDataService>();
#endif
        // Register pages that use constructor injection
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
```

---

## MainPage Example

A complete page demonstrating all communication channels. Adapt this pattern for your own pages.

### MainPage.xaml

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:local="clr-namespace:{Namespace}"
             x:Class="{Namespace}.MainPage"
             x:DataType="local:MainPage">

    <ScrollView>
        <VerticalStackLayout Padding="30,0" Spacing="25">

            <Label Text="{Binding Counter}"
                   FontSize="64"
                   FontAttributes="Bold"
                   HorizontalTextAlignment="Center"
                   HorizontalOptions="Center" />

            <Grid ColumnDefinitions="*,*" ColumnSpacing="8">
                <Button Text="-" FontSize="40" Grid.Column="0"
                        Clicked="OnSubtractClicked" HorizontalOptions="Fill" />
                <Button Text="+" FontSize="40" Grid.Column="1"
                        Clicked="OnAddClicked" HorizontalOptions="Fill" />
            </Grid>

            <Label Text="{Binding StatusMessage}"
                   FontSize="14" TextColor="Gray"
                   HorizontalTextAlignment="Center" />

        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

### MainPage.xaml.cs

```csharp
using {Namespace}.Services;

namespace {Namespace};

public partial class MainPage : ContentPage
{
    private readonly IWidgetDataService _widgetService;

    public MainPage(IWidgetDataService widgetService)
    {
        InitializeComponent();
        _widgetService = widgetService;
        BindingContext = this;
    }

    private int _counter;
    public int Counter
    {
        get => _counter;
        set { if (_counter != value) { _counter = value; OnPropertyChanged(); } }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadIncomingWidgetData();
    }

    public void OnResumed() => LoadIncomingWidgetData();

    public void OnResumedByUrl(int incomingCounter)
    {
        Counter = incomingCounter;
        StatusMessage = "Updated via widget tap";
        SyncOutgoingData();
    }

    private void LoadIncomingWidgetData()
    {
        var incoming = _widgetService.ReadDataFromWidget();
        if (incoming != null)
        {
            Counter = incoming.Counter;
            StatusMessage = $"Updated by widget: {incoming.Message}";
            _widgetService.ClearWidgetIncomingData();
            SyncOutgoingData();
        }
        else
        {
            StatusMessage = string.Empty;
        }
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        Counter++;
        StatusMessage = string.Empty;
        SyncOutgoingData();
        _widgetService.RefreshWidget(WidgetConstants.WidgetKind);
    }

    private void OnSubtractClicked(object? sender, EventArgs e)
    {
        Counter--;
        StatusMessage = string.Empty;
        SyncOutgoingData();
        _widgetService.RefreshWidget(WidgetConstants.WidgetKind);
    }

    private void SyncOutgoingData()
    {
        _widgetService.SendDataToWidget(new WidgetData
        {
            Counter = Counter,
            Title = "{Namespace}",
            Message = "Sent from app",
            UpdatedAt = DateTime.UtcNow.ToString("o")
        });
    }
}
```

**The key pattern for any page:**
1. Inject `IWidgetDataService` via constructor
2. Read incoming data in `OnAppearing` and on resume
3. After any data change: write to widget → call `RefreshWidget`
4. Clear incoming data after reading it (so you don't re-process stale data)
