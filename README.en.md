# Resource Ledger

[日本語](README.md) | **English**

A RimWorld 1.7 mod for inspecting item stocks, changes and their causes. It includes weapons, equipped items, minified furniture and modded resources.

## Installation

Requires RimWorld 1.7 and Harmony for 1.7. Extract the latest `ResourceLedger_<version>_Mod.zip` from [Releases](https://github.com/pengin0503/rimworld-ResourceLedger/releases), place the `ResourceLedger` folder in `RimWorld/Mods`, enable it after Core and Harmony, and restart. `Source.zip` is not the installable mod.

It can be added to an existing save and begins recording after installation. Items first observed become the opening balance; unknown past values are not filled with zero. Back up saves before changing mods.

## Use

Open the ledger icon in the bottom bar. Filter items by name, category or source mod and compare up to six types. Stocks are bars; gains, losses and net changes are lines. Choose the whole map, storage, a particular storage location or all colonies, and switch between item count, nutrition and base market value.

Cause breakdowns, activity details, storage locations, alerts and CSV exports are available. Alerts can use stock thresholds, amount or rate of decrease, and estimated days remaining. CSV files are saved in `ResourceLedgerExports` under the user data folder.

## Scope and limitations

The ledger covers owned visible items on the ground, shelves and supported containers, plus items carried or equipped by colony pawns. Enemy and visitor possessions, undiscovered items, corpses and installed buildings are excluded. Records are by item type, not separated by quality or material.

Unattributed differences are shown as unknown causes; classification of every mod-specific action is not guaranteed. Recording intervals and retention are configurable. Cause tracking scans map items, so its effect on TPS in large colonies remains unverified. If load is a concern, disable recording hauling and map-to-map movement.

**The 0.3.2 UI, normal gameplay and save upgrades have not been verified in-game.**
