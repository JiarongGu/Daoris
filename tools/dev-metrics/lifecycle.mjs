import { elapsed, earliest, missing, moment } from './records.mjs';
import { execGit } from '../fsx.mjs';
import { existsSync } from 'node:fs';

function firstCommit(sessions) {
  const clocks = [];
  for (const s of sessions) {
    if (!s.tree || !existsSync(s.tree) || !/^[a-f0-9]{40,64}$/i.test(s.base_commit ?? '')) continue;
    try {
      const rows = execGit(['--no-optional-locks', '-C', s.tree, 'log', '--first-parent', '--reverse', '--format=%cI', `${s.base_commit}..HEAD`], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim().split('\n');
      if (rows[0]) clocks.push(rows[0]);
    } catch { /* a gone tree or unreadable range does not establish a commit clock */ }
  }
  return moment(earliest(clocks), 'session tree first commit timestamp after base_commit (git first-parent committer clock); no readable range');
}

export function lifecycleRows(quests, sessions, links, home, landings, logs, workflow) {
  return quests.map(q => {
    const ids = [...links].filter(([, quest]) => quest === q.id).map(([id]) => id);
    const operations = home.tables.quest_log.filter(o => o.quest === q.id).sort((a, b) => Date.parse(a.at) - Date.parse(b.at));
    const op = kind => earliest(operations.filter(o => o.kind.toLowerCase() === kind).map(o => o.at));
    const starts = home.tables.event_journal.filter(e => e.family === 'sessions' && e.change === 'state' && e.state_new?.toLowerCase() === 'working' && (e.quest === q.id || ids.includes(e.record?.replace(/^session:/, ''))));
    const l = landings.filter(l => l.quest === q.id || ids.includes(l.session));
    const steps = workflow.filter(r => r.run === q.id).flatMap(r => r.steps);
    const stepEnd = kind => earliest(steps.filter(s => kind(s.kind) && s.state === 'reached').map(s => s.endedAt));
    const stages = {
      published: moment(q.filed ?? op('published'), 'quests.filed or quest_log published.at'),
      taken: moment(op('taken') ?? (q.status === 'Taken' ? q.updated : undefined), 'knowledge.db quest_log taken.at'),
      started: moment(earliest([...starts.map(e => new Date(e.at).toISOString()), ...logs.filter(e => e.event === 'session.started' && ids.includes(e.data?.session)).map(e => e.time)]), 'knowledge.db event_journal sessions state Working or logs session.started; sessions.created is queued time'),
      firstCommit: firstCommit(sessions.filter(s => ids.includes(s.id))),
      done: moment(op('done') ?? q.submission?.at ?? (q.status === 'Done' ? q.updated : undefined), 'quest_log done.at or quest submission.at'),
      submitted: moment(op('submitted') ?? q.submission?.at, 'quest_log submitted.at or quest submission.at'),
      held: moment(earliest(steps.filter(s => s.waiting).map(s => s.holdObservedAt)), 'workflow hold entry timestamp; quest submitted is readiness, not a hold'),
      questAccepted: moment(op('accepted') ?? q.accepted, 'quest_log accepted.at or quest.accepted (departure/evidence yes, separate from workflow Accept)'),
      accepted: moment(stepEnd(k => k === 'review'), 'workflow review Accept completion timestamp; quest.accepted is a different yes'),
      landed: moment(earliest(l.map(l => l.landedAt)), 'landings branches.landedAt'),
      pushed: moment(stepEnd(k => k === 'push'), 'push completion timestamp; landings.pushed/pushedTip records no clock'),
      prMerged: moment(earliest(l.filter(l => l.pullRequestState?.state === 'merged').map(l => l.pullRequestState.at)), 'landings pullRequestState.at when state=merged'),
      released: moment(stepEnd(k => k?.endsWith('/release')), 'plugin release node completion timestamp bound to this quest and commit in workflow observations; built-in release cleans a tree'),
    };
    return { quest: q.id, sessions: ids, stages, transitions: operations.map(o => ({ stage: o.kind, at: moment(o.at, 'quest_log at') })), stageClock: 'first retained clock per stage; transitions retain repeated takes, submissions and reopens', firstCommitClock: 'git first-parent committer timestamp; the session tree must still be readable', elapsedMs: {
      publishedToTaken: elapsed(stages.published, stages.taken, 'quest_log taken.at and quests.filed'),
      takenToStarted: elapsed(stages.taken, stages.started, 'quest taken.at and session Working transition'),
      startedToFirstCommit: elapsed(stages.started, stages.firstCommit, 'session first commit clock'),
      firstCommitToDone: elapsed(stages.firstCommit, stages.done, 'session first commit clock and quest done.at'),
      doneToHeld: elapsed(stages.done, stages.held, 'workflow hold entry timestamp'),
      doneToAccepted: elapsed(stages.done, stages.accepted, 'quest done and acceptance timestamps'),
      doneToQuestAccepted: elapsed(stages.done, stages.questAccepted, 'quest done and departure/evidence yes timestamps'),
      acceptedToLanded: elapsed(stages.accepted, stages.landed, 'acceptance and landing timestamps'),
      doneToLanded: elapsed(stages.done, stages.landed, 'quest done and landing timestamps'),
      landedToPushed: elapsed(stages.landed, stages.pushed, 'push completion timestamp'),
      pushedToPrMerged: elapsed(stages.pushed, stages.prMerged, 'push and PR merge completion timestamps'),
      prMergedToReleased: elapsed(stages.prMerged, stages.released, 'release completion timestamp'),
    } };
  });
}
