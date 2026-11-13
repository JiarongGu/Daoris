import { EventEmitter } from 'node:events';

let targets = 0;

/** A Playwright page as the scoped view and the isolated context read one. */
export function fakePage(url = 'about:blank', storage = {}) {
  const page = new EventEmitter();
  page.targetId = `target-${++targets}`;
  page.url = () => url;
  page.closed = false;
  page.close = async () => { page.closed = true; page.emit('close'); };
  page.evaluate = async () => Object.entries(storage);
  return page;
}

/**
 * A browser context: its pages, and a browser whose `Target.createTarget` makes a page in it, announced after `attachDelay` ms
 * (the host's relay may answer before it announces the tab), unless `browser` is given.
 */
export function fakeContext({ cookies = [], pages = [], browser = null, attachDelay = 0 } = {}) {
  const context = new EventEmitter();
  const open = [...pages];
  context.closed = false;
  context.pages = () => [...open];
  context.cookies = async () => cookies;
  context.appear = page => { open.push(page); context.emit('page', page); };
  context.newCDPSession = async page => ({
    send: async method => {
      if (method !== 'Target.getTargetInfo') throw new Error(`unexpected ${method}`);
      return { targetInfo: { targetId: page.targetId } };
    },
    detach: async () => {},
  });
  context.close = async () => { context.closed = true; };
  const own = browser ?? {
    newBrowserCDPSession: async () => ({
      send: async method => {
        if (method !== 'Target.createTarget') throw new Error(`unexpected ${method}`);
        const page = fakePage();
        context.created = [...(context.created ?? []), page];
        if (attachDelay) setTimeout(() => context.appear(page), attachDelay);
        else context.appear(page);
        return { targetId: page.targetId };
      },
      detach: async () => {},
    }),
  };
  context.browser = () => own;
  return context;
}

/** A browser holding the person's context first, and the contexts it makes. */
export function fakeBrowser(person) {
  const browser = { made: [] };
  browser.contexts = () => [person, ...browser.made];
  browser.newContext = async options => {
    const own = fakeContext({ browser });
    own.options = options;
    browser.made.push(own);
    return own;
  };
  return browser;
}
