import { test, expect } from './fixtures';
import { ROUTES } from '../src/constants';

test.describe('navigating app', () => {
  test('overview should render successfully', async ({ gotoPage, page }) => {
    await gotoPage(`/${ROUTES.Overview}`);
    await expect(page.getByText('Overview')).toBeVisible();
  });

  test('nodes page should render successfully', async ({ gotoPage, page }) => {
    await gotoPage(`/${ROUTES.Nodes}`);
    await expect(page.getByText('Nodes')).toBeVisible();
  });

  test('tests page should render successfully', async ({ gotoPage, page }) => {
    await gotoPage(`/${ROUTES.Tests}`);
    await expect(page.getByText('Tests')).toBeVisible();
  });

  test('logs page should render successfully', async ({ gotoPage, page }) => {
    await gotoPage(`/${ROUTES.Logs}`);
    await expect(page.getByText('Logs')).toBeVisible();
  });

  test('results page should render successfully', async ({ gotoPage, page }) => {
    await gotoPage(`/${ROUTES.Results}`);
    await expect(page.getByText('Results')).toBeVisible();
  });

  test('server page should render successfully', async ({ gotoPage, page }) => {
    await gotoPage(`/${ROUTES.Server}`);
    await expect(page.getByText('Server')).toBeVisible();
  });
});
