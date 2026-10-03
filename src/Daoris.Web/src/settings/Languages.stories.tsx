import type { Meta, StoryObj } from '@storybook/react-vite';
import { LanguageList } from './Languages';

// Each workspace's and repository's session language (LANG1c), in the shape the driver's LINES and STATE answers take: set for
// a repository, taken from its workspace, none at all, and a machine with nothing to set one for.

const TABLE = [
  { code: 'en', name: 'English' },
  { code: 'zh', name: 'Simplified Chinese (简体中文)' },
];

const meta = {
  title: 'Settings/Session language',
  component: LanguageList,
  args: { onSet: () => {}, workspaceLanguages: [], languages: [], table: TABLE },
} satisfies Meta<typeof LanguageList>;

export default meta;
type Story = StoryObj<typeof meta>;

export const EverySource: Story = {
  args: {
    workspaceLanguages: [{ workspace: 'aurora', language: 'zh' }],
    languages: [
      { repository: 'engine', workspace: 'aurora', language: 'en', name: 'English', source: 'repository' },
      { repository: 'game', workspace: 'aurora', language: 'zh', name: 'Simplified Chinese (简体中文)', source: 'workspace' },
      { repository: 'tools', workspace: 'default' },
    ],
  },
};

export const NoneSet: Story = {
  args: {
    languages: [
      { repository: 'engine', workspace: 'aurora' },
      { repository: 'game', workspace: 'aurora' },
    ],
  },
};

export const NothingHere: Story = {};
