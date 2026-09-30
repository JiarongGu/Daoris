import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDriver, useSetNotify, useSetStrikes } from '../shell';
import { Card, CheckField, failure, type Notify, PathText, SettingRow } from '../ui';

/**
 * Whether the host's notice says this start overrode the home the account names (D105). A terminal's
 * daoris then reads that other folder, and the notice says so, so the home's hint stops claiming a
 * terminal reads this one (LEFT1). Read from the host's own sentence (`InstallHome.Establish`), since
 * the state carries no other sign: `InstallHomeTests` holds "left as it is" in the notice exactly when
 * a start overrode, and `DriverDomain.test.tsx` holds this against that sentence.
 */
const overrodeHome = (notice: string | null | undefined) => notice?.includes('left as it is') ?? false;

/**
 * The Driver domain (D75): where this machine's Daoris lives, and the driver's two dials over
 * `driver.json`, the same file `daoris driver` edits (D50). The file is the truth and this is an
 * editor over it: hand-editing keeps working, and neither surface is the only way to say anything.
 *
 * **Shell-only, like every machine domain.** A browser over a keyed remote must never read where a
 * machine lives or syncs, and never re-point it, so the state lives behind the shell's bridge and the
 * service has no route onto it at all. For the wiring, the key goes in and never comes out: what is
 * rendered is the audit prefix a deployment's own `keys list` prints (`WiringSettings`).
 */
export function DriverDomain({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  // Whether this machine interrupts the person (SURF5b) — the same `driver.json` field
  // `daoris driver notify on|off` edits, which is what makes this a door rather than the door.
  const driver = useDriver();
  const setNotify = useSetNotify();
  const setStrikesMutation = useSetStrikes();

  // Held as text while it is being typed: a number input mid-edit passes through the empty string
  // and through "0", and writing either straight to the config would park nothing while the person
  // was still reaching for the second digit.
  const [strikes, setStrikes] = useState<string | null>(null);
  const onError = failure(notify);

  return (
    <>
      {/* 🔴 A setting is a ROW. Every card here used to open with a
          paragraph and put its one control beneath it, so the first checkbox sat 580px below the
          title and the next dial a screen further down. `SettingRow` carries the shape now — the
          label leads, the hint is one line, the control is at the right, the paragraph is on the
          glyph — and the cards are the sections of one settings page rather than five essays. */}

      {/* The driver's two dials, in one card: that one asks to be TOLD when a driver stops, this one
          bounds what it spends before anyone is told (D58). The notification switch leads because it
          is the setting a person is most likely to have come here to change. */}
      <Card>
        {/* Where this machine's Daoris lives (D63): the card's first row since D75, where it had
            floated above the card with no label once the page lost its "This machine" heading. The
            notice is the shell's own sentence about what the start did, carried in the state rather
            than only raised: a toast raised before the page subscribed reached nobody. */}
        {driver.data?.home && (
          <SettingRow
            label={t('settings.home.label')}
            why={t(overrodeHome(driver.data.homeNotice) ? 'settings.home.hintOverridden' : 'settings.home.hint')}
            control={<PathText path={driver.data.home} className="text-small text-ink-soft" />}
          >
            {driver.data.homeNotice && (
              <p className="max-w-prose border-l-[3px] border-accent bg-page/60 px-3.5 py-2 text-body text-ink-soft">
                {driver.data.homeNotice}
              </p>
            )}
            {/* The host this window adopted serves another install's page (case study 4d). A
                standing fact, so a standing line: the toast that carried it fired before this page
                existed to hear it, which is how the second deployment showed a new window, an old
                page, and no surface saying so. */}
            {driver.data.hostNotice && (
              <p className="mt-2 max-w-prose border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
                {driver.data.hostNotice}
              </p>
            )}
          </SettingRow>
        )}
        <SettingRow
          label={t('settings.notify.label')}
          hint={t('settings.notify.terminal')}
          why={t('settings.notify.body')}
          control={(
            <CheckField
              hideLabel
              checked={driver.data?.notify ?? true}
              onChange={(on) => setNotify.mutate({ notify: on }, {
                onSuccess: () => notify(t(on ? 'settings.notify.on' : 'settings.notify.off')),
                onError,
              })}
              label={t('settings.notify.label')}
            />
          )}
        />

        <SettingRow
          label={t('settings.strikes.label')}
          hint={t('settings.strikes.terminal')}
          why={t('settings.strikes.body')}
          control={(
            /* Sized to what it HOLDS, which is one or two digits. A full-width box for a number reads
               as a text field somebody forgot. */
            <input
              type="number"
              min={0}
              max={99}
              aria-label={t('settings.strikes.label')}
              value={strikes ?? String(driver.data?.strikes ?? 3)}
              onChange={(event) => setStrikes(event.target.value)}
              onBlur={() => {
                // 🔴 Passing through is not a write (REV3): `Number(null)` and `Number('')` are both 0,
                // the one value that means "never park". Nothing typed, a cleared box, or the value it
                // already holds all leave the config alone and put the held value back.
                const held = driver.data?.strikes ?? 3;
                const value = strikes === null || strikes.trim() === '' ? held : Number(strikes);
                if (!Number.isInteger(value) || value < 0 || value === held) {
                  setStrikes(null);
                  return;
                }

                setStrikesMutation.mutate({ strikes: value }, {
                  onSuccess: () => notify(t(value === 0 ? 'settings.strikes.never' : 'settings.strikes.set', { count: value })),
                  onError,
                });
              }}
              className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink"
            />
          )}
        >
          {/* Stated where the zero is, because zero is the one value whose consequence is invisible. */}
          {Number(strikes ?? driver.data?.strikes ?? 3) === 0 && (
            <p className="max-w-prose border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
              {t('settings.strikes.zero')}
            </p>
          )}
        </SettingRow>
      </Card>
    </>
  );
}
