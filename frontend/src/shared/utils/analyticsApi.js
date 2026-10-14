import { apiRequest, buildApiUrl } from './apiClient';

const ANALYTICS = '/analytics';

const request = (path, accessToken, options = {}) =>
  apiRequest(buildApiUrl(ANALYTICS, path), {
    accessToken,
    ...options,
  });

export const getProjectOverview = (token, projectId, period = 'week') =>
  request(`/projects/${projectId}/overview?period=${encodeURIComponent(period)}`, token);
