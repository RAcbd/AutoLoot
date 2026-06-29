namespace OriathPlugins.Common.Loot;

using System.Numerics;

public enum GroundLootMarkerKind
{
    Clickable,
    Filtered,
    OutOfRange,
    NoScreen,
    Valuable,
}

public readonly record struct GroundLootMarker(
    Vector2 ClientPosition,
    GroundLootMarkerKind Kind,
    string Label,
    string ItemPath,
    double DivineValue);

public readonly record struct GroundLootCandidate(
    uint EntityId,
    string ItemPath,
    string DisplayName,
    float Distance,
    Vector2 ClientPosition,
    bool HasScreenPosition,
    double DivineValue,
    int PickupPriority);

/// <summary>Loot type visible on the ground for whitelist/blacklist UI.</summary>
public readonly record struct GroundLootFilterOption(string Key, string DisplayName, string ItemPath);

public sealed class GroundLootScanSettings
{
    public bool StackablesOnly
    {
        get => CurrencyOnly;
        set => CurrencyOnly = value;
    }

    public bool CurrencyOnly = true;

    public bool UseValueFilter;

    public double MinDivineValue;

    public float PickupDistance = 600f;

    public bool AlwaysPickupWaystonesAndTablets = true;

    /// <summary>Path or display-name substrings that are always picked up (even when currency-only).</summary>
    public IReadOnlyList<string> PickupWhitelist = Array.Empty<string>();

    /// <summary>Path or display-name substrings that are never picked up.</summary>
    public IReadOnlyList<string> PickupBlacklist = Array.Empty<string>();
}

public sealed class GroundLootScanDiagnostics
{
    public int AwakeEntities;

    public int GroundEntities;

    public int FilteredByPath;

    public int OutOfRange;

    public int MissingScreen;

    public int Clickable;
}
