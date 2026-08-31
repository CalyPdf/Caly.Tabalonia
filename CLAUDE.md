# Tabalonia

Avalonia library: tab control with draggable, detachable and reattachable tabs. Port of [Dragablz](https://github.com/ButchersBoy/Dragablz) (WPF). Published to NuGet as `Tabalonia`.

## Structure

- `Tabalonia/` — the library (the NuGet package)
  - `Controls/TabsControl.cs` — main control, inherits `TabControl`; drag, detach/reattach, add/close logic
  - `Controls/DragTabItem.cs` — tab container; `Controls/LeftPressedThumb.cs` — drag thumb primitive
  - `Panels/TabsPanel.cs`, `Panels/TopPanel.cs` — tab strip layout math
  - `Themes/Custom/` and `Themes/Fluent/` — two shipped themes
  - `GlobalUsings.cs` — Avalonia namespaces are globally imported; don't add `using Avalonia.*` in library files
- `Tabalonia.Demo/` — demo app (CommunityToolkit.Mvvm)
- `Tabalonia.Tests/` — xunit v3 + `Avalonia.Headless.XUnit` (use `[AvaloniaFact]`, not `[Fact]`)

## Commands

```bash
dotnet build Tabalonia.sln
dotnet test Tabalonia.Tests/Tabalonia.Tests.csproj
dotnet run --project Tabalonia.Demo   # visual check of drag/detach behavior
```

## Rules

- **Both themes must stay in sync**: any change to template parts (`PART_*` names, template structure) in `Themes/Custom/*.axaml` must be mirrored in `Themes/Fluent/*.axaml` and vice versa. `OnApplyTemplate` in `TabsControl` requires `PART_TopPanel`, `PART_LeftDragWindowThumb`, `PART_RightDragWindowThumb`, and optionally binds `PART_ScrollTabsLeftButton` / `PART_ScrollTabsRightButton`. `TopPanel` looks its children up by those names, so their XAML order does not matter. `Fluent_Theme_Offers_The_Same_Scroll_Buttons_As_The_Custom_Theme` guards the scroll buttons; extend it when you add a part.
- `TabsPanel` takes `ItemWidth`, `MinItemWidth` and `ItemOffset` as plain CLR properties, which Avalonia cannot see. Every assignment to them (`TabsControl.OnPropertyChanged`) must be followed by `_tabsPanel.InvalidateMeasure()`, or the new value only lands on the next unrelated layout pass.
- Shared build settings (LangVersion, Nullable, analyzers, `$(AvaloniaVersion)`) live in `Directory.Build.props` — don't duplicate them in csproj files.
- The library keeps the **lowest** supported Avalonia version (`12.0.0`) as its dependency; only Demo/Tests reference `$(AvaloniaVersion)`. Don't bump the library's Avalonia reference without a reason — it raises the floor for consumers.
- Library is AOT-compatible (`IsAotCompatible`) — avoid reflection-based APIs.
- Drag reorder, cross-window attach/detach and tab strip scrolling **are** covered by headless tests (`DragSessionTests`, `TabDragEventTests`, `TabDetachHostTests`, `TabScrollingTests`). What the headless platform cannot show — animations, real pointer capture, window chrome, DPI — still needs a pass through the Demo app.
- **Headless tests must close every window they open**: derive from `TabsWindowTest` and use `ShowWindow` / `TrackDetachedWindowsOf`. The headless platform maps all windows onto one screen space and a `TabsControl` stays in the static drag-target registry until it leaves the visual tree, so a leaked window silently steals dragged tabs from later tests.
- Commits: conventional commits (`feat:`, `fix:`, `build:`, `docs:`, `test:`). Work happens on `develop`; `main` is the release branch.
- Releases: bump `<Version>` in `Tabalonia/Tabalonia.csproj`, update `CHANGELOG.md`, tag `v<version>` — CI publishes to NuGet.
