import { useTranslation } from 'react-i18next';
import { SelectField, Tip } from './ui';

/** Radix Select cannot carry an empty value, so "every workspace" travels as a sentinel (QuestsView's precedent). */
const EVERY = '*';

/**
 * The scope control (WSP5): which circle the console is looking at. Global chrome, in the sidebar's
 * foot with the other global state (platform language §2) — one scope per query, never five filters
 * that can disagree (workspace design §4).
 *
 * **Absent while the deployment holds one workspace.** A family that is one circle needs no control
 * to say so, and silence keeps today's behaviour byte for byte — the same rule as session trees.
 * **"Every workspace" is stated, not implied**, with the count: the D24 shape — report the scope that
 * ran — rather than a page that silently mixes circles or silently picks one.
 *
 * Props only, no hook from `./queries` or `./shell` (the component plan's rule): every state here is
 * reachable by passing it, which is what its story and its test do.
 */
export function WorkspaceSwitcher({ workspaces, value, onChange }: {
  workspaces: string[];
  value: string | null;
  onChange: (workspace: string | null) => void;
}) {
  const { t } = useTranslation();
  if (workspaces.length < 2) return null;

  const options = [
    { value: EVERY, label: t('scope.every', { count: workspaces.length }) },
    ...workspaces.map((workspace) => ({ value: workspace, label: workspace })),
  ];

  return (
    <Tip content={t('scope.tip')}>
      <div className="grid">
        <SelectField
          ariaLabel={t('scope.label')}
          value={value ?? EVERY}
          onChange={(next) => onChange(next === EVERY ? null : next)}
          options={options}
        />
      </div>
    </Tip>
  );
}
