import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, CardStack, CheckField, Icon, MiddlePath, SectionTitle, SettingList, SettingRow, WhyGlyph } from '../design/ui';
import type { StarterDoor } from '../help/starters';
import type { Answered } from '../work/InlineConfirm';
import type { PluginShown } from '../plugins/catalog';
import { PluginSettings } from './plugins/PluginSettings';
import { CopyBoundSetting } from './machine/CopyBoundSetting';
import { ManifestBoundsSetting } from './machine/ManifestBoundsSetting';
import { type MinutesDeclared, MinutesSetting } from './machine/MinutesSetting';
import { type TerminalTier, TerminalTierSetting } from './machine/TerminalTierSetting';
import type { TerminalTierChange } from '../person/terminalActs';
import { type AccessEdit, type AccessLevel, AccessRows, resolveAccess } from './repository/Access';
import { type GitAccessEdit, type GitAccessLevel, GitAccessRows, resolveGitAccess } from './repository/GitAccess';

/** What Agents and sessions shows, as the shell's state answers it; a field a shell does not answer is absent. */
export type DriverFacts = {
  /** Where plugins are looked for; none where the answer has none, and the row is absent. */
  pluginsFolder: string | null;
  /** The installed plugins, for the Plugins section; none where the answer has none or before it answers, and the section is absent. */
  plugins?: readonly PluginShown[] | null;
  notify: boolean;
  /** Minutes an account cools when its agent names no reset; absent where the answer lacks it. */
  coolOff?: number | null;
  /** Minutes a session may be quiet before Daoris looks in; absent where the answer lacks it. */
  quiet?: number | null;
  /** How many sessions Daoris starts at once here, and the tier of a terminal's presses; absent where the answer lacks it. */
  cap?: number | null;
  terminal?: TerminalTier | null;
  terminalChanges?: TerminalTierChange[];
  /** The most files, and the most minutes, a folder's manifest read spends; absent where the answer lacks them. */
  manifestFiles?: number | null;
  manifestMinutes?: number | null;
  /** The most megabytes a folder may hold to be copied for a session working apart; absent where the answer lacks it. */
  manifestCopy?: number | null;
  /** This machine's sessions' access; absent where the answer lacks it. */
  machineAccess?: AccessLevel | null;
  /** This machine's git access as set; absent where the answer lacks it. */
  machineGitAccess?: GitAccessLevel | null;
};

/**
 * Agents and sessions' cards, drawn from the shell's facts: the driver's dials over `driver.json` with where plugins are
 * looked for, this machine's sessions' access, and the plugins' settings. `DriverDomain` holds the hooks and hands
 * the facts here; a story hands a fixture, so the domain is drawn at the frame's widths.
 * A molecule: every state by props, every press out.
 */
export function DriverCards({ facts, minutes, busy, onGo, onNotify, onCoolOff, onQuiet, onManifest, onCap, onTerminal, onAccess, onGitAccess }: {
  facts: DriverFacts;
  /** The settings held in minutes as the driver's list declares them, which the domain reads from the list. */
  minutes: { cooloff: MinutesDeclared; quiet: MinutesDeclared; manifest?: MinutesDeclared };
  busy: { access: boolean; gitAccess?: boolean };
  onGo: (door: StarterDoor) => void;
  onNotify: (on: boolean) => void;
  /** A cool-off in minutes the person typed and left: whole, one or more, and not the one held. */
  onCoolOff: (minutes: number) => void;
  /** A quiet bound in minutes the person typed and left: whole, in its bounds, and not the one held. */
  onQuiet?: (minutes: number) => void;
  /** One bound of the manifest setting the person typed and left, the copy's in megabytes: whole, and not the one held. */
  onManifest?: (bound: { files: number } | { minutes: number } | { copy: number }) => void;
  /** A cap the person typed and left: whole, one or more, and not the one held. */
  onCap: (cap: number) => void;
  onTerminal?: (tier: TerminalTier) => void;
  onAccess: (edit: AccessEdit, answered?: Answered) => void;
  /** A change to this machine's git access, a loosening confirmed in its row first. */
  onGitAccess?: (edit: GitAccessEdit, answered?: Answered) => void;
}) {
  const { t } = useTranslation();
  // The cap held as text while typed: a number input mid-edit passes through the empty string, which the config refuses.
  const [cap, setCap] = useState<string | null>(null);

  return (
    <CardStack>
      {/* 🔴 A setting is a ROW, not a paragraph with its one control beneath it, which would put the first
          checkbox far below the title and the next dial a screen further down. `SettingRow` carries the shape:
          the label leads, the hint is one line, the value beside its words on the card's track, the paragraph on the
          glyph — and the cards are the sections of one settings page rather than five essays. */}

      {/* The driver's dials, in one card. The notification switch leads because it is the setting a
          person is most likely to have come here to change. */}
      <Card>
        <SettingList>
          {/* The switch says what it governs beside its value, as full access does, and
              what it never governs: a terminal's press, whose terminal waits on the person, and what Notifications lists. */}
          <SettingRow
            label={t('settings.notify.label')}
            twin={t('settings.notify.terminal')}
            why={t('settings.notify.body')}
            control={<CheckField hideLabel checked={facts.notify} onChange={onNotify} label={t('settings.notify.label')} />}
          >
            <p className="m-0 text-small text-ink-soft">{t(facts.notify ? 'settings.notify.state.on' : 'settings.notify.state.off')}</p>
            <p className="m-0 mt-1 text-small text-ink-faint">{t('settings.notify.press')}</p>
          </SettingRow>

          {/* How long an account cools when its agent names no reset: offered where the shell says it,
              since the answer has no such field and has no route to write it. */}
          {typeof facts.coolOff === 'number' && (
            <MinutesSetting setting={minutes.cooloff} held={facts.coolOff} onSet={onCoolOff} />
          )}

          {/* How long a session may say nothing and run nothing before Daoris looks in: the one quiet setting, offered where the
              shell says it. A session is never stopped for running long. */}
          {typeof facts.quiet === 'number' && onQuiet && (
            <MinutesSetting setting={minutes.quiet} held={facts.quiet} onSet={onQuiet} />
          )}

          {/* How large a folder's manifest read may grow before the folder is too large to follow: files and minutes, one setting. */}
          {typeof facts.manifestFiles === 'number' && typeof facts.manifestMinutes === 'number' && minutes.manifest && onManifest && (
            <ManifestBoundsSetting
              setting={minutes.manifest}
              files={facts.manifestFiles}
              minutes={facts.manifestMinutes}
              onFiles={(files) => onManifest({ files })}
              onMinutes={(value) => onManifest({ minutes: value })}
            />
          )}

          {/* The manifest setting's third bound beside its first two: how large a folder may be to be copied for a session set apart. */}
          {typeof facts.manifestCopy === 'number' && onManifest && (
            <CopyBoundSetting megabytes={facts.manifestCopy} onCopy={(copy) => onManifest({ copy })} />
          )}

          {/* How many sessions Daoris starts at once: the person's one dial for the machine,
              never per account. Offered where the shell answers the field, as the cool-off is. */}
          {typeof facts.cap === 'number' && (
            <SettingRow
              label={t('settings.cap.label')}
              twin={t('settings.cap.terminal')}
              why={t('settings.cap.body')}
              control={(
                <input
                  type="number"
                  min={1}
                  aria-label={t('settings.cap.label')}
                  value={cap ?? String(facts.cap)}
                  onChange={(event) => setCap(event.target.value)}
                  onBlur={() => {
                    // As the cool-off: a value under one, not whole, or the one held puts the held value back.
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

          {facts.terminal && onTerminal && <TerminalTierSetting held={facts.terminal} changes={facts.terminalChanges} onSet={onTerminal} />}

          {facts.pluginsFolder && (
            <SettingRow
              label={t('plugin.folder')}
              twin={t('settings.pluginsFolder.hint')}
              why={t('plugin.body')}
              control={(
                <>
                  <MiddlePath path={facts.pluginsFolder} />
                  <Button onClick={() => onGo({ view: 'plugins' })}>
                    <Icon name="plug" size={14} />
                    {t('settings.pluginsFolder.open')}
                  </Button>
                </>
              )}
            />
          )}
        </SettingList>
      </Card>

      {/* This machine's sessions' access: the level under every workspace's and repository's, whose
          own are on their Setup tabs. Here because it is the machine's, as the driver's dials are, and offered where the shell
          answers it, since an answer without the field has no route to write it. Nothing stands above it, so turning full access off clears it.
          Its lead paragraph is its title's ⓘ, as Setup's fold carries its summary line. */}
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
              {/* This machine's git access, the level under every workspace's and repository's,
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

      {/* Each installed plugin with what it holds set, under the folder they are looked for
          in: read only, each a door to its page, where its settings are changed. */}
      {facts.plugins && <PluginSettings plugins={facts.plugins} onOpen={(id) => onGo({ view: 'plugins', item: id })} />}
    </CardStack>
  );
}
