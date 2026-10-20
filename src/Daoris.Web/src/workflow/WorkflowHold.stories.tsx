import type { Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { sessionWidths } from '../design/storyWidths';
import { WorkflowHold } from './WorkflowHold';

// A run's workflow holding its landing, as a session's head and foot draw it beside the landing (`Lands`): in a session's page
// at the frame's widths (the UI/UX design R6), in 中文 and dark.

const meta: Meta<typeof WorkflowHold> = {
  title: 'Workflow/WorkflowHold', component: WorkflowHold,
  args: { gate: { state: 'kind-paths', holds: true,
    says: 'Holds: this work changed `src/report.ts` outside Documentation. Keep its workflow with `daoris-driver workflow keep s1`.' } },
  decorators: [sessionWidths.half],
};
export default meta;
type Story = StoryObj<typeof WorkflowHold>;

export const OutsideKindPaths: Story = {};
/** The same at a 1546 px window. */
export const OutsideKindPathsWide: Story = { decorators: [sessionWidths.wide] };
/** The same at the main area's 400 px floor. */
export const OutsideKindPathsNarrow: Story = { decorators: [sessionWidths.narrow] };
export const BindingUnread: Story = { args: { gate: { state: 'unread', holds: true,
  says: 'Its stored run binding could not be read. Restore the binding to continue.' } } };
export const VersionMissing: Story = { args: { gate: { state: 'cannot-start', holds: true,
  says: 'The bound workflow version is no longer saved here. Restore it to continue.' } } };
export const ChineseDark: Story = { decorators: [chinese, (Story) => <InTheme theme="dark"><Story /></InTheme>] };
