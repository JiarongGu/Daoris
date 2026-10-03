import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { type ComponentProps, useState } from 'react';
import { I18nextProvider } from 'react-i18next';
import i18n from '../i18n';
import { InTheme } from '../plugins/storyIcons';
import { HowItCameToBe } from './HowItCameToBe';
import {
  answer, MISSING_CHAIN, NO_ASK_CHAIN, NOTHING, OLD_CHAIN, QUEST_CHAIN, SESSION_CHAIN, UNREAD_ANSWER, WAITING_CHAIN,
} from './traceFixtures';

// How a session or a quest came to be (TRACE1b, D143, D50), as its page's section shows it: folded, as every page first
// shows it; folded once read, with its story; a quest's chain whole, open; a session's; links nothing keeps, each said
// where it would have been; a quest no ask asked, with a running session's rules; an ask from before words were kept, a
// merge kept only as the acceptance, and a teammate's record; a store that did not answer; nothing found; reading; a
// refusal; and each in 中文, in dark and narrow.

/** A reader of 中文, whatever the window's language, sharing the catalogues. */
const zh = i18n.cloneInstance({ lng: 'zh' });
const chinese: Decorator = (Story) => <I18nextProvider i18n={zh}><Story /></I18nextProvider>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
const narrow: Decorator = (Story) => <div style={{ width: 360 }}><Story /></div>;

type Props = ComponentProps<typeof HowItCameToBe>;

/** The page holds the fold; here a story does, so a press opens it as it would there. */
function Held(props: Props) {
  const [open, setOpen] = useState(props.open);
  return <HowItCameToBe {...props} open={open} onToggle={() => setOpen((was) => !was)} />;
}

const meta: Meta<typeof HowItCameToBe> = {
  title: 'Work/HowItCameToBe',
  component: HowItCameToBe,
  args: {
    kind: 'quest', id: 'q1', open: true, onToggle: () => {}, answer: answer(QUEST_CHAIN),
    onSession: () => {}, onQuest: () => {}, onAsk: () => {},
  },
  render: (args) => <Held {...args} />,
};
export default meta;

type Story = StoryObj<typeof HowItCameToBe>;

/** Folded, as a page first shows it: nothing read until the person opens it. */
export const Folded: Story = { args: { open: false, answer: undefined } };

/** Folded once read: its story on one line, from the ask through the quest to its sessions. */
export const FoldedRead: Story = { args: { open: false } };

/** A quest's chain, whole: the ask with the person's words and go-aheads, the quest, both its sessions and the carry-on. */
export const Quest: Story = {};

/** The same in 中文: Daoris's lines worded, the person's words as written. */
export const QuestChinese: Story = { ...Quest, decorators: [chinese] };

/** The same in dark. */
export const QuestDark: Story = { ...Quest, decorators: [dark] };

/** A session's chain: its ask, its quest, and that session alone. */
export const Session: Story = { args: { kind: 'session', id: 's2', answer: answer(SESSION_CHAIN) } };

/** A session's chain in 中文. */
export const SessionChinese: Story = { ...Session, decorators: [chinese] };

/** A session's chain in dark. */
export const SessionDark: Story = { ...Session, decorators: [dark] };

/** Links nothing keeps, each said missing where it would have been, and never guessed (D143 point 3). */
export const Missing: Story = { args: { kind: 'session', id: 's3', answer: answer(MISSING_CHAIN) } };

/** Missing links in 中文. */
export const MissingChinese: Story = { ...Missing, decorators: [chinese] };

/** A quest no ask asked, its running session's rules read from the file it was handed, and a cut instruction. */
export const NoAsk: Story = { args: { id: 'q5', answer: answer(NO_ASK_CHAIN) } };

/** An ask from before words were kept, a merge kept only as the person's acceptance, and a teammate's record. */
export const OldRecords: Story = { args: { id: 'q7', answer: answer(OLD_CHAIN) } };

/** Waiting to land automatically (LAND2b): held once by a branch that stands, and an earlier landing not saying who accepted it. */
export const WaitingToLand: Story = { args: { kind: 'session', id: 's8', answer: answer(WAITING_CHAIN) } };

/** Waiting to land, in 中文. */
export const WaitingToLandChinese: Story = { ...WaitingToLand, decorators: [chinese] };

/** A store that did not answer: the session records, so no session is read, and the terminal's door named. */
export const StoreUnread: Story = { args: { answer: UNREAD_ANSWER } };

/** Nothing on the record names it. */
export const Nothing: Story = { args: { kind: 'session', id: 's9', answer: NOTHING } };

/** Reading, on the first open. */
export const Reading: Story = { args: { answer: undefined, reading: true } };

/** The route refused, in its own sentence. */
export const Refused: Story = { args: { answer: undefined, refusal: 'the driver is still coming up — its service is not answering yet. A moment.' } };

/** A narrow pane: every line wraps at the pane's edge, a branch's name anywhere. */
export const Narrow: Story = { ...Quest, decorators: [narrow] };

/** Narrow, in 中文. */
export const NarrowChinese: Story = { ...Quest, decorators: [narrow, chinese] };
