# puhu.plugin.template

Template for creating [Puhu](https://github.com/st0o0/puhu) plugins. Click **"Use this template"** on GitHub to create your own plugin repo.

## Getting Started

1. Click "Use this template" and name your repo (e.g. `puhu.plugin.awesome`)
2. Clone your new repo locally
3. Rename everything from `MyPlugin` to your plugin name:

| What | Replace |
|------|---------|
| Folder/file names | `MyPlugin` → `Awesome` |
| Namespaces | `Puhu.Plugin.MyPlugin` → `Puhu.Plugin.Awesome` |
| Plugin name in `MyPlugin.cs` | `Name => "Awesome"` |
| Tab label and route | `"MyPlugin", "/myplugin"` → `"Awesome", "/awesome"` |
| `puhu-manifest.json` | Update id, name, description, author, repository, asset |
| `csproj` filename | `Puhu.Plugin.MyPlugin.csproj` → `Puhu.Plugin.Awesome.csproj` |

4. Set the `Puhu.Plugin` package version in the `.csproj` once it's published on NuGet

## Project Structure

```
src/
  Puhu.Plugin.MyPlugin.csproj   # Project file with Puhu.Plugin NuGet reference
  MyPlugin.cs                    # IPuhuPlugin — entry point, registers tab + route
  Pages/
    MyPage.cs                    # ReactivePage with IKeyHintProvider
    MyViewModel.cs               # ReactiveViewModel with reactive state
puhu-manifest.json               # Plugin manifest for the Puhu registry
```

## Building

```bash
dotnet build src/Puhu.Plugin.MyPlugin.csproj
```

## Publishing to the Registry

1. Create a GitHub Release with your built DLL as an asset
2. Open a PR to [puhu.registry](https://github.com/st0o0/puhu.registry) adding your plugin entry to `index.json`
