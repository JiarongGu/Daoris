import type { Meta, StoryObj } from '@storybook/react-vite';
import { ConversationView } from './ConversationView';
import { type SessionEvent, toTurns } from './conversation';

// Every state of a conversation (D76, CONV2), built by the same fold the organism uses, so what a
// reviewer designs against is what a session shows: a finished turn folded, a running one open, a
// driven session's composed target, the kinds a tool call can be, a plan, the driver's own note, an
// update this version does not know, a door that carries only text, and 中文.

let seq = 0;
// A second and a half between events, so a finished turn's meter reads a span a session could take (CONV5).
const ev = (over: Partial<SessionEvent>): SessionEvent =>
  ({ seq: ++seq, at: new Date(Date.UTC(2026, 8, 25, 9) + seq * 1500).toISOString(), kind: 'message', ...over });

const DRIVEN: SessionEvent[] = [
  ev({ kind: 'user', origin: 'target', text: 'You are the engine repository\'s agent. Take quest #q1 — "Expose a streaming budget" — with `quest_respond`, do the work in this repository, commit it, and close the quest.\n\nThe game needs a per-frame cap on chunk hydration.' }),
  ev({ kind: 'plan', entries: [
    { content: 'Read the streamer', status: 'completed' },
    { content: 'Cap hydration per frame', status: 'in_progress' },
    { content: 'Test and commit', status: 'pending' },
  ] }),
  ev({ kind: 'thought', text: 'The cap belongs in the streamer, not the loader: the loader has no notion of a frame.\nI will read chunk.rs first.' }),
  ev({ kind: 'tool', id: 'c1', title: 'Read src/world/chunk.rs', toolKind: 'read', status: 'completed', locations: ['src/world/chunk.rs'] }),
  ev({ kind: 'tool', id: 'c2', title: 'Edit src/world/chunk.rs', toolKind: 'edit', status: 'completed', locations: ['src/world/chunk.rs'],
    content: [{ type: 'diff', path: 'src/world/chunk.rs',
      oldText: 'pub fn hydrate(region: &Region) {\n    for tile in &region.tiles {\n        load(tile);\n    }\n}',
      newText: 'pub fn hydrate(region: &Region, budget: &mut Budget) {\n    for tile in &region.tiles {\n        if budget.spent() {\n            break;\n        }\n        load(tile);\n    }\n}' }] }),
  ev({ kind: 'tool', id: 'c3', title: 'cargo test streaming', toolKind: 'execute', status: 'completed',
    content: [{ type: 'text', text: 'running 4 tests\ntest streaming::cap_holds ... ok\ntest streaming::budget_resets ... ok\n\ntest result: ok. 4 passed' }] }),
  ev({ kind: 'note', text: 'permission refused: git push — the repository\'s own configuration governs, and the driver may not widen it' }),
  ev({ kind: 'message', text: 'Capped hydration at **4 chunks per frame** in `hydrate`:\n\n```rust\nif budget.spent() {\n    break;\n}\n```\n\n| test | result |\n|---|---|\n| cap_holds | ok |\n| budget_resets | ok |\n\nCommitted as `a1b2c3d`. The push is yours.' }),
  ev({ kind: 'turn', stopReason: 'end_turn', tokens: { input: 12, output: 1_840, cacheRead: 214_300, cacheWrite: 18_950 } }),
];

const RUNNING: SessionEvent[] = [
  ev({ kind: 'user', origin: 'person', text: 'Now make the budget configurable from the level file.' }),
  ev({ kind: 'thought', text: 'Levels are parsed in level.rs.' }),
  ev({ kind: 'tool', id: 'c9', title: 'Search "parse_level"', toolKind: 'search', status: 'completed' }),
  ev({ kind: 'message', text: 'Found the parser. Adding a `streaming_budget` field' }),
  ev({ kind: 'tool', id: 'c10', title: 'Edit src/level.rs', toolKind: 'edit', status: 'in_progress', locations: ['src/level.rs'] }),
];

const FAILED: SessionEvent[] = [
  ev({ kind: 'tool', id: 'c11', title: 'cargo build', toolKind: 'execute', status: 'failed',
    output: 'error[E0061]: this function takes 2 arguments but 1 argument was supplied\n --> src/world/mod.rs:88:9' }),
  ev({ kind: 'raw', title: 'available_commands_update', raw: '{"sessionUpdate":"available_commands_update","availableCommands":[{"name":"compact"}]}' }),
  ev({ kind: 'turn', stopReason: 'cancelled' }),
];

/**
 * Two turns the person stopped (CONV4b): one cut while a tool ran — Claude Code answers that call as
 * failed, and it draws as stopped — and one stopped while the model was still thinking, with no words.
 */
const STOPPED: SessionEvent[] = [
  ev({ kind: 'user', origin: 'person', text: 'Run the whole test suite.' }),
  ev({ kind: 'message', text: 'Running every test.' }),
  ev({ kind: 'tool', id: 'c20', title: 'cargo test', toolKind: 'execute', status: 'in_progress' }),
  ev({ kind: 'tool', id: 'c20', status: 'failed',
    content: [{ type: 'text', text: "The user doesn't want to proceed with this tool use. The tool use was rejected." }] }),
  ev({ kind: 'turn', stopReason: 'cancelled' }),
  ev({ kind: 'user', origin: 'person', text: 'Write the numbers from 1 to 1000 as words.' }),
  ev({ kind: 'turn', stopReason: 'cancelled' }),
];

/**
 * STEER1 (D136): a driven session told two things while it works — one its agent reads at its next step, one at its
 * turn's end — waiting where they were said, the run in hand still going.
 */
const TOLD: SessionEvent[] = [
  ev({ kind: 'user', origin: 'target', text: 'You are the engine repository\'s agent. Take quest #q1 — "Expose a streaming budget".' }),
  ev({ kind: 'message', text: 'Running the gates before I commit.' }),
  ev({ kind: 'tool', id: 'c30', title: 'cargo test', toolKind: 'execute', status: 'in_progress' }),
  ev({ kind: 'user', origin: 'person', id: 'said-1', reaches: 'next-step', text: 'The budget lives in level.json, not config.json.', files: ['level.json'] }),
  ev({ kind: 'user', origin: 'person', id: 'said-2', reaches: 'turn-end', text: 'And cap it at 64 KiB.' }),
];

const CHINESE: SessionEvent[] = [
  ev({ kind: 'user', origin: 'person', text: '把每帧的加载上限做成可配置的。' }),
  ev({ kind: 'message', text: '已在 `level.rs` 中加入 `streaming_budget` 字段，默认值为 **4**。' }),
  ev({ kind: 'turn', stopReason: 'end_turn', tokens: { input: 3, output: 212, cacheRead: 35_480, cacheWrite: 39 } }),
];

const meta: Meta<typeof ConversationView> = {
  title: 'Work/ConversationView',
  component: ConversationView,
  decorators: [(Story) => <div className="max-w-4xl p-4"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof ConversationView>;

export const DrivenAndFinished: Story = { args: { turns: toTurns(DRIVEN).turns } };
export const Running: Story = { args: { turns: toTurns(RUNNING).turns, live: true } };
export const FailedAndCancelled: Story = { args: { turns: toTurns(FAILED).turns } };
export const Stopped: Story = { args: { turns: toTurns(STOPPED).turns } };
export const ToldWhileWorking: Story = { args: { turns: toTurns(TOLD).turns, live: true } };
export const WithEarlierTurns: Story = { args: { turns: toTurns(DRIVEN).turns, earlier: true, onLoadEarlier: () => {} } };
export const Chinese: Story = { args: { turns: toTurns(CHINESE).turns } };
export const TextOnlyDoor: Story = { args: { turns: [] } };
export const TextOnlyDoorStillRunning: Story = { args: { turns: [], live: true } };
