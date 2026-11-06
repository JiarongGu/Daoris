namespace Daoris.Driver;

/// <summary>
/// The build's own migrations of <see cref="DesktopStore"/>: numbered steps in code, forward only. A step that adds only tables or
/// nullable columns an older build never names leaves the writer stamp where it is; one that raises it says so.
/// </summary>
internal static class DesktopStoreSteps
{
    private const int FirstWriter = DesktopStore.FirstWriter;

    private const int DeliveryWriter = DesktopStore.DeliveryWriter;

    private const int PolicyWriter = DesktopStore.PolicyWriter;

    private const string StandingMoves = DesktopStore.StandingMoves;

    /// <summary>The steps, contiguous from 1: version 1 makes the store's four tables.</summary>
    public static IReadOnlyList<StoreMigration> All { get; } =
    [
        new(1, FirstWriter, write => write.Execute(
            """
            CREATE TABLE store_version (
              one     INTEGER PRIMARY KEY CHECK (one = 1),
              version INTEGER NOT NULL,
              writer  INTEGER NOT NULL,
              build   TEXT NOT NULL,
              at      TEXT NOT NULL
            );
            CREATE TABLE store_revisions (
              family   TEXT PRIMARY KEY,
              revision INTEGER NOT NULL
            );
            CREATE TABLE store_runs (
              run     TEXT PRIMARY KEY,
              kind    TEXT NOT NULL,
              pid     INTEGER NOT NULL,
              started INTEGER,
              began   TEXT NOT NULL
            );
            CREATE TABLE imports (
              id     INTEGER PRIMARY KEY AUTOINCREMENT,
              family TEXT NOT NULL,
              path   TEXT NOT NULL,
              size   INTEGER NOT NULL,
              sha256 TEXT NOT NULL,
              at     TEXT NOT NULL,
              build  TEXT NOT NULL,
              rows   INTEGER NOT NULL,
              class  TEXT NOT NULL
            );
            CREATE INDEX imports_family ON imports (family, path);
            """)),

        // Row z-iii: the participants' bindings. A new table, every column required, which a build of z-i never opens, so the stamp stays
        // where it is and the step runs at the first start (§13.3); later rows add their own columns nullable, each writer naming its own.
        new(2, FirstWriter, write => write.Execute(
            """
            CREATE TABLE bindings (
              session      TEXT PRIMARY KEY,
              adapter      TEXT NOT NULL,
              conversation TEXT NOT NULL,
              kept         TEXT NOT NULL
            );
            """)),

        // Row a-ii: whether the person picked a session's account, kept with its binding (§13.3). A new table, every column required, so
        // a build of z-iii, which reads `bindings` as a conversation to resume, never meets a row with no conversation in it, and the
        // stamp stays where it is (`compatible-builds.md`): the pick is known when a start opens, the conversation's id only when the wire names it.
        new(3, FirstWriter, write => write.Execute(
            """
            CREATE TABLE binding_picks (
              session TEXT PRIMARY KEY,
              account TEXT NOT NULL,
              at      TEXT NOT NULL
            );
            """)),

        // Row f-i: the course's move record (§3.3, §13.3's family *The course*). New tables an older store-aware build never opens, so the
        // stamp stays where it is and the step runs at the first start. One standing move per record left by the partial unique index, a
        // line moving again once its move has ended and the ended row kept as evidence; the claim names the run that holds it, a row of
        // `store_runs`, and its generation. A column every decision writes is required; each later phase's moment and what it carries, the
        // effect mark, the selection and the recipe are nullable. No phase or reason is held by a CHECK, so a newer build's word is kept as written.
        new(4, FirstWriter, write => write.Execute(
            $"""
            CREATE TABLE moves (
              id               INTEGER PRIMARY KEY AUTOINCREMENT,
              record_left      TEXT NOT NULL,
              executor         TEXT NOT NULL,
              how              TEXT NOT NULL,
              from_why         TEXT NOT NULL,
              from_by          TEXT NOT NULL,
              phase            TEXT NOT NULL,
              claim_run        TEXT NOT NULL REFERENCES store_runs (run),
              claim_generation INTEGER NOT NULL,
              account          TEXT,
              recipe_why       TEXT,
              recipe_bound     INTEGER,
              opens            TEXT,
              destination      TEXT,
              effect           TEXT,
              effect_begun     TEXT,
              effect_outcome   TEXT,
              effect_ended     TEXT,
              fell_back_code   TEXT,
              held_why         TEXT,
              closed_why       TEXT,
              decided_at       TEXT NOT NULL,
              quiescing_at     TEXT,
              old_ended_at     TEXT,
              opening_at       TEXT,
              opened_at        TEXT,
              copied_at        TEXT,
              verified_at      TEXT,
              delivering_at    TEXT,
              delivered_at     TEXT,
              fell_back_at     TEXT,
              held_at          TEXT,
              closed_at        TEXT
            );
            CREATE UNIQUE INDEX moves_standing ON moves (record_left) WHERE {StandingMoves};
            CREATE INDEX moves_left ON moves (record_left, id);
            CREATE TABLE move_words (
              move        INTEGER NOT NULL REFERENCES moves (id),
              transfer    TEXT NOT NULL,
              word        TEXT NOT NULL,
              producer    TEXT,
              position    INTEGER NOT NULL,
              disposition TEXT,
              disposed_at TEXT,
              PRIMARY KEY (move, transfer)
            );
            """)),

        // Row f-i part 2: what a box an older build wrote carries that step 4's columns do not, and a quiet move's telling (§3.3, §13.5,
        // §19.1). Each nullable, added to the table a step-4 build already writes, which names its own columns and leaves these empty, so the
        // stamp stays where it is and the step runs at the first start: the agent a hand-over opens its new part on, a limit's refused
        // turns' bound, and the phase moment a quiet move was last said quiet for, so two processes looking say it once between them.
        new(5, FirstWriter, write => write.Execute(
            """
            ALTER TABLE moves ADD COLUMN agent TEXT;
            ALTER TABLE moves ADD COLUMN refused_bound INTEGER;
            ALTER TABLE moves ADD COLUMN quiet_at TEXT;
            """)),

        // INTEGRATE1e part e2: the integration's family (the integration design §5.6, D212 §13), Workplace's. New tables an older
        // store-aware build never opens, so the stamp stays where it is and the step runs at the first start. One open request per
        // repository and branch, one standing candidate per line, one standing membership per request and one open step per candidate, each
        // by a partial unique index the write meets; one job per candidate by its own. A column every row of its table carries is
        // required; a route's own (a records branch has no unit, session, quest or proof), a requester's (only the steward's act names an
        // act) and a later step's (the candidate's tip, a step's end) are nullable. No CHECK holds a word, so a newer build's word is kept.
        new(6, FirstWriter, write => write.Execute(
            """
            CREATE TABLE integration_requests (
              id               TEXT NOT NULL PRIMARY KEY,
              repository       TEXT NOT NULL,
              workspace        TEXT NOT NULL,
              branch           TEXT NOT NULL,
              tip              TEXT NOT NULL,
              route            TEXT NOT NULL,
              line             TEXT NOT NULL,
              requester        TEXT NOT NULL,
              door             TEXT NOT NULL,
              act              TEXT,
              generation       INTEGER,
              policy_request   TEXT NOT NULL,
              policy_commit    TEXT NOT NULL,
              policy_batch     INTEGER NOT NULL,
              policy_order     TEXT NOT NULL,
              policy_gates     TEXT NOT NULL,
              policy_unreached TEXT NOT NULL,
              policy_reached   TEXT NOT NULL,
              policy_conflicts TEXT NOT NULL,
              policy_overlap   TEXT NOT NULL,
              policy_line      TEXT,
              rule_source      TEXT,
              unit             TEXT,
              session          TEXT,
              quest            TEXT,
              landed_at        TEXT,
              proof            TEXT,
              at               TEXT NOT NULL,
              closed_at        TEXT,
              closed_why       TEXT
            );
            CREATE UNIQUE INDEX integration_requests_open ON integration_requests (repository COLLATE NOCASE, branch) WHERE closed_at IS NULL;
            CREATE INDEX integration_requests_queue ON integration_requests (repository COLLATE NOCASE, line, at);
            CREATE TABLE integration_candidates (
              id               TEXT NOT NULL PRIMARY KEY,
              repository       TEXT NOT NULL,
              workspace        TEXT NOT NULL,
              line             TEXT NOT NULL,
              base             TEXT NOT NULL,
              job              TEXT NOT NULL,
              claim_run        TEXT NOT NULL REFERENCES store_runs (run),
              claim_generation INTEGER NOT NULL,
              formed_at        TEXT NOT NULL,
              tip              TEXT,
              promoted_at      TEXT,
              ended_at         TEXT,
              ended_why        TEXT
            );
            CREATE UNIQUE INDEX integration_candidates_standing ON integration_candidates (repository COLLATE NOCASE, line) WHERE ended_at IS NULL;
            CREATE UNIQUE INDEX integration_candidates_job ON integration_candidates (job);
            CREATE TABLE integration_members (
              candidate    TEXT NOT NULL REFERENCES integration_candidates (id),
              request      TEXT NOT NULL REFERENCES integration_requests (id),
              position     INTEGER NOT NULL,
              state        TEXT NOT NULL,
              joined_at    TEXT NOT NULL,
              replayed     TEXT,
              contained_by TEXT REFERENCES integration_requests (id),
              said         TEXT,
              ended_at     TEXT,
              PRIMARY KEY (candidate, request)
            );
            CREATE UNIQUE INDEX integration_members_standing ON integration_members (request) WHERE ended_at IS NULL;
            CREATE TABLE integration_steps (
              id               INTEGER PRIMARY KEY AUTOINCREMENT,
              candidate        TEXT NOT NULL REFERENCES integration_candidates (id),
              step             TEXT NOT NULL,
              attempt          INTEGER NOT NULL,
              claim_run        TEXT NOT NULL REFERENCES store_runs (run),
              claim_generation INTEGER NOT NULL,
              began_at         TEXT NOT NULL,
              request          TEXT REFERENCES integration_requests (id),
              line_commit      TEXT,
              ended_at         TEXT,
              outcome          TEXT,
              said             TEXT
            );
            CREATE UNIQUE INDEX integration_steps_open ON integration_steps (candidate) WHERE ended_at IS NULL;
            CREATE INDEX integration_steps_of ON integration_steps (candidate, id);
            """)),

        // INTEGRATE1e part e4b: a superseded member's pending disposition (the integration design §5.7), Workplace's. A new table an older
        // store-aware build never opens, so the stamp stays where it is and the step runs at the first start. One row for each superseded commit
        // of a request, written in the settle step's own end, so the member settling and its work being kept in view are one write; it stays
        // pending (no `answered_at`) until the session, the steward or the owner answers it. `reference` is the recovery ref under
        // `refs/daoris/discarded/` the member's branch work is kept on, `by_commit` the line's commit that superseded it.
        new(7, FirstWriter, write => write.Execute(
            """
            CREATE TABLE integration_dispositions (
              request     TEXT NOT NULL REFERENCES integration_requests (id),
              commit_id   TEXT NOT NULL,
              candidate   TEXT NOT NULL REFERENCES integration_candidates (id),
              reference   TEXT NOT NULL,
              by_commit   TEXT NOT NULL,
              files       TEXT NOT NULL,
              subject     TEXT NOT NULL,
              at          TEXT NOT NULL,
              answered_at TEXT,
              PRIMARY KEY (request, commit_id)
            );
            CREATE INDEX integration_dispositions_pending ON integration_dispositions (at) WHERE answered_at IS NULL;
            """)),

        // FOUNDATION1w-s part 1: the roles' staffing (the foundation design §19.15), Roles'. New tables an older store-aware build never
        // opens, so the stamp stays where it is and the step runs at the first start. `role_staffing` holds one row for each role that has
        // a stored entry or, for Build, Intake and Ask Daoris, a first-agent key: `state` is `before` (no entry, the key alone), `set`
        // (`choices` the entry's list, `[]` being off) or `unreadable` (`choices` the entry as the file showed it, `why` what does not
        // read). `first_agent` is the store's own key, `adapter` for Build, `intakeAdapter` for Intake and `helperAdapter` for Ask Daoris,
        // null where the file shows none. The projection's one row, `written`, and each found row hold `driver.json`'s four staffing paths
        // as that file's own JSON text, compact, a null being a path the file does not hold.
        new(8, FirstWriter, write => write.Execute(
            """
            CREATE TABLE role_staffing (
              role        TEXT NOT NULL PRIMARY KEY,
              state       TEXT NOT NULL,
              choices     TEXT,
              why         TEXT,
              first_agent TEXT,
              at          TEXT NOT NULL
            );
            CREATE TABLE role_staffing_projection (
              one            INTEGER PRIMARY KEY CHECK (one = 1),
              actors         TEXT,
              adapter        TEXT,
              intake_adapter TEXT,
              helper_adapter TEXT,
              at             TEXT NOT NULL
            );
            CREATE TABLE role_staffing_found (
              id                     INTEGER PRIMARY KEY AUTOINCREMENT,
              at                     TEXT NOT NULL,
              actors                 TEXT,
              adapter                TEXT,
              intake_adapter         TEXT,
              helper_adapter         TEXT,
              written_actors         TEXT,
              written_adapter        TEXT,
              written_intake_adapter TEXT,
              written_helper_adapter TEXT,
              answer                 TEXT,
              answered_at            TEXT
            );
            CREATE INDEX role_staffing_found_pending ON role_staffing_found (id) WHERE answered_at IS NULL;
            """)),

        // FOUNDATION1z-v: the news (§4.5, §13.3), Events'. New tables an older store-aware build never opens, so the stamp stays where it
        // is and the step runs at the first start. `notices` holds `data/notices.json`'s notices by id, one column for each field the page
        // reads; `news_said` the occurrences already told, apart from the notices so a prune or a dismissal never forgets one; and
        // `journal_cursors` one journal position for each teller, with the store identity it was read against.
        new(9, FirstWriter, write => write.Execute(
            """
            CREATE TABLE notices (
              id         TEXT NOT NULL PRIMARY KEY,
              kind       TEXT NOT NULL,
              at         TEXT NOT NULL,
              read       INTEGER NOT NULL,
              workspace  TEXT,
              session    TEXT,
              quest      TEXT,
              repository TEXT,
              state      TEXT,
              route      TEXT,
              act        TEXT,
              code       TEXT,
              count      INTEGER
            );
            CREATE INDEX notices_at ON notices (at);
            CREATE TABLE news_said (
              key TEXT NOT NULL PRIMARY KEY,
              at  TEXT NOT NULL
            );
            CREATE TABLE journal_cursors (
              teller   TEXT NOT NULL PRIMARY KEY,
              store    TEXT NOT NULL,
              position INTEGER NOT NULL,
              at       TEXT NOT NULL
            );
            """)),

        // FOUNDATION1w: the role catalogue (§2.7, §13.3), Roles'. New tables an older store-aware build never opens, so the stamp stays where
        // it is. `roles` holds one row for each role id the person changed: `state` is `set` (`record` the role's JSON, as written) or
        // `removed` (a seeded role taken out); an id with no row is the seed's. `role_revisions` keeps every revision of every id, never
        // rewritten and never reset by a removal; its `state` adds `default` (a seeded id's record deleted) and `deleted` (an added one's).
        new(10, FirstWriter, write => write.Execute(
            """
            CREATE TABLE roles (
              id       TEXT NOT NULL PRIMARY KEY,
              state    TEXT NOT NULL,
              record   TEXT NOT NULL,
              revision INTEGER NOT NULL,
              at       TEXT NOT NULL
            );
            CREATE TABLE role_revisions (
              id       TEXT NOT NULL,
              revision INTEGER NOT NULL,
              state    TEXT NOT NULL,
              record   TEXT,
              at       TEXT NOT NULL,
              PRIMARY KEY (id, revision)
            );
            """)),

        // FOUNDATION1x: the role a start took and its revision, kept with its binding (§2.7, §13.3's Bindings family). A new table, as
        // `binding_picks` was, so the role is known when a start opens while the conversation's id comes only when the wire names it, a
        // build that knows no role never opens it, and each writer of a binding names its own columns (O3-16). The stamp stays.
        new(11, FirstWriter, write => write.Execute(
            """
            CREATE TABLE binding_roles (
              session  TEXT PRIMARY KEY,
              role     TEXT NOT NULL,
              revision INTEGER NOT NULL,
              at       TEXT NOT NULL
            );
            """)),

        // INTEGRATE1h: the owner's decide on a candidate held for one, a queued request's place, and a disposition's answer (the integration
        // design §5.7, §5.8, §8), Workplace's. A new table and two nullable columns an older build names none of, so the stamp stays where it
        // is: an older build orders its queue by arrival and answers a disposition with its moment alone. One decide for each candidate.
        new(12, FirstWriter, write => write.Execute(
            """
            CREATE TABLE integration_decides (
              candidate TEXT NOT NULL PRIMARY KEY REFERENCES integration_candidates (id),
              decide    TEXT NOT NULL,
              requester TEXT NOT NULL,
              at        TEXT NOT NULL
            );
            ALTER TABLE integration_requests ADD COLUMN queue_order INTEGER;
            ALTER TABLE integration_dispositions ADD COLUMN answer TEXT;
            """)),

        // GITAPP2a: the act journal (the git-writes design §5, D204), Workplace's. New tables an older build never opens, so the stamp stays
        // where it is; a record carries its own `format`, and one past a build's is held by it, never reconciled. One open act per checkout
        // (`common_dir`, the checkout lock's key), one job per act; each step's start is written before its effect and its end after.
        new(13, FirstWriter, write => write.Execute(
            """
            CREATE TABLE git_acts (
              id               TEXT NOT NULL PRIMARY KEY,
              format           INTEGER NOT NULL,
              kind             TEXT NOT NULL,
              press            TEXT NOT NULL,
              confirmed        TEXT,
              repository       TEXT NOT NULL,
              checkout         TEXT NOT NULL,
              common_dir       TEXT NOT NULL,
              plan             TEXT NOT NULL,
              job              TEXT NOT NULL,
              claim_run        TEXT NOT NULL REFERENCES store_runs (run),
              claim_generation INTEGER NOT NULL,
              phase            TEXT NOT NULL,
              recovery         TEXT,
              outcome          TEXT,
              said             TEXT,
              at               TEXT NOT NULL,
              reconciled_at    TEXT,
              closed_at        TEXT,
              expires_at       TEXT,
              expired_at       TEXT
            );
            CREATE UNIQUE INDEX git_acts_open ON git_acts (common_dir) WHERE closed_at IS NULL;
            CREATE UNIQUE INDEX git_acts_job ON git_acts (job);
            CREATE INDEX git_acts_expiring ON git_acts (expires_at) WHERE expired_at IS NULL;
            CREATE TABLE git_act_steps (
              act            TEXT NOT NULL REFERENCES git_acts (id),
              ordinal        INTEGER NOT NULL,
              kind           TEXT NOT NULL,
              arguments      TEXT NOT NULL,
              postconditions TEXT NOT NULL,
              rollback       TEXT,
              began_at       TEXT,
              deadline       TEXT,
              pid            INTEGER,
              pid_started    INTEGER,
              ended_at       TEXT,
              exit_code      INTEGER,
              unended        INTEGER,
              outcome        TEXT,
              said           TEXT,
              PRIMARY KEY (act, ordinal)
            );
            """)),

        // LANDNODE1g: the line's epoch (the land node design §5), Workplace's. A new table an older build never opens, so the stamp stays where
        // it is; a request or candidate with no row reads as its own epoch. An older build still forms candidates by its first request's
        // policy alone, which this build cannot stop: the remainder D215's note keeps.
        new(14, FirstWriter, write => write.Execute(
            """
            CREATE TABLE integration_epochs (
              subject TEXT NOT NULL,
              id      TEXT NOT NULL,
              epoch   TEXT NOT NULL,
              PRIMARY KEY (subject, id)
            );
            """)),

        // LANDNODE1b: the delivery record (the land node design §3, §4), Workplace's, and the first step that raises the writer stamp. From 2
        // an older build reads the store and its every write is refused inside its transaction, so none writes beside a delivery's phases; the
        // step waits for the start's confirmation as every raise does (§13.3, §13.5). One unresolved delivery per unit by the partial unique
        // index, its key the digest of the unit, the bound tip, the method and the target; one phase entry without its outcome per delivery by
        // its own. What a later phase or a settlement sets is nullable, and no CHECK holds a word, so a newer build's is kept as written.
        new(15, DeliveryWriter, write => write.Execute(
            """
            CREATE TABLE deliveries (
              key              TEXT NOT NULL PRIMARY KEY,
              format           INTEGER NOT NULL,
              unit             TEXT NOT NULL,
              repository       TEXT NOT NULL,
              tip              TEXT NOT NULL,
              method           TEXT NOT NULL,
              target           TEXT NOT NULL,
              line             TEXT NOT NULL,
              branch           TEXT,
              ticket           TEXT,
              message          TEXT NOT NULL,
              plugin           TEXT,
              epoch            TEXT NOT NULL,
              authority        TEXT NOT NULL,
              accept           TEXT,
              scope            TEXT,
              revision         TEXT NOT NULL,
              graph            TEXT NOT NULL,
              options          TEXT NOT NULL,
              receipts         TEXT NOT NULL,
              claim_run        TEXT NOT NULL REFERENCES store_runs (run),
              claim_generation INTEGER NOT NULL,
              at               TEXT NOT NULL,
              settled          TEXT,
              settled_at       TEXT,
              said             TEXT
            );
            CREATE UNIQUE INDEX deliveries_unresolved ON deliveries (unit) WHERE settled_at IS NULL;
            CREATE TABLE delivery_phases (
              delivery TEXT NOT NULL REFERENCES deliveries (key),
              ordinal  INTEGER NOT NULL,
              phase    TEXT NOT NULL,
              intended TEXT,
              began_at TEXT NOT NULL,
              ended_at TEXT,
              outcome  TEXT,
              evidence TEXT,
              said     TEXT,
              PRIMARY KEY (delivery, ordinal)
            );
            CREATE UNIQUE INDEX delivery_phases_open ON delivery_phases (delivery) WHERE ended_at IS NULL;
            """)),

        // STAFFPOLICY1a: a role's staffing policy, its revisions and the role's new starts (the staff policy design §2.1, §11), Roles'. The
        // second step to raise the writer stamp: an older build reads `role_staffing` and would start a choice past its budget or outside the
        // works it takes, so from 3 its every write is refused; the step waits for the start's confirmation as every raise does. `staffing` is
        // the digest of the choices the policy was set for; a revision with no policy is a deletion; `choice` keeps the choice's tuple.
        new(16, PolicyWriter, write => write.Execute(
            """
            CREATE TABLE role_policy (
              role     TEXT NOT NULL PRIMARY KEY,
              policy   TEXT NOT NULL,
              staffing TEXT NOT NULL,
              revision INTEGER NOT NULL,
              since    TEXT NOT NULL,
              at       TEXT NOT NULL
            );
            CREATE TABLE role_policy_revisions (
              role     TEXT NOT NULL,
              revision INTEGER NOT NULL,
              policy   TEXT,
              staffing TEXT,
              at       TEXT NOT NULL,
              PRIMARY KEY (role, revision)
            );
            CREATE TABLE role_starts (
              session   TEXT NOT NULL PRIMARY KEY,
              role      TEXT NOT NULL,
              choice    TEXT NOT NULL,
              by        TEXT NOT NULL,
              condition TEXT,
              at        TEXT NOT NULL
            );
            CREATE INDEX role_starts_role ON role_starts (role, at);
            """)),

        // STAFFPOLICY1e: a line's moves to another choice and the person's approvals of a policy's ask (the staff policy design §4.2, §11 point 5),
        // Roles'. Keyed by the line's identity (a quest's id, a conversation's), so a restart keeps the count and an in-start replacement, which
        // writes no part link, is counted beside a part's. No raise: an older build reads no table it does not know.
        new(17, PolicyWriter, write => write.Execute(
            """
            CREATE TABLE role_moves (
              id        INTEGER PRIMARY KEY AUTOINCREMENT,
              line      TEXT NOT NULL,
              role      TEXT NOT NULL,
              kind      TEXT NOT NULL,
              session   TEXT,
              from_choice TEXT,
              to_choice TEXT,
              condition TEXT,
              action    TEXT,
              at        TEXT NOT NULL
            );
            CREATE INDEX role_moves_line ON role_moves (line, at);
            """)),

        // STAFFPOLICY1d (the staff policy design §11 point 4): the model and effort of the choice a hand-over was offered and approved on, kept
        // with the move so a restart takes it as approved and never picks again. Each nullable, added to the table a step-16 build already
        // writes, which names its own columns and leaves these empty, so the stamp stays where it is and the step runs at the first start.
        new(18, PolicyWriter, write => write.Execute(
            """
            ALTER TABLE moves ADD COLUMN model TEXT;
            ALTER TABLE moves ADD COLUMN effort TEXT;
            """)),

        // CONTEXTDRIVE1c (the context counters design §4.4): a place's supervisor rules, one row per rule, and each place's set as written with
        // its revision, Roles'. No raise: an older build reads no table it does not know and drives by the numbers compiled into it.
        new(19, PolicyWriter, write => write.Execute(
            """
            CREATE TABLE supervisor_rules (
              place    TEXT NOT NULL,
              ord      INTEGER NOT NULL,
              id       TEXT NOT NULL,
              roles    TEXT,
              count    TEXT NOT NULL,
              window   TEXT NOT NULL,
              at       INTEGER NOT NULL,
              do       TEXT NOT NULL,
              up_to    INTEGER,
              revision INTEGER NOT NULL,
              PRIMARY KEY (place, ord)
            );
            CREATE TABLE supervisor_rule_revisions (
              place    TEXT NOT NULL,
              revision INTEGER NOT NULL,
              rules    TEXT NOT NULL,
              at       TEXT NOT NULL,
              door     TEXT NOT NULL,
              PRIMARY KEY (place, revision)
            );
            """)),

        // COMMS1g (the comms design §5.3, §13 point 8): the answers-alone count, one admission per line keyed by its one key, Course's. A line's
        // reading of its events (what it read of each part, and the prompt it read last), its epochs (the seed, then each reset), each go-on
        // with its disposition, and when the admissions began, which ends the seed's kept read. No raise: an older build reads no table it does
        // not know and counts from the events, and a line it went on meanwhile is read on from its events at the next read.
        new(20, PolicyWriter, write => write.Execute(
            """
            CREATE TABLE continuation_lines (
              line  TEXT PRIMARY KEY,
              read  TEXT NOT NULL,
              carry TEXT NOT NULL
            );
            CREATE TABLE continuation_epochs (
              line   TEXT NOT NULL,
              epoch  INTEGER NOT NULL,
              cause  TEXT NOT NULL,
              source TEXT,
              n      INTEGER NOT NULL,
              known  TEXT NOT NULL,
              why    TEXT,
              since  TEXT NOT NULL,
              PRIMARY KEY (line, epoch)
            );
            CREATE TABLE continuation_admissions (
              id        INTEGER PRIMARY KEY AUTOINCREMENT,
              line      TEXT NOT NULL,
              epoch     INTEGER NOT NULL,
              source    TEXT NOT NULL,
              words     TEXT NOT NULL,
              session   TEXT,
              reserved  TEXT NOT NULL,
              attempted TEXT,
              taken     TEXT,
              cancelled TEXT,
              uncertain TEXT
            );
            CREATE INDEX continuation_admissions_line ON continuation_admissions (line, epoch);
            CREATE TABLE continuation_made (
              one INTEGER PRIMARY KEY CHECK (one = 1),
              at  TEXT NOT NULL
            );
            INSERT INTO continuation_made (one, at) VALUES (1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            """)),

        // The context counters design §4.4: each repository's workspace as the registry placed it at the last look, Roles', so
        // the rules' judge in any process weighs a write on the chain the lines read. No raise: an older build reads no table it does not know.
        new(21, PolicyWriter, write => write.Execute(
            """
            CREATE TABLE repository_workspaces (
              repository TEXT PRIMARY KEY COLLATE NOCASE,
              workspace  TEXT NOT NULL
            );
            """)),
    ];
}
