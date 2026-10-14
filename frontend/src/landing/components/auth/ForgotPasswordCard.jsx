import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { forgotPassword } from '../../../shared/utils/authApi';

const ForgotPasswordCard = ({ onSwitchToLogin }) => {
  const { t } = useTranslation(['auth', 'common']);
  const [email, setEmail] = useState('');
  const [errors, setErrors] = useState({});
  const [loading, setLoading] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [focusedInput, setFocusedInput] = useState(null);

  const handleInputChange = (event) => {
    setEmail(event.target.value);
    if (errors.email || errors.general) {
      setErrors({});
    }
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setErrors({});

    if (!email.trim()) {
      setErrors({ email: t('auth:validation.emailRequired') });
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      setErrors({ email: t('auth:validation.emailInvalid') });
      return;
    }

    setLoading(true);

    try {
      await forgotPassword(email);
      setSubmitted(true);
    } catch (error) {
      setErrors({ general: error.message || t('auth:forgot.generalError') });
    } finally {
      setLoading(false);
    }
  };

  if (submitted) {
    return (
      <>
        <h3 className="step-title">{t('auth:forgot.successTitle')}</h3>

        <div className="auth-success-message">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none">
            <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" />
            <path d="M8 12l2 2 4-4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          <p>{t('auth:forgot.successDescription', { email })}</p>
        </div>

        <div className="auth-info-message">
          {t('auth:forgot.spamHint')}
        </div>

        <div className="form-navigation">
          <button type="button" className="submit-btn" onClick={onSwitchToLogin}>
            <span>{t('auth:common.backToLogin')}</span>
            <span className="btn-arrow">-&gt;</span>
          </button>
        </div>
      </>
    );
  }

  return (
    <>
      <button type="button" className="back-to-options" onClick={onSwitchToLogin}>
        <svg width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M15 10H5M5 10L10 15M5 10L10 5" />
        </svg>
        {t('auth:common.backToLogin')}
      </button>

      <h3 className="step-title">{t('auth:forgot.title')}</h3>
      <p className="step-description">{t('auth:forgot.description')}</p>

      {errors.general && (
        <div className="error-alert">
          <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
            <circle cx="10" cy="10" r="9" stroke="currentColor" strokeWidth="2" />
            <path d="M10 6v4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            <circle cx="10" cy="15" r="1" fill="currentColor" />
          </svg>
          <span>{errors.general}</span>
        </div>
      )}

      <div className={`input-group ${focusedInput === 'email' || email ? 'focused' : ''} ${errors.email ? 'error' : ''}`}>
        <input
          type="email"
          name="email"
          id="forgot-email"
          value={email}
          onChange={handleInputChange}
          onFocus={() => setFocusedInput('email')}
          onBlur={() => setFocusedInput(null)}
          required
          placeholder=" "
        />
        <label htmlFor="forgot-email">
          <span className="label-text">{t('labels.email', { ns: 'common' })}</span>
        </label>
        <div className="input-line" />
        {errors.email && <span className="error-text">{errors.email}</span>}
      </div>

      <div className="form-navigation">
        <button
          type="submit"
          className={`submit-btn ${loading ? 'loading' : ''}`}
          onClick={handleSubmit}
          disabled={loading}
        >
          {loading ? (
            <>
              <span className="spinner" />
              <span>{t('status.loading', { ns: 'common' })}</span>
            </>
          ) : (
            <>
              <span>{t('auth:forgot.submit')}</span>
              <span className="btn-arrow">-&gt;</span>
            </>
          )}
        </button>
      </div>
    </>
  );
};

export default ForgotPasswordCard;
