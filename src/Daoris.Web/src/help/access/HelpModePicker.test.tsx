import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { HelpMode } from '../../bridge/help';
import i18n from '../../locales/i18n';
import { type Answered, Refused } from '../../work/InlineConfirm';
import { FullAccessAsk, HelpModeChip, useModeChoice } from './HelpModePicker';

// HELPMODE1b (D183 point 2; the modes design §1, §5): Ask Daoris's access, chosen from its chip. The chip shows the mode the
// conversation in view runs under, a switch that waits for its next start, and full access in the warning colour; restricted is
// set at once, and full access asks once, inline, naming the servers it would add and when it takes effect. ACCESSASK1 (the UI/UX
// design §3.4): the ask is the one `InlineConfirm`, open until the driver answers, and a refusal is said where it was pressed.

/** The chip and its confirmation as their holders compose them: the panel's strip, and Settings' row. */
function Picker({ selected, running = null, adds, onChoose }: {
  selected: HelpMode; running?: HelpMode | null; adds?: string[] | null; onChoose: (mode: HelpMode, answered: Answered) => void;
}) {
  const choice = useModeChoice(selected, onChoose);
  return (
    <Tooltip.Provider>
      {choice.confirming && <FullAccessAsk adds={adds} onEnable={choice.enable} onClose={choice.cancel} />}
      <HelpModeChip selected={selected} running={running} onPick={choice.pick} />
      {choice.refusal && <Refused sentence={choice.refusal} />}
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

    expect(onChoose).toHaveBeenCalledWith('restricted', expect.anything());
    expect(screen.queryByRole('group', { name: i18n.t('help.mode.confirm.label') })).toBeNull();
  });

  it('says a switch back the driver refused under the chip, until the next pick', async () => {
    await i18n.changeLanguage(language);
    const onChoose = vi.fn((_mode: HelpMode, answered: Answered) => answered.refused('The driver could not write its settings.'));
    render(<Picker selected="full" running="full" onChoose={onChoose} />);

    await pick('restricted');

    expect(await screen.findByRole('alert')).toHaveTextContent('The driver could not write its settings.');
    await pick('full');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('asks once before full access, naming the servers it would add and when it takes effect', async () => {
    await i18n.changeLanguage(language);
    const onChoose = vi.fn();
    render(<Picker selected="restricted" onChoose={onChoose} adds={['browser', 'tracker']} />);

    await pick('full');

    // Nothing is set by the pick: the confirmation asks first, and its sentence takes the focus.
    expect(onChoose).not.toHaveBeenCalled();
    const asks = screen.getByRole('group', { name: i18n.t('help.mode.confirm.label') });
    await waitFor(() => expect(asks).toContainElement(document.activeElement as HTMLElement));
    expect(within(asks).getByText(i18n.t('help.mode.confirm.says'))).toBeInTheDocument();
    expect(within(asks).getByText(i18n.t('help.mode.confirm.adds'))).toBeInTheDocument();
    expect(within(asks).getByText('browser')).toBeInTheDocument();
    expect(within(asks).getByText('tracker')).toBeInTheDocument();
    expect(within(asks).getByText(i18n.t('help.mode.confirm.when'))).toBeInTheDocument();
    // The press is reached by the keyboard from the sentence.
    await userEvent.tab();
    expect(screen.getByRole('button', { name: i18n.t('help.mode.confirm.enable') })).toHaveFocus();
    await userEvent.keyboard('{Enter}');

    expect(onChoose).toHaveBeenCalledWith('full', expect.anything());
    // It waits for the driver's answer, open, and says so; only `done` puts it down.
    expect(screen.getByRole('group', { name: i18n.t('help.mode.confirm.label') })).toBe(asks);
    expect(within(asks).getByRole('status')).toHaveTextContent(i18n.t('work.confirm.pending'));
    act(() => onChoose.mock.calls[0]![1].done());
    expect(screen.queryByRole('group', { name: i18n.t('help.mode.confirm.label') })).toBeNull();
  });

  it('says a refusal of full access inside its ask, which stays open to be pressed again, in the warning tone', async () => {
    await i18n.changeLanguage(language);
    const onChoose = vi.fn((_mode: HelpMode, answered: Answered) => answered.refused('The driver could not write its settings.'));
    render(<Picker selected="restricted" onChoose={onChoose} adds={[]} />);

    await pick('full');
    const asks = screen.getByRole('group', { name: i18n.t('help.mode.confirm.label') });
    expect(asks.className).toContain('border-l-warn');
    const enable = within(asks).getByRole('button', { name: i18n.t('help.mode.confirm.enable') });
    expect(enable.className).toContain('border-warn');
    await userEvent.click(enable);

    expect(within(asks).getByRole('alert')).toHaveTextContent('The driver could not write its settings.');
    expect(enable).toBeEnabled();
    // Never mind puts it down and gives the focus back to the chip.
    await userEvent.click(within(asks).getByRole('button', { name: i18n.t('common.cancel') }));
    expect(screen.queryByRole('group', { name: i18n.t('help.mode.confirm.label') })).toBeNull();
    await waitFor(() => expect(chip(i18n.t('help.mode.restricted'))).toHaveFocus());
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
