# Migration notes

## V0.0.3 to V1.0.0

- Missing element IDs receive new stable IDs; duplicates and invalid IDs are replaced.
- Existing element order and visible state are retained.
- Missing lock, rotation, and blend fields use `false`, `0`, and `normal`.
- A legacy mask with no `maskTargetId` uses the drawable layer immediately below it as its migration target when valid.
- Invalid or missing mask targets are cleared instead of being redirected to an unrelated layer.
- Legacy `app: "brace-mindmap-ui"` is recognized only by `LegacyProjectMigrator`; new saves use `knotjot-ui-editor-project` and `knotjot-ui-skin`.
- V0.0.1 `regions + shapes`, V0.0.2 `elements`, V0.0.3 `designs`, and V1.0.0 fixtures are routed explicitly and tested.
- Old files without `export` ask whether to adopt the current defaults or the safe defaults. Migrated projects are opened as unsaved so the source is not overwritten silently.

Migration occurs in memory when a file is opened. The source file is not overwritten until the user explicitly saves. A failed read or migration leaves the original file untouched and reports an error.

## Dynamic library migration

KnotJot publishes its actual `ui-skins.json` location through a command-line handshake and IPC. The editor reads the old ThoughtCanvas library once only when the KnotJot library is absent, then writes `knotjot-ui-skins` while preserving unrelated records. Repeated syncs update the same stable ID.

## Text-box style isolation

UI skin migration does not read or write `text-styles/custom-text-styles.json`. UI CSS is injected before `text-styles.css`, so text-box sizing, text regions, input rules, decoration SVG, and node attachment remain owned by the text-style layer.
