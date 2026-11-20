import { useContext } from 'react';
import { useTranslation } from 'react-i18next';
import { moment } from '../../locales/format';
import type { Block } from '../conversation/conversationTypes';
import { CannotLine, CodedLine, handedTo, HandedLine, NoteLine, WentLine } from '../conversation/NoteLines';
import { SaidDoors } from '../conversation/saidDoors';

/**
 * A driver's note in the story, with the doors it offers from the host's `SaidDoors`: a word a stop cut off (*Say it again*), where
 * the person's words went (the session they went to), why they cannot go on here (*Start a conversation*, *Go on with Daoris's
 * brief*), a conversation handed to another agent, or the note worded by its parts.
 */
export function SaidNote({ block }: { block: Block }) {
  if (block.code) return <CodedLine block={block} />;
  if (block.to) return <WentLine block={block} />;
  if (block.why) return <CannotLine block={block} />;
  const to = handedTo(block);
  if (to) return <HandedLine block={block} to={to} />;
  return <NoteLine text={block.text ?? ''} parts={block.parts} under={block.under} />;
}

/** When held words reach the session: where they wait for a resume, as it goes on (at a cooling account's reset), else after the turn. */
export function HeldWhen({ reaches }: { reaches?: string | null }) {
  const { t } = useTranslation();
  const { chat, cooling } = useContext(SaidDoors);
  const resume = reaches === 'resume';
  return (
    <span className="text-caption text-ink-faint">
      {resume && cooling ? t('work.conversation.held.cooling', { time: moment(cooling.until) })
        : resume ? t(chat ? 'work.conversation.held.reopen' : 'work.conversation.held.resume')
          : t('work.session.story.held')}
    </span>
  );
}
