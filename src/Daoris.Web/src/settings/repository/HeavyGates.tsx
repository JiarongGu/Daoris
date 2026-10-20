import type { TFunction } from 'i18next';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, FormField, FormGrid, FormRow, Icon, Inline, SettingRow } from '../../design/ui';
import { shellWord } from '../../lib/shellWord';
import { type Answered, Refused } from '../../work/InlineConfirm';

/**
 * One of this machine's heavy gates for a repository (`STATE`'s `heavyGates`, HEAVYRUN1b2b): its name, the command it runs, and
 * the folder under the checkout it runs in, none for the checkout itself.
 */
export type HeavyGate = { name: string; run: string; cwd?: string | null };

/** A gate as the editor holds it: every field text, and a key that stays with the gate as it moves. */
type Draft = { key: number; name: string; run: string; cwd: string };

const blank = (draft: Draft) => !draft.name.trim() && !draft.run.trim() && !draft.cwd.trim();

/**
 * The list as `SET_HEAVY_GATES` takes it: each gate trimmed, a folder left out where none is written, a row left wholly blank passed
 * over, and none where nothing is left, which clears it. A half-written gate is sent as it stands: the driver judges it.
 */
function listOf(drafts: readonly Draft[]): HeavyGate[] | null {
  const gates = drafts.filter((draft) => !blank(draft)).map(({ name, run, cwd }) => ({
    name: name.trim(),
    run: run.trim(),
    ...(cwd.trim() ? { cwd: cwd.trim() } : {}),
  }));
  return gates.length > 0 ? gates : null;
}

/** Whether a list sends what is already set: the same gates in the same order. */
const same = (sent: HeavyGate[] | null, set: readonly HeavyGate[]) =>
  (sent ?? []).length === set.length
  && (sent ?? []).every((gate, at) => gate.name === set[at]!.name && gate.run === set[at]!.run && (gate.cwd ?? null) === (set[at]!.cwd ?? null));

/**
 * Whether the repository's own `daoris.gates.json` wins over this machine's list, as the driver read it (`HEAVY_MARKS`, HEAVYRUN1b2c):
 * `marks` true where the file marks heavy gates (their `names`), false where it marks none or there is no file, null with the driver's
 * `refusal` where it cannot be told. The page judges nothing of its own.
 */
export type HeavyMarks = {
  marks: boolean | null;
  names: string[];
  refusal?: string | null;
  /** The gates the file declares and does not mark heavy (FABRIC-PIPELINE1b); absent on a shell older than it. */
  light?: string[];
};

/**
 * A repository's heavy gates in its Setup's folded line: each by name in its order, or that none is set here; cued "not used" where the
 * repository's own file marks heavy gates and so wins.
 */
export function heavySummary(t: TFunction, gates: readonly HeavyGate[], marks?: HeavyMarks | null): string {
  if (gates.length === 0) return t('settings.heavy.summaryNone');
  const listed = t('settings.heavy.summary', { count: gates.length, names: gates.map((gate) => gate.name).join(t('projects.setup.summary.listJoin')) });
  return marks?.marks === true ? t('settings.heavy.summaryUnused', { summary: listed }) : listed;
}

/** The section's sentence on where this list applies: what the driver read of the repository's own file, or the general rule until it has. */
function whereOf(t: TFunction, repository: string, marks?: HeavyMarks | null): string {
  if (!marks) return t('settings.heavy.where', { repository });
  if (marks.marks === true) {
    return t('settings.heavy.whereMarks', { repository, names: marks.names.join(t('projects.setup.summary.listJoin')) });
  }
  if (marks.marks === false) return t('settings.heavy.whereNone', { repository });
  return t('settings.heavy.whereUnknown', { repository, refusal: marks.refusal ?? '' });
}

/** What a save said once the driver took it: the list set, by its count, or cleared. */
export function heavyToast(t: TFunction, name: string, gates: readonly HeavyGate[] | null): string {
  return gates === null ? t('settings.heavy.cleared', { name }) : t('settings.heavy.saved', { name, count: gates.length });
}

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/**
 * **This machine's heavy gates for a repository** (HEAVYRUN1b2b, D180's HEAVYRUN1b2a note): the builds and suites a driven session
 * runs once, at its end, through `daoris-driver run-heavy`, one run at a time in the repository, used only where the repository's own
 * `daoris.gates.json` marks no heavy gate. A row of its Setup's section of their own, the same `heavyGates` `daoris-driver heavy`
 * edits (D50), kept on this machine and never written into the repository.
 *
 * @remarks
 * **The list is one sequence, edited whole** (D180: one gate at a time was rejected, since the order is the point). *Edit*, or *Add
 * heavy gates* where none is set, opens every gate in place, each its name, its command and its folder, with *Move up*, *Move down* and
 * *Remove*, and *Add a gate* at the end; *Save* sends the whole list in its order, and *Clear* takes it away. The page judges nothing:
 * a list the driver would not read is its refusal, said under Save in its words, with the list still open as it was.
 *
 * It says plainly where it applies and that a command may bound its own workers, which is where an undeclared repository's workers
 * are bounded (HEAVYRUN1a2's case).
 *
 * A molecule: every state is reached by its props, and every press goes out.
 */
export function HeavyGates({ repository, gates, marks, busy = false, onSave }: {
  repository: string;
  /** Whether the repository's own file wins, as the driver read it; none until it has answered, and the section says the general rule. */
  marks?: HeavyMarks | null;
  /** The gates set for it on this machine, in the order they run; none where none is. */
  gates: readonly HeavyGate[];
  busy?: boolean;
  /** The whole list, or null to clear it; the answer says whether it landed or the driver's sentence. */
  onSave: (gates: HeavyGate[] | null, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const next = useRef(0);
  const [drafts, setDrafts] = useState<Draft[] | null>(null);
  const [refused, setRefused] = useState<string | null>(null);
  const draftOf = (gate?: HeavyGate): Draft => ({ key: next.current++, name: gate?.name ?? '', run: gate?.run ?? '', cwd: gate?.cwd ?? '' });
  const open = () => { setRefused(null); setDrafts(gates.length > 0 ? gates.map(draftOf) : [draftOf()]); };
  const save = (list: HeavyGate[] | null, landed: () => void) => {
    setRefused(null);
    onSave(list, { done: landed, refused: setRefused });
  };
  const change = (key: number, field: 'name' | 'run' | 'cwd', value: string) =>
    setDrafts((now) => now && now.map((draft) => (draft.key === key ? { ...draft, [field]: value } : draft)));
  const move = (from: number, to: number) => setDrafts((now) => {
    if (!now || to < 0 || to >= now.length) return now;
    const moved = [...now];
    [moved[from], moved[to]] = [moved[to]!, moved[from]!];
    return moved;
  });
  const sent = drafts && listOf(drafts);
  const editing = drafts !== null;

  return (
    <SettingRow
      label={t('settings.heavy.label')}
      hint={t('settings.heavy.twin', { repository: shellWord(repository, '<repository>') })}
      why={t('settings.heavy.why')}
      control={!editing && (
        <>
          <Button variant="ghost" disabled={busy} onClick={open}>{t(gates.length > 0 ? 'settings.heavy.edit' : 'settings.heavy.add')}</Button>
          {gates.length > 0 && (
            <Button variant="ghost" disabled={busy} onClick={() => save(null, () => undefined)}>{t('settings.heavy.clear')}</Button>
          )}
        </>
      )}
    >
      <div className="grid gap-2">
        <p className="m-0 text-small text-ink-soft"><Inline text={whereOf(t, repository, marks)} /></p>
        <p className="m-0 text-small text-ink-soft"><Inline text={t('settings.heavy.workers')} /></p>
        {!editing && gates.length === 0 && <p className="m-0 text-small text-ink-soft">{t('settings.heavy.none')}</p>}
        {!editing && gates.length > 0 && (
          <ol aria-label={t('settings.heavy.list', { repository })} className="m-0 grid list-decimal gap-1 pl-6">
            {gates.map((gate) => (
              <li key={gate.name} className="text-body text-ink">
                <span className="inline-flex min-w-0 flex-wrap items-baseline gap-x-2">
                  <span className="font-medium">{gate.name}</span>
                  <code className="wrap-anywhere font-mono text-small text-ink-soft">{gate.run}</code>
                  {gate.cwd && <span className="text-small text-ink-faint">{t('settings.heavy.in', { folder: gate.cwd })}</span>}
                </span>
              </li>
            ))}
          </ol>
        )}
        {editing && (
          <form
            className="grid gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              if (!busy && !same(sent, gates)) save(sent, () => setDrafts(null));
            }}
          >
            {/* The list lays out by its own width (R1, UIFIT1d): a name, the command, a folder and the acts in columns
                while the form holds them, each gate stacked under its own field names below 40rem, never by the window. */}
            <FormGrid columns={['short', 'long', 'short', 'acts']} floor="40rem">
              <FormRow head>
                <span className="text-meta text-ink-faint">{t('settings.heavy.field.name')}</span>
                <span className="text-meta text-ink-faint">{t('settings.heavy.field.run')}</span>
                <span className="text-meta text-ink-faint">{t('settings.heavy.field.cwd')}</span>
                <span />
              </FormRow>
              {drafts.map((draft, at) => {
                const n = at + 1;
                return (
                  <FormRow key={draft.key}>
                    <FormField label={t('settings.heavy.field.name')} when="stacked">
                      <input
                        aria-label={t('settings.heavy.nameOf', { n })}
                        value={draft.name}
                        spellCheck={false}
                        onChange={(event) => change(draft.key, 'name', event.target.value)}
                        className={FIELD}
                      />
                    </FormField>
                    <FormField label={t('settings.heavy.field.run')} when="stacked">
                      <input
                        aria-label={t('settings.heavy.runOf', { n })}
                        value={draft.run}
                        spellCheck={false}
                        onChange={(event) => change(draft.key, 'run', event.target.value)}
                        className={FIELD}
                      />
                    </FormField>
                    <FormField label={t('settings.heavy.field.cwd')} when="stacked">
                      <input
                        aria-label={t('settings.heavy.cwdOf', { n })}
                        value={draft.cwd}
                        spellCheck={false}
                        placeholder={t('settings.heavy.checkout')}
                        onChange={(event) => change(draft.key, 'cwd', event.target.value)}
                        className={FIELD}
                      />
                    </FormField>
                    <span className="flex items-center">
                      <Button variant="ghost" disabled={busy || at === 0} aria-label={t('settings.heavy.up', { n })} onClick={() => move(at, at - 1)}>
                        <Icon name="chevronUp" size={12} />
                      </Button>
                      <Button
                        variant="ghost"
                        disabled={busy || at === drafts.length - 1}
                        aria-label={t('settings.heavy.down', { n })}
                        onClick={() => move(at, at + 1)}
                      >
                        <Icon name="chevronDown" size={12} />
                      </Button>
                      <Button
                        variant="ghost"
                        disabled={busy}
                        aria-label={t('settings.heavy.remove', { n })}
                        onClick={() => setDrafts((now) => now && now.filter((each) => each.key !== draft.key))}
                      >
                        <Icon name="x" size={12} />
                      </Button>
                    </span>
                  </FormRow>
                );
              })}
            </FormGrid>
            <div className="flex flex-wrap gap-2">
              <Button variant="ghost" disabled={busy} onClick={() => setDrafts((now) => [...(now ?? []), draftOf()])}>
                <Icon name="plus" size={12} />
                {t('settings.heavy.addGate')}
              </Button>
              <Button type="submit" disabled={busy || same(sent, gates)}>{t('settings.heavy.save')}</Button>
              <Button variant="ghost" disabled={busy} onClick={() => { setRefused(null); setDrafts(null); }}>{t('common.cancel')}</Button>
            </div>
          </form>
        )}
        {refused && <Refused sentence={refused} />}
      </div>
    </SettingRow>
  );
}
