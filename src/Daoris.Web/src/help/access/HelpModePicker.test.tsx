import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { HelpMode } from '../../bridge/help';
import i18n from '../../locales/i18n';
import { FullAccessConfirm, HelpModeChip, useModeChoice } from './HelpModePicker';

// HELPMODE1b (D183 point 2; the modes design §1, §5): Ask Daoris's access, chosen from its chip. The chip shows the mode the
// conversation in view runs under, a switch that waits for its next start, and full access in the warning colour; restricted is
// set at once, and full access asks once, inline, naming the servers it would add and when it takes effect.

/** The chip and its confirmation as their holders compose them: the panel's strip, and Settings' row. */
function Picker({ selected, running = null, adds, onChoose }: {
  selected: HelpMode; running?: HelpMode | null; adds?: string[] | null; onChoose: (mode: HelpMode) => void;
}) {
  const choice = useModeChoice(selected, onChoose);
  return (
    <Tooltip.Provider>
      <HelpModeChip selected={selected} running={running} onPick={choice.pick} />
      {choice.confirming && <FullAccessConfirm adds={adds} onEnable={choice.enable} onCancel={choice.cancel} />}
    </Tooltip.Provider>
  );
}

const chip = (mode: string) => screen.getByRole('button', { name: i18n.t('help.mode.trigger', { mode }) });

/** The chip's menu, opened by the keyboard: Radix opens on a key in a page with no pointer. */
async function pick(mode: HelpMode) {
  screen.getByRole('button', { name: (name) => name.startsWith(i18n.t('help.mode.trigger', { mode: '' })) }).focus();
  await userEvent.keyboard('{Enter}');
  await userEvent.click(await screen.findByRole('menuitemradio', { name: i18n.t(`help.mode.option.${mode}`) }));
}

describe.each(['en', 'zh'])('Ask Daoris’s access chip in %s', (language) => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('shows the mode the conversation in view runs under, and the one chosen before any start', async () => {
    await i18n.changeLanguage(language);
    render(<Picker selected="restricted" running="restricted" onChoose={vi.fn()} />);
    expect(chip(i18n.t('help.mode.restricted'))).toHaveTextContent(i18n.t('help.mode.restricted'));
    expect(chip(i18n.t('help.mode.restricted')).className).not.toContain('border-warn');

    cleanup();
    render(<Picker selected="full" onChoose={vi.fn()} />);
    // Full access wears the warning colour.
    expect(chip(i18n.t('help.mode.full'))).toHaveTextContent(i18n.t('help.mode.full'));
    expect(chip(i18n.t('help.mode.full')).className).toContain('border-warn');
  });

  it('says a switch that waits for the conversation’s next turn, both ways (ACCESSSWAP1)', async () => {
    await i18n.changeLanguage(language);
    render(<Picker selected="full" running="restricted" onChoose={vi.fn()} />);
    expect(chip(i18n.t('help.mode.toFull'))).toHaveTextContent(i18n.t('help.mode.toFull'));
    expect(i18n.t('help.mode.toFull')).toBe(language === 'en' ? 'Restricted · full access next turn' : '受限 · 下一轮起完全访问');

    cleanup();
    render(<Picker selected="restricted" running="full" onChoose={vi.fn()} />);
    expect(chip(i18n.t('help.mode.toRestricted'))).toHaveTextContent(i18n.t('help.mode.toRestricted'));
  });

  it('sets restricted at once, with no confirmation', async () => {
    await i18n.changeLanguage(language);
    const onChoose = vi.fn();
    render(<Picker selected="full" running="full" onChoose={onChoose} />);

    await pick('restricted');

    expect(onChoose).toHaveBeenCalledWith('restricted');
    expect(screen.queryByRole('group', { name: i18n.t('help.mode.confirm.label') })).toBeNull();
  });

  it('asks once before full access, naming the servers it would add and when it takes effect', async () => {
    await i18n.changeLanguage(language);
    const onChoose = vi.fn();
    render(<Picker selected="restricted" onChoose={onChoose} adds={['browser', 'tracker']} />);

    await pick('full');

    // Nothing is set by the pick: the confirmation asks first, and takes the focus.
    expect(onChoose).not.toHaveBeenCalled();
    const asks = screen.getByRole('group', { name: i18n.t('help.mode.confirm.label') });
    await waitFor(() => expect(asks).toHaveFocus());
    expect(within(asks).getByText(i18n.t('help.mode.confirm.says'))).toBeInTheDocument();
    expect(within(asks).getByText(i18n.t('help.mode.confirm.adds'))).toBeInTheDocument();
    expect(within(asks).getByText('browser')).toBeInTheDocument();
    expect(within(asks).getByText('tracker')).toBeInTheDocument();
    expect(within(asks).getByText(i18n.t('help.mode.confirm.when'))).toBeInTheDocument();
    // The press is reached by the keyboard from the sentence.
    await userEvent.tab();
    expect(screen.getByRole('button', { name: i18n.t('help.mode.confirm.enable') })).toHaveFocus();
    await userEvent.keyboard('{Enter}');

    expect(onChoose).toHaveBeenCalledWith('full');
    expect(screen.queryByRole('group', { name: i18n.t('help.mode.confirm.label') })).toBeNull();
  });

  it('sets nothing on Never mind, and says when no server is withheld or the servers are still being read', async () => {
    await i18n.changeLanguage(language);
    const onChoose = vi.fn();
    render(<Picker selected="restricted" onChoose={onChoose} adds={[]} />);

    await pick('full');
    expect(screen.getByText(i18n.t('help.mode.confirm.addsNone'))).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('common.cancel') }));

    expect(onChoose).not.toHaveBeenCalled();
    expect(screen.queryByRole('group', { name: i18n.t('help.mode.confirm.label') })).toBeNull();

    cleanup();
    render(<Picker selected="restricted" onChoose={onChoose} adds={null} />);
    await pick('full');
    expect(screen.getByText(i18n.t('help.mode.confirm.addsReading'))).toBeInTheDocument();
  });

  // HELPMODE1c1 (design §5): the menu's door to restricted mode's configured access, offered only where a holder gives it one.
  it('offers Configure restricted access… only where its holder gives it a door, and presses that', async () => {
    await i18n.changeLanguage(language);
    const onConfigure = vi.fn();
    const openMenu = async () => {
      screen.getByRole('button', { name: (name) => name.startsWith(i18n.t('help.mode.trigger', { mode: '' })) }).focus();
      await userEvent.keyboard('{Enter}');
      await screen.findByRole('menuitemradio', { name: i18n.t('help.mode.option.restricted') });
    };
    render(<Tooltip.Provider><HelpModeChip selected="restricted" onPick={vi.fn()} onConfigure={onConfigure} /></Tooltip.Provider>);

    await openMenu();
    await userEvent.click(screen.getByRole('menuitem', { name: i18n.t('help.mode.configure') }));
    expect(onConfigure).toHaveBeenCalledOnce();

    cleanup();
    render(<Picker selected="restricted" onChoose={vi.fn()} />);
    await openMenu();
    expect(screen.queryByRole('menuitem', { name: i18n.t('help.mode.configure') })).toBeNull();
  });

  it('asks nothing when the mode chosen is picked again', async () => {
    await i18n.changeLanguage(language);
    const onChoose = vi.fn();
    render(<Picker selected="full" onChoose={onChoose} />);

    await pick('full');

    expect(onChoose).not.toHaveBeenCalled();
    expect(screen.queryByRole('group', { name: i18n.t('help.mode.confirm.label') })).toBeNull();
  });
});
