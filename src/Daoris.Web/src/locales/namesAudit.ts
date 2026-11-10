/** The budget judgements the names report leaves for the window, each a name whose length is what it says. */
export const BUDGET_LEFT = [
  // A grant's box names each act it lets through, which a shorter name would leave out.
  'help.grants.op.acts.pause:en', 'help.grants.op.acts.pause:zh',
  'help.setup:en', 'help.setup:zh',
  // The chat-flow design's own words for the press past the look (§5), a menu row that wraps rather than clipping.
  'review.act.withoutLook:en',
  'scope.every:en', 'scope.every:zh',
  'signin.titleNew:en', 'signin.titleNew:zh',
  // The owner's own name for the act, a menu row that wraps in the ⋯ rather than clipping.
  'work.act.handOver:en',
  'work.group.noCheckout:en',
];

/** The sentences the audit found saying a word that rightly names another thing than the term that avoids it. */
export const STRAY_AUDITED = [
  'errors.HARNESS_ACTION_IDLE:zh', 'errors.INVALID_PAYLOAD_VALUE:zh',
  // 主目录 is a home's word, the Daoris home's and an agent's own, never a folder's.
  'help.proposal.plugin.copied:zh',
  // 焦点 is the keyboard's focus, which the focus term's own reason names as another thing.
  'menu.keys.field:zh',
  // 远程 here is the act's own: a git act on a git remote, which is no Daoris remote.
  'person.route.GIT_ANSWER:zh',
  'person.view.browser-use-edge:en',
  // 改动 is the diff's changes, which review's own reason names as another thing.
  'projects.folder.noGit:zh', 'projects.manage.folderNote:zh',
  // An appointment's scope, a permission rule's and the window's workspace: none is a conversation's focus.
  'projects.supervisor.refused.scope:en',
  // 远端 is the glossary's own word for a Daoris remote, which this sentence means; git remote's entry avoids it (FABRIC-CLOSE1i2).
  'projects.workspace.machines.none:zh',
  'settings.browser.which.hintDaoris:en', 'settings.browser.which.hintEdge:en',
  'settings.daoris.toolHome.absent:zh', 'settings.daoris.toolHome.caseFolded:zh', 'settings.daoris.toolHome.listWhy.inside-daoris:zh',
  'settings.daoris.toolHome.listWhy.unread:zh', 'settings.daoris.toolHome.what:zh',
  'settings.rules.why:en',
  // A subagent its hook said stopped, which says no outcome, is 已结束, never done's 已完成 (the cowork view design §5.4).
  'work.atWork.child.finished:zh',
  'work.status.indexedTip:en',
];
