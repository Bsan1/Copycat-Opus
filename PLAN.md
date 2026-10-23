# Copycat Opus — Development Plan (Unity, focused on lawn care)

**Goal:** recreate the **lawn care mechanics** (mowing, trimming, watering, show remaining, finishing patches) and the **look** (dense stylised grass, a colourful patch quilt, a pastel/hazy atmosphere, a paper-card interface) of the game in the reference video as closely as possible.

**Out of scope:** children, family, toys, memory scenes, keepsakes, years/ages, the end-of-summer photo. These will not be made.

**Models:** for now simple geometry built by hand in code (cubes, cylinders, simple meshes we generate). Detailed models later.

**Engine:** Unity 6 (installed: `6000.3.11f1`), URP. Development happens **without opening the editor, through the Unity command line (batchmode)**.

Source video: `copycat game.mp4` (the 18 Summers trailer). Reference frames: [docs/reference/](docs/reference/), HUD close-ups: [docs/reference/hud/](docs/reference/hud/).

---

## 1. What to take from the video (only the lawn and visual side)

### 1.1 Core loop
1. The garden is split into organically shaped **patches**; every patch has its own grass colour/variety → the garden looks like a colourful **quilt** (yellow, straw, grey-blue, lime green areas side by side with sharp borders).
2. Each patch's tasks: **Mow %**, **Weeds: N left**, **Water %** (and automatic edge trimming).
3. When a patch is finished: a sparkle effect + `<Patch> – Done / All done ✓`.
4. When all patches are finished the level is complete → (for us) **a new area unlocks**.

### 1.2 Tools
| Tool | Seen in the video | Ours |
|---|---|---|
| **Mower** | A red mower in front of the camera, the handle coming in from the bottom corners of the screen. It cuts long grass leaving a short, light trail; clippings fly; the long grass in front of it leans aside. It does not enter obstacles → uncut strips are left at fence/flowerbed bases. | The same + a light/dark **stripe** pattern by mowing direction. |
| **Trimmer** | A string trimmer in hand. It cuts the strips at the fence base and flowerbed edges and the weeds, throwing dark debris. It clears ivy/bush clusters. | The same. |
| **Hose** | A curved soft white-blue water stream, a **dashed ring** where it lands, splashes. Dry straw-coloured spots turn green. `Hose / Full`, `RB to change spray`. | The same; 2 spray modes (Wide / Narrow). |
| **Hand (interaction)** | Interacting with objects. | For opening gates, unlocking areas etc. |

The toolbar is at the bottom left: 4 square icons, the selected one highlighted green, the tool's name above it, a shortcut hint below it (`LB next tool / hold LB wheel`).

### 1.3 How the grass looks
- **Long grass**: thin, long, dense blades; dark at the base, light at the tips; wave-like bands of shine in the wind.
- **Mowed grass**: short, dense, **bright lime green** (lighter and more vivid than long grass). A dry area once mowed is short brown-straw.
- **Dry spot**: long, straw-coloured; when watered it greens up from the centre outwards, dark/wet green at first.
- **Grey-blue grass**: the cool, pale long grass of some patches.
- **Show remaining** (`Press R3 to highlight`): the remaining undone area flashes **bright cyan** for a moment.
- **Patch border**: an **orange dashed line** on the ground around the active patch; orange markers in the air for the remaining weeds.
- **Finish effect**: a cyan wave + yellow sparkle sweeps the area, then small white/yellow/lilac flowers on the grass.

### 1.4 Atmosphere and style
- A low-contrast, pastel, slightly "washed-out" colour scheme; soft bloom.
- **A lavender-pink sky**, watercolour clouds, orange/teal tree clusters and conical pines in the distance that look painted.
- Strong **distance fog**: everything far away blends into lavender-white.
- Garden: a grey two-storey house + veranda/stairs, a white vinyl fence, a blue-grey shed, flowerbeds (purple flowers, hostas), a big tree.

### 1.5 Colours measured from the video (starting palette)
Approximate values because of video compression and fog; fine-tuning will be done by screen comparison.

| Item | Colour |
|---|---|
| Long grass (yellow) | `#A89E31` – `#B5A451` |
| Long grass tip light | `#B9B161` |
| Mowed grass (lime) | `#ABBF4F` |
| Dry spot (long) | `#AC9558` |
| Dry area mowed | `#AC8B66` |
| Watered green | `#95B24B` |
| Grey-blue grass | `#8A9087` |
| Sky/fog lavender | `#C8BABE` |
| Fence white (hazy) | `#C7C0BE` |
| Highlight cyan | `#81CAC8` |
| Interface paper cream | `#F3ECDF` |

### 1.6 Interface (only the parts related to lawn work)
- **Top left**: a circular ring counter `Garden 10/46` (finished/total patches) + a strip: a **patch-specific coloured pill** (every patch has a colour: orange, teal, purple, olive…), the patch name (`???` if unknown), task rows (`Mow ▬▬▬ 96%` / `Weeds ✎✎ 1 left` / `Water ▬▬ 52%`), `Show remaining: H` on the right.
- **Top right**: area name/progress and an objective text ("Complete every patch to finish this area").
- **Bottom left**: the toolbar. **Bottom centre**: a contextual prompt strip (`Hold: water / R: change spray`).
- Style: cream paper cards, washi tape on top, a soft shadow, rounded corners, a **handwritten font**.

---

## 2. Technical approach

### 2.1 Environment (checked)
- Unity `6000.3.11f1`: `C:\Program Files\Unity\Hub\Editor\6000.3.11f1-x86_64\Editor\Unity.exe`, licence active.
- Modules: Windows (Mono + IL2CPP), WebGL, UWP.
- Hardware: RTX 4060 Laptop, i7-13620H, 32 GB, 1920×1080 @144 Hz. Target: **at least 60 fps** at 1080p, preferably 144.
- Note: on the laptop the game is slow if it opens on the Intel UHD; the exe may need "High performance" in the Windows Graphics settings.

### 2.2 Working without opening the editor (Unity CLI)
The Unity project sits at the repository root (`Assets/`, `Packages/`, `ProjectSettings/` → next to `PLAN.md` and `docs/`; Unity does not touch these).

| Step | Command (summary) |
|---|---|
| Create the project | `Unity.exe -batchmode -quit -createProject <root> -logFile Logs/create.log` |
| Packages | `Packages/manifest.json` written by hand: URP, Input System, uGUI (including TextMeshPro), Test Framework, Mathematics/Burst/Collections |
| Project settings | `-executeMethod Opus.Editor.Setup.ConfigureProject` → URP asset, quality, colour space (Linear), Input System, player settings |
| Compile check | The project is opened in batchmode, `error CS` is searched for in `Logs/*.log` |
| Tests | `-runTests -testPlatform EditMode -testResults Logs/editmode.xml` (+ PlayMode) |
| Game exe | `-executeMethod Opus.Editor.Build.Windows` → `Builds/Windows/CopycatOpus.exe` |
| Visual check | `tools/Shots.ps1 -Scenario <name>` → the exe runs off screen, plays a scenario, takes screenshots including the interface, quits |

- All of it is wired to `tools/*.ps1` scripts (`Build.ps1`, `Test.ps1`, `Shots.ps1`).
- **Code-first scene**: the scene file is almost empty; the garden, props, light and interface are built from code at runtime. So everything can be edited as text, without the editor.
- **Screenshot automation**: the scenarios set up camera poses similar to the reference frames (e.g. "ref01: entering yellow grass with the mower") and take screenshots including the interface (Canvas "Screen Space – Camera" + a URP Overlay interface camera, the world camera draws into a RenderTexture; since the window is off screen the user sees nothing). Then I put the screenshots **side by side** with the reference frames and compare.
- Constraint: batchmode does not work while the Unity editor has this project open (project lock). You open the exe to play; if you need to open it in the editor, I do not build in the meantime.

### 2.3 Grass system (the heart of the project)
**Lawn state (LawnState)**
- A grid with ~8–10 cm cells over the garden (e.g. 40×40 m → 500×500 cells).
- A byte array on the CPU (master data) + a `Texture2D` on the GPU (changed cells are uploaded):
  - R = height (long → mowed), G = dryness, B = mowing direction (stripes), A = flags (highlight, flower, wetness time etc.)
- A static **patch id texture** + a patch palette (every patch's long/tip/mowed/dry colours).
- Progress counters are kept incrementally (the brush counts the cells it changes, the whole map is not scanned every frame).

**Rendering**
- No buffer per blade: with `Graphics.RenderMeshPrimitives` the GPU draws N tuft instances for every **chunk (4×4 m)**; the vertex shader generates the tuft's position from the R2 low-discrepancy sequence and its rotation from a hash, and reads height/dryness/patch colour from the state texture.
- Tuft mesh: 12 blades, each a 4-segment tapered strip (9 vertices).
- Density by distance (since prefixes of the R2 sequence are evenly spread, the first K instances are drawn and the tufts at the threshold shrink away) + chunk frustum culling; the thinning is not noticeable because the ground shader is painted with the same texture.
- Light is computed per vertex (shadows included); enough at blade scale and very cheap.
- **Ground shader**: carries the grass colour so dark soil does not show between the blades.
- A custom URP HLSL shader: soft toon light, shadow receiving, a base-to-tip colour gradient, translucency at the tips, URP fog.
- **Wind**: noise sliding in world space + wind wave bands (the shine waves in the video).
- **Pushing**: blades lean outwards around the mower/player.
- A density setting (Low/Medium/High).

**Brushes and effects**
- Mower: a ≈50 cm circle; since the body cannot enter obstacles, edge strips are left **by themselves**; clipping particles by the number of cut cells.
- The cut does not "pop" instantly; the height drops over a short time.
- Stripes: cut blades lean in the mowing direction → light/dark bands depending on the light.
- Trimmer: ≈20–25 cm, reaches all the way to an obstacle's base; breaks weeds and ivy; dark debris particles.
- Hose: a Bezier water tube from the nozzle to the target (a sliding, translucent shader), a dashed ring + splash at the target; the dryness inside the ring drops over time; wet cells stay dark green for a while.
- Highlight: the remaining cells flash cyan in the shader.
- Patch border: a dashed orange strip mesh sitting on the ground, from the patch's polygon.
- Finish: a cyan + yellow wave, then instanced small flowers.

### 2.4 Other systems
- **Player**: `CharacterController`, a first-person camera (wide FOV ~75). With the mower slower, with inertia and heavy turning; the camera looks slightly down; head bob.
- **Tools**: a shared `Tool` interface; keys 1–4, Q/E, the wheel, holding Tab = tool wheel; when switching, the model goes down off screen and comes back up.
- **Patches**: organic polygons defined in JSON (name, colour, tasks, grass variety). Active patch = the unfinished patch the player is in/nearest to.
- **Obstacles**: house, fence, shed, flowerbed, tree → collider + grass mask (no grass under them).
- **Save**: JSON (`Application.persistentDataPath`), the lawn state compressed.
- **Input**: Input System; keyboard/mouse first, gamepad support later with the same action map.
- **Interface**: uGUI + TextMeshPro; the paper card look with 9-slice textures generated in code.

### 2.5 Folder layout
```
Assets/Opus/
  Scripts/
    Core/      GameBootstrap, GameState, Save, Settings, ScenarioRunner (automated tests/screenshots)
    Player/    PlayerController, CameraRig, ViewModelSway
    Lawn/      LawnState, LawnBrush, GrassRenderer, GroundRenderer, PatchMap, LawnProgress, Clippings, Flowers
    Tools/     Tool, ToolBelt, Mower, Trimmer, Hose, Hand
    World/     YardBuilder, PrimitiveKit (simple mesh generation), Obstacles, Gate/AreaUnlock
    Visual/    SkyBuilder, PainterlyBackdrop, PostFxSetup
    UI/        Hud, Toolbar, ToolWheel, PromptBar, PaperCard
  Resources/Shaders/  Grass.shader, Ground.shader, Sky.shader, WaterStream.shader, DashedLine.shader, Highlight.hlsl
  Data/        yard_01.json (patches, props, colours)
  Editor/      Setup.cs, Build.cs, ShotTools.cs
  Tests/       EditMode/, PlayMode/
tools/         Build.ps1, Test.ps1, Shots.ps1
docs/reference/
```

---

## 3. Progression / unlocking new areas — options
Only options for now; **A** is done in the first version, we decide the rest after playing.

- **A. Unlocking areas through gates (suggested start):** when all patches of an area are finished the gate in the fence opens and you move to the next yard. A chain such as front yard → back yard → side strip → the neighbour's yard.
- **B. Job/home model (PowerWash Simulator style):** every level is a different house's garden; when finished, stars/points, the next house unlocks.
- **C. Day cycle + regrowth:** the grass grows back over days, dry spots come back; a relaxing upkeep routine.
- **D. Tool upgrades:** a wider mower deck, a faster trimmer, a longer hose, a new spray head with money/points.
- **E. Pattern mode:** free/rewarded levels based on drawing a requested stripe pattern (diagonal, checker).
- **F. Small items found in the grass:** collections such as a ball, a key, a glove (no story, just collecting).

---

## 4. Stages and "done" criteria
At the end of every stage: build + tests + automated screenshots + a side-by-side comparison with the reference; you play the exe and give feedback.

| # | Stage | Counts as done when… |
|---|---|---|
| **U0** | **CLI skeleton**: create the project, packages, URP + Linear + Input System settings, an empty code-first scene, `Build/Test/Shots` scripts, the scenario/screenshot infrastructure | One command outputs the exe, a scenario produces a PNG including the interface, a sample test passes. |
| **U1** | **Player + mower feel**: FPS controls, a simple mower model (body, engine, wheels, handle), inertia, collisions, camera framing | The framing matches reference frame 01; walking with the mower feels heavy and smooth. |
| **U2** | **Grass rendering**: LawnState, GPU-instanced blades, chunks/LOD, ground, wind, the patch colour quilt | ≥60 fps at 1080p; the long grass looks like the reference. |
| **U3** | **Mowing**: brush, soft cut, stripes, clippings, pushing, edges left by themselves | Mowing a stripe looks like the video frames 1:03–1:14. |
| **U4** | **Trimmer + weeds + ivy** | The fence base and flowerbed edge can be cleaned; the weed counter goes down. |
| **U5** | **Hose + dryness**: water tube, ring, splash, greening, wet look, 2 spray modes | A watered dry spot greens up as in the video. |
| **U6** | **Patches + HUD**: JSON patches, progress, top-left/top-right cards, toolbar and wheel, prompt strip, highlight (H), orange border, finish effect + flowers | When a patch is finished the counter goes up, the effect plays, the interface has the same layout as the reference. |
| **U7** | **Garden + atmosphere**: a simple house, fence, shed, flowerbeds, tree; lavender sky, watercolour cloud/tree cards, fog, post-process (bloom, contrast/colour, vignette); a colour tuning pass | Screenshots placed side by side with the reference frames have the same mood. |
| **U8** | **Unlocking areas + saves + menus**: a second area through a gate, save/load, pause, settings (graphics density, mouse sensitivity, sound) | When the first area is finished you move on to the second; the save restores. |
| **U9** | **Sound + polish + performance**: mower engine (pitch by load), trimmer, water, birds/ambience; profiling and optimisation | A playable, polished demo. |
| **U10+** | The chosen progression models (§3), gamepad support, detailed models | — |

---

### Status
- **U0 done (18 October 2026):** project set up through the CLI (URP 17.3, Linear, the new Input System only, TMP), a code-first scene, a placeholder garden (house, fence, shed, flowerbed, tree, pines), a lavender sky + fog + stylised light shader, a paper card interface (Patrick Hand, Turkish characters at the time), `tools/Build.ps1` · `Test.ps1` (23 tests pass) · `Shots.ps1`. Frame time ~1.9 ms at 1080p in the simple scene.
- **U1 done (18 October 2026):** the input layer (keyboard/mouse + gamepad + scenario simulation), the tool system (slots, lower/raise switching), a hand-built mower model, movement profiles (mower: 2.1 m/s, 2.8 m/s² start, ~0.5 m coasting, delayed turning; walking: 3.4 m/s), the mower housing not entering obstacles (an unmowable strip is left by itself at fence/flowerbed bases), engine vibration, leaning in turns, spinning wheels. The framing was chosen by measuring side by side with the reference: with the mower **FOV 52°, pitch 40°** (60° while walking). Scenarios can run step by step and do checks (`u1_mower`: 12 checks); 38 unit tests pass.
- Bug caught in U1: at high frame rates the `CharacterController`'s 1 mm threshold swallowed the movement (`minMoveDistance = 0`).
- **U2 done (18 October 2026):** a 10 cm lawn grid (initially 360×300 cells: height, dryness, patch id; 480×300 in U8), a 16-patch organic "quilt" (Voronoi warped by noise, 6 grass varieties, colour and height differences between varieties), 1 dry patch + 4 dry spots, no grass under obstacles automatically. Grass: 12-blade tufts drawn with `Graphics.RenderMeshPrimitives` in 4×4 m chunks; placement, blade arc (length-preserving bend), combing direction, wind bands, mower/feet pushing and colour in the shader. Density drops with distance without gaps through the R2 sequence. The ground takes its root tone from the same data. ~870 thousand blades at full density at 1080p in **2.1 ms/frame**. Scenario `u2_grass` (10 checks), 52 unit tests.
- **U3 done (18 October 2026):** the blade circle (r 0.28 m) mows by being swept every frame; the logical cut is instant (patch counters, mowing direction), the visible height drops smoothly in 0.14 s. Mowed grass is bright lime green; light/dark **stripes** by the last mowing direction. Clippings with URP Particles/Simple Lit in the patch colour (my own particle shader could not take the vertex colour, removed). Load in long grass: up to 10% slowdown + vibration. A ~10 cm unmowable strip is left at the fence base. Scenario `u3_mow` (7 checks), 62 unit tests.
- **U4 done (18 October 2026):** a string trimmer (red-black body, long shaft, D handle, a head with an orange cap; the shaft stretches to the hands every frame). The head comes down where the gaze meets the ground (0.8–1.8 m), cannot enter obstacles, but the cutting circle (r 0.2 m) reaches the base → the strip the mower leaves at fence/flowerbed bases gets cleaned. Holding the button spins the line, cuts the grass (writes no stripe direction), throws grass and dark weed debris. 13 weeds (bushy, flowering, taller than the grass; most at bases the mower cannot reach) and 3 ivy clusters at the shed's base: only the trimmer pulls them (weed ~0.35 s, ivy ~1.4 s, thinning leaf by leaf); the mower only flattens them when passing over. Scenario `u4_trim` (7 checks), 68 unit tests.
- Known cost: the weed/ivy leaves were separate objects (~1,000); frame time in the grass scene 2.1 → 3.7 ms. To be merged into a single mesh if needed.
- **U5 done (18 October 2026):** a hose with a pistol nozzle (green body, yellow grip, black mouth; seen from the side at the bottom right). The target is where the gaze meets the ground and stops in front of obstacles (the fence). While the left button is held the water curls from the mouth to the target (a Bezier tube, translucent, sliding flow lines) and splashes there; a dashed ring visible on top of the grass on the ground. Watering removes dryness independently of the frame rate (full at the centre, falling off towards the edge), independently of mowing. A watered spot looks dark and wet for ~9 s; a watered dry spot permanently turns fresh green. With R / RB **Wide** (near, wide, 0.85 m) ↔ **Narrow** (up to 6.5 m, 0.38 m, strong) spray; the prompt strip and tool name ("Hose / Wide") update. Scenario `u5_hose` (11 checks), 80 unit tests.
- **U6 done (18 October 2026):** a task list for each of the 16 patches: mow (98%), weeds/ivy (if any), water (if it has ≥0.4 m² of dry area, 95%). A patch's name is revealed at the first real work (≥1 m² mowed, 5% watered or one weed), `???` until then. HUD in the reference layout: at the top left a "Garden 3/16" badge + a progress arc and the active patch's strip (coloured pill, name, "H: show remaining", Mow/Weeds/Water rows; when finished "– Done / All done ✓"), at the top right a taped area card (sprout icon), at the bottom left the toolbar (the icons are drawn from the tools' own models, the selected slot sage green), at the bottom centre the prompt strip. Active patch = where the tool works (blade/trimmer head/water target). On the grass the active patch's **orange dashed border** (the dashes from the walking distance along the border), a cyan wave showing the remaining work in the active patch with **H / R3**, orange markers over the weeds in the active patch. When a patch is finished a cyan wave + yellow sparkle spreads from the centre, then white/yellow/lilac **flowers** open. When all patches are finished the area is complete (the gate in U8). F3 developer metrics, F8 finishes the active patch. Scenario `u6_hud` (15 checks), 89 unit tests.
- Two important bugs found in U6: (1) `Shader.SetGlobalColor` does not convert sRGB → linear; all global colours looked washed out → `ShaderColors` (the atmosphere tones were rewritten keeping their look; the dry spot now has the same tone as the reference). (2) The build strips the instancing shader variants requested by materials created at runtime → the flowers were invisible; an instanced material asset is generated in Resources.
- **U7 done (18 October 2026):** a watercolour sky (domain-warped fBm clouds, light on the side facing the sun, paint pooling at the edges, paper texture, horizon fog, slow drift). Outside the garden 82 watercolour tree cards painted from code (orange, mustard, teal, green; haze with distance, a soft edge with MSAA) and a dark cypress row to the north. Simple but rich models (`YardProps`): a vinyl fence with board seams and rails, a house with siding, shutters and a chimney; a veranda with a railing, a lattice skirt, stairs, a grill; a shed with plates and X doors; a stone-edged flowerbed with purple spikes, hostas, ferns; a tree with root flares, branches and a tire swing. Every group is merged into a static batch; the collision boxes keep the previous sizes. Post-process (Volume): soft bloom, low contrast, lavender shadow / warm light split toning, a light vignette, −0.25 EV. **Colour tuning was done by measurement:** the scene was ~15% brighter than the reference; the exposure, sky/horizon, house and grass varieties were pulled to values sampled from the reference frames (grass average `#B9AD5D` ↔ reference `#B2AE53`). The interface is not affected by post-process (the card cream is exactly `#F3ECDF`). Scenario `u7_scene` (7 reference framings, 4 checks), 93 unit tests; the heaviest scene 3.9 ms (~260 fps).
- **U8 done (18 October 2026):** the garden was split into two areas: **Area 1 "Back yard"** (36×30 m, 16 patches) and to its east **Area 2 "Side yard"** (12×30 m, 6 patches; birdbath, compost bin, a dry spot, 4 weeds + 1 ivy). The lawn grid is 480×300 cells; patches only take cells of their own area (the two sides of the fence do not spill into each other). In the fence between them an arched, framed, Z-braced **gate**: it cannot be passed while closed; when area 1's last patch is finished it opens towards the side yard in 1.6 s with a "Gate opened!" banner. Patches of a locked area (even if watered over the fence) are not discovered/finished. The HUD changes with the current area (the top-right card "Side yard | Area 2", the badge and counter for that area's patches, the objective text: finish → "The gate is open, the Side yard is waiting" → "The whole garden is done!"). **Save/load:** a single slot `bahce.json` (the persistent data folder; the file name kept its Turkish name so existing saves still load), the lawn state 6 bytes per cell (mowed/watered flags, dryness, stripe direction, greening) gzip+base64 → ~8 KB with both areas mowed; the weeds' health, discovered/finished patches, unlocked areas, the gate, the player's position/look, the tool and the spray mode. Written to a temporary file first; a save whose version and patch map digest do not match is rejected (the state is not corrupted). Autosave every 30 s, when an area unlocks, on a break and on exit; on launch it continues from the save. F5 quick save, F9 quick load. **Break menu** (Esc / Start): the game stops, the cursor is free; Continue, Save, Load, Settings, New game (confirmed), Quit; navigated with mouse and gamepad. **Settings** (PlayerPrefs, applied instantly): look sensitivity, invert vertical look, grass density (Low/Medium/High), fullscreen, vertical sync. The volume setting came with the sound in U9. Scenarios do not touch the user's save and settings (a temporary folder, changeable with `-save-dir`). Scenario `u8_areas` (20 checks: gate blocking, opening, passing, mowing in the side yard, save → mess up → load exactly, 0 mm drift on a break, settings, new game, the whole garden), 102 unit tests; all scenarios (10 scenarios, 86 checks) pass. 3.5–4.8 ms at 1080p with both areas in bloom. The heaviest measurement is now 6.8 ms (the U6 flower framing; it was 3.9 ms in U7): the side yard's grass behind the fence is drawn too, and the measurements vary ±30% from run to run. To be looked at in U9's performance pass.
- Bug found in U8: the collision boxes of decorations built with `collider: false` were deleted with `Destroy` at the end of the frame; since the lawn's "under obstacle" scan ran in the same frame, these pieces cleared the grass too (a soil strip at the gate threshold). The box is now disabled immediately.
- **U9 done (18 October 2026):** **the sounds are fully synthesised from code** (no ready-made sound files, no copyright risk; `Assets/Opus/Scripts/Audio`). The mower engine is a single-cylinder four-stroke model: firing pulses pass through exhaust/housing resonances, with blade hum and mechanical ticking on top; in long grass the revs drop 5–8%, a heavy "load" layer comes in, the blade's grass-chewing sound follows the mowed cells. The trimmer is a two-stroke: idling in hand, revving up in 0.25 s on the trigger, dropping on release; line hits follow the cut grass. Hose: a wide hiss ↔ a narrow jet, splashing on the ground, a plastic click when switching spray. Footsteps in the grass (quieter with the mower), a 16 s wind loop (leaf rustle in gusts), 10 bird calls (chirp, trill, whistle, sparrow, dove) positioned in 3D on 11 perches in the surrounding trees. Feedback: a patch chime (not on an area's last patch, where the area arpeggio plays), a discovery "pling", a gate latch + hinge creak (in 3D from the gate), weed pulling, tool switch, menu open/close/click. The game sounds stop in the break menu. **Master / effects / ambience** volumes were added to the settings. A peak limiter at the end of the mix; the same one-shot sound does not play again within 0.08–0.4 s (so consecutive finishes do not stack up and clip). `tools/Sounds.ps1` writes all sounds as WAVs; scenarios make no sound through the speakers, `u9_audio` records the mix as `mix.wav`. **Performance:** measuring infrastructure (CPU main thread, batch/setpass/triangles; the GPU time cannot be read reliably on this driver). The weeds and ivy were merged into a single mesh per material (ivy leaves shrink by the submesh index count): the same framing **4,022 → 593 batches**, frame 8.6 → ~6 ms. **Occlusion between areas:** since the eye (1.62 m) is below the fence top (1.9 m), the other area's grass and flowers are only drawn if visible through the gate opening (chunks touching the fence line are always drawn); looking east 21 fewer chunks, ~1.5 ms shorter frames; the occlusion on/off screenshots are pixel for pixel identical in frozen time. Flowers are grouped per area; once all are open their matrices are cached. Scenarios `u9_perf` (9 checks), `u9_audio` (14 checks); in total 12 scenarios / 109 checks, 118 unit tests. Another game (GPU 59%) was open on the machine during the measurements; even so every framing was ≥109 fps at 1080p.
- Bugs found in U9: (1) the first version of the occlusion also removed blades that bent in the wind through the thin fence board to the other side; the pixel comparison caught it. (2) In a scenario where 15 patches of an area finished one after another, 15 chimes stacked up and drove the mix to 1.8; with the repeat limit and the limiter the peak is 0.42.
- Postponed: a distance LOD for the grass tuft (fewer blade segments). Since the GPU time could not be measured, the gain could not be verified.
- **U10 done (19 October 2026):** the chosen progression models (§8-4) and detailed tool models. **Day cycle + regrowth (C):** "End the day" in the break menu → a day summary card (m² mowed/watered, weeds pulled, patches finished, items found, points earned) and night. The grass now carries a partial height: overnight mowed grass grows at a different speed per patch (multiplier 0.8–1.35), once past the height threshold (64/255) it counts as needing mowing again and its stripe direction is cleared; dry spots dry out again, 30% of the pulled weeds come back. Patches go back and forth between "done" ↔ "needs upkeep"; area unlocking and flowers only depend on the first finish (the gate does not close). ~25% of the nights are rainy (never the first): the dry spots get watered by themselves, the grass is wet in the morning. Everything is seeded with the day number. **Points and workshop (D):** per m² mowing 1 / watering 2, weed 8, ivy 20, patch 40, area 250, item 60, pattern 150–300 points. Five upgrades in the workshop: wide deck (blade circle and housing 15%/30%, the model widens), strong engine (no slowdown in long grass, 8%/16% faster), thick line (25%/50% wider, weeds 1.5/2 times faster), pressure hose (25%/50% power, narrow spray +1/+2 m), sprinkler head (a third spray mode "Rain"). **The day's pattern (E):** every day one patch in the unlocked areas asks for north-south/east-west stripes, diagonal stripes or (after the 3rd day) a checker; while the player is on that patch the requested bands are lightly tinted over the long grass (a shader guide), and it is scored when the cells' last mowing direction matches the requested one (80%, 70% for the checker). **Lost items (F):** 12 ordinary garden finds (a glove, a key, a coin, sunglasses, a seed packet, a pine cone, a snail shell, a bird feather, pruning shears, a bottle cap, a button, a pencil; no toys), at most three a day hidden in the long grass (0.8 m away from obstacles), sparkling now and then; once the grass over one is mowed it pops out into the collection. "End the day", "Workshop" and "Collection" pages in the menu. **Gamepad:** button names by the last used device (RT/RB/R3/LB ↔ Left click/R/H/Q-E), tool/highlight/spray on the D-pad, mower load/trimmer/water rumble and short pulses (can be turned off in the settings). **Detailed tools:** on the mower an edge lip, a side discharge chute, height levers, treaded tires and rims, a vented engine cover, a ribbed starter cover, an air filter, an exhaust, a foam grip, a control bar, a cable and the starter rope (177 parts → 7 renderers once merged); on the trimmer a battery, a grip area, a safety, a guarded head; on the hose a spray dial, finger grooves, a coupling. Save version 2 (day, points, upgrades, collection, pattern, grass height); U8 saves are read. Writing the save retries against temporary file locks on Windows. Scenarios `u10_progress` (10 checks), `u10_pattern` (3), `u10_tools` (4); in total 15 scenarios / 126 checks, 133 unit tests. With no other application using the GPU, the heaviest framing is 3.9 ms (~258 fps) at 1080p.
- Problems found in U10: (1) in an edit made with perl the `$"` at the start of a string was taken for a perl variable and deleted (the compiler caught it); for C# edits the Edit tool is preferred over perl. (2) On merged models the tool icon studio only turned the first submaterial into ink.
- **U11 done (19 October 2026): detailed models of the garden props.** The reference frames (shed, swing, flowerbed) and the current look were put side by side and every prop was reworked; the colliding bodies kept the same sizes. **House:** a foundation band, shingle rows on the front roof, gutters and downspouts (with elbows), four-pane windows and pediments, a four-panel door, a brass knocker, a door lamp, mortar lines on the chimney; on the veranda four columns (base, capital), a beam and a shingled roof rising towards the house. **Shed** (as in the reference): a gable roof (two dark faces, shingle rows, a ridge, white edge boards), siding and a vent louvre in the gable triangle, horizontal lap siding on all four walls, a skirting board and corner trim, a white double door with black strap hinges and handles, a four-pane side window and a sill. **Fence:** pyramid caps and skirts on the posts. **Gate:** an arch with uprights, pyramid caps, a ring handle. **Flowerbed:** a round-cornered white curb (with a cap), mulch lumps, marigolds and ornamental grass added (five kinds), a white lattice at the back with ivy climbing on it (leaves, purple flowers). **Tree:** a tapering, curving trunk (the colliding cylinder is invisible), bark strips, two-step roots, main branches with side branches, a clustered crown with a dark underside; on the swing a knotted rope and a treaded, real-size tire. **Birdbath:** a stepped plinth, a grooved stem, a neck, a bowl with a ring rim, water, a small stone bird on the rim, moss. **Compost:** lumpy filling, dry leaves and colourful scraps inside an invisible colliding body, a lid leaning at the back, a pitchfork beside it. Scenario `u11_props` (11 close framings + frame time). All 16 scenarios and 133 unit tests pass; the batch count rose by ~100, the frame time did not change (overall framing ~3.4–3.9 ms).
- **U12 done (19 October 2026): materials and textures (§9.3).** A response to the user's "it looks like plastic" feedback. **Textures:** `SurfaceTextures` paints 17 tiling patterns from code (periodic value noise, fBm, cellular noise; no downloads): paint, lap siding, vertical boards (joints, chamfers, knots), vinyl, shingles (staggered tabs), brick, concrete (pores, cracks), stone, bark (sharp-edged dark paint blotches), soil/mulch, rubber, painted metal, plastic, leaf clusters, fabric, glass (diagonal reflection bands) and the grime mask. The patterns are "tone multipliers" (0.5 = the palette colour); light cues such as joint shadows and shingle lips are painted into the texture (instead of a normal map, closer to the hand-painted style). `Setup.GenerateContent` writes them as `Resources/SurfaceTextures/*.png` (linear, mipmapped, version-stamped; not rewritten unless the code changes). **Shader `Opus/Surface`:** triplanar mapping without UVs in metric object space (on the statically merged garden props this is world space, on tools the texture turns with the part), on shingles the top projection follows the slope (rows parallel to the ridge, the light cue always pointing up the slope), a large-scale tone variation, sharp-edged brown-grey grime blotches and streaks at the bottom (like the shed in the reference), a continuous darkening at the very bottom, a contact shadow up to 35 cm, rim light 0.12 → 0.04; the lavender shadow, wrap light and fog unchanged. **Catalogue:** `Surface` (kind + colour + scale; a plain colour becomes "paint" by itself), `SurfaceLibrary` (pattern, repeat length, grime strength/height, contact shadow by kind), `PrimitiveKit.MaterialFor(Surface)`; a `PrimitiveKit.DefaultKind` scope for small items and plants near the ground (lost items stay clean and stand out, plants do not get grimy). **Applied to:** the house (28 cm siding, shingles, a brick chimney, a concrete foundation, a wooden door, glass, louvred shutters), the veranda (wooden decking and steps, a shingled roof), the shed (16 cm siding, shingles, wooden doors, metal hinges), the fence and gate (vinyl, metal hinges/ring), the flowerbed (concrete curb, soil, mulch, plants), the tree (bark, leaf crown, rubber tire, fabric rope), the birdbath (stone, glass water), the compost (one siding row per slat, wooden posts, soil), the tools (painted metal deck, plastic bodies, rubber tires/grips), the weeds, the lost items and the cypresses in the background. The line boxes the texture now draws were removed (house and shed siding battens, shingle rows, mortar, deck lines, door board lines, bark strips). Compared with the reference crops (shed, the tree with the swing) and tuned in two passes: the leaf pattern softened, the bark moved from contour lines to paint blotches, the grime darkened and sharpened. All 16 scenarios and 189 unit tests pass (new: pattern determinism, seamless tiling, mean tone, the assets existing, the catalogue, material sharing, the kind scope, the shader). The batch count did not change; `u11_props` 4.42 ms in three consecutive measurements (4.35 ms in U11).
- **U13 done (19 October 2026): data-driven garden and multi-gate areas (§9.4).** **Definition:** `YardDefinition` (plain data): id and version, cell size, areas, a **gate list** (`GateDef`: from which area to which, hinge, width, fence direction X or Z), patch seeds and names, dry spots, weeds, fence lines, prop placements (house, shed, flowerbed, tree, birdbath, compost), spawn point. The grid and the scenery rectangle come from the union of the areas; gate openings are cut out of the fence lines automatically (both directions, several gates on the same line); `Validate()` checks unique ids, non-overlapping areas, gates on shared edges, seeds/weeds in their own area, reachability from the first area through gates and the spawn point. Today's garden became the `HomeYard` definition (480×300 grid, the fences and gate exactly as in U12; fixed by a test). **Building:** `YardBuilder.Build` builds from the active definition, `YardBuilder.Use(definition)` switches to another garden (tests; later client gardens); the house can now be built at a position. **Gates:** `Gate` carries its definition, turns 90° on a fence along X, its leaf swings towards the area it opens into; `GatesTo(area)`. **Unlock chain:** when an area is finished the area behind every gate leading out of it unlocks (`LawnTasks.AreasOpenedBy`, the interface's "gate is open, … is waiting" comes from here too); when restoring a save every gate is open if its target area is open. **Occlusion:** every area counts as fenced in; the line of sight must pass through one of the open gates of both the eye's area and the target's area (two necessary conditions, the safe side); gates along X use the same calculation with the axes swapped. In the two-area garden it is the same as the old behaviour: `u9_perf`'s occlusion on/off pixel comparison passes. **Save version 3:** the garden id/version, grid origin, area rectangles, patch seeds and weed positions are written. A save that belongs to the same garden but whose patch map does not match is migrated area by area with `SaveMigration`: the grass (by world position, even if the grid shifts), weeds, patch states (a new id by seed position), hidden items and the day's pattern of areas whose rectangle and seeds stayed the same are kept; new or changed areas start fresh; day, points, upgrades, collection and unlocked areas are always kept. U10 (version 2) saves match today's garden exactly, so they load as they are and switch to version 3 on the first save. **Tests:** the home definition's validity and sameness with the old fence/grid, gate cutting (both directions), catching broken definitions, a 3-area test garden (1 ─x=10─ 2 ─z=10─ 3; gate building and swing directions, the chain, occlusion through two gates, visibility from area 2 through the north gate), migration (when an area is added to the west and the seeds shift to the front, area 1 is kept, the changed area 2 and the new area 3 start fresh; another garden and unstamped saves are rejected). All 16 scenarios and 197 unit tests pass. Frame times did not change (since the laptop ran on battery, this round's measurements vary ±0.5 ms: `u11_props` 4.19–4.76 ms).
- **U14 done (19 October 2026): orchard and vegetable garden, grass LOD (§9.5).** **Areas** (`HomeYard` version 2, unlock order 1 → 2 → 3 → 4): area 3 **Orchard** (X -30..30, Z -29..-15; its gate in the side yard's south fence): two staggered rows, 15 apple trees (an invisible trunk cylinder, the mower passes between them), a long wavy meadow (straw/golden/mustard), 8 patches, weeds at the tree bases, ivy on the south fence, two dry spots, an apple crate. Area 4 **Vegetable garden** (X -30..-18, Z -15..15; its gate in the orchard's north fence, the vegetable garden's south): six raised beds (cabbage, tomato, lettuce, carrot; 1.8 m grass paths between them), a "West path" patch that starts dry and three dry spots (watering-heavy), weeds at the bed ends and fence bases, ivy beside a bed and at the trellis base, a bean trellis, a wooden rain barrel, a wheelbarrow; 5 patches. The numbers in the plan (vegetables 3, orchard 4) changed with the unlock order. Grid 600×440 (areas ~1,440 → 2,640 m², 22 → 35 patches). The new props are in `GardenProps`, with the U12 materials (bark, leaf, wood, soil, metal, rubber). **Falling apples:** while the orchard is unlocked, every morning 1–2 apples drop under ~65% of the trees (seeded by the day), and under an area's trees when it unlocks; so they do not get lost in long grass they rest on top of it by the grass height, and drop to the ground once mowed; walking over them collects them (+12 points, a short chime, "Apples collected" in the day summary). The apples and their day are kept in the save. **Grass mesh LOD:** distant chunks are drawn with an 8 blade × 2 segment tuft (the near mesh's first eight blades, width ×1.4; 29% of the triangles); the switch per chunk at 18 m with a ±1.5 m delayed band, at 11.7 m at low/medium grass density. The existing density LOD (tuft count by distance) is unchanged. **Save:** pre-U13 (unstamped, version 1–2) saves are stamped with the first version of the home garden (`HomeYard.FirstVersion()`: areas 1–2, the first 22 seeds, the first 21 weeds) and migrated; U8's 6-byte lawn data is extended too. The user's save: the back and side yards are kept, the new areas locked and fresh. **Measurement** (`u14_areas`): with the whole garden visible from above (no occlusion) grass triangles 8.37 M → 5.21 M (38% fewer), 95 of 129 chunks with the far mesh, image difference 1.0/255 on average; from the ground along the orchard 22% fewer triangles, difference 0.02/255. Frame time on the normal power profile 3.34 / 3.61 ms (LOD off 4.07 ms; the < 6 ms target held); on the "Silent" power profile 6.0–6.4 ms (LOD off 7.0–7.4) — the scenario's hard limit is 60 fps, the 6 ms target is reported in the detail. **Tests:** the new areas' validity and chain, the weeds/dry spots/gate fronts/bed paths being clear once the whole garden is built, apples dropping only under trees in unlocked areas, collecting and saving them, the far mesh's triangle count, the delayed LOD switch, migrating an old unstamped save; grid/area/patch tests updated for 4 areas. All 17 scenarios and 203 unit tests pass. Found: in U12 the Turkish characters of two files (PaintedBackdrop, SurfaceLibrary) had been corrupted by PowerShell `Get-Content/Set-Content` (not noticed because the build was not affected); fixed, that path is no longer used.
- **U15 done (19 October 2026): job/home model, three client gardens, payment by stars (§9.6).** **Flow:** at home the **Job board** in the break menu shows three jobs (client, introduction, target time, payment range, pattern request, best stars; an unfinished job shows "Continue"). Accepting a job saves home, the transition card fades in, the scene is rebuilt with the client's garden and the card opens. On a job the card's day line becomes "client · time / target"; there are no points per task (payment on delivery), no daily pattern, lost items or apples; the menu offers "Deliver the job" and "Leave the job, continue later" (no workshop, collection, save or new game). When every patch is finished the delivery card opens by itself. **Rating:** completion = the area-weighted average of the tasks in the patches (mowing; watering and weeds if any); below 50% 0, at least 50% one, two stars if every patch is finished; the third star for finishing within the target time and (if asked) doing the requested pattern. Payment: city 250/450/650, country house 350/650/950, manor 450/850/1,200 points. Card: three painted stars (a star sprite drawn from code), completion, time, pattern, payment. **Return:** on delivery the payment is added to the profile, the job record (best stars, delivery count) is written to the home save, the half-done job save is deleted; when left, the job is saved to its own slot (`is_…json`) and later continues where it was left (with its time). Either way it is night when you get home (a job counts as a day), and the night card starts with the job's summary. **Client gardens** (`ClientYards`, single-area definitions, houses in colour variants: cream, sage, weathered brown): *City garden* (Ms. Selin, 16×12 m, a tree with a swing, two flowerbeds, a birdbath, a vegetable bed, a barrel; 5 patches, target 5 min), *Manor lawn* (the Kaya family, 40×28 m, two trees, a birdbath, two flowerbeds; 7 patches, north-south stripes requested on the "Front lawn", target 12 min), *Country house garden* (Uncle Hasan, 26×20 m, a long straw meadow, a shed, compost, a wheelbarrow, a barrel; 14 weeds, 5 ivy, 4 dry spots, a patch that starts dry; target 9 min). **Technical:** a garden change reloads the scene; `Career` (static) holds the active job, the carried profile (day, points, upgrades, collection), the job records and the transition cards; save slots (`SaveSystem.Slot`); the scenario runner survives the scene change and attaches to the new player. The build time on coming home was 0.8–1.1 s at first: the patch layout is generated once per definition and copied (0.32 s → 0), the dry spots' noise is not computed on distant cells, the scenery's painted tree textures are kept between scenes; a garden change now takes 84–262 ms. **Found and fixed:** (1) the clippings took the vertex colour wrongly in the ParticleSystem's mesh mode (red pieces were drawn black; that was the small dark pieces in the old screenshots) and after a scene reload the GPU-instanced drawing turned into long coloured streaks. The clippings are now simulated in C# (gravity, drag, tumbling, stopping on the ground, shrinking at the end) and drawn with GPU instancing with their own shader (`Opus/Clipping`, a colour per instance, the garden's light); their colours dark green-khaki as in U11. (2) In the test checking that no weeds/spots are left under props once the garden is built, the collider bounds were stale (edit mode needs `Physics.SyncTransforms`); once fixed, the U14 layouts were really verified too. **Tests:** completion weighting, the star rules, the payment table, the client gardens' validity and no weeds/spots/entry under props, job records, save slots. Scenario `u15_jobs` (15 checks): accept the city job → work (no points on the job) → the automatic delivery card with 3 stars → home, night, +650; leave the manor half done → continue the next day where it was left → 2 stars without the pattern, +850; pull a weed with the trimmer at the country house → early delivery 0 stars; 3 job records in the home save; the longest of 8 garden changes 262 ms. All 18 scenarios and 213 unit tests pass; frame 3.64 ms with the whole garden visible.
- **U16 pilot (19 October 2026): Blender model pipeline, calibration and wheel.** A response to the user's "the real problem is the 3D models" feedback; the job model's polish was postponed. The rules the user set: the video is the only design source (no mixing in examples from the internet; parts not visible in the video are a continuation of what is visible and marked as "a guess"), no ready-made shapes stacked on each other, 1:1 scale and the right orientation in Unity (by test, not by eye), tools held in hand by the first-person framing. **Pipeline:** `tools/Models.ps1` runs Blender 5.2 (Steam install) in the background; the `tools/blender/models/*.py` generators build the model with the shared `opus_model.py` (profile revolving, pushing out of a surface, a single-segment chamfer + weighted normals, edges over 60° sharp), validate it (open edges, inside-out shells, zero-area faces, loose vertices, material names, triangle budget), and write the FBX, a spec file in Unity axes (`Assets/Opus/ModelSpecs/*.json`) and a labelled contact sheet (views + video crops). In Unity `ModelImport` fixes the import, `ModelLibrary` turns the material names (`Rubber_2B2B30` = `Surface.ToString()`) into Opus surfaces, `ModelImportTests` compares every model with its spec file. **The calibration model** (a 1 m cube, front/right/up arrows of different lengths, a positioned ball) caught two real bugs: (1) Unity's "Bake Axis Conversion" setting turned back the axis declared in the FBX and brought the model in 180° reversed; choosing the forward axis in Blender did not fix it; with the setting off (axis conversion only in Blender, no rotation on the root) front became +Z, right +X, up +Y; (2) at the shaft-head step of an arrow the weighted normals smoothed the 90° edge and shaded 15 triangles wrongly → sharp edges are marked sharp. The region push leaving the original faces (three-faced edges) and crushed faces at chamfer corners were caught by the validation gate too. **Wheel** (`wheel.py`; rear Ø 230 × 68 mm, front Ø 190 × 58 mm, sizes the same as the radii in the physics): the tire is revolved from a cross-section profile (bulging sidewall, round shoulder), the lugs are pushed out of the same surface (two rows, offset by half a pitch, spilling from the shoulder onto the sidewall; the stepped silhouette in the video), the rim from its own profile (dish, lip, hub, axle hole) with eight pockets pushed into the dish; a red domed hub cap. The rim and hub are a guess since they are not visible in the video. A two-segment rounding was 22 thousand triangles per wheel; with a single-segment chamfer + weighted normals the same look in 12.4 thousand. On the mower, instead of the four old wheels (each a tire cylinder + 14 lug boxes + 5 spokes + a hub) a single model per axle (turned 180° on the left side); the `u10_tools` detail measure moved from part count to triangle count. Scenario `u16_models`: calibration in game (balls computed by Unity at the arrow tips; measured deviation 0.00 mm), wheel size (rear r 114.9 mm / physics 115, front 95.0 / 95), the wheel up close, the mower from the front side, the u10 angle and the player's eye. All 19 scenarios and 221 unit tests pass; the mower 55.8 thousand triangles, 7 renderers, frame times did not change.
- **U16 mower (20 October 2026): the whole mower in Blender.** The user's extra rule: mechanical logic in a realistic design; every part should have a job and a place it attaches to, cables should not go to meaningless places, model piece by piece and assemble. **Reference board** (`Builds/Models/mower_reference.png`, video 0:02/0:46/1:12; new frames `docs/reference/22`, `23`): in the video the mower is only seen from behind and above and leaves the scene when the tool changes. Read from the video: a two-ring dome, a stepped rear body and a dark rear flap, a dark cover with a ribbed grille in front and a ridge from the middle to the back, two hollows at its back (a knurled fuel cap on the left, the primer bulb on the right) and a lip; under the lip a silver starter cover with the rope handle leaning on it (the rope does not go to the handle). **Model** (`mower.py`, points in Unity axes, parts in their places on the mower): *Housing* (a ring-by-ring loft from an outline sampled by angle: skirt, shoulder, flat top, dome, collar; a stepped rear body rising towards the back, a hinged rear flap, wheel plates, axle pins, bushings, height levers), *Motor* (block, soft-cornered cover; the hollows, the rear opening and the grille slots carved with booleans only into its own shell; starter, hub, rope handle, fuel cap, primer bulb, brake tab), *Handle* (lower tubes bolted to the plate, a single bent upper U tube with wing nuts at the fold, foam, a hinged control bar, a control cable going down from the bar tab under the handle with clips, held with clips at the edge of the rear body and on the housing, entering the brake tab through the opening at the back of the cover). Overlaps found in my own check and fixed: the handle bolt went into the wheel, the cable passed through the plate, the starter handle went into the cover's lip, there was a gap between the cover corners and the dome. **Pipeline fixes:** a boolean applied to nested shells together deleted half of the engine → `carve` (carving only that shell) + `merge`; rewriting the material slots reset every face index (the test's "Housing materials" check caught it) → the indices are saved and written back; the boolean did not carry the cutter's material in an empty slot → placeholder materials. Connection points (grips, axles) in the spec file; `MowerConnectsWhereTheGameExpects` checks that the handle reaches the hands at `MowerTool.GripFromPlayer`, the axle pins sit on the in-game wheel axles, and with the wide deck the wheels stay on the pins (the wide deck now only widens the housing; the handle and wheels move out by the same ratio). The old hand-built mower code (about 180 parts) was removed; the mower is 3 parts + 4 wheels, 7 renderers. Tuned in game side by side with the reference framing (the cover and rear flap darkened, the starter matte silver + a dark hub, the rear body smaller and rising towards the back, the cable rerouted close to the surface). 224 unit tests and all 19 scenarios pass; the mower 70 thousand triangles (wheels 49.5 thousand), 7 renderers. In this round frame times measured 1–2 ms higher in some scenarios; in an A/B measurement in the same exe with the mower hidden the difference came not from the mower but from the laptop's power/heat state (two repeats of the same setting 5.1 and 3.2 ms; with the mower hidden 5.6 and 4.3 ms).
- **U16 material (20 October 2026): a hard-surface finish for the models.** User: "there is a problem with the material, it is not like the video". The video and the game were cropped from the same angle and measured: in the video the housing `#B9292F`, its back face `#9D2B36`, the cover top/side `#443F44`/`#373339`; ours the housing `#D02E3B`, the back face the same as the top (`#D8313A`), the cover `#4F4553`/`#463D4E`: light, bluish, and the form did not read. The cause was the light: in the soft painted light chosen for the garden the shadow colour is an almost white lavender (`#D2CDE3`) + ambient light, the shadow side barely darkens; no highlights; the grime blotches and paint pattern reaching everything near the ground made the mower look speckled. **Fix (only Blender models; the garden props unchanged):** a hard-surface path in `Opus/Surface` (`OpusShadeHard`: the shadow side drops to a dark neutral, a sun highlight, sky reflection at the edges; `_ShadeDepth`, `_Gloss`, `_SpecPower`, `_Reflect`) and baked AO (`_VertexAO`: R of the vertex colour). In Blender AO is baked into every part with Cycles (8 cm, darkest 0.3; on the mower the parts and wheels shade each other, in the wheel library each part alone). `SurfaceLibrary.CreateModel` a finish per kind (painted metal glossy, plastic medium, rubber matte), no grime, a light pattern; `ModelLibrary.MaterialFor` shared model materials. Colours by measurement: housing `#C21F22`, cover `#2F2E31`, starter `#8C8E94`. Result: housing `#BB272E` (video `#B9292F`), the dome rings and edges read with bright lines, the cover charcoal grey, the hollows and under the lip dark. Test: every model part has a vertex colour (AO), the models use grime-free materials. The hard-surface path is behind the `_OPUS_HARD` keyword: the garden props compile with exactly the old shader code (it was a select expression first; the compiler could compute both light paths and read the shadow map twice). The highlight showed a sawtooth pattern on the housing's dome ring: normals weighted by face area spoiled the normals on a soft shell with wide and narrow rings side by side → the housing with plain (angle-based) smoothing, weighted normals only on hard parts (`part(weighted=False)`).
- **U16 surface finishes (20 October 2026).** User: "the black parts are matte because they are plastic, the red is glossy because it is metal; you made all of it glossy; do you model without understanding what the materials are?" Right: the shine was given wholesale by kind (metal/plastic/rubber); the plastic cover was semi-glossy, the black handle and plates counted as "metal" were as glossy as the red sheet metal. Read from the video part by part (10 frames): the red sheet metal has thin bright lines at the edges and on the dome (glossy), no highlight on the engine cover (matte), a white reflection on the starter cover (metallic), the handles flat dark (satin). **Made mandatory in the pipeline:** `model.surface(kind, colour, finish, reason)`: no material is built without a finish (glossy, metallic, satin, matte) and the reason "what it really is, what shows in the video"; the material name carries the finish (`Plastic_2F2E31_matte`); Unity gives no default by kind (`SurfaceLibrary.ModelFinishes` by name, `ModelLibrary.TryParseModelMaterial` rejects a name without a finish). A **material card** under the contact sheet (colour, finish, reason), the Blender preview by finish (roughness/metallic). Tests: every material of every model has a finish and a reason; on the mower the finishes read from the video (sheet metal glossy, cover matte, handle satin, starter metallic), matte does not shine, satin < glossy < metallic. Measured: cover top/side `#423843`/`#3E3645` (video `#443F44`/`#373339`), no highlight; the brightest point on the red sheet metal 195 (video 209). 228 unit tests pass.
- **U16 hose nozzle, trimmer, tree (20 October 2026).** User: "make the hose, the trimmer and the tree in the same process; for the tree look at free models online as examples, it should have proper leaves". First a reference board and material card for all three (`Builds/Models/tools_tree_reference.png`; new frames `docs/reference/24`-`27`). For the tree I only looked online, nothing was downloaded (OpenGameArt "Sinuous Toon Tree", itch.io CC0 tree packs, articles on the stylised leaf technique): the method learned is clusters of leaves at the branch tips with "spherical" normals pointing out of the cluster. **Hose nozzle** (`hose_nozzle.py`, Body + Trigger): in the first version I had put the trigger in front and the finger grooves at the back; compared with the video the nozzle turned out to have a *rear trigger* squeezed by the palm: the yellow trigger comes down behind the grip from the joint under the knob; the knob faces the camera inside the yellow collar; the finger grooves are in front; the barrel was shortened. In game the nozzle's leftward turn from the target dropped from 48° to 16° (it was set so the old straight pipe showed from the side; in the video the knob faces the camera), the trigger turns 6° when squeezed (it does not touch the grip). **Trimmer** (`trimmer.py`): a unit-length shaft (stretched to the hands in game with the Z scale), a round red body (top panel, vent slots, white label, grey underside, rubber grip area, trigger, shaft clamp), a framed black battery attached at the back (a dark red recessed panel, ribs, latch), a thick D handle clamped to the shaft, the head (gearbox, bell, orange ring, spool, orange bump knob, line holes, a guard outside the bell above the line), the spinning and resting line as separate parts. **Tree** (`tree.py`, the garden tree): as in the video a widely buttressed trunk and roots diving into the soil, the dark blotches of the bark texture, branches forking from the trunk; 4,313 real-shaped double-sided leaves in 23 clusters (three tones of green, the inner ones dark), a rope wrapped twice around a branch and knotted, a rope wrapped around the tire's upper cross-section and the tire. The colliding trunk and swing sizes are the same as the earlier tree. **Pipeline:** leaf parts (`part(foliage=True)`: open double-sided surfaces, normals from the generator, the test expects half the winding each way), a separate AO for the leaves (short distance, high floor; otherwise the crown darkened to 0.42 on average), closed-ring sweeps (D handle, rope), custom cross-section sweeps (flat trigger), building a part at its position in the file (`keepPosition`: the trigger joint, the line), unused material slots dropped (the FBX dropped them, the spec file counted them; the test caught it). On models natural textures such as bark and fabric get the full pattern, leaves a soft light. Contact sheets and in-game shots in the `u16_models` scenario (the hose and trimmer up close and from the player's eye, the tree from the garden, its trunk, its crown). The "no stretched objects" check of `u15_jobs` (it catches the old clipping streak bug) took the new swing part (a narrow rope 3 m tall + a tire) for stretched; it was added to the allowed long parts like the old tree's rope. 237 unit tests and all 19 scenarios pass; frame 3.3 ms in the garden overview, the draw count dropped from about 600 to 573.
- **U16 fixes (20 October 2026).** The user's three objections: (1) "the brown on the tree was not a stain but the shadow of the leaves, you made it a separate mark; the texture is pixelated": since U12 the bark texture had drawn the dark areas of the trunk in the video as sharp-edged paint blotches; those areas are shadow falling from the crown. The bark was rewritten (soft vertical fibres, shallow fissures, a light tone variation; no blotches or sharp edges), 1024 pixels (resolution per pattern `SurfaceTextures.SizeOf`; the 512 texture with a 1.3 m repeat grew ~2.5 times up close and pixelated), anisotropy 8; the leaves now cast the shadow. Since the fruit trees use the same texture, the blotches are gone there too. (2) Trimmer: the user was asked; the answer "the grey strip structure is big in the video, ours does not look like it; the back is completely unrelated; ours is too matte in game". The frames were enlarged and read again: the upper half of the body is a glossy red shell, the lower half a light grey-white shell from end to end, a small button panel on top, a red neck in front; the black battery is recessed into the upper back of the body and comes down at the back, almost its whole back face is a frame, dark inside, a red latch on the right. The earlier inventions (side vent slots, a rubber band, a box battery stuck on the back) were removed; the red "glossy", the battery and the white "satin". The same mistake twice: the inner shell (the white lower shell) showed through the battery's hollow; the shells were cut where they overlap. (3) Hose: "your depth perception is mediocre, you could not perceive its length and made it crooked". I had made the barrel of the nozzle seen from behind far too long (head to knob 15 cm); it was brought down to the proportions of a real pistol nozzle (the back of the head a flat wide disc, a 4 cm body, a thin dial band). The grip was found by measurement, not by eye: the screen position and size of the head and the knob were measured in the video frame (head 0.651, 0.317, r 0.063; knob 0.698, 0.202, r 0.038), `u16_hose_tuning` tried a grid of in-hand position × leftward turn × lift in game and found the closest (hand 0.22, −0.14, 0.71; 20°; lift 4; result head 0.665, 0.299, r 0.069; knob 0.697, 0.207, r 0.038). The nozzle now looks towards the target (the water comes out the same way); `u16_models` checks the framing's deviation from the video.
- **U16 water effect and hose nozzle (20 October 2026).** User: "the hose's texture work is weak; you did not make the water effect, make it like the video". Asked about the nozzle; the answer: no shiny material in the video (ours shines), the head's angle and shape differ, the grip angle differs, the head is longer in the video. In the previous round I had shortened the barrel as "short and not elegant"; the problem was not the length but a thin long pipe + a side view. **The nozzle from scratch:** the back of the head a flat wide disc, a thin densely knurled dial band, a thin long neck coming out of the lower half of the head, a big round yellow body (a black stripe in front, a big black flow knob at the back: a grey ring, a white dot), a grip coming down from under it (finger grooves in front), a rear trigger coming down from the yellow body; all surfaces matte. **Grip:** now fixed to the camera (like first-person tools: `HoseTool.HandAnchor` + `HoldEuler`); the head and knob position/radius measured from the video frame (0:20.6), the apparent roundness of the head (0.85) and the direction the grip goes down were fitted in `u16_hose_tuning` to a position × rotation tried thousands of times in the same frame (40 random starts + coordinate descent): head 0.649, 0.320 r 0.067 round 0.85 (video 0.649, 0.320 r 0.070, 0.85); knob 0.698, 0.202 r 0.048 (video r 0.043); grip direction −0.11, −0.99 (video −0.10, −1.00). **Water** (`WaterSpray`, `Opus/WaterPuff`; the old capsule `WaterStream`/`Opus/Water` deleted): in the video the water is a short, puffy, cloud-like light blue-white jet from the nozzle (white on top, bluish underneath, lumpy edges), travelling 30-60 cm and dispersing; on the ground the spot where the water lands darkens and thin faint rings spread outwards. Jet: 150 "puffs" per second (a camera-facing quad with a circle with lumpy edges inside) thrown from the front of the nozzle, slowed by drag, growing and fading; its direction sways slightly (it curls). On the ground: a thin solid target ring and faint rings born every 0.6 s spreading to 2.2 times the radius in 1.9 s (`GroundRing` thin solid line option). Splash drops cut to a third (hardly visible in the video). The watering gameplay (target, radius, speed) did not change. Scenario checks: hose framing deviation from the video, water jet (puff count, jet length 0.25-0.7 m, rings).
- **U16 shed (20 October 2026): in Blender, with the proportions of the video.** User: "the house and the shed look very plastic". The plan was approved with two decisions: the dark shutters on the house go, and the shed's gable triangle gets no vent, the siding continues. **Model** (`shed.py`): every lap siding board separate (its bottom edge 2.4 cm, its top edge 0.8 cm out; the shadow line comes from this step; a slight tone difference per board); corner, rake and fascia boards; casings; three-tab shingles with rows offset by half a pitch and ridge caps; a framed double door (stiles, top/middle/bottom rails, flat panels 1.4 cm back); triangular hinges screwed to the rails and hinged to the casing; handles bolted to the inner stile; a recessed window with 2x2 panes. Compared in game from the video's angle (0:16), four fixes were made. (1) **Proportions:** in the video the door is almost as tall as the wall, the roof steep, the shed nearly square. A 3 × 2.6 m base, 2.5 m walls, a 34° roof → a 2.6 × 2.6 m base, 2.2 m walls, a 45° roof (the ridge the same, 3.5 m); the collision box accordingly (the grassless area is computed from the box). The window was narrowed and lowered, the rake boards widened. (2) **Grime:** the siding and the door had round grime blotches; the shed in the video is clean (the brown thing at the base is ivy). On models grime is now only on concrete, brick and stone. (3) **The doors** were in a raw wood texture; they became the same white paint as the casings. (4) **AO bug (pipeline):** AO is baked into the vertices; a 3 m board only had its end vertices and the ends were under the corner board. The darkness of the ends spread over the whole board, the siding and door panels looked grey-purple. `part(ao_grid=...)` splits long faces at regular intervals; the door panels are now separate parts inside the frame. **Colour:** measured from frame ref 05 without haze (ref 28 has a yellow-green haze). The game's purple sky light turned the siding purple, so the base colour was shifted to green. Siding `#849CA4`, whites a neutral `#DADCE0`, shingles `#2E3331`. Result: the siding in shadow `#80848E` (video `#7B8489`). Showroom: 7 directions, the angle in the video, close-ups of the door and the window-eave. In the showroom orbit the model's front is turned to its direction in the garden (`frontYaw`). 25.7 thousand triangles in total.
- **U16 house (20 October 2026): in Blender, following the garden facade in the video.** The user approved the shed ("move on to the house"). The frames where the house shows were extracted from the video (`docs/reference/29` 0:57, `30` 0:41.6, `31` 0:53.5). Visible in the video:
  - a two-storey, side-gabled house, no shutters;
  - five evenly spaced tall sliding windows on the upper floor;
  - on the ground floor windows on the left, a glazed door and a double glass door on the right, above both a gently sloped porch roof covering the right half of the facade;
  - a low deck along the facade: a cedar edge, a railing with white balusters, a white lattice underneath;
  - grey-brown shingles, a rose-brown gutter at the eave, a downspout at the corner, a thin black pipe on the roof; no chimney.

  The old house had four windows, blue shutters and a veranda with columns and a full roof in the middle. **Model** (`house.py`):
  - **Body and facades:** body 16 × 10 m, eave 6.25 m, roof 34°. The window and door openings carved into the core with booleans. Separate lap siding boards, their ends under the casings and corner boards. Frieze, eave and soffit, rake boards.
  - **Windows and doors:** recessed sliding windows (the upper sash outside, the lower inside; light glass). A garden and a street door (2x2 glass, two raised panels) and a glass door.
  - **Roof and gutters:** 75 cm shingle strips offset by half a pitch per row; ridge caps. K-profile gutter. The downspouts turn from the end of the gutter to the gable wall with an elbow, come down in front of the corner board and drain onto a stone; they do not pierce the porch roof.
  - **Deck and porch:** deck x −4.2..7.8, top 0.5 m, two steps. A railing with posts, balusters 11 cm apart, a two-layer diagonal lattice. The porch roof comes down from a ledger on the wall to a beam carried by posts, a white ceiling underneath.
  - **Details:** a lamp, door handles, a vent pipe.
  - **Layout:** the deck and porch stay within the house's width (in the client gardens the fences run up to the house). The grill moved to the east end of the deck (as in the video).

  **In game:** the body, deck and steps are invisible collision boxes; the grassless area is computed from them. The colour variants replace the siding and door surfaces with `ModelLibrary.Whole(..., recolor)` when built; variant 0 is the model's own colours. The `HouseVariantsRecolorSidingAndDoor` test checks the variants and that the door colour in house.py and in C# match. **Pipeline:** the model does not draw the joints in the deck board (`Planks`) texture a second time, only the paint marks.

  **Triangles:** the first version was 154 thousand. The AO split interval was enlarged; the 2 mm chamfers on the siding and windows were removed (not visible from the house's distance). Result 90.6 thousand.

  **Colour:** measured from the same area as the video. The siding in the video `#A7A6A7`; in game light blue-grey, a close tone. The casings turned cream in the sun: the white base was shifted slightly to blue, the glass lightened, the shingles lightened. Showroom: 7 directions, the three angles in the video, the doors and porch, the eave corner.

  **Verification:** 244 unit tests and all 21 scenarios pass. In a full run the frame times looked doubled (u9_perf from 3.5 ms to 9 ms; u4_trim, which has nothing to do with the house, rose too). In the same exe an A/B with the house hidden/shown was measured (u9_perf, two rounds): hidden 4.43 / 3.43 ms, shown 4.46 / 4.34 ms. The difference is within the noise; the house adds about 0.25 M triangles to the draw, the rise is from the machine's state at the time.
- **Model showroom scene (20 October 2026).** User: "I looked at the models in detail, we are in a pretty bad/mediocre state; make a scene where we can see all the models, I will inspect them one by one in Unity and send screenshots". The project move request was cancelled (the project is already in `Documents\GitHub\Copycat Opus`).
  - **Scene:** `Assets/Opus/Scenes/ModelShowroom.unity`; inspected in the editor, without pressing Play (the user's choice).
  - **Content:** `ModelShowroom` runs in the editor. It lines up 21 models in two rows with the game's own builders:
    - Blender models: mower, hose nozzle, trimmer, shed, house, tree;
    - hand-built garden props: fence, flowerbed, birdbath, compost, barrel, wheelbarrow, apple crate, bean trellis, four vegetable beds, two apple trees, the garden gate.
  - **Look:** the game's sun, sky, ambient colours and colour grading exactly (`Atmosphere` split into parts: `SetShaderGlobals`, `CreateSkyMaterial`, `ConfigureSun`). `YardBuilder.BuildProp` made public.
  - **Saving:** what is built is not saved into the scene; since the materials are generated from code it is built when the scene opens.
  - **Command line:** `tools/Showroom.ps1` builds the scene and captures the editor view from two sides for every model. Verified to work in batchmode too, with the materials and light coming through.
  - **Next:** the user's screenshots and objections.
- **Model pipeline from scratch (20 October 2026): the textured pipeline and the swing pilot.**

  The user's findings from the showroom scene:
  - Every model has too many mesh renderers; models must be a single piece (except moving parts).
  - No model has textures, everything is a flat colour.
  - The hose nozzle is crooked.
  - At the shed's corner and on the window sill overlapping faces flicker (z-fighting).
  - There is an unexplained shadow beside the window.
  - The lamp does not look like a lamp.
  - The tree texture is low resolution, the root-trunk join is bad.
  - The rope looks like a plastic pipe, the tire like a plastic ring.

  The causes were found in the code:
  - The neck had been deliberately offset 1.2 cm from the head.
  - The corner boards fill the same volume at the corner.
  - The bottom face of the window opening lies in the same plane as the sill.
  - AO is baked into the vertex colour and vertices 1.2 m apart spread the shadow like a stain.
  - The shader used a triplanar pattern projected from world space without UVs.

  Researched online: Blender Lab's official MCP, the community "MCP for Blender", Claude Code skills ("blender-game-skills"), the UV/texel density/baking workflow, CC0 sources. The user's decisions:
  - Downloading CC0 textures is allowed (Poly Haven, ambientCG).
  - The method goes into our own pipeline; MCP is to be tried separately and compared.
  - The swing as the pilot.

  **Pipeline** (`opus_model.py`):
  - `textured_surface` (a material from source textures and nodes);
  - source UVs (`sweep` twisted rope: 3 strands, pitch = the lines in the texture; `revolve`/`loft` UV callbacks);
  - `finish_textured`: a z-fighting hunter (visible faces in the same plane, facing the same way, overlapping and not lying against each other), joining the static parts, an atlas UV (the source UV cut up and packed: on the swing 479 → 1250 px/m), Cycles baking (colour, normal, roughness, metallic, AO), a single `Atlas_<model>` material.

  Z-fighting the hunter found in the old models:
  - shed: 1593 pairs, the largest the front corner boards;
  - house: 11989 pairs, the door threshold and recess faces;
  - mower: 75 pairs; hose: 133 pairs; trimmer: 17 pairs.

  **Unity:**
  - the `_OPUS_UVMAP` path of Opus/Surface: atlas colour, normal map, AO, shine from roughness;
  - `ModelLibrary.AtlasMaterialFor`;
  - `ModelImport`: MikkTSpace tangents, the colour space and normal type of the atlas textures;
  - tests: UVs, tangents, a single material, texture settings on textured models.

  **Pilot** `tire_swing.py`:
  - the old rope and tire on the tree were removed;
  - rope: CC0 ambientCG Rope001, a 3-strand twist, two turns around the branch, a knot wrap (the ends buried inside the rope), 3.5 turns around the tire's cross-section (the end in the hole);
  - tire: a tall-sidewall 195/80 R13 cross-section, two longitudinal grooves, block lugs and sidewall rings from nodes, charcoal grey, dust on top;
  - a single object, 34 thousand triangles, a 2048 atlas.
- **Swing fix and MCP install (20 October 2026).**

  **The user's objection:** "is the rope tied like that, can't you see it in the reference; the tire is a disgrace; study tire swing examples online". Right: I had wrapped the rope 3.5 turns around the tire; in the video (1:07, `docs/reference/32`) the attachment is different:
  - a knot lump right above the tire;
  - the rope goes down in front of the tire into the hole, its tail hanging like a tassel.

  **Example studied** (Sketchfab "Tire Swing", 3Dee):
  - A hitch: the loop wraps the tire's upper cross-section, the two legs come down the front and back sidewalls and meet in the hole, the knot is on top of the tire.
  - The tire has raised block lugs and shoulder lugs.

  **The new swing:**
  - **A single, continuous rope path:** hanging rope, front leg, the loop under the bead opening, back leg, a 3.5-turn wrap through the knot, tail.
  - **Rope path smoothing:** Chaikin corner cutting and equal-spacing resampling; the rope does not flatten in tight turns.
  - **Tassel:** 14 fibres at the end of the tail.
  - **Tire:** raised blocks in geometry on the tread (shoulder and middle rows, three longitudinal grooves; in the first try they came out as pits because the face normals pointed inwards, the shell was turned outwards first). A deep ring groove and a rim protector rib on the sidewall (the two rings in the video show in game). Rubber: a cool charcoal grey, scuff marks, wear on sharp edges (Cycles Pointiness), dust, fine grain.
  - **Measured:** 47.7 thousand triangles, 1187 px/m.

  **MCP for Blender installed** (with the user's permission):
  - uv 0.13.2 (winget, astral-sh.uv);
  - `uvx mcp-for-blender`, the addon `%APPDATA%\Blender Foundation\Blender\5.2\scripts\addons\blender_mcp.py`, enabled in the Blender preferences; the server starts by itself when Blender opens (localhost:9876); telemetry off;
  - added to Claude Code as a local MCP for this project: `claude mcp add blender --scope local --env DISABLE_TELEMETRY=true -- uvx mcp-for-blender`.
  - The tools load in a new session; the comparison in a separate session.
- **Tire tread (20 October 2026).**

  User: "are the lugs that blocky; where did you research online, show me". Shown (`Builds/Models/research/`): Sketchfab "Tire Swing" (3Dee; where the rope attachment came from) and a close-up of the tread. No example had been looked at for the lug pattern before. Now looked at:
  - "Dirty Old Tires": V/arrow blocks with rounded corners, zigzag grooves, fine sipes;
  - "Old Tires - Dirt".

  On an old tire the silhouette is round, the pattern in the normal map. The new tread:
  - The blocky blocks raised in geometry were removed.
  - The tread pattern is a seamless height texture generated from code (`source_textures/generated/tire_tread.png`): zigzag longitudinal grooves in the middle and on both sides, V-sloped lateral grooves, shoulder grooves widening outwards, half-depth fine sipes in the blocks, block edges rounded over 1.4 mm, 56 lug pitches (a whole number around the circumference).
  - Baked into the normal map and the colour: dirt in the groove bottoms, wear on the block edges.
  - Measured: 44 thousand triangles; 248 of 256 tests passed, 0 failed.
- **Rope tail, MCP test and rigid body placement (20 October 2026).**

  **Rope tail:** the user showed that under the wrap the tail bent to the left in a physically impossible way. The wrap now ends at the angle the tail comes out at (`TAIL_X`, right beside the front leg), and the tail comes straight down at a constant x. Verified in game (`v_swing_wrap`).

  **MCP test** (`Builds/MCP_test/tire_swing/report.md`, `comparison.png`):
  - The same swing was built in a live Blender with MCP: about 28 calls, 1.3–10.8 s per call.
  - The Poly Haven "Old Tyre" (CC0, scanned) came in with a single call; clearly more realistic than our procedural tire. For the rope our pipeline (a 3-strand geometric twist) is better.
  - On the MCP curve rope the NURBS parameter is not proportional to length, so the texture stretched on the legs; fixed by equal-spacing resampling (our pipeline already does this).
  - Conclusion: MCP for exploration and quick trials, permanent models in our pipeline (code file, atlas, bake, tests). Scanned CC0 assets can be brought into the pipeline as "external assets".

  **Rigid body placement** (the user's suggestion: instead of placing apples one by one, drop them into a crate and let them find their places in a simulation):
  - `opus_model.settle(bodies, colliders)` runs a Bullet simulation in the background.
    - It runs until the bodies stop (at least 200, at most 600 frames); a resting body goes to sleep (deactivation); angular damping stops endless rolling on a flat floor.
    - At the end it bakes the rotation into the vertices: only the position stays on the object (the pipeline's rule).
    - Parts interpenetrating at the start raise an error: bodies starting interpenetrated flung each other, one fell through the floor.
    - The same input gives the same result.
  - Many parts are dropped in waves: the settled ones become colliders for the next wave. Dropping all of them at once from 7 layers up toppled the pile and a stone bounced out of the crate.
  - The check `tools/blender/checks/settle.py` (`Models.ps1 -Checks`): 40 irregular stones dropped into a shallow crate. Checked:
    - all stopped;
    - all inside the crate;
    - sinking under 2 mm (the gauge is tested with a deliberate 1 cm sink);
    - no rotation or scale;
    - two runs exactly the same.
  - The first candidate use: the stones at the flowerbed edge.
- **The project in English (20 October 2026).** User: "translate the game and all the files in the project into English, the project will be English".
  - **Code:** every comment, log, error and test message, and every string in C#, shaders, Python and PowerShell; the interface (menus, HUD, banners, tool names and prompts, area/patch/item/upgrade/job names, numbers in the `en-US` format and percentages as `98%`).
  - **Names:** models and generators (`calibration`, `wheel`, `mower`, `hose_nozzle`, `trimmer`, `tree`, `tire_swing`, `shed`, `house`), Blender parts and anchors, material finishes (`glossy`, `satin`, `matte`, `metallic`), atlas textures (`_color`), surface textures, scene and object names, scenario/shot/check names, sounds, Unity layers (`Player`, `Tool`), tool scripts (`Showroom.ps1`), folders (`tools/blender/checks`, `source_textures`, `docs/reference`) and reference frames. Unity assets were renamed together with their `.meta` files so the GUIDs stayed the same.
  - **Kept on purpose:** the save keys (the `bahce.json` slot, the `is_sehir/is_malikane/is_kir` job slots, the home garden id `ev`, the lost item ids) so existing saves still load; commented in the code.
  - **Safety:** the translation was applied with a script that rewrote only comments and strings and refused any file whose code outside them changed; a backup of the sources was taken first (`Builds/Backup/before_translation_2026-10-20.zip`). Old generated outputs with Turkish names were moved to `Builds/Backup/old_outputs/`; all models, contact sheets, sounds, scenario shots and the showroom capture were regenerated with English names.
  - **Verification:** 248 of 256 unit tests pass (8 expected skips), all 21 scenarios pass. In the first full run u9_perf, u11_props and u14_areas failed only on frame time; rerun, they passed. Frame times are about 2–3 times the usual values, because Jagged Alliance 3 was open using the GPU (68%) and the power profile was "Silent". For the same reason u14's soft target (< 6 ms) was missed (LOD on 14.8 / 11.2 ms); the hard limit of 60 fps held. To be measured again on an idle machine.
- **Outside assets (21 October 2026).** User: "add all the places we can get free 3D models from; think beyond this project; ask me for whatever keys are needed".
  - **Shared tool:** free-assets (`../free-assets`). It is a command line tool and an MCP server registered for every project at Claude Code's user scope.
    - Sources: Poly Haven, ambientCG, Poly Pizza, Sketchfab, Kenney, Quaternius (hand download + `add-local`), BlenderKit, OpenGameArt.
    - It checks every licence: only CC0, CC-BY, OGA-BY and BlenderKit royalty-free pass unless allowed explicitly. It keeps a record per asset (`asset.json`) plus `assets.json` and `CREDITS.md`.
    - It unpacks zips safely. Keys live in the user's profile (`free-assets key set <source>`), never in a project or the chat.
  - **In this project:**
    - `tools/Assets.ps1` fetches into `tools/blender/external/`.
    - `om.external(...)` imports an asset as a part: licence gate; one mesh; size, centring and budget; textures baked into the model's atlas; an `external` record in the spec.
    - `finish_textured(export=False)` lets checks bake without touching Unity.
    - Check: `tools/blender/checks/external.py` (Poly Haven "Planter Pot Clay", CC0).
    - Unity tests: `OutsideAssetsHaveAllowedLicencesAndCredits` and `FetchedOutsideAssetsHaveAllowedLicences`.
  - **Keys waiting for the user:** Poly Pizza (search and download), Sketchfab (download), BlenderKit (download). Search works without them on Sketchfab and BlenderKit.
  - **Style decision:** per the user, the simpler procedural tyre stays. Scanned textures on our own shapes fit the game better than whole scanned models. Research and sources: `docs/external_assets.md`.
- **Model fixes with the new tools (21 October 2026).** User: "can you fix these models with the new tools" (the worst three: trimmer, tree trunk, tire swing).
  - **Tire swing.** Measured in the 1:07 frame against the 63 cm tire.
    - Rope 2.3 cm thick (was 3.4).
    - The knot is a two-turn wrap of about 7 × 6 cm sitting right on the tire (was three turns, 13 × 14 cm).
    - The tire base is olive brown-grey: the lavender sky light had turned the neutral base purple (in game 95/85/92, now 82/73/66; video 80/72/56).
    - The rope is lighter.
  - **Tree.** Body (trunk, roots, branches) is in the textured pipeline.
    - The roots are buttress fins of the lofted cross-section: five rounded fins reaching about 1 m, their top edge sloping into the soil, fading into folds up the trunk. They replace the thin swept roots.
    - Bark: ambientCG Bark003 (CC0, fetched with `tools/Assets.ps1`), box-projected. It gives the grain as bump and a little tone on the video's flat warm brown (in game 97/59/45; video 89–98/56–68/38–50).
    - The leaves stay on the soft foliage material (`finish_textured(untextured=...)`). The crown is unchanged (the random sequence is kept).
    - The showroom mows around the tree, because the video's 1:06 frame shows the finished patch with the roots in view.
  - **Trimmer, model.** Textured pipeline: each moving part is its own object, one atlas, AO baked per part.
    - The D handle is a thick arch across the shaft (was an O-ring).
    - The head is a flat puck with a ringed knob (was a bell with a wide guard).
    - The four dark button squares on top are gone (not in the video).
  - **Trimmer, framing.** Fitted to the 0:07 frame by `u16_trimmer_tuning`: the head, the body's front, the D handle's top and the battery's back face (position, width, height, upright) on screen.
    - Hands at (0.281, −0.106, 1.136) in camera space, roll −3.9°, pitch 45° (`EquipPitch` 34 → 45).
    - The D handle stands 11 cm in front of the body (was at 60 % of the shaft).
    - `MaxReach` 1.8 → 1.4 m: in the video the trimmer is held compact, the head just beyond the D handle.
    - The head is held about 3 cm off the ground (`HeadLift` 0.124 → 0.15): at the old height the mown grass cut its lower edge into spikes.
    - Remaining deviation 0.180: the video's body is a little bigger and its camera a little more level.
    - `u16_models` checks the framing (`trimmer_framing_matches_video`).
  - **Pipeline.**
    - `finish_textured(untextured=..., ao_together=False)`.
    - `Model.external_texture(ref, file)` (licence-checked outside textures, listed in the spec).
    - The z-fighting guard caught two overlaps on the trimmer (the shells' front caps, the guard arm), both fixed.
  - **Verification.**
    - Unit tests: 261 of 267 pass (6 expected skips). `TrimmerTests` follows the new reach.
    - All 22 scenarios pass, including the new `u16_trimmer_tuning`.
    - The pipeline checks (`external`, `settle`) pass.
    - Boards: `Builds/Models/comparison/{tire_swing,tree,trimmer}_fixed.png`.
- **Next four models (21 October 2026).** User: "now move on to the next models (hose nozzle, shed, house window and lamp, the mower's wheels)".
  - **Mower wheels** (textured pipeline; each wheel its own object). The first version read as a toy off-road tire: 16 big blocks per row, 6 mm tall, spilling down the sidewall, and a red hub cap. Now:
    - 32 small blocks per row, 3.5 mm tall, only on the tread;
    - a dark grey cap;
    - rubber grain (CC0 ambientCG Rubber001 height), light worn block edges, dust on top.
  - **Hose nozzle** (textured pipeline). Moulded plastic with a fine grain; colours sampled from the 0:21 frame (green 33/117/68, yellow 202/184/91; the old flat colours read 63/136/87 and 231/200/93 in game).
    - The knob's ring and dot are inlays, and the stripe's back cap is clear of the neck: they had z-fought.
  - **Shed** (textured pipeline: one mesh, a 4096 atlas). Painted wood: CC0 ambientCG PaintedWood009C grain and tone, a per-board tone (Random Per Island), granular asphalt shingles. Bolder wedge hinges as in the video. The overlap guard went from 1,593 pairs to none:
    - only the front and back corner boards reach round the corner;
    - siding row ends are staggered;
    - casing sides stop under their heads;
    - the muntins sit at different depths;
    - the shingle rows' gable ends and the ridge caps' edges are staggered;
    - the corner boards end 4 mm into the roof.
  - **House** (still the old pipeline, because client gardens recolour its siding and door).
    - Window sills and thresholds sit 4 mm above the opening's floor: in its plane they z-fought (the flickering sill).
    - The corner boards, siding ends, shingle ends and rake boards were fixed the same way as the shed: from 11,989 overlap pairs (92 m²) to 412 (0.12 m²), the rest small or under the ground.
    - The lamp is now a gooseneck barn light right of the glass door, as at 0:57: a round wall plate, an arm bending down, a grey enamelled dish shade with a white inside, a frosted bulb. The old box lantern "did not look like a lamp".
  - **Overlap checker bug.** `coplanar_overlaps` rebuilt sample points with the face normal but a frame built on the rounded normal. On slopes that are not 45° the points missed the plane by centimetres, so faces lying on the sheathing were not seen as "pressed". This caused most of the house roof's false pairs. Fixed: points are lifted onto the plane.
  - Old-pipeline models write their full overlap list to `Logs/overlaps-<model>.json`.
  - `finish_textured` joining several static parts: the bookkeeping read removed objects (it had never been reached before the shed).
  - `Model.moulded_surface(...)`: a shared helper for moulded or painted surfaces.
  - **Volume in double precision.** The lamp shade sits 7 m from the house's origin. Blender (mathutils, single precision) and Unity (float `Vector3`) both lost about 1 % of its volume to rounding, so `signed_volume` and `ModelMatchesBlenderSpec` now compute relative to the shell, in double precision. The shade's first profile crossed itself; its inner face now runs 4–8 mm under the outer one.
  - **Verification.**
    - Unit tests: 264 of 267 pass (3 expected skips).
    - All 22 scenarios pass.
    - The pipeline checks pass.
    - Boards: `Builds/Models/comparison/{mower_wheels,hose_nozzle,shed,house}_fixed.png`.
- **The house in the textured pipeline (21 October 2026).** User: "move the house to the textured pipeline".
  - **One mesh, a 4096 atlas.**
    - Painted wood (CC0 ambientCG PaintedWood009C) for the siding, trim, door and deck (cedar tone), each board a little different.
    - Granular asphalt shingles, concrete, moulded metal and glass.
    - The shared helpers `painted_wood_surface` and `asphalt_surface` are in `opus_model.py`.
  - **Overlaps: from 412 pairs to none.**
    - Foundation steps, post caps against handrail ends, handrails meeting at corners, side lattices, the last side post against a deck board's edge.
    - Glass-door and door muntins, fascia ends at the roof edge and on the porch, gable corner boards.
    - Ridge caps: alternate widths, and their ends kept clear of the shingle strips' ends.
  - **Colour variants survive.** `finish_textured(tint=...)` also bakes `house_tint.png`: R = siding, G = door. `house_tint.json` names the two reference surfaces (`Siding_A6B0B8`, `Paint_94817F`).
    - Opus/Surface multiplies the masked areas by `_TintA` / `_TintB`.
    - `ModelLibrary.Whole(..., recolor)` gives a variant its own material with multiplier = new colour / model colour (linear).
    - `HouseVariantsRecolorSidingAndDoor` checks the mask, the multipliers and that variant 0 is unchanged.
  - **Atlas space.**
    - **Hidden faces.** `drop_hidden_faces` deletes faces nobody can see: faces pressed against another shell, and faces from which no ray leaves the model above the ground (inside volumes, under the ground, covered). The house dropped 27,844 faces, the shed 3,775.
    - **Cut first.** `cut=1.0` splits long faces before that test, so a large face is judged piece by piece.
    - **Packing.** `pack_shape="AABB"` with a fixed 4 px gap (`margin=2`). The default per-island margin had filled the house atlas with gaps: at first only 0.85 % of it was used, 8 px/m.
    - Texel density now: house 76 px/m, shed 342 px/m (was 113).
  - Unity imports model textures up to 4096 (was 2048: the shed's 4096 atlas had been halved).
  - **Verification.**
    - Unit tests: 265 of 267 pass (2 expected skips).
    - All 22 scenarios pass.
    - The pipeline checks pass.
    - The client gardens show the textured house cream with a brick-red door (city), sage green (manor) and beige (country).
    - Boards: `Builds/Models/comparison/house_textured.png`, `house_colour_variants.png`.
- **Quality control rework (21 October 2026).** The user found holes (roof tabs, shed window, door edge), black lines (shingle butts, downspout, deck corner) and a rope that read as "a bent square metal tube" in the textured house, shed and swing I had called clean. User: "do them all in order" (gates, no hidden-face deletion + atlas fill, tiling for the buildings, the deck/lattice/downspout construction, the rope from real references, every model through the gates, Unity tests + close review with numbers).
  - **Gates (`tools/blender/qc.py`, both pipelines, a failure stops the build, numbers in `Builds/QC/<model>.json`).**
    - z-fighting is now a hard failure on the old pipeline too.
    - Holes: close cameras (0.25-1.6 m, one per m², at least 300) cast rays the way Unity draws; a back face seen first fails. Two fixes while bringing it up: a camera counts only where it sees the sky (the first "inside" test was fooled by boards pressed on the wall core: 397,243 false hits on the house), and a hit on a shared edge is confirmed by rays shifted 0.1 mm (1 float-precision hit on a siding board's edge).
    - Texels (atlas models): every face over 1 cm² at least 1.5 px across its narrow side; faces under the ground skipped.
    - `drop_hidden_faces` and the tint mask are gone; unused atlas pixels are filled with the nearest colour (push-pull).
  - **Buildings on tiling textures.** House and shed: one mesh each, `<Kind>_<RRGGBB>_<finish>` materials, UVs in metres along each piece's grain, AO and per-board tone in the vertex colour; tiling sets in `Resources/Models/Tiling` (painted wood from CC0 PaintedWood009C, asphalt, concrete, smooth); Opus/Surface `_OPUS_TILED`. Colour variants are back to the material colour. The old atlases were deleted.
  - **House construction.** The deck boards rest on the rim joists and overhang them by 2 cm, joists every 40 cm and a ledger carry them (there was a 1 cm see-through slot); the lattice sits behind corner posts and a bottom rail; the downspout's shoe ends above its splash block; the stair stringers are notched under the treads (they had been cut as one straight slope and stuck up through both treads: the user spotted it in the step shot) and the steps' risers reach under the tread in front and the treads run into the riser behind (a 5 mm light slot under each riser, seen in the game shots: the hole gate cannot see slots that look onto front faces).
  - **Static batching broke the house in game.** Joined into one mesh (over 64k vertices) it passed every Blender gate and the asset tests, but `StaticBatchingUtility.Combine` scrambled its triangles. `YardBuilder.Batchable` leaves meshes over 64,000 vertices out; `BigModelMeshesStayOutOfStaticBatches` tests it.
  - **Rope rebuilt.** Real laid rope and tire-swing rigging studied (3 strands Z-laid, surface yarns along the rope; round turn and half hitches, bowline guides). Three modelled strands along one continuous centreline (lay 3 diameters, as in the 1:07 frame), a generated yarn texture, plain smooth normals. Branch: a 2.5-turn round turn on the bark, leaving from the branch's side (the tire moved 9 cm to hang right under it), two half hitches, a frayed tail. Tire: two half hitches on top, the loop lying on the tire (its hull grown by the rope radius, corners at least one diameter), one leg down the front into the hole as in the video, the tail over the back shoulder. A rope audit measures it: rope-to-rope 1.95 R (touching, never through), tightest bend 0.90 diameter, the loop on the rubber along 67 % (the rest bridges the hole).
  - **Every model through the gates.** Mower: 75 z-fighting pairs fixed (the ridge and rear bar in the cover hollows' wall plane, stacked tops, the brake tab, axle pins starting in the plate's face). Tree: the buried trunk cap no longer counts for texels.
  - **Verification (measured, not judged by eye).**
    - Gates, all 9 models: z-fighting 0, holes 0 (house 3,480 cameras / 5.22 M rays, trimmer 1,800, wheel 600, the others 300), thin atlas faces 0. The pipeline checks (external, settle) pass.
    - Unit tests: 270 of 270 pass (no skips: the atlas test now checks that untextured models have no stale atlas).
    - All 22 scenarios pass; frame times 1.4-4.1 ms.
    - Close-ups in the built game (`u16_showroom`): the deck corner, the deck corner from low down, the step and lattice, the downspout's shoe (behind the fence line: the garden fence hides it from the player), the rope in the video angle (its colour 151/123/90 against 148/120/81 in the 1:07 frame), the knot, the round turn on the branch.
    - Boards: `Builds/Models/comparison/{rope_rebuilt,rope_detail,house_deck_fixed,buildings_tiled}.png`.
    - The moiré on the house roof found here is fixed in the next entry.
- **Roofs: moiré and a closer match to the video (21 October 2026).** User: "fix the moiré and let's work a bit on both the house's and the shed's roof".
  - **Cause, measured.** Down the roof in the 24 m game shot, one-pixel dips about 20 levels darker every 3-6 px: the 12 mm butt face of every shingle row, lit as a face turned down the slope, under half a pixel wide at that distance. MSAA 4x antialiases its edges but not its colour, so the lines flickered in and out of pixels and drew concentric arcs (from 8 m on).
  - **Fix.**
    - Shingle strips take their roof plane's normal on every face (`om.preset_faces(normal=...)`), so a butt shades like the roof.
    - Their UVs run up the slope from each strip's butt, one exposure = 14 cm of texture.
    - The row line, the lit butt edge and the shadow under the next butt are in the new `shingle` tiling set (four rows per repeat, granules and blotches). The mipmaps average it out at a distance.
    - The baked AO is one value per strip (the AO gradient along every strip drew the rows again).
  - **Like a real roof.**
    - Every strip is two exposures + 1 cm long, so there are two layers everywhere and the slots show the strip below.
    - Rows are offset by a third of a strip. With a half offset, the rows that now overlap had their slots in one place and z-fought.
    - The rows reaching the ridge are cut 4 mm apart.
    - The shingles overhang the rake boards by 1-1.7 cm. The porch's rake boards stood above the shingles and drew a dotted white line along its edge from afar.
    - The sheathing runs under the overhang.
  - **Colour, measured against the video (roof-only patches).**
    - House: 0:53/0:57 read about 137/128/128. The game read 153/139/138 and now reads 133-136/123-126/126-128 (base #535F61; the haze at 20-25 m adds a lot).
    - Shed: 0:16 reads 76/73/71; the game reads 75/71/75 (base #2F352D).
  - **Verification.**
    - Dips in three columns down the roof: 11/5/11 before, 0/0/0 now.
    - Gates: house z-fighting 0, holes 0 (3,772 cameras); shed z-fighting 0, holes 0. On the exported FBX, 95 % (house) and 91 % (shed) of the shingle faces turned from the sky carry the roof normal; the rest are ridge caps and sheathing.
    - Unit tests: 272 of 272 pass (new: `ShingleRowsShadeLikeTheirRoof`; the flipped-triangle check leaves shingle triangles to the volume check).
    - Board: `Builds/Models/comparison/roofs_fixed.png`.
- **Joints that do not sit: a gate, and the fixes it found (21 October 2026).** User: the porch's slanted board "does not sit at its start and end; the system should notice and fix this, parts must not come out like this".
  - **The board.** The porch roof's side (rake) boards run down a 15 degree slope, and both of their ends were cut square to that slope, not plumb. At the front the end crossed the vertical fascia: 9 mm proud of it at the top, 4 cm behind it at the bottom. At the wall it hung tilted in the air.
  - **Joint gate (`qc.joint_audit`, in `finish` and `finish_textured`; numbers in `Builds/QC/<model>.json` "joints").**
    - For every long shell (board, rail, pipe), its end faces are tested against the faces of other shells within 3 cm. An end that crosses such a face at 3-60 degrees, with corners more than 3 mm on both sides of it (none further than 6 cm), is cut at the wrong angle.
    - Skipped: chamfer strips under 1 cm, and parallel members (lapped boards run behind each other by design).
    - `tools/blender/checks/joints.py` (in `Models.ps1 -Checks`) proves it still finds a square-cut sloped board against a fascia and passes a plumb cut and a lap.
  - **What it found on the house (31, then 12, then 0).**
    - The porch rake boards (the user's find): now cut plumb at both ends, at the wall in the facade's plane and in front 2 mm behind the fascia's face.
    - The gable corner boards: cut level, they stood 3 cm into the sloped rake soffit at the corner and 7 cm short of it 14 cm in. Their tops now follow the soffit's slope.
    - The eave corners, where the rake meets the eave:
      - the sloped rake soffit dipped 4 mm through the eave soffit;
      - the rake board ended 4.6 cm below the fascia;
      - the fascia's top showed between the shingles and the gutter.
      Now the rake soffit stops at the house's corner, and the rake board runs on to 6 mm behind the fascia's face, 23.6 cm deep, so it covers the fascia's end and the eave soffit's end (at 20.2 cm a 5 cm sliver of the soffit's end still showed under it at the house's corner, seen in the new `v_house_eave_corner` shot). (At 2 mm behind, the board's and the fascia's 45 degree edge chamfers fell in one plane and the z-fighting gate caught it.) The fascia's top is under the shingles and the first shingle row overhangs the fascia by 1.2 cm. The roof sheathing's eave end is cut plumb.
    - Door casings (2 mm, invisible): the threshold is now 3 mm.
    - The other 8 models: 0 (two shed finds were lapped gable boards, which led to the parallel-member rule).
  - **Verification.** All 9 models: z-fighting 0, holes 0, joints 0; pipeline checks 3 of 3 (external, joints, settle); unit tests 272 of 272; all 22 scenarios pass. New close shots `v_house_porch_corner`, `v_house_eave_corner`. Board: `Builds/Models/comparison/joints_fixed.png`.
- **Material rule, wood variety, a joint helper (21 October 2026).** User: joint errors must not come back in later generations. Two different materials, by their type or the context they are in (not just a repaint), must have logically, subtly different textures, and this applies to every material, not only wood; set a rule so it does not repeat. Download two CC0 textures and sort out the wood.
  - **Material registry and rules** (`MATERIALS` in `tools/blender/opus_model.py`). Every surface of a tiled model names its material with `Model.surface(material=...)`; a colour variant is the same material.
    1. A surface names a registered material of its kind.
    2. No two materials look alike: another texture set, or tone 0.1 / bump 0.15 / repeat ×1.3 apart.
    3. A material with an owner belongs to that model only (the same stuff in another context is its own material). Painted trim, painted metal, glass and concrete are shared.
    - Checked at import (`check_material_registry`), per surface, at `finish(tiled=True)`, and in Unity (`MaterialsFollowTheTextureRules`).
    - `tiling.json` now holds the materials' looks and each tiled model's slot → material map (merged across builds). `SurfaceLibrary.TileLookFor(model, slot)` and `ModelLibrary.ApplySurfaces(..., file)` use it; recoloured variants keep their slot's look.
  - **Materials now.**
    - House: `lap_siding` (the painted-wood photo, grain softened); `painted_trim` (trim, sashes, doors, railing: thick paint, grain barely there, brush streaks); `cedar_decking` (CC0 ambientCG Wood049); `asphalt_blend` (mottled architectural shingles).
    - Shed: `rough_boards` (CC0 ambientCG PaintedWood008A: rough-sawn, weathered paint, nail holes); `asphalt_3tab` (even granules, weathering streaks down the slope).
    - Shared: `concrete`, `painted_metal`, `enamel`, `plastic`, `glass`, `frosted_glass` (each with its own look).
  - **Measured in game** (texture contrast after removing the shading):
    - shed boards 3.37 → 4.95;
    - deck 6.73 → 9.53;
    - house siding 4.00 → 3.75 (smoother on purpose);
    - trim face 5.95 → 5.58 (the grain gone under the paint).
    - Board: `Builds/Models/comparison/wood_materials.png`; the sets: `tile_sets.png`.
  - **Joints by construction:** `om.board(bm, origin, axis, side, up, width, depth, start_plane, end_plane)` builds a member whose ends are cut by the planes of the faces they meet; the porch rake boards use it. The joint gate and `checks/joints.py` stay.
  - **Verification.** House and shed: z-fighting 0, holes 0, joints 0; pipeline checks 3 of 3; unit tests 273 of 273; all 22 scenarios pass.
- **Functional plausibility (22 October 2026).** User: models pass the gates yet contain logical mistakes (knots, cables, parts with no job); generalise the lessons. Done except CI (deferred).
  - **Plan first:** `model.plan(purpose, sources, components, supports, tier, category)` in all eight real generators: every component's role, job, attachment, motion and source (video, image, model or text). It is checked at once: a load path from every component to a support. It goes into the spec and onto the contact sheet (plan card).
  - **Construction gate** (`qc.connectivity`, `qc.check_links`): nothing held by nothing (intersecting or within 3 mm, ground counts); declared `route`s (mower control cable, swing rope, four downspouts) and `attach` points (nozzle trigger hinge) must hold. `checks/plausibility.py` proves what it finds and leaves alone. All nine models: 0 loose pieces, 0 link failures (two false alarms fixed: the calibration marker and the trimmer's line tips are `free=True`).
  - **Preview** (`Models.ps1 -Preview`): structure only, in seconds (house 11 s instead of 162 s).
  - **Independent review** (`tools/Review.py`, skill `.claude/skills/model-plausibility`: principles, categories, lessons, reviewer instructions). Trial on the mower: three findings. The fuel cap and primer bulb had no counterpart in the plan; the plan is now corrected. The missing blade and the bare starter handle are doubtful and put to the user. The trial cost 91k tokens because the reviewer explored the repo; the prompt now limits it to the pack.
  - **Verify:** content hashes; `history.jsonl`; approval state per model (`-Approve`, `-Stats`); a whole-model `total_budget`.
  - **Verification.** Geometry, gate numbers and textures are unchanged against the saved outputs. The exception is the wheel atlas, which differs between any two builds (0.67 % of pixels, mean 0.06 levels: the bake is not deterministic; this was already so). Checks 4/4, tests 273/273, scenarios 22/22.
- **Art style: gates, near misses, process, the full bible (24 October 2026).** User: carry out the research report's remaining steps (5-9) and the bible's gaps. Done:
  - **Gates in `Style.py report`**, each tied to a rule whose `enforce` says fail or report:
    - finish by kind (`finish_by_kind`);
    - triangles against the class budget;
    - chamfers on the hard parts of held tools and machines (`part(no_chamfer=why)` for a cord or a round tube with hidden ends);
    - the chroma ceiling per hue;
    - `exempt` for the calibration chart.
  - **Near misses** (`style.json` near_misses, `lookdev.near_miss_models` shed + tree): glossy plastic, oversaturated, baked shadow, black shadows. Each names its rule. The test showed that one chroma ceiling (from the mower's red deck) let a 2.6 × more saturated tree through. The ceiling is per hue now, and the tree's 3 colours fail. The do/don't page then showed the shed's grey-blue siding turning a loud teal (chroma 22) that still passed: muted colours were allowed in any hue up to twice the style's median chroma (31). The floor is the median now (15.5), and the siding fails. Caught ×2.6: tree 3/3, shed 1/5, house 1/13, mower 1/10; every built model still passes.
  - **`Style.py shots`** (Verify, warning only): each game shot against its trailer frame, both at 480 px. Tolerance is half the sources' spread. 1 of 6 pairs are close (the shed). The game lifts the darkest tones and loses the colour in the shadows: house and tire swing p10 26-49 → 50-58; house chroma in the shadows 17 → 0. The first-person tools' highlights are brighter (p90 78-83 → 89-93). This comes from the game's light and post-processing: the user's decision.
  - **Process:** `Style.py brief` writes the one-page brief. `Review.py decide` takes one decision per round, block-out then final; a note must name a view, and after 3 revisions the decision is escalated to the user. The review pack carries `heroes.png` and `near_misses.png`; a "style:" finding needs a location in the pack.
  - **The bible** has 14 sections:
    - prose in every section;
    - the labelled lineup;
    - a page per asset class;
    - do/don't with a red box where each near miss differs, plus what the measured checks see;
    - the game beside the trailer;
    - the process with the decision log.
  - **Findings left to the user:**
    - the wheel model is 38,080 triangles, above the machine budget of 30,000. It holds 2 wheels and the mower places 4, so about 76k triangles in the game against the mower body's 20.6k;
    - the hose nozzle body's chamfer is 0.8 mm, under the 1.0-2.5 mm band;
    - the game shots above.
  - **Fixed on the way:** `Review.py pack` read the schema-1 rules (it would have crashed on any real pack).
- **Art style as data (23 October 2026).** User: a detailed, researched art-style process for this and other games, plus a visual document. Research: `reports/Oyun art stili tanımlama ve denetim.md` (4 strands). Steps 1-4 of its plan, done:
  - `style.json` schema 2: 16 rules with ids, kinds (9 invariant) and checks; stylisation (anchor, tie-breaker, detail floors, chamfer band, budgets); `style_version` written into every spec.
  - `Style.py` extract on five axes. This game: value median L* 64, RMS contrast 14.9, Fourier slope 1.44, harmony T/X; leave-one-out median 88 % (lowest 65 %, a small wheel crop).
  - `LookDev.ps1`: lineups, 36 material spheres, silhouettes; 19/19 images pixel-identical over two runs.
  - `Style.py bible` → `Builds/Style/art_bible.html` (10 sections, 11 images).
  - Fixed on the way: a dark AO input turned atlas-less meshes black in Blender, and `Review.py` read `tiling.json`'s lists as dicts (a tiled model's approval would have crashed).
  - Verification: style 9/9 inside, tool tests 31/31, checks 5/5, Unity 275/275, scenarios 22/22.
- **Review integrity, support, features, reference comparison (22-23 October 2026).** User: carry out the second set of advice (A, B, E, D, C, F) and report a comparison table with measured results.
  - **A (`tools/Review.py`, `tools/tests/test_review.py`, 15 tests):** packs carry a manifest and an id, and `record` refuses findings for another pack or a changed pack. `carry` needs the same plan, links and features, bounds within 2 cm and pieces within 5 %. Reviews are tied to the *structure*, the user's approval to the *asset* (UVs, surfaces, textures within bake noise). An approved model with an open serious finding or a critical feature unverified is not clean. `Verify.ps1` uses this single implementation.
  - **B:** the plan's supports must be located (`model.support`); the main assembly is what the ground or a support holds (a whole object floating 500 mm up used to pass). `attach(parts=)` and `route(part=, ends=)` name the pieces. `spec.located` lists the components tied to geometry. Eleven new cases (broken and correct) plus the features check in `checks/plausibility.py`.
  - **E:** the mower review with the bounded prompt took 65k tokens, 5 calls and 60 s, against 91k, 20 and 225 s before. Decided findings are listed in the pack and were not repeated. It found one plan gap (the rear flap's hinge pin), now in the plan.
  - **D:** `model.features` on the six complex models (19 features, 13 critical). The house review gave the porch roof a pass; the stairs and the deck support are unverified because no view shows them, and stay open.
  - **C (`tools/Compare.py`, `tools/tests/test_compare.py`, 3 tests):** landmarks → camera solve → leave-one-out errors, segment ratios and an overlay.
    - Shed: rough (RMS 2.96 %); the solved focal matches the game's 60° camera within 2°; the window is 80 % of the frame's width (advisory).
    - Mower: poor (15 %, 7 mostly silhouette landmarks); no measurement given.
  - **F:** `ModelImportTests.DeliveryCostsAreMeasured`: 352k triangles, 402k Unity vertices (1.76 × Blender's), 54 draw calls. Measured only; no LODs.
  - **Found on the way:** the house and shed board tones changed every build (`hash()` seed → crc32). The fingerprints depended on mesh order; they are now order- and diagonal-independent. The wheel's UVs and atlas were not reproducible either: bmesh operators and the bevel modifier order faces, and where each face starts, by memory address. `om.canonical_order` / `om.canonical_mesh` now order every part by geometry (custom normals kept: 0.0000° change), and two wheel builds give identical UVs, atlas bytes and FBX meshes; `checks/determinism.py` fails without the fix.
  - **Verification.** 9/9 models with unchanged stats and gate numbers; tool tests 18/18, checks 4/4, Unity tests 274/274, scenarios 22/22. One timeout (`u7_scene`, a hang at engine start) passed on rerun.
- Next: the mower's face-like starter and boxy rear; U16's job model polish suggestions (a mailbox job board, weather/scenery differences for client gardens, changing weeds, price balance). U16's job model polish suggestions (a mailbox job board, weather/scenery differences for client gardens, changing weeds, price balance) are waiting.

### CLI details learned in U0
- On first launch in an empty folder Unity adds the default template packages (ads, purchasing, analytics…) to `Packages/manifest.json`; after the first setup the manifest was written back to its lean form.
- `AssetDatabase.ImportPackage` and `-importPackage` stop halfway when run with `-quit` → TMP Essential Resources are unpacked directly from the tar.gz with `UnityPackageExtractor` and imported synchronously.
- In a batchmode player cameras and uGUI canvases are not rendered; a URP render request does not render the uGUI canvas either. So the scenarios run in normal mode, with the game window embedded in a visible parent window off screen through `-parentHWND` (in a fully hidden window Unity stops rendering after a few frames).

## 5. Lessons from the Codex prototype
The neighbouring folder `../Copycat` (Codex) is a prototype with the same scope; I only looked at its screenshots and CLI layout without touching its code. Where we need to make a difference:
- There **mowed grass is dark green**; in the video mowed grass is **a lighter and brighter lime green** than long grass.
- The colours are very saturated and contrasty; the pastel, hazy, lavender atmosphere, the sky and the distant painted trees of the video are missing.
- No stripe pattern, patch quilt (colourful sharp borders), cyan highlight, orange border or finish effect.
- A useful lesson: with the new Input System on, configuration and build have to happen in **separate Unity runs**; screenshots can come out black in a hidden window → the camera RenderTexture method.

## 6. Risks
- **Grass performance and look**: the biggest job. Early measurement in U2, adjustable density.
- **Visual tuning through the CLI**: no live tuning in the editor; we compensate with scenario screenshots + side-by-side comparison + colour/density values read from a settings file (JSON) (values can change without rebuilding the exe).
- **Batchmode times**: the first launch can take a few minutes; long jobs run in the background.

## 7. Copyright note
The mechanics are free to use; the "18 Summers" name, logo, texts and models will not be used. Our own name, our own texts and our own simple models.

## 8. Decisions
1. **Font**: *Patrick Hand* (OFL, Google Fonts) — `Assets/Opus/Fonts/`.
2. **Game language**: the interface was Turkish until 20 October 2026; the whole project, including the game, is now English (the user's decision).
3. **Progression**: §3-A, unlocking areas through gates.
4. **U10 progression models (19 October 2026)**: §3-C (day cycle + regrowth), §3-D (tool upgrades), §3-E (pattern mode), §3-F (small items; no toys, only ordinary items lost in the garden).
5. **Detailed models**: the tools first (mower, trimmer, hose); garden props later.
6. **Visuals (19 October 2026)**: "Simple visuals do not mean plastic": props are drawn not with flat colours but with hand-painted stylised textures and materials (textures generated from code, wear/grime, a ground proximity shadow) (done in U12, §9.3). New props and areas are built with a material kind from the start; then the data-driven garden, new areas + LOD, the job/home model.

---

## 9. Next work: proposal (19 October 2026)
An assessment and a suggested order for the three topics the user asked for (§3-B job/home model, grass LOD, new areas).

### 9.1 Current state (measurements and structure)
- **Grass cost:** in the 1080p overall framing the frame is 3.2 ms at full density, 2.2 ms at half density (`u2_grass`). So ~2 ms of the frame is grass; 12 blades × 7 triangles per tuft. 300+ fps on the RTX 4060. LOD is not needed for speed today; it is needed for weak GPUs (integrated graphics) and a growing garden.
- **The garden data is hard-coded:** `YardBuilder` keeps the areas, 22 seeds, patch names, dry spots and weeds in static arrays. `GateTo` only knows area 2. `YardVisibility` assumes a single fence line (x = 18). Some scenarios look at `Areas[1]` directly. Both new areas and the job/home model need this structure generalised first.
- **Save:** the patch map digest is written into the save; when the grid or patches change an old save is deliberately rejected. New areas would invalidate today's saves; a migration decision is needed.

### 9.2 Suggested order (approved by the user, 19 October 2026; a materials stage inserted)
1. **U12: Materials and textures (1–1.5 stages).** The user's feedback: "The graphics do not look alike, it still looks like plastic. Simple visuals do not mean plastic; they mean textures and materials too." This is the most visible problem and independent of the other work. The later areas and gardens are built with these materials from the start.
2. **U13: Data-driven garden + multi-gate areas (infrastructure, 1 stage).** New areas and the job/home model rely on this.
3. **U14: New areas + grass LOD (1–1.5 stages).** Uses all the existing systems right away with new content. Since the garden grows, LOD makes sense here.
4. **U15–U16: Job/home model (§3-B, 2 stages).** The biggest structural change; its details are decided after U14 is played. The home garden (upkeep with the day cycle) + client gardens (one-off jobs) together.

### 9.3 U12: Materials and textures
**Why it looks like plastic:**
- `Opus/Primitive` gives every surface a single flat colour; no texture, colour variation, shaded corners or grime.
- A whitish rim light is added to the soft light; that is what gives the smooth plastic effect.

**What the reference has (close crops):**
- Shed: hand-painted board siding, shadow lines between the boards, colour variation, brown grime stains on the lower walls and doors, textured dark shingles.
- Tree trunk: bark with big dark paint blotches.
- Flowerbed curb: speckled concrete.
- Overall: not photorealistic, **hand-painted stylised textures**.

**To do:**
- **Texture library (painted from code, no downloads):** tileable, watercolour/hand-painted style textures. Painted boards and lap siding, vinyl fence, shingles, brick, concrete/stone, bark, soil/mulch, rubber, painted metal, painted plastic (tools), foam/fabric, glass.
  - Generated as assets by `Setup` in the editor/CLI (`Resources/SurfaceTextures`); not computed at startup, reproducible.
  - ~~A light normal map is also generated from the height information~~ → in practice the joint/shingle/bark shadows were painted into the texture (closer to the hand-painted style, no extra texture read).
- **A new surface shader (`Opus/Surface`):**
  - Triplanar mapping needing no UVs. In world space (statically merged garden props) or object space (tools; so the texture does not slide).
  - Parameters per material: texture scale, colour variation (low-frequency noise), roughness, texture/joint depth.
  - A grime and wear layer: a paint stain mask in world space. Darkens near the ground and at the bottom of vertical surfaces (like the shed stains in the reference); strength per material.
  - A ground proximity shadow (AO): surfaces closer than 30 cm to the ground darken.
  - The rim light is removed or greatly reduced.
  - The existing lavender shadow, wrap light and fog are kept.
- **Material catalogue:** `MaterialFor(kind, colour)` instead of `PrimitiveKit.MaterialFor(colour)`. Example kinds: Wood, Siding, Vinyl, Shingles, Brick, Concrete, Stone, Bark, Leaf, Soil, Rubber, Metal, Plastic, Glass, Water, Fabric.
  - The house, shed, fence, gate, flowerbed, tree, birdbath, compost, tools, weeds and lost items are built with their kinds.
  - Since merging (MeshBaker / static batching) works per material, the draw count is kept.
- **Verification:**
  - Side by side with the reference crops (shed, tree trunk, curb, fence, veranda), colour sampling in the same light.
  - A before/after comparison of the showroom scenarios (`u11_props`, `u10_tools`).
  - Frame time (triplanar mapping ~3–6 texture reads per pixel; target under +0.5 ms in the overall framing).
- **Done:** up close the props read as painted surfaces, not flat colours; the shed, tree and curb look like the same family next to the reference; the 16 scenarios and tests pass.

### 9.4 U13: Data-driven garden
- `YardDefinition` (plain C# data; JSON later if wanted): areas (rectangle + name), a **gate list** between areas (which two areas, fence line, opening), seeds/patch info, dry spots, weeds, prop placements (house, shed, flowerbed, tree…), spawn point.
- `YardBuilder` builds from the definition; today's garden becomes the first definition. The behaviour must stay exactly the same: all scenarios and tests pass unchanged.
- `GateTo(area)` → the gate in the definition; the unlock chain "when an area is finished the gates attached to it open".
- `YardVisibility` generalises with the gate list: every gate is a portal piece; when the eye and the target are in different areas, visibility through the gates between them. Today's pixel-for-pixel accuracy test is repeated for every gate.
- Save v3: definition id + version. If the same definition changes, migration by area (the old areas' grass/weed data kept, new areas fresh).
- **Done:** the existing 16 scenarios and tests pass unchanged. A 3-area test garden built from a definition is built in a test and the occlusion and gate chain are verified.

### 9.5 U14: New areas + grass LOD
- **Two suggested new areas** (in the home garden, with the gate chain):
  - *Area 3 "Vegetable garden"* (to the west): raised wooden beds with narrow grass paths between them (trimmer-heavy work), watering-heavy dry soil spots, a railing/trellis, a wheelbarrow, a rain barrel.
  - *Area 4 "Orchard"* (to the south, beyond the fence): rows of trees (passing between them with the mower), fallen apples (collectible points like lost items), long wavy grass, flat ground instead of a slope.
  - Each area 5–8 patches with its own weeds and dry spots; the day cycle, pattern task and items work automatically.
- **Grass LOD:**
  - Two tuft meshes: near (12 blades × 4 segments) and far (8 blades × 2 segments, slightly wider blades).
  - Selection per chunk by distance, a delayed switch at the boundary (no flicker).
  - The cut/long look comes from the same shader data.
  - Target: a 25–35% drop in the grass cost.
  - Verification: the image difference and time with LOD on/off in the same framing (like the occlusion test in U9).
  - The "Grass density: Low" setting also switches to the far LOD earlier.
- **Done:** the two new areas unlock through gates; a new scenario (`u14_areas`) finishes every area; frame time < 6 ms with the whole garden visible (RTX 4060); the image difference with LOD is unnoticeable.

### 9.6 U15–U16: Job/home model (§3-B)
- **Structure:** the player's **home garden** (today's garden, day cycle and upkeep) + **client jobs**. A job board as a mailbox next to the house door or in the break menu.
- **Client gardens** (with `YardDefinition`, each with a different character):
  - a small city garden (a short job, many obstacles);
  - a wide manor lawn (pattern requirement, a big area);
  - a neglected country house (many weeds, ivy, dry spots).
- **Flow:** pick a job → transition screen → the client garden is built → the tasks are finished → rating card → back home. Rating 1–3 stars: completion, time, the requested pattern. Payment by stars.
- A job counts as a "day": when you get back it is night in the home garden (tied to the upkeep cycle). Points, upgrades and the collection are shared; every client garden's unfinished job is in a separate save.
- **Technical:** tearing down and rebuilding the garden within the scene (grass, weeds, props, sound, occlusion reconnected). The build time is measured (target < 1 s). A client garden's scenery/sky tone can be slightly different.
- **Done:** the three client jobs are played from start to finish, stars and payment correct; on returning to the home garden the save and the day are consistent; scenarios (`u15_jobs`, `u16_rating`).

### 9.7 Decisions the user needs to make
1. ~~Order~~: the suggested order was approved; a materials and textures stage (U12) was inserted.
2. ~~New area themes~~: the vegetable garden and the orchard were approved and done in U14.
3. ~~Old saves~~: migration by area was done in U13 (unchanged areas kept, new areas fresh).
4. ~~The number of client gardens and the reward balance in the job/home model~~: the suggested 3 gardens and payment by stars were approved and done in U15 (250–1,200 points).
