import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  useDriver, usePlugins, useSayUpdate, useSetCoolOff, useSetNotify, useSetStrikes, useUpdateState,
} from '../shell';
import { Button, Card, CheckField, failure, Icon, PathText, SettingRow } from '../ui';
import type { SettingsDomainProps } from './domains';
import { UpdateSection } from './Update';

/**
 * The home's hint for how the home stands to the account's DAORIS_HOME, which a terminal's daoris reads
 * (D105; LEFT1, then LEFT2). Read from the state's `homeAccount` (`InstallHome.AccountOf`), never from the
 * notice's English, which is the host's sentence passed through as it was said.
 *
 * - `same`: a terminal reads this folder, and the hint says so.
 * - `overridden`: this install's own `data/` replaced another the account names; the notice under the row
 *   says which folder a terminal reads, and the hint points there.
 * - `this-start`: DAORIS_HOME was named for this start alone. D105 respects it and writes no notice, so the
 *   hint itself says a terminal does not read this folder.
 *
 * A shell older than the field says nothing of it, and the hint says what it always said.
 */
const HOME_HINT: Record<string, string> = {
  overridden: 'settings.home.hintOverridden',
  'this-start': 'settings.home.hintThisStart',
};

/**
 * The Driver domain (D75): where this machine's Daoris lives, and the driver's two dials over
 * `driver.json`, the same file `daoris driver` edits (D50). The file is the truth and this is an
 * editor over it: hand-editing keeps working, and neither surface is the only way to say anything.
 *
 * **Shell-only, like every machine domain.** A browser over a keyed remote must never read where a
 * machine lives or syncs, and never re-point it, so the state lives behind the shell's bridge and the
 * service has no route onto it at all. For the wiring, the key goes in and never comes out: what is
 * rendered is the audit prefix a deployment's own `keys list` prints (`WiringSettings`).
 *
 * **Where plugins are looked for** is a fact of the home, so it is a row under it since UX6j (D150 §2.3; the plugins
 * design §5), read-only, with the door to the Plugins place: Settings → Plugins retired there, since nothing about a
 * plugin is set here.
 */
export function DriverDomain({ notify, onGo }: Pick<SettingsDomainProps, 'notify' | 'onGo'>) {
  const { t } = useTranslation();
  // Whether this machine interrupts the person (SURF5b) — the same `driver.json` field
  // `daoris driver notify on|off` edits, which is what makes this a door rather than the door.
  const driver = useDriver();
  // The catalogue the Plugins place reads, asked for its folder alone. An older shell names none, and the row is absent.
  const plugins = usePlugins();
  const pluginsFolder = typeof plugins.data?.folder === 'string' ? plugins.data.folder : null;
  const setNotify = useSetNotify();
  const setStrikesMutation = useSetStrikes();

  // Held as text while it is being typed: a number input mid-edit passes through the empty string
  // and through "0", and writing either straight to the config would park nothing while the person
  // was still reaching for the second digit.
  const [strikes, setStrikes] = useState<string | null>(null);
  // The cool-off's minutes as typed (TOOL4g), held as text for the strikes' reason.
  const setCoolOffMutation = useSetCoolOff();
  const [coolOff, setCoolOff] = useState<string | null>(null);
  // The install's update (UPDATE1b, D139 §6): the query the banner reads, and the same word it says.
  const update = useUpdateState();
  const sayUpdate = useSayUpdate();
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
            why={t(HOME_HINT[driver.data.homeAccount ?? ''] ?? 'settings.home.hint')}
            control={<PathText path={driver.data.home} className="text-small text-ink-soft" />}
          >
            {driver.data.homeNotice && (
              <p className="border-l-[3px] border-accent bg-page/60 px-3.5 py-2 text-body text-ink-soft">
                {driver.data.homeNotice}
              </p>
            )}
            {/* The host this window adopted serves another install's page (case study 4d). A
                standing fact, so a standing line: the toast that carried it fired before this page
                existed to hear it, which is how the second deployment showed a new window, an old
                page, and no surface saying so. */}
            {driver.data.hostNotice && (
              <p className="mt-2 border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
                {driver.data.hostNotice}
              </p>
            )}
          </SettingRow>
        )}
        {pluginsFolder && (
          <SettingRow
            label={t('plugin.folder')}
            hint={t('settings.pluginsFolder.hint')}
            why={t('plugin.body')}
            control={(
              <>
                <PathText path={pluginsFolder} className="text-small text-ink-faint" />
                <Button onClick={() => onGo({ view: 'plugins' })}>
                  <Icon name="plug" size={14} />
                  {t('settings.pluginsFolder.open')}
                </Button>
              </>
            )}
          />
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
            <p className="border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
              {t('settings.strikes.zero')}
            </p>
          )}
        </SettingRow>

        {/* How long an account cools when its agent names no reset (TOOL4g, D125 §2.2): offered where the shell says it,
            since an older shell answers no such field and has no route to write it. */}
        {typeof driver.data?.coolOff === 'number' && (
          <SettingRow
            label={t('settings.cooloff.label')}
            hint={t('settings.cooloff.terminal')}
            why={t('settings.cooloff.body')}
            control={(
              <span className="inline-flex items-center gap-1.5">
                <input
                  type="number"
                  min={1}
                  aria-label={t('settings.cooloff.label')}
                  value={coolOff ?? String(driver.data.coolOff)}
                  onChange={(event) => setCoolOff(event.target.value)}
                  onBlur={() => {
                    // As the strikes: passing through is not a write, and less than a minute is the driver's refusal, so
                    // a value under one, not whole, or the one it holds puts the held value back.
                    const held = driver.data?.coolOff ?? 60;
                    const value = coolOff === null || coolOff.trim() === '' ? held : Number(coolOff);
                    setCoolOff(null);
                    if (!Number.isInteger(value) || value < 1 || value === held) return;
                    setCoolOffMutation.mutate({ minutes: value }, {
                      onSuccess: () => notify(t('settings.cooloff.set', { count: value })),
                      onError,
                    });
                  }}
                  className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink"
                />
                <span className="text-small text-ink-soft">{t('settings.cooloff.minutes')}</span>
              </span>
            )}
          />
        )}
      </Card>

      {/* The install's update (UPDATE1b): the banner, its other door on the screen, is gone once dismissed, and this
          stands. Here because the update is the application's own, as the home above is, and holds the driver's starts
          while it drains. A word the shell refuses is said in its sentence; one it takes is shown by the row itself. */}
      <UpdateSection
        update={update.data}
        busy={sayUpdate.isPending}
        onSay={(mode) => sayUpdate.mutate(mode, { onError })}
      />
    </>
  );
}
