# What the installed Claude Code does with a background command's end, and pipes across elevations (EVENTHOOK1a)

**Carried by:** EVENTHOOK1a (the event hook design §7, §9; the event channel design §4.4; D244 point 8). A record, not a contract.

**Date:** 2026-11-06 · **Harness:** Claude Code 2.1.319, its native Windows build · **Model:** the cheapest, `claude-haiku-5-5`
· **Machine:** Windows 11 with UAC off, so every process of the user runs elevated · **Cost:** below, under *Spend*, on the
machine's signed-in account, which the owner allowed once.

## How it was measured

- **The session** (M1, M5, M8): `src/Daoris.Cli/test/fixtures/event-hook/ProbeSession.cs`, a .NET file-based program run by hand
  with `dotnet run --file … --artifacts-path _fixtures/event-probe-build`. It starts a throwaway interactive `claude` under the driver's
  pseudo-console (`Driver/Platform/PseudoConsole.cs`), never the owner's session or the steward's, in a scratch git room under
  `_fixtures/event-probe/<when>/room`, which it removes at its end. The enclosing session's `CLAUDE*`, `DAORIS*` and `AI_AGENT`
  variables are not handed on; user settings and MCP servers are not loaded (`--setting-sources project,local`,
  `--strict-mcp-config`); the permission mode is `default`. The room holds a stand-in install (`app/daoris-driver/daoris-driver`, a
  script standing for `events relay --where`) and a stand-in home whose `bin/events-relay/<build>/daoris-driver` stands for the relay's
  copy, and its `.claude/settings.json` holds §6.1's allow rule by both prefix forms and hooks that log each `SessionStart`,
  `SessionEnd`, `Stop`, `Notification`, `PreCompact` and `PreToolUse`. The probe types each instruction, answers the folder's trust
  question, and reads the hooks' log: a `Stop` with nothing typed is a turn the session started on its own. Nothing signs in or out,
  and no credential is read.
- **The pipes** (M9, M10): `src/Daoris.Cli/test/fixtures/event-hook/PipeAcross.cs`, .NET, run by hand from an elevated shell. It
  makes pipes with `InstancePipe.Security` and with Node's `net.createServer`, opens them with Node's `net.connect`, and has a relay
  read `GetNamedPipeClientProcessId`, open the holder's named agents' job for query and ask `IsProcessInJob`. With UAC off there is no
  process of the user that is not elevated, so the other side runs on a restricted token of the probe's own shaped as UAC's filtered
  token: the Administrators group deny-only, no privilege but change-notify, medium integrity, the user its default owner, its
  default DACL the user's and SYSTEM's.

## Outcomes

Each outcome is a pass, a **limit** accepted and named with what follows from it, or a **failure** that blocks progress by §7's rule
with the rows it holds.

| # | Measured | Outcome | Class |
|---|---|---|---|
| M1 | `claude --version`; `/status`'s `Peer address` | 2.1.319; the address is `uds:` and a named pipe in the local namespace, `\\.\pipe\LOCAL\cc-msg-<id>` | pass |
| M5 | a background command started from Bash by the path a first command printed, under `default` with §6.1's allow rule | started, and neither the first command nor the copy's path prompted | pass |
| M5 | its end, two lines on stdout and one on stderr between them, in an idle session | a turn started 4 s after the end | pass |
| M5 | the end's output, as the turn it starts receives it | the notification carries the task's id, its status (`completed`), its exit code and the path of its output file, and no line of stdout or stderr; the session saw the lines only by reading that file | **limit**: see below |
| M5 | an end with nothing printed | a turn started 2 s after the end | pass |
| M5 | an end by a kill (the command's process tree killed from outside) | a turn started 1 s after the kill | pass |
| M5 | the same after a change of build (the printed path under another build's folder, same prefix) | started unprompted; a turn started 4 s after its end | pass |
| M5 | a wait of 3720 s with nothing printed, from the session's Bash | lived the whole wait; a turn started 2 s after its end | pass |
| M5 | a wait of the same length started from a `SessionStart` hook (the inbox's round) | lived the whole wait | pass |
| M5 | a wait across `/compact`, the compaction ending before the command's end | a turn started 3 s after its end | pass |
| M5 | a wait across `/clear` | a turn started 1 s after its end, in the cleared session | pass |
| M5 | a wait past the session's end | not read: the session did not exit within 60 s of `/exit` while its background command ran, and the command ended in the living session | not measured; 1e's M5L |
| M5 | the session's process id, as the command sees it | the variable `CLAUDE_PID` is in the command's environment, its value not compared with the session's process; a walk up the command's parent processes did not reach the session's process | **limit**: `--session-pid` rests on `CLAUDE_PID` until a run compares it |
| M8 | the messaging variables in the session's Bash, in its child, and in a stand-in Daoris child with `Removed` applied | present, present, absent | pass (EVENTHOOK1g's confirmation) |
| M9 | `InstancePipe.Security`'s pipe, not elevated, opened by Node elevated, and the reverse | opened, both | pass on this machine |
| M9 | a Node-made pipe across elevations, to confirm the failure | opened, both: no failure to confirm | **limit**: see below |
| M10 | a relay not elevated: the client's id, the elevated holder's job, `IsProcessInJob`; and the reverse | read, opened, true; the same in reverse | **limit**: see below |

**M9 and M10 could not run across real elevations here.** With UAC off, a new object's default DACL names the logon session beside
SYSTEM and the Administrators, and the restricted token keeps the logon session, so every open succeeded, the Node-made pipe and the
elevated holder's job included. That is this machine's grant, not evidence about a machine with UAC on, where the expected answers
(a Node-made pipe refused across elevations; a relay that is not elevated unable to open an elevated holder's job, so `NamedHolds`
answers null and the client is taken untold) stay unmeasured. On this machine, where the owner's install and the steward run, no two
processes of the user differ in elevation, so the relay's step 2 and step 3 meet the case that passed. The probe is kept to run
on a machine with UAC on before 1c's step 2 and step 3 are taken as confirmed there; 1b rests on neither.

**The end wakes the session with where its output is, not with the output.** §2.4 of the design took the end to carry the command's
lines; on 2.1.319 the turn it starts is told the task ended and where its output file is, and the model reads the batch from that file
(one read of a file Claude Code wrote, under the session's own permissions). Whether stderr is kept in that file beside stdout was not
read. Nothing the relay writes changes: every line is already a fixed sentence (§6.1 step 1). What follows: the steward's procedure
(§8.1) reads the output file named by the end before it acts, and 1c's proof of the installed wake (1h) reads the batch that way.

## The decision rule, read

- **M5 answers yes**: a background command started by a printed path lives for an hour and more, across a compaction and `/clear`, and
  its end, printed or silent or killed, starts a turn in an idle session within seconds, unprompted under `default` with §6.1's allow
  rule before and after a change of build. The output arrives as a file named by the end, a named limit above, not a failure §7's rule
  lists. So **1b and 1c go on.**
- **The re-arm does not prompt**: no permission prompt in any case, the copy's path under a second build's folder included.
- **M9 does not refuse** on this machine, and **M10 reads the reverse of neither**: both opened everything, for the reason above. So
  §6.1's step 2 and step 3 stand for this machine; on a machine with UAC on they stay unconfirmed until `PipeAcross` runs there.
- **The limits named**: the output read from a file; stderr in it unread; `CLAUDE_PID` not compared; past the session's end not read;
  M9 and M10 across real elevations not run.

## Spend

Two probe sessions spent model turns; the startups that stopped at the trust question spent none.

| Run | Model | Messages | Input | Output | Cache written | Cache read |
|---|---|---|---|---|---|---|
| the first, stopped at the hour's start by the probe's own read race | `claude-haiku-5-5` | 3 | 6 | 549 | 18,634 | 132,977 |
| the measured one, about 35 minutes of turns around a 62-minute idle wait | `claude-haiku-5-5` | 32 | 74 | 4,930 | 71,091 | 1,685,325 |

The one compaction's own call is not in the transcripts and is not counted above. In all: 35 assistant messages, about 5,500 output
tokens and 1.8 million cache-read tokens of the cheapest model, on the owner's signed-in account.

## For later rounds

- The session hands its Bash `CLAUDE_PID`, `CLAUDE_CODE_SESSION_ID`, `CLAUDECODE`, `CLAUDE_CODE_ENTRYPOINT`,
  `CLAUDE_CODE_EXECPATH`, `CLAUDE_CODE_CHILD_SESSION`, `CLAUDE_CODE_SESSION_ATTENDED`, `CLAUDE_EFFORT` and `AI_AGENT` beside the two
  messaging variables (names only were read). `--session-pid` may be filled from `CLAUDE_PID` once a run compares its value.
- The folder's trust question offers *No, exit* first where the folder's settings pre-approve permissions; a key sent before the
  question takes input is lost, so the probe answers it again until the session starts.
