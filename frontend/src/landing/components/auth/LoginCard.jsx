import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

const EyeIcon = ({ visible }) => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    {visible ? (
      <>
        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
        <circle cx="12" cy="12" r="3" />
      </>
    ) : (
      <>
        <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94" />
        <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19" />
        <path d="M14.12 14.12a3 3 0 1 1-4.24-4.24" />
        <line x1="1" y1="1" x2="23" y2="23" />
      </>
    )}
  </svg>
);

const LoginCard = ({
  formData,
  errors,
  loading,
  focusedInput,
  onInputChange,
  onFocus,
  onBlur,
  onSubmit,
  onSwitchToRegister,
  googleButton,
}) => {
  const { t } = useTranslation(['auth', 'common']);
  const navigate = useNavigate();
  const [showPassword, setShowPassword] = useState(false);

  return (
    <>
      <h3 className="step-title">{t('auth:login.title')}</h3>
      <p className="step-description">{t('auth:login.description')}</p>

      {googleButton && (
        <>
          <div className="social-auth">{googleButton}</div>
          <div className="divider">
            <span>{t('auth:common.or')}</span>
          </div>
        </>
      )}

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

      <div className={`input-group ${focusedInput === 'email' || formData.email ? 'focused' : ''} ${errors.email ? 'error' : ''}`}>
        <input
          type="text"
          name="email"
          id="email"
          data-testid="login-email"
          value={formData.email}
          onChange={onInputChange}
          onFocus={() => onFocus('email')}
          onBlur={onBlur}
          required
          autoComplete="username"
          placeholder=" "
        />
        <label htmlFor="email">
          <span className="label-text">{t('labels.email', { ns: 'common' })}</span>
        </label>
        <div className="input-line" />
        {errors.email && <span className="error-text">{errors.email}</span>}
      </div>

      <div className={`input-group has-toggle ${focusedInput === 'password' || formData.password ? 'focused' : ''} ${errors.password ? 'error' : ''}`}>
        <input
          type={showPassword ? 'text' : 'password'}
          name="password"
          id="password"
          data-testid="login-password"
          value={formData.password}
          onChange={onInputChange}
          onFocus={() => onFocus('password')}
          onBlur={onBlur}
          required
          autoComplete="current-password"
          placeholder=" "
        />
        <label htmlFor="password">
          <span className="label-text">{t('labels.password', { ns: 'common' })}</span>
        </label>
        <button
          type="button"
          className="password-toggle"
          onClick={() => setShowPassword((v) => !v)}
          tabIndex={-1}
          aria-label={showPassword ? 'Hide password' : 'Show password'}
        >
          <EyeIcon visible={showPassword} />
        </button>
        <div className="input-line" />
        {errors.password && <span className="error-text">{errors.password}</span>}
      </div>

      <div className="forgot-password">
        <button
          type="button"
          className="link-btn"
          onClick={() => navigate('/forgot-password')}
        >
          {t('auth:login.forgotPassword')}
        </button>
      </div>

      <div className="form-navigation">
        <button
          type="submit"
          data-testid="login-submit"
          className={`submit-btn ${loading ? 'loading' : ''}`}
          onClick={onSubmit}
          disabled={loading}
        >
          {loading ? (
            <>
              <span className="spinner" />
              <span>{t('status.loading', { ns: 'common' })}</span>
            </>
          ) : (
            <>
              <span>{t('auth:login.submit')}</span>
              <span className="btn-arrow">&rarr;</span>
            </>
          )}
        </button>
      </div>

      <div className="auth-footer">
        <p>
          {t('auth:page.switchToRegister')}{' '}
          <button type="button" className="link-btn" onClick={onSwitchToRegister}>
            {t('auth:register.title')}
          </button>
        </p>
      </div>
    </>
  );
};

export default LoginCard;
