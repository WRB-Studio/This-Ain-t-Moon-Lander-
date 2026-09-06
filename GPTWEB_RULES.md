# GPT Web Development Rules

These rules apply to all ChatGPT Web development work in this repository.

## Git safety — non-negotiable

1. **Never work directly on `main`.**
   - No commits, file updates, deletions, merges, or other writes to `main`.
   - If a planned write targets `main`, stop and use the GPT Web branch workflow instead.

2. **Permanent GPT Web integration branch:** `gptweb/main`
   - All GPT Web work integrates here first.
   - `gptweb/main` is the base for new GPT Web feature/fix branches.
   - Do not create normal GPT Web work directly from `main` once `gptweb/main` exists.

3. **Every implementation uses its own branch.**
   - Feature: `gptweb/feature-<name>`
   - Fix: `gptweb/fix-<name>`
   - Keep one logical change per branch.

4. **Merge direction:**
   - `gptweb/main` → `gptweb/feature-*` / `gptweb/fix-*`
   - completed branch → `gptweb/main`
   - **Never automatically merge `gptweb/main` into `main`.**
   - `gptweb/main` → `main` only when the user explicitly decides to do that after local testing/review.

5. **Before every GitHub write, verify the target branch.**
   - `main` must never be the write target.

## Required context before changes

Before implementing a feature or fix:

1. Read `GPTWEB_RULES.md`.
2. Read `README.md` and `GAME_CONCEPT.md` when relevant to gameplay/design.
3. Inspect the existing implementation and its dependencies before changing code.
4. Prefer the project's existing style and structure over introducing a new architecture.

`GAME_CONCEPT.md` is an evolving pool of ideas, **not a fixed specification**. New ideas must be discussed/defined as needed rather than assumed to be mandatory.

## Development principles

- Extend existing systems instead of rebuilding them without a clear reason.
- Keep Unity/C# code simple, readable, and mobile-friendly.
- Avoid unnecessary managers, interfaces, abstractions, frameworks, and design-pattern overhead.
- Do not refactor unrelated code while implementing a feature.
- Do not create side work that is not needed for the requested task.
- Preserve Unity serialization where practical; avoid unnecessarily breaking `SerializeField` references, prefabs, scenes, and inspector setup.
- Avoid modifying packages, `ProjectSettings`, scenes, prefabs, or other broad project configuration unless the feature genuinely requires it.
- Consider mobile performance and allocations, but do not prematurely optimize simple code.
- Reuse current project conventions when several technically valid solutions exist.

## Unity Editor work

If manual Unity Editor work is required:

- State it briefly and precisely.
- List only the necessary steps/fields/references.
- Do not block code work unnecessarily just because the user may currently be on a phone and unable to open Unity.
- Prefer solutions that minimize repetitive manual editor setup when this does not complicate the code.

## Feature workflow

1. Inspect relevant code and dependencies.
2. Create a branch from `gptweb/main`.
3. Implement only the requested feature/fix.
4. Review the resulting diff for unrelated changes and likely Unity serialization issues.
5. Merge the completed branch back into `gptweb/main`.
6. Report briefly:
   - what changed,
   - any required Unity Editor steps,
   - anything that still needs local testing,
   - relevant risks or assumptions.

## Bug-fix workflow

When the user reports an error:

- Inspect the repository and find the actual cause before patching symptoms.
- Use a `gptweb/fix-*` branch from `gptweb/main` unless the bug belongs to a still-open feature branch.
- Keep the fix focused.
- Merge the completed fix into `gptweb/main`.

## Project direction

The game begins as an intentionally simple Lunar-Lander-style game and gradually reveals itself as a hidden space open-world sandbox.

New systems should generally be unlocked through **curiosity, discovery, and breaking the apparent rules**, rather than by openly announcing a large feature set upfront.

The original lander gameplay should remain meaningful as the world expands.
