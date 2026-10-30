---
name: connector-tools
applies_when: adding a tool to Daoris's connector (the service's MCP server a session is handed, the way `go_ahead_ask`, `handover_propose` and `question_ask` were added), or changing which sessions may call one
enforces: a new connector tool goes into every one of its places in the same change, the class and its line in `KnowledgeServer.ToolTypes`, the registration test's two lists, the brainstorm pass's denial and its goldens, the connector rule's allow-list with its test and the agents' text fixture, a reviewer's and helper's denial with its test, its description's test and the service README's row; several of those tests sit in the driver's suite
---

# Connector tools — every place a new tool goes

**A connector tool is offered to a session only where every list that names the connector's tools names it, and denied where every
list that denies them does.** Nothing finds a tool by scanning: the host registers one list, and the driver hands each kind of
session its own allow- and deny-lists by name. PARKVIEW1's `question_ask` walked every place below; MIXAGENT1c's
`handover_propose` is the other exemplar.

| Place | File | What goes there | Held by |
|---|---|---|---|
| The tool | `Daoris.Service.Mcp/<Name>Tools.cs` | `[McpServerToolType]` class, `[McpServerTool(Name = "…")]` method, descriptions written for the moment of choosing; a refusal is the sentence the agent reads, never thrown | the tool's own `Mcp…ToolTests` |
| Its box | `Daoris.Service.Core/…` | what it writes, where a file is the contract with the driver (`HandOverAskBox`, `QuestionBox`): hand-rolled atomic JSON, one writer | the tool's tests, and the driver's reader's tests on the same shape (`twins.md`) |
| The host's list | `Daoris.Service.Mcp/KnowledgeServer.cs`, `ToolTypes` | one line | `McpToolRegistrationTests`: the advertised names and the class names, both lists |
| A brainstorm pass's denial | `Daoris.Desktop.Driver/WorkOrders/Domain/BrainstormPermissions.cs`, `ConnectorTools` | the name, in order; the server's call layer already refuses a pass every tool but its turn's result tool, by the turn on its connector (D219's note; FOUNDATION1r) | `McpToolRegistrationTests.The_driver_denies_a_brainstorm_pass_every_tool_this_host_advertises` (the service's suite reads the driver's file); the driver's `golden/brainstorm-rules/give.json` and `synthesize.json`; `McpRoleFilterTests` for the server's half |
| The connector rule | `Daoris.Desktop.Driver/Agents/Permissions.cs`, the `connector` default, with the comment saying why it is safe to allow | the name, if a working session may call it unasked | `PermissionRulesTests`' `Connector` list; `fixtures/agents-text.json`'s four `connector` lines (`AgentsTextTests`) |
| Every work's row, on the server | `Daoris.Service.Mcp/ToolClasses.cs` (its class) and `CallTable.cs` (the call layer's table, FOUNDATION1r): served to every work but a partner, a reviewer and a brainstorm's voice unless its box judges by the work alone; `PartnerServed` or `ReviewerServed` where they may call it | the class; a cell in each column of `fixtures/call-table.json` | `ToolClassesTests` and `McpRoleFilterTests`' census over the registered names and every column |
| A reviewer's and a helper's denial | `Daoris.Desktop.Driver/Review/Domain/OpinionPermissions.cs`, `Acting` | the name, where neither may call it; a read both may call goes on `Offered` instead, as `quest_list` and HANDBACK1's `handback_read` do | `OpinionPacketTests`' denied list, and its allowed asserts for an offered read |
| A partner's denial, in its harness | `Daoris.Desktop.Driver/Participants/Domain/PartnerPermissions.cs`: `Acting` is every brainstorm-listed tool not `Offered`, so a tool refused on the server is denied here by itself | nothing, unless a partner may call it (then `Offered`, matching `PartnerServed`) | `PartnerConfigurationTests`, and `McpRoleFilterTests`' match of `Offered` to `PartnerAllowed` |
| Its words | `Daoris.Service.Tests/Mcp/McpToolDescriptionTests.cs` | a test of what its description must say (no decision number, the turn's end, where a sibling tool answers instead) | itself |
| The README | `src/Daoris.Service/README.md`, the connector's tools table | a row in the table's own prose | read, not tested |

Where the instruction tells a session to use the tool (`TargetPrompt`), its goldens (`golden/target-prompt/*.md` and `*.account.json`)
are written again with `DAORIS_GOLDEN_WRITE=1` and read before they are committed, and `AskAndWaitPromptTests` holds the sentence.
