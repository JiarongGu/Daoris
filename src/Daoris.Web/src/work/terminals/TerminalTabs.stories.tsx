import type { Meta, StoryObj } from '@storybook/react-vite';
import { TerminalTabs } from './TerminalTabs';

// The terminal view's tabs (CONSOLE4c), in every state they have. A molecule, so each is reached by
// passing props: no shell, no bridge (components plan §2).

const SHELLS = [
  { shell: 'pwsh', name: 'PowerShell', default: true },
  { shell: 'cmd', name: 'Command Prompt', default: false },
  { shell: 'bash', name: 'Git Bash', default: false },
];

const meta: Meta<typeof TerminalTabs> = {
  title: 'Work/TerminalTabs',
  component: TerminalTabs,
  args: {
    tabs: [{ id: 't1', name: 'PowerShell · engine', tip: 'PowerShell, started in C:\\somewhere\\engine', ended: false }],
    selected: 't1',
    shells: SHELLS,
    onSelect: () => {},
    onClose: () => {},
    onNew: () => {},
  },
  // The panel's width: the tabs give way before "+" does.
  decorators: [(Story) => <div className="w-[36rem]"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof TerminalTabs>;

export const One: Story = {};

/** Three shells, two alike told apart, one of them ended. */
export const Several: Story = {
  args: {
    tabs: [
      { id: 't1', name: 'PowerShell · engine', tip: 'PowerShell, started in C:\\somewhere\\engine', ended: false },
      { id: 't2', name: 'Command Prompt · data', tip: 'Command Prompt, started in C:\\somewhere\\data', ended: true },
      { id: 't3', name: 'PowerShell · engine (2)', tip: 'PowerShell, started in C:\\somewhere\\engine', ended: false },
    ],
    selected: 't3',
  },
};

/** A long folder, and a 中文 one: a tab cuts its name, never the row. */
export const LongNames: Story = {
  args: {
    tabs: [
      { id: 't1', name: 'Windows PowerShell · a-repository-with-a-very-long-name-indeed', tip: 'a long path', ended: false },
      { id: 't2', name: 'Git Bash · 世界流式加载', tip: 'Git Bash, started in C:\\somewhere\\世界流式加载', ended: false },
    ],
    selected: 't2',
  },
};

/** A machine with one shell: "+" opens it, with nothing to choose. */
export const OneShell: Story = {
  args: { shells: [{ shell: 'powershell', name: 'Windows PowerShell', default: true }] },
};

/** Before the machine has said which shells it has: "+" opens the default. */
export const ShellsUnknown: Story = {
  args: { shells: null },
};
