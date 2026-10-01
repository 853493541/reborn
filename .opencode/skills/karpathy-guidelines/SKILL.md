---
name: karpathy-guidelines
description: Use when implementing or editing code to avoid common LLM coding mistakes — think before coding, keep changes minimal and surgical, and stay on the stated goal. Adapted from the community "Karpathy guidelines" behavioral rules (multica-ai/andrej-karpathy-skills).
metadata:
  source: https://github.com/multica-ai/andrej-karpathy-skills (adapted summary, no license stated upstream)
---

# Karpathy guidelines (adapted)

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific
instructions (this repo's `AGENTS.md` wins on any conflict).

**Tradeoff:** these bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think before coding

Don't assume. Don't hide confusion. Surface tradeoffs.

- State assumptions explicitly; if uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so; push back when warranted.
- If something is unclear, stop and name what is confusing.

## 2. Simplicity first

Minimum code that solves the problem. Nothing speculative.

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility"/"configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you wrote 200 lines and it could be 50, rewrite it.

Ask: "Would a senior engineer call this overcomplicated?" If yes, simplify.

## 3. Surgical changes

Touch only what you must. Clean up only your own mess.

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- Notice unrelated dead code? Mention it — don't delete it.
- Remove imports/variables your own change made unused; leave pre-existing ones.

Test: every changed line should trace directly to the request.

## 4. Goal-driven execution

Define success criteria. Loop until verified.

- "Add validation" → write tests for invalid inputs, then make them pass.
- "Fix the bug" → write a test that reproduces it, then make it pass.
- "Refactor X" → tests pass before and after.

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
```

Strong criteria let you loop independently; weak ones ("make it work") require constant
clarification.

---

These guidelines are working if: fewer unnecessary changes in diffs, fewer rewrites from
overcomplication, and clarifying questions come before implementation rather than after
mistakes.
