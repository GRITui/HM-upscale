# ADR 0004: Asset manifest

Status: accepted. Implemented by `HM.Importers.AssetManifest` and `ManifestImporter`.

## Context

ADR 0003 says converted assets live outside git. The Unity importer needs one
machine-readable index of what a converter produced, with stable ids so maps
and code never depend on original file names.

## Decision

The converter writes `manifest.json` at the root of the converted folder. The
folder is `$HM_CONVERTED`, else the EditorPrefs override, else
`<repo>/assets/hm-raw/converted`. It is git-ignored and never committed.

```json
{
  "schema": 1,
  "assets": [
    { "id": "mdl_box", "type": "model", "path": "models/box.gltf",
      "tags": ["prop"], "license": "original-synthetic" }
  ]
}
```

Rules, enforced by `AssetManifest.Validate()` (an invalid manifest imports nothing):

- `id`: `^[a-z][a-z0-9_]{0,63}$`, unique across the manifest.
- `type`: `model` (.gltf/.glb), `texture` (.png), `audio` (.ogg), `data` (.json).
- `path`: relative to the converted root, no `..`, no backslash, no colon.
- `tags`: same pattern as ids; become Addressables labels.
- `license`: required, free text (for example `original-synthetic`, `user-extracted`).
- `schema` newer than the importer supports is rejected.

## Import behaviour

- Files are copied to `Assets/_HM/Imported/{type}/{id}.{ext}`; original names
  never enter the project (licensing-safe naming). glTF sidecar files referenced
  by uri are copied alongside.
- Each model also gets `Imported/prefab/{id}.prefab`.
- One Addressables group per type: `HM_Model`, `HM_Texture`, `HM_Audio`,
  `HM_Data`. Address = id, labels = tags. Re-import is idempotent.
- `Imported/` and `AddressableAssetsData/` are git-ignored.
- Entry points: menu `HM/Importers/Import Converted Assets`, batch
  `-executeMethod HM.Importers.ManifestImporter.ImportBatch`.

## Testing

EditMode tests generate a cube glTF, a PNG and a manifest in code. No game data
is used.
