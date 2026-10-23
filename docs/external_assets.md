# External assets: sources, licences and how to bring them in

Research: 20 October 2026; the tool and the pipeline import: 21 October 2026.
Board of keyless garden assets: `Builds/Research/external_assets/keyless_garden_assets.png`.

## Summary

- **Without any key we already have two good CC0 sources:** Poly Haven (521 models, 869 textures, HDRIs) and ambientCG
  (2,036 materials). Both have open JSON APIs. Poly Haven's "the_shed" collection alone has 69 garden and shed props
  (hose reel, sprinkler, watering can, gloves, spade, trowel, planters, crates, basket, bench).
- **The installed MCP (`mcp-for-blender` 2.2.x) can do more than we used:** Sketchfab and Poly Pizza search and import,
  and four AI generators (Hyper3D Rodin, Hunyuan3D, Tripo, Meshy). Each one needs a key or a paid plan.
- **Style is the real limit, not access.** Poly Haven models are photo scans. They look out of place next to our
  simpler look; the user kept our procedural tyre over the scanned one for this reason. Scanned *textures* fit better
  than scanned *models*, because we can map them onto our own simple shapes and bake them.
- **Done (21 October 2026):** a shared tool, **free-assets** (`../free-assets`, also an MCP server for every
  project), searches and fetches from eight sources with a licence record. In this project `tools/Assets.ps1` fetches
  into `tools/blender/external/`, and `om.external(...)` turns an asset into a part of a textured model, so every
  outside asset goes through our atlas, bake and tests. The Blender MCP stays for browsing and quick trials.

## What the installed MCP can reach

Server `uvx mcp-for-blender` (the old `blender-mcp`, by ahujasid). Blender add-on `blender_mcp.py`, on port 9876.
Telemetry is off (`DISABLE_TELEMETRY=true` in our Claude Code config).

| Tool | Source | Key | Switch in Blender |
|---|---|---|---|
| `search_assets`, `import_asset` | Poly Haven | none | `scene.blendermcp_use_polyhaven` |
| | Sketchfab | API token (free account) | `blendermcp_use_sketchfab` |
| | Poly Pizza | API key (free, poly.pizza/settings/api) | `blendermcp_use_polypizza` |
| `generate_3d` | Hyper3D Rodin | key, or a shared free-trial key | `blendermcp_use_hyper3d` |
| | Hunyuan3D | Tencent Cloud SecretId/SecretKey, or a local server | `blendermcp_use_hunyuan3d` |
| | Meshy | key; a paid Meshy plan is needed for API use | `blendermcp_use_meshy` |
| | Tripo | only through the paid "MCP for Blender Premium" | – |

Where keys go: the add-on reads them in this order: add-on preferences, then scene properties, then environment
variables (`BLENDERMCP_SKETCHFAB_API_KEY`, `BLENDERMCP_POLYPIZZA_API_KEY`, `BLENDERMCP_HYPER3D_API_KEY`,
`BLENDERMCP_MESHY_API_KEY`, `BLENDERMCP_HUNYUAN3D_SECRET_ID`, `BLENDERMCP_HUNYUAN3D_SECRET_KEY`). **The user enters keys
themselves** (Blender: Edit > Preferences > Add-ons > MCP for Blender, or a user environment variable). Claude never
types or stores a key.

What the code shows:
- Poly Haven imports are tagged on the Blender datablocks (`polyhaven_id`, `polyhaven_url`, `polyhaven_licence`,
  authors). Poly Pizza imports get similar `polypizza_*` tags. This is useful for a licence manifest.
- **Sketchfab search does not filter by licence.** It asks only for downloadable models, so non-commercial (NC) and
  share-alike (SA) models come back too. Every Sketchfab model's licence has to be checked before import.
- Generated models arrive at arbitrary scale and facing (Meshy: real size, origin at the bottom), as one dense mesh.

## Sources

| Source | Content | Style | Licence | Access | Fit for us |
|---|---|---|---|---|---|
| [Poly Haven](https://polyhaven.com) | 521 models, 869 textures, HDRIs | photo scans | CC0 | open API, no key; needs its own User-Agent; asks for a credit when the live API is built into a tool | textures and HDRIs: good. Props: realistic, 3k–35k tris; plants and trees up to millions of tris (not game-ready) |
| [ambientCG](https://ambientcg.com) | 2,036 materials, HDRIs | photo scans | CC0 | open API v3 (`/api/v3/assets?q=…&type=material&include=downloads`), zips 1K–8K | good, already used (Rope001, Rope003, Rubber001). One person runs it, so keep local copies |
| [Poly Pizza](https://poly.pizza) | 10,000+ low-poly models | simple, stylised | CC0 or CC-BY per model | free API key; built into MCP; licence filter | style close to ours; CC-BY needs a credit |
| [Quaternius](https://quaternius.com) | packs, e.g. Stylized Nature MegaKit: 116 models (40 trees, 35 plants and flowers, 27 rocks) | stylised | CC0 | zip from the site or itch.io, no API; the .blend and Unity URP source versions are for Patreon members | good for vegetation ideas and bases |
| [Kenney](https://kenney.nl) | many 3D kits (city, forest, town…) | very simple, toy-like | CC0, no credit needed | zip, no API | too simple for final models; good for blocking out |
| [Sketchfab](https://sketchfab.com) | hundreds of thousands of models | everything | per model: CC0, CC-BY, CC-BY-SA, CC-BY-NC… | API token; downloads require a signed-in account; the app must show licence and author | only with a strict licence check; CC0 and CC-BY only |
| [BlenderKit](https://www.blenderkit.com) | 100,000+ assets, about half free | mixed | royalty-free or CC0 | its own Blender add-on and account; not in MCP | possible later; licence per asset |

AI generators (all through `generate_3d`):

| Generator | Cost | Licence of the output |
|---|---|---|
| Meshy | free plan 100 credits a month, no API; Pro 879 TRY a month with API | free plan: CC BY 4.0 (credit Meshy); paid: owned by the user |
| Hunyuan3D 2.1 (Tencent) | open source, can run locally (needs a strong GPU) or on Tencent Cloud | outputs belong to the user; the licence excludes the EU, UK and South Korea; over 1 million monthly users needs a separate licence |
| Hyper3D Rodin | paid, with a shared free-trial key | not checked |
| Tripo | only through the paid Premium tier here | not checked (the pricing page blocked automated access) |

AI models come out as dense meshes with lighting painted into the texture and arbitrary UVs. They break our rules (one
clean mesh, real UVs, baked maps, 1:1 scale) without manual retopology, so they are only useful for concept and
blocking.

## How an outside asset enters the pipeline

1. **Find it.** `tools/Assets.ps1 -Search "watering can" [-Source polyhaven] [-Kind model]` (or the `free-assets` MCP
   tools `asset_search`, `asset_info` with thumbnails). Only CC0, CC-BY, OGA-BY and BlenderKit-RF results are listed.
2. **Fetch it** after the user approves the asset: `tools/Assets.ps1 -Get polyhaven:planter_pot_clay -ApprovedBy "…"`.
   - Files go to `tools/blender/external/<source>/<id>/`, never into `Assets/`.
   - `asset.json` there records source, page, authors, licence and hashes; `assets.json` and `CREDITS.md` in
     `tools/blender/external/` list everything.
3. **Use it in a generator.** `model.external("polyhaven:planter_pot_clay", "matte", "<what it really is, why this
   asset>", size=0.24, axis="z", budget=1500)` in `tools/blender/models/<model>.py`:
   - refuses the asset if `asset.json` is missing or its licence is not in `EXTERNAL_LICENCES`;
   - imports glTF, FBX or OBJ (never .blend: it can run code), applies transforms and joins the meshes into one part;
   - turns it (`turn`), sizes it to the measured video size, centres it with its bottom at z = 0, moves it to
     `location`;
   - decimates to `budget` triangles and re-shades it like the generated parts;
   - routes its textures through the source UV, so `finish_textured` bakes them into the model's single atlas;
   - writes an `external` record (ref, licence, authors, page) into the model's spec file.
4. **Tests.**
   - `tools/Models.ps1 -Checks` runs `tools/blender/checks/external.py`: licence gate, one part, size, centring,
     triangle budget, and the baked colour matches the source texture.
   - Unity `ModelImportTests.OutsideAssetsHaveAllowedLicencesAndCredits` checks that a CC-BY asset has a line in
     `Assets/Opus/Resources/ThirdPartyCredits.txt`.
   - `FetchedOutsideAssetsHaveAllowedLicences` checks everything in `tools/blender/external/assets.json`.

Style still decides. Per the user, the game suits simpler graphics (our procedural tyre stayed). Scanned textures on our
own shapes fit better than whole scanned models.

## Keys the user creates

The keys are free. Each is stored per user with `free-assets key set <source>` (hidden prompt) in a terminal, never
in the project and never in the chat.

| Key | Opens | Where |
|---|---|---|
| Poly Pizza | search and download of 10,000+ stylised low-poly models (single Quaternius and Kenney models too) | https://poly.pizza/settings/api > create an app |
| Sketchfab | downloads (search already works without it) | https://sketchfab.com/settings/password > API token |
| BlenderKit | downloads of free assets (search already works without it) | https://www.blenderkit.com/profile > API key |

Poly Pizza's API terms (the user asked, 21 October 2026):
- credit every model used through the API, CC0 ones too, and name Poly Pizza with a link;
- do not resell the content or offer it as a service;
- no commercial use of the API if you or your company earn more than $50,000 a year.

free-assets marks every Poly Pizza asset as needing credit and adds "Models provided by Poly Pizza" to `CREDITS.md`.

The Blender MCP reads the Poly Pizza and Sketchfab keys too, but only when they are set as the environment variables
`BLENDERMCP_POLYPIZZA_API_KEY` and `BLENDERMCP_SKETCHFAB_API_KEY`, or in its add-on preferences.
