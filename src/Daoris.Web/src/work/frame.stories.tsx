import type { Meta, StoryObj } from '@storybook/react-vite';
import { SessionConsole } from '../SessionConsole';
import * as Tooltip from '@radix-ui/react-tooltip';
import { OutputPanel, StatusBar } from './frame';
import { StartSession } from './StartSession';

// The window's furniture (D55) — regions the platform had nowhere to put, plus the control that
// starts a session. The panel's console is absent here on purpose: a stream arrives over the shell's
// bridge and Storybook has none, so what these stories are for is the CHROME around it.

const meta: Meta = { title: 'Work/Frame' };
export default meta;

/**
 * 🔴 Wrapped in the tooltip provider the application mounts once (`main.tsx`). Every status item
 * carries a tip since the bar became a bar (2026-09-22), and a `Tooltip` outside a provider throws —
 * so a story that renders one without it is a story that cannot render at all. Caught by the suite
 * that asserts every story renders, which is exactly what that suite is for.
 */
export const Statuses: StoryObj = {
  render: () => (
    <Tooltip.Provider>
      <div className="grid gap-3">
        <StatusBar driver="running" sessions={2} workspace="default" remote onDriver={() => {}} />
        <StatusBar driver="running" sessions={0} workspace={null} remote={false} />
        {/* A shell whose driver did not answer — different from having none, and it says so. */}
        <StatusBar driver="stopped" sessions={0} workspace="aurora" remote />
        {/* A browser: no driver, and the remote question is not one it can be asked. */}
        <StatusBar driver="absent" sessions={3} workspace="工作区" remote={null} />
      </div>
    </Tooltip.Provider>
  ),
};

/** The four shapes of the panel. Empty in each, because a story has no driver to stream from. */
export const Panels: StoryObj = {
  render: () => (
    <div className="grid gap-6">
      <OutputPanel console={<SessionConsole id="s1a2b3c4" fill quiet="nothing held here" />} height={140} collapsed={false} onResize={() => {}} onToggle={() => {}} />
      <OutputPanel console={<SessionConsole id="s1a2b3c4" fill quiet="nothing held here" />} height={360} collapsed={false} onResize={() => {}} onToggle={() => {}} />
      <OutputPanel console={<SessionConsole id="s1a2b3c4" fill quiet="nothing held here" />} height={200} collapsed onResize={() => {}} onToggle={() => {}} />
      <OutputPanel console={null} height={140} collapsed={false} onResize={() => {}} onToggle={() => {}} />
    </div>
  ),
};

/**
 * A tab for each thing that is running (CONSOLE2c): the session, then a subagent that finished, a
 * background task still going, one that failed, and one with a name too long for its tab.
 */
export const PanelTabs: StoryObj = {
  render: () => (
    <div className="grid w-[40rem] gap-6">
      <OutputPanel
        console={<SessionConsole id="s1a2b3c4/task/bs00" fill quiet="nothing held here" />}
        height={140}
        collapsed={false}
        onResize={() => {}}
        onToggle={() => {}}
        selected="s1a2b3c4/task/bs00"
        onSelect={() => {}}
        tabs={[
          { key: 's1a2b3c4', kind: 'session', label: 'session', tone: 'idle', status: "the session's own console" },
          { key: 's1a2b3c4/subagent/a2fe', kind: 'subagent', label: 'Read README first line', tone: 'ended', status: 'subagent · completed' },
          { key: 's1a2b3c4/task/bs00', kind: 'task', label: 'dev server', tone: 'live', status: 'background · running' },
          { key: 's1a2b3c4/task/bt01', kind: 'task', label: 'npm test', tone: 'failed', status: 'background · failed' },
          { key: 's1a2b3c4/subagent/a9', kind: 'subagent', label: '检查世界流式加载引擎的每一个分块预算，并报告超出预算的地方', tone: 'live', status: 'subagent · running' },
        ]}
      />
    </div>
  ),
};

/**
 * The panel as a region (DOCK1b): holding the console and Ask Daoris, moved in from the right side
 * bar, with Ask Daoris shown; holding the timeline alone, named for it; and emptied, saying how to
 * fill it.
 */
export const PanelViews: StoryObj = {
  render: () => (
    <div className="grid w-[40rem] gap-6">
      <OutputPanel
        console={<SessionConsole id="s1a2b3c4" fill quiet="nothing held here" />}
        height={140} collapsed={false} onResize={() => {}} onToggle={() => {}}
        views={['console', 'ask']} view="ask" onView={() => {}} onMove={() => {}} onReset={() => {}}
      >
        <p className="m-0 p-3 text-small text-ink-soft">Ask Daoris, the conversation about this machine.</p>
      </OutputPanel>
      <OutputPanel
        console={null} height={100} collapsed={false} onResize={() => {}} onToggle={() => {}}
        views={['timeline']} onMove={() => {}} onReset={() => {}}
      >
        <p className="m-0 p-3 text-small text-ink-soft">session opened · 12m ago</p>
      </OutputPanel>
      <OutputPanel console={null} height={80} collapsed={false} onResize={() => {}} onToggle={() => {}} views={[]} onReset={() => {}} />
    </div>
  ),
};

const PROFILES = [
  { name: 'personal', login: 'in' as const },
  { name: 'work', login: 'out' as const },
];

export const Starting: StoryObj = {
  decorators: [(Story) => <Tooltip.Provider><Story /></Tooltip.Provider>],
  render: () => (
    <div className="grid w-[18rem] gap-6 border border-line">
      <StartSession
        repositories={['engine', 'game', '世界流式加载引擎']}
        harnesses={['claude-code', 'codex']}
        defaultHarness="claude-code"
        accounts={{ 'claude-code': PROFILES }}
        onStart={() => {}}
      />
      {/* One harness and no accounts: both choices are absent rather than offered as one option. */}
      <StartSession repositories={['engine']} harnesses={['claude-code']} accounts={{}} onStart={() => {}} />
      {/* Nothing on this machine to talk in. */}
      <StartSession repositories={[]} harnesses={[]} accounts={{}} onStart={() => {}} />
    </div>
  ),
};
