---
name: art-style
description: Maintain and apply Copycat Opus 3D asset style guidance from reference images and user feedback. Use for this game's asset style rules, scoped modelling briefs, or visual style reports; not for lighting/post-processing or 2D concept selection.
---

# Reference-driven 3D style

Work in this game checkout. Sources are `tools/blender/style/style.json` and `references.json` beside it. Do not update the standalone pipeline as part of this workflow.

## Daily asset work

Run `py -3 tools/Style.py brief NAME --class CLASS` (known models infer their class). Read the resulting short brief and its one numbered reference board in `Builds/Style/briefs/`. For material-specific work use `StyleGuide.py brief NAME --class CLASS --materials MATERIALS`.

Load detailed guidance only for relevant rule IDs in `Builds/Style/style_guide_ai.md`. Do not reread the whole guide or every reference for each model. Use existing sources and captures; research or render only to answer an actual missing question. The 2D concept-design skill does not apply to 3D modelling.

The brief distinguishes source evidence, interpretation, user decisions and technical choices. Draft guidance is advisory. Lit reference pixels are not unlit material colours. Current models are implementation examples, not the source of the target style. Only explicitly approved exemplars whose stored hashes still match may serve as approved examples. Ordinary asset approval does not expand the style palette or approve the guideline.

## Define or revise guidance

Register each image with its intended role and class scope. Keep source groups and conflicting directions separate; do not average unlike references into one target. Trace instructions to relevant image regions or actual user decisions. Label uncertainty and explain what an image cannot establish.

`py -3 tools/Style.py bible` generates both outputs from the same source:
- `Builds/Style/style_guide_ai.md`: detailed AI-readable rules and provenance;
- `Builds/Style/art_bible.html`: illustrated human report, with the same rule IDs and status.

`py -3 tools/Style.py extract [IMAGE ...]` records optional per-image measurements. It does not alter the rules, pool a target palette, recover albedo or approve models. Keep artistic thresholds advisory unless their scope and evidence justify a specific check.

Use `StyleGuide.py snapshot --out PATH` before a change and `StyleGuide.py impact PATH` afterward to list affected rules/classes. `StyleGuide.py revise RULE --text TEXT --note USER_FEEDBACK` records a targeted user decision: use it only for feedback the user actually gave. Do not fabricate feedback or infer a new global style from approval of one asset. `add-reference PATH --id ID --roles ROLES --scope CLASSES` adds evidence without changing rules; interpret the new source separately.

A documentation revision does not require mesh rebuilding, new pilots, a fresh independent review or a Unity run. Keep geometry/reference checks in their existing workflow. After a relevant asset change, use existing technical checks and a focused visual look; do not present numerical success as visual approval.

## Review and cost

`Review.py pack` adds the asset's selected brief and at most one board, not the whole guide. A style finding names the rule, evidence and location in a pack image; do not restate decided findings without new evidence. `Style.py report` reports technical rules and ordinary asset review state separately from visual style.

For an unregistered model, use `Review.py pack NAME --class CLASS`. Without a known class, review still runs with common rules and marks class-specific style assessment incomplete; choose the class before claiming that assessment is complete.

Printed token counts are estimates for selected text. They exclude image processing, system context, chat history and tool output. Local generation saves repeated context; it does not make reading images or text cost-free. Regenerate documents locally and inspect changed sections; avoid repeated full audits for editorial changes.
