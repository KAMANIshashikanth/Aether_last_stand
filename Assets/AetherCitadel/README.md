# Aether – Desert Citadel

A procedural walled desert fortress-town and the open dune country around it:
an irregular double ring of battered curtain wall with square bastions, a keep of five
great round towers, a dense medina of flat-roofed sandstone houses, ruined outskirts on
every side, a caravan camp, and a sandstorm blowing through all of it.

Everything is generated from code – no FBX to re-import, no external DCC tool needed.
You get **both** a fully assembled scene **and** the modular kit it is built from.

---

## Quick start

1. **Tools → Aether → Citadel Builder**
2. **1 – Bake Kit Pieces** — textures, materials and 28 modular prefabs
3. **2 – Build Full Citadel** — assembles the city, the dunes and the desert environment

Then select `Aether_Citadel` in the Hierarchy and press **F** over the Scene view; the
terrain is 900 m across, so nothing is visible until you frame it.

Change **Seed** and rebuild for a different street plan. The walls, keep and landmarks
stay put; the houses, ruins and dunes re-roll.

---

## The Sandstorm slider

The **Sandstorm** slider (0–1) drives the whole atmosphere in one go — sky haze, fog
density and colour, sun intensity and warmth, and the emission rate of all three
particle layers.

| Value | Look |
|---|---|
| 0.0 | clear, hard sun, faint distance haze |
| 0.3–0.6 | typical hazy desert day (default 0.55) |
| 0.8–1.0 | full storm: the far wall disappears, sand streaks past the camera |

**3 – Desert Environment Only** re-dresses the scene without rebuilding geometry, so you
can dial the storm in quickly. It is also driveable from script:

```csharp
Aether.Citadel.CitadelEnvironment.Build("Assets/AetherCitadel", 0.85f, true, false);
```

The storm is three world-space particle layers — slow dust banks overhead, sheets of sand
skimming the ground, and fast grains near the viewer — parented to a rig carrying
`SandstormFollow`, which keeps them centred on the viewer (snapped to a grid so nothing
pops): the **Scene view camera** while editing, the game camera in play mode. Wind blows
towards **+X +Z**; sand drifts are banked on the wall faces turned into it, so if you
change `CitadelEnvironment.Wind`, rebuild the city too.

> Particles only *animate* in the Scene view while it is repainting. If the storm looks
> frozen, enable the Scene view's **Always Refresh** toggle (the ⟳ in its toolbar) or just
> press Play.

---

## What gets created

```
Assets/AetherCitadel/
  Editor/            generator source
  Runtime/           SandstormFollow (the only runtime component)
  Textures/          8 albedo + normal pairs, seamless and procedural, plus 2 particle sprites
  Materials/         M_Citadel_Stone / Plaster / PlasterWarm / PlasterPale / MudBrick /
                     Timber / Roof / Sand / Sky / DustPuff / SandStreak
  Meshes/            SM_* mesh assets (kit pieces + city chunks)
  Prefabs/           P_* kit prefabs + P_Aether_Citadel (the whole city)
```

Scene objects: **`Aether_Citadel`** (the geometry, split into chunks) and
**`Aether_Desert_Environment`** (sun + sandstorm rig).

The city chunks: `Terrain`, `Desert_Detail`, `Wall_Outer`, `Sand_Drifts`, `Wall_Inner`,
`Keep`, `Town_0..3`, `Landmarks`, `Outskirts`, `Lost_Ruins`. Roughly 178k triangles total
(248 houses, 49 towers, 101 ruins at the default seed).

## The kit

| Prefab | Notes |
|---|---|
| `P_Wall_Curtain_20m` / `_10m` | 11 m battered wall, walkway, merlons, arrow slits |
| `P_Wall_Inner_20m`, `P_Wall_Compound_12m` | second ring and courtyard walls |
| `P_Gatehouse` | arched gate + two flanking bastions |
| `P_Tower_Square_Large` / `_Small` | corner bastions, corbelled crown |
| `P_Tower_Round_Great` / `_Small` | keep towers, machicolated |
| `P_Turret`, `P_Stairs_WallWalk` | wall-top turret, ramp to the parapet |
| `P_House_Flat_A/B/C`, `P_House_Adobe` | 1–3 storey flat-roof houses with setbacks |
| `P_House_Pitched_A/B`, `P_House_Tower` | timber roofs, tall narrow house |
| `P_Hall_Domed`, `P_Minaret` | landmarks |
| `P_Ruin_House_A/B`, `P_Rubble_Pile` | collapsed shells with ragged wall lines |
| `P_Tent_Caravan` | black goat-hair tent |
| `P_Sand_Mound`, `P_Rock_A/B`, `P_Dune_Tile_120m` | desert dressing |

Wall prefabs are authored **centred on the origin, running along +X, outer face towards −Z**,
so you can snap them end to end and rotate in 90°/45° steps.

## Scale

1 Unity unit = 1 metre. Curtain wall 11 m, great round towers 27–34 m, city footprint
about 195 × 155 m, terrain 900 × 900 m. The city stands on a wind-scoured flat of radius
128 m (`CitadelLayout.FlatRadius`); dunes build up outside it and reach ~23 m crests.

Anything you place outside that radius should be sampled onto the dune surface:

```csharp
Vector2 off = Pieces.DuneOffset(seed);
float y = Pieces.DuneHeight(p.x + off.x, p.z + off.y, CitadelLayout.FlatRadius, p.x, p.z);
```

## Export to Blender / Max / Maya

Select the `Aether_Citadel` root (or any prefab instance) →
**Tools → Aether → Citadel → Export Selection to OBJ**. Writes one `.obj`, a `.mtl`,
and copies the textures next to them.

## Rendering notes

Materials use `Universal Render Pipeline/Lit` and fall back to `Standard` if URP is not
found; particles use `URP/Particles/Unlit` with the same kind of fallback chain. They are
plain albedo + normal, smoothness ≈ 0.05, metallic 0.

Fog is scene fog (Lighting window → Fog), which URP honours. The sky is a
`Skybox/Procedural` material saved as `M_Citadel_Sky`.

For extra polish, add a URP **Volume** with Tonemapping (ACES), a little Bloom and a warm
Color Adjustments tint — the generator deliberately does not touch post-processing so it
stays compatible with projects that have not installed it.

## Tuning the shape

- Footprint polygon: `CitadelLayout.Ring`
- Keep tower positions/sizes: `CitadelLayout.KeepTowers`
- Wall proportions: `WallSpec.Curtain()` / `Inner()` / `Compound()` in `CitadelPieces.cs`
- Streets and reserved plots: the `roads` / `reserved` lists in `CitadelLayout.Build`
- Outskirt mix (house / ruin / tent / rubble): the roll thresholds in the outskirts loop
- Dune shape: `Pieces.DuneHeight`
- Weathering, cracking and how much render has flaked off: the `RenderCoat` calls at the
  top of `CitadelTextures.BuildPalette`
- Global texture density and foundation depth: `CK.D` and `CK.Sink`

## Geometry convention

If you add your own pieces: a face spanned by (u, v) from an origin is visible from
`Cross(v, u)`. `MeshBuilder.Quad`/`Tri` handle the winding; use `Vertex` + `Triangle`
for smooth-shaded indexed surfaces like the dune field.
