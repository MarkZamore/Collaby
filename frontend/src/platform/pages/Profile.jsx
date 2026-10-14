import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Award,
  Camera,
  Globe,
  Image as ImageIcon,
  LogOut,
  Mail,
  MapPin,
  Save,
  Settings,
  User,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import BackgroundPicker from '../components/BackgroundPicker';
import { cn } from '../../shared/utils/cn';
import { useAuth } from '../../shared/context/AuthContext';
import { useGamification } from '../../shared/context/GamificationContext';
import { usePreferences } from '../context/PreferencesContext';
import { usePlatform } from '../context/PlatformContext';
import i18n, { normalizeLanguage } from '../../shared/i18n/config';
import { resolveUserAvatar } from '../../shared/utils/avatar';
import { getEquippedAvatarFrame } from '../../shared/utils/avatarFrames';
import AvatarWithFrame from '../../shared/components/AvatarWithFrame';
import Skeleton from '../../shared/components/Skeleton';
import { formatDateTime } from '../../shared/utils/locale';
import {
  completeMyOnboarding,
  updateMyOnboarding,
  updateMyPreferences,
  updateMyProfile,
} from '../../shared/utils/usersApi';

const ONBOARDING_STATUSES = ['not_started', 'in_progress', 'completed'];

const createDefaultProfile = (fallbackName = '') => ({
  displayName: fallbackName,
  avatarUrl: '',
  bio: '',
  timezone: 'UTC',
  language: normalizeLanguage(i18n.resolvedLanguage || i18n.language),
});

const createDefaultPreferences = () => ({
  theme: 'dark',
  emailNotifications: true,
  pushNotifications: true,
  weeklyDigest: false,
  defaultBoardBackground: null,
});

const createDefaultOnboarding = () => ({
  status: 'not_started',
  currentStep: 0,
  completedAt: null,
});

const Toast = ({ toast, onClose }) => {
  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timer = setTimeout(onClose, 2500);
    return () => clearTimeout(timer);
  }, [toast, onClose]);

  if (!toast) {
    return null;
  }

  const isError = toast.type === 'error';

  return (
    <div className="fixed bottom-6 right-6 z-50 animate-in fade-in slide-in-from-bottom-4 duration-300">
      <div
        className="flex items-center gap-2 rounded-xl border px-5 py-3 shadow-2xl"
        data-testid={isError ? 'toast-error' : 'toast-success'}
        style={{
          backgroundColor: 'var(--app-layer-surface)',
          borderColor: isError ? 'var(--color-error, #ef4444)' : 'var(--app-accent)',
          color: 'var(--app-text-main)',
        }}
      >
        <Save size={16} className={isError ? 'text-red-400' : 'text-green-400'} />
        <span className="text-sm font-medium">{toast.message}</span>
      </div>
    </div>
  );
};

const Profile = () => {
  const { t } = useTranslation(['profile', 'common', 'platform', 'errors']);
  const { accessToken, currentUser: authCurrentUser, logout } = useAuth();
  const { xp: gamXp, level: gamLevel, inventory } = useGamification();
  const { language, setLanguage, setThemeMode } = usePreferences();
  const {
    currentUser: platformCurrentUser,
    meBundle,
    meBundleLoading,
    refreshMeBundle,
  } = usePlatform();
  const currentUser = platformCurrentUser || authCurrentUser;

  const [activeTab, setActiveTab] = useState('personal');
  const [saving, setSaving] = useState(false);
  const [loggingOut, setLoggingOut] = useState(false);
  const [toast, setToast] = useState(null);
  const [profile, setProfile] = useState(createDefaultProfile());
  const [preferences, setPreferences] = useState(createDefaultPreferences());
  const [onboarding, setOnboarding] = useState(createDefaultOnboarding());
  const [activity, setActivity] = useState({ lastActiveAt: null });
  const [snapshot, setSnapshot] = useState(null);

  const applyLanguagePreference = useCallback(
    (nextLanguage, markDirty = false) => {
      setProfile((prev) => ({ ...prev, language: nextLanguage }));
      setLanguage(nextLanguage, { markDirty });
    },
    [setLanguage]
  );

  const applyThemePreference = useCallback(
    (nextTheme, markDirty = false) => {
      setPreferences((prev) => ({ ...prev, theme: nextTheme }));
      setThemeMode(nextTheme, { markDirty });
    },
    [setThemeMode]
  );

  const applyBundle = useCallback(
    (bundle) => {
      const fallbackName = currentUser?.username || '';
      const nextProfile = {
        ...createDefaultProfile(fallbackName),
        ...(bundle?.profile || {}),
        displayName: bundle?.profile?.displayName || fallbackName,
      };
      const nextPreferences = {
        ...createDefaultPreferences(),
        ...(bundle?.preferences || {}),
      };
      const nextOnboarding = {
        ...createDefaultOnboarding(),
        ...(bundle?.onboarding || {}),
      };

      setProfile(nextProfile);
      setPreferences(nextPreferences);
      setOnboarding(nextOnboarding);
      setActivity({ lastActiveAt: bundle?.activity?.lastActiveAt || null });
      setSnapshot({
        profile: nextProfile,
        preferences: nextPreferences,
        onboarding: nextOnboarding,
      });
      setLanguage(nextProfile.language, { markDirty: false });
      setThemeMode(nextPreferences.theme, { markDirty: false });
    },
    [currentUser?.username, setLanguage, setThemeMode]
  );

  useEffect(() => {
    if (!accessToken || !meBundle) {
      return;
    }

    applyBundle(meBundle);
  }, [accessToken, applyBundle, meBundle]);

  useEffect(() => {
    if (!accessToken || meBundleLoading || meBundle || snapshot) {
      return;
    }

    setToast({
      type: 'error',
      message: t('profile:toast.loadError'),
    });
  }, [accessToken, meBundle, meBundleLoading, snapshot, t]);

  const loading = meBundleLoading && !snapshot;
  const equippedAvatarFrame = getEquippedAvatarFrame(inventory);

  const avatarUrl = useMemo(() => {
    return resolveUserAvatar({
      avatarUrl: profile.avatarUrl,
      displayName: profile.displayName,
      username: currentUser?.username,
      email: currentUser?.email,
      userId: currentUser?.id,
    });
  }, [currentUser?.email, currentUser?.id, currentUser?.username, profile.avatarUrl, profile.displayName]);

  const tabs = useMemo(
    () => [
      { id: 'personal', label: t('profile:tabs.personal'), icon: User },
      { id: 'skills', label: t('profile:tabs.skills'), icon: Award },
      { id: 'background', label: t('profile:tabs.background'), icon: ImageIcon },
      { id: 'settings', label: t('profile:tabs.preferences'), icon: Settings },
    ],
    [t]
  );

  const skills = useMemo(() => {
    const currentLevelFloor = gamLevel <= 1 ? 0 : gamLevel * 100;
    const nextLevelThreshold = (gamLevel + 1) * 100;
    return [
      {
        name: t('profile:skills.overallLevel'),
        current: Math.max(gamXp - currentLevelFloor, 0),
        max: Math.max(nextLevelThreshold - currentLevelFloor, 1),
        color: 'bg-orange-500',
      },
    ];
  }, [gamLevel, gamXp, t]);

  const lastActiveLabel = activity.lastActiveAt
    ? formatDateTime(activity.lastActiveAt, language)
    : t('profile:preferences.neverActive');

  const completedAtLabel = onboarding.completedAt
    ? formatDateTime(onboarding.completedAt, language)
    : t('profile:onboarding.notCompleted');

  const handleCancel = useCallback(() => {
    if (!snapshot) {
      return;
    }

    setProfile(snapshot.profile);
    setPreferences(snapshot.preferences);
    setOnboarding(snapshot.onboarding);
    setLanguage(snapshot.profile.language, { markDirty: false });
    setThemeMode(snapshot.preferences.theme, { markDirty: false });
  }, [setLanguage, setThemeMode, snapshot]);

  const handleCompleteOnboarding = useCallback(async () => {
    if (!accessToken) {
      return;
    }

    setSaving(true);
    try {
      const completed = await completeMyOnboarding(accessToken);
      const nextOnboarding = {
        status: completed?.status || 'completed',
        currentStep:
          typeof completed?.currentStep === 'number'
            ? completed.currentStep
            : Math.max(onboarding.currentStep, 100),
        completedAt: completed?.completedAt || new Date().toISOString(),
      };

      setOnboarding(nextOnboarding);
      setSnapshot((prev) => (prev ? { ...prev, onboarding: nextOnboarding } : prev));
      await refreshMeBundle({ ping: false });
      const nextLanguage = profile.language || language;
      const fixedProfileT = i18n.getFixedT(nextLanguage, 'profile');
      setToast({ type: 'success', message: fixedProfileT('toast.onboardingCompleted') });
    } catch (error) {
      setToast({
        type: 'error',
        message: error?.message || t('errors:generic.completeOnboarding'),
      });
    } finally {
      setSaving(false);
    }
  }, [accessToken, language, onboarding.currentStep, profile.language, refreshMeBundle, t]);

  const handleSave = useCallback(async () => {
    if (!accessToken) {
      return;
    }

    setSaving(true);
    try {
      await Promise.all([
        updateMyProfile(accessToken, {
          displayName: profile.displayName,
          avatarUrl: profile.avatarUrl || null,
          bio: profile.bio || null,
          timezone: profile.timezone,
          language: profile.language,
        }),
        updateMyPreferences(accessToken, {
          theme: preferences.theme,
          emailNotifications: preferences.emailNotifications,
          pushNotifications: preferences.pushNotifications,
          weeklyDigest: preferences.weeklyDigest,
        }),
        updateMyOnboarding(accessToken, {
          status: onboarding.status,
          currentStep: Number(onboarding.currentStep) || 0,
        }),
      ]);

      await refreshMeBundle({ ping: true });
      const nextLanguage = profile.language || language;
      const fixedProfileT = i18n.getFixedT(nextLanguage, 'profile');
      setToast({ type: 'success', message: fixedProfileT('toast.saved') });
    } catch (error) {
      setToast({
        type: 'error',
        message: error?.message || t('errors:generic.saveProfile'),
      });
    } finally {
      setSaving(false);
    }
  }, [accessToken, language, onboarding, preferences, profile, refreshMeBundle, t]);

  const handleLogout = useCallback(async () => {
    setLoggingOut(true);
    await logout();
  }, [logout]);

  return (
    <main className="custom-scrollbar flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8">
      <div className="mx-auto grid max-w-6xl grid-cols-1 gap-6 md:grid-cols-[280px_1fr] lg:grid-cols-[350px_1fr]">
              <div className="space-y-6">
                <div
                  className="overflow-hidden rounded-2xl border p-6"
                  style={{
                    backgroundColor: 'var(--app-layer-surface)',
                    borderColor: 'var(--app-border)',
                  }}
                >
                  <div className="flex flex-col items-center text-center">
                    <div className="group relative mb-4">
                      <AvatarWithFrame
                        src={avatarUrl}
                        alt={t('profile:messages.profileImageAlt')}
                        frame={equippedAvatarFrame}
                        sizeClassName="h-32 w-32"
                        noFrameShellClassName="bg-gradient-to-br from-accent to-purple-500 p-1 shadow-lg"
                        noFrameInnerClassName="p-1"
                      />
                      <div className="absolute bottom-0 right-0 rounded-full bg-accent p-2 text-white opacity-0 transition-opacity group-hover:opacity-100">
                        <Camera size={16} />
                      </div>
                    </div>

                    <h2 className="mb-1 text-xl font-bold">
                      {profile.displayName || currentUser?.username || t('platform:sidebar.defaultUser')}
                    </h2>
                    <p className="mb-2 text-sm font-medium" style={{ color: 'var(--app-text-secondary)' }}>
                      {currentUser?.username || 'collaby-member'}
                    </p>
                    <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                      {t('profile:preferences.lastActive')}: {lastActiveLabel}
                    </p>
                  </div>
                </div>
              </div>

              <div
                className="flex flex-col overflow-hidden rounded-2xl border"
                style={{
                  backgroundColor: 'var(--app-layer-surface)',
                  borderColor: 'var(--app-border)',
                }}
              >
                <div className="flex overflow-x-auto border-b" style={{ borderColor: 'var(--app-border)' }}>
                  {tabs.map((tab) => (
                    <button
                      key={tab.id}
                      type="button"
                      onClick={() => setActiveTab(tab.id)}
                      className={cn(
                        'flex items-center gap-2 whitespace-nowrap border-b-2 px-3 py-3 text-sm font-bold transition-all sm:px-6 sm:py-4',
                        activeTab === tab.id ? 'border-[var(--app-accent)]' : 'border-transparent hover:opacity-80'
                      )}
                      style={{
                        color: activeTab === tab.id ? 'var(--app-accent)' : 'var(--app-text-secondary)',
                      }}
                    >
                      <tab.icon size={18} />
                      {tab.label}
                    </button>
                  ))}
                </div>

                <div className="flex-1 p-6 lg:p-8">
                  {loading && (
                    <div className="space-y-6">
                      <div className="flex items-center gap-4">
                        <Skeleton circle size={48} />
                        <div className="flex-1 space-y-2">
                          <Skeleton width="50%" height={16} />
                          <Skeleton width="30%" height={12} />
                        </div>
                      </div>
                      <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                        <div className="space-y-2">
                          <Skeleton width="30%" height={12} />
                          <Skeleton width="100%" height={42} style={{ borderRadius: 12 }} />
                        </div>
                        <div className="space-y-2">
                          <Skeleton width="30%" height={12} />
                          <Skeleton width="100%" height={42} style={{ borderRadius: 12 }} />
                        </div>
                      </div>
                      <div className="space-y-2">
                        <Skeleton width="20%" height={12} />
                        <Skeleton width="100%" height={80} style={{ borderRadius: 12 }} />
                      </div>
                    </div>
                  )}

                  {!loading && activeTab === 'personal' && (
                    <div className="space-y-6">
                      <div className="space-y-2">
                        <h3 className="text-base font-bold">{t('profile:personal.title')}</h3>
                        <p className="text-sm" style={{ color: 'var(--app-text-secondary)' }}>
                          {t('profile:personal.description')}
                        </p>
                      </div>

                      <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                        <div className="space-y-2">
                          <label className="text-sm font-bold" style={{ color: 'var(--app-text-secondary)' }}>
                            {t('profile:form.displayName')}
                          </label>
                          <div className="relative">
                            <User className="absolute left-3 top-1/2 -translate-y-1/2" size={18} style={{ color: 'var(--app-text-secondary)' }} />
                            <input
                              type="text"
                              value={profile.displayName}
                              data-testid="profile-displayName"
                              onChange={(event) => setProfile((prev) => ({ ...prev, displayName: event.target.value }))}
                              className="w-full rounded-xl border py-2.5 pl-10 pr-4 outline-none focus:ring-2 focus:ring-accent"
                              style={{
                                backgroundColor: 'var(--app-bg)',
                                borderColor: 'var(--app-border)',
                                color: 'var(--app-text-main)',
                              }}
                            />
                          </div>
                          <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                            {t('profile:personal.displayNameHelp')}
                          </p>
                        </div>

                        <div className="space-y-2">
                          <label className="text-sm font-bold" style={{ color: 'var(--app-text-secondary)' }}>
                            {t('profile:form.email')}
                          </label>
                          <div className="relative">
                            <Mail className="absolute left-3 top-1/2 -translate-y-1/2" size={18} style={{ color: 'var(--app-text-secondary)' }} />
                            <input
                              type="email"
                              value={currentUser?.email || ''}
                              readOnly
                              className="w-full rounded-xl border py-2.5 pl-10 pr-4 opacity-70"
                              style={{
                                backgroundColor: 'var(--app-bg)',
                                borderColor: 'var(--app-border)',
                                color: 'var(--app-text-main)',
                              }}
                            />
                          </div>
                        </div>

                        <div className="space-y-2">
                          <label className="text-sm font-bold" style={{ color: 'var(--app-text-secondary)' }}>
                            {t('profile:form.timezone')}
                          </label>
                          <div className="relative">
                            <MapPin className="absolute left-3 top-1/2 -translate-y-1/2" size={18} style={{ color: 'var(--app-text-secondary)' }} />
                            <input
                              type="text"
                              value={profile.timezone}
                              data-testid="profile-timezone"
                              onChange={(event) => setProfile((prev) => ({ ...prev, timezone: event.target.value }))}
                              className="w-full rounded-xl border py-2.5 pl-10 pr-4 outline-none focus:ring-2 focus:ring-accent"
                              style={{
                                backgroundColor: 'var(--app-bg)',
                                borderColor: 'var(--app-border)',
                                color: 'var(--app-text-main)',
                              }}
                            />
                          </div>
                          <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                            {t('profile:personal.timezoneHelp')}
                          </p>
                        </div>

                        <div className="space-y-2">
                          <label className="text-sm font-bold" style={{ color: 'var(--app-text-secondary)' }}>
                            {t('profile:form.language')}
                          </label>
                          <div className="relative">
                            <Globe className="absolute left-3 top-1/2 -translate-y-1/2" size={18} style={{ color: 'var(--app-text-secondary)' }} />
                            <select
                              value={profile.language}
                              data-testid="profile-language"
                              onChange={(event) => applyLanguagePreference(event.target.value, true)}
                              className="w-full rounded-xl border py-2.5 pl-10 pr-4 outline-none focus:ring-2 focus:ring-accent"
                              style={{
                                backgroundColor: 'var(--app-bg)',
                                borderColor: 'var(--app-border)',
                                color: 'var(--app-text-main)',
                              }}
                            >
                              <option value="en">EN</option>
                              <option value="ru">RU</option>
                              <option value="kk">KZ</option>
                            </select>
                          </div>
                        </div>
                      </div>

                      <div className="space-y-2">
                        <label className="text-sm font-bold" style={{ color: 'var(--app-text-secondary)' }}>
                          {t('profile:form.avatarUrl')}
                        </label>
                        <input
                          type="text"
                          value={profile.avatarUrl}
                          data-testid="profile-avatarUrl"
                          onChange={(event) => setProfile((prev) => ({ ...prev, avatarUrl: event.target.value }))}
                          className="w-full rounded-xl border p-4 outline-none focus:ring-2 focus:ring-accent"
                          style={{
                            backgroundColor: 'var(--app-bg)',
                            borderColor: 'var(--app-border)',
                            color: 'var(--app-text-main)',
                          }}
                        />
                        <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                          {t('profile:personal.avatarHelp')}
                        </p>
                      </div>

                      <div className="space-y-2">
                        <label className="text-sm font-bold" style={{ color: 'var(--app-text-secondary)' }}>
                          {t('profile:form.bio')}
                        </label>
                        <textarea
                          value={profile.bio}
                          data-testid="profile-bio"
                          onChange={(event) => setProfile((prev) => ({ ...prev, bio: event.target.value }))}
                          rows={4}
                          className="w-full resize-none rounded-xl border p-4 outline-none focus:ring-2 focus:ring-accent"
                          style={{
                            backgroundColor: 'var(--app-bg)',
                            borderColor: 'var(--app-border)',
                            color: 'var(--app-text-main)',
                          }}
                        />
                        <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                          {t('profile:personal.bioHelp')}
                        </p>
                      </div>

                      <div className="space-y-3 rounded-xl border p-4" style={{ borderColor: 'var(--app-border)' }}>
                        <div className="text-sm font-bold">{t('profile:onboarding.title')}</div>
                        <div
                          className="rounded-lg border px-3 py-2 text-xs leading-5"
                          style={{
                            borderColor: 'var(--app-border)',
                            backgroundColor: 'var(--app-layer-base)',
                            color: 'var(--app-text-secondary)',
                          }}
                        >
                          {t('profile:onboarding.help')}
                        </div>
                        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                          <div className="space-y-1">
                            <label className="text-xs font-bold" style={{ color: 'var(--app-text-secondary)' }}>
                              {t('profile:onboarding.status')}
                            </label>
                            <select
                              value={onboarding.status}
                              data-testid="onboarding-status"
                              onChange={(event) => setOnboarding((prev) => ({ ...prev, status: event.target.value }))}
                              className="w-full rounded-lg border px-3 py-2"
                              style={{
                                backgroundColor: 'var(--app-bg)',
                                borderColor: 'var(--app-border)',
                                color: 'var(--app-text-main)',
                              }}
                            >
                              {ONBOARDING_STATUSES.map((status) => (
                                <option value={status} key={status}>
                                  {t(`profile:statusOptions.${status}`)}
                                </option>
                              ))}
                            </select>
                          </div>
                          <div className="space-y-1">
                            <label className="text-xs font-bold" style={{ color: 'var(--app-text-secondary)' }}>
                              {t('profile:onboarding.step')}
                            </label>
                            <input
                              type="number"
                              min={0}
                              max={100}
                              value={onboarding.currentStep}
                              data-testid="onboarding-step"
                              onChange={(event) =>
                                setOnboarding((prev) => ({
                                  ...prev,
                                  currentStep: Math.max(0, Math.min(100, Number(event.target.value) || 0)),
                                }))
                              }
                              className="w-full rounded-lg border px-3 py-2"
                              style={{
                                backgroundColor: 'var(--app-bg)',
                                borderColor: 'var(--app-border)',
                                color: 'var(--app-text-main)',
                              }}
                            />
                          </div>
                        </div>
                        <div className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                          {t('profile:onboarding.completed')}: {completedAtLabel}
                        </div>
                        <button
                          type="button"
                          data-testid="onboarding-complete"
                          onClick={handleCompleteOnboarding}
                          disabled={saving}
                          className="rounded-xl border px-4 py-2 text-sm font-bold transition-all hover:opacity-80"
                          style={{
                            borderColor: 'var(--app-border)',
                            color: 'var(--app-text-main)',
                            backgroundColor: 'var(--app-layer-base)',
                          }}
                        >
                          {t('profile:buttons.completeOnboarding')}
                        </button>
                      </div>
                    </div>
                  )}

                  {!loading && activeTab === 'settings' && (
                    <div className="max-w-2xl space-y-6">
                      <div className="space-y-2">
                        <h3 className="text-base font-bold">{t('profile:preferences.title')}</h3>
                        <p className="text-sm" style={{ color: 'var(--app-text-secondary)' }}>
                          {t('profile:preferences.description')}
                        </p>
                      </div>

                      <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                        <div className="space-y-2">
                          <label className="text-sm font-bold" style={{ color: 'var(--app-text-secondary)' }}>
                            {t('profile:preferences.themeLabel')}
                          </label>
                          <select
                            value={preferences.theme}
                            data-testid="pref-theme"
                            onChange={(event) => applyThemePreference(event.target.value, true)}
                            className="w-full rounded-xl border px-4 py-3 outline-none focus:ring-2 focus:ring-accent"
                            style={{
                              backgroundColor: 'var(--app-bg)',
                              borderColor: 'var(--app-border)',
                              color: 'var(--app-text-main)',
                            }}
                          >
                            <option value="light">{t('common:theme.light')}</option>
                            <option value="dark">{t('common:theme.dark')}</option>
                            <option value="system">{t('common:theme.system')}</option>
                          </select>
                        </div>

                        <div className="space-y-2">
                          <label className="text-sm font-bold" style={{ color: 'var(--app-text-secondary)' }}>
                            {t('profile:preferences.languageLabel')}
                          </label>
                          <select
                            value={profile.language}
                            data-testid="pref-language"
                            onChange={(event) => applyLanguagePreference(event.target.value, true)}
                            className="w-full rounded-xl border px-4 py-3 outline-none focus:ring-2 focus:ring-accent"
                            style={{
                              backgroundColor: 'var(--app-bg)',
                              borderColor: 'var(--app-border)',
                              color: 'var(--app-text-main)',
                            }}
                          >
                            <option value="en">EN</option>
                            <option value="ru">RU</option>
                            <option value="kk">KZ</option>
                          </select>
                        </div>
                      </div>

                      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                        <label className="flex items-center gap-3 text-sm">
                          <input
                            type="checkbox"
                            checked={preferences.emailNotifications}
                            data-testid="pref-emailNotif"
                            onChange={(event) => setPreferences((prev) => ({ ...prev, emailNotifications: event.target.checked }))}
                          />
                          {t('profile:notifications.email')}
                        </label>
                        <label className="flex items-center gap-3 text-sm">
                          <input
                            type="checkbox"
                            checked={preferences.pushNotifications}
                            data-testid="pref-pushNotif"
                            onChange={(event) => setPreferences((prev) => ({ ...prev, pushNotifications: event.target.checked }))}
                          />
                          {t('profile:notifications.push')}
                        </label>
                        <label className="flex items-center gap-3 text-sm">
                          <input
                            type="checkbox"
                            checked={preferences.weeklyDigest}
                            data-testid="pref-weeklyDigest"
                            onChange={(event) => setPreferences((prev) => ({ ...prev, weeklyDigest: event.target.checked }))}
                          />
                          {t('profile:notifications.weekly')}
                        </label>
                      </div>

                      <div
                        className="rounded-xl border px-4 py-3 text-sm"
                        style={{
                          backgroundColor: 'var(--app-layer-base)',
                          borderColor: 'color-mix(in srgb, var(--color-error, #ef4444) 20%, transparent)',
                          color: 'var(--app-text-secondary)',
                        }}
                      >
                        {t('profile:preferences.logoutDescription')}
                      </div>
                    </div>
                  )}

                  {!loading && activeTab === 'skills' && (
                    <div className="space-y-6">
                      {skills.map((skill) => (
                        <div
                          key={skill.name}
                          className="rounded-xl border p-5"
                          style={{
                            backgroundColor: 'var(--app-bg)',
                            borderColor: 'var(--app-border)',
                          }}
                        >
                          <div className="mb-3 flex items-center justify-between">
                            <div className="flex items-center gap-3">
                              <div className={cn('h-3 w-3 rounded-full', skill.color.replace('bg-', 'text-'))} />
                              <span className="font-bold">{skill.name}</span>
                            </div>
                            <span className="text-xs font-bold" style={{ color: 'var(--app-text-secondary)' }}>
                              {skill.current} / {skill.max} {t('platform:analytics.table.xp')}
                            </span>
                          </div>
                          <div className="h-2.5 overflow-hidden rounded-full" style={{ backgroundColor: 'var(--app-layer-surface)' }}>
                            <div className={cn('h-full rounded-full transition-all duration-1000', skill.color)} style={{ width: `${(skill.current / skill.max) * 100}%` }} />
                          </div>
                        </div>
                      ))}
                    </div>
                  )}

                  {!loading && activeTab === 'background' && (
                    <div className="max-w-2xl space-y-3">
                      <div className="space-y-1">
                        <h3 className="text-base font-bold">{t('profile:background.title')}</h3>
                        <p className="text-sm" style={{ color: 'var(--app-text-secondary)' }}>
                          {t('profile:background.description')}
                        </p>
                      </div>
                      <BackgroundPicker showForAllCheckbox={false} />
                    </div>
                  )}

                  {!loading && activeTab !== 'background' && (
                    <div className="flex items-center gap-3 pt-8">
                      <button
                        type="button"
                        data-testid="profile-cancel"
                        onClick={handleCancel}
                        disabled={saving || loggingOut}
                        className="rounded-xl border px-6 py-2.5 text-sm font-bold transition-all hover:opacity-80"
                        style={{
                          borderColor: 'var(--app-border)',
                          color: 'var(--app-text-main)',
                          backgroundColor: 'var(--app-layer-base)',
                        }}
                      >
                        {t('profile:form.cancel')}
                      </button>
                      <button
                        type="button"
                        data-testid="profile-save"
                        onClick={handleSave}
                        disabled={saving || loggingOut}
                        className="flex items-center gap-2 rounded-xl px-6 py-2.5 text-sm font-bold text-white shadow-lg transition-all hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50"
                        style={{ backgroundColor: 'var(--app-accent)' }}
                      >
                        <Save size={16} />
                        {saving ? t('profile:form.saving') : t('profile:form.save')}
                      </button>
                      {activeTab === 'settings' && (
                        <button
                          type="button"
                          data-testid="profile-logout"
                          onClick={handleLogout}
                          disabled={saving || loggingOut}
                          className="ml-auto flex items-center gap-2 rounded-xl border px-6 py-2.5 text-sm font-bold transition-all hover:opacity-80 disabled:opacity-50"
                          style={{
                            borderColor: 'color-mix(in srgb, var(--color-error, #ef4444) 35%, transparent)',
                            backgroundColor: 'color-mix(in srgb, var(--color-error, #ef4444) 8%, transparent)',
                            color: 'var(--color-error, #ef4444)',
                          }}
                        >
                          <LogOut size={16} />
                          {loggingOut ? t('common:actions.loggingOut') : t('common:actions.logout')}
                        </button>
                      )}
                    </div>
                  )}
                </div>
              </div>
      </div>

      <Toast toast={toast} onClose={() => setToast(null)} />
    </main>
  );
};

export default Profile;
