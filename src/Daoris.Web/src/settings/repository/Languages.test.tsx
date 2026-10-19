import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { LanguageChoice, type LanguageOption } from './Languages';

// LANG1c (D142 point 7): a session language's field, named from the driver's own table, set as `daoris driver language`
// sets it from a terminal (D50). The window's language is Appearance's, and nothing here touches it. One field, on a
// workspace's Setup and a repository's (UX6f, UX6g; D150 §4.2, §4.3), so its behaviour is held here.

const TABLE: LanguageOption[] = [
  { code: 'en', name: 'English' },
  { code: 'zh', name: 'Simplified Chinese (简体中文)' },
];

const draw = (set: string | undefined, inherited?: string, onChoose = vi.fn()) => {
  render(
    <Tooltip.Provider>
      <LanguageChoice label="The session language for aurora" set={set} inherited={inherited} table={TABLE} onChoose={onChoose} />
    </Tooltip.Provider>,
  );
  return onChoose;
};

const choose = async (option: string) => {
  const user = userEvent.setup();
  screen.getByRole('combobox', { name: 'The session language for aurora' }).focus();
  await user.keyboard('{Enter}');
  await user.click(await screen.findByRole('option', { name: option }));
};

describe("a session language's field", () => {
  it('shows what is SET, or that none is', () => {
    draw('zh');
    expect(screen.getByRole('combobox', { name: 'The session language for aurora' })).toHaveTextContent('Simplified Chinese (简体中文)');
  });

  it("offers the driver's table by the names a session is told, sets one, and clears it", async () => {
    const onChoose = draw('zh');

    await choose('English');
    expect(onChoose).toHaveBeenLastCalledWith('en');
    await choose('Not set');
    expect(onChoose).toHaveBeenLastCalledWith(undefined);
  });

  /** A repository's unset choice names what stands without it: its workspace's language. */
  it('names what stands without it, and sends nothing for what is already chosen', async () => {
    const onChoose = draw(undefined, 'English');

    expect(screen.getByRole('combobox', { name: 'The session language for aurora' })).toHaveTextContent("Its workspace's: English");
    await choose("Its workspace's: English");
    expect(onChoose).not.toHaveBeenCalled();
  });
});
