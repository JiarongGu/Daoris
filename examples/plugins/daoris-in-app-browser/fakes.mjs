import { EventEmitter } from 'node:events';

/** A Playwright page as the scoped view and the isolated context read one. */
export function fakePage(url = 'about:blank', storage = {}) {
  const page = new EventEmitter();
  page.url = () => url;
  page.closed = false;
  page.close = async () => { page.closed = true; page.emit('close'); };
  page.evaluate = async () => Object.entries(storage);
  return page;
}

/** A browser context: its pages, the pages it opens, and the cookies it holds. */
export function fakeContext({ cookies = [], pages = [], browser = null } = {}) {
  const context = new EventEmitter();
  const open = [...pages];
  context.opened = [];
  context.closed = false;
  context.pages = () => [...open];
  context.cookies = async () => cookies;
  context.browser = () => browser;
  context.newPage = async () => {
    const page = fakePage();
    open.push(page);
    context.opened.push(page);
    context.emit('page', page);
    return page;
  };
  context.close = async () => { context.closed = true; };
  context.appear = page => { open.push(page); context.emit('page', page); };
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
