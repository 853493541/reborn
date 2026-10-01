---
name: skill-creator
description: Create new skills, modify and improve existing skills, and reason about skill triggering. Use when the user wants to create a skill from scratch, edit or optimize an existing skill, or make a skill's description trigger more reliably.
license: Apache-2.0
metadata:
  source: https://github.com/anthropics/skills (skills/skill-creator)
  copyright: Copyright 2026 Anthropic, PBC
  modified: trimmed adaptation — the upstream bundled eval tooling (scripts/, agents/, eval-viewer/, references/, assets/) is NOT included here; fetch the source repo if you want the quantitative eval loop
---

# Skill Creator (adapted)

A skill for creating new skills and iteratively improving them. This is a trimmed
adaptation of Anthropic's skill-creator: the methodology is kept, the bundled eval
tooling is not.

At a high level:

- Decide what the skill should do and roughly how it should do it
- Write a draft of the skill
- Create a few test prompts and run the agent with the skill on them
- Evaluate the results qualitatively (and quantitatively if you have evals)
- Rewrite based on feedback; repeat until satisfied

Figure out where the user is in this process and jump in there. If they already have a
draft, go straight to the eval/iterate part. If they say "just vibe with me", do that.

## Communicating with the user

Users range from terminal newcomers to seasoned engineers. Pay attention to context
cues: use "evaluation"/"benchmark" freely, but explain "JSON"/"assertion" if there is no
signal they know those terms. Briefly defining a term when in doubt is always fine.

## Creating a skill

### Capture intent

The conversation may already contain the workflow to capture. Extract: tools used, step
sequence, corrections the user made, input/output formats. Confirm gaps before writing.

1. What should the skill enable the agent to do?
2. When should it trigger? (user phrases/contexts)
3. What's the expected output format?
4. Test cases? Objectively verifiable outputs (file transforms, extraction, codegen,
   fixed workflows) benefit from them; subjective outputs (style, art) usually don't.
   Suggest a default and let the user decide.

Ask about edge cases, formats, examples, success criteria, dependencies. Check available
MCPs/tools if useful for research.

### Write SKILL.md

- **name**: identifier (lowercase-hyphenated, matches the folder).
- **description**: the primary triggering mechanism. Cover what it does AND the specific
  contexts to use it. All "when to use" info goes here, not in the body. Agents tend to
  *undertrigger*, so make descriptions slightly pushy — name the concrete phrases,
  filenames, and symptoms a user would say, even if they don't name the skill.
- **compatibility** (optional): required tools/deps.
- Body: the instructions.

### Anatomy

```
skill-name/
├── SKILL.md (required: frontmatter + markdown instructions)
└── bundled resources (optional)
    ├── scripts/    - deterministic/repetitive code
    ├── references/ - docs loaded as needed
    └── assets/     - output templates, icons, fonts
```

### Progressive disclosure

Three levels: metadata (name+description, always in context) → SKILL.md body (on
trigger, keep under ~500 lines) → bundled resources (loaded only as needed; scripts can
execute without loading).

- Keep SKILL.md under ~500 lines; add hierarchy with clear pointers if you approach it.
- Reference bundled files with guidance on when to read them.
- Large reference files (>300 lines) get a table of contents.
- Multi-domain skills: SKILL.md selects, `references/<variant>.md` holds the details.

### Writing patterns

Prefer imperative instructions. Explain *why* things matter instead of heavy-handed
ALWAYS/NEVER — modern models do better with reasoning than with rigid MUSTs. Skills must
not contain malware, exploits, or anything that would surprise the user if described.

Output formats — give the exact template:

```markdown
## Report structure
ALWAYS use this exact template:
# [Title]
## Executive summary
## Key findings
## Recommendations
```

Examples — show input → output pairs.

### Test cases

After the draft, write 2–3 realistic prompts a real user would say. Share them with the
user ("do these look right, or add more?"), then run them. For skills with verifiable
outputs, draft assertions that check the user's exact symptom, and grade outputs against
them (scripted checks beat eyeballing). Subjective skills: evaluate qualitatively.

## Improving a skill

This is the heart of the loop.

1. **Generalize from feedback.** You iterate on a few examples so the skill can be used
   across many prompts. Avoid fiddly overfit changes and oppressive MUSTs; if something
   is stubborn, try a different metaphor or working pattern.
2. **Keep the prompt lean.** Remove what isn't pulling its weight. Read transcripts, not
   just outputs — if the skill makes the model waste time, cut that part.
3. **Explain the why.** Transmit the understanding behind the feedback into the
   instructions.
4. **Look for repeated work.** If every test run writes the same helper script, bundle it
   in `scripts/` once and point the skill at it.

Iteration loop: improve → rerun test cases (new iteration dir) → review → repeat until
the user is happy or feedback is empty.

## Description optimization

The description is what decides triggering. To optimize it:

1. Write ~20 trigger-eval queries — 8–10 should-trigger and 8–10 **near-miss**
   should-not-trigger (same keywords, different need). Make them realistic and concrete
   (file paths, casual phrasing, typos).
2. Review the set with the user.
3. Evaluate the current description against them (run each query a few times to get a
   reliable trigger rate), propose improved descriptions, and pick the best by held-out
   score rather than train score.

Triggering mechanics: skills appear as name + description only; the agent consults a
skill for tasks it can't trivially do itself, so simple one-step queries are poor tests.
Complex, specialized queries trigger reliably when the description matches.
