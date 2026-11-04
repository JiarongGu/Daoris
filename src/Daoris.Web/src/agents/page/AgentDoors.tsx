import type * as React from 'react';
import type { Tool } from '../tools';
import { Button, Chip, Inline, Pill, Tip } from '../../design/ui';
import { doorsSummary, versionOnly } from '../agents';
import { type TFunction } from 'i18next';
import { Fold } from './Fold';
import type { AgentActs, AgentSection } from '../AgentPage';

export function AgentDoors({ t, tool, adapter, open, toggle, onOpenPlugin, busy, acts, pinOpen, setPinOpen, pinning, setPinning, doorRunning, doorConsole }: {
  t: TFunction<"translation", undefined>;
  tool: Tool;
  adapter: string | undefined;
  open: ReadonlySet<AgentSection>;
  toggle: (section: AgentSection) => void;
  onOpenPlugin: ((plugin: string) => void) | undefined;
  busy: boolean;
  acts: AgentActs;
  pinOpen: string | null;
  setPinOpen: React.Dispatch<React.SetStateAction<string | null>>;
  pinning: Record<string, string>;
  setPinning: React.Dispatch<React.SetStateAction<Record<string, string>>>;
  doorRunning: string | null;
  doorConsole: React.ReactNode;
}) {
  return (
    <>
<Fold section="doors" title={t('harness.doors')} summary={doorsSummary(tool, adapter)} open={open.has('doors')} onToggle={toggle}>
        {tool.doors.map((each) => (
          <div key={each.harness} className="border-t border-line py-2 first:border-t-0">
            <header className="flex flex-wrap items-center gap-2">
              <Pill tone="neutral">{t(each.wire === 'acp' ? 'harness.wire.acp' : 'harness.wire.pipe')}</Pill>
              <span className="font-mono text-small text-ink">{each.harness}</span>
              {each.present
                ? <span className="font-mono text-small text-ink-faint">{versionOnly(each.version ?? '')}</span>
                : <span className="text-small text-ink-faint">{t('harness.absent')}</span>}
              {each.harness === adapter && <Chip accent>{t('harness.spawns')}</Chip>}
              {each.plugin && (onOpenPlugin
                // A door to the plugin's page (PLUGUI1c2b, the Plugins design §5): a control, so a button, not a chip.
                ? <Button variant="ghost" onClick={() => onOpenPlugin(each.plugin!)}>{t('harness.declaredBy', { plugin: each.plugin })}</Button>
                : <Chip>{t('harness.declaredBy', { plugin: each.plugin })}</Chip>)}
              <span className="ml-auto flex gap-2">
                {!each.present && each.installs !== false && <Button disabled={busy} onClick={() => acts.onDoor(each.harness, 'install')}>{t('harness.install')}</Button>}
                {each.present && (each.updates === 'pin' || each.updates === 'tool') && (
                  <Tip content={t(each.updates === 'pin' ? 'harness.update.pinTip' : 'harness.update.toolTip')}>
                    <Button variant="ghost" disabled={busy} onClick={() => acts.onDoor(each.harness, 'update')}>{t('harness.update')}</Button>
                  </Tip>
                )}
              </span>
            </header>
            {each.problem && (
              <Tip content={each.problem}>
                <p className="mt-1.5 line-clamp-2 text-small text-ink-soft"><Inline text={each.problem} /></p>
              </Tip>
            )}
            {each.pinnable && (
              <div className="mt-2 flex flex-wrap items-center gap-2 text-small">
                {each.pinned ? (
                  <>
                    <Pill tone={each.managed ? 'done' : 'declined'}>
                      {t(each.managed ? 'harness.pin.pinned' : 'harness.pin.missing', { version: each.pinned })}
                    </Pill>
                    {each.managed && (
                      <Tip content={t('harness.pin.managedTip')}>
                        <span className="min-w-0 flex-1 truncate font-mono text-meta text-ink-faint">{each.managed}</span>
                      </Tip>
                    )}
                    <Button variant="ghost" className="ml-auto" disabled={busy} onClick={() => acts.onDoor(each.harness, 'unpin')}>
                      {t('harness.pin.unpin')}
                    </Button>
                  </>
                ) : (
                  <>
                    <span className="text-ink-faint">{t(each.present ? 'harness.pin.fromPath' : 'harness.pin.fromPathAbsent')}</span>
                    <Button
                      variant="ghost"
                      className="ml-auto"
                      aria-expanded={pinOpen === each.harness}
                      onClick={() => setPinOpen(pinOpen === each.harness ? null : each.harness)}
                    >
                      {t('harness.pin.open')}
                    </Button>
                  </>
                )}
              </div>
            )}
            {each.pinnable && !each.pinned && pinOpen === each.harness && (
              <form
                className="mt-2 flex flex-wrap items-center gap-2"
                onSubmit={(event) => {
                  event.preventDefault();
                  const version = (pinning[each.harness] ?? '').trim();
                  if (version) acts.onPin(each.harness, version);
                }}
              >
                <input
                  autoFocus
                  aria-label={t('harness.pin.version', { harness: each.harness })}
                  value={pinning[each.harness] ?? ''}
                  onChange={(event) => setPinning((held) => ({ ...held, [each.harness]: event.target.value }))}
                  placeholder={t('harness.pin.placeholder')}
                  className="w-32 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
                />
                <Button type="submit" disabled={busy || !(pinning[each.harness] ?? '').trim()}>{t('harness.pin.action')}</Button>
              </form>
            )}
            {doorRunning === each.harness && doorConsole}
          </div>
        ))}
      </Fold>

      {/* What it may do (PERM1, D72): the rules every session on this machine is handed, Daoris's defaults and the
          proposals, only where Daoris hands this agent the rules file (§5.1). */}
    </>
  );
}
