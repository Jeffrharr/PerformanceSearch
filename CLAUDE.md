# PerformanceSearch

## Harmony patches

Prefer Prefix/Postfix patches over Transpilers. Transpilers operate on IL directly,
break silently when Ludeon reshuffles method bodies, and are hard to read and maintain.
Use a Transpiler only when no Prefix/Postfix combination can achieve the same result.

## About.xml description

The description must stay in sync with what is actually merged to master — do not
describe fixes that are only on a branch.

Format: a one-line intro followed by a bullet per fix. Each bullet should be
understandable by an average RimWorld player with no modding knowledge:
- Name the thing they interact with (search box, storage tab, architect menu)
- Describe the symptom they experienced (stutter, freeze, lag)
- Describe the result (faster, no lag, smoother)
- No technical terms (no "debounce", "LINQ", "IL transpiler", "GlobalBills")

When a fix is merged, add its bullet immediately as part of the same PR.
