# V1.0.0 developer notes

## Main modules

- `MainWindow.xaml(.cs)`: application shell, file operations, component properties, preview, connection, and sync commands.
- `MainWindowEditor.cs`: canvas selection, marquee, grouped transforms, layer list, locks, drawing, images, ink, and mask preview.
- `MainWindowDesign.cs`: per-component design switching and collection.
- `Elements.cs`: editable element model with stable IDs and mask references.
- `ModelSafety.cs`: migration, validation, normalization, and bounded resources.
- `CssBuilder.cs`: component-scoped CSS and SVG generation, including stable masks.
- `AtomicFile.cs`: same-directory temporary write followed by replacement.
- `KnotJotConnection.cs` and `UiSkinProtocol.cs`: path handshake, connection configuration, shared protocol, legacy-library import and KnotJot UI skin writes.
- `ProjectFileService.cs`: bounded reads and schema-independent complexity checks before window state changes.
- `LegacyProjectMigrator.cs`: pure V0.0.1–V1.0.0 migration routing.
- `PreviewService.cs`: catalog-driven dimming and privacy-safe diagnostics.
- `DirtyGuard.cs`: the single save/discard/cancel guard used by recent files, open, new and close.

## Layer and selection invariants

`_elements` is ordered bottom to top. The layer list displays it in reverse. Multi-layer moves keep original relative order. Locked layers may be selected but are excluded from destructive or geometric edits. Components representing fixed original UI regions are not group-scaled.

Every element has a stable `Id`. A mask is a non-line `ShapeElement` with `IsMask=true`, a `MaskMode`, and `MaskTargetId`. Reordering never changes this reference. The adjacent fallback exists only for legacy data missing a target ID.

## Storage and synchronization

Standalone `.knotjot-ui` files contain editable per-component projects, project-level export settings and generated CSS. Legacy `.bmapui` projects remain readable. Dynamic sync writes the path returned by KnotJot, with a stable skin ID. Records carry the generated CSS plus the full standalone object as `editorData`.

Atomic writes use a unique temporary file in the destination directory. If a destination already exists, replacement occurs only after the new UTF-8 file is complete. Model and library validation happens before this write.

KnotJot watches the library every 450 ms and notifies its renderer. The renderer merges records by ID and reapplies the active skin with persistence disabled, preventing a write-notification loop.

## Verification

Run:

```powershell
dotnet build "KnotJot.UiEditor.csproj" -c Release
dotnet run --project "tests\CoreSmoke\CoreSmoke.csproj" -c Release
```

The KnotJot source also contains `tests/ui-skins-hot-reload-smoke.js`, which verifies two external revisions, unchanged document state, and no renderer rewrite of the external revision.
