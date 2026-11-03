---
name: file-tool-discipline
applies_when: inspecting or editing files, or running a destructive or irreversible command
enforces: read, search and edit with the dedicated tools, not the shell or a script; never route a command around an approval
---

# Use the file tools — and never route around an approval

**Read, search and edit files with the dedicated tools, and keep the shell for builds, tests, version
control and running programs. Never send a command through another channel to avoid an approval
prompt.**

A scripted edit passes your text through another language's escaping, and a pattern that stops matching
changes nothing and reports nothing; a bulk edit scripted anyway asserts its count. Delete only the text
you matched, never a region found by offsets. Where a harness has no file tools, its shell's read-only
commands read and a program's file API writes. A destructive command (a recursive delete, a hard reset,
a force push, a skipped hook, a killed process) deserves a pause exactly where it no longer prompts.
If a prompt is wrong, change the policy openly; hiding a command from it circumvents a safety control.
