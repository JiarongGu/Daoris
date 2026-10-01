import type { Meta, StoryObj } from '@storybook/react-vite';
import type { OfferShown } from './catalog';
import { OfferPage } from './OfferPage';

// One of Daoris's own plugins, not installed here (PLUGUI1b, D119 §3.2): with what it needs, and with a problem.

const OFFER: OfferShown = {
  id: 'land-github', name: 'Land on GitHub', version: '0.2.0',
  description: 'Pushes a landed branch to its repository\'s origin and opens its pull request.',
  problem: null, harnesses: [], points: ['work/land'], servers: [],
  needs: ['`gh` on the PATH, signed in to the account that pushes', 'A repository whose origin is on GitHub'],
  installed: false,
};

const meta: Meta<typeof OfferPage> = {
  title: 'Plugins/OfferPage',
  component: OfferPage,
  args: { offer: OFFER, kitPoints: [{ name: 'work/land', kind: 'act' }], onInstall: () => {} },
  decorators: [(Story) => (
    <div className="flex h-[34rem] w-[46rem] max-w-full border border-line bg-page">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof OfferPage>;

/** With what it needs: its README's bullets, verbatim, and *Install*. */
export const WithNeeds: Story = {};

/** A server it hands every session, and nothing it needs. */
export const HandsAServer: Story = {
  args: {
    offer: {
      ...OFFER, id: 'in-app-browser', name: 'In-app browser', version: '0.1.0', points: [], servers: ['browser'], needs: [],
      description: 'Daoris\'s own browser, handed to every session as a server it can drive.',
    },
  },
};

/** With a problem: the driver's sentence in *Install*'s place. */
export const WithAProblem: Story = {
  args: { offer: { ...OFFER, problem: 'needs plugin API 2, and this build speaks 1 — update Daoris to install it.' } },
};
