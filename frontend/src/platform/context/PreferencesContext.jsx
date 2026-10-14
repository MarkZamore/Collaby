import React, { createContext, useCallback, useContext, useEffect, useLayoutEffect, useMemo, useState } from 'react';
import i18n, { getInitialLanguage, normalizeLanguage, normalizeTheme } from '../../shared/i18n/config';
import { adjustBrightness, adjustSolidColor, getLuminance, hexToRgb } from '../../shared/utils/themeUtils';

export const PreferencesContext = createContext(null);

export const usePreferences = () => useContext(PreferencesContext);

const STORAGE_KEY = 'collaby-ui-preferences';

const createDefaultUiPreferences = () => ({
  language: getInitialLanguage(),
  theme: 'dark',
  accountHydrated: false,
  hasGuestOverride: false,
});

const DEFAULT_UI_PREFERENCES = createDefaultUiPreferences();

const DEFAULT_THEME = {
  mode: 'dark',
  accent: '#5E6AD2',
  background: '#101218',
  sidebarBackground: '#14161C',
  contrast: 50,
  sidebarCustom: false,
};

const THEME_PRESETS = {
  light: {
    background: '#FAFAFA',
    sidebarBackground: '#F5F6F8',
    cardBackground: '#F2F3F5',
    accent: '#5E6AD2',
    textMain: '#1B1F27',
    textSecondary: 'rgba(27, 31, 39, 0.56)',
    border: 'rgba(0, 0, 0, 0.07)',
  },
  dark: {
    background: '#101218',
    sidebarBackground: '#14161C',
    cardBackground: '#181B22',
    accent: '#5E6AD2',
    textMain: '#ECEEF2',
    textSecondary: 'rgba(236, 238, 242, 0.58)',
    border: 'rgba(255, 255, 255, 0.07)',
  },
};

const getSystemTheme = () => {
  if (typeof window === 'undefined') {
    return 'dark';
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
};

const getStoredPreferencesPayload = () => {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    return saved ? JSON.parse(saved) : null;
  } catch (error) {
    console.error('Failed to load stored preferences payload:', error);
    return null;
  }
};

const normalizeVisualMode = (value) => {
  if (value === 'light' || value === 'dark' || value === 'custom') {
    return value;
  }

  return 'dark';
};

const getBackgroundIsDark = (hex) => {
  const rgb = hexToRgb(hex);
  if (!rgb) {
    return true;
  }

  return getLuminance(rgb.r, rgb.g, rgb.b) < 0.5;
};

const getStoredUiPreferences = () => {
  const parsed = getStoredPreferencesPayload();
  if (parsed) {
    const { themeConfig: _themeConfig, ...storedUiPreferences } = parsed;
    return {
      ...DEFAULT_UI_PREFERENCES,
      ...storedUiPreferences,
      language: normalizeLanguage(storedUiPreferences?.language),
      theme: normalizeTheme(storedUiPreferences?.theme),
    };
  }

  return createDefaultUiPreferences();
};

const getStoredTheme = () => {
  const parsed = getStoredPreferencesPayload();
  if (parsed?.themeConfig) {
    return {
      ...DEFAULT_THEME,
      ...parsed.themeConfig,
      mode: normalizeVisualMode(parsed.themeConfig?.mode),
    };
  }

  if (parsed) {
    const normalizedTheme = normalizeTheme(parsed?.theme);
    return {
      ...DEFAULT_THEME,
      mode: normalizedTheme === 'system' ? getSystemTheme() : normalizedTheme,
    };
  }

  return DEFAULT_THEME;
};

const getAccentTextColor = (accent) => {
  const rgb = hexToRgb(accent);
  if (!rgb) {
    return '#FFFFFF';
  }

  return getLuminance(rgb.r, rgb.g, rgb.b) > 0.45 ? '#111111' : '#FFFFFF';
};

export const PreferencesProvider = ({ children }) => {
  const [uiPreferences, setUiPreferences] = useState(getStoredUiPreferences);
  const [theme, setTheme] = useState(getStoredTheme);
  const [systemTheme, setSystemTheme] = useState(getSystemTheme);

  useEffect(() => {
    if (typeof window === 'undefined') {
      return undefined;
    }

    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const handleChange = (event) => {
      setSystemTheme(event.matches ? 'dark' : 'light');
    };

    mediaQuery.addEventListener('change', handleChange);
    return () => mediaQuery.removeEventListener('change', handleChange);
  }, []);

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    localStorage.setItem(STORAGE_KEY, JSON.stringify({
      ...uiPreferences,
      themeConfig: theme,
    }));
    localStorage.removeItem('collaby-theme');
    localStorage.removeItem('collaby-theme-prefs');
  }, [theme, uiPreferences]);

  useEffect(() => {
    const nextLanguage = normalizeLanguage(uiPreferences.language);
    if (i18n.language !== nextLanguage) {
      i18n.changeLanguage(nextLanguage);
    }

    if (typeof document !== 'undefined') {
      document.documentElement.lang = nextLanguage;
    }
  }, [uiPreferences.language]);

  const resolvedTheme = useMemo(() => {
    if (theme.mode === 'custom') {
      return getBackgroundIsDark(theme.background) ? 'dark' : 'light';
    }

    return uiPreferences.theme === 'system' ? systemTheme : normalizeTheme(uiPreferences.theme);
  }, [systemTheme, theme.background, theme.mode, uiPreferences.theme]);

  useLayoutEffect(() => {
    if (typeof document === 'undefined') {
      return;
    }

    const root = document.documentElement;

    let activeBg;
    let activeSidebarBg;
    let activeCardBg;
    let activeAccent;
    let activeOverlayBg;

    const contrastOpacity = Math.max(0.1, Math.min(1, (theme.contrast || 50) / 100));

    if (theme.mode === 'custom') {
      activeBg = theme.background;
      activeSidebarBg = theme.sidebarCustom ? theme.sidebarBackground : theme.background;
      activeAccent = theme.accent;

      const isBgDark = getBackgroundIsDark(activeBg);
      activeCardBg = adjustSolidColor(activeBg, isBgDark ? 0.03 : -0.03);
      activeOverlayBg = adjustSolidColor(activeBg, isBgDark ? 0.08 : 0.05);
    } else {
      const preset = THEME_PRESETS[resolvedTheme] || THEME_PRESETS.dark;
      activeBg = preset.background;
      activeSidebarBg = preset.sidebarBackground;
      activeCardBg = preset.cardBackground;
      activeAccent = theme.accent || preset.accent;
      activeOverlayBg = preset.cardBackground;
    }

    const isBgDark = getBackgroundIsDark(activeBg);
    const textMain =
      theme.mode === 'custom'
        ? (isBgDark ? '#FFFFFF' : '#000000')
        : (THEME_PRESETS[resolvedTheme] || THEME_PRESETS.dark).textMain;
    const textSecondary =
      theme.mode === 'custom'
        ? (isBgDark ? 'rgba(255, 255, 255, 0.7)' : 'rgba(0, 0, 0, 0.6)')
        : (THEME_PRESETS[resolvedTheme] || THEME_PRESETS.dark).textSecondary;
    const borderBase = isBgDark ? '255, 255, 255' : '0, 0, 0';
    const activeBorder =
      theme.mode === 'custom'
        ? `rgba(${borderBase}, ${contrastOpacity})`
        : (THEME_PRESETS[resolvedTheme] || THEME_PRESETS.dark).border;
    const isSidebarDark = getBackgroundIsDark(activeSidebarBg);

    root.classList.remove('light', 'dark', 'dark-theme');
    root.classList.add(resolvedTheme);
    if (resolvedTheme === 'dark') {
      root.classList.add('dark-theme');
    }

    root.style.setProperty('--app-layer-base', activeBg);
    root.style.setProperty('--app-layer-surface', activeSidebarBg);
    root.style.setProperty('--app-layer-overlay', activeOverlayBg);
    root.style.setProperty('--app-bg', activeBg);
    root.style.setProperty('--app-content-bg', activeCardBg);
    root.style.setProperty('--app-card-bg', activeCardBg);
    root.style.setProperty('--app-sidebar-bg', activeSidebarBg);
    root.style.setProperty('--app-accent', activeAccent);
    root.style.setProperty('--app-accent-text', getAccentTextColor(activeAccent));
    root.style.setProperty('--app-border', activeBorder);
    root.style.setProperty('--app-layer-border', activeBorder);
    root.style.setProperty(
      '--app-sidebar-item-active-bg',
      theme.mode === 'custom'
        ? adjustBrightness(activeSidebarBg, isSidebarDark ? 10 : -5)
        : resolvedTheme === 'dark'
          ? '#1E2028'
          : '#E8EBF0'
    );
    root.style.setProperty('--app-sidebar-text', textMain);
    root.style.setProperty('--app-text-main', textMain);
    root.style.setProperty('--app-text-secondary', textSecondary);
  }, [resolvedTheme, theme]);

  const updateUiPreferences = useCallback((patch, markDirty = true) => {
    setUiPreferences((prev) => {
      const nextState = {
        ...prev,
        ...patch,
        hasGuestOverride: markDirty ? true : prev.hasGuestOverride,
      };

      const changed = Object.keys(nextState).some((key) => nextState[key] !== prev[key]);
      return changed ? nextState : prev;
    });
  }, []);

  const setLanguage = useCallback((language, { markDirty = true } = {}) => {
    updateUiPreferences({ language: normalizeLanguage(language) }, markDirty);
  }, [updateUiPreferences]);

  const setThemeMode = useCallback((nextTheme, { markDirty = true } = {}) => {
    const normalizedTheme = normalizeTheme(nextTheme);
    updateUiPreferences({ theme: normalizedTheme }, markDirty);
    setTheme((prev) => ({
      ...prev,
      mode: normalizedTheme === 'system' ? systemTheme : normalizedTheme,
    }));
  }, [systemTheme, updateUiPreferences]);

  const hydrateFromAccount = useCallback(({ language, theme: accountTheme } = {}) => {
    const nextLanguage = normalizeLanguage(language);
    const nextTheme = normalizeTheme(accountTheme);

    setUiPreferences((prev) => ({
      ...prev,
      language: nextLanguage,
      theme: nextTheme,
      accountHydrated: true,
      hasGuestOverride: false,
    }));

    setTheme((prev) => {
      if (prev.mode === 'custom') {
        return prev;
      }

      return {
        ...prev,
        mode: nextTheme === 'system' ? systemTheme : nextTheme,
      };
    });
  }, [systemTheme]);

  const markAccountHydrated = useCallback(() => {
    setUiPreferences((prev) => (prev.accountHydrated ? prev : {
      ...prev,
      accountHydrated: true,
    }));
  }, []);

  const updateTheme = useCallback((key, value) => {
    if (key === 'mode') {
      const normalizedMode = normalizeVisualMode(value);

      if (normalizedMode === 'custom') {
        setTheme((prev) => ({ ...prev, mode: 'custom' }));
        return;
      }

      setThemeMode(normalizedMode);
        return;
    }

    setTheme((prev) => ({ ...prev, [key]: value }));
  }, [setThemeMode]);

  const contextValue = useMemo(() => ({
    language: uiPreferences.language,
    themeMode: uiPreferences.theme,
    resolvedTheme,
    theme,
    uiPreferences,
    setLanguage,
    setThemeMode,
    hydrateFromAccount,
    markAccountHydrated,
    updateTheme,
  }), [
    hydrateFromAccount,
    markAccountHydrated,
    resolvedTheme,
    setLanguage,
    setThemeMode,
    theme,
    uiPreferences,
    updateTheme,
  ]);

  return (
    <PreferencesContext.Provider value={contextValue}>
      {children}
    </PreferencesContext.Provider>
  );
};
