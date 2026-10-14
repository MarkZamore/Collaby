import { apiRequest, buildApiUrl } from './apiClient';

const USERS_PATH = '/users';

const request = (path, accessToken, options = {}) =>
  apiRequest(buildApiUrl(USERS_PATH, path), {
    accessToken,
    ...options,
  });

export const getCurrentUserBundle = async (accessToken) => {
  return request('/me', accessToken);
};

export const getMyProfile = async (accessToken) => {
  return request('/me/profile', accessToken);
};

export const updateMyProfile = async (accessToken, payload) => {
  return request('/me/profile', accessToken, {
    method: 'PATCH',
    body: JSON.stringify(payload),
  });
};

export const getMyPreferences = async (accessToken) => {
  return request('/me/preferences', accessToken);
};

export const updateMyPreferences = async (accessToken, payload) => {
  return request('/me/preferences', accessToken, {
    method: 'PATCH',
    body: JSON.stringify(payload),
  });
};

export const getMyOnboarding = async (accessToken) => {
  return request('/me/onboarding', accessToken);
};

export const updateMyOnboarding = async (accessToken, payload) => {
  return request('/me/onboarding', accessToken, {
    method: 'PATCH',
    body: JSON.stringify(payload),
  });
};

export const completeMyOnboarding = async (accessToken) => {
  return request('/me/onboarding/complete', accessToken, {
    method: 'POST',
    body: JSON.stringify({}),
  });
};

export const pingMyActivity = async (accessToken) => {
  return request('/me/activity/ping', accessToken, {
    method: 'POST',
    body: JSON.stringify({}),
  });
};

export const updateMyActivity = async (accessToken, payload) => {
  return request('/me/activity', accessToken, {
    method: 'PATCH',
    body: JSON.stringify(payload),
  });
};

export const getPublicUsersBatch = async (accessToken, userIds) => {
  return request('/public/batch', accessToken, {
    method: 'POST',
    body: JSON.stringify(userIds),
  });
};

export const searchUsers = async (accessToken, q, limit = 20) => {
  return request(`/search?q=${encodeURIComponent(q)}&limit=${limit}`, accessToken);
};
