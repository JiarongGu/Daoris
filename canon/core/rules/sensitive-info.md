---
name: sensitive-info
applies_when: writing any tracked file or commit message, or rewriting history
enforces: no machine paths, no private repository names, no credentials; a committed leak is a history problem
---

# Sensitive info — keep machine specifics and private names out of tracked files

**Never put a developer machine's absolute path, a private repository's name, or any credential into a
tracked file or a commit message.**

Once committed it lives in the history, where deleting the line does not remove it, and a commit message
is history too. Use repository-relative paths or a neutral placeholder, name a sibling project neutrally
unless it is public under that name, and keep private context in an untracked directory. Credentials
belong in the environment or a secret store; tests use a deterministic stub. A leak already committed
needs a history rewrite, with a backup first (`leak-repair`).
