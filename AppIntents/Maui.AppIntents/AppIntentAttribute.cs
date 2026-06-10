using System;

namespace Maui.AppIntents;

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

    public string? ParameterSummary { get; set; }
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
