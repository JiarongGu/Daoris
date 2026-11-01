import type { PlaceApplied, PlaceAsk, PlaceDeclaration, PlacePlanned, PlaceRepositoryPlan, PlaceSide } from './placePlan';

// A place's plan as `PLACE_PLAN` answers it, for the set-up's tests and stories (AGENTSPLACE1j): two repositories of the workspace *aurora*,
// each with the facts the driver reads. What the ask changes follows the driver's rules only as far as the page shows them.

const side = (driven: boolean, preset: string | null = null): PlaceSide => ({ driven, held: false, trees: driven, match: { preset, own: [] } });

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
    suggested: { preset: 'merge-after-accept', adoption: false },
  },
  site: {
    repository: 'site', workspace: 'aurora', before: side(false), lowers: [], reach: [], adopted: false,
    suggested: { preset: 'pull-request', adoption: true },
  },
  archive: {
    repository: 'archive', workspace: 'aurora', before: side(false), lowers: [], reach: [], adopted: false,
    suggested: { preset: 'not-driven', adoption: true },
  },
};

/**
 * The plan a repository set asks of: *A pull request* is refused for `site`, which has no landing plugin; *Integrated* offers `engine`'s
 * declaration for acceptance; driving releases `engine`'s queued work; and a delivery lowers the part the person has in what lands.
 */
export function planOf(ask: PlaceAsk): PlacePlanned {
  const delivers = ask.preset !== 'not-driven';
  const rows: PlaceRepositoryPlan[] = ask.repositories.map((name) => {
    const known = REPOSITORIES[name] ?? REPOSITORIES.engine!;
    const { adopted: _adopted, ...row } = known;
    const changes = [
      ...(delivers && !known.before.driven ? [{ key: 'SET_DRIVABLE', part: 'driving', said: 'general driving', before: 'off', after: 'on' }] : []),
      ...(delivers && known.before.match.preset !== ask.preset
        ? [{ key: 'SET_LANDING', part: 'delivery', said: 'how its work lands', before: '`merge`', after: `\`${ask.preset}\`` }]
        : []),
      ...(ask.adoption ? [{ key: 'SETUP_PRESS', part: 'adoption', said: 'a set-up asked of its own session', before: 'not asked', after: ask.builds ? `asked, for ${ask.builds}` : 'asked' }] : []),
    ];
    return {
      ...row,
      repository: name,
      after: side(delivers || known.before.driven, delivers ? ask.preset : null),
      changes,
      lowers: delivers && ask.preset !== 'merge-after-accept' ? ['your part in what lands: it lands after your accept now, and would not'] : [],
      released: ask.drive && name === 'engine' ? [{ quest: 'q-12', title: 'Tidy the docs' }, { quest: 'q-15', title: 'Add the retry' }] : [],
      declaration: name === 'engine' ? declaration(ask.preset === 'integrated' ? ['integration'] : []) : null,
    };
  });
  const refusals = ask.preset === 'pull-request' && ask.repositories.includes('site')
    ? [{
      repository: 'site', part: 'delivery', code: 'no-landing-plugin',
      sentence: '`site`\'s remote is on `forge.example`, and no landing plugin here opens a pull request there. Add one on Plugins, or choose *A branch to push*.',
    }]
    : [];
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
