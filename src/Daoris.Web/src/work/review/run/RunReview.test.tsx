import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import i18n from '../../../locales/i18n';
import type { NodeWordAsk, NodeWordAnswer } from '../../../workflow/run';
import { RunReview } from './RunReview';
import { ANSWER_REVIEW, BRANCH_REVIEW, COPY_REVIEW, FOLDER_REVIEW, NEWER_REVIEW, PLAN_REVIEW } from './runReviewFixtures';

// The run's review, props in: the decision bar says what is decided by kind and presses the run's node door; the changes are drawn by
// kind; a newer revision is announced and never swapped in; viewed marks follow a file's content.

const kept = (ask: NodeWordAsk): Promise<NodeWordAnswer> => Promise.resolve({ run: ask.run, node: ask.node, act: ask.act, done: true });
const marks = { isViewed: () => false, mark: vi.fn() };

describe('the run\'s review', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', FOLDER_REVIEW, 'Accept the 2 files changed in this folder during its turns: they are there already.'],
    ['en', COPY_REVIEW, 'Accept the 3 files it changed in its copy, and they are brought back into your folder.'],
    ['en', PLAN_REVIEW, 'Accept its plan of 3 changes to sample-dns, and Daoris applies it.'],
    ['en', ANSWER_REVIEW, 'It changed nothing: read its answer, and accept it.'],
    ['en', BRANCH_REVIEW, 'Accept the commits on daoris/q41.'],
    ['zh', FOLDER_REVIEW, '采纳它的回合期间这个文件夹里改动的 2 个文件：它们已经在文件夹里了。'],
    ['zh', PLAN_REVIEW, '采纳它对 sample-dns 的 3 项变更计划，随后由 Daoris 应用。'],
    ['zh', ANSWER_REVIEW, '它什么都没有改动：读完它的回答后采纳。'],
  ])('says in %s what is decided by the kind of work', async (language, answer, says) => {
    await i18n.changeLanguage(language);
    render(<RunReview answer={answer} viewed={marks} />);
    expect(screen.getByRole('region', { name: language === 'en' ? 'Review' : '审阅' }).querySelector('header p')?.textContent).toBe(says);
  });

  it('presses Accept on the record drawn, with its attempt and revision', async () => {
    const onWord = vi.fn(kept);
    render(<RunReview answer={FOLDER_REVIEW} viewed={marks} onWord={onWord} />);
    fireEvent.click(screen.getByRole('button', { name: 'Accept…' }));
    fireEvent.click(screen.getByRole('button', { name: 'Accept' }));
    await waitFor(() => expect(onWord).toHaveBeenCalledWith(expect.objectContaining({
      run: 'q41', node: 'review', act: 'accept', attempt: 2, revision: 'b7c1e09a44f2', words: null,
    })));
  });

  it('draws a branch\'s files from the record\'s own list, opened on the record\'s versions, and marks them viewed by content', () => {
    const files = [
      { path: 'src/flag.ts', change: 'modified', before: 'a1b2c3d4e5f6', after: 'f6e5d4c3b2a1', marks: [] },
      { path: 'src/new.ts', change: 'added', before: null, after: '0badc0de0badc', marks: [] },
    ];
    const body = vi.fn(() => <p>the record's versions</p>);
    const mark = vi.fn();
    render(<RunReview answer={{ ...BRANCH_REVIEW, files }} viewed={{ isViewed: () => false, mark }} fileBody={body} />);

    expect(screen.getByText('src/flag.ts')).toBeInTheDocument();
    expect(screen.getByText('src/new.ts')).toBeInTheDocument();
    expect(screen.getByText('Versions a1b2c3d → f6e5d4c')).toBeInTheDocument();
    fireEvent.click(screen.getByText('src/new.ts'));
    expect(body).toHaveBeenCalledWith(expect.objectContaining({ path: 'src/new.ts' }));
    fireEvent.click(screen.getAllByRole('checkbox', { name: 'Viewed' })[0]!);
    expect(mark).toHaveBeenCalledWith('src/flag.ts', expect.any(String), true);
  });

  it('says a refused Accept in the driver\'s own sentence inside the ask, and changes nothing', async () => {
    const refused = "the repository's own checkout is not clean (2 paths), and merging into somebody's work in flight is exactly what Daoris does not do.";
    const onWord = vi.fn((ask: NodeWordAsk): Promise<NodeWordAnswer> => Promise.resolve({ run: ask.run, node: ask.node, act: ask.act, done: false, message: refused }));
    render(<RunReview answer={BRANCH_REVIEW} viewed={marks} onWord={onWord} />);
    fireEvent.click(screen.getByRole('button', { name: 'Accept…' }));
    fireEvent.click(screen.getByRole('button', { name: 'Accept' }));
    expect(await screen.findByText(/not clean \(2 paths\)/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Accept' })).toBeInTheDocument();
  });

  it('sends back with the person\'s words to the part they choose, as the changes word', async () => {
    const onWord = vi.fn(kept);
    render(<RunReview answer={BRANCH_REVIEW} viewed={marks} onWord={onWord} />);
    fireEvent.click(screen.getByRole('button', { name: 'Send back…' }));
    const parts = screen.getByRole('radiogroup', { name: 'The part your words go to' });
    // The newest part is chosen until the person names another.
    expect(within(parts).getByRole('radio', { name: 'Part 2 · Check the totals' }).getAttribute('aria-checked')).toBe('true');
    fireEvent.click(within(parts).getByRole('radio', { name: 'Part 1 · Tidy the budget notes' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'What should change' }), { target: { value: 'Keep the old rows.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Send back' }));
    await waitFor(() => expect(onWord).toHaveBeenCalledWith(expect.objectContaining({
      run: 'q41', node: 'review', act: 'changes', words: 'Keep the old rows.', part: 'q41',
    })));
  });

  it('stops the run whole from the review, saying what stays by kind', async () => {
    const onWord = vi.fn(kept);
    render(<RunReview answer={PLAN_REVIEW} viewed={marks} onWord={onWord} />);
    fireEvent.click(screen.getByRole('button', { name: 'Stop…' }));
    expect(screen.getByText(/Nothing more is applied\./)).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Stop the run' }));
    await waitFor(() => expect(onWord).toHaveBeenCalledWith(expect.objectContaining({ act: 'stop', whole: true, node: 'review' })));
  });

  it('offers no press on a revision the review no longer stands on, and announces the newer one with a way to read it', () => {
    const onRead = vi.fn();
    render(<RunReview answer={NEWER_REVIEW} viewed={marks} onWord={vi.fn(kept)} onRead={onRead} />);
    expect(screen.queryByRole('button', { name: 'Accept…' })).toBeNull();
    expect(screen.getByText('A newer revision is ready: 2 files changed since you started reading.')).toBeTruthy();
    expect(screen.getByText(/You are reading an older revision, b7c1e09/)).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Read it' }));
    expect(onRead).toHaveBeenCalledWith(3);
  });

  it('draws the supervisor\'s held Accept on the bar, by its kind of work', () => {
    render(<RunReview answer={BRANCH_REVIEW} viewed={marks} />);
    const bar = screen.getByRole('region', { name: 'Review' }).querySelector('header')!;
    expect(within(bar as HTMLElement).getByText(/The supervisor accepted the commits on this branch/)).toBeTruthy();
  });

  it('draws a plan\'s operations, an answer as Markdown, and a folder\'s files with their versions and marks', () => {
    const { unmount } = render(<RunReview answer={PLAN_REVIEW} viewed={marks} />);
    expect(screen.getByText('record/www')).toBeTruthy();
    expect(screen.getByText('create')).toBeTruthy();
    unmount();

    const answered = render(<RunReview answer={ANSWER_REVIEW} viewed={marks} />);
    expect(screen.getByRole('heading', { name: 'What I found' })).toBeTruthy();
    answered.unmount();

    render(<RunReview answer={FOLDER_REVIEW} viewed={marks} />);
    expect(screen.getByText('notes/old-rows.csv')).toBeTruthy();
    expect(screen.getByText('Versions a11 → a12')).toBeTruthy();
    expect(screen.getByText('changed outside its turns')).toBeTruthy();
  });

  it('marks a file viewed by its content', () => {
    const mark = vi.fn();
    render(<RunReview answer={FOLDER_REVIEW} viewed={{ isViewed: (path) => path === 'notes/summary.md', mark }} />);
    expect(screen.getByText('1 of 2 viewed')).toBeTruthy();
    fireEvent.click(screen.getAllByRole('checkbox', { name: 'Viewed' })[1]);
    expect(mark).toHaveBeenCalledWith('notes/old-rows.csv', 'deleted:c31', true);
  });
});
