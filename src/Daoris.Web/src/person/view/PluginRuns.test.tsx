import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

// A plugin's press is drawn on the Confirm card from its route's view: what it will run, as its manifest writes it, or every tool an
// approval of its server names, worded from the catalogue.

const code = (text: string) => (_: string, element: Element | null) => element?.tagName === 'CODE' && element.textContent === text;

const ADD: PressView = {
  kind: 'plugin-add',
  facts: { id: 'acme.quiet-hours' },
  command: 'daoris plugin add /checkouts/house-plugins/quiet-hours',
  plugin: {
    id: 'acme.quiet-hours', name: 'Quiet hours', version: '1.0.0',
    command: ['node', '${plugin}/hooks.mjs'], points: ['quest/consider', 'session/ended'],
    harnesses: [{ name: 'acme-agent', command: ['acme-agent', '--acp'] }],
    servers: [{ name: 'browser', command: ['npx', '-y', '@playwright/mcp@latest'] }],
  },
};

const UPDATE: PressView = {
  kind: 'plugin-update',
  facts: { id: 'acme.quiet-hours' },
  command: 'daoris plugin update acme.quiet-hours --yes',
  plugin: {
    id: 'acme.quiet-hours', name: 'Quiet hours', version: '1.1.0', points: [], harnesses: [], servers: [],
    changes: [{ what: 'version', was: '1.0.0', now: '1.1.0' }],
    needs: ['an identity: Settings → Git.'],
  },
};

const APPROVE: PressView = {
  kind: 'plugin-approve',
  facts: { id: 'in-app-browser', server: 'browser' },
  command: 'daoris plugins approve in-app-browser browser --writes browser_click',
  grant: {
    plugin: 'in-app-browser', name: 'In-app browser', server: 'browser',
    reads: ['browser_navigate', 'browser_snapshot'], writes: ['browser_click', 'browser_type'], accepted: ['browser_click'],
  },
};

describe("a plugin's press on the Confirm card", () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('says an add and what will run, its command as the manifest writes it, with the command the press is', () => {
    render(<PressViewLine view={ADD} />);

    expect(screen.getByText(/^Adds plugin acme.quiet-hours from the folder below/)).toBeInTheDocument();
    const runs = within(screen.getByRole('list', { name: 'what the plugin runs' }));
    expect(runs.getByText(code('node ${plugin}/hooks.mjs'))).toBeInTheDocument();
    expect(runs.getByText(code('session/ended'))).toBeInTheDocument();
    expect(runs.getByText(code('acme-agent --acp'))).toBeInTheDocument();
    expect(runs.getByText(code('npx -y @playwright/mcp@latest'))).toBeInTheDocument();
    expect(screen.getByText(ADD.command)).toBeInTheDocument();
  });

  it('says what an update changes and what it needs', () => {
    render(<PressViewLine view={UPDATE} />);

    const runs = within(screen.getByRole('list', { name: 'what the plugin runs' }));
    expect(runs.getByText(code('1.0.0'))).toBeInTheDocument();
    expect(runs.getByText(code('1.1.0'))).toBeInTheDocument();
    expect(runs.getByText(/^needs:/)).toBeInTheDocument();
  });

  it('names every tool an approval hands Ask Daoris, reads and writes apart, and what a withdrawal takes back', () => {
    render(<PressViewLine view={APPROVE} />);

    const tools = screen.getByRole('list', { name: 'what Ask Daoris would be handed' });
    expect(tools).toHaveTextContent('server browser of plugin in-app-browser (In-app browser)');
    expect(tools).toHaveTextContent('reads with browser_navigate, browser_snapshot');
    expect(tools).toHaveTextContent('writes outside Daoris with browser_click, as you, accepted');
    expect(tools).toHaveTextContent('writes outside Daoris with browser_type, not accepted: Ask Daoris is refused it');
    cleanup();

    render(<PressViewLine view={{ ...APPROVE, kind: 'plugin-withdraw', grant: { ...APPROVE.grant!, withdraw: true } }} />);
    expect(screen.getByText(/^Withdraws Ask Daoris's approval of server browser/)).toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'what Ask Daoris would no longer be handed' })).toHaveTextContent('reads with browser_navigate');
  });

  it('words them in 中文, the commands as written', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={ADD} />);
    expect(screen.getByText(/^从下面的文件夹添加插件 acme.quiet-hours/)).toBeInTheDocument();
    expect(within(screen.getByRole('list', { name: '插件会运行什么' })).getByText(code('node ${plugin}/hooks.mjs'))).toBeInTheDocument();
    cleanup();

    render(<PressViewLine view={APPROVE} />);
    expect(screen.getByRole('list', { name: '问道衍会拿到什么' })).toHaveTextContent('读取：browser_navigate, browser_snapshot');
  });
});
