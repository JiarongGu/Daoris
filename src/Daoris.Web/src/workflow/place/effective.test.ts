import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { EFFECTIVE_CURRENT, EFFECTIVE_DOCS, EFFECTIVE_LANDED_NONE, EFFECTIVE_RELEASE, EFFECTIVE_UNREAD } from './effectiveFixtures';
import { effectiveFlow, effectiveLabel, routeKeyOf, routeRows, routeWords } from './effective';
import { NO_VERIFICATION, START } from '../fixtures';

// SETUPUX1o (D221; the Setup design's §2.3): the place's routes drawn as a selection, and the chart the chosen one draws: Current or a saved
// version, labelled, read onto the one drawing from the driver's `effective` answer.

const t = i18n.t.bind(i18n);
const scope = { repository: 'engine', workspace: 'aurora' };

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("the place's routes", () => {
  it('are default work, each kind the workspace declares, and landed work, each with the workflow it follows and why', () => {
    const rows = routeRows(t, START, scope);

    expect(rows.map((row) => [row.key, row.label, row.target])).toEqual([
      ['default', 'Default work', 'Current'],
      ['kind:docs', 'Documentation', 'Documentation to a pull request'],
      ['landed', 'Landed work', 'Verify the line'],
    ]);
    expect(rows[1]!.why).toBe('chosen by the workspace aurora for Documentation');
    expect(rows.map((row) => row.landed)).toEqual([false, false, true]);
  });

  it('read landed Current as no verification here', () => {
    expect(routeRows(t, NO_VERIFICATION, scope).at(-1)).toMatchObject({ key: 'landed', target: 'No verification here' });
  });

  it('are keyed as the driver asks a route: default, kind:<name>, landed', () => {
    expect([null, 'docs'].map(routeKeyOf)).toEqual(['default', 'kind:docs']);
  });

  it('say their route in a sentence: default work, landed work, or the kind as the person named it', () => {
    expect([routeWords(t, 'default', null), routeWords(t, 'landed', null), routeWords(t, 'kind:docs', 'Documentation')])
      .toEqual(['default work', 'landed work', 'Documentation']);
  });
});

describe('the chart label', () => {
  it('says Current follows its old landing rule', () => {
    expect(effectiveLabel(t, EFFECTIVE_CURRENT, 'default work')).toBe('Follows its old landing rule');
  });

  it('names a saved workflow, its version and the route that follows it', () => {
    expect(effectiveLabel(t, EFFECTIVE_RELEASE, 'default work')).toBe('Release for engine v2, the saved workflow default work follows');
    expect(effectiveLabel(t, EFFECTIVE_DOCS, 'Documentation')).toBe('Documents workflow v1, the saved workflow Documentation follows');
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    expect(effectiveLabel(i18n.t.bind(i18n), EFFECTIVE_CURRENT, '默认工作')).toContain('旧的合并规则');
  });
});

describe('the effective graph drawn', () => {
  it('draws the saved graph in sections through the one drawing, a cell for each step it names', () => {
    const drawn = effectiveFlow(t, i18n, EFFECTIVE_RELEASE, { label: 'How work moves' });

    expect(drawn.flow.sections.map((section) => section.title)).toEqual(['Before it lands', 'After it lands']);
    expect(drawn.flow.nodes.some((node) => node.mark?.word === 'Work done here')).toBe(true);
    expect([...drawn.cells.keys()]).toEqual(EFFECTIVE_RELEASE.cells.map((cell) => cell.node));
  });

  it("says a step's way on in its cell's press, and automatically where it has none", () => {
    const { flow } = effectiveFlow(t, i18n, EFFECTIVE_RELEASE, { label: 'x' });
    const review = flow.edges.find((edge) => edge.from === 'review' && edge.to === 'landing')!;
    const work = flow.edges.find((edge) => edge.from === 'work')!;
    expect([review.says, work.says]).toEqual(['once you accept it', 'automatically']);
  });

  it('draws nothing where the driver answers no graph: landed Current, or a workflow that does not read', () => {
    expect(effectiveFlow(t, i18n, EFFECTIVE_LANDED_NONE, { label: 'x' }).flow.nodes).toEqual([]);
    expect(effectiveFlow(t, i18n, EFFECTIVE_UNREAD, { label: 'x' }).flow.nodes).toEqual([]);
  });
});
