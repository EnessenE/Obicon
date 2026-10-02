import { test, expect } from './fixtures';

test('should be possible to save app configuration', async ({ appConfigPage, page }) => {
  const saveButton = page.getByRole('button', { name: /Save settings/i });

  // enter some valid values
  await page.getByTestId('obicon-config-server-url').fill('http://obicon:5000');

  // reset the configured secret and enter a new one
  await page.getByRole('button', { name: /reset/i }).click();
  await page.getByTestId('obicon-config-auth-header').fill('uwu');

  // pick the Prometheus datasource (the first option in the picker)
  await page.getByTestId('obicon-config-prometheus').click();
  await page.getByRole('option').first().click();

  // listen for the server response on the saved form
  const saveResponse = appConfigPage.waitForSettingsResponse();

  await saveButton.click();
  await expect(saveResponse).toBeOK();
});
