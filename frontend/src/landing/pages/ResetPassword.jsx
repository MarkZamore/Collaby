import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ResetPasswordCard } from '../components/auth';
import { usePreferences } from '../../platform/context/PreferencesContext';
import '../styles/Auth.css';

const ResetPassword = () => {
  const { t } = useTranslation(['auth', 'common']);
  const { resolvedTheme } = usePreferences();
  const isDark = resolvedTheme === 'dark';
  const navigate = useNavigate();

  const handleSwitchToLogin = () => {
    navigate('/auth');
  };

  return (
    <div className={`auth-page ${isDark ? 'dark-theme' : ''}`}>
      {/* Back button */}
      <button className="auth-back-btn" onClick={() => navigate('/')}>
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
          <path d="M19 12H5M5 12L12 19M5 12L12 5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        <span>{t('actions.back', { ns: 'common' })}</span>
      </button>

      <div className="auth-layout">
        {/* Brand panel — left side */}
        <div className="auth-brand">
          <div className="auth-brand-content">
            <div className="auth-brand-logo">&#9670; Collaby</div>
            <h2 className="auth-brand-title">{t('auth:page.brandTitle', { defaultValue: t('auth:page.loginFeature1') })}</h2>
            <p className="auth-brand-subtitle">{t('auth:page.brandSubtitle', { defaultValue: t('auth:page.loginFeature2') })}</p>
            <div className="auth-brand-features">
              <div className="auth-brand-feature">
                <span className="auth-brand-feature-icon">&#10003;</span>
                <span>{t('auth:page.loginFeature1')}</span>
              </div>
              <div className="auth-brand-feature">
                <span className="auth-brand-feature-icon">&#10003;</span>
                <span>{t('auth:page.loginFeature2')}</span>
              </div>
              <div className="auth-brand-feature">
                <span className="auth-brand-feature-icon">&#10003;</span>
                <span>{t('auth:page.loginFeature3')}</span>
              </div>
            </div>
          </div>
        </div>

        {/* Form panel — right side */}
        <div className="auth-form-panel">
          <div className="auth-card">
            <form onSubmit={(e) => e.preventDefault()} className="auth-form">
              <ResetPasswordCard onSwitchToLogin={handleSwitchToLogin} />
            </form>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ResetPassword;
