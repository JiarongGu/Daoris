import type { Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { WorkflowHold } from './WorkflowHold';

const meta: Meta<typeof WorkflowHold> = {
  title: 'Workflow/WorkflowHold', component: WorkflowHold,
  args: { gate: { state: 'kind-paths', holds: true,
    says: 'Holds: this work changed `src/report.ts` outside Documentation. Keep its workflow with `daoris-driver workflow keep s1`.' } },
  decorators: [(Story) => <div className="max-w-sm bg-page p-3"><Story /></div>],
};
export default meta;
type Story = StoryObj<typeof WorkflowHold>;

export const OutsideKindPaths: Story = {};
export const BindingUnread: Story = { args: { gate: { state: 'unread', holds: true,
  says: 'Its stored run binding could not be read. Restore the binding to continue.' } } };
export const VersionMissing: Story = { args: { gate: { state: 'cannot-start', holds: true,
  says: 'The bound workflow version is no longer saved here. Restore it to continue.' } } };
export const ChineseDark: Story = { decorators: [chinese, (Story) => <InTheme theme="dark"><Story /></InTheme>] };
