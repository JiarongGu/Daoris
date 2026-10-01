import { type ReactNode, useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { NO_CARRY } from '../compose/carry';
import { AskRow } from './AskRow';
import { AskComposer, type AskDraft } from './AskComposer';
import {
  BY_INTAKE, CLOSED, DONE, INTAKE_ASKED, LONG_CJK, NAMED, PROPOSED, PUBLISHED, REFUSED, UNKNOWN_TIER, UNMATCHED,
} from './fixtures';

// An ask (D65 §1a), made at a workspace and read back as a record (INT4c): its row in Quests' list and the composer
// that makes one, a drawer since it is a form (FRAME1d, D118 §3d). Its page is `AskPage.stories.tsx`. Every state a
// real machine reaches, and a sentence long enough and CJK enough to try the layout.

const meta: Meta = { title: 'Asks' };
export default meta;

const noop = () => {};
const RECEIVERS = ['engine', 'game', 'lantern'];

const Provided = ({ children }: { children: ReactNode }) => <Tooltip.Provider>{children}</Tooltip.Provider>;

/** Rows at the list's width (D118 §3a: 280 px to start). */
const Rows = ({ children }: { children: ReactNode }) => (
  <ul className="m-0 w-[280px] list-none border border-line bg-page p-0">{children}</ul>
);

export const RowsInEveryState: StoryObj = {
  render: () => (
    <Rows>
      {[PROPOSED, UNMATCHED, PUBLISHED, NAMED, BY_INTAKE, REFUSED, DONE, CLOSED, UNKNOWN_TIER, LONG_CJK].map((ask, index) => (
        <AskRow key={ask.id} ask={ask} chosen={index === 0} onOpen={noop} />
      ))}
    </Rows>
  ),
};

/** What an ask waits for, as the band says it (POLISH4): an intake reading it, then one asking you. */
export const RowsWithTheirIntake: StoryObj = {
  render: () => (
    <Rows>
      <AskRow ask={INTAKE_ASKED} intake="working" onOpen={noop} />
      <AskRow ask={INTAKE_ASKED} intake="awaiting-person" onOpen={noop} />
    </Rows>
  ),
};

function Composing({ fixed, circles, start }: { fixed: string | null; circles: string[]; start?: Partial<AskDraft> }) {
  const [draft, setDraft] = useState<AskDraft>({ circle: '', sentence: '', to: '', ...NO_CARRY, ...start });
  return (
    <Provided>
      <AskComposer
        draft={draft} onChange={setDraft} fixed={fixed} circles={circles} receivers={RECEIVERS}
        onSubmit={noop} onCancel={noop}
      />
    </Provided>
  );
}

/** The page is scoped to one circle — or the machine holds only one — so the ask is made there. */
export const ComposeInTheScopedCircle: StoryObj = { render: () => <Composing fixed="aurora" circles={['aurora']} /> };

/** Several circles and none chosen: the person says which, because an ask's circle is whom it reaches. */
export const ComposeChoosingTheCircle: StoryObj = {
  render: () => <Composing fixed={null} circles={['aurora', 'tools', '工作区']} />,
};

/** A machine with no repository holds no circle: the composer says so rather than offering a form. */
export const ComposeWithNowhereToAsk: StoryObj = { render: () => <Composing fixed={null} circles={[]} /> };

export const ComposeCarrying: StoryObj = {
  render: () => (
    <Composing
      fixed="aurora" circles={['aurora']}
      start={{
        sentence: PROPOSED.sentence, to: 'engine', links: 'https://tickets.example/T-42',
        files: [new File([new Uint8Array(2_300)], 'trace.log')],
      }}
    />
  ),
};
