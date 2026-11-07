import type { Meta, StoryObj } from '@storybook/react-vite';
import { DomainLoading } from './DomainLoading';

// A machine domain's first load: one card's place, and the two Permissions holds.

const meta = {
  title: 'Settings/DomainLoading',
  component: DomainLoading,
} satisfies Meta<typeof DomainLoading>;

export default meta;
type Story = StoryObj<typeof meta>;

export const OneCard: Story = {};

export const TwoCards: Story = {
  render: () => (
    <div className="[&>*:first-child]:mt-0">
      <DomainLoading rows={3} />
      <DomainLoading />
    </div>
  ),
};
