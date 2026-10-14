// src/shared/utils/gamificationApi.js
import { apiRequest, buildApiUrl } from './apiClient';

const GAMIFICATION = '/gamification';

const request = (path, accessToken, options = {}) =>
  apiRequest(buildApiUrl(GAMIFICATION, path), {
    accessToken,
    ...options,
  });

export const getGlobalXp = (token) => request('/xp/global', token);

export const getProjectXp = (token, projectId) => request(`/xp/project/${projectId}`, token);

export const getXpHistory = (token, count = 20) => request(`/xp/history?count=${count}`, token);

export const getGlobalLeaderboard = (token, count = 10) =>
  request(`/leaderboard/global?count=${count}`, token);

export const getProjectLeaderboard = (token, projectId, count = 10) =>
  request(`/leaderboard/project/${projectId}?count=${count}`, token);

export const getQuests = (token) => request('/quests', token);

export const claimQuest = (token, questId) =>
  request(`/quests/${questId}/claim`, token, {
    method: 'POST',
    body: JSON.stringify({}),
  });

export const getStreak = (token) => request('/streak', token);

export const useStreakFreeze = (token) =>
  request('/streak/freeze', token, {
    method: 'POST',
    body: JSON.stringify({}),
  });

export const getShopItems = (token, category = null) =>
  request(`/shop${category ? `?category=${category}` : ''}`, token);

export const purchaseItem = (token, itemId) =>
  request('/shop/buy', token, {
    method: 'POST',
    body: JSON.stringify({ itemId }),
  });

export const getInventory = (token) => request('/shop/inventory', token);

export const equipItem = (token, itemId) =>
  request(`/shop/equip/${itemId}`, token, {
    method: 'POST',
    body: JSON.stringify({}),
  });

export const unequipItem = (token, itemId) =>
  request(`/shop/unequip/${itemId}`, token, {
    method: 'POST',
    body: JSON.stringify({}),
  });

export const getSkillTree = (token, projectId) => request(`/skill-tree/${projectId}`, token);

export const getSkillTreeConfig = (token, projectId) =>
  request(`/skill-tree/${projectId}/config`, token);

export const updateSkillTreeConfig = (token, projectId, payload) =>
  request(`/skill-tree/${projectId}/config`, token, {
    method: 'PUT',
    body: JSON.stringify(payload),
  });

export const resetSkillTreeConfig = (token, projectId) =>
  request(`/skill-tree/${projectId}/reset`, token, {
    method: 'POST',
    body: JSON.stringify({}),
  });

export const chooseSkillNode = (token, projectId, nodeId) =>
  request(`/skill-tree/${projectId}/choose`, token, {
    method: 'POST',
    body: JSON.stringify({ nodeId }),
  });

export const getProjectProgress = (token, projectId) =>
  request(`/progress/project/${projectId}`, token);
