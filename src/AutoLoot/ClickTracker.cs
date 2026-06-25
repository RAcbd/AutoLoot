namespace AutoLoot;

using OriathHub.RemoteEnums.Entity;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathPlugins.Common.Loot;

internal sealed class ClickTracker
{
    private readonly Dictionary<uint, DateTime> ignoredEntityUntilUtc = new();
    private uint? pendingEntityId;
    private DateTime pendingSinceUtc = DateTime.MinValue;
    private DateTime lastClickUtc = DateTime.MinValue;
    private DateTime successCooldownUntilUtc = DateTime.MinValue;
    private int clicksOnPending;

    public const int MaxClicksPerTarget = 5;

    public int ClicksOnPending => clicksOnPending;

    private const int RetryClickIntervalMs = 40;
    private const int PickupTimeoutMs = 250;
    private const int IgnoreAfterFailedMs = 200;
    private const int SuccessCooldownMs = 25;

    public void Reset()
    {
        ignoredEntityUntilUtc.Clear();
        pendingEntityId = null;
        pendingSinceUtc = DateTime.MinValue;
        lastClickUtc = DateTime.MinValue;
        successCooldownUntilUtc = DateTime.MinValue;
        clicksOnPending = 0;
    }

    public bool IsIgnored(uint entityId, int clientX, int clientY)
    {
        _ = clientX;
        _ = clientY;
        Prune();
        return ignoredEntityUntilUtc.TryGetValue(entityId, out var entityUntil) &&
               entityUntil > DateTime.UtcNow;
    }

    public bool IsPickingUp => pendingEntityId.HasValue;

    public bool IsInSuccessCooldown() => DateTime.UtcNow < successCooldownUntilUtc;

    public bool ShouldRetryClick()
    {
        if (!pendingEntityId.HasValue || clicksOnPending >= MaxClicksPerTarget)
        {
            return false;
        }

        return clicksOnPending == 0 ||
               (DateTime.UtcNow - lastClickUtc).TotalMilliseconds >= RetryClickIntervalMs;
    }

    public bool HasPendingPickup => pendingEntityId.HasValue;

    public void BeginPickup(uint entityId, int clientX, int clientY, bool isWorldItem)
    {
        _ = clientX;
        _ = clientY;
        _ = isWorldItem;
        if (pendingEntityId != entityId)
        {
            pendingEntityId = entityId;
            pendingSinceUtc = DateTime.UtcNow;
            clicksOnPending = 0;
        }

        RecordClick();
    }

    public void RecordClick()
    {
        clicksOnPending++;
        lastClickUtc = DateTime.UtcNow;
    }

    public void ClearPending()
    {
        pendingEntityId = null;
        clicksOnPending = 0;
    }

    public void UpdateFrame(AreaInstance area)
    {
        Prune();
        if (!pendingEntityId.HasValue)
        {
            return;
        }

        var entityId = pendingEntityId.Value;
        if (WasRemovedThisFrame(area, entityId) || !TryFindEntity(area, entityId, out var entity))
        {
            ClearPending();
            successCooldownUntilUtc = DateTime.UtcNow.AddMilliseconds(SuccessCooldownMs);
            return;
        }

        if (entity.EntityState is EntityStates.Useless)
        {
            ClearPending();
            return;
        }

        if (!GroundLootRules.IsGroundLootEntity(entity))
        {
            ClearPending();
            successCooldownUntilUtc = DateTime.UtcNow.AddMilliseconds(SuccessCooldownMs);
            return;
        }

        var pendingMs = (DateTime.UtcNow - pendingSinceUtc).TotalMilliseconds;
        if (pendingMs < PickupTimeoutMs)
        {
            return;
        }

        ignoredEntityUntilUtc[entityId] = DateTime.UtcNow.AddMilliseconds(IgnoreAfterFailedMs);
        ClearPending();
    }

    private void Prune()
    {
        var now = DateTime.UtcNow;
        foreach (var key in ignoredEntityUntilUtc.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToList())
        {
            ignoredEntityUntilUtc.Remove(key);
        }
    }

    private static bool WasRemovedThisFrame(AreaInstance area, uint entityId)
    {
        foreach (var entity in area.EntitiesRemovedThisFrame)
        {
            if (entity.Id == entityId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindEntity(AreaInstance area, uint entityId, out Entity entity)
    {
        foreach (var (_, candidate) in area.AwakeEntities)
        {
            if (candidate.Id == entityId)
            {
                entity = candidate;
                return true;
            }
        }

        entity = default!;
        return false;
    }
}
