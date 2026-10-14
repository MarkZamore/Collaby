import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  CLOUDFLARE_ACCESS_LOGOUT_PATH,
  getCloudflareAccessReturnPath,
  redirectToCloudflareAccessLogin,
  redirectToCloudflareAccessLogout,
} from './cloudflareAccess';

describe('cloudflareAccess helpers', () => {
  beforeEach(() => {
    window.history.replaceState({}, '', '/');
  });

  it('captures the current protected path for access login', () => {
    window.history.replaceState({}, '', '/admin/ops?tab=services#focus');

    expect(getCloudflareAccessReturnPath()).toBe('/admin/ops?tab=services#focus');
  });

  it('redirects access login back to the current protected path', () => {
    const locationObject = { replace: vi.fn() };

    redirectToCloudflareAccessLogin('/admin/ops?tab=services#focus', locationObject);

    expect(locationObject.replace).toHaveBeenCalledWith('/admin/ops?tab=services#focus');
  });

  it('normalizes unsafe access login targets back to the host root', () => {
    const locationObject = { replace: vi.fn() };

    redirectToCloudflareAccessLogin('https://example.com/phishing', locationObject);

    expect(locationObject.replace).toHaveBeenCalledWith('/');
  });

  it('keeps the explicit logout flow on the Cloudflare logout path', () => {
    const locationObject = { replace: vi.fn() };

    redirectToCloudflareAccessLogout(locationObject);

    expect(locationObject.replace).toHaveBeenCalledWith(CLOUDFLARE_ACCESS_LOGOUT_PATH);
  });
});