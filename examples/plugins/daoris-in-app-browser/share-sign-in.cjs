// Playwright MCP's --init-page, run on each tab it opens. With --isolated each session drives a browser context of its own,
// which starts with no cookies; the person signs in once in the browser's own context (the first), so each new tab of a
// session's context is given that context's cookies. Nothing goes back: a session never signs the person in or out.
exports.default = async ({ page }) => {
  const own = page.context();
  const person = own.browser()?.contexts()[0];
  if (!person || person === own) return;
  await own.addCookies(await person.cookies());
};
