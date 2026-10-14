import { describe, expect, it } from 'vitest';

import { getHealthText, getProbeStatusValue } from '@admin/pages/AdminOps.jsx';

describe('AdminOps health presentation helpers', () => {
  it('marks missing probes as not_probed instead of failed', () => {
    expect(getProbeStatusValue(null)).toBe('not_probed');
    expect(getHealthText(null)).toBe('not probed');
  });

  it('keeps successful probes green and preserves HTTP code text', () => {
    const probe = { isHealthy: true, statusCode: 200, error: null };

    expect(getProbeStatusValue(probe)).toBe('succeeded');
    expect(getHealthText(probe)).toBe('HTTP 200');
  });

  it('keeps failing probes red and surfaces backend error text', () => {
    const probe = { isHealthy: false, statusCode: null, error: 'timeout' };

    expect(getProbeStatusValue(probe)).toBe('failed');
    expect(getHealthText(probe)).toBe('timeout');
  });
});
