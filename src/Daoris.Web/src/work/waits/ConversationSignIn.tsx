import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { WaitingCard } from '../../design/ui';
import type { ParkQuestion } from './parkQuestion';
import { QuestionCard } from './QuestionCard';
import { SIGNED_IN, SignInAsk } from './SignInAsk';
import { InConversation } from './WaitAnchor';

/**
 * **A conversation's sign-in question in its dock**: the agent's words, then *Open the sign-in…* and *Signed in*, as a driven
 * session's park draws them (`AwaitingPerson`), and no more. A conversation has no park to finish or decline, and its composer takes
 * the person's own words, which answer it as *Signed in* does: the driver clears the question once any word of theirs is kept.
 *
 * @remarks
 * *Signed in* is sent through the frame's say door (`onAnswer`), which may refuse in a sentence; it is said here, beside the press.
 * A sign-in Daoris's browser sees answers it as well, by the driver's look.
 * A molecule: the answer's door and its pending are the frame's.
 */
export function ConversationSignIn({ question, site, pending = false, onAnswer, onConversation }: {
  question: ParkQuestion;
  /** The page whose sign-in it waits on, as the question names it. */
  site: string;
  pending?: boolean;
  /** The answer *Signed in* sends, settling with the sentence a refusal is said in, or nothing once it is kept. */
  onAnswer?: (answer: string) => Promise<string | null> | void;
  /** The way to the message that asked, in the conversation, where it holds it: *In the conversation*. */
  onConversation?: () => void;
}) {
  const { t } = useTranslation();
  const [sending, setSending] = useState(false);
  const [refused, setRefused] = useState<string | null>(null);
  const signed = async () => {
    if (!onAnswer) return;
    setSending(true);
    setRefused(null);
    try {
      setRefused((await onAnswer(SIGNED_IN)) ?? null);
    } finally {
      setSending(false);
    }
  };
  return (
    <WaitingCard title={t('work.head.waiting')}>
      <div className="mt-1.5">
        <QuestionCard question={question} />
        <SignInAsk site={site} busy={pending || sending} onSigned={onAnswer ? () => { void signed(); } : undefined} />
        {refused && <p role="status" className="m-0 mt-2 text-small text-ink-danger [overflow-wrap:anywhere]">{refused}</p>}
      </div>
      <p className="m-0 mt-2 flex flex-wrap items-center gap-x-2 text-small text-ink-faint">
        <span>{t('work.wait.signIn.conversation')}</span>
        {onConversation && <InConversation onPress={onConversation} />}
      </p>
    </WaitingCard>
  );
}
