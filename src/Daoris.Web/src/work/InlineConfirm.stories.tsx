import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { InlineConfirm } from './InlineConfirm';

// The one inline confirmation for a destructive act (UXFIX2): a sentence on one line with the move first and *Never mind*;
// a body of rows (a clear's *What goes* and *What stays*); its wait, the page's busy and its refusal said inside it, word for
// word; and *Close* alone where nothing can be confirmed. Its words are the quest's delete's and a workspace's clear's, read
// in the story's language, at the main area's width beside a 1546 px window and at 680 px, in both themes. UXFIX2c adds the
// move pressed, saying its act started, and a refusal with a token nothing breaks at the main area's 400 px floor.

const nothing = () => {};

/** The main area at a width: 1180 px beside a 1546 px window's side bar and list, 600 px at a 680 px window. */
function Main({ width, children }: { width: number; children: ReactNode }) {
  return <div className="@container/main max-w-full bg-page p-4" style={{ width }}>{children}</div>;
}

const wide: Decorator = (Story) => <Main width={1180}><Story /></Main>;
const narrow: Decorator = (Story) => <Main width={600}><Story /></Main>;
/** The main area's floor (UXFIX2c), 400 px beside an open list and side bar in a wider window. */
const atFloor: Decorator = (Story) => <Main width={400}><Story /></Main>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/**
 * A refusal with tokens nothing breaks (UXFIX2c): a raw error naming an object by its 64-digit id, then the same id as code,
 * each wider than the floor on one line, with no hyphen or space a line could break at.
 */
const OBJECT = '3f9a1c7e5b2d8f4a6c0e9b7d5f3a1c8e6b4d2f0a9c7e5b3d1f8a6c4e2b0d9f7a';
const UNBROKEN = `git refused: fatal: bad object ${OBJECT}, so \`${OBJECT}\` stays where it was.`;

/** A quest's delete, in the story's language: one sentence, then *Delete quest* and *Never mind*. */
function QuestDelete({ busy = false, refused = false, unbroken = false }: { busy?: boolean; refused?: boolean; unbroken?: boolean }) {
  const { t } = useTranslation();
  return (
    <InlineConfirm
      label={t('quests.detail.deleteTitle')}
      says={t('quests.detail.deleteConfirm')}
      meanIt={t('quests.detail.deleteMeanIt')}
      busy={busy}
      refused={unbroken ? UNBROKEN : refused ? t('errors.QUEST_PAUSED', { id: 'abc123', pause: 'a1b2c3' }) : null}
      onConfirm={nothing}
      onClose={nothing}
    />
  );
}

/** The move pressed, its act on its way and never answering: the ask says it started (UXFIX2c). */
const pressTheMove: StoryObj<typeof QuestDelete>['play'] = async ({ canvasElement }) => {
  canvasElement.querySelector<HTMLButtonElement>('[role="group"] button')?.click();
};

/** A body of rows, as a workspace's clear lists: what it takes, what goes and what stays, then *Clear 3*. */
function Rows({ refused = false, nothingToConfirm = false }: { refused?: boolean; nothingToConfirm?: boolean }) {
  const { t } = useTranslation();
  return (
    <InlineConfirm
      block
      label={t('history.title')}
      says={(
        <>
          <p className="m-0 text-small text-ink-soft">{t(nothingToConfirm ? 'history.nothing' : 'quests.detail.deleteConfirm')}</p>
          {!nothingToConfirm && (
            <div className="grid gap-4 @min-[44rem]/main:grid-cols-2">
              <div>
                <h3 className="m-0 mb-1.5 text-small font-semibold text-ink-faint">{t('history.goes')}</h3>
                <ul className="m-0 grid list-none gap-2 p-0"><li className="text-body text-ink">#9a8b7c</li></ul>
              </div>
              <div>
                <h3 className="m-0 mb-1.5 text-small font-semibold text-ink-faint">{t('history.stays')}</h3>
                <ul className="m-0 grid list-none gap-2 p-0"><li className="text-body text-ink">#6c7d8e</li></ul>
              </div>
            </div>
          )}
        </>
      )}
      meanIt={nothingToConfirm ? undefined : t('projects.workspace.clearMeanIt', { count: 3 })}
      refused={refused ? t('errors.QUEST_PAUSED', { id: '6c7d8e', pause: 'b2c3d4' }) : null}
      onConfirm={nothing}
      onClose={nothing}
    />
  );
}

const meta: Meta<typeof QuestDelete> = {
  title: 'Work/Inline confirmation',
  component: QuestDelete,
  decorators: [wide],
};
export default meta;

type Story = StoryObj<typeof QuestDelete>;

/** One sentence: what the second press does, then the move and *Never mind*. */
export const Sentence: Story = {};

/** The same in 中文. */
export const SentenceChinese: Story = { decorators: [chinese] };

/** The same in dark. */
export const SentenceDark: Story = { decorators: [dark] };

/** At 680 px: the presses wrap under the sentence. */
export const SentenceNarrow: Story = { decorators: [narrow] };

/** Another act on the record is on its way: both presses wait. */
export const Waiting: Story = { args: { busy: true } };

/** Pressed (UXFIX2c): its act on its way, both presses wait, the move keeps its name, and a status beside them says so. */
export const Pending: Story = { play: pressTheMove };

/** Pressed, in 中文 and dark, at 680 px. */
export const PendingNarrowChineseDark: Story = { decorators: [chinese, dark, narrow], play: pressTheMove };

/** Refused: the sentence said inside it, whole, under the presses, which may be pressed again. */
export const Refused: Story = { args: { refused: true } };

/** Refused, in 中文. */
export const RefusedChinese: Story = { args: { refused: true }, decorators: [chinese] };

/** Refused, in dark. */
export const RefusedDark: Story = { args: { refused: true }, decorators: [dark] };

/** Refused at 680 px, in 中文 and dark. */
export const RefusedNarrowChineseDark: Story = { args: { refused: true }, decorators: [chinese, dark, narrow] };

/** Refused at the main area's 400 px floor with a token nothing breaks (UXFIX2c): it breaks inside, and the ask stays 400 px. */
export const RefusedUnbrokenAtTheFloor: Story = { args: { unbroken: true }, decorators: [atFloor] };

/** The same, in dark. */
export const RefusedUnbrokenAtTheFloorDark: Story = { args: { unbroken: true }, decorators: [atFloor, dark] };

/** A body of rows, as a clear lists what goes and what stays. */
export const RowsOfABody: Story = { render: () => <Rows /> };

/** The rows in 中文. */
export const RowsChinese: Story = { render: () => <Rows />, decorators: [chinese] };

/** The rows in dark, refused. */
export const RowsRefusedDark: Story = { render: () => <Rows refused />, decorators: [dark] };

/** The rows at 680 px: what goes above what stays. */
export const RowsNarrow: Story = { render: () => <Rows />, decorators: [narrow] };

/** Nothing to confirm: said, with *Close* alone. */
export const NothingToConfirm: Story = { render: () => <Rows nothingToConfirm /> };
