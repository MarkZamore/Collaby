import React, { createContext, useContext, useState, useEffect, useMemo, useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import toast from 'react-hot-toast';
import { useAuth } from '../../shared/context/AuthContext';
import { normalizeLanguage, normalizeTheme } from '../../shared/i18n/config';
import { resolveUserAvatar } from '../../shared/utils/avatar';
import { getCurrentUserBundle, pingMyActivity, updateMyPreferences, updateMyProfile } from '../../shared/utils/usersApi';
import * as workApi from '../../shared/utils/workspaceApi';
import useSignalR from '../../shared/hooks/useSignalR';
import { usePreferences } from './PreferencesContext';
import {
  getStatusColor,
  getKnownStatusKey,
  getStatusDisplayName,
  isDoneStatus,
  normalizeStatusKey,
} from '../utils/statuses';
import { localizeProjectLabel } from '../utils/defaultProjectLabels';

const PlatformContext = createContext(null);

// ─── Вспомогательные функции ────────────────────────────────────────────────

const toInitials = (value) => {
  if (!value) return '??';
  const parts = value.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
};


/** Сравнивает user id устойчиво к разным форматам из разных сервисов. */
const sameUserId = (a, b) => a != null && b != null && String(a).toLowerCase() === String(b).toLowerCase();
const looksLikeEmail = (value) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(value || '').trim());
const normalizeEntityId = (value) => String(value ?? '').trim().toLowerCase();
const isPendingWorkspaceInvite = (notification) => (
  notification?.type === 'WorkspaceInvite'
  && !notification?.actionStatus
  && notification?.referenceId != null
);

const dedupeNotificationsList = (notifications) => {
  const seenIds = new Set();
  const seenPendingWorkspaceInvites = new Set();
  const deduped = [];

  (notifications || []).forEach((notification) => {
    if (!notification?.id) {
      return;
    }

    const notificationId = normalizeEntityId(notification.id);
    if (seenIds.has(notificationId)) {
      return;
    }
    seenIds.add(notificationId);

    if (isPendingWorkspaceInvite(notification)) {
      const inviteKey = normalizeEntityId(notification.referenceId);
      if (seenPendingWorkspaceInvites.has(inviteKey)) {
        return;
      }
      seenPendingWorkspaceInvites.add(inviteKey);
    }

    deduped.push(notification);
  });

  return deduped;
};

// Priority может прийти с backend как число (0-3) или как camelCase-строка.
const PRIORITY_MAP = {
  0: 'low', 1: null, 2: 'high', 3: 'critical',
  low: 'low', medium: null, high: 'high', critical: 'critical',
  Low: 'low', Medium: null, High: 'high', Critical: 'critical',
  major: 'high', normal: null, urgent: 'critical',
  Major: 'high', Normal: null, Urgent: 'critical',
};
const mapPriority = (v) => {
  if (v == null || v === '') return null;
  if (typeof v === 'string') {
    const mapped = PRIORITY_MAP[v] ?? PRIORITY_MAP[v.toLowerCase()];
    return mapped === undefined ? null : mapped;
  }
  const mapped = PRIORITY_MAP[v];
  return mapped === undefined ? null : mapped;
};

// ─── UI-only состояние в localStorage: стикеры, фон доски и цвета колонок ───

const UI_META_KEY = 'collaby_ui_meta_v1';
const loadUiMeta = () => { try { return JSON.parse(localStorage.getItem(UI_META_KEY) || '{}'); } catch { return {}; } };
const saveUiMeta = (meta) => localStorage.setItem(UI_META_KEY, JSON.stringify(meta));

// ─── Преобразование участника bootstrap в формат пользователя платформы ─────

const mapMemberToUser = (m) => {
  const displayName = m.displayName || m.username || `user-${String(m.userId).slice(0, 8)}`;
  return {
    id: m.userId,
    name: displayName,
    initials: toInitials(displayName),
    avatar: resolveUserAvatar({
      avatarUrl: m.avatarUrl,
      userId: m.userId,
      displayName,
      username: m.username,
    }),
    username: m.username || null,
  };
};

// ─── Каталог стикеров: пока чисто frontend-слой без отдельного backend ──────

export const STICKER_CATALOG = [
  { id: 'assignee', type: 'system', labelKey: 'stickers.catalog.labels.assignee', subtitleKey: 'stickers.catalog.subtitles.system', iconName: 'Users', removable: false, values: [] },
  { id: 'deadline', type: 'system', labelKey: 'stickers.catalog.labels.deadline', subtitleKey: 'stickers.catalog.subtitles.system', iconName: 'Calendar', removable: false, values: [] },
  { id: 'priority', type: 'state-set', labelKey: 'stickers.catalog.labels.priority', subtitleKey: 'stickers.catalog.subtitles.stateSet', iconName: 'BarChart3', removable: true, values: [
    { id: 'critical', label: 'critical', color: '#EF4444' },
    { id: 'major', label: 'major', color: '#F97316' },
    { id: 'normal', label: 'normal', color: '#22C55E' },
    { id: 'low', label: 'low', color: '#94A3B8' },
  ]},
  { id: 'task-type', type: 'state-set', labelKey: 'stickers.catalog.labels.taskType', subtitleKey: 'stickers.catalog.subtitles.stateSet', iconName: 'Code2', removable: true, values: [
    { id: 'feature', labelKey: 'stickers.catalog.values.feature', color: '#22C55E' },
    { id: 'bug', labelKey: 'stickers.catalog.values.bug', color: '#EF4444' },
    { id: 'improvement', labelKey: 'stickers.catalog.values.improvement', color: '#3B82F6' },
  ]},
  { id: 'sprint', type: 'state-set', labelKey: 'stickers.catalog.labels.sprint', subtitleKey: 'stickers.catalog.subtitles.sprint', iconName: 'RefreshCw', removable: true, values: [
    { id: 'sprint-1', labelKey: 'stickers.catalog.values.sprint1', color: '#3B82F6' },
    { id: 'sprint-2', labelKey: 'stickers.catalog.values.sprint2', color: '#8B5CF6' },
    { id: 'sprint-3', labelKey: 'stickers.catalog.values.sprint3', color: '#22C55E' },
  ]},
  { id: 'section', type: 'state-set', labelKey: 'stickers.catalog.labels.section', subtitleKey: 'stickers.catalog.subtitles.stateSet', iconName: 'Layers', removable: true, values: [
    { id: 'frontend', label: 'Frontend', color: '#3B82F6' },
    { id: 'backend', label: 'Backend', color: '#22C55E' },
    { id: 'design', labelKey: 'stickers.catalog.values.design', color: '#EC4899' },
    { id: 'devops', label: 'DevOps', color: '#F59E0B' },
  ]},
  { id: 'estimate', type: 'number', labelKey: 'stickers.catalog.labels.estimate', subtitleKey: 'stickers.catalog.subtitles.number', iconName: 'Hourglass', removable: true, values: [] },
  { id: 'device', type: 'state-set', labelKey: 'stickers.catalog.labels.device', subtitleKey: 'stickers.catalog.subtitles.stateSet', iconName: 'Monitor', removable: true, values: [
    { id: 'desktop', label: 'Desktop', color: '#3B82F6' },
    { id: 'mobile', label: 'Mobile', color: '#22C55E' },
    { id: 'tablet', label: 'Tablet', color: '#F59E0B' },
  ]},
  { id: 'comment-field', type: 'text', labelKey: 'stickers.catalog.labels.commentField', subtitleKey: 'stickers.catalog.subtitles.text', iconName: 'MessageCircle', removable: true, values: [] },
  { id: 'stopwatch', type: 'system', labelKey: 'stickers.catalog.labels.stopwatch', subtitleKey: 'stickers.catalog.subtitles.system', iconName: 'Clock', removable: false, values: [] },
  { id: 'recurring', type: 'system', labelKey: 'stickers.catalog.labels.recurring', subtitleKey: 'stickers.catalog.subtitles.system', iconName: 'RefreshCw', removable: false, values: [] },
  { id: 'timer', type: 'system', labelKey: 'stickers.catalog.labels.timer', subtitleKey: 'stickers.catalog.subtitles.system', iconName: 'Timer', removable: false, values: [] },
  { id: 'difficulty', type: 'state-set', labelKey: 'stickers.catalog.labels.difficulty', subtitleKey: 'stickers.catalog.subtitles.stateSet', iconName: 'BarChart3', removable: true, values: [
    { id: 'easy', labelKey: 'stickers.catalog.values.easy', color: '#22C55E' },
    { id: 'medium', labelKey: 'stickers.catalog.values.medium', color: '#F59E0B' },
    { id: 'hard', labelKey: 'stickers.catalog.values.hard', color: '#EF4444' },
  ]},
  { id: 'environment', type: 'state-set', labelKey: 'stickers.catalog.labels.environment', subtitleKey: 'stickers.catalog.subtitles.stateSet', iconName: 'Monitor', removable: true, values: [
    { id: 'dev', label: 'Dev', color: '#3B82F6' },
    { id: 'staging', label: 'Staging', color: '#F59E0B' },
    { id: 'production', label: 'Production', color: '#EF4444' },
  ]},
];

const DEFAULT_BOARD_STICKER_IDS = ['assignee', 'deadline', 'priority', 'task-type', 'sprint', 'estimate'];

// Яркие цвета колонок по умолчанию (при создании проекта с бэкенда могут прийти бледные — подменяем на фронте)
const BRIGHT_STATUS_COLORS = {
  'К выполнению': '#3B82F6',
  'К ВЫПОЛНЕНИЮ': '#3B82F6',
  'В процессе': '#F59E0B',
  'В ПРОЦЕССЕ': '#F59E0B',
  'Готово': '#22C55E',
  'ГОТОВО': '#22C55E',
};
const getBrightColorForStatus = (name) =>
  BRIGHT_STATUS_COLORS[name] || BRIGHT_STATUS_COLORS[(name || '').trim().charAt(0).toUpperCase() + (name || '').trim().slice(1).toLowerCase()];

// ═════════════════════════════════════════════════════════════════════════════
//  PlatformProvider держит всё состояние workspace, проектов и задач поверх реального API.
// ═════════════════════════════════════════════════════════════════════════════

export const PlatformProvider = ({ children }) => {
  const { t } = useTranslation('platform');
  const { accessToken, currentUser: authUser } = useAuth();
  const { uiPreferences, hydrateFromAccount } = usePreferences();

  // ── Основное состояние, загружаемое из API ───────────────────────────────
  const [workspaces, setWorkspaces] = useState([]);           // BootstrapWorkspace[]
  const [activeWorkspaceId, setActiveWorkspaceId] = useState(null);
  const [activeProjectId, setActiveProjectId] = useState(null);
  const [tasks, setTasks] = useState([]);                     // TaskResponse[] для активного проекта
  const [inboxTasksAll, setInboxTasksAll] = useState([]);     // InboxItemResponse[] из GET /me/inbox
  const [inboxLoading, setInboxLoading] = useState(false);
  const [inboxError, setInboxError] = useState(null);
  const [bootstrapped, setBootstrapped] = useState(false);
  const [loading, setLoading] = useState(true);

  // ── Локальное UI-состояние ────────────────────────────────────────────────
  const [uiMeta, setUiMeta] = useState(loadUiMeta);
  const [activePage, setActivePage] = useState('dashboard');
  const [meBundle, setMeBundle] = useState(null);
  const [meBundleLoading, setMeBundleLoading] = useState(false);

  // ── Состояние уведомлений ────────────────────────────────────────────────
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const notificationIdsRef = useRef(new Set());
  const accountPreferenceHydrationKeyRef = useRef(null);
  const guestPreferencePromotionKeyRef = useRef(null);

  // Сохраняем UI-метаданные, чтобы пользовательские настройки переживали перезагрузку.
  useEffect(() => { saveUiMeta(uiMeta); }, [uiMeta]);

  useEffect(() => {
    accountPreferenceHydrationKeyRef.current = null;
    guestPreferencePromotionKeyRef.current = null;
  }, [authUser?.id]);

  const refreshBootstrap = useCallback(async ({ preferredWorkspaceId = null, preferredProjectId = null } = {}) => {
    if (!accessToken) {
      setWorkspaces([]);
      return [];
    }

    const data = await workApi.getBootstrapData(accessToken);
    const wsList = data.workspaces || [];

    setWorkspaces(wsList);

    const resolvedWorkspaceId =
      (preferredWorkspaceId && wsList.some((workspace) => workspace.id === preferredWorkspaceId) && preferredWorkspaceId)
      || (activeWorkspaceId && wsList.some((workspace) => workspace.id === activeWorkspaceId) && activeWorkspaceId)
      || wsList[0]?.id
      || null;

    const resolvedWorkspace = wsList.find((workspace) => workspace.id === resolvedWorkspaceId) || null;

    const resolvedProjectId =
      (preferredProjectId && resolvedWorkspace?.projects?.some((project) => project.id === preferredProjectId) && preferredProjectId)
      || (activeProjectId && resolvedWorkspace?.projects?.some((project) => project.id === activeProjectId) && activeProjectId)
      || resolvedWorkspace?.projects?.[0]?.id
      || null;

    setActiveWorkspaceId(resolvedWorkspaceId);
    setActiveProjectId(resolvedProjectId);

    return wsList;
  }, [accessToken, activeProjectId, activeWorkspaceId]);

  const getProjectMeta = useCallback((pid) => uiMeta[pid] || {}, [uiMeta]);
  const setProjectMeta = useCallback((pid, patch) => {
    setUiMeta(prev => ({ ...prev, [pid]: { ...(prev[pid] || {}), ...patch } }));
  }, []);

  const localizedStickerCatalog = useMemo(() => (
    STICKER_CATALOG.map((sticker) => ({
      ...sticker,
      label: sticker.labelKey ? t(sticker.labelKey) : sticker.label,
      subtitle: sticker.subtitleKey ? t(sticker.subtitleKey) : sticker.subtitle,
      values: (sticker.values || []).map((value) => ({
        ...value,
        label: value.labelKey ? t(value.labelKey) : value.label,
      })),
    }))
  ), [t]);

  // ── Bootstrap: на старте загружаем workspaces, feature flags и базовый профиль ─

  useEffect(() => {
    if (!accessToken) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      try {
        const data = await workApi.getBootstrapData(accessToken);
        if (cancelled) return;
        const wsList = data.workspaces || [];
        setWorkspaces(wsList);
        // Если пользователь ещё ничего не выбрал, автоматически активируем первый workspace и первый проект.
        if (wsList.length > 0) {
          setActiveWorkspaceId(prev => prev || wsList[0].id);
          const firstWs = wsList[0];
          if (firstWs.projects?.length > 0) {
            setActiveProjectId(prev => prev || firstWs.projects[0].id);
          }
        }
        setBootstrapped(true);
      } catch (err) {
        console.error('Bootstrap failed:', err);
          setBootstrapped(true);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [accessToken]);

  // ── При смене активного проекта заново загружаем задачи ──────────────────

  const refreshTasks = useCallback(async () => {
    if (!accessToken || !activeProjectId) { setTasks([]); return; }
    try {
      const result = await workApi.getProjectTasks(accessToken, activeProjectId);
      setTasks(result || []);
    } catch (err) {
      console.error('Failed to load tasks:', err);
      setTasks([]);
    }
  }, [accessToken, activeProjectId]);

  useEffect(() => { refreshTasks(); }, [refreshTasks]);

  // ── Inbox: загружаем задачи «к выполнению» по всем рабочим пространствам ─
  const refreshInbox = useCallback(async () => {
    if (!accessToken) { setInboxTasksAll([]); setInboxError(null); return; }
    setInboxLoading(true);
    setInboxError(null);
    try {
      const list = await workApi.getInbox(accessToken);
      setInboxTasksAll(
        Array.isArray(list)
          ? list.map((item) => ({
              ...item,
              labels: (item.labels || []).map((label) =>
                localizeProjectLabel({ id: label.id, name: label.name, color: label.color }, t),
              ),
            }))
          : [],
      );
    } catch (err) {
      console.error('Failed to load inbox:', err);
      setInboxTasksAll([]);
      setInboxError(err?.message || t('inbox.loadError'));
    } finally {
      setInboxLoading(false);
    }
  }, [accessToken]);

  useEffect(() => { refreshInbox(); }, [refreshInbox]);

  // ── SignalR и realtime-обновления ────────────────────────────────────────

  // Стабильные refs нужны, чтобы SignalR-обработчики всегда вызывали актуальные функции.
  const refreshTasksRef = useRef(refreshTasks);
  refreshTasksRef.current = refreshTasks;
  const refreshInboxRef = useRef(refreshInbox);
  refreshInboxRef.current = refreshInbox;
  const activeWorkspaceIdRef = useRef(activeWorkspaceId);
  activeWorkspaceIdRef.current = activeWorkspaceId;
  const refreshWorkspaceMembersRef = useRef(null);

  // ── Вспомогательные операции для уведомлений ─────────────────────────────
  const refreshNotifications = useCallback(async () => {
    if (!accessToken) return;
    try {
      const list = await workApi.getNotifications(accessToken);
      const nextNotifications = dedupeNotificationsList(Array.isArray(list) ? list : []);
      notificationIdsRef.current = new Set(nextNotifications.map((notification) => normalizeEntityId(notification.id)));
      setNotifications(nextNotifications);
    } catch (err) {
      console.error('Failed to load notifications:', err);
    }
  }, [accessToken]);

  const refreshUnreadCount = useCallback(async () => {
    if (!accessToken) return;
    try {
      const data = await workApi.getUnreadCount(accessToken);
      setUnreadCount(data?.count ?? 0);
    } catch (err) {
      console.error('Failed to load unread count:', err);
    }
  }, [accessToken]);

  // После bootstrap сразу подгружаем список уведомлений и счётчик непрочитанных.
  useEffect(() => {
    if (bootstrapped && accessToken) {
      refreshNotifications();
      refreshUnreadCount();
    }
  }, [bootstrapped, accessToken, refreshNotifications, refreshUnreadCount]);

  const markNotificationRead = useCallback(async (notificationId) => {
    if (!accessToken) return;
    try {
      await workApi.markNotificationRead(accessToken, notificationId);
      setNotifications(prev => prev.map(n => n.id === notificationId ? { ...n, isRead: true } : n));
      setUnreadCount(prev => Math.max(0, prev - 1));
    } catch (err) {
      console.error('Failed to mark notification read:', err);
    }
  }, [accessToken]);

  const markAllNotificationsRead = useCallback(async () => {
    if (!accessToken) return;
    try {
      await workApi.markAllNotificationsRead(accessToken);
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      setUnreadCount(0);
    } catch (err) {
      console.error('Failed to mark all read:', err);
    }
  }, [accessToken]);

  const handleNotificationAction = useCallback(async (notificationId, status) => {
    if (!accessToken) return;
    try {
      await workApi.setNotificationAction(accessToken, notificationId, status);
      setNotifications(prev => prev.map(n =>
        n.id === notificationId ? { ...n, actionStatus: status, isRead: true } : n
      ));
      setUnreadCount(prev => Math.max(0, prev - 1));

      // После принятия приглашения нужно перечитать bootstrap, чтобы появился новый workspace.
      if (status === 'accepted') {
        try {
          const data = await workApi.getBootstrapData(accessToken);
          const wsList = data.workspaces || [];
          setWorkspaces(wsList);
        } catch (e) {
          console.error('Failed to refresh workspaces after accept:', e);
        }
      }
    } catch (err) {
      console.error('Failed to set notification action:', err);
    }
  }, [accessToken]);

  const refreshNotificationsRef = useRef(refreshNotifications);
  refreshNotificationsRef.current = refreshNotifications;
  const refreshUnreadCountRef = useRef(refreshUnreadCount);
  refreshUnreadCountRef.current = refreshUnreadCount;

  // Храним время последнего refresh участников, чтобы серия одинаковых событий не заспамила API.
  const lastMemberRefreshRef = useRef(0);

  const signalRHandlers = useMemo(() => ({
    TaskCreated:    () => { refreshTasksRef.current(); refreshInboxRef.current(); },
    TaskUpdated:    () => { refreshTasksRef.current(); refreshInboxRef.current(); },
    TaskDeleted:    () => { refreshTasksRef.current(); refreshInboxRef.current(); },
    TaskMoved:      () => { refreshTasksRef.current(); },
    SubTaskChanged: () => { refreshTasksRef.current(); },
    CommentChanged: () => { refreshTasksRef.current(); },
    LabelChanged:   () => { refreshTasksRef.current(); },
    MemberChanged:  () => {
      const wsId = activeWorkspaceIdRef.current;
      if (!wsId) return;
      const now = Date.now();
      // Если список уже обновлялся совсем недавно, повторный запрос пропускаем.
      if (now - lastMemberRefreshRef.current < 2000) return;
      lastMemberRefreshRef.current = now;
      refreshWorkspaceMembersRef.current(wsId);
    },
    NotificationReceived: (notification) => {
      const notificationId = normalizeEntityId(notification?.id);
      if (!notification?.id || notificationIdsRef.current.has(notificationId)) {
        return;
      }

      setNotifications(prev => {
        const alreadyHasPendingInvite = isPendingWorkspaceInvite(notification)
          && prev.some((currentNotification) => (
            isPendingWorkspaceInvite(currentNotification)
            && normalizeEntityId(currentNotification.referenceId) === normalizeEntityId(notification.referenceId)
          ));

        if (!notification.isRead && !alreadyHasPendingInvite) {
          setUnreadCount((currentUnread) => currentUnread + 1);
        }

        const nextNotifications = dedupeNotificationsList([notification, ...prev]);
        notificationIdsRef.current = new Set(nextNotifications.map((item) => normalizeEntityId(item.id)));
        return nextNotifications;
      });

      if (notification.type === 'TaskAssigned') {
        toast.success(
          t('notifications.toast.taskAssigned', {
            task: notification.message || t('notifications.toast.taskAssignedFallback'),
          }),
          { id: `task-assigned-${notification.id}` },
        );
      }
    },
    UnreadCountChanged: (data) => {
      setUnreadCount(data?.count ?? 0);
    },
  }), []);       // stable — handlers use refs internally

  const {
    joinProject,
    leaveProject,
    joinWorkspace,
    leaveWorkspace,
  } =
    useSignalR(accessToken, signalRHandlers);

  // При смене activeWorkspaceId входим в нужную SignalR-группу и выходим из прежней.
  useEffect(() => {
    if (!activeWorkspaceId) return;
    joinWorkspace(activeWorkspaceId);
    return () => leaveWorkspace(activeWorkspaceId);
  }, [activeWorkspaceId, joinWorkspace, leaveWorkspace]);

  // При смене activeProjectId поддерживаем актуальное membership в проектной группе.
  useEffect(() => {
    if (!activeProjectId) return;
    joinProject(activeProjectId);
    return () => leaveProject(activeProjectId);
  }, [activeProjectId, joinProject, leaveProject]);

  // ── MeBundle + Activity ping (UserService) ───────────────────────────────

  const refreshMeBundle = useCallback(async ({ ping = true } = {}) => {
    if (!accessToken || !authUser?.id) {
      setMeBundle(null);
      setMeBundleLoading(false);
      return null;
    }

    setMeBundleLoading(true);
    try {
      const bundle = await getCurrentUserBundle(accessToken);
      let lastActiveAt = bundle?.activity?.lastActiveAt || null;

      if (ping) {
        const pingResult = await pingMyActivity(accessToken).catch(() => null);
        lastActiveAt = pingResult?.lastActiveAt || lastActiveAt;
      }

      const nextBundle = {
        ...bundle,
        activity: {
          ...(bundle?.activity || {}),
          lastActiveAt,
        },
      };
      setMeBundle(nextBundle);
      return nextBundle;
    } catch {
      setMeBundle(null);
      return null;
    } finally {
      setMeBundleLoading(false);
    }
  }, [accessToken, authUser?.id]);

  useEffect(() => {
    let cancelled = false;

    if (!accessToken || !authUser?.id) {
      setMeBundle(null);
      setMeBundleLoading(false);
      return undefined;
    }

    (async () => {
      const bundle = await refreshMeBundle({ ping: true });
      if (cancelled) {
        return;
      }
      if (!bundle) {
        setMeBundle(null);
      }
    })();

    return () => { cancelled = true; };
  }, [accessToken, authUser?.id, refreshMeBundle]);

  useEffect(() => {
    if (!accessToken || !meBundle) {
      return undefined;
    }

    let cancelled = false;

    const syncAccountPreferences = async () => {
      const accountLanguage = normalizeLanguage(meBundle?.profile?.language);
      const accountTheme = normalizeTheme(meBundle?.preferences?.theme);
      const guestLanguage = normalizeLanguage(uiPreferences.language);
      const guestTheme = normalizeTheme(uiPreferences.theme);

      let nextLanguage = accountLanguage;
      let nextTheme = accountTheme;

      const shouldPromoteGuestChoice =
        uiPreferences.hasGuestOverride &&
        !uiPreferences.accountHydrated &&
        accountLanguage === 'en' &&
        accountTheme === 'dark';

      if (shouldPromoteGuestChoice) {
        const promotionKey = `${authUser?.id || 'guest'}:${guestLanguage}:${guestTheme}`;

        if (guestPreferencePromotionKeyRef.current !== promotionKey) {
          guestPreferencePromotionKeyRef.current = promotionKey;

          try {
            await Promise.all([
              updateMyProfile(accessToken, { language: guestLanguage }),
              updateMyPreferences(accessToken, { theme: guestTheme }),
            ]);
            nextLanguage = guestLanguage;
            nextTheme = guestTheme;
          } catch (syncError) {
            console.warn('Не удалось сохранить гостевые UI-настройки в аккаунт:', syncError);
          }
        } else {
          nextLanguage = guestLanguage;
          nextTheme = guestTheme;
        }
      }

      if (cancelled) {
        return;
      }

      const hydrationKey = `${authUser?.id || 'guest'}:${nextLanguage}:${nextTheme}`;
      const preferencesAlreadyHydrated =
        uiPreferences.accountHydrated &&
        normalizeLanguage(uiPreferences.language) === nextLanguage &&
        normalizeTheme(uiPreferences.theme) === nextTheme;

      if (preferencesAlreadyHydrated && accountPreferenceHydrationKeyRef.current === hydrationKey) {
        return;
      }

      accountPreferenceHydrationKeyRef.current = hydrationKey;
      hydrateFromAccount({
        language: nextLanguage,
        theme: nextTheme,
      });
    };

    syncAccountPreferences();

    return () => {
      cancelled = true;
    };
  }, [
    accessToken,
    authUser?.id,
    hydrateFromAccount,
    meBundle,
    uiPreferences.accountHydrated,
    uiPreferences.hasGuestOverride,
    uiPreferences.language,
    uiPreferences.theme,
  ]);

  useEffect(() => {
    if (!accessToken || !authUser?.id) return undefined;

    const ping = async () => {
      try {
        const result = await pingMyActivity(accessToken);
        if (!result?.lastActiveAt) return;
        setMeBundle((prev) => (
          prev
            ? {
                ...prev,
                activity: {
                  ...(prev.activity || {}),
                  lastActiveAt: result.lastActiveAt,
                },
              }
            : prev
        ));
      } catch {
        // Heartbeat не должен ломать UI: это только фоновая проверка активности.
      }
    };

    const id = setInterval(ping, 60_000);
    return () => clearInterval(id);
  }, [accessToken, authUser?.id]);

  // ── Derived state ────────────────────────────────────────────────────────

  const activeWorkspace = useMemo(
    () => workspaces.find(w => w.id === activeWorkspaceId) || null,
    [workspaces, activeWorkspaceId],
  );

  const activeProjectMeta = useMemo(
    () => activeWorkspace?.projects?.find(p => p.id === activeProjectId) || null,
    [activeWorkspace, activeProjectId],
  );
  const accountDefaultBoardBackground = meBundle?.preferences?.defaultBoardBackground || null;

  // Собираем boardData в форме, которую ожидают BoardView и соседние UI-компоненты.
  const activeProject = useMemo(() => {
    if (!activeProjectMeta) return null;
    const statuses = activeProjectMeta.statuses || [];
    const pMeta = getProjectMeta(activeProjectMeta.id);
    const projectBoardBackground = pMeta.boardBackground || null;
    const effectiveBoardBackground = projectBoardBackground || accountDefaultBoardBackground || null;

    // Быстрый lookup userId -> user собираем из участников текущего workspace.
    const memberMap = new Map();
    workspaces.forEach(ws => {
      (ws.members || []).forEach(m => { if (m.userId) memberMap.set(m.userId, mapMemberToUser(m)); });
    });

    // Колонки строим из статусов и учитываем сохранённый порядок, если он уже есть в meta.
    const columns = {};
    const columnOrder = [];

    // Если пользователь уже менял порядок колонок, используем сохранённый pMeta.columnOrder.
    const savedOrder = Array.isArray(pMeta.columnOrder) ? pMeta.columnOrder : null;
    const orderedStatuses = savedOrder && savedOrder.length
      ? [...statuses].sort((a, b) => {
          const ai = savedOrder.indexOf(a.id);
          const bi = savedOrder.indexOf(b.id);
          const baseIndexA = statuses.indexOf(a);
          const baseIndexB = statuses.indexOf(b);
          const orderA = ai === -1 ? savedOrder.length + baseIndexA : ai;
          const orderB = bi === -1 ? savedOrder.length + baseIndexB : bi;
          return orderA - orderB;
        })
      : statuses;

    orderedStatuses.forEach(s => {
      const statusKey = normalizeStatusKey(s.name);
      columns[s.id] = {
        id: s.id,
        title: getStatusDisplayName(t, s.name),
        rawTitle: s.name,
        statusKey,
        isFinal: Boolean(s.isFinal),
        taskIds: [],
        color: pMeta.columnColors?.[s.id] ?? s.color ?? getStatusColor(s.name),
      };
      columnOrder.push(s.id);
    });

    // Преобразуем задачи в формат, который понимает канбан-представление.
    const taskMap = {};
    [...tasks]
      .sort((a, b) => a.order - b.order)
      .forEach(task => {
        const statusKey = normalizeStatusKey(task.statusName);
        // Разрешаем assignee в полноценный объект пользователя с именем, инициалами и аватаром.
        let assigneeObj = null;
        if (task.assigneeId) {
          const u = memberMap.get(task.assigneeId);
          if (u) {
            assigneeObj = { id: u.id, name: u.name, initials: u.initials, avatar: u.avatar };
          } else {
            assigneeObj = { id: task.assigneeId, name: task.assigneeId.slice(0, 8), initials: '??', avatar: null };
          }
        }

        taskMap[task.id] = {
          id: task.id,
          content: task.title,
          title: task.title,
          description: task.description || '',
          priority: mapPriority(task.priority),
          assigneeId: task.assigneeId || null,
          assignee: assigneeObj,
          dueDate: task.dueDate ? task.dueDate.split('T')[0] : null,
          createdBy: task.createdBy,
          status: getStatusDisplayName(t, task.statusName),
          statusRaw: task.statusName,
          statusKey,
          statusId: task.statusId,
          tags: (task.labels || []).map((l) =>
            localizeProjectLabel({ id: l.id, name: l.name, color: l.color }, t),
          ),
          comments: (task.comments || []).map(c => ({
            id: c.id, text: c.text, authorId: c.authorId,
            createdAt: c.createdAt, updatedAt: c.updatedAt,
          })),
          subtasks: (task.subTasks || []).map(s => ({
            id: s.id, title: s.title, isCompleted: !!s.isCompleted, completed: !!s.isCompleted, order: s.order,
          })),
          stickerValues: task.stickerValues || {},
          createdAt: task.createdAt,
          updatedAt: task.updatedAt,
        };
        if (columns[task.statusId]) columns[task.statusId].taskIds.push(task.id);
      });

    // Метки проекта нужны для экрана управления label-ами и карточек задач.
    const projectLabels = (activeProjectMeta.labels || []).map((l) =>
      localizeProjectLabel({ id: l.id, name: l.name, color: l.color }, t),
    );

    return {
      id: activeProjectMeta.id,
      name: activeProjectMeta.name,
      description: activeProjectMeta.description,
      boardStickerIds: activeProjectMeta.boardStickerIds || DEFAULT_BOARD_STICKER_IDS,
      boardAutomations: pMeta.boardAutomations || [],
      boardBackground: effectiveBoardBackground,
      boardBackgroundOverride: projectBoardBackground,
      boardBackgroundOrigin: projectBoardBackground ? 'project' : accountDefaultBoardBackground ? 'account' : null,
      boardData: { tasks: taskMap, columns, columnOrder },
      labels: projectLabels,
    };
  }, [accountDefaultBoardBackground, activeProjectMeta, tasks, getProjectMeta, t, workspaces]);

  // currentUser
  const currentUser = useMemo(() => {
    if (!authUser?.id) {
      return {
        id: 'guest',
        name: 'Guest',
        initials: 'GU',
        avatar: resolveUserAvatar({ seed: 'guest', fallback: 'guest' }),
      };
    }
    const displayName = meBundle?.profile?.displayName || authUser.username || authUser.email || 'User';
    const avatar = resolveUserAvatar({
      avatarUrl: meBundle?.profile?.avatarUrl || authUser.avatarUrl,
      userId: authUser.id,
      displayName,
      username: authUser.username,
      email: authUser.email,
    });
    return {
      id: authUser.id,
      name: displayName,
      initials: toInitials(displayName),
      avatar,
      username: authUser.username || null,
      email: authUser.email || null,
      isSystemAdmin: Boolean(authUser.isSystemAdmin),
    };
  }, [authUser, meBundle]);

  // users — строится из участников активного workspace
  const users = useMemo(() => {
    const map = new Map();
    workspaces.forEach(ws => {
      (ws.members || []).forEach(m => { if (m.userId) map.set(m.userId, mapMemberToUser(m)); });
    });
    if (currentUser?.id && currentUser.id !== 'guest') {
      map.set(currentUser.id, { ...(map.get(currentUser.id) || {}), ...currentUser });
    }
    return Array.from(map.values());
  }, [workspaces, currentUser]);

  // Текущая роль пользователя в активном workspace
  const currentUserRole = useMemo(() => {
    if (!activeWorkspace || !currentUser?.id || currentUser.id === 'guest') return null;
    const member = (activeWorkspace.members || []).find(m => sameUserId(m.userId, currentUser.id));
    return member?.role || null;
  }, [activeWorkspace, currentUser]);

  // Счётчик inbox берём из общего API, а счётчик моих задач считаем только по активному проекту.
  const inboxCount = inboxTasksAll.length;

  const myTasksCount = useMemo(() => {
    const data = activeProject?.boardData;
    const uid = currentUser?.id;
    if (!data || !uid || uid === 'guest') return 0;
    const tasksMap = data.tasks || {};
    return Object.values(tasksMap).filter(t => sameUserId(t.assigneeId, uid)).length;
  }, [activeProject, currentUser?.id]);

  // ── Actions: API‑backed ──────────────────────────────────────────────────

  const createWorkspace = useCallback(async (name, _metadata = {}) => {
    if (!accessToken) return;
    try {
      const ws = await workApi.createWorkspace(accessToken, { name });
      const newWs = { ...ws, projects: [], members: ws.members || [], role: 'Owner' };
      setWorkspaces(prev => [...prev, newWs]);
      setActiveWorkspaceId(ws.id);
      setActiveProjectId(null);
      return ws.id;
    } catch (err) {
      console.error('createWorkspace failed:', err);
      return null;
    }
  }, [accessToken, t]);

  const createProject = useCallback(async (workspaceId, projectName, metadata = {}) => {
    if (!accessToken) return;
    try {
      const proj = await workApi.createProject(accessToken, workspaceId, {
        name: projectName,
        description: metadata.description || '',
      });
      const newProj = {
        id: proj.id,
        name: proj.name,
        description: proj.description,
        createdById: proj.createdById,
        createdAt: proj.createdAt,
        isArchived: proj.isArchived,
        statuses: (proj.statuses || []).map(s => ({
          id: s.id, name: s.name, order: s.order, color: s.color, isFinal: s.isFinal,
        })),
      };
      setWorkspaces(prev => prev.map(ws =>
        ws.id === workspaceId ? { ...ws, projects: [...(ws.projects || []), newProj] } : ws,
      ));
      setActiveWorkspaceId(workspaceId);
      setActiveProjectId(proj.id);
      return { workspaceId, projectId: proj.id };
    } catch (err) {
      console.error('createProject failed:', err);
      return null;
    }
  }, [accessToken]);

  const selectProject = useCallback((projectId) => {
    const ws = workspaces.find(w => (w.projects || []).some(p => p.id === projectId));
    setActiveProjectId(projectId);
    if (ws) setActiveWorkspaceId(ws.id);
  }, [workspaces]);

  const selectWorkspace = useCallback((workspaceId) => {
    setActiveWorkspaceId(workspaceId);
    setActiveProjectId(null);
  }, []);

  // ── Workspace Members ────────────────────────────────────────────────────

  const refreshWorkspaceMembers = useCallback(async (workspaceId) => {
    if (!accessToken || !workspaceId) return;
    try {
      const members = await workApi.getWorkspaceMembers(accessToken, workspaceId);
      setWorkspaces(prev => prev.map(ws =>
        ws.id === workspaceId ? { ...ws, members: members || [] } : ws
      ));
    } catch (err) {
      console.error('refreshWorkspaceMembers failed:', err);
    }
  }, [accessToken]);
  refreshWorkspaceMembersRef.current = refreshWorkspaceMembers;

  const inviteMember = useCallback(async (workspaceId, target, role = 'Member') => {
    if (!accessToken) return;
    try {
      const normalizedTarget =
        target && typeof target === 'object'
          ? target
          : looksLikeEmail(target)
            ? { email: String(target).trim().toLowerCase() }
            : { userId: target };

      if (normalizedTarget.email) {
        const invitation = await workApi.createInvitation(accessToken, workspaceId, {
          email: normalizedTarget.email,
          role,
          expiresInDays: 7,
        });
        return { kind: 'invitation', invitation };
      }

      await workApi.addWorkspaceMember(accessToken, workspaceId, { userId: normalizedTarget.userId, role });
      await refreshWorkspaceMembers(workspaceId);
      return { kind: 'member' };
    } catch (err) {
      console.error('inviteMember failed:', err);
      throw err;
    }
  }, [accessToken, refreshWorkspaceMembers]);

  const changeMemberRole = useCallback(async (workspaceId, userId, role) => {
    if (!accessToken) return;
    try {
      await workApi.updateMemberRole(accessToken, workspaceId, userId, { role });
      setWorkspaces(prev => prev.map(ws =>
        ws.id === workspaceId
          ? { ...ws, members: (ws.members || []).map(m => m.userId === userId ? { ...m, role } : m) }
          : ws
      ));
    } catch (err) {
      console.error('changeMemberRole failed:', err);
      throw err;
    }
  }, [accessToken]);

  const kickMember = useCallback(async (workspaceId, userId) => {
    if (!accessToken) return;
    try {
      await workApi.removeMember(accessToken, workspaceId, userId);
      setWorkspaces(prev => prev.map(ws =>
        ws.id === workspaceId
          ? { ...ws, members: (ws.members || []).filter(m => m.userId !== userId) }
          : ws
      ));
    } catch (err) {
      console.error('kickMember failed:', err);
      throw err;
    }
  }, [accessToken]);

  // ── Task CRUD ────────────────────────────────────────────────────────────

  const addTask = useCallback(async (taskData) => {
    if (!accessToken || !activeProject) return;
    const { columns, columnOrder } = activeProject.boardData;
    const requestedStatus = taskData.statusId ?? taskData.status;
    const targetStatusId = Object.keys(columns).find(
      colId =>
        colId === requestedStatus
        || columns[colId].rawTitle === requestedStatus
        || columns[colId].title === requestedStatus,
    ) || columnOrder[0];

    try {
      const created = await workApi.createTask(accessToken, activeProjectId, {
        title: taskData.title || taskData.content,
        description: taskData.description || '',
        priority: taskData.priority ? mapPriority(taskData.priority) : undefined,
        assigneeId: taskData.assigneeId || null,
        dueDate: taskData.dueDate || null,
      });
      // Если backend вернул другой статус, переносим карточку в фактически назначенную колонку.
      if (targetStatusId && created.statusId !== targetStatusId) {
        await workApi.moveTask(accessToken, created.id, { statusId: targetStatusId, order: 0 });
      }
      await refreshTasks();
      refreshInbox();
    } catch (err) {
      console.error('addTask failed:', err);
    }
  }, [accessToken, activeProject, activeProjectId, refreshTasks, refreshInbox]);

  const updateTask = useCallback(async (taskId, updates) => {
    if (!accessToken) return;
    // Сначала оптимистично обновляем локальное состояние.
    setTasks(prev => prev.map(t =>
      t.id === taskId ? { ...t, ...updates, title: updates.content || updates.title || t.title } : t,
    ));
    try {
      const current = tasks.find(t => t.id === taskId);
      if (!current) return;

      // Если менялся assigneeId, используем специальный endpoint назначения исполнителя.
      if ('assigneeId' in updates && updates.assigneeId !== current.assigneeId) {
        await workApi.assignTask(accessToken, taskId, { assigneeId: updates.assigneeId || null });
      }

      // Остальные поля обновляем через общий updateTask endpoint.
      const hasFieldChanges = ['title', 'content', 'description', 'priority', 'dueDate']
        .some(k => k in updates);
      if (hasFieldChanges) {
        // dueDate нормализуем в ISO 8601, потому что backend ожидает DateTime.
        const rawDue = updates.dueDate ?? current.dueDate;
        const dueDateISO = rawDue && !rawDue.includes('T') ? rawDue + 'T00:00:00Z' : rawDue || null;
        await workApi.updateTask(accessToken, taskId, {
          title: updates.title || updates.content || current.title,
          description: updates.description ?? current.description,
          priority: mapPriority(updates.priority ?? current.priority),
          dueDate: dueDateISO,
        });
      }

      await refreshTasks();
      refreshInbox();
    } catch (err) {
      console.error('updateTask failed:', err);
      await refreshTasks(); // revert
    }
  }, [accessToken, tasks, refreshTasks, refreshInbox]);

  const deleteTask = useCallback(async (taskId) => {
    if (!accessToken) return;
    setTasks(prev => prev.filter(t => t.id !== taskId)); // optimistic
    try {
      await workApi.deleteTask(accessToken, taskId);
    } catch (err) {
      console.error('deleteTask failed:', err);
      await refreshTasks();
    }
    refreshInbox();
  }, [accessToken, refreshTasks, refreshInbox]);

  const moveTask = useCallback(async (taskId, overId) => {
    if (!activeProject) return;
    const { columns } = activeProject.boardData;

    // Определяем колонку назначения.
    let destStatusId;
    if (columns[overId]) {
      destStatusId = overId;
    } else {
      destStatusId = Object.keys(columns).find(colId => columns[colId].taskIds.includes(overId));
    }
    if (!destStatusId) return;

    // Определяем исходную колонку.
    const srcStatusId = Object.keys(columns).find(colId => columns[colId].taskIds.includes(taskId));
    if (!srcStatusId) return;

    // Считаем новый порядок задачи внутри колонки.
    let newOrder = 0;
    if (srcStatusId === destStatusId) {
      const ids = [...columns[destStatusId].taskIds];
      const oldIdx = ids.indexOf(taskId);
      const newIdx = ids.indexOf(overId);
      if (newIdx === -1 || oldIdx === newIdx) return;
      ids.splice(oldIdx, 1);
      ids.splice(newIdx, 0, taskId);
      newOrder = ids.indexOf(taskId);
    } else {
      newOrder = columns[destStatusId].taskIds.length;
    }

    // Сначала делаем optimistic update, затем синхронизируемся с backend.
    setTasks(prev => prev.map(t =>
      t.id === taskId
        ? { ...t, statusId: destStatusId, statusName: columns[destStatusId]?.rawTitle || columns[destStatusId]?.title, order: newOrder }
        : t,
    ));

    try {
      await workApi.moveTask(accessToken, taskId, { statusId: destStatusId, order: newOrder });
      await refreshTasks();
      refreshInbox();
    } catch (err) {
      console.error('moveTask failed:', err);
      await refreshTasks(); // revert
    }
  }, [accessToken, activeProject, refreshTasks, refreshInbox]);

  const addColumn = useCallback(async (title, options = {}) => {
    if (!accessToken || !activeProjectId) return;
    const colors = ['#EF4444', '#F97316', '#F59E0B', '#EAB308', '#22C55E', '#3B82F6', '#6366F1', '#8B5CF6'];
    const color = options.color || colors[Math.floor(Math.random() * colors.length)];
    try {
      const status = await workApi.createStatus(accessToken, activeProjectId, {
        name: title,
        color,
        isFinal: Boolean(options.isFinal),
      });
      setWorkspaces(prev => prev.map(ws => ({
        ...ws,
        projects: (ws.projects || []).map(p =>
          p.id === activeProjectId
            ? { ...p, statuses: [...(p.statuses || []), { id: status.id, name: status.name, order: status.order, color: status.color, isFinal: status.isFinal }] }
            : p,
        ),
      })));
      return status.id;
    } catch (err) {
      console.error('addColumn failed:', err);
      return null;
    }
  }, [accessToken, activeProjectId]);

  // ── Комментарии через backend API ────────────────────────────────────────

  const addComment = useCallback(async (taskId, text) => {
    if (!accessToken || !activeProjectId) return;
    try {
      await workApi.createComment(accessToken, taskId, { text });
      await refreshTasks();
    } catch (err) {
      console.error('addComment failed:', err);
    }
  }, [accessToken, activeProjectId, refreshTasks]);

  const deleteComment = useCallback(async (commentId) => {
    if (!accessToken) return;
    try {
      await workApi.deleteComment(accessToken, commentId);
      await refreshTasks();
    } catch (err) {
      console.error('deleteComment failed:', err);
    }
  }, [accessToken, refreshTasks]);

  const updateComment = useCallback(async (commentId, text) => {
    if (!accessToken) return;
    try {
      await workApi.updateComment(accessToken, commentId, { text });
      await refreshTasks();
    } catch (err) {
      console.error('updateComment failed:', err);
    }
  }, [accessToken, refreshTasks]);

  // ── Подзадачи через backend API ──────────────────────────────────────────

  const addSubTask = useCallback(async (taskId, title) => {
    if (!accessToken) return;
    try {
      await workApi.createSubTask(accessToken, taskId, { title });
      await refreshTasks();
    } catch (err) {
      console.error('addSubTask failed:', err);
    }
  }, [accessToken, refreshTasks]);

  const updateSubTask = useCallback(async (subtaskId, updates) => {
    if (!accessToken) return;
    try {
      await workApi.updateSubTask(accessToken, subtaskId, updates);
      await refreshTasks();

      // Если все подзадачи закрыты, автоматически переносим задачу в колонку «Готово».
      if (updates.isCompleted && activeProject) {
        const updatedTasks = await workApi.getProjectTasks(accessToken, activeProjectId);
        const parentTask = (updatedTasks || []).find(t =>
          (t.subTasks || []).some(s => s.id === subtaskId)
        );
        if (parentTask && parentTask.subTasks?.length > 0) {
          const allDone = parentTask.subTasks.every(s => s.isCompleted);
          if (allDone) {
            const { columns } = activeProject.boardData;
            const doneCol = Object.values(columns).find(c => c.statusKey === 'done' || isDoneStatus(c.rawTitle || c.title));
            if (doneCol && parentTask.statusId !== doneCol.id) {
              await workApi.moveTask(accessToken, parentTask.id, { statusId: doneCol.id, order: 0 });
              await refreshTasks();
            }
          }
        }
      }
    } catch (err) {
      console.error('updateSubTask failed:', err);
    }
  }, [accessToken, activeProject, activeProjectId, refreshTasks]);

  const deleteSubTask = useCallback(async (subtaskId) => {
    if (!accessToken) return;
    try {
      await workApi.deleteSubTask(accessToken, subtaskId);
      await refreshTasks();
    } catch (err) {
      console.error('deleteSubTask failed:', err);
    }
  }, [accessToken, refreshTasks]);

  // ── Метки через backend API ──────────────────────────────────────────────

  const createLabel = useCallback(async (name, color) => {
    if (!accessToken || !activeProjectId) return;
    try {
      const label = await workApi.createProjectLabel(accessToken, activeProjectId, { name, color });
      // Сразу отражаем новую метку в локальном состоянии, чтобы UI не ждал полного refresh.
      setWorkspaces(prev => prev.map(ws => ({
        ...ws,
        projects: (ws.projects || []).map(p =>
          p.id === activeProjectId
            ? {
                ...p,
                labels: [
                  ...(p.labels || []),
                  localizeProjectLabel({ id: label.id, name: label.name, color: label.color }, t),
                ],
              }
            : p
        ),
      })));
      return label;
    } catch (err) {
      console.error('createLabel failed:', err);
    }
  }, [accessToken, activeProjectId, t]);

  const deleteLabel = useCallback(async (labelId) => {
    if (!accessToken) return;
    try {
      await workApi.deleteProjectLabel(accessToken, labelId);
      setWorkspaces(prev => prev.map(ws => ({
        ...ws,
        projects: (ws.projects || []).map(p => ({
          ...p,
          labels: (p.labels || []).filter(l => l.id !== labelId),
        })),
      })));
      await refreshTasks();
    } catch (err) {
      console.error('deleteLabel failed:', err);
    }
  }, [accessToken, refreshTasks]);

  const assignLabel = useCallback(async (taskId, labelId) => {
    if (!accessToken) return;
    try {
      await workApi.assignLabelToTask(accessToken, taskId, { labelId });
      await refreshTasks();
    } catch (err) {
      console.error('assignLabel failed:', err);
    }
  }, [accessToken, refreshTasks]);

  const removeLabel = useCallback(async (taskId, labelId) => {
    if (!accessToken) return;
    try {
      await workApi.removeLabelFromTask(accessToken, taskId, labelId);
      await refreshTasks();
    } catch (err) {
      console.error('removeLabel failed:', err);
    }
  }, [accessToken, refreshTasks]);

  // ── Стикеры, автоматизации и фон доски: локальный UI-слой ────────────────

  const updateProjectMetaField = useCallback((field, value) => {
    if (!activeProjectId) return;
    setProjectMeta(activeProjectId, { [field]: value });
  }, [activeProjectId, setProjectMeta]);

  const getBoardStickers = () => {
    if (!activeProject) return [];
    const ids = activeProject.boardStickerIds || DEFAULT_BOARD_STICKER_IDS;
    return ids.map(id => localizedStickerCatalog.find(s => s.id === id)).filter(Boolean);
  };

  const addBoardSticker = useCallback(async (stickerId) => {
    if (!accessToken || !activeProjectId) return;
    const current = activeProject?.boardStickerIds || DEFAULT_BOARD_STICKER_IDS;
    if (current.includes(stickerId)) return;
    const newIds = [...current, stickerId];
    try {
      await workApi.updateBoardStickers(accessToken, activeProjectId, newIds);
      // Сразу обновляем локальное состояние, чтобы UI отреагировал без дополнительного запроса.
      setWorkspaces(prev => prev.map(ws => ({
        ...ws,
        projects: (ws.projects || []).map(p =>
          p.id === activeProjectId ? { ...p, boardStickerIds: newIds } : p
        ),
      })));
    } catch (err) {
      console.error('addBoardSticker failed:', err);
    }
  }, [accessToken, activeProjectId, activeProject?.boardStickerIds]);

  const removeBoardSticker = useCallback(async (stickerId) => {
    if (!accessToken || !activeProjectId) return;
    const current = activeProject?.boardStickerIds || DEFAULT_BOARD_STICKER_IDS;
    const newIds = current.filter(id => id !== stickerId);
    try {
      await workApi.updateBoardStickers(accessToken, activeProjectId, newIds);
      setWorkspaces(prev => prev.map(ws => ({
        ...ws,
        projects: (ws.projects || []).map(p =>
          p.id === activeProjectId ? { ...p, boardStickerIds: newIds } : p
        ),
      })));
    } catch (err) {
      console.error('removeBoardSticker failed:', err);
    }
  }, [accessToken, activeProjectId, activeProject?.boardStickerIds]);

  const addBoardAutomation = useCallback((data) => {
    if (!activeProjectId) return;
    const current = getProjectMeta(activeProjectId).boardAutomations || [];
    const newAuto = {
      id: `auto-${Date.now()}`,
      name: data.name, trigger: data.trigger, action: data.action, enabled: true,
    };
    updateProjectMetaField('boardAutomations', [...current, newAuto]);
  }, [activeProjectId, getProjectMeta, updateProjectMetaField]);

  const removeBoardAutomation = useCallback((autoId) => {
    if (!activeProjectId) return;
    const current = getProjectMeta(activeProjectId).boardAutomations || [];
    updateProjectMetaField('boardAutomations', current.filter(a => a.id !== autoId));
  }, [activeProjectId, getProjectMeta, updateProjectMetaField]);

  const toggleBoardAutomation = useCallback((autoId) => {
    if (!activeProjectId) return;
    const current = getProjectMeta(activeProjectId).boardAutomations || [];
    updateProjectMetaField('boardAutomations', current.map(a => a.id === autoId ? { ...a, enabled: !a.enabled } : a));
  }, [activeProjectId, getProjectMeta, updateProjectMetaField]);

  const updateTaskStickerValue = useCallback(async (taskId, stickerId, value) => {
    if (!accessToken || !activeProjectId) return;
    try {
      await workApi.updateTaskStickers(accessToken, taskId, { [stickerId]: value || '' });
      await refreshTasks();
    } catch (err) {
      console.error('updateTaskStickers failed:', err);
    }
  }, [accessToken, activeProjectId, refreshTasks]);

  const setDefaultBoardBackground = useCallback(async (bg) => {
    if (!accessToken) return null;

    try {
      const nextPreferences = await updateMyPreferences(accessToken, bg
        ? { defaultBoardBackground: bg }
        : { resetDefaultBoardBackground: true });

      setMeBundle((prev) => {
        if (!prev) {
          return prev;
        }

        return {
          ...prev,
          preferences: {
            ...(prev.preferences || {}),
            ...(nextPreferences || {}),
            defaultBoardBackground: nextPreferences?.defaultBoardBackground || null,
          },
        };
      });

      return nextPreferences?.defaultBoardBackground || null;
    } catch (err) {
      console.error('setDefaultBoardBackground failed:', err);
      return null;
    }
  }, [accessToken]);

  const clearProjectBoardBackground = useCallback(() => {
    updateProjectMetaField('boardBackground', null);
  }, [updateProjectMetaField]);

  const clearDefaultBoardBackground = useCallback(async () => {
    return setDefaultBoardBackground(null);
  }, [setDefaultBoardBackground]);

  const setBoardBackground = useCallback(async (bg, options = {}) => {
    if (options.forAll || !activeProjectId) {
      return setDefaultBoardBackground(bg);
    }

    updateProjectMetaField('boardBackground', bg);
    return bg;
  }, [activeProjectId, setDefaultBoardBackground, updateProjectMetaField]);

  const updateColumnColor = useCallback((columnId, color) => {
    if (!activeProjectId) return;
    const meta = getProjectMeta(activeProjectId);
    setProjectMeta(activeProjectId, {
      columnColors: { ...(meta.columnColors || {}), [columnId]: color },
    });
  }, [activeProjectId, getProjectMeta, setProjectMeta]);

  const reorderColumns = useCallback((activeColumnId, overColumnId) => {
    if (!activeProjectId) return;
    const baseOrder = [...(activeProject?.boardData?.columnOrder || [])];

    const oldIndex = baseOrder.indexOf(activeColumnId);
    const newIndex = baseOrder.indexOf(overColumnId);
    if (oldIndex === -1 || newIndex === -1 || oldIndex === newIndex) return;

    baseOrder.splice(oldIndex, 1);
    baseOrder.splice(newIndex, 0, activeColumnId);

    // Сохраняем новый порядок колонок в project meta как frontend-only метаданные.
    setProjectMeta(activeProjectId, { columnOrder: baseOrder });
  }, [activeProject, activeProjectId, getProjectMeta, setProjectMeta]);

  const reorderColumnsToOrder = useCallback(async (newColumnIds) => {
    if (!accessToken || !activeProjectId || !Array.isArray(newColumnIds) || newColumnIds.length === 0) return;

    await workApi.reorderStatuses(accessToken, activeProjectId, newColumnIds);

    setWorkspaces(prev => prev.map(ws => ({
      ...ws,
      projects: (ws.projects || []).map(project => {
        if (project.id !== activeProjectId) return project;

        const statusesById = new Map((project.statuses || []).map(status => [status.id, status]));
        const reorderedStatuses = newColumnIds
          .map((statusId, index) => {
            const status = statusesById.get(statusId);
            if (!status) return null;
            return { ...status, order: index };
          })
          .filter(Boolean);

        const missingStatuses = (project.statuses || [])
          .filter(status => !newColumnIds.includes(status.id))
          .map((status, index) => ({ ...status, order: reorderedStatuses.length + index }));

        return {
          ...project,
          statuses: [...reorderedStatuses, ...missingStatuses],
        };
      }),
    })));

    setProjectMeta(activeProjectId, { columnOrder: [...newColumnIds] });
  }, [accessToken, activeProjectId, setProjectMeta]);

  // ── Удаление workspace и проектов ────────────────────────────────────────

  const deleteWorkspace = useCallback(async (wsId) => {
    try {
      await workApi.deleteWorkspace(accessToken, wsId);
    } catch (err) {
      console.error('Failed to delete workspace:', err);
      throw err;
    }
    setWorkspaces(prev => {
      const next = prev.filter(w => w.id !== wsId);
      if (activeWorkspaceId === wsId) {
        setActiveWorkspaceId(next.length > 0 ? next[0].id : null);
        setActiveProjectId(next.length > 0 ? (next[0].projects?.[0]?.id ?? null) : null);
      }
      return next;
    });
  }, [accessToken, activeWorkspaceId]);

  const reorderWorkspaces = useCallback((activeWorkspaceIdLocal, overWorkspaceIdLocal) => {
    setWorkspaces(prev => {
      const next = [...prev];
      const oldIndex = next.findIndex(w => w.id === activeWorkspaceIdLocal);
      const newIndex = next.findIndex(w => w.id === overWorkspaceIdLocal);
      if (oldIndex === -1 || newIndex === -1 || oldIndex === newIndex) return prev;
      const [moved] = next.splice(oldIndex, 1);
      next.splice(newIndex, 0, moved);
      return next;
    });
  }, []);

  const deleteProject = useCallback(async (wsId, projectId) => {
    if (!accessToken) throw new Error('Not authenticated');
    
    try {
      const result = await workApi.deleteProject(accessToken, projectId);
      console.log('[deleteProject] API success, result:', result);
    } catch (err) {
      console.error('[deleteProject] API failed:', err);
      throw new Error(err.message || 'Failed to delete project');
    }
    
    // Локальное состояние меняем только после успешного ответа API.
    setWorkspaces(prev => prev.map(ws =>
      ws.id === wsId ? { ...ws, projects: (ws.projects || []).filter(p => p.id !== projectId) } : ws,
    ));
    if (activeProjectId === projectId) {
      const ws = workspaces.find(w => w.id === wsId);
      const remaining = (ws?.projects || []).filter(p => p.id !== projectId);
      setActiveProjectId(remaining.length > 0 ? remaining[0].id : null);
    }
  }, [accessToken, activeProjectId, workspaces]);

  const resetData = useCallback(() => {
    localStorage.removeItem(UI_META_KEY);
    setWorkspaces([]);
    setActiveWorkspaceId(null);
    setActiveProjectId(null);
    setTasks([]);
    setUiMeta({});
    window.location.reload();
  }, []);

  // ── Доступные теги, полученные из API ────────────────────────────────────

  const availableTags = useMemo(
    () => activeProject?.labels || [],
    [activeProject?.labels],
  );

  // ── Значение контекста, которое отдаём всему platform-слою ──────────────

  return (
    <PlatformContext.Provider value={{
      workspaces,
      activeWorkspaceId,
      activeWorkspace,
      activeProjectId,
      activeProject,
      loading,
      bootstrapped,
      meBundle,
      meBundleLoading,
      refreshMeBundle,
      boardStickers: getBoardStickers(),
      boardStickerIds: activeProject?.boardStickerIds || DEFAULT_BOARD_STICKER_IDS,
      boardAutomations: activeProject?.boardAutomations || [],
      stickerCatalog: localizedStickerCatalog,
      createWorkspace,
      createProject,
      selectProject,
      selectWorkspace,
      deleteWorkspace,
      deleteProject,
      reorderWorkspaces,
      resetData,
      moveTask,
      addTask,
      addColumn,
      updateTask,
      deleteTask,
      // комментарии
      addComment,
      deleteComment,
      updateComment,
      // подзадачи
      addSubTask,
      updateSubTask,
      deleteSubTask,
      // метки
      createLabel,
      deleteLabel,
      assignLabel,
      removeLabel,
      // стикеры доски и автоматизации
      addBoardSticker,
      removeBoardSticker,
      addBoardAutomation,
      removeBoardAutomation,
      toggleBoardAutomation,
      updateTaskStickerValue,
      boardBackground: activeProject?.boardBackground || null,
      defaultBoardBackground: accountDefaultBoardBackground,
      projectBoardBackgroundOverride: activeProject?.boardBackgroundOverride || null,
      boardBackgroundOrigin: activeProject?.boardBackgroundOrigin || null,
      setBoardBackground,
      clearProjectBoardBackground,
      clearDefaultBoardBackground,
      updateColumnColor,
      reorderColumns,
      reorderColumnsToOrder,
      users,
      availableTags,
      currentUser,
      currentUserRole,
      activePage,
      setActivePage,
      inboxCount,
      myTasksCount,
      inboxTasksAll,
      inboxLoading,
      inboxError,
      refreshInbox,
      // управление участниками
      inviteMember,
      changeMemberRole,
      kickMember,
      refreshWorkspaceMembers,
      // уведомления
      notifications,
      unreadCount,
      markNotificationRead,
      markAllNotificationsRead,
      handleNotificationAction,
      refreshNotifications,
    }}>
      {children}
    </PlatformContext.Provider>
  );
};

export const usePlatform = () => {
  const context = useContext(PlatformContext);
  if (!context) {
    throw new Error('usePlatform must be used within PlatformProvider');
  }
  return context;
};
