# AutoLoot

OriathHub plugin for Path of Exile 2 that automates ground loot pickup with optional currency-only and value filters.

**Author:** Raff  
**Version:** 0.7.2

## Features

- Automated ground loot clicking with pickup distance and safety pauses
- Currency-only mode for orbs, shards, fragments, runes, omens, and similar drops
- Optional minimum divine value filter using OriathHub host pricing (SDK 0.10.1)
- Ground-loot entity cache (snapshot + per-frame deltas) for faster, stable scanning
- Skips gold piles in all modes (game auto-picks them); currency-only mode works for orb WorldItem placeholders
- Cursor position restored after each pickup click
- Liability disclaimer in the dashboard
- Loot HUD and session totals via BetterLootTracker integration

## Requirements

- [OriathHub](https://github.com/danthespal/OriathHubSDK) with SDK 0.10.1+
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (build from source only)
- [BetterLootTracker](https://github.com/RAcbd/BetterLootTracker) (recommended, for loot HUD)

## Install

**OriathHub Marketplace (recommended):** install or update from the in-app catalog. Marketplace builds from this repo’s source, or installs the latest [Release zip](https://github.com/RAcbd/AutoLoot/releases).

**Manual from Release:** download `AutoLoot-<version>.zip` from [Releases](https://github.com/RAcbd/AutoLoot/releases) and extract into your OriathHub `Plugins/` folder.

**Manual from source:** clone this repo and build (see below), then copy the output DLLs plus `config/` and `data/` into `Plugins/AutoLoot/`.

## Repository layout

This repo is **source only**. DLLs and release zips are not committed — they are published on [GitHub Releases](https://github.com/RAcbd/AutoLoot/releases) when tagged.

```
AutoLoot/
  src/AutoLoot/          # C# source
  config/                # Example settings
  data/                  # Default currency name mappings
```

## Build from source

```powershell
cd src/AutoLoot
dotnet restore
dotnet build -c Release
```

## License

MIT
