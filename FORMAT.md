# UI project and library formats

## Standalone `.knotjot-ui`

New projects use `.knotjot-ui`. The editor continues to open legacy `.bmapui` projects.

Important top-level fields:

```json
{
  "app": "knotjot-ui-editor-project",
  "id": "ui_<stable-id>",
  "name": "Custom UI",
  "css": "...",
  "editor": "knotjot-ui-editor",
  "version": 5,
  "schema": "knotjot-ui-skin",
  "schemaVersion": 2,
  "activeTarget": "card",
  "export": {
    "clipToFrame": true,
    "customTextAllowed": false
  },
  "designs": {}
}
```

Each design contains element DTOs. V1.0.0 adds `id`, `locked`, `rotation`, `blendMode`, `maskMode`, and `maskTargetId`. Unknown optional fields should be preserved by future migrations where practical.

## Dynamic UI skin library

```json
{
  "format": "knotjot-ui-skins",
  "version": 1,
  "revision": 7,
  "writerId": "knotjot-ui-editor-...",
  "updatedUtc": "2026-07-15T00:00:00.0000000Z",
  "current": "ui_<stable-id>",
  "skins": {
    "ui_<stable-id>": {
      "name": "Custom UI",
      "kind": "css",
      "css": "...",
      "schemaVersion": 1,
      "updatedUtc": "...",
      "contentHash": "SHA256_HEX",
      "writerId": "...",
      "sourceFile": "optional path",
      "editorData": {}
    }
  }
}
```

The editor queries KnotJot for the real library location before writing. A legacy ThoughtCanvas library is read only when the KnotJot library does not yet exist; the next write uses the new format and path.

## Limits

- Standalone file: 32 MB; 128 designs; 1000 elements per design and 5000 total; 100000 points per ink layer; 12 MB Base64 characters per image and 24 MB total.
- Image import: 12 MB encoded file; 8192 maximum dimension; 40 million pixels.
- Dynamic library limits come from the packaged [protocols/ui-skin-protocol.json](protocols/ui-skin-protocol.json), mirrored from [the shared contract](../../../../shared/ui-skin-protocol.json): currently 16 MB, 500 skins and 2 MB generated CSS per skin; editable project data is limited to 12 MB per sync.
