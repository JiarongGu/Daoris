import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { settingOf } from '../DriverDomain';
import { DriverCards } from '../DriverCards';

// The copy bound's row beside the manifest's: the megabytes a folder may hold to be copied for a session working apart, in the
// reader's words, written alone when it changes to a whole number in its bounds.

afterEach(async () => { await i18n.changeLanguage('en'); });

const cards = (onManifest = vi.fn()) => render(
  <Tooltip.Provider>
    <DriverCards
      facts={{ pluginsFolder: null, notify: true, manifestFiles: 250000, manifestMinutes: 5, manifestCopy: 2048 }}
      minutes={{ cooloff: settingOf('cooloff'), quiet: settingOf('quiet'), manifest: settingOf('manifest') }}
      busy={{ access: false }}
      onGo={vi.fn()}
      onNotify={vi.fn()}
      onCoolOff={vi.fn()}
      onManifest={onManifest}
      onCap={vi.fn()}
      onTerminal={vi.fn()}
      onAccess={vi.fn()}
    />
  </Tooltip.Provider>,
);

describe("the copy bound's row", () => {
  it.each([
    ['en', 'A folder is copied for a session working apart up to', 'Most megabytes a folder may hold to be copied'],
    ['zh', '为分开工作的会话复制文件夹的上限', '可复制的文件夹最多多少 MB'],
  ])('in %s, sits beside the manifest bounds and writes the megabytes typed', async (language, label, field) => {
    await i18n.changeLanguage(language);
    const onManifest = vi.fn();
    cards(onManifest);

    expect(screen.getByText(label)).toBeInTheDocument();
    const input = screen.getByRole('spinbutton', { name: field });
    expect(input).toHaveValue(2048);
    fireEvent.change(input, { target: { value: '4096' } });
    fireEvent.blur(input);

    expect(onManifest).toHaveBeenCalledWith({ copy: 4096 });
  });

  it('writes nothing for a value held, under one, or not whole', () => {
    const onManifest = vi.fn();
    cards(onManifest);
    const input = screen.getByRole('spinbutton', { name: 'Most megabytes a folder may hold to be copied' });
    for (const value of ['2048', '0', '1.5', '']) {
      fireEvent.change(input, { target: { value } });
      fireEvent.blur(input);
    }

    expect(onManifest).not.toHaveBeenCalled();
  });
});
