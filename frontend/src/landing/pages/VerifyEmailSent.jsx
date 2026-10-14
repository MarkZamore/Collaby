import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import toast, { Toaster } from 'react-hot-toast';
import { usePreferences } from '../../platform/context/PreferencesContext';
import { resendVerificationEmail } from '../../shared/utils/authApi';
import '../styles/Auth.css';
import '../styles/AuthStatus.css';

const MailIcon = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" aria-hidden="true">
    <path d="M4 6H20C21.1046 6 22 6.89543 22 8V16C22 17.1046 21.1046 18 20 18H4C2.89543 18 2 17.1046 2 16V8C2 6.89543 2.89543 6 4 6Z" stroke="currentColor" strokeWidth="2.25" strokeLinejoin="round" />
    <path d="M22 8L13.03 13.7C12.3887 14.1078 11.6113 14.1078 10.97 13.7L2 8" stroke="currentColor" strokeWidth="2.25" strokeLinejoin="round" />
  </svg>
);

const VerifyEmailSent = () => {
  const { t } = useTranslation(['auth', 'common', 'errors']);
  const { resolvedTheme } = usePreferences();
  const isDark = resolvedTheme === 'dark';
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const queryEmail = searchParams.get('email') || '';
  const storedEmail = sessionStorage.getItem('pendingVerificationEmail') || '';
  const email = useMemo(() => queryEmail || storedEmail, [queryEmail, storedEmail]);
  const [loading, setLoading] = useState(false);
  const [cooldown, setCooldown] = useState(0);

  useEffect(() => {
    if (email && storedEmail !== email) {
      sessionStorage.setItem('pendingVerificationEmail', email);
    }
  }, [email, storedEmail]);

  useEffect(() => {
    if (cooldown <= 0) {
      return undefined;
    }

    const timer = setTimeout(() => setCooldown((value) => value - 1), 1000);
    return () => clearTimeout(timer);
  }, [cooldown]);

  const handleResend = async () => {
    if (!email || loading || cooldown > 0) {
      return;
    }

    setLoading(true);

    try {
      const result = await resendVerificationEmail(email);
      if (result.success) {
        toast.success(t('auth:verify.sentTitle'));
        setCooldown(60);
        return;
      }

      toast.error(result.message || t('errors:generic.tryAgain'));
    } catch (error) {
      toast.error(error.message || t('errors:generic.tryAgain'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={`auth-page auth-status-page ${isDark ? 'dark-theme' : ''}`}>
      <Toaster
        position="top-right"
        toastOptions={{
          duration: 4000,
          style: {
            background: isDark ? '#14161c' : '#fff',
            color: isDark ? '#eceef2' : '#1b1f27',
            border: `1px solid ${isDark ? '#334155' : '#e2e8f0'}`,
          },
        }}
      />

      <button className="auth-back-btn" onClick={() => navigate('/')}>
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
          <path d="M19 12H5M5 12L12 19M5 12L12 5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        <span>{t('actions.back', { ns: 'common' })}</span>
      </button>

      <div className="auth-layout">
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

        <div className="auth-form-panel">
          <div className="auth-card auth-status-card">
            <div className="auth-status-content">
              <div className="auth-status-pill auth-status-pill-success">{t('auth:verify.sentTitle')}</div>
              <div className="auth-status-icon auth-status-icon-success">
                <MailIcon />
              </div>
              <h1 className="auth-status-title">{t('auth:verify.sentTitle')}</h1>
              <p className="auth-status-description">{t('auth:verify.sentDescription')}</p>

              {email && (
                <div className="auth-status-email">
                  <span className="auth-status-email-label">{t('auth:verify.emailLabel')}</span>
                  <strong>{email}</strong>
                </div>
              )}

              <div className="auth-status-note">
                <strong>{t('auth:verify.nextStepTitle')}</strong>
                <p>{t('auth:forgot.spamHint')}</p>
              </div>

              <div className="auth-status-actions auth-status-actions-split">
                {email ? (
                  <button className="submit-btn" type="button" onClick={handleResend} disabled={loading || cooldown > 0}>
                    {loading ? (
                      <>
                        <span className="spinner" />
                        <span>{t('common:status.loading')}</span>
                      </>
                    ) : (
                      <>
                        <span>
                          {cooldown > 0
                            ? `${t('auth:verify.requestNewEmail')} (${cooldown}s)`
                            : t('auth:verify.requestNewEmail')}
                        </span>
                        <span className="btn-arrow">&rarr;</span>
                      </>
                    )}
                  </button>
                ) : (
                  <button className="submit-btn" type="button" onClick={() => navigate('/auth?mode=register')}>
                    <span>{t('auth:verify.requestNewEmail')}</span>
                    <span className="btn-arrow">&rarr;</span>
                  </button>
                )}
                <button className="nav-btn" type="button" onClick={() => navigate('/auth')}>
                  <span>{t('auth:verify.goToLogin')}</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default VerifyEmailSent;
