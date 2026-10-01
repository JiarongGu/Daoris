/**
 * How an element's columns are laid out (D118 §3b): by the main area's own width, as the container named
 * `main` (`@4xl/main:grid-cols-2`), or by the viewport's (`lg:grid-cols-2`). jsdom lays nothing out, so a
 * page's split is held by the property it rests on: the side bar and the list narrow the main area while
 * the window stays as wide, so a viewport breakpoint keeps two columns in a main area too narrow for them.
 */
export function columnsFollow(element: Element): { main: string[]; viewport: string[] } {
  const classes = [...element.classList];
  return {
    main: classes.filter((name) => /^@[\w[\]().-]+\/main:grid-cols-/.test(name)),
    viewport: classes.filter((name) => /^(?:sm|md|lg|xl|2xl|max-(?:sm|md|lg|xl|2xl)):grid-cols-/.test(name)),
  };
}
