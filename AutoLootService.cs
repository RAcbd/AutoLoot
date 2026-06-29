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
    private readonly GroundLootEntityCache groundLootCache = new();
    private readonly LootPriceService prices = new();
    private DateTime lastPickupAttemptUtc = DateTime.MinValue;
    private IReadOnlyList<GroundLootCandidate> lastCandidates = Array.Empty<GroundLootCandidate>();
    private GroundLootScanDiagnostics lastDiagnostics = new();
    private bool pricesInitialized;
    private int debugOverlayFrame;
    private uint? stickyTargetId;

    public int PickupAttempts { get; private set; }

    public int CachedGroundLootCount => groundLootCache.Count;

    public string StatusMessage { get; private set; } = "Idle";

    public string PriceStatusMessage => prices.StatusMessage;

    public IReadOnlyList<GroundLootCandidate> LastCandidates => lastCandidates;

    public IReadOnlyList<GroundLootMarker> DebugMarkers => debugMarkers;

    public GroundLootScanDiagnostics LastDiagnostics => lastDiagnostics;

    public void Initialize(string dllDirectory)
    {
        if (pricesInitialized)
        {
            return;
        }

        prices.Initialize(dllDirectory);
        prices.RefreshPrices();
        pricesInitialized = true;
    }

    public void ReloadPrices() => prices.RefreshPrices();

    public void OnAreaChanged() => groundLootCache.Reset();

    public void ResetStats()
    {
        PickupAttempts = 0;
        lastPickupAttemptUtc = DateTime.MinValue;
        lastCandidates = Array.Empty<GroundLootCandidate>();
        lastDiagnostics = new GroundLootScanDiagnostics();
        debugMarkers.Clear();
        clickTracker.Reset();
        groundLootCache.Reset();
        stickyTargetId = null;
        StatusMessage = "Idle";
    }

    public void ProcessFrame(
        InGameState inGame,
        AreaInstance area,
        string areaId,
        bool isTown,
        bool isHideout,
        AutoLootSettings settings)
    {
        clickTracker.UpdateFrame(area, groundLootCache);
        groundLootCache.UpdateFrame(area, areaId);

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
        var priceService = settings.UseValueFilter ? prices : null;
        var groundLoot = groundLootCache.Entities;
        if (clickTracker.IsInSuccessCooldown())
        {
            StatusMessage = "Waiting for pickup";
            return;
        }

        var diagnostics = new GroundLootScanDiagnostics();
        if (!settings.ShowDebugOverlay)
        {
            debugMarkers.Clear();
        }
        else if (++debugOverlayFrame % 4 == 0)
        {
            GroundLootScanner.Scan(
                inGame,
                area,
                groundLoot,
                scanSettings,
                priceService,
                clickTracker.IsIgnored,
                new GroundLootScanDiagnostics(),
                debugMarkers);
        }

        GroundLootCandidate target;
        if (clickTracker.IsPickingUp && stickyTargetId.HasValue)
        {
            if (!GroundLootScanner.TryFindPickupTarget(
                    inGame,
                    area,
                    groundLoot,
                    scanSettings,
                    priceService,
                    clickTracker.IsIgnored,
                    diagnostics,
                    out target,
                    stickyTargetId))
            {
                stickyTargetId = null;
                lastCandidates = Array.Empty<GroundLootCandidate>();
                lastDiagnostics = diagnostics;
                StatusMessage = "Waiting for pickup";
                return;
            }

            lastCandidates = [target];
            lastDiagnostics = diagnostics;
            stickyTargetId = target.EntityId;

            if (!clickTracker.ShouldRetryClick())
            {
                StatusMessage = $"Waiting for pickup ({clickTracker.ClicksOnPending}/{ClickTracker.MaxClicksPerTarget})";
                return;
            }

            if (!TryClickTarget(target, diagnostics, settings, allowRetry: true))
            {
                return;
            }

            clickTracker.RecordClick();
            return;
        }

        if (!GroundLootScanner.TryFindPickupTarget(
                inGame,
                area,
                groundLoot,
                scanSettings,
                priceService,
                clickTracker.IsIgnored,
                diagnostics,
                out target,
                stickyTargetId))
        {
            stickyTargetId = null;
            lastCandidates = Array.Empty<GroundLootCandidate>();
            lastDiagnostics = diagnostics;
            StatusMessage = diagnostics.GroundEntities == 0
                ? $"No ground loot seen ({diagnostics.AwakeEntities} awake, {groundLootCache.Count} cached)"
                : diagnostics.FilteredByPath == diagnostics.GroundEntities
                    ? $"All loot filtered ({diagnostics.GroundEntities} ground)"
                    : $"No loot in range ({diagnostics.GroundEntities} ground, {diagnostics.OutOfRange} far)";
            return;
        }

        lastCandidates = [target];
        lastDiagnostics = diagnostics;
        stickyTargetId = target.EntityId;

        if (target == default || !target.HasScreenPosition)
        {
            StatusMessage = diagnostics.Clickable == 0
                ? $"No loot in range ({diagnostics.GroundEntities} ground, {diagnostics.OutOfRange} far)"
                : $"Loot in range but off-screen ({diagnostics.Clickable})";
            return;
        }

        if (!TryClickTarget(target, diagnostics, settings))
        {
            return;
        }

        clickTracker.BeginPickup(
            target.EntityId,
            (int)Math.Round(target.ClientPosition.X),
            (int)Math.Round(target.ClientPosition.Y),
            GroundLootRules.IsWorldItemPath(target.ItemPath));
    }

    private bool TryClickTarget(
        GroundLootCandidate target,
        GroundLootScanDiagnostics diagnostics,
        AutoLootSettings settings,
        bool allowRetry = false)
    {
        var cooldownMs = Math.Max(settings.MinMsBetweenPickups, 0);
        if (!allowRetry &&
            (DateTime.UtcNow - lastPickupAttemptUtc).TotalMilliseconds < cooldownMs)
        {
            StatusMessage = $"Waiting ({diagnostics.Clickable} clickable)";
            return false;
        }

        if (!ScreenPositionResolver.TryGetClickableScreenPosition(target.ClientPosition, out var screen))
        {
            StatusMessage = "Target off-screen";
            return false;
        }

        var restoreCursor = diagnostics.Clickable <= 1 && !clickTracker.HasPendingPickup;
        var clicked = GameMouseInput.TryClick(
            (int)Math.Round(screen.X),
            (int)Math.Round(screen.Y),
            settings.ClickHoldMs,
            restoreCursor);
        if (!clicked)
        {
            StatusMessage = "Click failed";
            return false;
        }

        lastPickupAttemptUtc = DateTime.UtcNow;
        PickupAttempts++;

        var label = string.IsNullOrWhiteSpace(target.DisplayName)
            ? LootPathMatcher.GetDisplayName(target.ItemPath)
            : target.DisplayName;

        if (settings.EnableDebugLogging)
        {
            Log.Info(
                $"click {label} @ ({screen.X:0},{screen.Y:0}) value={target.DivineValue:0.###} dist={target.Distance:0.#}",
                "Auto Loot");
        }

        StatusMessage = target.DivineValue > 0
            ? $"Clicked {label} ({target.DivineValue:0.##}e)"
            : $"Clicked {label}";
        return true;
    }

    private static GroundLootScanSettings BuildScanSettings(AutoLootSettings settings) => new()
    {
        StackablesOnly = settings.CurrencyOnly,
        UseValueFilter = settings.UseValueFilter,
        MinDivineValue = settings.MinDivineValue,
        PickupDistance = settings.PickupDistance,
        AlwaysPickupWaystonesAndTablets = settings.AlwaysPickupWaystonesAndTablets,
        PickupWhitelist = settings.PickupWhitelist,
        PickupBlacklist = settings.PickupBlacklist,
    };
}
