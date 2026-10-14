import { useCallback, useEffect, useState } from 'react';
import { usePreferences } from '../../platform/context/PreferencesContext';

export const usePublicPageState = () => {
  const { resolvedTheme, setThemeMode } = usePreferences();
  const [isScrolled, setIsScrolled] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 50);
    };

    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  const isDark = resolvedTheme === 'dark';

  const toggleTheme = useCallback(() => {
    setThemeMode(isDark ? 'light' : 'dark');
  }, [isDark, setThemeMode]);

  return {
    isDark,
    isScrolled,
    toggleTheme,
  };
};
