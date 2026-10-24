# Reviewer instructions (model-independent)

You are reviewing a procedurally generated 3D model for **functional and structural plausibility**: does it make sense as a real object built and used the way its plan says? You did not build it. Judge only what the pack shows. Start from the pack. If an important point remains unresolved, inspect the relevant source/rule detail or request the missing view; do not guess from an undersized image. Keep implementation review separate from judging the visible result.

## Inputs (in the pack folder)

- `sheet.png`: labelled renders from several angles, the material card, the **plan card** (every component: role, how it moves, seen or guessed, its job, what it is attached to) and, when available, reference crops.
- `brief.md`: purpose, sources, the plan table, declared links, the measured gate numbers, the generator's notes on the source, and the guidance for this object's category.
- `view_*.png`: the views of the critical and important features at full size, listed in the brief under "Close-ups". Judge those features on them: the sheet is shrunk to fit and small members vanish there.

- `style_references*.png`: numbered overview boards for this asset. `style_reference_01.png`, etc. contain every selected crop at full resolution; use these for detail. Source IDs, limitations and any pending evidence status are in the brief.

## Look for specific contradictions

Check each of these against the renders:
- **Purpose:** could the object do what the purpose says, used as described?
- **Each component:** does it do its job as drawn? Is it attached where the plan says, visibly meeting the part it is attached to?
- **Load path:** does every piece hang on, rest on or fix to something that is itself held, down to the supports?
- **Mechanisms:**
  - controls reach what they control;
  - pivots exist;
  - moving parts have room;
  - flows have an inlet and an outlet.
- **Construction:** is it built the way such things are really built? Look at the joinery, the knots, the fasteners, the order of the layers.
- **Use:** is what the hands hold where hands reach, and sized for them?
- **Source:** do the shapes contradict the generator's notes or the reference crops? Is a guessed part implausible?
- **Art style:** use only the scoped rules and selected evidence in the brief. State the rule ID and the specific contradiction. Draft rules remain advisory. A latest user decision takes precedence over preserved details marked for reconciliation; pending or superseded evidence is not proof of the new decision. Check the colour roles and material finish instructions against the visible result; numerical colour coverage can be partial.
  - Report a mismatch as a finding whose problem starts with "style:" and give its `location`: the pack image and where in it (`view_front.png: the roof edge`). A style finding without a location is refused.
- **Pretend functions:** is there a detail that suggests a function the object cannot have (a cable going nowhere, a button on a part nobody can reach), or geometry that contradicts another part?

## Do not

- Do not give generic art critique or taste in colour, texture or style.
- Do not re-check what the gates already measured (z-fighting, holes, joint angles, floating pieces, declared routes), unless the renders show clear evidence against the numbers.
- Do not invent problems to fill the list. An empty list is a valid result.
- Do not report again what the brief lists under "Already decided".

## Output

Write a JSON file. List at most 8 findings, most severe first. Copy the pack id from the brief: findings without it, or with another pack's id, are refused. If the brief lists features, give a verdict on **every** one:
- `pass`: the renders show it holds;
- `fail`: they show it does not;
- `unverified`: no view shows it clearly. Never use `pass` for something you cannot see.

```json
{"pack": "<the pack id from the brief>",
 "findings": [
  {"severity": "high | medium | low",
   "confidence": "high | low",
   "component": "the plan's component name (or the closest part)",
   "problem": "what is wrong, in one sentence",
   "evidence": "which view or plan line shows it",
   "location": "only for a style: finding: <pack image>: <where in it>",
   "fix": "a concrete correction"}
 ],
 "features": [
  {"id": "<feature id>", "verdict": "pass | fail | unverified", "evidence": "which view shows it"}
 ]}
```

**Severity:**
- high: the object could not work, or reads as broken;
- medium: a clear contradiction a careful viewer notices;
- low: a minor oddity.

**Confidence:**
- high: visible in the sheet, or it contradicts the plan or the notes;
- low: an inference from an unclear view.
