.PHONY: all sim clean help

MAUI_PROJECT = src/MauiAppIntentsSample/MauiAppIntentsSample.csproj
TFM = net10.0-ios

# Default target: build everything (Swift framework built automatically via XcodeProject)
all:
	@echo "=== Building .NET MAUI App (Swift framework builds automatically) ==="
	dotnet build $(MAUI_PROJECT) -f $(TFM)

# Build for simulator (skips code signing entitlements)
sim:
	@echo "=== Building .NET MAUI App for Simulator ==="
	dotnet build $(MAUI_PROJECT) -f $(TFM) -r iossimulator-arm64 -p:CodesignEntitlements=""

# Clean all build artifacts
clean:
	@echo "=== Cleaning ==="
	dotnet clean $(MAUI_PROJECT) -f $(TFM) 2>/dev/null || true
	@echo "=== Clean complete ==="

help:
	@echo "Available targets:"
	@echo "  all   - Build everything (dotnet build handles Swift + binding + MAUI)"
	@echo "  sim   - Build for iOS Simulator (no code signing)"
	@echo "  clean - Clean all build artifacts"
	@echo ""
	@echo "The Swift framework is built automatically via <XcodeProject> in the"
	@echo "binding project — no separate build step needed."
	@echo ""
	@echo "Prerequisites:"
	@echo "  - .NET 10+ SDK with MAUI workload"
	@echo "  - Xcode 16+ with iOS 17+ SDK"
	@echo "  - macOS (required for iOS builds)"
