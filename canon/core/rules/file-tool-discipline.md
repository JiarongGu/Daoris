---
name: file-tool-discipline
applies_when: inspecting or editing files, or running a destructive or irreversible command
enforces: use the dedicated read/search/find/edit tools, not shell or scripted equivalents; never route a command through a side channel to skip approval
---

# Use the dedicated file tools — and never evade the approval gate

**Inspect and edit files with the dedicated tools rather than shell or scripted equivalents. Reserve the
shell for genuine shell work. Never route a command through a side channel to avoid an approval
prompt.**

## Why

The dedicated tools are purpose-built for inspection: clickable file-and-line results, integration with
the approval system, fast indexed search, and no prompt for a read that was never risky. Reaching for a
shell command to do the same job is worse on every axis, and on a stricter policy it also prompts — so
the discipline removes the friction at its source rather than working around it.

The second half matters more. Where the shell is broadly permitted, destructive commands stop prompting —
which makes this a safety rule, not an ergonomics one. And routing a command through some other channel
specifically to skip a prompt is not "reducing friction"; it is circumventing a safety control.

## How to apply

- **Reading a file → the read tool. Searching content → the search tool. Finding files → the find tool.**
- **Editing a file → the edit tool.** Not a script that rewrites it. A scripted edit passes the content
  through another language's escaping on the way in, and what lands is not what you wrote: literal
  newlines inside string literals, control bytes, a backreference that eats the text before it, a
  platform's line endings undoing a normalization. Worse, a pattern that no longer matches **changes
  nothing and reports nothing** — so the edit is silently skipped and the run still looks fine.
- **A bulk edit is where this bites, and where it feels most justified.** Many small identical changes
  are exactly when scripting is tempting and exactly when a single bad escape corrupts every one of
  them. If you script it anyway, assert the substitution count and read one result back.
- **Never delete a region by computed offsets.** "From this heading to the next" is a guess about
  structure, and when the guess is wrong it takes the rest of the file with it. Match the exact text you
  mean to remove.
- **Genuine shell work → the shell**: builds, tests, version control, package managers, running programs.
- **Destructive commands deserve a pause** precisely when they no longer prompt — recursive deletes,
  hard resets, force pushes, skipping hooks, killing processes, writing to a live datastore. Look before
  you leap, prefer the reversible alternative, and confirm anything irreversible or outward-facing.
- **If something needs approval, let it ask.** Adjust the policy deliberately if the prompt is wrong —
  never hide the command from it.
