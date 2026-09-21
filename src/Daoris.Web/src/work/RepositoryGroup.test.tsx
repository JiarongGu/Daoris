import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../i18n';
import { RepositoryGroup } from './RepositoryGroup';

// The rail's second level (design §3): grouping is by repository because that is the axis a person
// switches on, and the header is where the REPOSITORY's own facts live — the session rows beneath
// it carry only the session's. Props-only, like every molecule here.

const row = <li>a session</li>;

describe('a repository group', () => {
  it('names the repository and how many sessions sit under it', () => {
    render(<RepositoryGroup repository="engine" count={3}>{row}</RepositoryGroup>);

    expect(screen.getByText('engine')).toBeInTheDocument();
    expect(screen.getByText('3')).toHaveAttribute('title', '3 session(s)');
    expect(screen.getByText('a session')).toBeInTheDocument();
  });

  it('says the driver works here, which is a standing choice rather than a state', () => {
    render(<RepositoryGroup repository="engine" count={1} drivable>{row}</RepositoryGroup>);
    expect(screen.getByText('drives here')).toBeInTheDocument();
  });

  /**
   * A hold is the person stopping the driver from starting anything new here (D46 §6). It outranks
   * "drives here" on the header, because the standing choice is no longer what is happening.
   */
  it('shows a hold instead of the standing choice it suspends', () => {
    render(<RepositoryGroup repository="engine" count={1} drivable held>{row}</RepositoryGroup>);

    expect(screen.getByText('held')).toBeInTheDocument();
    expect(screen.queryByText('drives here')).not.toBeInTheDocument();
  });

  it('names the tree that is busy, because a repository may now have more than one (D51)', () => {
    render(
      <RepositoryGroup repository="engine" count={1} busy="streaming-budget">{row}</RepositoryGroup>,
    );
    expect(screen.getByText('busy · streaming-budget')).toBeInTheDocument();
  });

  it('says busy with no name when the tree is the registered root', () => {
    render(<RepositoryGroup repository="engine" count={1} busy>{row}</RepositoryGroup>);
    expect(screen.getByText('busy')).toBeInTheDocument();
  });

  /**
   * A session can run in a repository that never adopted doctrine, and the header says so rather
   * than leaving the person to wonder why nothing can be asked of it (D48 §4).
   */
  it('marks a repository that has not adopted', () => {
    render(<RepositoryGroup repository="sandbox" count={1} adopted={false}>{row}</RepositoryGroup>);
    expect(screen.getByText('not adopted')).toBeInTheDocument();
  });

  it('marks a registration with no checkout here — a teammate\'s, mirrored (D48 §3)', () => {
    render(
      <RepositoryGroup repository="engine" count={1} hasCheckout={false}>{row}</RepositoryGroup>,
    );
    expect(screen.getByText('not on this machine')).toBeInTheDocument();
  });

  /**
   * Unknown is not false. In a browser the driver never answers and the registry may say nothing
   * about a checkout — a header that asserted "not adopted" from silence would be inventing news.
   */
  it('asserts nothing it was not told', () => {
    render(<RepositoryGroup repository="engine" count={1}>{row}</RepositoryGroup>);

    for (const claim of ['drives here', 'held', 'busy', 'not adopted', 'not on this machine']) {
      expect(screen.queryByText(claim)).not.toBeInTheDocument();
    }
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    render(<RepositoryGroup repository="engine" count={1} held>{row}</RepositoryGroup>);

    expect(screen.getByText('已暂停')).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});
