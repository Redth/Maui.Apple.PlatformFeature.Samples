using System;

namespace Maui.AppIntents;

/// <summary>
/// Represents a file passed into an App Intent from another app, the share sheet, or the Files app.
/// Use it as an <c>[IntentParameter]</c> property type on an intent request to receive file content.
/// The generator maps this to Apple's <c>IntentFile</c> parameter type; the file bytes are bridged
/// to C# as a base64 payload.
/// </summary>
public sealed class AppIntentFile
{
    /// <summary>
    /// The raw file bytes. Bound from the bridged base64 payload.
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// The file name, including extension when available.
    /// </summary>
    public string FileName { get; set; } = "";

    /// <summary>
    /// The uniform type identifier (UTType) of the file, when the system provides one (e.g. <c>public.plain-text</c>).
    /// </summary>
    public string? ContentType { get; set; }
}
