import { useCallback } from 'react';
import { usePreferences } from '../../platform/context/PreferencesContext';

const useDarkMode = () => {
  const { resolvedTheme, setThemeMode } = usePreferences();
  const isDark = resolvedTheme === 'dark';

  const toggleTheme = useCallback(() => {
    setThemeMode(isDark ? 'light' : 'dark');
  }, [isDark, setThemeMode]);

  return [isDark, toggleTheme];
};

export default useDarkMode;
