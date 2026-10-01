import type { Meta, StoryObj } from '@storybook/react-vite';
import { DomainList } from './DomainList';

// Settings' list (FRAME1g, D118 §5) at its 176 px start: a shell's ten domains, a browser's four with what it
// is not offered, the 中文 names, and the chosen domain last in a long list.

const SHELL = [
  { id: 'start', label: 'Setup' },
  { id: 'appearance', label: 'Appearance' },
  { id: 'ai', label: 'AI features' },
  { id: 'workspace', label: 'Workspace' },
  { id: 'driver', label: 'Driver' },
  { id: 'agents', label: 'Agents' },
  { id: 'permissions', label: 'Permissions' },
  { id: 'plugins', label: 'Plugins' },
  { id: 'browser', label: 'Browser' },
  { id: 'logs', label: 'Machine log' },
];

const meta = {
  title: 'Settings/DomainList',
  component: DomainList,
  args: { label: 'Settings domains', domains: SHELL, chosen: 'appearance', onChoose: () => {} },
  decorators: [(Story) => <div style={{ width: 176 }}><Story /></div>],
} satisfies Meta<typeof DomainList>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Shell: Story = {};

export const Browser: Story = {
  args: {
    domains: SHELL.slice(0, 4),
    note: "A machine's own settings are on the desktop, where the machine is.",
  },
};

export const Chinese: Story = {
  args: {
    label: '设置分类',
    domains: [
      { id: 'start', label: '配置' }, { id: 'appearance', label: '外观' }, { id: 'ai', label: 'AI 功能' },
      { id: 'workspace', label: '工作区' }, { id: 'driver', label: '驱动' }, { id: 'agents', label: '智能体' },
      { id: 'permissions', label: '权限' }, { id: 'plugins', label: '插件' }, { id: 'browser', label: '浏览器' },
      { id: 'logs', label: '本机日志' },
    ],
    chosen: 'agents',
  },
};

export const LastChosen: Story = { args: { chosen: 'logs' } };
