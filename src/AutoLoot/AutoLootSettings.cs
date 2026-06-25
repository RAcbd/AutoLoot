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

    public int MinMsBetweenPickups = 50;

    public int ClickHoldMs = 12;

    public bool LootStackablesOnly = true;

    public bool UseValueFilter;

    public double MinDivineValue;

    public bool AlwaysPickupWaystonesAndTablets = true;

    public string NinjaLeague = "Runes of Aldur";

    public bool EnableDebugLogging;

    public bool ShowDebugOverlay;

    public Vector4 AccentColor = new(0.85f, 0.72f, 0.25f, 1f);
}
