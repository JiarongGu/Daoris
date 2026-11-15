---
name: file-tools
applies_when: about to edit files through the shell, a script or a bulk replacement, or to run a destructive command
enforces: read, search and edit with the dedicated tools; a scripted bulk edit asserts its count; delete only what you matched; never route a command around an approval
---
<!-- daoris: core/core/knowledge/file-tools.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# Use the file tools, and never route around an approval

The always-loaded rule says: use the file tools to read, search and edit, never a script or a console redirection,
and never route a command around an approval. This is why, and what to do where the tools are missing.

## Why

- **A scripted edit fails silently.** It passes your text through another language's escaping, and a pattern that
  stops matching changes nothing and reports nothing. The file tool refuses an edit it cannot place; the script
  exits zero.
- **A console redirection rewrites the text.** A console whose encoding is not UTF-8 turns every character outside
  its code page into something else, and the file looks right until a reader meets the damage.
- **A region found by offsets is a guess.** The offsets were true of the file you read, not of the file as it is
  when the edit lands.
- **An approval prompt is a safety control.** Sending the same command through another channel because it does not
  prompt there circumvents the control rather than answering it.

## How to apply

- Keep the shell for builds, tests, version control and running programs.
- A bulk edit that must be scripted anyway asserts how many places it changed, and fails when the count differs.
- Delete only the text you matched, never a region found by offsets.
- Where a harness has no file tools, its shell's read-only commands read and a program's file API writes.
- A destructive command (a recursive delete, a hard reset, a force push, a skipped hook, a killed process)
  deserves a pause exactly where it no longer prompts.
- If a prompt is wrong, change the policy openly.
