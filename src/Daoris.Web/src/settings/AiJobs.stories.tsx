import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { AiJobs } from './AiJobs';

// Daoris's own AI (AGT6): each job it may use a model for, the tier that answers it now, and how to
// change it — in the shapes the service's status answers with. Intake and Ask Daoris are rows of *Actors* since MODELROLE1d
// (`ai/Actors.stories.tsx`).

const LEXICAL = {
  tier: 'lexical only', semantic: false,
  note: 'Set DAORIS_EMBED_MODEL to enable semantic recall — it is what finds two repositories that reached the same conclusion in different words.',
};

const meta: Meta<typeof AiJobs> = {
  title: 'Settings/AiJobs',
  component: AiJobs,
  args: { search: LEXICAL },
  // The main area at its wide width, as the frame gives it (the UI/UX design R6, UIFIT1).
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof AiJobs>;

/** A machine as it arrives: no embedding model. */
export const NothingNamed: Story = {};

/** The same at the half width, 630 px. */
export const NothingNamedHalf: Story = { decorators: [storyWidths.half] };

/** The same at the 400 px floor: each value under its words. */
export const NothingNamedNarrow: Story = { decorators: [storyWidths.narrow] };

/** The same in 中文, wide. */
export const NothingNamedChinese: Story = { decorators: [chinese] };

/** The same in 中文 at the floor. */
export const NothingNamedChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** A model for search. */
export const SearchOnAModel: Story = {
  args: { search: { tier: 'lexical + semantic', semantic: true, note: null } },
};

/** Before the service has answered: the tier is not claimed. */
export const TierNotYetKnown: Story = {
  args: { search: undefined },
};

/**
 * HELPMODE1c1 (the modes design §5's sketch): Ask Daoris on Claude Code, restricted, with its configured access under the access
 * row, housekeeping's inspection chosen and the browser waiting for approval.
 */
export const RestrictedWithConfiguredAccess: Story = {
  args: {
    access: { mode: 'restricted', confirming: false, adds: [], onPick: () => {}, onEnable: () => {}, onCancel: () => {} },
    grants: {
      agent: { name: 'Claude Code', rules: true },
      grants: [
        { id: 'housekeeping', chosen: ['inspect'] },
        { id: 'browser', chosen: [], plugin: 'in-app-browser' },
        { id: 'checkouts', chosen: ['engine'] },
        { id: 'settings', chosen: ['notify'] },
      ],
      built: {
        housekeeping: ['inspect', 'clean-up', 'discard-branch'], settings: ['notify', 'language', 'timeout', 'cap', 'cooloff'],
        checkouts: ['engine', 'reports-db'],
      },
      onChoose: () => {},
      onManage: () => {},
    },
  },
};
