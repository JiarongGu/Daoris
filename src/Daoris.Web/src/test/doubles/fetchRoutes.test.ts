import { afterEach, describe, expect, it, vi } from 'vitest';
import { routed, stubFetch } from './fetchRoutes';

describe('the service stood in over fetch', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('answers the first route whose prefix the URL starts with, a function of the URL included', async () => {
    const respond = routed({ '/api/sessions': (url: string) => (url.includes('help') ? [] : ['s1']), '/api/s': ['wide'] });

    expect(await respond('/api/sessions?kind=chat').json()).toEqual(['s1']);
    expect(await respond('/api/sessions?kind=help').json()).toEqual([]);
    expect(await respond('/api/status').json()).toEqual(['wide']);
  });

  it('answers an empty list, or what the suite says, where no route names the URL', async () => {
    expect(await routed({})('/api/quests').json()).toEqual([]);
    expect(await routed({}, { none: true })('/api/quests').json()).toEqual({ none: true });
  });

  it('stubs the global fetch and hands back the mock its reads are counted on', async () => {
    const fetch = stubFetch({ '/api/registry': [{ repository: 'engine' }] });

    const answer = await globalThis.fetch('/api/registry');

    expect(await answer.json()).toEqual([{ repository: 'engine' }]);
    expect(fetch).toHaveBeenCalledWith('/api/registry');
  });
});
