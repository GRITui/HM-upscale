# ADR 0005: Map JSON

Status: accepted. Implemented by `HM.World.MapData` and `MapLoader`.

## Context

Areas must be built at runtime from converted data. `JsonUtility` cannot read
2D arrays or vector types, so the schema is flat arrays only.

## Schema (version 1)

```json
{
  "schema": 1, "id": "test_area", "name": "Test Area",
  "terrain": { "width": 3, "depth": 3, "cellSize": 2,
               "heights": [0,0,0, 0,2,0, 0,0,0], "color": [0.3,0.5,0.2] },
  "objects":  [ { "id": "o1", "model": "mdl_box", "position": [1,0,1],
                  "rotationY": 90, "scale": 1, "size": [1,1,1] } ],
  "spawns":   [ { "name": "default", "position": [2,2,2], "rotationY": 180 } ],
  "colliders":[ { "center": [2,1,2], "size": [1,2,1], "rotationY": 45 } ]
}
```

- `terrain.heights`: row-major, index `z * width + x`, length `width * depth`;
  `width, depth >= 2`; `cellSize > 0`. Vertices sit at `(x * cellSize, h, z * cellSize)`.
- Vectors are `float[3]`; rotation is yaw in degrees.
- `objects[].model` is an asset manifest id (ADR 0004). If the source cannot
  supply it, a placeholder cube is built, sized by `size` and multiplied by `scale`.
- `spawns` are looked up by name. `colliders` become BoxColliders.
- Unknown schema versions and malformed terrain throw `FormatException`.

## Loading

`MapLoader.Build(json, IModelSource, terrainMaterial, placeholderMaterial)`
returns a `BuiltMap` with Root, Spawns, ModelCount and PlaceholderCount.
`AddressablesModelSource` resolves ids through Addressables; tests use
`DictionaryModelSource`. Tests use a synthetic map only.

Additive per-area scene loading is done: `AreaSceneService` loads and unloads area scenes on top of the persistent Boot scene, and `MapAreas` holds runtime Map-JSON areas.
