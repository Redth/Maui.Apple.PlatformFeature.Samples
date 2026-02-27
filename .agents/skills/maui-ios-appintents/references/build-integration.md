# Build Integration

Build configuration for the Xcode project → .NET binding → MAUI app pipeline using Native Library Interop.

## Table of Contents
1. [Xcode Project Setup](#xcode-project-setup)
2. [Binding Project with XcodeProject](#binding-project-with-xcodeproject)
3. [Build Order](#build-order)
4. [Makefile (Optional)](#makefile-optional)
5. [Troubleshooting Build Issues](#troubleshooting-build-issues)

---

## Xcode Project Setup

Create an Xcode Framework project (`.xcodeproj`) for the App Intents. This replaces the old Swift Package approach and integrates directly with the .NET build via `<XcodeProject>`.

### Creating the Xcode Project

1. In Xcode: File → New → Project → Framework (iOS)
2. Name it `{FrameworkName}` (e.g., `MauiAppIntentsSampleIntents`)
3. Set deployment target to iOS 17.0
4. Add all Swift source files to the project
5. Link the `AppIntents` framework (Targets → General → Frameworks and Libraries)

### Critical Build Settings

Set these in the Xcode project's build settings (Targets → Build Settings):

| Setting | Value | Why |
|---------|-------|-----|
| `SWIFT_REFLECTION_METADATA_LEVEL` | `all` | Required for `appintentsmetadataprocessor` to find AppIntent types |
| `SWIFT_INSTALL_OBJC_HEADER` | `YES` | Generates the `-Swift.h` header for ObjC interop |
| `SKIP_INSTALL` | `NO` | Ensures the framework is placed in a discoverable location for archiving |
| `DEFINES_MODULE` | `YES` | Required for framework modules |

**Note:** `BUILD_LIBRARY_FOR_DISTRIBUTION=YES` is automatically passed by the `<XcodeProject>` MSBuild integration — you don't need to set it in the Xcode project.

---

## Binding Project with XcodeProject

The binding project uses `<XcodeProject>` to automatically build the Xcode project during `dotnet build`. This eliminates the need for shell scripts or Makefiles.

### Binding .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-ios</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>true</ImplicitUsings>
    <IsBindingProject>true</IsBindingProject>
  </PropertyGroup>

  <ItemGroup>
    <ObjcBindingApiDefinition Include="ApiDefinition.cs" />
    <ObjcBindingCoreSource Include="StructsAndEnums.cs" />
  </ItemGroup>

  <!-- Build the Xcode project automatically via Native Library Interop -->
  <ItemGroup>
    <XcodeProject Include="../{AppName}.AppIntents/{FrameworkName}.xcodeproj">
      <SchemeName>{FrameworkName}</SchemeName>
      <ForceLoad>true</ForceLoad>
      <SmartLink>false</SmartLink>
    </XcodeProject>
  </ItemGroup>

  <!-- Extract App Intents Metadata from the xcarchive after the Xcode project builds -->
  <Target Name="ExtractAppIntentsMetadata" AfterTargets="_BuildXcodeProjects">
    <PropertyGroup>
      <_MetadataOutputDir>$(IntermediateOutputPath)xcode/Metadata.appintents</_MetadataOutputDir>
      <_XcodeOutputDir>$(IntermediateOutputPath)xcode</_XcodeOutputDir>
    </PropertyGroup>
    <Exec Command="rm -rf &quot;$(_MetadataOutputDir)&quot;" Condition="Exists('$(_MetadataOutputDir)')" />
    <Exec Command="METADATA_SRC=%24(find &quot;$(_XcodeOutputDir)&quot; -path &apos;*/archives/*Metadata.appintents&apos; -type d | head -1) &amp;&amp; if [ -n &quot;%24METADATA_SRC&quot; ]; then cp -R &quot;%24METADATA_SRC&quot; &quot;$(_MetadataOutputDir)&quot; &amp;&amp; echo &quot;Copied Metadata.appintents&quot;; else echo &quot;WARNING: Metadata.appintents not found&quot;; fi" />
  </Target>
</Project>
```

**Key settings on the `<XcodeProject>` item:**
- `ForceLoad=true` ensures the framework's ObjC classes are loaded even if not directly referenced — App Intents metadata types need this.
- `SmartLink=false` prevents the linker from stripping "unused" symbols that App Intents actually needs.
- The `<XcodeProject>` MSBuild item automatically: builds xcarchives for device + simulator, creates xcframework, adds it as `NativeReference`.

**The `ExtractAppIntentsMetadata` target** finds `Metadata.appintents` in the xcarchive output and copies it to a known location (`obj/.../xcode/Metadata.appintents`). The MAUI project then copies this into the app bundle.

---

## Build Order

The pipeline is now fully automated by MSBuild:

```
dotnet build (MAUI app)
  └─ ProjectReference → Binding project
       └─ <XcodeProject> → xcodebuild archive (device)
       └─ <XcodeProject> → xcodebuild archive (simulator)
       └─ CreateXcFramework → xcframework
       └─ ExtractAppIntentsMetadata → Metadata.appintents
  └─ CopyAppIntentsMetadata → app bundle
```

Just run `dotnet build` — everything happens automatically in the correct order.

---

## Makefile (Optional)

A Makefile can still serve as a convenience wrapper:

```makefile
.PHONY: all sim clean help

MAUI_PROJECT = src/{AppName}/{AppName}.csproj
TFM = net10.0-ios

all:
	dotnet build $(MAUI_PROJECT) -f $(TFM)

sim:
	dotnet build $(MAUI_PROJECT) -f $(TFM) -r iossimulator-arm64 -p:CodesignEntitlements=""

clean:
	dotnet clean $(MAUI_PROJECT) -f $(TFM) 2>/dev/null || true

help:
	@echo "Targets:"
	@echo "  all   - Build everything (dotnet build handles Swift + binding + MAUI)"
	@echo "  sim   - Build for iOS Simulator (no code signing)"
	@echo "  clean - Clean all build artifacts"
```

### Usage

```bash
# Full build
dotnet build src/{AppName}/{AppName}.csproj -f net10.0-ios

# Simulator build
dotnet build src/{AppName}/{AppName}.csproj -f net10.0-ios -r iossimulator-arm64 -p:CodesignEntitlements=""

# Clean
dotnet clean src/{AppName}/{AppName}.csproj -f net10.0-ios
```

---

## Troubleshooting Build Issues

### "Undefined symbols" at link time

**Symptom:** `Undefined symbol: _OBJC_CLASS_$_BridgeTaskItem`

**Cause:** Swift class names are mangled. Without `@objc(BridgeTaskItem)`, the actual symbol is `_OBJC_CLASS_$__TtC27FrameworkName14BridgeTaskItem`.

**Fix:** Add `@objc(ClassName)` annotation to every Swift class and protocol that crosses the bridge.

### Metadata.appintents not found / intents not appearing in Siri

**Symptom:** App builds fine, but Siri doesn't respond to phrases and intents don't show in Shortcuts app.

**Cause (most common):** `Metadata.appintents` isn't in the app bundle. Check by inspecting the built .app:

```bash
ls -la /path/to/{AppName}.app/Metadata.appintents/
```

**Fix:** Ensure both MSBuild targets exist:
1. `ExtractAppIntentsMetadata` in the binding `.csproj` (finds metadata in xcarchive output)
2. `CopyAppIntentsMetadata` in the MAUI `.csproj` (copies from binding output to app bundle)

### "SWIFT_REFLECTION_METADATA_LEVEL" warning or metadata is empty

**Symptom:** `Metadata.appintents` exists but is empty or only contains `version.json`.

**Cause:** `SWIFT_REFLECTION_METADATA_LEVEL=all` wasn't set in the Xcode project's build settings.

**Fix:** Set this in the Xcode project (Targets → Build Settings → Swift Compiler - General).

### Code signing errors when building for simulator

**Symptom:** `error: Signing requires a development team` or similar.

**Fix:** Add `-r iossimulator-arm64 -p:CodesignEntitlements=""` to skip entitlements on simulator builds. For device builds, ensure a valid provisioning profile with the Siri entitlement is configured.

### App Intents not appearing in Shortcuts app on simulator

**Symptom:** App runs, bridge logs "wired up successfully", but Shortcuts app doesn't show the intents.

**Cause:** `Metadata.appintents` is not inside the `.app` bundle. Verify:
```bash
ls -la path/to/{AppName}.app/Metadata.appintents/
```

If the directory exists but is _next to_ the `.app` instead of _inside_ it (e.g., `{AppName}.appMetadata.appintents`), the MSBuild `DestinationFolder` path is wrong — the `$(AppBundleDir)` property doesn't end with `/`, causing concatenation without a separator.

**Fix:** Ensure the MSBuild target uses `$(AppBundleDir)/Metadata.appintents/%(RecursiveDir)` with an explicit `/`.

### Binding nullable type errors

**Symptom:** Compiler errors about nullable reference types in ApiDefinition.cs.

**Cause:** The binding code generator doesn't fully support C# nullable reference type annotations.

**Fix:** Never use `?` on reference types in ApiDefinition.cs. Instead use:
```csharp
// Wrong:
string? Notes { get; set; }

// Right:
[NullAllowed]
string Notes { get; set; }

// For return types:
[return: NullAllowed]
BridgeTaskItem Get(string id);

// For parameters:
void Create(string name, [NullAllowed] NSDate date);
```
