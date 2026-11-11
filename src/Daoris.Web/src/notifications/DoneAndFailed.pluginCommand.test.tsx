import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { noticeWords } from './DoneAndFailed';

const notice = (route: string, state: string) => ({
  id: `plugin-command:acme-boards/status/${route}`, kind: 'plugin-command', at: '2026-11-12T09:02:00.000Z', read: false,
  code: 'acme-boards', act: 'status', route, state,
});

describe('Done and failed: a plugin command', () => {
  it.each([
    ['en', 'agent', 'reached', 'acme-boards ran status for a session'],
    ['en', 'removal', 'reached', 'acme-boards ran status as it was removed'],
    ['en', 'window', 'failed', 'status of acme-boards failed'],
    ['zh', 'agent', 'reached', '插件 acme-boards 为一个会话运行了 status'],
    ['zh', 'removal', 'reached', '插件 acme-boards 在被移除时运行了 status'],
    ['zh', 'agent', 'failed', '插件 acme-boards 的 status 失败了'],
  ])('says the plugin and the command by name and who asked (%s, %s, %s)', async (language, route, state, words) => {
    await i18n.changeLanguage(language);
    expect(noticeWords(notice(route, state), i18n.t)).toBe(words);
    await i18n.changeLanguage('en');
  });
});
