---
name: no-global-memory
applies_when: about to save a project fact to the assistant's global or cross-project memory
enforces: project facts live in the repository, versioned and reviewable; global memory is the person's preferences only
---

# No global memory for project facts — they belong in the repository

**Never put project information into the assistant's global or cross-project memory.** Global memory
is for facts about the person (their role, how they like to be answered) that hold across every project
they touch. Everything about this project goes in the repository.

## Why

A fact in global memory is invisible to everyone else who clones the repository, unversioned,
unreviewable and silently loaded, so review cannot correct it and it quietly diverges from the code it
describes. The same fact in a tracked file is none of those things, and it still works when the tool
changes.

## How to apply

- **The test is whether a fresh clone would be defective without it.** "They prefer terse answers" is a
  taste, and travels with the person. A protocol for terse answers, with carve-outs saying never to
  compress a warning or an identifier, is doctrine, and a repository document.
- **Learned something durable about this project?** Rewrite the area document's sentence, or record the
  decision; a repeatable procedure is a skill in the repository.
- **About to save a project- or feedback-shaped memory? Stop:** that is a repository document. The same
  holds for a per-project memory the tooling keeps outside the repository.
- A product's own memory feature is a different thing entirely; this rule is about the assistant's.
