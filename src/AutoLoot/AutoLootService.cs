namespace AutoLoot;

using OriathHub;
using OriathHub.RemoteObjects.States;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathHub.Utils;
using OriathPlugins.Common.Loot;
using OriathPlugins.Common.Pricing;

internal sealed class AutoLootService
{
    private readonly List<GroundLootMarker> debugMarkers = [];
    private readonly ClickTracker clickTracker = new();
    private readonly LootPriceService prices = new();
    private DateTime lastPickupAttemptUtc = DateTime.MinValue;
    private IReadOnlyList<GroundLootCandidate> lastCandidates = Array.Empty<GroundLootCandidate>();
    private GroundLootScanDiagnostics lastDiagnostics = new();
    private DateTime lastFilterWarningUtc = DateTime.MinValue;
    private uint? lastTargetEntityId;
    private bool pricesInitialized;

    public int PickupAttempts { get; private set; }

    public string StatusMessage { get; private set; } = "Idle";

    public string PriceStatusMessage => prices.StatusMessage;

    public IReadOnlyList<GroundLootCandidate> LastCandidates => lastCandidates;

    public IReadOnlyList<GroundLootMarker> DebugMarkers => debugMarkers;

    public GroundLootScanDiagnostics LastDiagnostics => lastDiagnostics;

    public void Initialize(string dllDirectory, string pluginsRoot, AutoLootSettings settings)
    {
        if (pricesInitialized)
        {
            return;
        }

        prices.Initialize(dllDirectory);
        prices.Reload(pluginsRoot, settings.NinjaLeague);
        pricesInitialized = true;
    }

    public void ReloadPrices(string pluginsRoot, string league) => prices.Reload(pluginsRoot, league);

    public void ResetStats()
    {
        PickupAttempts = 0;
        lastPickupAttemptUtc = DateTime.MinValue;
        lastCandidates = Array.Empty<GroundLootCandidate>();
        lastDiagnostics = new GroundLootScanDiagnostics();
        debugMarkers.Clear();
        clickTracker.Reset();
        lastTargetEntityId = null;
        StatusMessage = "Idle";
    }

    public void ProcessFrame(
        InGameState inGame,
        AreaInstance area,
        bool isTown,
        bool isHideout,
        AutoLootSettings settings)
    {
        clickTracker.UpdateFrame(area);

        if (!settings.Enabled)
        {
            StatusMessage = "Disabled";
            lastCandidates = Array.Empty<GroundLootCandidate>();
            debugMarkers.Clear();
            return;
        }

        if (settings.PauseInTown && isTown)
        {
            StatusMessage = "Paused in town";
            lastCandidates = Array.Empty<GroundLootCandidate>();
            debugMarkers.Clear();
            return;
        }

        if (settings.PauseInHideout && isHideout)
        {
            StatusMessage = "Paused in hideout";
            lastCandidates = Array.Empty<GroundLootCandidate>();
            debugMarkers.Clear();
            return;
        }

        if (!PickupSafety.CanPickup(inGame, settings))
        {
            StatusMessage = PickupSafety.GetPauseReason(inGame, settings);
            lastCandidates = Array.Empty<GroundLootCandidate>();
            debugMarkers.Clear();
            return;
        }

        var scanSettings = BuildScanSettings(settings);
        if (clickTracker.ShouldWaitForPickup())
        {
            StatusMessage = "Waiting for pickup";
            var waitDiagnostics = new GroundLootScanDiagnostics();
            lastCandidates = GroundLootScanner.Scan(
                inGame,
                area,
                scanSettings,
                prices,
                clickTracker.IsIgnored,
                waitDiagnostics,
                settings.ShowDebugOverlay ? debugMarkers : null);
            lastDiagnostics = waitDiagnostics;
            return;
        }

        var diagnostics = new GroundLootScanDiagnostics();
        if (!settings.ShowDebugOverlay)
        {
            debugMarkers.Clear();
        }

        lastCandidates = GroundLootScanner.Scan(
            inGame,
            area,
            scanSettings,
            prices,
            clickTracker.IsIgnored,
            diagnostics,
            settings.ShowDebugOverlay ? debugMarkers : null);
        lastDiagnostics = diagnostics;

        if (lastCandidates.Count == 0)
        {
            StatusMessage = diagnostics.GroundEntities == 0
                ? $"No ground loot seen ({diagnostics.AwakeEntities} awake)"
                : diagnostics.FilteredByPath == diagnostics.GroundEntities
                    ? $"All loot filtered ({diagnostics.GroundEntities} ground)"
                    : $"No loot in range ({diagnostics.GroundEntities} ground, {diagnostics.OutOfRange} far)";
            return;
        }

        var target = PickNextTarget(lastCandidates);
        if (target == default || !target.HasScreenPosition)
        {
            StatusMessage = $"Loot in range but off-screen ({lastCandidates.Count})";
            return;
        }

        var cooldownMs = Math.Max(settings.MinMsBetweenPickups, 30);
        if ((DateTime.UtcNow - lastPickupAttemptUtc).TotalMilliseconds < cooldownMs)
        {
            StatusMessage = $"Waiting ({lastDiagnostics.Clickable} clickable)";
            return;
        }

        var screen = ScreenPositionResolver.ClientToScreen(target.ClientPosition);
        var clicked = GameMouseInput.TryClick(
            (int)Math.Round(screen.X),
            (int)Math.Round(screen.Y),
            settings.ClickHoldMs);
        if (!clicked)
        {
            StatusMessage = "Click failed";
            return;
        }

        lastPickupAttemptUtc = DateTime.UtcNow;
        PickupAttempts++;
        lastTargetEntityId = target.EntityId;
        clickTracker.BeginPickup(
            target.EntityId,
            (int)Math.Round(target.ClientPosition.X),
            (int)Math.Round(target.ClientPosition.Y),
            GroundLootRules.IsWorldItemPath(target.ItemPath));

        if (settings.EnableDebugLogging)
        {
            Log.Info(
                $"click {target.DisplayName} @ ({screen.X:0},{screen.Y:0}) value={target.DivineValue:0.###} dist={target.Distance:0.#}",
                "Auto Loot");
        }

        StatusMessage = target.DivineValue > 0
            ? $"Clicked {target.DisplayName} ({target.DivineValue:0.##}e)"
            : $"Clicked {target.DisplayName}";
    }

    private static GroundLootScanSettings BuildScanSettings(AutoLootSettings settings) => new()
    {
        StackablesOnly = settings.LootStackablesOnly,
        UseValueFilter = settings.UseValueFilter,
        MinDivineValue = settings.MinDivineValue,
        PickupDistance = settings.PickupDistance,
        AlwaysPickupWaystonesAndTablets = settings.AlwaysPickupWaystonesAndTablets,
    };

    private GroundLootCandidate PickNextTarget(IReadOnlyList<GroundLootCandidate> candidates)
    {
        GroundLootCandidate? alternate = null;
        GroundLootCandidate? fallback = null;

        foreach (var candidate in candidates)
        {
            if (!candidate.HasScreenPosition)
            {
                continue;
            }

            fallback ??= candidate;
            if (lastTargetEntityId.HasValue && candidate.EntityId == lastTargetEntityId.Value)
            {
                continue;
            }

            alternate ??= candidate;
        }

        return alternate ?? fallback ?? default;
    }
}
