// The twelve real cases of docs/2026-11-13-performance-matrix.md section 2. A case is built when it names the ask it files and the
// expectations judged on the reads; the others name what each needs before it can be.

const REQUIRE_QUOTE = 'the notes file ends with the word done';

export const cases = [
  {
    id: 1,
    title: 'One-repository quest through to landed',
    built: true,
    repositories: 1,
    ask: { words: 'Add a file notes.txt at the repository root saying what this repository is for, in one sentence.', workflow: 'dev-line' },
    expect: ['created', 'set-up', 'filed', 'taken', 'started', 'done', 'review-reached', 'accepted', 'landed'],
    needs: [],
  },
  {
    id: 2,
    title: 'A quest with a requirement checked against its done',
    built: true,
    repositories: 1,
    ask: {
      words: `Add a file notes.txt at the repository root with one line of notes; ${REQUIRE_QUOTE}.`,
      require: [[REQUIRE_QUOTE, 'read notes.txt: its last word is done']],
      workflow: 'dev-line',
    },
    expect: ['created', 'set-up', 'filed', 'requirement-kept', 'taken', 'started', 'done', 'requirement-answered', 'review-reached', 'accepted', 'landed'],
    needs: [],
  },
  { id: 3, title: 'A chain across two repositories, the later building on the first\'s commit', built: false, repositories: 2, needs: ['a second scratch repository and a chain step (`then`) on the first quest', 'a read of the later quest\'s base against the first\'s landed commit'] },
  { id: 4, title: 'Co-working: a contract exchanged on the channel and a helper question answered', built: false, repositories: 1, needs: ['a team ask with two parts', 'channel reads (`daoris ask channel`) and helper records', 'expectations for delivery within one look'] },
  { id: 5, title: 'A review with a send-back, a second revision, then Accept', built: false, repositories: 1, needs: ['the person\'s send-back word (`--with-word` with a scripted reason)', 'a read that the re-landing moved a pushed branch (LANDAGAIN1)'] },
  { id: 6, title: 'A workflow with push, pull request, watch and clean-up through a plugin', built: false, repositories: 1, needs: ['a sandbox remote and a pull-request plugin on the scratch repository', 'no remote is touched by this tool, so the plugin must be a stand-in'] },
  { id: 7, title: 'A release step on a declared target, observe-only', built: false, repositories: 1, needs: ['a sandbox release definition (RELEASETARGET1, DELIVERY1 a)'] },
  { id: 8, title: 'A restart in the middle: every session carries on, nothing re-sent', built: false, repositories: 1, needs: ['the steward\'s update and restart of the install between two reads'] },
  { id: 9, title: 'A limit hit mid-turn: the work moves or resumes by itself', built: false, repositories: 1, needs: ['an account at its limit (LIMITRESUME1) and a read of the hand-over'] },
  { id: 10, title: 'Ask Daoris drives a case end to end from the person\'s words', built: false, repositories: 1, needs: ['the help conversation\'s session events', 'the second-model read on'] },
  { id: 11, title: 'A browser-using session on the person\'s one profile while the person browses', built: false, repositories: 1, needs: ['a probe of the plugin server\'s tab-open log (BROWSERSHARE1)'] },
  { id: 12, title: 'The multi-repository delivery case replayed on fixture repositories', built: false, repositories: 3, needs: ['three scratch repositories and the delivery case\'s asks (DELIVERY1 j)'] },
];

export function caseById(id) {
  const found = cases.find(c => String(c.id) === String(id));
  if (!found) throw new Error(`case ${id} is not in the catalogue (1 to ${cases.length})`);
  return found;
}
