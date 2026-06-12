using System;

namespace Maui.AppIntents;

/// <summary>
/// Execution surfaces an intent supports. Maps to the iOS 26 <c>IntentModes</c> /
/// <c>supportedModes</c> API. On iOS versions earlier than 26 the generated code
/// falls back to <c>openAppWhenRun</c> when <see cref="AppIntentExecutionModes.Foreground"/>
/// is requested.
/// </summary>
[Flags]
public enum AppIntentExecutionModes
{
    /// <summary>No explicit mode; the generated intent uses <c>openAppWhenRun</c> only.</summary>
    Default = 0,

    /// <summary>The intent can run in the foreground (bringing the app to the front).</summary>
    Foreground = 1,

    /// <summary>The intent can run in the background without launching the app UI.</summary>
    Background = 2,
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AppIntentAttribute : Attribute
{
    public AppIntentAttribute(string identifier)
    {
        Identifier = identifier;
    }

    public string Identifier { get; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public bool OpenAppWhenRun { get; set; }

    /// <summary>
    /// A parameter summary shown in Shortcuts. Supports <c>{ParameterName}</c> placeholders
    /// that are rendered as live parameter interpolations (for example
    /// <c>"Create {Title} with priority {Priority}"</c>). For conditional summaries use one or
    /// more <see cref="AppIntentSummaryAttribute"/> entries instead.
    /// </summary>
    public string? ParameterSummary { get; set; }

    /// <summary>
    /// The execution modes the intent supports. When set to anything other than
    /// <see cref="AppIntentExecutionModes.Default"/> the generator emits an iOS 26
    /// <c>supportedModes</c> declaration, and (for foreground modes) also sets
    /// <c>openAppWhenRun</c> so the behavior is preserved on earlier OS versions.
    /// </summary>
    public AppIntentExecutionModes SupportedModes { get; set; } = AppIntentExecutionModes.Default;

    /// <summary>
    /// When <c>true</c>, the generated <c>perform()</c> asks the system to confirm the action
    /// (via <c>requestConfirmation</c>) before invoking the C# handler. This is the common
    /// "confirm before doing X" flow. The confirmation prompt is gated to iOS 18+; on earlier
    /// versions the action proceeds without an explicit prompt.
    /// </summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    /// The dialog text shown when <see cref="RequiresConfirmation"/> is <c>true</c>. Defaults to
    /// the intent title when left null.
    /// </summary>
    public string? ConfirmationDialog { get; set; }

    /// <summary>
    /// The system action verb used for the confirmation button when
    /// <see cref="RequiresConfirmation"/> is <c>true</c>. Maps to Apple's
    /// <c>ConfirmationActionName</c>.
    /// </summary>
    public AppIntentConfirmationAction ConfirmationActionName { get; set; } = AppIntentConfirmationAction.Continue;
}

/// <summary>
/// System action verbs for an intent confirmation button. Maps to Apple's
/// <c>ConfirmationActionName</c> values used by <c>requestConfirmation(actionName:dialog:)</c>.
/// </summary>
public enum AppIntentConfirmationAction
{
    Continue = 0,
    Add = 1,
    Book = 2,
    Buy = 3,
    Call = 4,
    Create = 5,
    Do = 6,
    Download = 7,
    Go = 8,
    Open = 9,
    Order = 10,
    Pay = 11,
    Post = 12,
    Run = 13,
    Search = 14,
    Send = 15,
    Set = 16,
    Share = 17,
    Start = 18,
    Toggle = 19,
    TurnOff = 20,
    TurnOn = 21,
    View = 22,
}

/// <summary>
/// A conditional parameter summary. Apply multiple times to build a
/// <c>When(...) { } otherwise: { }</c> chain. The <see cref="Format"/> supports
/// <c>{ParameterName}</c> placeholders just like <see cref="AppIntentAttribute.ParameterSummary"/>.
/// An entry with no <see cref="WhenParameter"/> is the default/"otherwise" branch.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AppIntentSummaryAttribute : Attribute
{
    public AppIntentSummaryAttribute(string format)
    {
        Format = format;
    }

    /// <summary>The summary format, supporting <c>{ParameterName}</c> placeholders.</summary>
    public string Format { get; }

    /// <summary>The C# parameter (request property) name this condition tests.</summary>
    public string? WhenParameter { get; set; }

    /// <summary>
    /// The value the parameter must equal for this summary to apply. For enums use the C#
    /// case name; for booleans use <c>"true"</c>/<c>"false"</c>. Leave null for the default branch.
    /// </summary>
    public string? EqualsValue { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AppShortcutAttribute : Attribute
{
    public AppShortcutAttribute(string phrase)
    {
        Phrase = phrase;
    }

    public string Phrase { get; }

    public string? ShortTitle { get; set; }

    public string? SystemImageName { get; set; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class IntentParameterAttribute : Attribute
{
    public IntentParameterAttribute(string title)
    {
        Title = title;
    }

    public string Title { get; }

    public string? Name { get; set; }

    public bool IsOptional { get; set; }

    public double InclusiveMinimum { get; set; } = double.NaN;

    public double InclusiveMaximum { get; set; } = double.NaN;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class AppEntityAttribute : Attribute
{
    public AppEntityAttribute(string identifier)
    {
        Identifier = identifier;
    }

    public string Identifier { get; }

    public string? TypeDisplayName { get; set; }

    /// <summary>
    /// When true the generated Swift entity conforms to <c>IndexedEntity</c> (iOS 18+) so the
    /// system can index it in Spotlight. Properties flagged with
    /// <see cref="AppEntityPropertyAttribute.IndexingKey"/> are mapped into the searchable
    /// attribute set. The base entity type still targets the configured minimum OS via a gated
    /// extension.
    /// </summary>
    public bool Indexed { get; set; }

    /// <summary>
    /// When true the generated Swift entity conforms to <c>UniqueAppEntity</c> (iOS 18+) — a
    /// singleton entity (for example app settings) with a single <c>uniqueEntity()</c> resolved
    /// through the query bridge instead of a string query. Unique entities require iOS 18, so any
    /// intent that references one is emitted as iOS 18+ and cannot expose an App Shortcut.
    /// </summary>
    public bool Unique { get; set; }

    /// <summary>
    /// An optional deep-link URL template that makes the entity conform to
    /// <c>URLRepresentableEntity</c> (iOS 18+). Use <c>{id}</c> for the entity identifier and
    /// <c>{PropertyName}</c> for any <see cref="AppEntityPropertyAttribute"/> value, for example
    /// <c>"myapp://tasks/{id}"</c>.
    /// </summary>
    public string? UrlRepresentation { get; set; }
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class AppEntityIdentifierAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class AppEntityDisplayAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class AppEntitySubtitleAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class AppEntityPropertyAttribute : Attribute
{
    public AppEntityPropertyAttribute(string title)
    {
        Title = title;
    }

    public string Title { get; }

    public string? Name { get; set; }

    /// <summary>
    /// When the owning entity is <see cref="AppEntityAttribute.Indexed"/>, maps this property into
    /// the Spotlight searchable attribute set under the given key. Supported keys:
    /// <c>contentDescription</c>, <c>title</c>, and <c>keywords</c>.
    /// </summary>
    public string? IndexingKey { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AppEntityQueryHandlerAttribute : Attribute
{
    public AppEntityQueryHandlerAttribute(Type entityType)
    {
        EntityType = entityType;
    }

    public Type EntityType { get; }
}

[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class AppEnumAttribute : Attribute
{
    public AppEnumAttribute(string typeDisplayName)
    {
        TypeDisplayName = typeDisplayName;
    }

    public string TypeDisplayName { get; }

    /// <summary>
    /// An optional deep-link URL template that makes the generated Swift enum conform to
    /// <c>URLRepresentableEnum</c> (iOS 18+), for example <c>"myapp://priority"</c>.
    /// </summary>
    public string? UrlRepresentation { get; set; }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class AppEnumCaseAttribute : Attribute
{
    public AppEnumCaseAttribute(string title)
    {
        Title = title;
    }

    public string Title { get; }

    public string? Subtitle { get; set; }
}
