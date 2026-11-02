import { render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { ChoiceChip } from './ChoiceChip';

const MOVED = { choice: '2', chosen: { rule: 'order', by: 'moved', condition: 'limit', from: '1' } };

beforeEach(async () => { await i18n.changeLanguage('en'); });

const draw = (session: Parameters<typeof ChoiceChip>[0]['session'], narrow?: boolean) =>
  render(<Tooltip.Provider><ChoiceChip session={session} narrow={narrow} /></Tooltip.Provider>);

describe('the head’s choice chip with its reason (STAFFPOLICY1f)', () => {
  it('says the reason after the number, and holds no ⓘ where the line has room', () => {
    const { container } = draw(MOVED);
    expect(container.querySelector('[data-choice]')).toHaveTextContent('choice 2 · after a limit');
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('keeps the number alone at the narrow width and puts the sentence in an ⓘ', () => {
    const { container } = draw(MOVED, true);
    expect(container.querySelector('[data-choice]')).toHaveTextContent(/^choice 2$/);
    expect(screen.getByRole('button', { name: 'Moved here from choice 1: every account of that choice reached its limit.' })).toBeInTheDocument();
  });

  it('wears no ⓘ where there is no reason, and nothing for a first choice', () => {
    draw({ choice: '2', chosen: null }, true);
    expect(screen.queryByRole('button')).toBeNull();
    const { container } = draw({ choice: '1', chosen: MOVED.chosen }, true);
    expect(container.querySelector('[data-choice]')).toBeNull();
  });

  it('is the same in 中文', async () => {
    await i18n.changeLanguage('zh');
    const { container } = draw({ choice: '2', chosen: { rule: 'weight', by: 'weight' } });
    expect(container.querySelector('[data-choice]')).toHaveTextContent('选择 2 · 按权重');
  });
});
