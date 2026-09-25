# What a message can carry, checked against both doors (CONV4c)

**Date:** 2026-09-25 · **Binaries:** Claude Code 2.1.281 on `PATH`; `claude-code-acp` 0.79.0 (the
dsh probe's install under `_fixtures/dsh/npm`) · **Cost:** two real sessions of four and six
one-word turns, under the owner's authorisation of 2026-09-24.

D76 §6 puts attachments and `@` a file in the composer. Before either is built, this is what each door
does with them. Every result below was read off the session's own output. Where a claim comes from a
bundle rather than the wire, it says so.

## The fixture

A working folder holding `notes.md` (*the password is heliotrope*). A folder **outside** it holding
`secret.txt` (*the code word is marigold*) and a 32-pixel red PNG, standing for what a person would
attach. And the grant INT4j already hands a session for its own kept files: one
`Read(//<outside>/**)` rule, on `--settings` for the native door and in `session/new`'s `_meta` for
the protocol door. That is how Daoris hands rules today (PERM1).

## The native door: `claude -p --input-format stream-json`, `acceptEdits`

| Turn | Sent | What happened |
|---|---|---|
| `@` a file in the tree | text `What is the password in @notes.md?` | answered `heliotrope` with **no tool call**. The binary expands the mention itself, and no frame on the wire shows it |
| an inline image | a text block and `{"type":"image","source":{"type":"base64","media_type":"image/png",…}}` | answered `Red` |
| a text file outside, by path | text naming the absolute path | `Read` on that path, answered `marigold`, **no permission denial** |
| an image outside, by path | text naming the PNG's absolute path | `Read` returned the image, answered `Red`, no denial |

## The protocol door: `claude-code-acp`, `acceptEdits`

`initialize` answered `promptCapabilities: { image: true, embeddedContext: true }`; `resource_link` is
the protocol's baseline and needs no capability.

| Turn | Sent | What happened |
|---|---|---|
| `@` a file in the tree | text `…@notes.md…` | `heliotrope`, no tool call |
| a `resource_link` in the tree | text and `{"type":"resource_link","uri":"file:///…/notes.md","name":"notes.md"}` | `heliotrope`, no tool call |
| an inline image | text and `{"type":"image","data":…,"mimeType":"image/png"}` | `Red` |
| a text file outside, by path | text naming the absolute path | `Read File`, completed, `marigold`, no permission request |
| a `resource_link` outside | text and a `resource_link` to `secret.txt` | `marigold`, no tool call, no permission request |
| an embedded resource | text and `{"type":"resource","resource":{"uri":…,"text":…}}` | `juniper`, no tool call |

**Why a link needs no tool call** (bundle evidence, the adapter's `acp-agent.js`). A `resource_link`
becomes the text `[@name](file:///…)`, so Claude Code meets a mention and expands it. An embedded
resource becomes the same link plus a `<context ref="…">` block holding its text. An embedded blob is
dropped.

## What Daoris takes from it

- **`@` a file is text.** The person's `@relative/path` travels as they typed it, and both doors
  expand it. Sending a `resource_link` beside it would hand Claude the same file twice, since the
  adapter turns the link into a mention too.
- **An attachment is a file kept for the session, granted read, and referenced once per door.**
  - **Kept** under the session's own folder in the home. It is transcript-class, like the transcript
    and the record (D47 §4), and it is never written into the tree.
  - **Granted** by the INT4j rule, at spawn, for that folder alone.
  - **Referenced** as each door reads best. On the native door, a line naming its absolute path,
    which the agent reads with its own tool (measured, text and image). On the protocol door, a
    `resource_link` per file, the protocol's baseline block (measured).
- **Not taken:** inline image blocks and embedded resources. Both work on both doors, but they put a
  file's bytes on the wire inside the message, and a door that cannot take a kind (an embedded blob
  is dropped) would need a second way. A kept file is read by the agent when it needs it, and every
  kind of file travels one way, image or not.
