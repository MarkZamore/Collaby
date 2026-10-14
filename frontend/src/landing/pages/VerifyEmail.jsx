import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import toast, { Toaster } from 'react-hot-toast';
import { usePreferences } from '../../platform/context/PreferencesContext';
import { verifyEmail } from '../../shared/utils/authApi';
import '../styles/Auth.css';
import '../styles/AuthStatus.css';

const SuccessIcon = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" aria-hidden="true">
    <path d="M20 7L9 18L4 13" stroke="currentColor" strokeWidth="2.25" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

const ErrorIcon = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" aria-hidden="true">
    <path d="M12 8V12" stroke="currentColor" strokeWidth="2.25" strokeLinecap="round" />
    <path d="M12 16H12.01" stroke="currentColor" strokeWidth="2.25" strokeLinecap="round" />
    <path d="M10.29 3.86L1.82 18A2 2 0 0 0 3.53 21H20.47A2 2 0 0 0 22.18 18L13.71 3.86A2 2 0 0 0 10.29 3.86Z" stroke="currentColor" strokeWidth="2.25" strokeLinejoin="round" />
  </svg>
);

const resolveVerificationErrorMessage = (error, t) => {
  const rawMessage = String(error?.message || '').trim().toLowerCase();
  if (!rawMessage) {
    return t('auth:verify.errorDescription');
  }

  if (
    rawMessage.includes('invalid verification token')
    || rawMessage.includes('expired verification token')
    || rawMessage.includes('verification token')
  ) {
    return t('auth:verify.invalidTokenDescription');
  }

  return t('auth:verify.errorDescription');
};

const VerifyEmail = () => {
  const { t } = useTranslation(['auth', 'common', 'errors']);
  const navigate = useNavigate();
  const { resolvedTheme } = usePreferences();
  const isDark = resolvedTheme === 'dark';
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const [status, setStatus] = useState('verifying');
  const [message, setMessage] = useState('');

  useEffect(() => {
    const runVerification = async () => {
      if (!token) {
        const missingTokenMessage = t('auth:verify.missingToken');
        setStatus('error');
        setMessage(missingTokenMessage);
        toast.error(missingTokenMessage);
        return;
      }

      try {
        await verifyEmail(token);
        setStatus('success');
        setMessage(t('auth:verify.successDescription'));
        toast.success(t('auth:verify.successTitle'));
        setTimeout(() => navigate('/auth?verified=true'), 10000);
      } catch (error) {
        const nextMessage = resolveVerificationErrorMessage(error, t);
        setStatus('error');
        setMessage(nextMessage);
        toast.error(nextMessage);
      }
    };

    runVerification();
  }, [navigate, t, token]);

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
            {status === 'verifying' && (
              <div className="auth-status-content">
                <div className="auth-status-pill auth-status-pill-neutral">{t('auth:verify.title')}</div>
                <div className="auth-status-icon auth-status-icon-neutral" aria-hidden="true">
                  <div className="auth-status-spinner" />
                </div>
                <h1 className="auth-status-title">{t('auth:verify.verifying')}</h1>
                <p className="auth-status-description">{t('auth:verify.verifyingHint')}</p>
              </div>
            )}

            {status === 'success' && (
              <div className="auth-status-content">
                <div className="auth-status-pill auth-status-pill-success">{t('auth:verify.successTitle')}</div>
                <div className="auth-status-icon auth-status-icon-success">
                  <SuccessIcon />
                </div>
                <h1 className="auth-status-title">{t('auth:verify.successTitle')}</h1>
                <p className="auth-status-description">{message}</p>
                <p className="auth-status-caption">{t('auth:verify.successRedirect')}</p>
                <div className="auth-status-actions">
                  <button className="submit-btn" type="button" onClick={() => navigate('/auth')}>
                    <span>{t('auth:verify.goToLogin')}</span>
                    <span className="btn-arrow">&rarr;</span>
                  </button>
                </div>
              </div>
            )}

            {status === 'error' && (
              <div className="auth-status-content">
                <div className="auth-status-pill auth-status-pill-error">{t('auth:verify.errorTitle')}</div>
                <div className="auth-status-icon auth-status-icon-error">
                  <ErrorIcon />
                </div>
                <h1 className="auth-status-title">{t('auth:verify.errorTitle')}</h1>
                <p className="auth-status-description">{message}</p>
                <div className="auth-status-actions auth-status-actions-split">
                  <button className="submit-btn" type="button" onClick={() => navigate('/auth')}>
                    <span>{t('auth:verify.goToLogin')}</span>
                    <span className="btn-arrow">&rarr;</span>
                  </button>
                  <button className="nav-btn" type="button" onClick={() => navigate('/verify-email-sent')}>
                    <span>{t('auth:verify.requestNewEmail')}</span>
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default VerifyEmail;
