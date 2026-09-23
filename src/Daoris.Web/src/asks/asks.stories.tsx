import { type ReactNode, useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { Ask, Session } from '../api';
import { NO_CARRY } from '../compose/carry';
import { AskCard } from './AskCard';
import { AskComposer, type AskDraft } from './AskComposer';
import { AskRecord } from './AskRecord';
import {
  BY_INTAKE, CLOSED, INTAKE_ASKED, INTAKE_PARKED, INTAKE_SESSION, LONG_CJK, NAMED, PROPOSED, PUBLISHED, REFUSED,
  UNKNOWN_TIER, UNMATCHED,
} from './fixtures';

// An ask (D65 §1a), made at a workspace and read back as a record (INT4c): its card in the Quests
// view, the drawer that is its record, and the composer that makes one. Every state a real machine
// reaches, and a sentence long enough and CJK enough to try the layout.

const meta: Meta = { title: 'Asks' };
export default meta;

const noop = () => {};
const RECEIVERS = ['engine', 'game', 'lantern'];
const TITLES = { '9a8b7c6d5e4f': 'The chunk streamer stalls on a cold cache — cap its hydration per frame.' };

const Provided = ({ children }: { children: ReactNode }) => <Tooltip.Provider>{children}</Tooltip.Provider>;

export const Cards: StoryObj = {
  render: () => (
    <div className="grid max-w-3xl gap-0">
      {[PROPOSED, UNMATCHED, PUBLISHED, NAMED, BY_INTAKE, REFUSED, CLOSED, UNKNOWN_TIER, LONG_CJK].map((ask) => (
        <AskCard key={ask.id} ask={ask} onOpen={noop} />
      ))}
    </div>
  ),
};

const record = (ask: Ask, intake: Session | null = null, attend = false) => () => (
  <Provided>
    <AskRecord
      ask={ask} receivers={RECEIVERS} questTitles={TITLES} intake={intake} onAttend={attend ? noop : undefined}
      onPublish={noop} onClose={noop} onOpenQuest={noop} onDismiss={noop}
    />
  </Provided>
);

export const RecordProposed: StoryObj = { render: record(PROPOSED) };
export const RecordUnmatched: StoryObj = { render: record(UNMATCHED) };
export const RecordPublished: StoryObj = { render: record(PUBLISHED) };
export const RecordNamed: StoryObj = { render: record(NAMED) };
export const RecordRefusedReceiver: StoryObj = { render: record(REFUSED) };
export const RecordClosed: StoryObj = { render: record(CLOSED) };
export const RecordUnknownTier: StoryObj = { render: record(UNKNOWN_TIER) };
export const RecordLongCjk: StoryObj = { render: record(LONG_CJK) };

// Who answered (INT4d): the intake session that served the ask, a door into Sessions on the desktop.
/** Its intake read it and published it — on the desktop, the session is a door. */
export const RecordByIntake: StoryObj = { render: record(BY_INTAKE, INTAKE_SESSION, true) };
/** The same record in a browser: the session is named, and is not a door. */
export const RecordByIntakeInABrowser: StoryObj = { render: record(BY_INTAKE, INTAKE_SESSION) };
/** Its intake could not settle whose it is and parked asking — the proposal is still the person's. */
export const RecordIntakeAsked: StoryObj = { render: record(INTAKE_ASKED, INTAKE_PARKED, true) };
/** An intake the page has not loaded: named by its id, and no door that would open nothing. */
export const RecordIntakeUnloaded: StoryObj = { render: record(BY_INTAKE, null, true) };

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
