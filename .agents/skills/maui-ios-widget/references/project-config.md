# Project Configuration Templates

All configuration files needed to wire the iOS Widget Extension into a .NET MAUI build.

Replace placeholders:
- `{AppBundleId}` — e.g., `com.example.myapp`
- `{GroupId}` — e.g., `group.com.example.myapp`
- `{UrlScheme}` — e.g., `myapp`
- `{ExtensionName}` — e.g., `SimpleWidgetExtension`
- `{BundleEntryPoint}` — e.g., `SimpleWidgetExtension.SimpleWidgetBundle`

## Table of Contents

1. [Entitlements and Plists](#entitlements-and-plists)
2. [Project File Configuration](#project-file-configuration)
3. [Build Script](#build-script)
4. [Asset Catalogs](#asset-catalogs)

---

## Entitlements and Plists

### Entitlements.plist (MAUI App)

Place at `Platforms/iOS/Entitlements.plist`. This grants the MAUI app access to the App Group shared storage.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>com.apple.security.application-groups</key>
	<array>
		<string>{GroupId}</string>
	</array>
</dict>
</plist>
```

### Entitlements.WidgetExtension.plist (Widget)

Place at `Platforms/iOS/Entitlements.WidgetExtension.plist`. Same App Group — this is how both sides share UserDefaults.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>com.apple.security.application-groups</key>
	<array>
		<string>{GroupId}</string>
	</array>
</dict>
</plist>
```

**CRITICAL: Line endings must be LF (Unix-style), not CRLF (Windows-style).** CRLF line endings cause the MAUI build to silently fail when reading entitlements. After creating these files, always run:

```bash
sed -i '' 's/\r$//' Platforms/iOS/Entitlements.plist Platforms/iOS/Entitlements.WidgetExtension.plist
```

### Widget Entitlements (Xcode side)

Place at `XCodeWidget/SimpleWidgetExtension.entitlements`. Referenced by the Xcode project's build settings.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>com.apple.security.application-groups</key>
	<array>
		<string>{GroupId}</string>
	</array>
</dict>
</plist>
```

### Info.plist (MAUI App — URL Scheme)

Add the `CFBundleURLTypes` block to the existing `Platforms/iOS/Info.plist` to register the deep link URL scheme:

```xml
<key>CFBundleURLTypes</key>
<array>
    <dict>
        <key>CFBundleURLName</key>
        <string>{AppBundleId}</string>
        <key>CFBundleURLSchemes</key>
        <array>
            <string>{UrlScheme}</string>
        </array>
    </dict>
</array>
```

Insert this inside the top-level `<dict>`, before the closing `</dict>`.

### Info.plist (Widget Extension)

Place at `XCodeWidget/{ExtensionName}/Info.plist`. Configures the widget extension's entry point and icon.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>NSExtension</key>
	<dict>
		<key>NSExtensionPointIdentifier</key>
		<string>com.apple.widgetkit-extension</string>
	</dict>
	<key>NSExtensionPrincipalClass</key>
	<string>{BundleEntryPoint}</string>
	<key>CFBundleIcons</key>
	<dict>
		<key>CFBundlePrimaryIcon</key>
		<dict>
			<key>CFBundleIconFiles</key>
			<array>
				<string>AppIcon</string>
			</array>
			<key>UIPrerenderedIcon</key>
			<false/>
		</dict>
	</dict>
	<key>CFBundleIconName</key>
	<string>AppIcon</string>
</dict>
</plist>
```

**NSExtensionPrincipalClass format:** `{TargetProductModuleName}.{WidgetBundleStructName}`. The module name can be found in Xcode: Extension target → Build Settings → Product Module Name.

---

## Project File Configuration

### .csproj Additions

Add these sections to the MAUI `.csproj` file. They go after the existing `<ItemGroup>` elements.

```xml
<!-- iOS-specific: Widget Extension embedding and WidgetKit binding -->
<PropertyGroup Condition="$(TargetFramework.Contains('-ios'))">
    <CodesignEntitlements>Platforms/iOS/Entitlements.plist</CodesignEntitlements>
</PropertyGroup>

<ItemGroup Condition="$(TargetFramework.Contains('-ios'))">
    <PackageReference Include="WidgetKit.WidgetCenterProxy" Version="9.0.3" />

    <!-- Copy the correct widget .appex to output based on platform -->
    <Content Remove="Platforms\iOS\WidgetExtensions\**" />
    <Content Condition="'$(ComputedPlatform)' == 'iPhone'"
             Include=".\Platforms\iOS\WidgetExtensions\Release-iphoneos\{ExtensionName}.appex\**"
             CopyToOutputDirectory="PreserveNewest" />
    <Content Condition="'$(ComputedPlatform)' == 'iPhoneSimulator'"
             Include=".\Platforms\iOS\WidgetExtensions\Release-iphonesimulator\{ExtensionName}.appex\**"
             CopyToOutputDirectory="PreserveNewest" />

    <Content Include=".\Platforms\iOS\Entitlements.WidgetExtension.plist"
             CopyToOutputDirectory="PreserveNewest" />

    <!-- Embed the widget extension into the app bundle -->
    <AdditionalAppExtensions Include="$(MSBuildProjectDirectory)/Platforms/iOS/WidgetExtensions">
        <Name>{ExtensionName}</Name>
        <BuildOutput Condition="'$(ComputedPlatform)' == 'iPhone'">Release-iphoneos</BuildOutput>
        <BuildOutput Condition="'$(ComputedPlatform)' == 'iPhoneSimulator'">Release-iphonesimulator</BuildOutput>
        <CodesignEntitlements>Platforms/iOS/Entitlements.WidgetExtension.plist</CodesignEntitlements>
    </AdditionalAppExtensions>
</ItemGroup>

<!-- Automatically build the Xcode widget extension as part of dotnet build.
     The XcodeProject item group (from dotnet/macios) only supports frameworks, not .appex bundles,
     so we use a custom target that invokes xcodebuild and feeds the result to AdditionalAppExtensions.
     Skip with: dotnet build -p:SkipWidgetBuild=true -->
<Target Name="BuildWidgetExtension"
        AfterTargets="ResolveReferences"
        Condition="$(TargetFramework.Contains('-ios')) AND '$(SkipWidgetBuild)' != 'true'">

    <PropertyGroup>
        <_WidgetIsSimulator>$(RuntimeIdentifier.Contains('simulator'))</_WidgetIsSimulator>
        <_WidgetSdk Condition="'$(_WidgetIsSimulator)' == 'true'">iphonesimulator</_WidgetSdk>
        <_WidgetSdk Condition="'$(_WidgetIsSimulator)' != 'true'">iphoneos</_WidgetSdk>
        <_WidgetArch Condition="$(RuntimeIdentifier.Contains('arm64'))">arm64</_WidgetArch>
        <_WidgetArch Condition="$(RuntimeIdentifier.Contains('x64'))">x86_64</_WidgetArch>
        <_WidgetArch Condition="'$(_WidgetArch)' == ''">arm64</_WidgetArch>
        <_WidgetConfiguration>Release</_WidgetConfiguration>
        <_WidgetOutputSubdir>$(_WidgetConfiguration)-$(_WidgetSdk)</_WidgetOutputSubdir>
        <_XcodeProjectDir>$(MSBuildProjectDirectory)/XCodeWidget</_XcodeProjectDir>
        <_XcodeProjectPath>$(_XcodeProjectDir)/XCodeWidget.xcodeproj</_XcodeProjectPath>
        <_WidgetBuildDir>$(_XcodeProjectDir)/build</_WidgetBuildDir>
        <_WidgetAppexSource>$(_WidgetBuildDir)/$(_WidgetOutputSubdir)/{ExtensionName}.appex</_WidgetAppexSource>
        <_WidgetDestDir>$(MSBuildProjectDirectory)/Platforms/iOS/WidgetExtensions/$(_WidgetOutputSubdir)</_WidgetDestDir>
    </PropertyGroup>

    <!-- Generate xcodeproj from project.yml if .xcodeproj doesn't exist -->
    <Error Text="XCodeWidget directory not found. Expected Xcode widget project at: $(_XcodeProjectDir)"
           Condition="!Exists('$(_XcodeProjectDir)')" />
    <Exec Command="xcodegen generate"
          WorkingDirectory="$(_XcodeProjectDir)"
          Condition="!Exists('$(_XcodeProjectPath)') AND Exists('$(_XcodeProjectDir)/project.yml')" />
    <Error Text="No .xcodeproj or project.yml found in $(_XcodeProjectDir). Cannot build widget extension."
           Condition="!Exists('$(_XcodeProjectPath)')" />

    <!-- Build the widget extension -->
    <Message Text="Building widget extension ({ExtensionName}) for $(_WidgetSdk) $(_WidgetArch)..." Importance="high" />
    <Exec Command="xcodebuild -quiet -project XCodeWidget.xcodeproj -target {ExtensionName} -configuration $(_WidgetConfiguration) -sdk $(_WidgetSdk) -arch $(_WidgetArch) CODE_SIGN_IDENTITY=&quot;-&quot; CODE_SIGNING_REQUIRED=NO CODE_SIGNING_ALLOWED=NO BUILD_DIR=build build"
          WorkingDirectory="$(_XcodeProjectDir)" />

    <!-- Copy .appex to WidgetExtensions directory for AdditionalAppExtensions to embed -->
    <MakeDir Directories="$(_WidgetDestDir)" />
    <Exec Command="rm -rf &quot;$(_WidgetDestDir)/{ExtensionName}.appex&quot; &amp;&amp; cp -R &quot;$(_WidgetAppexSource)&quot; &quot;$(_WidgetDestDir)/&quot;" />
    <Message Text="Widget extension built and staged at $(_WidgetDestDir)" Importance="high" />
</Target>
```

**What each piece does:**
- `CodesignEntitlements` — tells the build to sign the app with the App Group entitlement
- `WidgetKit.WidgetCenterProxy` — NuGet package that provides `.NET` bindings for `WidgetKit` (check NuGet.org for the latest version that matches the target framework)
- `Content` items — copy the pre-built `.appex` files to the output directory so the build host can find them
- `Content Remove` — prevents double-inclusion of widget files
- `AdditionalAppExtensions` — the critical element that tells the MAUI build to embed the `.appex` into the final `.app` bundle and code-sign it with the widget's entitlements
- `BuildWidgetExtension` target — automatically invokes `xcodebuild` to build the widget extension during `dotnet build`, so you don't need a separate build step. Derives SDK (iphonesimulator/iphoneos) and architecture from the RuntimeIdentifier. Will also run `xcodegen generate` if `.xcodeproj` doesn't exist.

**Why a custom target instead of `XcodeProject`?** The `<XcodeProject>` MSBuild item group (from dotnet/macios, introduced in .NET 9) only supports building Xcode projects into XCFrameworks for use as `NativeReference`. Widget extensions produce `.appex` bundles, not frameworks, so `XcodeProject` can't be used for them.

**Paths are case-sensitive and must be exact.** A wrong path means the widget silently won't be included.

---

## Build Script

### build-release.sh

Place at `XCodeWidget/build-release.sh`. Builds the widget extension for both device and simulator.

```bash
#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

# Update these to match your Xcode project
XCODEPROJ="XCodeWidget.xcodeproj"
SCHEME="{ExtensionName}"

echo "🧹 Cleaning previous builds..."
rm -Rf XReleases

echo "📱 Building for iOS device (iphoneos)..."
xcodebuild -project "$XCODEPROJ" \
    -scheme "$SCHEME" \
    -configuration Release \
    -sdk iphoneos \
    BUILD_DIR="$(pwd)/XReleases" clean build

echo "🖥️  Building for iOS simulator (iphonesimulator)..."
xcodebuild -project "$XCODEPROJ" \
    -scheme "$SCHEME" \
    -configuration Release \
    -sdk iphonesimulator \
    BUILD_DIR="$(pwd)/XReleases" clean build

echo ""
echo "✅ Build complete!"
echo "   Device:    XReleases/Release-iphoneos/{ExtensionName}.appex"
echo "   Simulator: XReleases/Release-iphonesimulator/{ExtensionName}.appex"
echo ""
echo "📋 Copy to MAUI project:"
echo "   cp -R XReleases/Release-iphoneos ../Platforms/iOS/WidgetExtensions/"
echo "   cp -R XReleases/Release-iphonesimulator ../Platforms/iOS/WidgetExtensions/"
```

Make executable: `chmod +x XCodeWidget/build-release.sh`

**Why `XReleases`?** The `X` prefix prevents the folder from being excluded by the default Visual Studio `.gitignore`, which typically ignores `Releases/`.

---

## Asset Catalogs

### Assets.xcassets/Contents.json

```json
{
  "info" : {
    "author" : "xcode",
    "version" : 1
  }
}
```

### Assets.xcassets/AppIcon.appiconset/Contents.json

```json
{
  "images" : [
    {
      "idiom" : "universal",
      "platform" : "ios",
      "size" : "1024x1024"
    }
  ],
  "info" : {
    "author" : "xcode",
    "version" : 1
  }
}
```

Place a 1024×1024 PNG in this directory and reference it in the JSON to set the widget's icon. If no icon is provided, the widget uses the app's icon.

### Assets.xcassets/AccentColor.colorset/Contents.json

```json
{
  "colors" : [
    {
      "idiom" : "universal"
    }
  ],
  "info" : {
    "author" : "xcode",
    "version" : 1
  }
}
```

### Assets.xcassets/WidgetBackground.colorset/Contents.json

```json
{
  "colors" : [
    {
      "idiom" : "universal"
    }
  ],
  "info" : {
    "author" : "xcode",
    "version" : 1
  }
}
```

---

## Directory Structure Summary

After setup, the MAUI project should have this iOS-specific structure:

```
Platforms/iOS/
├── AppDelegate.cs
├── Info.plist                              # With CFBundleURLTypes
├── Entitlements.plist                      # App Group for the app
├── Entitlements.WidgetExtension.plist      # App Group for the widget
├── WidgetDataService.cs                    # iOS service implementation
├── Program.cs                             # (standard, unchanged)
└── WidgetExtensions/                      # Built .appex files
    ├── Release-iphoneos/
    │   └── {ExtensionName}.appex/
    └── Release-iphonesimulator/
        └── {ExtensionName}.appex/
```

And the Xcode project:

```
XCodeWidget/
├── XCodeWidget.xcodeproj/                 # Created via Xcode
├── XCodeWidget/                           # Thin host app (not shipped)
│   ├── XCodeWidgetApp.swift
│   └── ContentView.swift
├── {ExtensionName}/                       # Widget extension
│   ├── Assets.xcassets/
│   ├── Info.plist
│   ├── Settings.swift
│   ├── WidgetData.swift
│   ├── SharedStorage.swift
│   ├── SimpleEntry.swift
│   ├── Provider.swift
│   ├── SimpleWidgetView.swift
│   ├── SimpleWidget.swift
│   ├── SimpleWidgetBundle.swift
│   ├── Intents/
│   │   ├── ConfigurationAppIntent.swift
│   │   ├── IncrementCounterIntent.swift
│   │   └── DecrementCounterIntent.swift
│   └── Services/
│       └── SilentNotificationService.swift
├── {ExtensionName}.entitlements
└── build-release.sh
```

---

## xcodegen project.yml

Instead of creating the Xcode project manually, use [xcodegen](https://github.com/yonaskolb/XcodeGen) to generate it. Install with `brew install xcodegen`, create this `project.yml` in the `XCodeWidget/` directory, and run `xcodegen generate`.

```yaml
name: XCodeWidget
options:
  bundleIdPrefix: com.companyname
  deploymentTarget:
    iOS: "17.0"

targets:
  XCodeWidget:
    type: application
    platform: iOS
    sources:
      - XCodeWidget
    settings:
      base:
        PRODUCT_BUNDLE_IDENTIFIER: {AppBundleId}

  {ExtensionName}:
    type: app-extension
    platform: iOS
    sources:
      - {ExtensionName}
    settings:
      base:
        PRODUCT_BUNDLE_IDENTIFIER: {AppBundleId}.{ExtensionName}
        INFOPLIST_FILE: {ExtensionName}/Info.plist
        GENERATE_INFOPLIST_FILE: "NO"
        CODE_SIGN_ENTITLEMENTS: {ExtensionName}.entitlements
    entitlements:
      path: {ExtensionName}.entitlements
      properties:
        com.apple.security.application-groups:
          - "{GroupId}"
```

**Important notes:**
- The widget extension bundle ID must be a child of the app bundle ID
- Set `GENERATE_INFOPLIST_FILE: "NO"` because we provide our own Info.plist with the full NSExtension configuration
- After generating, build with `-target {ExtensionName}` (not `-scheme`) to avoid host app build issues
