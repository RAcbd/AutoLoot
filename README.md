# AutoLoot

OriathHub plugin for Path of Exile 2 that automates ground loot pickup with optional currency-only and value filters.

**Author:** Raff  
**Version:** 0.7.2

## Install

**OriathHub Marketplace (recommended):** install or update from the in-app catalog — builds from this repo or installs the latest [Release zip](https://github.com/RAcbd/AutoLoot/releases).

**Manual from Release:** download `AutoLoot-<version>.zip` — contains a single `AutoLoot.dll` (shared code merged in).

## Repository layout

Source-only repo. Flat layout — no nested `src/AutoLoot/`, no committed `config/` or `data/` (created at runtime).

```
AutoLoot/
  *.cs                 # plugin source
  AutoLoot.csproj
  SDK/                 # OriathHub.Sdk.nupkg (offline / Marketplace builds)
  OriathPlugins.Common/
  build/
```

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download). The [OriathHub SDK](https://github.com/danthespal/OriathHubSDK) package is bundled in `SDK/` — no external setup needed to build.

## Build

```powershell
dotnet build AutoLoot.csproj -c Release
```

Release output is a **single** `bin/Release/net10.0-windows/AutoLoot.dll` (Common merged via ILRepack).

## License

MIT
