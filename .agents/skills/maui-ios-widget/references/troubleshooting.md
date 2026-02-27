# Troubleshooting iOS Widget Extensions with .NET MAUI

Common issues, their causes, and solutions. Organized from most-common to least-common.

## Build Failures

### "Could not find any available provisioning profiles"

**Cause:** The app's Bundle ID doesn't have a provisioning profile configured in the Apple Developer Console, or the profile isn't installed on the build machine.

**Fix:**
1. Go to [Apple Developer Console](https://developer.apple.com/account) → Certificates, Identifiers & Profiles
2. Ensure you have an App ID for both the main app AND the widget extension
3. Both must have the "App Groups" capability enabled with the same Group ID
4. Create/download provisioning profiles for both
5. Install them on the build machine (double-click the `.mobileprovision` files)

### "Error reading entitlements" / Build fails during code signing

**Cause:** The entitlements plist files have CRLF (Windows) line endings instead of LF (Unix).

**Fix:**
```bash
sed -i '' 's/\r$//' Platforms/iOS/Entitlements.plist
sed -i '' 's/\r$//' Platforms/iOS/Entitlements.WidgetExtension.plist
```

This is one of the most common and frustrating issues — the error message doesn't mention line endings at all.

### "Entitlements could not be found"

**Cause:** The entitlements file isn't being copied to the build output.

**Fix:** Ensure this line is in the `.csproj`:
```xml
<Content Include=".\Platforms\iOS\Entitlements.WidgetExtension.plist"
         CopyToOutputDirectory="PreserveNewest" />
```

### Widget extension not embedded in the app bundle

**Cause:** The `AdditionalAppExtensions` paths don't match the actual file locations, or the `.appex` hasn't been built/copied yet.

**Fix:**
1. If using the `BuildWidgetExtension` MSBuild target (recommended), make sure the `XCodeWidget/` directory exists with either an `.xcodeproj` or a `project.yml`. The target runs automatically during `dotnet build` and stages the `.appex` for you.
2. If building manually, verify the `.appex` folders exist at `Platforms/iOS/WidgetExtensions/Release-iphoneos/{Name}.appex/` and `Release-iphonesimulator/{Name}.appex/`
3. Check that the `<Name>` element in `AdditionalAppExtensions` matches the `.appex` filename (without the `.appex` suffix)
4. Check that `<BuildOutput>` paths match the actual folder names (`Release-iphoneos`, `Release-iphonesimulator`)
5. Paths are case-sensitive
6. To skip the auto-build and use a manually built `.appex`: `dotnet build ... -p:SkipWidgetBuild=true`

### NuGet package version mismatch

**Cause:** `WidgetKit.WidgetCenterProxy` version doesn't match the .NET TFM.

**Fix:** Check [NuGet.org](https://www.nuget.org/packages/WidgetKit.WidgetCenterProxy) for available versions. Use a version that supports your target framework (e.g., 9.0.x for net9.0-ios).

---

## Runtime Issues

### Widget doesn't appear in the widget gallery

**Possible causes:**
1. The `.appex` wasn't properly embedded. Rebuild the MAUI app and check the `.app` bundle contents.
2. The `NSExtensionPrincipalClass` in the widget's `Info.plist` doesn't match the actual `@main` struct name.
3. The app hasn't been launched at least once (iOS requires this before showing widgets).

**Fix:** Launch the app once, then check the widget gallery. If it's still missing, inspect the `.app` bundle:
```bash
ls -la bin/Debug/net10.0-ios/iossimulator-arm64/YourApp.app/PlugIns/
```
You should see the `.appex` folder here.

### Widget shows "Unable to Load" or stays blank

**Possible causes:**
1. The widget extension is crashing. Check Xcode console or device logs.
2. The `Info.plist` `NSExtensionPrincipalClass` format is wrong. It must be `{ModuleName}.{BundleStructName}`.
3. The minimum iOS version doesn't match between the MAUI app and widget.

**Fix:** In Xcode, set a breakpoint in the Provider and run the widget extension target directly to debug.

### Shared data not syncing between app and widget

**Most likely cause:** Using `UserDefaults(suiteName:)` for cross-process communication. This is **unreliable** — `UserDefaults` can resolve to different plist files for the MAUI app process vs. the widget extension process. The app writes to the App Group container (`Containers/Shared/AppGroup/.../Library/Preferences/`), but the widget extension writes to the system-level preferences (`Library/Preferences/`). They appear to be using the same `suiteName`, but the backing store is different.

**The fix:** Use **file-based I/O** via `NSFileManager.GetContainerUrl()` (C#) / `FileManager.containerURL(forSecurityApplicationGroupIdentifier:)` (Swift). Write JSON files directly to the shared container directory. See the updated templates in `csharp-templates.md` and `swift-templates.md`.

**Other possible causes:**
1. **App Group ID mismatch** — The Group ID must be identical in:
   - `WidgetConstants.cs` (C#)
   - `Settings.swift` (Swift)
   - `Entitlements.plist` (MAUI app)
   - `Entitlements.WidgetExtension.plist` (widget)
   - Apple Developer Console capability
2. **JSON serialization mismatch** — Property names in C# `[JsonPropertyName]` must match Swift property names
3. **Missing re-signing step** — When building with `CodesignRequireProvisioningProfile=false`, the MAUI build generates an empty `.xcent` file, stripping the App Group entitlement. Without the entitlement, `NSFileManager.GetContainerUrl()` returns null. Re-sign the app after building:
   ```bash
   /usr/bin/codesign -v --force --timestamp=none --sign - \
     --entitlements Platforms/iOS/Entitlements.plist "$APP_PATH"
   ```

**Debug approach:**
- Check if `NSFileManager.GetContainerUrl()` returns null — if so, the app isn't re-signed with entitlements
- Check the shared container on the simulator filesystem: `~/Library/Developer/CoreSimulator/Devices/<UUID>/data/Containers/Shared/AppGroup/`
- Use `os.log` Logger on the Swift side for debugging (visible via `xcrun simctl spawn <device> log show --predicate '...'`)
- Check for separate plist files at both `Containers/Shared/AppGroup/.../Library/Preferences/` and `data/Library/Preferences/` — if both exist, UserDefaults is writing to different locations

### Widget not refreshing after app data changes

**Possible causes:**
1. `WidgetKit.WidgetCenterProxy.ReloadTimeLinesOfKind` not being called
2. The `kind` string doesn't match between C# and Swift
3. iOS is throttling refreshes (happens if called too frequently)

**Fix:** Verify the `kind` matches. In development, throttling is rarely an issue. If unsure, try `ReloadAllTimeLines()` temporarily (but switch back to `ReloadTimeLinesOfKind` for production).

### Widget buttons don't work (counter doesn't change on tap)

**Possible causes:**
1. **`widgetURL()` intercepting button taps** — If `.widgetURL()` is applied to a parent container that wraps `Button(intent:)`, the URL handler can intercept taps before the button intent fires. **Fix:** Use `Link(destination:)` on individual non-interactive areas instead of `.widgetURL()` on the entire view.
2. **Priority logic overrides widget data** — If `getBestCounter()` always prefers app data, a widget increment (e.g., 10→11) gets overridden on the next timeline reload when app data (counter=10) takes priority. **Fix:** Compare `updatedAt` timestamps and use whichever source was updated most recently.
3. **Widget extension can't write to the container** — The widget's `FileManager.containerURL()` returns nil. Check entitlements.
4. **Old cached widget binary** — After reinstalling the app, the widget might still run a cached version. Remove the widget from the home screen and re-add it.

### Deep link not working (tapping widget doesn't pass data to app)

**Possible causes:**
1. URL scheme not registered in `Info.plist` (`CFBundleURLTypes`)
2. `AppDelegate.OpenUrl` not overridden
3. URL scheme string doesn't match between `WidgetConstants.UrlScheme` and `Settings.urlScheme`
4. `widgetURL()` not set on the widget view (or set with an empty string)

**Fix:** Check all four. Test the URL scheme independently:
```bash
xcrun simctl openurl booted "yourscheme://widget?counter=5"
```

### Widget shows wrong icon

**Cause:** iOS aggressively caches widget icons.

**Fix:**
1. Ensure AppIcon images are in the widget extension's `Assets.xcassets/AppIcon.appiconset/`
2. Reference them in the widget's `Info.plist` (see the `CFBundleIcons` section)
3. **Reboot the test device/simulator** — this clears the icon cache
4. Delete the app and reinstall

## Simulator-Specific Issues

### App entitlements are empty after building with CodesignRequireProvisioningProfile=false

**Cause:** The MAUI build system generates an empty `.xcent` file when `CodesignRequireProvisioningProfile=false` is set. This strips the App Group entitlement from the main app binary, causing `NSFileManager.GetContainerUrl()` to return null and breaking all cross-process communication.

**Fix:** After building, re-sign the app with the correct entitlements:
```bash
APP_PATH=$(find bin/Debug/net10.0-ios/iossimulator-arm64 -name "*.app" -maxdepth 1)
/usr/bin/codesign -v --force --timestamp=none --sign - \
  --entitlements Platforms/iOS/Entitlements.plist "$APP_PATH"
```

Verify entitlements are embedded:
```bash
codesign -d --entitlements - "$APP_PATH"
```

### "Mismatched bundle IDs" when installing on simulator

**Cause:** The widget extension's bundle ID is not a child of the app's bundle ID. For example, the app is `com.companyname.myapp` but the widget is `com.myapp.WidgetExtension`.

**Fix:** The widget bundle ID must be a child of the app bundle ID:
- App: `com.companyname.myapp`
- Widget: `com.companyname.myapp.WidgetExtension` ✓
- Widget: `com.myapp.WidgetExtension` ✗

### Widget extension build fails with AppIntentsSSUTraining error

**Cause:** The widget extension's `Info.plist` is missing full CFBundle keys. When `GENERATE_INFOPLIST_FILE` is `false`, you need all standard keys.

**Fix:** Ensure Info.plist includes at minimum: `CFBundleDevelopmentRegion`, `CFBundleExecutable`, `CFBundleIdentifier`, `CFBundleInfoDictionaryVersion`, `CFBundleName`, `CFBundlePackageType` (XPC!), `CFBundleShortVersionString`, `CFBundleVersion`, plus the `NSExtension` dict.

### Building with `-scheme` fails but `-target` works

**Cause:** Building with `-scheme` tries to build the host app too, which may have an incomplete `Info.plist` (especially if using xcodegen with a minimal host app).

**Fix:** Build with `-target SimpleWidgetExtension` instead of `-scheme`:
```bash
xcodebuild -project XCodeWidget.xcodeproj \
  -target SimpleWidgetExtension \
  -configuration Release \
  -sdk iphonesimulator -arch arm64 \
  CODE_SIGN_IDENTITY="-" CODE_SIGNING_REQUIRED=NO CODE_SIGNING_ALLOWED=NO \
  BUILD_DIR=$(pwd)/build clean build
```

---

## Using xcodegen

Instead of creating the Xcode project manually, you can use [xcodegen](https://github.com/yonaskolb/XcodeGen) to generate it from a `project.yml` file. This is faster and more reproducible. Install with `brew install xcodegen`, create `project.yml` in the `XCodeWidget/` directory, and run `xcodegen generate`. See `references/project-config.md` for a sample `project.yml`.

---

### "No such module 'WidgetKit'" or "No such module 'AppIntents'"

**Cause:** The widget extension target's minimum deployment is set too low. WidgetKit interactive features require iOS 17+.

**Fix:** Set the minimum deployment target to iOS 17.0 for the widget extension (and the host app) in Xcode.

### Build succeeds in Xcode but xcodebuild fails

**Possible causes:**
1. Scheme name doesn't match the target name
2. Signing issues in command-line builds

**Fix:** List available schemes:
```bash
xcodebuild -project XCodeWidget.xcodeproj -list
```
Use the exact scheme name in `build-release.sh`.

### Swift files not included in the widget target

**Cause:** When adding files in Xcode, they weren't added to the correct target membership.

**Fix:** In Xcode, select each `.swift` file → File Inspector (right panel) → Target Membership → check the widget extension target.

---

## Performance Notes

- **WidgetKit throttling:** Apple limits widget refresh frequency. In development, refreshes are usually instant. In production, iOS may delay refreshes if you call them too often. Use `.never` timeline policy and manual refreshes for the most control.
- **Memory limits:** Widgets have strict memory limits (~30MB). Keep data small, avoid loading large images, and don't do heavy computation in the Provider.
- **Background execution:** AppIntents get a brief window for async work (~30 seconds). Use it for quick network calls, not long-running tasks.
- **Simulator vs. device:** Widgets sometimes behave differently on simulators (especially with icon caching and refresh timing). Always test on a real device before shipping.
