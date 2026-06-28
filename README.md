# AutoLoot

OriathHub plugin for Path of Exile 2 that automates ground loot pickup with optional currency-only and value filters.

**Author:** Raff  
**Version:** 0.7.0

## Features

- Automated ground loot clicking with pickup distance and safety pauses
- Currency-only mode for orbs, shards, fragments, runes, omens, and similar drops
- Optional minimum divine value filter using OriathHub host pricing (SDK 0.10.1)
- Ground-loot entity cache (snapshot + per-frame deltas) for faster, stable scanning
- Cursor position restored after each pickup click
- Liability disclaimer in the dashboard
- Loot HUD and session totals via BetterLootTracker integration

## Requirements

- [OriathHub](https://github.com/danthespal/OriathHubSDK) with SDK 0.10.1+
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (build from source only)
- [BetterLootTracker](https://github.com/RAcbd/BetterLootTracker) (recommended, for loot HUD)

## Install

1. Download **`AutoLoot-0.7.0.zip`** (recommended) or `AutoLoot.dll` + `OriathPlugins.Common.dll` from [Releases](https://github.com/RAcbd/AutoLoot/releases).
2. Extract or copy into your OriathHub `Plugins/` folder so you have:
   ```
   Plugins/AutoLoot/
     AutoLoot.dll
     OriathPlugins.Common.dll
     config/settings.json.example   → copy to settings.json
     data/currency-names.json
   ```
3. Enable the plugin in OriathHub.

## Build from source

```powershell
cd src/AutoLoot
dotnet restore
dotnet build -c Release
```

## License

MIT
