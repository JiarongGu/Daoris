---
name: openai-troubleshooting
description: Diagnose Codex configuration, command-review rejections and provider review availability while developing Daoris. Use for Codex tool or permission failures, not ordinary Daoris defects that merely mention Codex.
---

# Diagnose Codex controls and review availability

Keep this procedure in the repository; machine-specific evidence belongs in ignored local notes.

1. Record the exact failed action, tool result and execution environment. Separate a command rejected
   before execution from a process failure or a provider's account refusal.
2. Search and open current official OpenAI documentation for the exact setting or error before
   interpreting it. Start with [approvals](https://learn.chatgpt.com/docs/agent-approvals-security),
   [managed requirements](https://learn.chatgpt.com/docs/enterprise/managed-configuration), or
   [configuration precedence](https://learn.chatgpt.com/docs/config-file/config-basic#configuration-precedence).
   Cite what was actually checked; do not vendor a changing manual or infer an unavailable UI control.
3. Inspect only the relevant effective settings. Repository defaults do not establish the running
   session's requirements or the command reviewer's decision. A generic rejection does not identify
   its underlying rule. Do not expose credentials while inspecting configuration.
4. For scratch cleanup, follow the repository development guide's cleanup procedure. A rejected
   command remains rejected: use no alternate API, wrapper, shell or delegated deletion to evade it.
5. For an unavailable design reader, retain the provider's authoritative refusal and actual model
   evidence. Check configured availability through supported reads; do not forge a completed read,
   invent a reset or clear an account restriction. An owner exception to a repository review rule is
   recorded as an exception, scoped to the named work, separately from provider or tool permissions.
6. Record the outcome in its decision or open task. Keep exact machine paths in local evidence and
   explain any control that this session cannot change.
