import { elapsed, earliest, missing, moment } from './records.mjs';
import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';

function firstCommit(sessions) {
  const clocks = [];
  for (const s of sessions) {
    if (!s.tree || !existsSync(s.tree) || !/^[a-f0-9]{40,64}$/i.test(s.base_commit ?? '')) continue;
    try {
      const rows = execFileSync('git', ['--no-optional-locks', '-C', s.tree, 'log', '--first-parent', '--reverse', '--format=%cI', `${s.base_commit}..HEAD`], { encoding: 'utf8', windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] }).trim().split('\n');
      if (rows[0]) clocks.push(rows[0]);
    } catch { /* a gone tree or unreadable range does not establish a commit clock */ }
  }
  return moment(earliest(clocks), 'session tree first commit timestamp after base_commit (git first-parent committer clock); no readable range');
}

export function lifecycleRows(quests, sessions, links, home, landings, logs) {
  return quests.map(q => {
    const ids = [...links].filter(([, quest]) => quest === q.id).map(([id]) => id);
    const operations = home.tables.quest_log.filter(o => o.quest === q.id);
    const op = kind => earliest(operations.filter(o => o.kind.toLowerCase() === kind).map(o => o.at));
    const starts = home.tables.event_journal.filter(e => e.family === 'sessions' && e.change === 'state' && e.state_new?.toLowerCase() === 'working' && (e.quest === q.id || ids.includes(e.record?.replace(/^session:/, ''))));
    const l = landings.filter(l => l.quest === q.id || ids.includes(l.session));
    const stages = {
      published: moment(q.filed ?? op('published'), 'quests.filed or quest_log published.at'),
      taken: moment(op('taken') ?? (q.status === 'Taken' ? q.updated : undefined), 'knowledge.db quest_log taken.at'),
      started: moment(earliest([...starts.map(e => new Date(e.at).toISOString()), ...logs.filter(e => e.event === 'session.started' && ids.includes(e.data?.session)).map(e => e.time)]), 'knowledge.db event_journal sessions state Working or logs session.started; sessions.created is queued time'),
      firstCommit: firstCommit(sessions.filter(s => ids.includes(s.id))),
      done: moment(q.submission?.at ?? op('done') ?? (q.status === 'Done' ? q.updated : undefined), 'quest_log done.at or quest submission.at'),
      held: moment(op('submitted'), 'quest_log submitted.at or workflow hold entry timestamp'),
      accepted: moment(op('accepted') ?? q.accepted, 'quest_log accepted.at or quest.accepted; workflow review Accept requires its own timestamp'),
      landed: moment(earliest(l.map(l => l.landedAt)), 'landings branches.landedAt'),
      pushed: missing('push completion timestamp; landings.pushed/pushedTip records no clock'),
      prMerged: moment(earliest(l.filter(l => l.pullRequestState?.state === 'merged').map(l => l.pullRequestState.at)), 'landings pullRequestState.at when state=merged'),
      released: missing('release node completion timestamp bound to this quest and commit in workflow observations'),
    };
    return { quest: q.id, sessions: ids, stages, firstCommitClock: 'git first-parent committer timestamp; the session tree must still be readable', elapsedMs: {
      publishedToTaken: elapsed(stages.published, stages.taken, 'quest_log taken.at and quests.filed'),
      takenToStarted: elapsed(stages.taken, stages.started, 'quest taken.at and session Working transition'),
      startedToFirstCommit: elapsed(stages.started, stages.firstCommit, 'session first commit clock'),
      firstCommitToDone: elapsed(stages.firstCommit, stages.done, 'session first commit clock and quest done.at'),
      doneToHeld: elapsed(stages.done, stages.held, 'workflow hold entry timestamp'),
      doneToAccepted: elapsed(stages.done, stages.accepted, 'quest done and acceptance timestamps'),
      acceptedToLanded: elapsed(stages.accepted, stages.landed, 'acceptance and landing timestamps'),
      doneToLanded: elapsed(stages.done, stages.landed, 'quest done and landing timestamps'),
      landedToPushed: elapsed(stages.landed, stages.pushed, 'push completion timestamp'),
      pushedToPrMerged: elapsed(stages.pushed, stages.prMerged, 'push and PR merge completion timestamps'),
      prMergedToReleased: elapsed(stages.prMerged, stages.released, 'release completion timestamp'),
    } };
  });
}
