import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { NotePart } from '../data/api';
import { noteLines } from './noteLines';

const t = i18n.getFixedT('en');
const zh = i18n.getFixedT('zh');

const coded = (code: string, text: string, values: Record<string, unknown> = {}): NotePart => ({ code, values, text });
const words = (said: string, by: string): NotePart => ({ words: said, by });

describe('the landing note for the repository checkout', () => {
  /** A look that found the repository's own checkout dirty says it waits for the checkout, not for a review, in either language. */
  it('words a look held by the repository checkout as its own line, in either language', () => {
    const refused = "the repository's own checkout is not clean (the working tree has uncommitted changes (2 paths)).";
    const parts = [
      coded('landing.root-dirty', "not accepted automatically: the repository's own checkout holds uncommitted work …"),
      words(refused, 'program'),
    ];

    expect(noteLines(t, { parts }, 'en')).toEqual([
      {
        kind: 'said',
        text: "Not accepted automatically: the repository's own checkout holds uncommitted work, and Daoris does not merge into work in "
          + 'flight, as follows. It lands at the first look after the checkout is clean; no review is waiting on you.',
      },
      { kind: 'words', text: refused, by: 'program' },
    ]);
    expect(noteLines(zh, { parts }, 'zh')).toEqual([
      { kind: 'said', text: '未自动采纳：仓库自己的检出里有未提交的工作，Daoris 不会合并到进行中的工作里，详情如下。检出干净后的下一轮会落地它的工作；没有需要你审阅的事。' },
      { kind: 'words', text: refused, by: 'program' },
    ]);
  });
});
