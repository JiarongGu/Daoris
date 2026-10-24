import type { TFunction } from 'i18next';

/**
 * A chat run's press's own answer (CHATFLOW1d, `ChatPressCodes`): why the press refused or what it asked, a code the page words in the
 * person's language, and the reviewer it asked. Absent for every other press, and from a driver before it.
 */
export type ChatPressAnswer = { code?: string | null; reviewer?: string | null } | null | undefined;

/** The codes this page words; another a newer driver names is said in the driver's sentence. */
const CODES: readonly string[] = ['turn-running', 'held-elsewhere', 'opinion-asked', 'moved', 'unread', 'no-words'];

/**
 * What a chat run's press says in the person's language (CHATFLOW1d, the chat-flow design §5.2): a turn running, another window holding
 * the conversation, the second opinion asked at this press, the work moved while it was judged. Null where the press answered none of
 * these, and the driver's sentence is said.
 */
export function chatPressSaid(t: TFunction, chat: ChatPressAnswer): string | null {
  if (!chat?.code || !CODES.includes(chat.code)) return null;
  if (chat.code === 'opinion-asked') {
    return chat.reviewer
      ? t('work.review.chatPress.opinion-asked', { reviewer: chat.reviewer })
      : t('work.review.chatPress.opinion-askedAnyone');
  }

  return t(`work.review.chatPress.${chat.code}`);
}
