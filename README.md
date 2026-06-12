# Puhu.Btop

A btop-style system monitor delivered as a [Puhu](https://github.com/st0o0/puhu) plugin — CPU, memory, disks, network and processes on a single screen.

## Features

- Single-screen layout with **CPU**, **Memory + Disks**, **Network**, and **Processes** boxes — plus a **GPU** box when an NVIDIA or Apple GPU is present.
- Per-core CPU bars and gradient usage graphs.
- Process list with filtering, sorting, and selection.
- Theme-aware colors driven by the host theme.

## Keybindings

| Key | Action |
|------|--------|
| `1` | Toggle CPU box |
| `2` | Toggle Memory box |
| `3` | Toggle Network box |
| `4` | Toggle Processes box |
| `↑` / `↓` | Select process |
| `f` | Filter processes |
| `e` | Toggle process tree |
| `←` / `→` | Change sort column |
| `t` | Terminate process |
| `k` | Kill process |

> Note: the box toggles, process selection, and filter are wired up. Process actions (terminate / kill / tree) and arrow-key sorting are in progress — some of these hints are advertised but not yet fully bound.

## Installation

**Marketplace** — open the Puhu marketplace and search for "Btop", then install.

**Manual** — drop `Puhu.Btop.dll` into `~/.servus/plugins/`.

## Development

Clone with submodules (the Puhu SDK is consumed via the `lib/puhu` git submodule — it is not on NuGet yet, and has a nested `lib/termina` submodule):

```bash
git clone --recurse-submodules https://github.com/st0o0/puhu.btop
```

If you already cloned without submodules:

```bash
git submodule update --init --recursive
```

Then build and test:

```bash
dotnet build src/Puhu.Btop.csproj
dotnet test tests/Puhu.Btop.Tests
```
