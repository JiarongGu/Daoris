import type { PresetWorkflow } from '../../workflow/edit/answers';
import type { PlaceApplied, PlaceAsk, PlaceDeclaration, PlacePlanned, PlaceRefusal, PlaceRepositoryPlan, PlaceSide } from './placePlan';

// A place's plan as `PLACE_PLAN` answers it, for the set-up's tests and stories (AGENTSPLACE1j): two repositories of the workspace *aurora*,
// each with the facts the driver reads. What the ask changes follows the driver's rules only as far as the page shows them.

const side = (driven: boolean, preset: string | null = null): PlaceSide => ({ driven, held: false, trees: driven, match: { preset, own: [] } });

const WHY = {
  default: { code: 'default', said: 'Nothing here asks for another, so work merges into the line after you accept.' },
  plugin: { code: 'plugin', said: 'A landing plugin serves its remote\'s platform.' },
  kept: { code: 'kept', said: 'It is how this place already lands its work.' },
} as const;

/** The four deliveries a set-up offers, as `WORKFLOW_LIST` announces them (`setUp`, `order`, `delivery`): English in the data, worded by id on the page. */
export const SETUP_PRESETS: PresetWorkflow[] = [
  { id: 'merge-after-accept', name: 'Merge workflow', graph: true, line: '', preset: true, order: 1, setUp: true,
    delivery: { id: 'merge-after-accept', method: 'merge', accept: 'you', name: 'Merge after you accept', for: 'For a repository whose work you accept: once the work\'s requests are answered you review it, and it merges into the line.' } },
  { id: 'branch-to-push', name: 'Branch workflow', graph: true, line: '', preset: true, order: 4, setUp: true,
    delivery: { id: 'branch-to-push', method: 'branch', accept: 'you', name: 'A branch to push', for: 'For a repository you push yourself: you review the work, and it lands on a branch of its own for you to push.' } },
  { id: 'pull-request', name: 'Hosted merge workflow', graph: true, line: '', preset: true, order: 5, setUp: true,
    delivery: { id: 'pull-request', method: 'pull-request', accept: 'you', name: 'A pull request', for: 'For a repository on a code host: you review the work, and a plugin pushes its branch and opens a pull request for you to merge.' } },
  { id: 'integrated', name: 'Integration workflow', graph: true, line: '', preset: true, order: 6, setUp: true,
    delivery: { id: 'integrated', method: 'integrate', accept: 'you', name: 'Integrated into the line', for: 'For a line that integrates its work: you review the work onto a branch, and you ask for it to be integrated into the line.' } },
  { id: 'documents', name: 'Documents workflow', graph: true, line: '', preset: true, order: 3, setUp: false,
    delivery: { id: 'merge-automatic', method: 'merge', accept: 'automatic', name: 'Merge with no review', for: 'For a development repository.' } },
];

const BLOB = '3b18e512dba79e4c8300dd08aeb37f8e728b8dad';
const TEXT = 'integration:\n  request: land\n  gates: [lint, tests]\n';

const declaration = (purposes: ('landed' | 'integration')[], standing = 'unaccepted'): PlaceDeclaration => ({
  line: 'main',
  commit: 'aa11bb2c3d4e',
  blob: BLOB,
  text: TEXT,
  acceptances: purposes.map((purpose) => ({
    purpose,
    standing,
    said: purpose === 'integration'
      ? 'Accept for integration: the line integrates by this declaration\'s `integration`, and a changed declaration waits for you.'
      : 'Accept for landed verification: `web-verify` verifies the work that lands by this declaration\'s gates.',
  })),
});

/** A digest the ask alone makes, so a changed ask is a changed plan. */
export const digestOf = (ask: PlaceAsk): string => {
  let hash = 7;
  for (const each of JSON.stringify(ask)) hash = (hash * 31 + each.charCodeAt(0)) >>> 0;
  return hash.toString(16).padStart(8, '0').repeat(8);
};

const REPOSITORIES: Record<string, Omit<PlaceRepositoryPlan, 'changes' | 'after' | 'released' | 'declaration'> & { adopted: boolean }> = {
  engine: {
    repository: 'engine', workspace: 'aurora', before: side(true, 'merge-after-accept'), lowers: [], reach: [], adopted: true,
    suggested: { preset: 'merge-after-accept', adoption: false, why: WHY.default },
  },
  site: {
    repository: 'site', workspace: 'aurora', before: side(false), lowers: [], reach: [], adopted: false,
    suggested: { preset: 'pull-request', adoption: true, why: WHY.plugin },
  },
  archive: {
    repository: 'archive', workspace: 'aurora', before: side(false), lowers: [], reach: [], adopted: false,
    suggested: { preset: 'not-driven', adoption: true, why: WHY.default },
  },
  // An imported repository of the list's 41, not driven.
  'archive-1': {
    repository: 'archive-1', workspace: 'aurora', before: side(false), lowers: [], reach: [], adopted: false,
    suggested: { preset: 'not-driven', adoption: true, why: WHY.default },
  },
};

/** A repository whose landing was chosen and whose work follows a saved workflow: the driver suggests the preset it matches, for `kept`. */
export const KEPT_ROW: Partial<PlaceRepositoryPlan> = {
  before: { ...side(true, 'merge-after-accept'), values: { workflow: { value: 'Release for engine v2', source: 'repository' } } },
  suggested: { preset: 'merge-after-accept', adoption: false, why: WHY.kept },
};

/**
 * The plan a repository set asks of: *A pull request* is refused for `site`, which has no landing plugin; *Integrated* offers `engine`'s
 * declaration for acceptance; driving releases `engine`'s queued work; and a delivery lowers the part the person has in what lands.
 */
export function planOf(ask: PlaceAsk, mix: Record<string, Partial<PlaceRepositoryPlan>> = {}): PlacePlanned {
  const delivers = ask.preset !== 'not-driven';
  const rows: PlaceRepositoryPlan[] = ask.repositories.map((name) => {
    const known = REPOSITORIES[name] ?? REPOSITORIES.engine!;
    const { adopted: _adopted, ...row } = known;
    const changes = [
      ...(delivers && ask.drive && !known.before.driven ? [{ key: 'SET_DRIVABLE', part: 'driving', said: 'general driving', before: 'off', after: 'on' }] : []),
      ...(!delivers && known.before.driven ? [{ key: 'SET_DRIVABLE', part: 'delivery', said: 'driven here', before: 'on', after: 'off' }] : []),
      ...(delivers && ask.preset !== 'keep' && known.before.match.preset !== ask.preset
        ? [
          { key: 'WORKFLOW_SAVE', part: 'delivery', said: 'a saved copy of the workflow Daoris ships', before: 'none', after: `\`${ask.preset}\`` },
          { key: 'WORKFLOW_USE', part: 'delivery', said: 'the workflow its work follows', before: 'Current', after: `\`${ask.preset}\`` },
        ]
        : []),
      ...(ask.adoption ? [{ key: 'SETUP_PRESS', part: 'adoption', said: 'a set-up asked of its own session', before: 'not asked', after: ask.builds ? `asked, for ${ask.builds}` : 'asked' }] : []),
    ];
    return {
      ...row,
      repository: name,
      after: side(delivers && (known.before.driven || ask.drive), delivers && ask.preset !== 'keep' ? ask.preset : known.before.match.preset),
      changes,
      lowers: delivers && ask.preset !== 'merge-after-accept' ? ['your part in what lands: it lands after your accept now, and would not'] : [],
      released: ask.drive && name === 'engine' ? [{ quest: 'q-12', title: 'Tidy the docs' }, { quest: 'q-15', title: 'Add the retry' }] : [],
      declaration: name === 'engine' ? declaration(ask.preset === 'integrated' ? ['integration'] : []) : null,
      ...mix[name],
    };
  });
  const site = ask.repositories.includes('site');
  const refusals: PlaceRefusal[] = [
    ...(ask.preset === 'pull-request' && site
      ? [{
        repository: 'site', part: 'delivery', code: 'no-landing-plugin',
        sentence: '`site`\'s remote is on `forge.example`, and no landing plugin here opens a pull request there. Add one on Plugins, or choose *A branch to push*.',
      }]
      : []),
    ...(ask.opinion && site
      ? [{
        repository: 'site', part: 'delivery', code: 'no-reviewers',
        sentence: 'a second opinion names the agents that read `site`\'s work, and none is set for it: they are yours to name, never guessed. Name them on its Setup → Second opinion.',
      }]
      : []),
    ...(ask.look && site
      ? [{
        repository: 'site', part: 'delivery', code: 'no-review-environment',
        sentence: 'your look needs a review environment for `site`, where its work runs for you before it lands, and none is set: add one on its Setup → Review.',
      }]
      : []),
    ...(ask.adoption && site
      ? [{ repository: 'site', part: 'adoption', code: 'no-checkout', sentence: '`site` has no checkout on this machine, so its own session cannot be asked to adopt Daoris here.' }]
      : []),
    ...(ask.drive && !delivers
      ? [{ repository: null, part: 'driving', code: 'not-driven', sentence: '*Not driven* takes no general driving: choose another delivery, or leave driving off.' }]
      : []),
  ];
  const made = rows.flatMap((row) => row.changes.map((change) => ({ change, repository: row.repository })));
  const changes = made.reduce<PlacePlanned['changes']>((all, each) => {
    const same = all.find((one) => one.change.key === each.change.key && one.change.after === each.change.after && one.change.before === each.change.before);
    if (same) same.repositories.push(each.repository);
    else all.push({ change: each.change, repositories: [each.repository] });
    return all;
  }, []);
  return {
    asked: ask,
    repositories: rows,
    changes,
    refusals,
    terminal: `daoris-driver place ${ask.repositories.join(' ')} --preset ${ask.preset}${ask.adoption ? ' --adoption' : ''} --plan`,
    applies: refusals.length === 0,
    digest: digestOf(ask),
  };
}

export const APPLIED: PlaceApplied = {
  done: true,
  refused: null,
  message: 'configuration applied; adoption pending',
  digest: digestOf({ repositories: ['engine'], preset: 'merge-after-accept', opinion: false, look: false, adoption: false, drive: false }),
  operations: [
    { id: 'config', kind: 'config', repository: null, said: 'the configuration written', outcome: 'done' },
    { id: 'quest:engine', kind: 'quest', repository: 'engine', said: 'the set-up quest asked of `engine`', outcome: 'done', quest: 'q-77' },
  ],
};

export const MOVED: PlaceApplied = {
  done: false,
  refused: 'moved',
  message: 'the plan you read no longer holds: a value, a declaration or the line moved since, so nothing was written. Plan it again (now `0a1b2c3d4e5f`).',
  digest: digestOf({ repositories: ['engine'], preset: 'integrated', opinion: false, look: false, adoption: false, drive: false }),
  operations: [],
};
