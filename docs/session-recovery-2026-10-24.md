# Copycat Opus session recovery — 24 October 2026

## Purpose and sources

The user asked to inspect work saved before a context-overflow failure, finish any interrupted work and carry the useful context into the new chat. This record is a concise continuation note, not a full transcript import or a claim that the broken chats were repaired.

Workspace: `C:/Users/Baris/Documents/GitHub/Copycat Opus`.
Branch: `codex/style-guideline`.
New chat: `01a16f80-b472-7a02-aa11-c5f171d356f4`.

Relevant chats read through the app's thread tools:

- **Copycat game planlama** — `01a1509f-7796-7981-9d64-7165c59bc945`.
- **Copycat game planlama (2)** — `01a16f78-35f2-7483-8c9c-24bfcc7dbfc7`.
- **İncele ve devralma notlarını bul** — `01a16af8-d706-7222-9368-84ead7bbc1be`.

The first two chats failed while compacting, with `context_length_exceeded` / HTTP 400. Their latest continue/compact requests contain no subsequent implementation work. They include an older imported game-development history. Its final Verify result is also preserved in `Logs/verify-full.txt`: 273 tests passed, build OK, 22/22 scenarios passed. Those are historical results, not a fresh full-game validation.

The third chat contains the more recent style-guideline work matching the current working tree. Its final turn stopped at an account usage limit, after code, tests, documentation and generated outputs had been written. These are separate failures and separate work histories.

## Current objective and user decisions

The project is a Unity lawn-care game with a procedural Blender modelling pipeline. The current style task concerns 3D asset guidance, scoped modelling briefs, review tooling and their illustrated report. Runtime lighting and post-processing are separate work.

The latest implementation request was to remove token-saving restrictions that reduced quality, fix the reported guidance defects, explain bevel/chamfer, and defer the multi-game experiment. The game checkout is the scope; the sibling `opus-model-pipeline` repository is not.

Continue in Turkish with the user. Reuse existing references and analysis. Do not load the 2D concept-design skill for Blender modelling, mesh edits, rigging, exports or Unity validation. Technical success does not constitute visual or user approval. Do not modify player saves/settings or expose API keys. Do not publish the copyrighted reference frames in `docs/reference/`.

## Saved implementation verified

The latest recorded commit is `cd436ed` (unclassified review fixes and synchronized review instructions), after `671ea68` (the initial reference-backed style guide). The following newer fixes are present locally but uncommitted:

- `tools/StyleGuide.py`: complete relevant production instructions, avoid lists, rationale, material finishes and colour targets; all selected boards and full-resolution crops; no hard text/image cap; feedback preserves detailed fields and full before/after history, with explicit reconciliation status.
- `tools/Style.py` and `tools/blender/style/style.json`: draft role-based colour checks with explicit measurement coverage; bevel checks for missing/invalid widths rather than a universal millimetre band; obsolete schema-2 execution and trailer scoring paths removed.
- `tools/Review.py`: the current review pack consumes the complete scoped guidance. Existing unclassified-model support is preserved.
- `.agents` and `.claude` art-style/model-plausibility instructions and `README.md`: synchronized with the new workflow.
- Tests include the new, currently untracked `tools/tests/test_style_feedback.py`; preserve it with the rest of the change set.

Sources of truth are `tools/blender/style/style.json` (schema 3, guideline revision 2) and adjacent `references.json`. The reference set has nine crops from seven images of this game's trailer. The visual guidance and numerical colour targets remain drafts, not user-approved standards or measured unlit albedo.

## Recovery verification and completed follow-up

- Independent read-only review found no concrete functional regression in the saved changes.
- `py -3 -m unittest discover -s tools/tests`: **69 tests passed**.
- `Style.py validate` and `StyleGuide.py validate`: passed.
- `Style.py report`: **8 production models pass technical checks**, calibration exempt. `wheel` has a non-blocking triangle-budget warning: **38,080 versus 30,000**.
- `Style.py bible`: **changed: false**, revision **2**. The report, AI guide and sample briefs already match the source data. No missing report-generation patch was found.
- Browser verification: 36 report images loaded, 0 broken images, 0 JavaScript errors, no horizontal overflow at desktop/mobile widths; the machine filter showed 14 matching/common rules and 0 unrelated rules. The cover and colour section were visually inspected; `report_preview.png` was refreshed.
- Art-style and review instruction copies are identical; the model-plausibility skill differs only in its intentional `.agents` / `.claude` reviewer path. `git diff --check` passed.
- The implementation-plan delivery record was updated; the historical handoff now points to this continuation record.
- No Unity/Blender build was needed for these tooling/documentation checks. No game code, model generator or geometry was changed during recovery.

Generated documents (ignored by Git, reproducible locally):

- `Builds/Style/art_bible.html` — illustrated report.
- `Builds/Style/style_guide_ai.md` — detailed AI guide.
- `Builds/Style/briefs/` — scoped production briefs and reference images.
- `Builds/Style/guide_manifest.json` — source hashes, revision and output statistics.

## Remaining work and continuation boundary

The interrupted style-tooling implementation is verified. Remaining visual work is not silently approved: several asset reviews are missing/outdated, critical-feature verdicts are unverified, and source textures often have no numeric colour measurement. The wheel budget warning also remains.

The user explicitly deferred the multi-game reference experiment. A new model has not yet been produced and visually validated end to end using this revised guide. Continue with the user's chosen game/model task; do not launch a multi-game research project merely to close this note.

At the end of the 24 October recovery, the local fixes and recovery documentation were not yet committed or pushed. Existing historical commits had been pushed. The old failed chats remain intact.

## 25 October follow-up: build upload and separate commits

GitHub metadata showed the repository as public, contrary to the earlier private-repository description. After this was explained, the user selected only the Windows game build for upload. Commit `83e4c71` was pushed to `codex/style-guideline`: 168 player files (227.23 MiB) tracked with Git LFS, plus the Git tracking configuration. The Burst `DoNotShip` debug folder, other generated outputs, machine caches and backups remain ignored. The remote commit matched the local commit after upload.

The user then requested that the remaining changes be committed in appropriate groups:

- `706ffd8` — schema-3 style checks, role-based colour coverage, explicit bevel policy and legacy scoring cleanup. Its isolated index snapshot passed 62 tests.
- `3fcb3e1` — complete scoped guidance, reference delivery, review packs and preserved feedback history. Its isolated index snapshot passed all 69 tests.
- A separate documentation commit contains the mirrored skill instructions, README, implementation plan and recovery records.

These three follow-up commits are local; the request at this stage was to commit them. The Windows build upload remains the latest pushed revision.
