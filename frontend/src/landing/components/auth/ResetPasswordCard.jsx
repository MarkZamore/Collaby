import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { resetPassword, validateResetPasswordToken } from '../../../shared/utils/authApi';

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

const resolveResetTokenMessage = (error, t) => {
  const rawMessage = String(error?.message || '').trim().toLowerCase();
  if (!rawMessage) {
    return t('auth:reset.invalidTokenDescription');
  }

  if (
    rawMessage.includes('invalid or expired reset token')
    || rawMessage.includes('reset token')
    || rawMessage.includes('token')
  ) {
    return t('auth:reset.invalidTokenDescription');
  }

  return t('auth:reset.generalError');
};

const ResetPasswordCard = ({ onSwitchToLogin }) => {
  const { t } = useTranslation(['auth', 'common']);
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const token = searchParams.get('token');
  const email = searchParams.get('email');
  const [passwords, setPasswords] = useState({ newPassword: '', confirmPassword: '' });
  const [errors, setErrors] = useState({});
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [tokenValidation, setTokenValidation] = useState({
    status: token && email ? 'checking' : 'invalid',
    message: token && email ? '' : t('auth:reset.missingToken'),
  });
  const [focusedInput, setFocusedInput] = useState(null);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const requirements = t('auth:reset.requirementsList', { returnObjects: true });

  useEffect(() => {
    let isMounted = true;

    const checkToken = async () => {
      if (!token || !email) {
        return;
      }

      try {
        await validateResetPasswordToken(token, email);
        if (isMounted) {
          setTokenValidation({ status: 'valid', message: '' });
        }
      } catch (error) {
        if (isMounted) {
          setTokenValidation({
            status: 'invalid',
            message: resolveResetTokenMessage(error, t),
          });
        }
      }
    };

    checkToken();

    return () => {
      isMounted = false;
    };
  }, [email, t, token]);

  const validatePassword = (password) => {
    if (password.length < 8) return t('auth:validation.passwordMin');
    if (!/[A-Z]/.test(password)) return t('auth:validation.passwordUpper');
    if (!/[a-z]/.test(password)) return t('auth:validation.passwordLower');
    if (!/[0-9]/.test(password)) return t('auth:validation.passwordNumber');
    if (!/[!@#$%^&*(),.?\":{}|<>]/.test(password)) return t('auth:validation.passwordSpecial');
    return null;
  };

  const handleInputChange = (event) => {
    const { name, value } = event.target;
    setPasswords((prev) => ({ ...prev, [name]: value }));
    if (errors[name] || errors.general) {
      setErrors((prev) => ({ ...prev, [name]: '', general: '' }));
    }
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setErrors({});

    if (!passwords.newPassword) {
      setErrors({ newPassword: t('auth:validation.passwordRequired') });
      return;
    }

    const passwordError = validatePassword(passwords.newPassword);
    if (passwordError) {
      setErrors({ newPassword: passwordError });
      return;
    }

    if (!passwords.confirmPassword) {
      setErrors({ confirmPassword: t('auth:validation.confirmPasswordRequired') });
      return;
    }

    if (passwords.newPassword !== passwords.confirmPassword) {
      setErrors({ confirmPassword: t('auth:validation.passwordMatch') });
      return;
    }

    if (tokenValidation.status !== 'valid') {
      setErrors({ general: t('auth:reset.missingToken') });
      return;
    }

    setLoading(true);

    try {
      await resetPassword(token, email, passwords.newPassword, passwords.confirmPassword);
      setSuccess(true);
      setTimeout(() => navigate('/auth'), 3000);
    } catch (error) {
      setErrors({ general: resolveResetTokenMessage(error, t) });
    } finally {
      setLoading(false);
    }
  };

  if (success) {
    return (
      <>
        <h3 className="step-title">{t('auth:reset.successTitle')}</h3>

        <div className="auth-success-message">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none">
            <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" />
            <path d="M8 12l2 2 4-4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          <p>{t('auth:reset.successDescription')}</p>
        </div>

        <div className="form-navigation">
          <button type="button" className="submit-btn" onClick={() => navigate('/auth')}>
            <span>{t('auth:login.submit')}</span>
            <span className="btn-arrow">-&gt;</span>
          </button>
        </div>
      </>
    );
  }

  if (tokenValidation.status === 'checking') {
    return (
      <>
        <button type="button" className="back-to-options" onClick={onSwitchToLogin}>
          <svg width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M15 10H5M5 10L10 15M5 10L10 5" />
          </svg>
          {t('auth:common.backToLogin')}
        </button>

        <h3 className="step-title">{t('auth:reset.validatingToken')}</h3>
        <p className="step-description">{t('auth:verify.verifyingHint')}</p>
      </>
    );
  }

  if (tokenValidation.status === 'invalid') {
    return (
      <>
        <button type="button" className="back-to-options" onClick={onSwitchToLogin}>
          <svg width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M15 10H5M5 10L10 15M5 10L10 5" />
          </svg>
          {t('auth:common.backToLogin')}
        </button>

        <h3 className="step-title">{t('auth:reset.invalidTokenTitle')}</h3>
        <div className="error-alert">
          <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
            <circle cx="10" cy="10" r="9" stroke="currentColor" strokeWidth="2" />
            <path d="M10 6v4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            <circle cx="10" cy="15" r="1" fill="currentColor" />
          </svg>
          <span>{tokenValidation.message || t('auth:reset.invalidTokenDescription')}</span>
        </div>

        <p className="step-description">{t('auth:reset.invalidTokenDescription')}</p>

        <div className="form-navigation">
          <button type="button" className="submit-btn" onClick={() => navigate('/auth')}>
            <span>{t('auth:login.submit')}</span>
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

      <h3 className="step-title">{t('auth:reset.title')}</h3>

      {email && (
        <p className="step-description">{t('auth:reset.emailDescription', { email })}</p>
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

      <div className={`input-group ${focusedInput === 'newPassword' || passwords.newPassword ? 'focused' : ''} ${errors.newPassword ? 'error' : ''}`}>
        <input
          type={showPassword ? 'text' : 'password'}
          name="newPassword"
          id="newPassword"
          value={passwords.newPassword}
          onChange={handleInputChange}
          onFocus={() => setFocusedInput('newPassword')}
          onBlur={() => setFocusedInput(null)}
          required
          placeholder=" "
          autoComplete="new-password"
        />
        <label htmlFor="newPassword">
          <span className="label-text">{t('labels.password', { ns: 'common' })}</span>
        </label>
        <button type="button" className="password-toggle" onClick={() => setShowPassword(!showPassword)} tabIndex={-1}>
          <EyeIcon visible={showPassword} />
        </button>
        <div className="input-line" />
        {errors.newPassword && <span className="error-text">{errors.newPassword}</span>}
      </div>

      <div className={`input-group ${focusedInput === 'confirmPassword' || passwords.confirmPassword ? 'focused' : ''} ${errors.confirmPassword ? 'error' : ''}`}>
        <input
          type={showConfirmPassword ? 'text' : 'password'}
          name="confirmPassword"
          id="confirmPassword"
          value={passwords.confirmPassword}
          onChange={handleInputChange}
          onFocus={() => setFocusedInput('confirmPassword')}
          onBlur={() => setFocusedInput(null)}
          required
          placeholder=" "
          autoComplete="new-password"
        />
        <label htmlFor="confirmPassword">
          <span className="label-text">{t('labels.confirmPassword', { ns: 'common' })}</span>
        </label>
        <button type="button" className="password-toggle" onClick={() => setShowConfirmPassword(!showConfirmPassword)} tabIndex={-1}>
          <EyeIcon visible={showConfirmPassword} />
        </button>
        <div className="input-line" />
        {errors.confirmPassword && <span className="error-text">{errors.confirmPassword}</span>}
      </div>

      <div className="auth-info-message">
        <strong>{t('auth:reset.passwordRequirements')}</strong>
        <ul>
          {requirements.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
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
              <span>{t('auth:reset.submit')}</span>
              <span className="btn-arrow">-&gt;</span>
            </>
          )}
        </button>
      </div>
    </>
  );
};

export default ResetPasswordCard;
