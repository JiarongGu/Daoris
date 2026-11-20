import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { declarationChangedWhy } from './declarationWords';
import type { DeclarationCommandsChange } from './attentionPushed';

// The difference in commands the driver read: the checks by name, the other parts by key; their commands are Setup's to show.
const change: DeclarationCommandsChange = {
  added: [{ name: 'e2e', run: 'npm run e2e' }], removed: [{ name: 'lint', run: 'npm run lint' }],
  changed: [{ name: 'unit', run: 'dotnet test -c Release', was: 'dotnet test' }, { name: 'tools', run: 'npm test' }], other: ['generated'],
};

describe("a changed declaration's row, worded", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('names the checks its commands added, removed and changed, and says work not changing them is checked as accepted', () => {
    expect(declarationChangedWhy('trunk', change)).toBe(
      'The commands in `daoris.gates.json` on trunk changed since you accepted them: added `e2e`; removed `lint`; changed `unit`, `tools`; '
      + 'changed its `generated` part. Any work that does not change them itself is checked under the ones you accepted; read the change on Setup and accept it.');
    expect(declarationChangedWhy('trunk', null)).toBe('The commands in `daoris.gates.json` on trunk changed since you accepted them. '
      + 'Any work that does not change them itself is checked under the ones you accepted; read the change on Setup and accept it.');
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    expect(declarationChangedWhy('trunk', change)).toBe('trunk 上 `daoris.gates.json` 里的命令自你采纳后已改动：新增 `e2e`；移除 `lint`；改动 `unit`、`tools`；'
      + '改动了它的 `generated` 部分。自己没有改动它们的工作仍按你采纳过的那份验证；请在配置里读一读改动并采纳它。');
    expect(declarationChangedWhy('trunk', { added: [], removed: [], changed: [], other: [] }))
      .toBe('trunk 上 `daoris.gates.json` 里的命令自你采纳后已改动。自己没有改动它们的工作仍按你采纳过的那份验证；请在配置里读一读改动并采纳它。');
  });
});
