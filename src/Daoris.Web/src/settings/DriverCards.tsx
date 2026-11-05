import { type ComponentProps, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, CardStack, CheckField, Icon, PathText, SectionTitle, SettingList, SettingRow, WhyGlyph } from '../design/ui';
import type { StarterDoor } from '../help/starters';
import type { UpdateState } from '../update/UpdateBanner';
import type { Answered } from '../work/InlineConfirm';
import type { PluginShown } from '../plugins/catalog';
import { PluginSettings } from './plugins/PluginSettings';
import { type MinutesDeclared, MinutesSetting } from './machine/MinutesSetting';
import { type AccessEdit, type AccessLevel, AccessRows, resolveAccess } from './repository/Access';
import { type GitAccessEdit, type GitAccessLevel, GitAccessRows, resolveGitAccess } from './repository/GitAccess';
import { UpdateSection } from './machine/Update';

/** What the Driver domain shows, as the shell's state answers it; a field a shell does not answer is absent. */
export type DriverFacts = {
  /** Where this machine's Daoris lives; none while the shell has not answered. */
  home?: string | null;
  /** Where plugins are looked for (UX6j); none on an older shell, and the row is absent. */
  pluginsFolder: string | null;
  /** The installed plugins, for the Plugins section (PLUGSET1); none on an older shell or before it answers, and the section is absent. */
  plugins?: readonly PluginShown[] | null;
  notify: boolean;
  /** Minutes an account cools when its agent names no reset (TOOL4g); absent on a shell older than the field. */
  coolOff?: number | null;
  /** Minutes a session may be quiet before Daoris looks in; absent on a shell older than the field. */
  quiet?: number | null;
  /** How many sessions Daoris starts at once here (CAPSET1); absent on a shell older than the route. */
  cap?: number | null;
  /** This machine's sessions' access (AUTONOMY1d); absent on a shell older than it. */
  machineAccess?: AccessLevel | null;
  /** This machine's git access as set (GITAUTH1f); absent on a shell older than it. */
  machineGitAccess?: GitAccessLevel | null;
  /** The install's update (UPDATE1b). */
  update?: UpdateState;
};

/**
 * The Driver domain's cards (D75), drawn from the shell's facts: where this machine's Daoris lives, the driver's dials
 * over `driver.json`, this machine's sessions' access and the install's update. `DriverDomain` holds the hooks and hands
 * the facts here; a story hands a fixture, so the domain is drawn at the frame's widths (the UI/UX design R6, UIFIT1).
 * A molecule: every state by props, every press out.
 */
export function DriverCards({ facts, minutes, busy, onGo, onNotify, onCoolOff, onQuiet, onCap, onAccess, onGitAccess, onSayUpdate }: {
  facts: DriverFacts;
  /** The two settings held in minutes as the driver's list declares them, which the domain reads from the list. */
  minutes: { cooloff: MinutesDeclared; quiet: MinutesDeclared };
  busy: { access: boolean; update: boolean; gitAccess?: boolean };
  onGo: (door: StarterDoor) => void;
  onNotify: (on: boolean) => void;
  /** A cool-off in minutes the person typed and left: whole, one or more, and not the one held. */
  onCoolOff: (minutes: number) => void;
  /** A quiet bound in minutes the person typed and left: whole, in its bounds, and not the one held. */
  onQuiet?: (minutes: number) => void;
  /** A cap the person typed and left: whole, one or more, and not the one held (CAPSET1). */
  onCap: (cap: number) => void;
  onAccess: (edit: AccessEdit, answered?: Answered) => void;
  /** A change to this machine's git access (GITAUTH1f), a loosening confirmed in its row first. */
  onGitAccess?: (edit: GitAccessEdit, answered?: Answered) => void;
  onSayUpdate: ComponentProps<typeof UpdateSection>['onSay'];
}) {
  const { t } = useTranslation();
  // The cap as typed (CAPSET1), held as text while it is being typed: a number input mid-edit passes through the empty string,
  // and writing it straight to the config would be refused while the person was still reaching for a digit.
  const [cap, setCap] = useState<string | null>(null);

  return (
    <CardStack>
      {/* 🔴 A setting is a ROW. Every card here used to open with a
          paragraph and put its one control beneath it, so the first checkbox sat 580px below the
          title and the next dial a screen further down. `SettingRow` carries the shape now — the
          label leads, the hint is one line, the value beside its words on the card's track (R1), the paragraph on the
          glyph — and the cards are the sections of one settings page rather than five essays. */}

      {/* The driver's dials, in one card. The notification switch leads because it is the setting a
          person is most likely to have come here to change. */}
      <Card>
        <SettingList>
          {/* Where this machine's Daoris lives (D63), the card's first row; the home's page is This Daoris since WIRING1c,
              and this row is its door (the this-Daoris design §2). */}
          {facts.home && (
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
          {/* NOTIFY2d (the notification design §3.3, §5): the switch says what it governs beside its value, as full access does, and
              what it never governs: a terminal's press, whose terminal waits on the person, and what Notifications lists. */}
          <SettingRow
            label={t('settings.notify.label')}
            hint={t('settings.notify.terminal')}
            why={t('settings.notify.body')}
            control={<CheckField hideLabel checked={facts.notify} onChange={onNotify} label={t('settings.notify.label')} />}
          >
            <p className="m-0 text-small text-ink-soft">{t(facts.notify ? 'settings.notify.state.on' : 'settings.notify.state.off')}</p>
            <p className="m-0 mt-1 text-small text-ink-faint">{t('settings.notify.press')}</p>
          </SettingRow>

          {/* How long an account cools when its agent names no reset (TOOL4g, D125 §2.2): offered where the shell says it,
              since an older shell answers no such field and has no route to write it. */}
          {typeof facts.coolOff === 'number' && (
            <MinutesSetting setting={minutes.cooloff} held={facts.coolOff} onSet={onCoolOff} />
          )}

          {/* How long a session may say nothing and run nothing before Daoris looks in: the one quiet setting, offered where the
              shell says it. A session is never stopped for running long. */}
          {typeof facts.quiet === 'number' && onQuiet && (
            <MinutesSetting setting={minutes.quiet} held={facts.quiet} onSet={onQuiet} />
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
              {/* GITAUTH1f (the git-auth design §7.3): this machine's git access, the level under every workspace's and repository's,
                  untouched by full access; Settings → Git draws the same rows. */}
              {facts.machineGitAccess && onGitAccess && (
                <GitAccessRows
                  owner="machine"
                  classes={resolveGitAccess([{ classes: facts.machineGitAccess, source: 'machine' }])}
                  above={resolveGitAccess([])}
                  busy={busy.gitAccess}
                  onChange={onGitAccess}
                />
              )}
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
