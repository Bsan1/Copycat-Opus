# Copycat Opus

A Unity project that recreates the **lawn care mechanics** (mowing, trimming, watering) and the **look** of the game in the reference video. Family/memory/year systems are out of scope. Detailed plan: [PLAN.md](PLAN.md).

- Unity `6000.3.11f1`, URP 17.3, Input System 1.19, uGUI + TextMeshPro
- The scene is built from code (`Assets/Opus/Scripts/Core/GameBootstrap.cs`); the models are hand-built geometry and (migration in progress) FBXs generated from code in Blender, drawn with textures painted from code (see "Materials" and "Models" below).
- Interface font: [Patrick Hand](Assets/Opus/Fonts/OFL.txt) (SIL Open Font License).

## Command line (without opening the editor)

These commands do not work while the Unity editor has this project open (project lock).

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build.ps1
```
Configures the project (URP, Linear, Input System, TMP resources), generates the font asset, the surface textures and the scene, and outputs `Builds/Windows/CopycatOpus.exe`.

```powershell
powershell -ExecutionPolicy Bypass -File tools/Test.ps1
```
Runs the EditMode tests and writes the result to `Logs/tests-EditMode.xml`.

```powershell
powershell -ExecutionPolicy Bypass -File tools/Verify.ps1 [-Models house] [-AllScenarios] [-NoUnity]
```
The whole check in one short report:
- rebuilds only the models whose inputs changed, several at a time;
- runs the pipeline checks when the shared code changed;
- runs the tests, the exe and the scenarios when anything the game uses changed: all of them after a code change, the three model scenarios after a model change;
- skips Unity when the editor has the project open.

Inputs are compared by content hash, so touching a file without changing it does not trigger a rebuild. Each model line ends with its approval state (`approved`, `needs review`, `reviewed`, `review: N open`, `not approved`). A passing build is technically sound; it is not approved. `-Approve house` records the user's approval of the current geometry. `-Stats` lists builds, failures and review findings per model from `Builds/QC/history.jsonl`.

It prints one line per model, step and scenario; the logs are in `Logs/verify-*.log`. `py -3 tools/Board.py out.png u16_showroom/v_house_eave_corner@700,300,1500,800=eave ...` puts several shots or crops into one image.

```powershell
powershell -ExecutionPolicy Bypass -File tools/Shots.ps1 -Scenario u0_overview
```
Runs the exe without opening a window on screen (the game window is embedded in an invisible parent window off screen) and takes screenshots, interface included, from the scenario's camera poses: `Builds/Shots/<scenario>/*.png` + `result.json` (a blank or duplicate screenshot counts as an error). The scenarios are in `Assets/Opus/Scripts/Core/Scenarios.cs`. Scenarios make no sound through the speakers; the mix is still measured (`u9_audio` records it as `mix.wav`). Frame time measurements are unreliable while another game or GPU-heavy application is open.

```powershell
powershell -ExecutionPolicy Bypass -File tools/Sounds.ps1
```
Writes every sound synthesised from code as `Builds/Sounds/*.wav` (loops twice so the seam can be heard). The game has no ready-made sound files; the engine, trimmer, water, wind, birds and feedback sounds are generated in `Assets/Opus/Scripts/Audio/SoundBank.cs`.

```powershell
powershell -ExecutionPolicy Bypass -File tools/Models.ps1 [-Only wheel] [-Checks]
```
Runs Blender in the background and generates the models from the `tools/blender/models/*.py` generators (see "Models" below). Blender path: `-BlenderPath`, the `BLENDER` environment variable or the Steam install. `-Checks` runs the pipeline checks (`tools/blender/checks/*.py`); without `-Only` it generates no models. Afterwards `Build.ps1` / `Test.ps1` import the models into Unity and validate them.

```powershell
powershell -ExecutionPolicy Bypass -File tools/Assets.ps1 -Search "watering can" [-Source polyhaven] [-Kind model]
powershell -ExecutionPolicy Bypass -File tools/Assets.ps1 -Get polyhaven:planter_pot_clay -ApprovedBy "<who approved it>"
```
Searches and fetches free outside assets (Poly Haven, ambientCG, Poly Pizza, Sketchfab, Kenney, Quaternius, BlenderKit, OpenGameArt) through the shared tool **free-assets** (`../free-assets`, or the folder in `FREE_ASSETS_HOME`; it is also an MCP server registered for every project). Only licences safe for the game are accepted (CC0, CC-BY, OGA-BY, BlenderKit royalty-free). The files go to `tools/blender/external/<source>/<id>/` with an `asset.json` licence record; `assets.json` and `CREDITS.md` there list everything. Research, sources and keys: `docs/external_assets.md`.

```powershell
powershell -ExecutionPolicy Bypass -File tools/Showroom.ps1 [-OnlyCapture]
```
Builds the model showroom scene (`Assets/Opus/Scenes/ModelShowroom.unity`) and captures its editor view from two sides for every model as `Builds/Shots/model_showroom/*.png`.

**Scene:** lines up every model in the game side by side the way the game builds them: materials, sun, sky, ambient colour, colour grading. Blender models in the back row, hand-built garden props in the front row. `ModelShowroom` runs in the editor.

**Inspecting:** in the editor, without pressing Play.
- The models are built when the scene opens. Double-click a model under `ModelShowroom > Showroom` in the Hierarchy (or select it and press F) to frame it.
- Right click + WASD to fly around, Alt + left click to orbit.
- The names show as labels in the Scene view while Gizmos are on.
- In the distance the game's fog washes things out; Fog can be turned off from the Effects menu in the Scene view.

**Saving:** the models are not saved into the scene (their materials are generated from code); changes made in the scene do not persist. Only `Main.unity` goes into the build.

**Command line tools:** they do not work while the project is open in the editor (the project is locked); close the editor first.

## Playing

`Builds/Windows/CopycatOpus.exe` — the game starts with the mower in hand; if there is a save it continues from it. A left click locks the cursor, Esc opens the break menu.

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Push / walk | WASD (or arrows) | Left stick |
| Steer / look | Mouse | Right stick |
| Select a tool | 1 mower, 2 trimmer, 3 hose, 4 hand | D-pad left / right |
| Trim / water | Hold left click | RT / X |
| Hose spray mode (Wide ↔ Narrow; Rain from the workshop) | R | RB or D-pad down |
| Show what is left in the active patch | H | R3 or D-pad up |
| Developer metrics / finish the active patch | F3 / F8 | — |
| Next / previous tool | E / Q or the wheel | LB |
| Break menu (continue, end the day, job board, workshop, collection, save, load, settings, new game, quit; on a job: deliver the job, leave the job) | Esc | Start (B: back) |
| Quick save / quick load | F5 / F9 | — |

Scenarios: `u0_overview` (the garden), `u1_mower` (mower feel and collision checks), `u1_framing` (field of view × pitch grid), `u2_grass` (grass quilt, obstacle mask, 1080p frame time), `u3_mow` (mowed area, soft cut, clippings, fence base strip, opposite-direction stripes), `u4_trim` (trimmer view, fence base, weed pulling, ivy, the mower not pulling weeds), `u5_hose` (watering, greening, spray modes, water does not pass through the fence), `u6_hud` (patch tasks, HUD, dashed border, show remaining, finish effect, flowers, area finish), `u7_scene` (garden, sky and scenery in the reference framings), `u8_areas` (gate, moving into the side yard, save/load, break menu, settings, new game), `u9_perf` (frame time in heavy framings with CPU/draw counts, the gain and pixel accuracy of the occlusion between areas), `u9_audio` (mower/trimmer/hose sounds, footsteps, birds, feedback sounds, going quiet on a break, the volume setting, recording the mix), `u10_progress` (points, lost items, workshop, end of day and night, rainy night, save, collection), `u10_pattern` (pattern guide, stripe and checker tasks), `u10_tools` (detailed tool models, gamepad button names, rumble), `u11_props` (close framings of the garden props: house, veranda, shed, flowerbed, tree, fence, birdbath, compost, gate), `u14_areas` (the gate chain of the orchard and the vegetable garden, collecting apples, mowing between the trees, watering the dry path, finishing the whole garden, occlusion through the new gate, frame time with the whole garden visible, the grass LOD gain and image difference), `u15_jobs` (job board, three client jobs: delivery, leaving half done and resuming, early delivery; stars, payment, night on coming home, job records, the build time of a garden change), `u16_models` (Blender models: the calibration model's axes in game, wheel size, mower, hose nozzle and trimmer up close and from the player's eye, a measured comparison of the hose framing with the video, the tree), `u16_hose_tuning` (searches for the hose nozzle's grip on the camera by measuring against the video frame: the screen position and size of the head and the knob, the roundness of the head, the direction of the grip), `u16_showroom` (Blender models in game: each on mown grass from six sides and from above, close-ups, from the player's eye; the `Builds/Models/showroom` sheets are made from these).

The garden has four areas that unlock in order through gates: **Back yard** (16 patches) → the **Side yard** to the east (6 patches) → through the side yard's south gate the **Orchard** (8 patches; two rows of apple trees, a long wavy meadow, mowing between the trees) → through the orchard's north-west gate the **Vegetable garden** (5 patches; narrow grass paths between raised beds, a path that starts dry, a bean trellis, a rain barrel, a wheelbarrow). All the garden's data (areas, gates, patches, dry spots, weeds, fences, props, spawn point) is in the definition in `Assets/Opus/Scripts/World/HomeYard.cs`; a new area or gate is added there (`YardDefinition.Validate()` checks consistency; when it changes `HomeYard.Version` is raised and saves migrate area by area). Each patch's tasks show in the strip at the top left: mow, pull the weeds (only the trimmer pulls them; ivy takes longer), water the dry spots (they green up once watered). When all of a patch's tasks are finished for the first time flowers open; when all of an area's patches are finished the next area's gate opens.

**Apples:** while the orchard is unlocked, apples drop under the trees every morning; they rest on top of long grass and drop to the ground once the grass is mowed. Walking over them collects them (+12 points, counted in the day summary).

**Client jobs:** three jobs can be taken from the **Job board** in the break menu: *City garden* (Ms. Selin; small, many obstacles), *Manor lawn* (the Kaya family; wide, wants north-south stripes on the front lawn), *Country house garden* (Uncle Hasan; neglected, many weeds and dry spots). A job takes a day: you travel to the client's garden, no points are earned during the job, and the delivery card opens when every patch is finished. Stars: at least 50% completion gives one, two if everything is finished; three if finished within the target time (and the requested pattern is done). Payment depends on the stars (250–1,200 points). A job can be left half done (it continues where you left off). When you get back it is night at home; the night card also shows the job's result. Points, upgrades, the day and the collection are shared between gardens.

**Days:** choosing "End the day" in the break menu shows the day summary and night falls. Overnight the mowed grass grows back at a different speed per patch (fast patches want upkeep the next day, slow ones two days later), dry spots dry out again and some of the pulled weeds come back. Some nights it rains: the dry spots get watered by themselves and the grass is wet in the morning.

**Points and workshop:** every m² mowed and watered, every weed pulled, patch finished, area unlocked, pattern completed and item found earns points. Upgrades are bought in the **Workshop** in the break menu: wide deck, strong engine, thick line, pressure hose, sprinkler head (a third spray mode).

**The day's pattern:** every day a requested stripe pattern (north-south, east-west, diagonal, checker) shows on one patch. While you are on that patch the requested bands are lightly tinted over the long grass; mowing the bands in the right direction completes the task.

**Lost items:** 12 ordinary garden finds (a glove, a key, a coin, a pine cone, a bird feather…) are hidden in the long grass. Now and then they sparkle above the grass; mowing the grass over one makes it pop out into the **Collection**. New ones are hidden every day.

When a gamepad is used, the button names on screen switch to the gamepad (RT, RB, R3, LB); the mower, the trimmer and the water rumble lightly (can be turned off in the settings).

Save: `%USERPROFILE%\AppData\LocalLow\Baris\Copycat Opus\bahce.json` (automatic every 30 s, when an area unlocks, when the day ends, on a break and on exit; U8 and U10 saves are read too, and when the garden definition changes the state of unchanged areas is kept). The save file name and the save keys inside it (garden, job and item ids) kept their names from the Turkish build so existing saves still load. Settings (look sensitivity, invert vertical look, grass density, fullscreen, vertical sync, gamepad rumble, master volume, effects, ambience) are in the break menu. Scenarios do not touch this save or the settings; the save folder can be changed with `-save-dir <folder>`.

The developer card shows the live fps, the number of tufts and grass chunks drawn. The grass is drawn with two LODs: the tuft count drops with distance, and beyond 18 m (~12 m at low/medium density) a light tuft mesh (8 blades × 2 segments) is used.

## Materials

Objects are drawn not with flat colours but with textures that look hand-painted; no texture files are downloaded, all are generated from code.

- **Patterns** (`Assets/Opus/Scripts/Visual/SurfaceTextures.cs`): siding, wood, vinyl, shingles, brick, concrete, stone, bark, soil, rubber, metal, plastic, leaf, fabric, glass, general paint and the grime mask. `Setup.GenerateContent` (also called by Build.ps1) writes them as `Assets/Opus/Resources/SurfaceTextures/*.png`; when the pattern code changes `SurfaceTextures.Version` is raised.
- **Shader** `Opus/Surface`: triplanar texture without UVs, grime blotches at the bottom, a ground proximity shadow. On Blender models also a hard-surface finish (a dark neutral shadow side, highlights, sky reflection at the edges) and the ambient occlusion baked in Blender (vertex colour); the settings are in `SurfaceLibrary.CreateModel`, the soft painted light of the garden props does not change.
- **Choosing the kind:** `PrimitiveKit` parts are given a surface instead of a colour: `Palette.ShedBlue.As(SurfaceKind.Siding)`; the second value is the pattern scale (`As(SurfaceKind.Siding, 1.75f)` = boards 1.75 times as wide). A plain colour counts as "paint". The pattern, repeat length and grime settings of the kinds are in `Assets/Opus/Scripts/World/SurfaceLibrary.cs`.

## Models (Blender pipeline)

The models are not made by hand but generated from code in Blender with Python; running it again gives the same result. No models are downloaded.

- **Generators:** `tools/blender/models/<name>.py`: `calibration`, `wheel` (textured: small-block tread, rim, cap), `mower` (housing, motor, handle; the parts in their places on the mower, the wheels on the axle pins, the cable from the lever to the engine's brake tab), `hose_nozzle` (textured: pistol nozzle with a rear trigger), `trimmer` (textured: unit-length shaft, body, battery, D handle arch, head puck, line; each moving part its own object), `tree` (textured trunk with buttress root fins and branches, CC0 bark; leaf clusters on the soft foliage material), `tire_swing` (textured: a 3-strand laid rope with every strand modelled, a round turn and two half hitches on the branch, two half hitches on top of the tire, the loop lying on the tire; and the tire), `shed` (tiled, one mesh: lap siding boards, corner and rake boards, shingle rows, framed double door, wedge hinges, recessed window), `house` (tiled, one mesh: two-storey house: siding, recessed sliding windows, doors, gutters and downspouts, deck on joists, railing, lattice skirt behind corner posts, steps, porch; colour variants in game through `ModelLibrary.Whole(..., recolor)`, which swaps the material colour). Shared rules in `tools/blender/opus_model.py`: 1 unit = 1 m; the model's front is -Y in Blender, its top +Z, its right -X (+Z, +Y, +X in Unity); models tied to in-game sizes can write their points directly in Unity axes (`part(unity=True)`); no rotation/scale on parts; material name `<Kind>_<RRGGBB>_<finish>` (e.g. `Plastic_2F2E31_matte`; the kind is a `SurfaceKind` name, the finish glossy/metallic/satin/matte is chosen from the part's real material and the video, not from the kind; `surface()` asks for a reason, shown as a material card under the contact sheet). Helpers: profile revolving (`revolve`, `revolve_at`), ring-by-ring shells (`loft`), bent pipes and cables (`fillet`, `spline`, `sweep`), members whose ends are cut to the faces they meet (`board`: a sloped rake board cut plumb at the wall and the fascia; `qc.joint_audit` fails a build on an end that crosses what it meets), pushing out of a surface (`extrude_outward`: lugs, pockets), boolean carving of a single shell (`carve`, `merge`), edge chamfer + weighted normals, leaf parts (`part(foliage=True)`: double-sided leaves, normals pointing out of the cluster, separate AO), splitting long faces at regular intervals for AO (`part(ao_grid=...)`: so the AO baked into the vertices does not spread the shadow at a long board's end over the whole face), a tone variation per shell (`tone_jitter`), connection points (`anchor`: the Unity test compares them with their in-game counterparts).
- **Textured pipeline (new models):** source materials are built with `model.textured_surface(name, finish, reason, nodes)`.
  - **Sources:** CC0 textures under `tools/blender/source_textures/` (licences in `LICENSES.md`) or Blender nodes; the source UV (`source`) comes from the generator (`sweep(uv_scale=, twist=)`, `revolve_at(uv=)`, `loft(uv=)`).
  - **Baking:** `finish_textured(size, moving=[...])` runs the quality gates (below) first. It joins the static parts into a single object; moving parts stay separate. It unwraps a shared atlas UV (the source UV is cut into pieces, the islands are equalised by 3D area and packed) and measures the texel density (px/m). It bakes base colour, tangent-space normal (OpenGL +Y), roughness, metallic and AO into the atlas with Cycles.
  - **Options:** `cut=1.0` splits long faces first; `pack_shape="AABB"` with a small fixed `margin` packs many box-like islands tightly; `ground=0.0` keeps the gates' cameras above the ground and skips buried faces; `untextured=("Leaves",)` leaves parts out of the atlas (they keep `<Kind>_<RRGGBB>_<finish>` materials and vertex AO: leaves need the soft foliage lighting); `ao_together=False` bakes every part's AO alone (parts that overlap in the file but move apart in game, e.g. the trimmer); `export=False` for checks.
  - **Output:** a single `Atlas_<model>` material; textures in `Assets/Opus/Resources/Models/Textures/<model>/<model>_{color,normal,orm}.png` (orm: R AO, G roughness, B metallic).
  - **Unity:** `ModelLibrary` wires it to the UV path of Opus/Surface (`_OPUS_UVMAP`); tangents are computed with MikkTSpace (`ModelImport`).
  - **Atlas fill:** pixels no face uses take the colour of the nearest used ones (push-pull), so mipmaps never mix in black.
- **Tiled pipeline (buildings):** `finish(join="House", tiled=True)`: one mesh with `<Kind>_<RRGGBB>_<finish>` materials, UVs in metres along each piece's grain (`tile_uv`), AO and a per-board tone in the vertex colour (`ao_grid`, `tone_jitter`). The tiling textures (`Assets/Opus/Resources/Models/Tiling/<set>_{tone,normal}.png` + `tiling.json`: painted wood from CC0 ambientCG PaintedWood009C, shingle rows, concrete, smooth) are written by `write_tile_sets`; every surface of a tiled model names a registered material (`MATERIALS` in `opus_model.py`, `Model.surface(material=...)`): different materials, by type or by context (the house's lap siding, the shed's rough boards, painted trim, cedar decking, two kinds of shingles...), never look alike (another set, or tone 0.1 / bump 0.15 / repeat x1.3 apart), and a material with an owner belongs to that model only; the build and `ModelImportTests.MaterialsFollowTheTextureRules` check it; shingle strips get their UVs and the roof plane's normal from the generator (`om.preset_layers` + `om.preset_faces`: v up the slope from each strip's butt, so the row lines are in the mipmapped texture, not in sub-pixel geometry); in Unity `ModelLibrary` gives such a mesh `SurfaceLibrary.CreateTiled` (Opus/Surface `_OPUS_TILED`). A 4096 atlas over a whole house gave 76 px/m and sub-pixel faces (black lines); tiling stays sharp at any distance and colour variants work by material colour.
- **Quality gates (`tools/blender/qc.py`, both pipelines; a failure stops the build, the numbers go to `Builds/QC/<model>.json`):**
  - **z-fighting:** overlapping faces in one plane facing the same way: must be 0.
  - **Holes:** close cameras (0.25-1.6 m) all over the surface cast rays the way Unity draws (back faces culled); a back face seen first is a hole: must be 0. A camera counts only where it can see the sky; a hit on a shared edge is confirmed with rays shifted 0.1 mm.
  - **Texels (atlas models):** every face over 1 cm² at least 1.5 atlas px across its narrow side.
  - **Joints:** a member's end that crosses the face it meets at an angle (part poking through, part leaving a wedge; the porch's square-cut rake boards) fails (`qc.joint_audit`; lapped parallel boards and edge chamfers are not counted; `checks/joints.py` keeps it honest).
  - **Rope audit (`tire_swing`):** rope-to-rope distance (≥ 1.9 R), the tightest bend (≥ 0.75 diameter), the loop's contact with the tire.
  - **Construction (`qc.connectivity`, `qc.check_links`):** the model is one assembly held by its supports: the ground and the plan's other supports, located with `model.support` (hands on the grips, the branch under the round turn, the wheels on the axle pins). Pieces count as joined through intersecting triangles or contact within 3 mm. A whole object floating above the ground fails; the gate used to take the biggest piece as held. Leaves and `part(free=True)` parts are exempt. Declared links must hold:
    - `model.route(name, centreline points, part=, ends=)`: that piece reaches those parts (the mower's control cable, the swing's hanging rope, the downspouts);
    - `model.attach(name, point, parts=)`: those two parts meet (the nozzle's trigger hinge).
    `checks/plausibility.py` proves, with a broken and a correct case each, that the gate finds what it should and only that.
  - The gates do not see thin slots that look through onto front faces (a riser 5 mm above a tread): the `u16_showroom` close-ups in the built game are part of the check.
- **Plausibility (plan, preview, review):** the `.claude/skills/model-plausibility` skill.
  - Every generator starts with `model.plan(...)`: purpose, sources (video, image, model or text), and per component its role, job, what holds it, whether it moves, seen or guessed. The build checks a load path to a support. The plan goes into the spec and onto the contact sheet.
  - `Models.ps1 -Preview` renders the structure in seconds (house: 11 s instead of 3 min).
  - **Critical features** (`model.features`): the relationships that make the object what it is (the mower's control chain, the swing's branch hitch, the house's stairs carried to the deck).
    - The build measures what it can.
    - The reviewer gives each one a verdict; an unverified critical feature is never counted as passed.
  - **Reference comparison** (`py -3 tools/Compare.py <model>`, landmarks in `tools/blender/references/<model>.json`):
    - solves the frame's camera and reports the alignment (aligned, rough or poor);
    - gives each landmark's leave-one-out error and each segment's length ratio, plus an overlay;
    - only advisory unless aligned; no IoU on perspective frames.
  - **Independent review** of complex models: `tools/Review.py pack` writes one image plus a brief with the pack id; a separate reviewer writes the findings and feature verdicts; `Review.py record` refuses findings for another pack or a changed pack.
    - The review is tied to the model's structure hash; the user's approval to its asset hash (UVs, surfaces, textures within bake noise).
    - `carry` moves a preview review to the final build only when the structure demonstrably did not change.
    - The review is never part of a build. `tools/tests/` covers this logic without Blender.
  - Past mistakes are kept as general rules in the skill's `lessons.md`.
  - A whole-model triangle budget (`total_budget`) sits beside the per-part one; `ModelImportTests.DeliveryCostsAreMeasured` records what each model costs once imported (`Builds/QC/delivery.json`).
- **Static batching:** `YardBuilder.Batchable` leaves meshes over 64,000 vertices out of a prop group's static batch; the joined house came out of it with its triangles scrambled.
- **Rigid body placement:** `om.settle(bodies, colliders)` drops parts instead of laying them out by hand. Bodies are parts such as apples in a crate or stones at a flowerbed edge; colliders are static objects such as the crate or the ground.
  - The Bullet simulation runs until the bodies stop, then their rotations are baked into the vertices.
  - Parts interpenetrating at the start raise an error.
  - Many parts are dropped in waves: the settled ones become colliders for the next wave.
  - The same input gives the same result.
  - `Models.ps1 -Checks` runs its check (`tools/blender/checks/settle.py`).
- **Outside assets:** `model.external("<source>:<id>", finish, reason, size=, axis=, budget=)` imports an asset fetched with `tools/Assets.ps1` as one part of a textured model.
  - It refuses the asset without an `asset.json` or with a licence outside `EXTERNAL_LICENCES`.
  - It imports glTF, FBX or OBJ (never .blend), joins the meshes and applies their transforms. It turns, sizes and centres the part (bottom at z = 0) and decimates it to the budget.
  - It routes the asset's textures through the source UV, so `finish_textured` bakes them into the atlas.
  - It writes an `external` record (licence, authors, page) into the spec file.
  - `Models.ps1 -Checks` runs its check (`tools/blender/checks/external.py`). Unity tests check that licences are allowed and that CC-BY assets have credits in `Assets/Opus/Resources/ThirdPartyCredits.txt`.
- **MCP for Blender (trial):** installed for exploring and quick trials in a live Blender. Permanent models are written in this pipeline. Comparison report: `Builds/MCP_test/tire_swing/report.md`.
- **Validation gate (Blender):** open edges, inside-out shells, zero-area faces, loose vertices, material names, triangle budget. If any fails, no FBX is written.
- **Outputs:**
  - `Assets/Opus/Resources/Models/<name>.fbx`: the model; the game loads it from here.
  - `Assets/Opus/Prefabs/Models/<name>.prefab`: a Prefab Variant of the FBX with the game's materials, for dragging into scenes. Its materials are saved in `Assets/Opus/Prefabs/Models/Materials/<name>/`. Built by `tools/Prefabs.ps1`, which `Verify.ps1` runs before the tests, and by every build; `ModelImportTests.EveryModelHasAPrefab` checks them.
  - `Assets/Opus/ModelSpecs/<name>.json`: sizes, positions, triangles, volume, materials, plan and fingerprints, in Unity axes.
  - `Builds/Models/<name>_contact.png`: labelled views, reference crops and the material and plan cards.
- **3D style guideline** (skill `.agents/skills/art-style`): `tools/blender/style/style.json` (schema 3) and `references.json` store rules, source regions, scope, rationale, uncertainty and user decisions. Current generated models are implementation examples; they do not define the target palette. Draft guidance is not user-approved.
  - `py -3 tools/Style.py bible` generates `Builds/Style/style_guide_ai.md` (detailed AI-readable guide) and `Builds/Style/art_bible.html` (illustrated human report) from the same data, without Blender, model builds or AI calls.
  - `py -3 tools/Style.py brief <name> [--class <class>]` selects relevant rules and at most three reference crops in one numbered board under `Builds/Style/briefs/`. Known model classes are inferred. Read this brief during asset work; open detailed rule IDs only when needed. `StyleGuide.py brief <name> --class <class> --materials <comma-separated materials>` narrows the material scope.
  - `py -3 tools/Style.py extract [images]` now records optional per-image observations. It does not modify target rules or pool unrelated images, and it does not recover unlit albedo from lit screenshots.
  - `py -3 tools/Style.py validate` checks sources and rule provenance. `Style.py report` (in Verify) separates technical checks, advisory thresholds and ordinary asset review state. Passing them does not certify visual style. Retired rules are ignored; a low triangle count is not a style defect.
  - `Review.py pack` includes the scoped brief and selected board, rather than the full guide and legacy hero/near-miss sheets. Use `pack <name> --class <class>` for a new model; registration in `style.json` is optional. With no known class, the pack includes common rules and explicitly marks class-specific style assessment incomplete. Invalid classes or source data still fail, preserving the previous pack. A visual finding identifies its rule, evidence and location. Existing construction review checks remain in place.
  - Incremental updates: `py -3 tools/StyleGuide.py snapshot --out <file.json>` before an edit, then `StyleGuide.py impact <file.json>` lists affected rules and classes. `StyleGuide.py revise <rule-id> --text "<instruction>" --note "<actual user feedback>"` records only the decision the user actually gave. Adding a source uses `StyleGuide.py add-reference <image> --id <id> --roles <comma-separated roles> --scope <comma-separated classes> [--crop x,y,w,h] [--label "..."] [--group "..."] [--note "..."]`; it does not change the rules automatically.
  - Migration: schema-2 colour envelopes and aggregate metrics are archived as legacy data. They no longer gate schema-3 assets. A guide revision does not invalidate a mesh or force a rebuild; approval of an asset does not automatically widen the style. New approved exemplars require explicit provenance and current asset/image hashes.
  - The guideline concerns 3D assets. Runtime lighting/post-processing and 1:1 trailer-frame scoring are separate; `Style.py shots` was removed. `Compare.py` and the Unity screenshot scenarios remain available for model accuracy and game validation. Existing Blender look-dev renders are approximations, not proof of runtime material parity or assembled tool proportions.
  - Reported text-token counts are estimates, excluding images, system instructions, history and tool results. Reuse source analysis, briefs and existing captures. Documentation edits do not require new pilot assets or repeated full reviews.
- **Research as needed:** reuse existing construction notes. Research a specific unresolved question before building it; avoid repeating research for a known object or material-only change.
- **Unity:** `Assets/Opus/Editor/ModelImport.cs` fixes the import settings (scale 1, file units, the axis conversion not baked into the root, normals from the file). `ModelImportTests` compares every model with its spec file (±1 mm bounds, position, no rotation/scale, triangle count, normal–winding agreement, volume, materials); the calibration model additionally proves the meaning of the front/right/up axes. In game `ModelLibrary.Part("wheel", "RearWheel", parent)` builds a part and turns the material names into Opus surfaces (painted texture, grime).

## Opening in the editor (optional)

Add the folder to Unity Hub, open the `Assets/Opus/Scenes/Main.unity` scene and press Play. The **Opus** menu has the configuration, content generation and build commands.
