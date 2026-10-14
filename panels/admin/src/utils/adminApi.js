import { apiRequest, buildApiUrl } from '@shared/utils/apiClient';

const AUTH_ADMIN_BASE = buildApiUrl('/auth', '/admin/users');
const MANAGEMENT_WORKSPACES_BASE = buildApiUrl('/management', '/admin/workspaces');
const MANAGEMENT_OPS_BASE = buildApiUrl('/management', '/admin/ops');
const MANAGEMENT_LANDING_BASE = buildApiUrl('/management', '/admin/landing');

const buildQuery = (params = {}) => {
  const searchParams = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    searchParams.set(key, String(value));
  });

  const query = searchParams.toString();
  return query ? `?${query}` : '';
};

export const getAdminUsers = (params, accessToken) =>
  apiRequest(`${AUTH_ADMIN_BASE}${buildQuery(params)}`, { accessToken });

export const getAdminUser = (userId, accessToken) =>
  apiRequest(`${AUTH_ADMIN_BASE}/${userId}`, { accessToken });

export const deactivateAdminUser = (userId, accessToken) =>
  apiRequest(`${AUTH_ADMIN_BASE}/${userId}/deactivate`, { accessToken, method: 'POST', body: JSON.stringify({}) });

export const reactivateAdminUser = (userId, accessToken) =>
  apiRequest(`${AUTH_ADMIN_BASE}/${userId}/reactivate`, { accessToken, method: 'POST', body: JSON.stringify({}) });

export const hardDeleteAdminUser = (userId, accessToken) =>
  apiRequest(`${AUTH_ADMIN_BASE}/${userId}/hard-delete`, { accessToken, method: 'POST', body: JSON.stringify({}) });

export const getUserDeletionOperation = (operationId, accessToken) =>
  apiRequest(`${AUTH_ADMIN_BASE}/deletion-operations/${operationId}`, { accessToken });

export const retryUserDeletionOperation = (operationId, accessToken) =>
  apiRequest(`${AUTH_ADMIN_BASE}/deletion-operations/${operationId}/retry`, { accessToken, method: 'POST', body: JSON.stringify({}) });

export const getSystemAdminRoster = (accessToken) =>
  apiRequest(`${AUTH_ADMIN_BASE}/system-admin-roster`, { accessToken });

export const getAdminWorkspaces = (params, accessToken) =>
  apiRequest(`${MANAGEMENT_WORKSPACES_BASE}${buildQuery(params)}`, { accessToken });

export const getAdminWorkspace = (workspaceId, accessToken) =>
  apiRequest(`${MANAGEMENT_WORKSPACES_BASE}/${workspaceId}`, { accessToken });

export const archiveAdminWorkspace = (workspaceId, accessToken) =>
  apiRequest(`${MANAGEMENT_WORKSPACES_BASE}/${workspaceId}/archive`, { accessToken, method: 'POST', body: JSON.stringify({}) });

export const unarchiveAdminWorkspace = (workspaceId, accessToken) =>
  apiRequest(`${MANAGEMENT_WORKSPACES_BASE}/${workspaceId}/unarchive`, { accessToken, method: 'POST', body: JSON.stringify({}) });

export const forceDeleteAdminWorkspace = (workspaceId, accessToken) =>
  apiRequest(`${MANAGEMENT_WORKSPACES_BASE}/${workspaceId}/force`, { accessToken, method: 'DELETE' });

export const getAdminOpsOverview = (accessToken) =>
  apiRequest(`${MANAGEMENT_OPS_BASE}/overview`, { accessToken });

export const getRestartOperations = (accessToken) =>
  apiRequest(`${MANAGEMENT_OPS_BASE}/restart-operations`, { accessToken });

export const getRestartOperation = (operationId, accessToken) =>
  apiRequest(`${MANAGEMENT_OPS_BASE}/restart-operations/${operationId}`, { accessToken });

export const createRestartOperation = (targetService, accessToken) =>
  apiRequest(`${MANAGEMENT_OPS_BASE}/restart-operations`, {
    accessToken,
    method: 'POST',
    body: JSON.stringify({ targetService }),
  });

export const getAdminLandingLocale = (locale, accessToken) =>
  apiRequest(`${MANAGEMENT_LANDING_BASE}/${locale}`, { accessToken });

export const saveAdminLandingLocale = (locale, content, accessToken) =>
  apiRequest(`${MANAGEMENT_LANDING_BASE}/${locale}`, {
    accessToken,
    method: 'PUT',
    body: JSON.stringify(content),
  });
