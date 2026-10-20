import { type ComponentProps, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, CardStack, CheckField, Icon, PathText, SectionTitle, SettingList, SettingRow, WhyGlyph } from '../design/ui';
import type { StarterDoor } from '../help/starters';
import type { UpdateState } from '../update/UpdateBanner';
import type { Answered } from '../work/InlineConfirm';
import type { PluginShown } from '../plugins/catalog';
import { PluginSettings } from './PluginSettings';
import { type AccessEdit, type AccessLevel, AccessRows, resolveAccess } from './repository/Access';
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

/** What the Driver domain shows, as the shell's state answers it; a field a shell does not answer is absent. */
export type DriverFacts = {
  /** Where this machine's Daoris lives; none while the shell has not answered. */
  home?: string | null;
  homeAccount?: string | null;
  homeNotice?: string | null;
  hostNotice?: string | null;
  /** Whether this shell answers This Daoris's read (WIRING1c): the home row is its door then, and the older row otherwise. */
  placeAnswers: boolean;
  /** Where plugins are looked for (UX6j); none on an older shell, and the row is absent. */
  pluginsFolder: string | null;
  /** The installed plugins, for the Plugins section (PLUGSET1); none on an older shell or before it answers, and the section is absent. */
  plugins?: readonly PluginShown[] | null;
  notify: boolean;
  strikes: number;
  /** Minutes an account cools when its agent names no reset (TOOL4g); absent on a shell older than the field. */
  coolOff?: number | null;
  /** How many sessions Daoris starts at once here (CAPSET1); absent on a shell older than the route. */
  cap?: number | null;
  /** This machine's sessions' access (AUTONOMY1d); absent on a shell older than it. */
  machineAccess?: AccessLevel | null;
  /** The install's update (UPDATE1b). */
  update?: UpdateState;
};

/**
 * The Driver domain's cards (D75), drawn from the shell's facts: where this machine's Daoris lives, the driver's dials
 * over `driver.json`, this machine's sessions' access and the install's update. `DriverDomain` holds the hooks and hands
 * the facts here; a story hands a fixture, so the domain is drawn at the frame's widths (the UI/UX design R6, UIFIT1).
 * A molecule: every state by props, every press out.
 */
export function DriverCards({ facts, busy, onGo, onNotify, onStrikes, onCoolOff, onCap, onAccess, onSayUpdate }: {
  facts: DriverFacts;
  busy: { access: boolean; update: boolean };
  onGo: (door: StarterDoor) => void;
  onNotify: (on: boolean) => void;
  /** A strikes count the person typed and left: whole, zero or more, and not the one held. */
  onStrikes: (count: number) => void;
  /** A cool-off in minutes the person typed and left: whole, one or more, and not the one held. */
  onCoolOff: (minutes: number) => void;
  /** A cap the person typed and left: whole, one or more, and not the one held (CAPSET1). */
  onCap: (cap: number) => void;
  onAccess: (edit: AccessEdit, answered?: Answered) => void;
  onSayUpdate: ComponentProps<typeof UpdateSection>['onSay'];
}) {
  const { t } = useTranslation();
  // Held as text while it is being typed: a number input mid-edit passes through the empty string
  // and through "0", and writing either straight to the config would park nothing while the person
  // was still reaching for the second digit.
  const [strikes, setStrikes] = useState<string | null>(null);
  // The cool-off's minutes as typed (TOOL4g), held as text for the strikes' reason.
  const [coolOff, setCoolOff] = useState<string | null>(null);
  // The cap as typed (CAPSET1), held as text for the same reason.
  const [cap, setCap] = useState<string | null>(null);

  return (
    <CardStack>
      {/* 🔴 A setting is a ROW. Every card here used to open with a
          paragraph and put its one control beneath it, so the first checkbox sat 580px below the
          title and the next dial a screen further down. `SettingRow` carries the shape now — the
          label leads, the hint is one line, the value beside its words on the card's track (R1), the paragraph on the
          glyph — and the cards are the sections of one settings page rather than five essays. */}

      {/* The driver's two dials, in one card: that one asks to be TOLD when a driver stops, this one
          bounds what it spends before anyone is told (D58). The notification switch leads because it
          is the setting a person is most likely to have come here to change. */}
      <Card>
        <SettingList>
          {/* Where this machine's Daoris lives (D63): the card's first row since D75, where it had
              floated above the card with no label once the page lost its "This machine" heading. The
              notice is the shell's own sentence about what the start did, carried in the state rather
              than only raised: a toast raised before the page subscribed reached nobody. */}
          {/* The home's page is This Daoris since WIRING1c, and this row is its door (the this-Daoris design §2). A shell that
              answers no wiring is older than that page, so the row stays as it was: path, hint and the two notices. */}
          {facts.home && facts.placeAnswers && (
            <SettingRow
              label={t('settings.home.label')}
              hint={t('settings.daoris.door')}
              control={(
                <>
                  <PathText path={facts.home} className="text-small text-ink-soft" />
                  <Button onClick={() => onGo({ view: 'settings', section: 'wiring' })}>{t('settings.daoris.button.here')}</Button>
                </>
              )}
            />
          )}
          {facts.home && !facts.placeAnswers && (
            <SettingRow
              label={t('settings.home.label')}
              why={t(HOME_HINT[facts.homeAccount ?? ''] ?? 'settings.home.hint')}
              control={<PathText path={facts.home} className="text-small text-ink-soft" />}
            >
              {facts.homeNotice && (
                <p className="border-l-[3px] border-accent bg-page/60 px-3.5 py-2 text-body text-ink-soft">
                  {facts.homeNotice}
                </p>
              )}
              {/* The host this window adopted serves another install's page (case study 4d). A
                  standing fact, so a standing line: the toast that carried it fired before this page
                  existed to hear it, which is how the second deployment showed a new window, an old
                  page, and no surface saying so. */}
              {facts.hostNotice && (
                <p className="mt-2 border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
                  {facts.hostNotice}
                </p>
              )}
            </SettingRow>
          )}
          {facts.pluginsFolder && (
            <SettingRow
              label={t('plugin.folder')}
              hint={t('settings.pluginsFolder.hint')}
              why={t('plugin.body')}
              control={(
                <>
                  <PathText path={facts.pluginsFolder} className="text-small text-ink-faint" />
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
            control={<CheckField hideLabel checked={facts.notify} onChange={onNotify} label={t('settings.notify.label')} />}
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
                value={strikes ?? String(facts.strikes)}
                onChange={(event) => setStrikes(event.target.value)}
                onBlur={() => {
                  // 🔴 Passing through is not a write (REV3): `Number(null)` and `Number('')` are both 0,
                  // the one value that means "never park". Nothing typed, a cleared box, or the value it
                  // already holds all leave the config alone and put the held value back.
                  const held = facts.strikes;
                  const value = strikes === null || strikes.trim() === '' ? held : Number(strikes);
                  if (!Number.isInteger(value) || value < 0 || value === held) {
                    setStrikes(null);
                    return;
                  }
                  onStrikes(value);
                }}
                className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink"
              />
            )}
          >
            {/* Stated where the zero is, because zero is the one value whose consequence is invisible. */}
            {Number(strikes ?? facts.strikes) === 0 && (
              <p className="border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
                {t('settings.strikes.zero')}
              </p>
            )}
          </SettingRow>

          {/* How long an account cools when its agent names no reset (TOOL4g, D125 §2.2): offered where the shell says it,
              since an older shell answers no such field and has no route to write it. */}
          {typeof facts.coolOff === 'number' && (
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
                    value={coolOff ?? String(facts.coolOff)}
                    onChange={(event) => setCoolOff(event.target.value)}
                    onBlur={() => {
                      // As the strikes: passing through is not a write, and less than a minute is the driver's refusal, so
                      // a value under one, not whole, or the one it holds puts the held value back.
                      const held = facts.coolOff ?? 60;
                      const value = coolOff === null || coolOff.trim() === '' ? held : Number(coolOff);
                      setCoolOff(null);
                      if (!Number.isInteger(value) || value < 1 || value === held) return;
                      onCoolOff(value);
                    }}
                    className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink"
                  />
                  <span className="text-small text-ink-soft">{t('settings.cooloff.minutes')}</span>
                </span>
              )}
            />
          )}

          {/* How many sessions Daoris starts at once (CAPSET1, D130's CAPCOUNT1 note): the person's one dial for the machine,
              never per account. Offered where the shell answers the field, as the cool-off is. */}
          {typeof facts.cap === 'number' && (
            <SettingRow
              label={t('settings.cap.label')}
              hint={t('settings.cap.terminal')}
              why={t('settings.cap.body')}
              control={(
                <input
                  type="number"
                  min={1}
                  aria-label={t('settings.cap.label')}
                  value={cap ?? String(facts.cap)}
                  onChange={(event) => setCap(event.target.value)}
                  onBlur={() => {
                    // As the cool-off: passing through is not a write, and under one is the driver's refusal, so a value
                    // under one, not whole, or the one it holds puts the held value back.
                    const held = facts.cap ?? 2;
                    const value = cap === null || cap.trim() === '' ? held : Number(cap);
                    setCap(null);
                    if (!Number.isInteger(value) || value < 1 || value === held) return;
                    onCap(value);
                  }}
                  className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink"
                />
              )}
            />
          )}
        </SettingList>
      </Card>

      {/* Each installed plugin with what it holds set (PLUGSET1, the modes design §4.1), beside the folder they are looked for
          in: read only, each a door to its page, where its settings are changed. */}
      {facts.plugins && <PluginSettings plugins={facts.plugins} onOpen={(id) => onGo({ view: 'plugins', item: id })} />}

      {/* This machine's sessions' access (AUTONOMY1d, D184 point 1): the level under every workspace's and repository's, whose
          own are on their Setup tabs. Here because it is the machine's, as the driver's dials are, and offered where the shell
          answers it, since an older shell has no route to write it. Nothing stands above it, so turning full access off clears it.
          Its lead paragraph is its title's ⓘ, as Setup's fold carries its summary line (ACCESSASK1, the UI/UX design §3.3). */}
      {facts.machineAccess && (
        <Card>
          <section aria-label={t('projects.setup.section.access')}>
            <SectionTitle>
              <span className="inline-flex items-center gap-1.5">
                {t('projects.setup.section.access')}
                <WhyGlyph why={t('settings.access.machineHint')} />
              </span>
            </SectionTitle>
            <SettingList>
              <AccessRows
                owner="machine"
                pauses={resolveAccess([{ setting: facts.machineAccess, source: 'machine' }])}
                aboveFull={false}
                busy={busy.access}
                setHere={t('settings.access.setHere.machine')}
                onChange={onAccess}
              />
            </SettingList>
          </section>
        </Card>
      )}

      {/* The install's update (UPDATE1b): the banner, its other door on the screen, is gone once dismissed, and this
          stands. Here because the update is the application's own, as the home above is, and holds the driver's starts
          while it drains. A word the shell refuses is said in its sentence; one it takes is shown by the row itself. */}
      <UpdateSection update={facts.update} busy={busy.update} onSay={onSayUpdate} />
    </CardStack>
  );
}
