import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import { type Confirmation, Confirmations } from './Confirmations';
import { standingSaid } from './standingSaid';

// CHANNELJOIN1c (D244 point 10): a join from a place the system would not tell is asked, and its card says so beside the asker.

const JOIN: Confirmation = {
  source: 'pipe', id: 'j1', module: 'DAORIS.DRIVER', route: 'CHANNEL_JOIN',
  fields: [{ name: '/ask', type: 'string', value: 'a1b2c3' }, { name: '/agent', type: 'string', value: 'codex' }],
  asker: { process: 'node', pid: 4120 }, asked: new Date(Date.now() - 60_000).toISOString(), waitedOn: true,
};

afterEach(async () => {
  await i18n.changeLanguage('en');
});

const draw = (waiting: Confirmation) => render(
  <Confirmations waiting={[waiting]} onAnswer={vi.fn(async () => ({ answered: true as const }))} notify={vi.fn()} names={{ session: () => null }} />,
);

describe('the card’s standing words', () => {
  it('says where the terminal stands only for the one place the system would not tell', () => {
    const t = i18n.t.bind(i18n);
    expect(standingSaid(t, 'untold')).toMatch(/could not tell whether this terminal runs inside an agent/);
    expect(standingSaid(t, 'running-here')).toBeNull();
    expect(standingSaid(t, null)).toBeNull();
    expect(standingSaid(t, undefined)).toBeNull();
  });

  it('draws the words beside the asker on an untold card, and none on a card told outside', () => {
    const { container, unmount } = draw({ ...JOIN, standing: 'untold' });
    expect(container.querySelector('[data-standing]')?.textContent).toMatch(/Confirm only if you started this agent yourself/);
    unmount();

    const { container: told } = draw({ ...JOIN, standing: null });
    expect(told.querySelector('[data-standing]')).toBeNull();
    expect(screen.getByText(/Sent by node, process 4120/)).toBeTruthy();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    const { container } = draw({ ...JOIN, standing: 'untold' });
    expect(container.querySelector('[data-standing]')?.textContent).toMatch(/无法判断这个终端是否运行在它自己启动的智能体里/);
  });
});
