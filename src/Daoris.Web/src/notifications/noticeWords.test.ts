import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { en, zh } from '../locales/index';
import { attentionToast, noticeWords } from './noticeWords';

/**
 * NOTIFY2d (D196, the notification design §3.3, §9): the words the page hands the shell's notifier, and the toast a focused window
 * says for the same attention, each in the reader's language.
 */
describe('the notices\' words (NOTIFY2d)', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  /** Every word the notifier reads, and every person's route's act, in the reader's language, its placeholders kept for the shell. */
  it('hands every notice\'s words and every act, in the reader\'s language, placeholders kept', async () => {
    const handed = Object.keys(en).filter((key) => key.startsWith('notifications.system.') || key.startsWith('person.route.')).sort();

    const english = noticeWords(i18n.t);
    expect(Object.keys(english).sort()).toEqual(handed);
    expect(english['notifications.system.parked.title']).toBe('{{repository}}: a session waits on you');
    expect(english['person.route.SESSION_INPUT']).toBe('say words to a session as yours');

    await i18n.changeLanguage('zh');
    const chinese = noticeWords(i18n.t);
    expect(Object.keys(chinese).sort()).toEqual(handed);
    expect(chinese['notifications.system.parked.title']).toBe('{{repository}}：一个会话等你处理');
    expect(chinese['notifications.system.confirming.body']).toBe('它请求{{act}}。打开 Daoris 确认或拒绝。');
    expect(chinese['notifications.system.questParked.titleCount']).toBe(zh['notifications.system.questParked.titleCount']);
  });

  it.each([
    ['en', { kind: 'Parked', repository: 'engine', headline: 'engine — a session needs you', detail: 'Two ways forward; I recommend capping.' },
      'engine: a session waits on you — Two ways forward; I recommend capping.'],
    ['zh', { kind: 'Parked', repository: 'engine', headline: 'engine — a session needs you', detail: '两条路可走；我建议封顶。' },
      'engine：一个会话等你处理——两条路可走；我建议封顶。'],
    ['zh', { kind: 'Parked', repository: 'engine', headline: 'engine — a session needs you' }, 'engine：一个会话等你处理'],
    ['en', { kind: 'QuestParked', repository: 'engine', quest: '7a82cc', strikes: 3, headline: 'h', detail: "You've hit your limit." },
      "engine: quest #7a82cc parked after 3 failed sessions — You've hit your limit."],
    ['zh', { kind: 'QuestParked', repository: 'engine', quest: '7a82cc', strikes: 1, headline: 'h' }, 'engine：委托 #7a82cc 在 1 个失败的会话后挂起'],
    ['zh', { kind: 'QuestParked', repository: 'engine', quest: '7a82cc', headline: 'h' }, 'engine：委托 #7a82cc 因失败的会话而挂起'],
    ['en', { kind: 'Ended', repository: 'tools', state: 'stood-down', headline: 'tools — a session stood-down' }, 'tools: a session stood down'],
    ['zh', { kind: 'Ended', repository: 'tools', state: 'failed', headline: 'tools — a session failed' }, 'tools：一个会话失败了'],
    ['zh', { kind: 'Ended', repository: 'tools', state: 'vanished', headline: 'tools — a session vanished' }, 'tools：一个会话已结束'],
    ['zh', { kind: 'Waiting', repository: 'engine', headline: 'engine — waits for an account', detail: 'Every account is cooling until 15:00.' },
      'engine：一次启动在等待账户'],
    ['zh', { kind: 'Answered', repository: 'engine', headline: 'engine — a conversation answered you' }, 'engine：一个对话回复了你'],
    ['zh', { kind: 'Answered', repository: 'daoris:help', headline: 'Ask Daoris answered you' }, '问道衍回复了你'],
    ['en', { kind: 'Holding', repository: 'engine', headline: 'engine: a session holds a tool call for you', detail: 'npm test' },
      'engine: a session holds a tool call for you — npm test'],
    ['zh', { kind: 'Holding', repository: 'engine', headline: 'engine: a session holds a tool call for you', detail: 'npm test' },
      'engine：一个会话的工具调用在等你处理——npm test'],
    ['zh', { kind: 'Later', headline: 'engine — something new', detail: 'As it said.' }, 'engine — something new: As it said.'],
  ] as const)('a %s window toasts %j in its own words', async (language, item, said) => {
    await i18n.changeLanguage(language);
    expect(attentionToast(i18n.t, item)).toBe(said);
  });

  /** An end's state is said by its own words, never its code in a sentence (§6). */
  it('never puts an end\'s state code in a sentence', async () => {
    await i18n.changeLanguage('zh');
    for (const state of ['completed', 'failed', 'stopped', 'declined', 'stood-down', 'vanished']) {
      expect(attentionToast(i18n.t, { kind: 'Ended', repository: 'tools', state, headline: 'h' })).not.toContain(state);
    }
  });
});
