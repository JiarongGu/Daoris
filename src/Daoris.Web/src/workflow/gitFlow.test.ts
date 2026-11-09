import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { drawnFromJson, graphFlow } from './graphFlow';
import { INTEGRATED_GIT_GRAPH } from './graphFixtures';

// The chart of a composition of the Git kinds, drawn before the cut-over offers them (the process-steps design §3): each stuck outcome a
// port routed on, each way back drawn as a return on the outcome it is taken on and worded by the kind it leaves, the arriving `conflicted`
// of an integrate among them, in both languages.

const t = i18n.t.bind(i18n);

afterEach(async () => { await i18n.changeLanguage('en'); });

const flowIn = async (language: string) => {
  await i18n.changeLanguage(language);
  return graphFlow(t, drawnFromJson(t, i18n, INTEGRATED_GIT_GRAPH), { label: 'How work moves' });
};

describe('the chart of the Git kinds', () => {
  it('draws each step\'s outcomes as routed ports, and each way back as a return on its outcome', async () => {
    const flow = await flowIn('en');
    const ports = (id: string) => flow.nodes.find((node) => node.id === id)!.step!.ports.map((port) => `${port.outcome}:${port.said ?? port.outcome}:${port.routed}`);
    expect(ports('rebase')).toEqual(['ready:ready:true', 'conflicted:conflicted:true', 'uncommitted:uncommitted work in its tree:true']);
    expect(ports('landing')).toEqual(['merged:merged:true', 'held:already on its target:true', 'behind:behind its target:true']);
    expect(ports('integrate')).toEqual(['conflicted:conflicted on the line:true']);

    const back = (id: string) => flow.edges.find((edge) => edge.id === id)!;
    expect([back('m3').style, back('m3').port, back('m3').to]).toEqual(['back', 'behind', 'rebase']);
    expect(back('m3').says).toContain('behind its target');
    expect([back('r4').style, back('r4').port]).toEqual(['back', 'resolved']);
    expect(back('i2').says).toContain('conflicted on the line');
  });

  it('words the same ports and returns in Chinese', async () => {
    const flow = await flowIn('zh');
    const said = (id: string) => flow.nodes.find((node) => node.id === id)!.step!.ports.map((port) => port.said ?? port.outcome);
    expect(said('landing')).toEqual(['已合并', '目标已含有它', '落后于目标']);
    expect(said('resolve')).toEqual(['已解决']);
    expect(flow.edges.find((edge) => edge.id === 'm3')!.says).toContain('落后于目标');
  });
});
