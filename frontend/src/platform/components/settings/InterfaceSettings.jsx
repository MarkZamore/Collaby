import React from 'react';
import { useTranslation } from 'react-i18next';
import ThemeCustomizer from './ThemeCustomizer';
import SettingCard from './SettingCard';
import SettingRow from './SettingRow';
import { usePreferences } from '../../context/PreferencesContext';

const InterfaceSettings = () => {
  const { t } = useTranslation(['profile', 'common']);
  const { language, setLanguage } = usePreferences();

  const selectStyle = {
    backgroundColor: 'transparent',
    borderColor: 'var(--app-border)',
    color: 'var(--app-text-main)',
  };

  return (
    <div className="animate-in fade-in slide-in-from-bottom-4 space-y-8 duration-500">
      <SettingCard title={t('profile:interface.generalTitle')}>
        <SettingRow
          label={t('profile:interface.homeView')}
          description={t('profile:interface.homeViewDescription')}
        >
          <select className="rounded-md border px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-accent" style={selectStyle}>
            <option style={{ color: 'black' }}>{t('profile:interface.views.dashboard')}</option>
            <option style={{ color: 'black' }}>{t('profile:interface.views.myTasks')}</option>
            <option style={{ color: 'black' }}>{t('profile:interface.views.allIssues')}</option>
          </select>
        </SettingRow>

        <SettingRow
          label={t('profile:interface.displayNames')}
          description={t('profile:interface.displayNamesDescription')}
        >
          <select className="rounded-md border px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-accent" style={selectStyle}>
            <option style={{ color: 'black' }}>{t('profile:interface.names.fullName')}</option>
            <option style={{ color: 'black' }}>{t('profile:interface.names.username')}</option>
          </select>
        </SettingRow>

        <SettingRow
          label={t('profile:interface.fontSize')}
          description={t('profile:interface.fontSizeDescription')}
        >
          <select className="rounded-md border px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-accent" style={selectStyle}>
            <option style={{ color: 'black' }}>{t('profile:interface.fontSizes.default')}</option>
            <option style={{ color: 'black' }}>{t('profile:interface.fontSizes.compact')}</option>
            <option style={{ color: 'black' }}>{t('profile:interface.fontSizes.large')}</option>
          </select>
        </SettingRow>

        <SettingRow
          label={t('profile:preferences.languageLabel')}
          description={t('profile:personal.languageHelp')}
        >
          <select
            value={language}
            onChange={(event) => setLanguage(event.target.value)}
            className="rounded-md border px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-accent"
            style={selectStyle}
          >
            <option value="en" style={{ color: 'black' }}>
              EN
            </option>
            <option value="ru" style={{ color: 'black' }}>
              RU
            </option>
            <option value="kk" style={{ color: 'black' }}>
              KZ
            </option>
          </select>
        </SettingRow>
      </SettingCard>

      <div className="space-y-3">
        <h3 className="pl-1 text-sm font-bold uppercase tracking-wider" style={{ color: 'var(--app-text-main)' }}>
          {t('profile:interface.title')}
        </h3>
        <ThemeCustomizer />
      </div>
    </div>
  );
};

export default InterfaceSettings;
