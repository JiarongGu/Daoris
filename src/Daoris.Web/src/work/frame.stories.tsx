import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { ModeSwitch, OutputPanel, StatusBar } from './frame';
import { StartSession } from './StartSession';

// The frame's furniture (D55) — three regions the platform had nowhere to put, plus the control
// that starts a session. The panel's console is absent here on purpose: a stream arrives over the
// shell's bridge and Storybook has none, so what these stories are for is the CHROME around it.

const meta: Meta = { title: 'Work/Frame' };
export default meta;

export const Modes: StoryObj = {
  render: () => (
    <div className="grid w-64 gap-3">
      <ModeSwitch mode="manage" available onChange={() => {}} />
      <ModeSwitch mode="work" available onChange={() => {}} />
      {/* A browser: Work does not exist there, so the switch is absent rather than disabled. */}
      <div className="text-[0.8rem] text-ink-faint">
        <ModeSwitch mode="manage" available={false} onChange={() => {}} />
        (no shell — nothing renders above this line)
      </div>
    </div>
  ),
};

export const Statuses: StoryObj = {
  render: () => (
    <div className="grid gap-3">
      <StatusBar driver="running" sessions={2} workspace="default" remote />
      <StatusBar driver="running" sessions={0} workspace={null} remote={false} />
      {/* A shell whose driver did not answer — different from having none, and it says so. */}
      <StatusBar driver="stopped" sessions={0} workspace="aurora" remote />
      {/* A browser: no driver, and the remote question is not one it can be asked. */}
      <StatusBar driver="absent" sessions={3} workspace="工作区" remote={null} />
    </div>
  ),
};

/** The four shapes of the panel. Empty in each, because a story has no driver to stream from. */
export const Panels: StoryObj = {
  render: () => (
    <div className="grid gap-6">
      <OutputPanel sessionId="s1a2b3c4" height={140} collapsed={false} onResize={() => {}} onToggle={() => {}} />
      <OutputPanel sessionId="s1a2b3c4" height={360} collapsed={false} onResize={() => {}} onToggle={() => {}} />
      <OutputPanel sessionId="s1a2b3c4" height={200} collapsed onResize={() => {}} onToggle={() => {}} />
      <OutputPanel sessionId={null} height={140} collapsed={false} onResize={() => {}} onToggle={() => {}} />
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
        profiles={PROFILES}
        onStart={() => {}}
      />
      {/* One harness and no accounts: both choices are absent rather than offered as one option. */}
      <StartSession repositories={['engine']} harnesses={['claude-code']} profiles={[]} onStart={() => {}} />
      {/* Nothing on this machine to talk in. */}
      <StartSession repositories={[]} harnesses={[]} profiles={[]} onStart={() => {}} />
    </div>
  ),
};
