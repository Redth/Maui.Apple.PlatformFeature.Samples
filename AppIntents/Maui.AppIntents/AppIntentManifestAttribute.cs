using System;

namespace Maui.AppIntents;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AppIntentManifestAttribute : Attribute
{
    public AppIntentManifestAttribute(int index, int total, string json)
    {
        Index = index;
        Total = total;
        Json = json;
    }

    public int Index { get; }

    public int Total { get; }

    public string Json { get; }
}
