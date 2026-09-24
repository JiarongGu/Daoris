import type { Meta, StoryObj } from '@storybook/react-vite';
import { Button } from '../ui';
import { DriverChoices } from './DriverChoices';

// A repository's standing choices for this machine's driver (D46 §6) — the row an adopter's card
// carries, and since INT3c an unadopted repository with a root here (D70).

const meta = {
  title: 'Projects/DriverChoices',
  component: DriverChoices,
  args: {
    drivable: false, held: false, ownTree: false,
    onDrive: () => {}, onHold: () => {}, onTrees: () => {},
  },
} satisfies Meta<typeof DriverChoices>;
export default meta;

type Story = StoryObj<typeof meta>;

/** Not driven here: no hold, because a hold on nothing is noise. */
export const NotDriven: Story = {};

/** Driven, with its own tree per session: the hold is offered now. */
export const Driven: Story = { args: { drivable: true, ownTree: true } };

/** Driven and held — a pause on something still opted in. */
export const Held: Story = { args: { drivable: true, held: true } };

/** An adopter's card ends the row with its own action. */
export const WithManage: Story = {
  args: { action: <Button variant="ghost">manage</Button> },
};

/**
 * An unadopted repository on a machine whose door is direct (INT3c): the choice stays, and the row
 * says what the planner will say when a quest arrives.
 */
export const UnadoptedOnADirectMachine: Story = {
  args: {
    note: 'This machine drives on a direct agent, so a quest here sits, saying why, until it drives on a protocol one.',
  },
};
