/**
 * The tool's verbs and their flags, read into the options `main` acts on, and the refusal a verb stops with.
 */
export const USAGE = [
  'usage: node tools/merge-branch.mjs <branch> [--batch <branch>…] [--full | --gated] [--keep-going] [--one-at-a-time] [--no-commit-check] [--no-prune] [--no-timeout]',
  '       node tools/merge-branch.mjs --continue [--full | --gated] [--keep-going] [--one-at-a-time] [--no-prune]',
  '       node tools/merge-branch.mjs --rerun <gate>… [--keep-going] [--one-at-a-time] [--no-timeout]',
  '       node tools/merge-branch.mjs --stale [--keep-going] [--one-at-a-time]',
  '       node tools/merge-branch.mjs --plan <branch> [--batch <branch>…] [--full]',
  '       node tools/merge-branch.mjs --full [--keep-going] [--one-at-a-time]',
  '       node tools/merge-branch.mjs --full --beside [<commit>] [--keep-going] [--one-at-a-time]',
  '       node tools/merge-branch.mjs --full --beside --daoris-verify [--keep-going] [--one-at-a-time]',
  '       node tools/merge-branch.mjs --ci [--stage | --night] [<commit>] [--one-at-a-time]',
  '       node tools/merge-branch.mjs --ci --watch [--one-at-a-time]',
  '       node tools/merge-branch.mjs --ci --stop',
  '       node tools/merge-branch.mjs --passed [--light]',
  '       node tools/merge-branch.mjs --prune [--plan]',
  '       node tools/merge-branch.mjs <branch> --preview',
  '       node tools/merge-branch.mjs <branch> --message <file under local/> [--one-at-a-time] [--no-commit-check]',
  '       node tools/merge-branch.mjs --queue [--recheck]',
  '       node tools/merge-branch.mjs --drop-batch [<branch> [--batch <branch>…]]',
].join('\n');

/** Why the tool stopped before doing its job. Exit 2 is a refusal; 1 is the branch failing a rule. */
export class Refusal extends Error {
  constructor(message, exitCode = 2, usage = false) {
    super(message);
    this.exitCode = exitCode;
    this.usage = usage;
  }
}

const usageError = (message) => new Refusal(message, 2, true);

export function parseArgs(argv) {
  const options = {
    branches: [], keepGoing: false, commitCheck: true, resume: false, dropBatch: false, plan: false, prune: false, autoPrune: true,
    full: false, gated: false, rerun: [], passed: false, light: false, stale: false, oneAtATime: false, beside: false, besideCommit: null,
    ci: false, ciCommit: null, watch: false, stop: false, ciStage: false, ciNight: false,
    preview: false, messageFile: null, queue: false, recheck: false, queueRun: false, daorisVerify: false,
  };
  let batch = false;
  let rerun = false;
  let positional = 0;
  let batched = 0;
  let besideNext = false;
  let ciNext = false;
  let messageNext = false;
  const flags = [];
  for (const arg of argv) {
    // The word after --message is its file.
    if (messageNext) {
      messageNext = false;
      if (arg.startsWith('-')) throw usageError('--message names its file: --message <file under local/>');
      options.messageFile = arg;
      continue;
    }
    if (arg.startsWith('-')) flags.push(arg);
    // The word right after --beside or --ci, if it is no option, is the commit it gates.
    const besideCommit = besideNext && !arg.startsWith('-');
    const ciCommit = ciNext && !arg.startsWith('-');
    besideNext = false;
    ciNext = false;
    if (besideCommit && !rerun) options.besideCommit = arg;
    else if (ciCommit && !rerun) options.ciCommit = arg;
    else if (arg === '--ci') {
      if (options.ci) throw usageError('--ci is given once, with the commit it gates after it');
      options.ci = true;
      ciNext = true;
    } else if (arg === '--stage') {
      // The stage's path, its commit right after it as after --ci.
      options.ciStage = true;
      ciNext = !options.ciCommit;
    } else if (arg === '--night') {
      // The nightly run by hand, every gate whole, its commit right after it as after --ci.
      options.ciNight = true;
      ciNext = !options.ciCommit;
    } else if (arg === '--watch') options.watch = true;
    else if (arg === '--stop') options.stop = true;
    else if (arg === '--preview') options.preview = true;
    else if (arg === '--message') {
      if (options.messageFile !== null) throw usageError('--message is given once');
      messageNext = true;
    } else if (arg === '--queue') options.queue = true;
    else if (arg === '--recheck') options.recheck = true;
    else if (arg === '--queue-run') options.queueRun = true;
    else if (arg === '--daoris-verify') options.daorisVerify = true;
    else if (arg === '--beside') {
      if (options.beside) throw usageError('--beside is given once, with the commit it gates after it');
      options.beside = true;
      besideNext = true;
    } else if (arg === '--batch') {
      if (batch) throw usageError('--batch is given once, before the branches it adds');
      if (positional === 0) throw usageError('--batch follows the first branch: <branch> --batch <branch>…');
      batch = true;
    } else if (arg === '--keep-going') options.keepGoing = true;
    else if (arg === '--one-at-a-time') options.oneAtATime = true;
    else if (arg === '--no-commit-check') options.commitCheck = false;
    else if (arg === '--continue') options.resume = true;
    else if (arg === '--drop-batch') options.dropBatch = true;
    else if (arg === '--plan') options.plan = true;
    else if (arg === '--prune') options.prune = true;
    else if (arg === '--no-prune') options.autoPrune = false;
    else if (arg === '--no-timeout') options.noTimeout = true;
    else if (arg === '--full') options.full = true;
    else if (arg === '--gated') options.gated = true;
    else if (arg === '--passed') options.passed = true;
    else if (arg === '--light') options.light = true;
    else if (arg === '--stale') options.stale = true;
    else if (arg === '--rerun') rerun = true;
    else if (arg.startsWith('-')) throw usageError(`unknown option '${arg}'`);
    else if (rerun) {
      // What follows --rerun are gates, not branches.
      if (options.rerun.includes(arg)) throw usageError(`'${arg}' is named twice`);
      options.rerun.push(arg);
    } else {
      if (batch) batched += 1;
      else if (++positional > 1) throw usageError('two branches without --batch: name the rest after --batch');
      if (options.branches.includes(arg)) throw usageError(`'${arg}' is named twice`);
      options.branches.push(arg);
    }
  }
  if (messageNext) throw usageError('--message names its file: --message <file under local/>');
  // The queue's verbs, each alone with what it takes.
  if (options.recheck && !options.queue) throw usageError("--recheck is the queue's: --queue --recheck rechecks a stop on the base");
  if (options.queueRun) {
    if (flags.length > 1 || options.branches.length) throw usageError('--queue-run is the runner, started by the queue: it takes nothing else');
    return options;
  }
  if (options.queue) {
    if (flags.some((flag) => !['--queue', '--recheck'].includes(flag)) || options.branches.length) throw usageError('--queue takes only --recheck: it says the queue, or rechecks its stop');
    return options;
  }
  if (options.preview) {
    if (options.branches.length !== 1 || flags.length > 1) throw usageError('--preview names one branch and nothing else: <branch> --preview');
    return options;
  }
  if (options.messageFile !== null) {
    if (options.branches.length !== 1) throw usageError('--message queues one branch: <branch> --message <file>');
    if (flags.some((flag) => !['--message', '--one-at-a-time', '--no-commit-check'].includes(flag))) throw usageError('<branch> --message <file> takes only --one-at-a-time and --no-commit-check');
    return options;
  }
  if (options.dropBatch && options.branches.length) {
    if (flags.some((flag) => !['--drop-batch', '--batch'].includes(flag))) throw usageError('--drop-batch <branch> [--batch <branch>…] takes queued branches off the queue, and nothing else');
    return options;
  }
  if (options.watch && !options.ci) throw usageError("--watch is the CI's: --ci --watch gates main's tip as it moves");
  if (options.stop && !options.ci) throw usageError("--stop is the CI watch's: --ci --stop ends a watch running here");
  if (options.ciStage && !options.ci) throw usageError("--stage is the CI's: --ci --stage [<commit>] gates a commit to stage, its rehearsals with it");
  if (options.ciNight && !options.ci) throw usageError("--night is the CI's: --ci --night [<commit>] runs every gate whole, as the nightly run does");
  if (options.ci) {
    // One run of the CI, a run for a stage, its watch, or the stop asked of the watch; each a verb alone.
    // A plain CI run always keeps going and a stage's ends at its first failure, so the flag would change nothing; refused rather than ignored.
    if (flags.includes('--keep-going')) throw usageError('--keep-going is not taken beside --ci: a plain CI run keeps going past a failure anyway, and a --stage run ends at its first failure by design');
    if (flags.some((flag) => !['--ci', '--stage', '--night', '--watch', '--stop', '--one-at-a-time'].includes(flag))) {
      throw usageError('--ci takes only a commit, --stage, --night, --watch, --stop and --one-at-a-time');
    }
    if (options.ciNight && (options.ciStage || options.watch)) throw usageError('--ci --night is a run of its own: not with --stage or --watch, whose watch runs the night itself');
    if (flags.filter((flag) => flag === '--stage').length > 1) throw usageError('--stage is given once');
    const words = Boolean(options.ciCommit) || options.branches.length > 0;
    if (options.stop && (options.watch || words || flags.length > 2)) throw usageError('--ci --stop takes nothing else: it asks the watch running here to end');
    if (options.watch && words) throw usageError("--ci --watch gates main's tip as it moves: it takes no commit");
    if (options.watch && options.ciStage) {
      throw usageError("--ci --watch owes the stage's rehearsals: --ci --stage [<commit>] runs them once, on the commit to stage");
    }
    if (options.branches.length) throw usageError("--ci takes no branch: it gates a commit, main's tip unless named, in the gate tree");
    return options;
  }
  // The flag alone makes `--full --beside` the declared runner; anywhere else it is a mistake.
  if (options.daorisVerify && !(options.full && options.beside)) {
    throw usageError("--daoris-verify is the declared runner's: --full --beside --daoris-verify, its inputs in DAORIS_VERIFY_*");
  }
  if (options.beside) {
    if (!options.full) throw usageError('--beside runs the full set: --full --beside [<commit>]');
    if (options.branches.length) throw usageError("--beside takes no branch: it gates a commit, main's tip unless named, in a worktree of its own");
    if (flags.some((flag) => !['--full', '--beside', '--daoris-verify', '--keep-going', '--one-at-a-time'].includes(flag))) {
      throw usageError('--full --beside takes only a commit, --keep-going and --one-at-a-time (and --daoris-verify, which takes its commit from DAORIS_VERIFY_COMMIT)');
    }
    if (options.daorisVerify && options.besideCommit) {
      throw usageError('--daoris-verify takes its commit from DAORIS_VERIFY_COMMIT: name none beside --beside');
    }
  }
  // `--plan` is a verb of its own, or the prune's plan; `--full` is one when it names no branch and modifies nothing else.
  const fullAlone = options.full && options.branches.length === 0 && !options.plan && !options.resume && !options.prune && !rerun && !options.stale;
  const verbs = [options.resume, options.dropBatch, options.plan && !options.prune, options.prune, rerun, options.passed, options.stale, fullAlone];
  if (verbs.filter(Boolean).length > 1) {
    throw usageError('give one of --continue, --rerun, --stale, --drop-batch, --plan, --passed, --prune (which takes --plan) and --full on its own');
  }
  if (options.light && !options.passed) throw usageError('--light is for --passed: the light stage asks whether the gates before the rehearsals passed');
  if (options.full && options.gated) throw usageError('--full and --gated are alternatives: --full runs every gate, --gated the reached ones');
  if (batch && batched === 0) throw usageError('--batch names at least one branch after it');
  if (rerun) {
    if (options.rerun.length === 0) throw usageError('--rerun names the gates to run again: --rerun <gate>…');
    if (flags.some((flag) => !['--rerun', '--keep-going', '--one-at-a-time', '--no-timeout'].includes(flag))) {
      throw usageError('--rerun takes gate names, --keep-going, --one-at-a-time and --no-timeout');
    }
    if (options.branches.length) throw usageError('--rerun takes no branch: it acts on the merge in place, or on the checkout when none is open');
  } else if (options.stale) {
    if (options.branches.length) throw usageError('--stale takes no branch: it acts on the checkout as it stands');
    if (flags.some((flag) => !['--stale', '--keep-going', '--one-at-a-time'].includes(flag))) {
      throw usageError('--stale takes only --keep-going and --one-at-a-time');
    }
  } else if (options.passed) {
    if (options.branches.length) throw usageError('--passed takes no branch: it asks about the checkout as it stands');
    if (flags.filter((flag) => flag !== '--light').length > 1) throw usageError('--passed takes nothing else: it only reads the verdicts');
  } else if (options.prune) {
    if (!options.autoPrune) throw usageError('--no-prune is for a merge; --prune is the prune itself');
    if (options.full) throw usageError('--full is for a merge or the checkout; --prune runs no gate');
    if (options.gated) throw usageError('--gated is for a merge; --prune runs no gate');
    if (options.oneAtATime) throw usageError('--one-at-a-time is for a run of gates; --prune runs no gate');
    if (options.branches.length) throw usageError('--prune takes no branch: it acts on every branch merged into main');
  } else if (options.resume || options.dropBatch) {
    if (options.branches.length) {
      throw usageError(`${options.resume ? '--continue' : '--drop-batch'} takes no branch: it acts on the batch already recorded`);
    }
  } else if (options.branches.length === 0 && !(options.full && !options.plan)) {
    // `--full` alone gates the checkout as it stands; everything else names a branch.
    throw usageError('name the branch to merge');
  }
  return options;
}
