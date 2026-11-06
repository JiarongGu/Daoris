import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { scopeOf } from '../settings/accounts/accountsFixtures';
import { nextStartSaid } from './agents';

// AGENTPROVIDER1 row s-ix o1 to o3: the agent card speaks for the account's own door, and says one line for each other door whose
// answer to *does a start run in its own home* differs, in both languages.

describe('the next start card’s doors', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  const labelOf = (name: string) => name;

  it('adds one line for each other door whose own-home answer differs', () => {
    const owed = scopeOf({ next: { account: null, reason: 'noAccount', others: [], doors: [{ door: 'p-claude-bare', startsInOwnHome: true }] } });
    expect(nextStartSaid(owed, labelOf)).toEqual({
      takes: 'No Daoris account is signed in for it; its starts wait until you sign in.', held: null,
      doors: ['A start on `p-claude-bare` runs in its own home, which is nobody\'s sign-in: it is owed no account.'],
    });
    // Where the card's own door starts in its own home, the card is drawn for the door beside it that is owed an account.
    const own = scopeOf({ next: { account: null, reason: 'own', others: [], doors: [{ door: 'p-b', startsInOwnHome: false }] } });
    expect(nextStartSaid(own, labelOf)?.doors).toEqual(['A start on `p-b` is owed a Daoris account, and waits until one is signed in.']);
  });

  it('says the same in 中文', async () => {
    await i18n.changeLanguage('zh');
    const owed = scopeOf({ next: { account: null, reason: 'noAccount', others: [], doors: [{ door: 'p-claude-bare', startsInOwnHome: true }] } });
    expect(nextStartSaid(owed, labelOf)?.doors).toEqual(['在 `p-claude-bare` 上的启动运行在它自己的配置目录中，那里不是任何人的登录：它不需要账户。']);
  });

  it('says nothing where the own-home reason has no other door, and nothing of an answer that names none', () => {
    expect(nextStartSaid(scopeOf({ next: { account: null, reason: 'own', others: [] } }), labelOf)).toBeNull();
    expect(nextStartSaid(scopeOf({ next: { account: null, reason: 'noAccount', others: [] } }), labelOf)?.doors).toEqual([]);
  });
});
