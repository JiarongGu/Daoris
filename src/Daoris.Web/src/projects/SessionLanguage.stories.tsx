import type { Meta, StoryObj } from '@storybook/react-vite';
import { SessionLanguage } from './SessionLanguage';

// A repository's session language on this machine (LANG1c, D142 point 7): what its sessions write to the person in, on the
// repository's page beside its standing answer, named from the driver's own table.

const TABLE = [
  { code: 'en', name: 'English' },
  { code: 'zh', name: 'Simplified Chinese (简体中文)' },
];

const meta = {
  title: 'Projects/SessionLanguage',
  component: SessionLanguage,
  args: { repository: 'engine', language: null, table: TABLE, onSet: () => {} },
  decorators: [(Story) => <div className="w-[40rem] max-w-full bg-page p-4"><Story /></div>],
} satisfies Meta<typeof SessionLanguage>;
export default meta;

type Story = StoryObj<typeof meta>;

/** None set here or for its workspace: its sessions are asked for no language. */
export const NoneSet: Story = {};

/** Set for this repository. */
export const ItsOwn: Story = {
  args: {
    language: { repository: 'engine', workspace: 'aurora', language: 'zh', name: 'Simplified Chinese (简体中文)', source: 'repository' },
    inherited: 'English',
  },
};

/** Taken from its workspace, which its unset choice names. */
export const FromItsWorkspace: Story = {
  args: { language: { repository: 'engine', workspace: 'aurora', language: 'en', name: 'English', source: 'workspace' } },
};
