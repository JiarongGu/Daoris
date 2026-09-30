import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ExternalLink, LinkOpener, webAddress } from './links';

/** Every source that renders, which is where a link could be written by hand. */
const sources = import.meta.glob('./**/*.tsx', { eager: true, query: '?raw', import: 'default' }) as Record<string, string>;

/** A hand-written anchor: `<a` followed by a space, a newline or `>`. */
const handWritten =(files: [path: string, source: string][]): string[] =>
  files.filter(([path, source]) => path !== './links.tsx' && /<a[\s>]/.test(source)).map(([path]) => path);

/**
 * 🔴 One place, held (BRW7): a link written by hand is a link the person's choice never reaches, and it
 * would look exactly like one that honours it.
 */
describe('the one place a link opens', () => {
  it('catches a hand-written link — the check itself, sabotaged', () => {
    expect(handWritten([['./work/Row.tsx', '<a href={x} target="_blank">x</a>']])).toEqual(['./work/Row.tsx']);
    expect(handWritten([['./work/Row.tsx', '<a\n  href={x}>x</a>']])).toEqual(['./work/Row.tsx']);
    expect(handWritten([['./work/Row.tsx', '<abbr title="x">x</abbr> <aside />']])).toEqual([]);
  });

  it('is looking at files at all', () => {
    expect(Object.keys(sources).length).toBeGreaterThan(50);
    expect(sources['./links.tsx']).toContain('<a ');
  });

  it('holds: no component but ExternalLink writes a link', () => {
    const rendered = Object.entries(sources).filter(([path]) => !/\.(test|stories)\.tsx$/.test(path));
    expect(handWritten(rendered)).toEqual([]);
  });
});

/**
 * BRW7: the one place a link on the page opens. Where the person chose Daoris's browser, a click on a
 * web page's link goes there through the opener the application provides; everywhere else — a browser
 * with no bridge, the system's choice, an address that is no web page — the link opens as a link
 * always has, outside the window.
 */
describe('webAddress', () => {
  it.each([
    ['https://tickets.example/browse/T-1', 'https://tickets.example/browse/T-1'],
    ['HTTP://Tickets.Example:8080/x?y=1#z', 'http://tickets.example:8080/x?y=1#z'],
    ['http://localhost:5177/api/quests/q1/attachments/abc', 'http://localhost:5177/api/quests/q1/attachments/abc'],
  ])('keeps %s as a web page', (href, expected) => {
    expect(webAddress(href)).toBe(expected);
  });

  it.each([
    [undefined], [null], [''], ['docs/readme.md'], ['#heading'], ['mailto:a@b.example'],
    ['javascript:alert(1)'], ['file:///C:/secrets.txt'], ['https://someone:pw@tickets.example/'],
    ['about:blank'],
  ])('is no web page for %s', (href) => {
    expect(webAddress(href)).toBeNull();
  });
});

describe('ExternalLink', () => {
  const link = () => screen.getByRole('link', { name: 'the ticket' });

  it('opens outside the window, as a link always has, where no opener is given — a browser', () => {
    render(<ExternalLink href="https://tickets.example/T-1">the ticket</ExternalLink>);

    expect(link()).toHaveAttribute('href', 'https://tickets.example/T-1');
    expect(link()).toHaveAttribute('target', '_blank');
    expect(link()).toHaveAttribute('rel', 'noreferrer');
    // Nothing prevented it: the browser's own handling stands.
    expect(fireEvent.click(link())).toBe(true);
  });

  it("hands a web page to Daoris's browser where the application gives an opener, and keeps it off the system's", () => {
    const open = vi.fn();
    render(
      <LinkOpener.Provider value={open}>
        <ExternalLink href="https://Tickets.Example/T-1">the ticket</ExternalLink>
      </LinkOpener.Provider>,
    );

    // Prevented: the system's browser never sees it.
    expect(fireEvent.click(link())).toBe(false);
    expect(open).toHaveBeenCalledWith('https://tickets.example/T-1');
    // Still a real link to read and copy.
    expect(link()).toHaveAttribute('href', 'https://Tickets.Example/T-1');
  });

  it('leaves what is no web page to the link itself, opener or not', () => {
    const open = vi.fn();
    render(
      <LinkOpener.Provider value={open}>
        <ExternalLink href="mailto:someone@site.example">the ticket</ExternalLink>
      </LinkOpener.Provider>,
    );

    expect(fireEvent.click(link())).toBe(true);
    expect(open).not.toHaveBeenCalled();
  });

  /** A sign-in belongs in the person's own browser: never one any process on the machine can drive (D78 §3.4). */
  it("keeps a link that says so on the system's browser, whatever the opener", () => {
    const open = vi.fn();
    render(
      <LinkOpener.Provider value={open}>
        <ExternalLink href="https://accounts.example/login" system>the ticket</ExternalLink>
      </LinkOpener.Provider>,
    );

    expect(fireEvent.click(link())).toBe(true);
    expect(open).not.toHaveBeenCalled();
  });
});
