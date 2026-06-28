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
  OriathPlugins.Common/  # shared library (compiled + merged into AutoLoot.dll on Release)
  build/               # ILRepack merge targets
```

`config/settings.json` and `data/currency-names.json` are created automatically on first run. Default currency names are embedded in the DLL.

## Build

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) and [OriathHub SDK 0.10.1+](https://github.com/danthespal/OriathHubSDK). Marketplace supplies the SDK when building from source.

```powershell
dotnet build AutoLoot.csproj -c Release
```

Release output is a **single** `bin/Release/net10.0-windows/AutoLoot.dll` (Common merged via ILRepack).

## License

MIT
