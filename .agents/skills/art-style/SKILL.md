---
name: art-style
description: Define an art style (from a video, images, a moodboard or a description), get it approved, and keep every new 3D asset inside it - measured colours (tools/Style.py), written rules (tools/blender/style/style.json), a style board, a check in Verify and in the review. Use when starting a project's look, before making any new asset, or when the style should change.
---

# Art style

The style is one file, `tools/blender/style/style.json`, and one image, `Builds/Style/style_board.png`. Everything else follows from them:
- **measured**: the colour gamut and palettes, extracted by `tools/Style.py`, never typed by hand;
- **rules**: shape, edges, surfaces, finish, colour handling, detail and budgets, written in words;
- **history**: every change to the style, with its date and reason.

Two kinds of colour are kept apart:
- **lit:** what the sources look like on screen. Game shots are compared with these.
- **albedo:** the base colours of the models that define the style. New assets' base colours are checked against these.

Comparing a base colour with a lit pixel is wrong: the game's light and haze soften it.

## 1. Define a style (once per project, or when the user changes it)

1. **Sources:**
   - the frames or images that show the look (a trailer, screenshots, a moodboard, concept art);
   - for a description only: find images first (model-plausibility step 2b) and show them to the user.
2. **Measure:** `py -3 tools/Style.py extract [images...]`. It writes the lit gamut and palette from the sources and draws the style board. With no images, it uses the reference crops the generators name.
3. **Write the rules** in `style.json`: shape language, edges, surfaces, finish, colour handling, detail and triangle budgets, and the summary sentence. Keep them short and checkable.
   - Good: "hard parts get a single-segment chamfer of 1-2.5 mm".
   - Bad: "nice edges".
4. **Pilot:** build one or two typical assets by the rules (one hard-surface prop, one organic). Show the user the style board and the pilots' contact sheets in one message, and ask one question: is this the style?
5. **Lock it:**
   - the user approves the pilots (`tools/Verify.ps1 -Approve <models>`);
   - run `py -3 tools/Style.py extract` again, so the base-colour envelope now comes from the approved models only;
   - add a `history` entry.

## 2. Apply it to every new asset

- **Before the plan:** read `style.json`'s summary and rules (they are short), and the board if the asset is a new kind of thing.
- **Colours:**
  - take them from the albedo palette, or sample them from the source and keep them inside the albedo gamut;
  - a colour that must stand outside (a warning lamp) gets `style exception: <why>` in its surface note.
- **Build:** `tools/Verify.ps1` runs `Style.py report` after the models. A base colour or an atlas outside the style fails the run with the surface's name.
- **Review:** every review pack carries `style_board.png` and the rules. The reviewer reports style mismatches as findings starting with "style:" (a glossy plastic look, sharp edges, a foreign colour, toy-like detail).
- **Look:** in the built game, compare the asset's shot with the board's lit palette. This is the one check that sees the light.

## 3. Evolve it

- The style changes only when the user decides.
  1. Edit the rules.
  2. Add a `history` entry with the date and reason.
  3. Run `Style.py extract` again.
  4. Run `Style.py report` over all models to see which ones now fall outside.
  5. List those models for the user rather than rebuilding them unasked.
- Approving a new asset that widens the look is also a style change, because the next `extract` takes it in. Say so when asking for the approval.

## Cost

`Style.py` runs locally, so it costs no tokens beyond its one-line output. Look at the board once per style change, not for every asset. The reviewer already receives it in the pack.
