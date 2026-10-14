import React from 'react';
import { Monitor, Moon, Sun } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { usePreferences } from '../../context/PreferencesContext';

const ThemeCustomizer = () => {
  const { t } = useTranslation(['profile', 'common']);
  const { themeMode, setThemeMode } = usePreferences();

  const options = [
    { value: 'light', icon: Sun, label: t('common:theme.light') },
    { value: 'dark', icon: Moon, label: t('common:theme.dark') },
    { value: 'system', icon: Monitor, label: t('common:theme.system') },
  ];

  return (
    <div
      className="overflow-hidden rounded-xl border backdrop-blur-sm"
      style={{
        backgroundColor: 'var(--app-bg)',
        borderColor: 'var(--app-border)',
      }}
    >
      <div
        className="border-b p-4"
        style={{
          borderColor: 'var(--app-border)',
          backgroundColor: 'rgba(var(--app-accent), 0.05)',
        }}
      >
        <h4 className="font-bold" style={{ color: 'var(--app-text-main)' }}>
          {t('profile:interface.themeLabel')}
        </h4>
        <p className="mt-1 text-xs" style={{ color: 'var(--app-text-secondary)' }}>
          {t('profile:interface.themeDescription')}
        </p>
      </div>

      <div className="space-y-3 p-4">
        {options.map((option) => {
          const active = option.value === themeMode;
          const Icon = option.icon;

          return (
            <button
              key={option.value}
              type="button"
              onClick={() => setThemeMode(option.value)}
              className="flex w-full items-center justify-between rounded-lg border px-4 py-3 text-left transition-all hover:opacity-90"
              style={{
                borderColor: active ? 'var(--app-accent)' : 'var(--app-border)',
                backgroundColor: active ? 'rgba(94, 106, 210, 0.12)' : 'transparent',
                color: 'var(--app-text-main)',
              }}
            >
              <span className="flex items-center gap-3">
                <Icon size={18} />
                <span className="text-sm font-medium">{option.label}</span>
              </span>
              {active && (
                <span className="text-xs font-semibold" style={{ color: 'var(--app-accent)' }}>
                  {t('common:status.ready')}
                </span>
              )}
            </button>
          );
        })}
      </div>
    </div>
  );
};

export default ThemeCustomizer;
