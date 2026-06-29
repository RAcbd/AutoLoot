namespace AutoLoot;

using System.Numerics;

public sealed class AutoLootSettings
{
    public bool Enabled;

    public bool PauseInTown = true;

    public bool PauseInHideout = true;

    public bool RequireGameForeground = true;

    public bool PauseWhenPanelsOpen = true;

    public bool PauseWhenChatOpen = true;

    public float PickupDistance = 600f;

    public int MinMsBetweenPickups;

    public int ClickHoldMs = 10;

    public bool LootStackablesOnly
    {
        get => CurrencyOnly;
        set => CurrencyOnly = value;
    }

    public bool CurrencyOnly = true;

    public bool UseValueFilter;

    public double MinDivineValue;

    public bool AlwaysPickupWaystonesAndTablets = true;

    /// <summary>One entry per line in settings UI — matched against item paths and display names.</summary>
    public List<string> PickupWhitelist = [];

    /// <summary>One entry per line in settings UI — matched against item paths and display names.</summary>
    public List<string> PickupBlacklist = [];

    public bool EnableDebugLogging;

    public bool ShowDebugOverlay;

    public Vector4 AccentColor = new(0.85f, 0.72f, 0.25f, 1f);
}
