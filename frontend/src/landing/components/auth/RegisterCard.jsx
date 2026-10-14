import React, { useState } from 'react';
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

const StepIndicator = ({ currentStep, totalSteps }) => {
  if (totalSteps <= 1) return null;

  return (
    <div className="step-indicator">
      {Array.from({ length: totalSteps }, (_, i) => {
        const step = i + 1;
        const isActive = step === currentStep;
        const isCompleted = step < currentStep;
        return (
          <React.Fragment key={step}>
            <div className={`step-dot ${isActive ? 'active' : ''} ${isCompleted ? 'completed' : ''}`}>
              {isCompleted ? '\u2713' : step}
            </div>
            {step < totalSteps && (
              <div className={`step-line ${isCompleted ? 'completed' : ''}`} />
            )}
          </React.Fragment>
        );
      })}
    </div>
  );
};

const RegisterCard = ({
  formData,
  errors,
  loading,
  focusedInput,
  currentStep,
  totalSteps,
  stepDirection,
  nicknameAvailable,
  nicknameChecking,
  onInputChange,
  onFocus,
  onBlur,
  onSubmit,
  onNextStep,
  onPrevStep,
  onSwitchToLogin,
  googleButton,
}) => {
  const { t } = useTranslation(['auth', 'common']);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const stepContent = currentStep === 1 ? 'nickname' : currentStep === 2 ? 'email' : 'password';
  const requirements = t('auth:reset.requirementsList', { returnObjects: true });

  return (
    <>
      <div className="progress-container">
        <StepIndicator currentStep={currentStep} totalSteps={totalSteps} />
      </div>

      <div className={`form-step ${stepDirection === 'forward' ? 'slide-left' : 'slide-right'}`}>
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

        {stepContent === 'nickname' && (
          <>
            <h3 className="step-title">{t('auth:register.nicknameTitle')}</h3>
            <p className="step-description">{t('auth:register.nicknameDescription')}</p>

            <div className={`input-group ${focusedInput === 'nickname' || formData.nickname ? 'focused' : ''} ${errors.nickname ? 'error' : ''}`}>
              <input
                type="text"
                name="nickname"
                id="nickname"
                value={formData.nickname}
                onChange={onInputChange}
                onFocus={() => onFocus('nickname')}
                onBlur={onBlur}
                autoFocus
                required
                placeholder=" "
              />
              <label htmlFor="nickname">
                <span className="label-text">{t('auth:register.nickname')}</span>
              </label>
              <div className="input-line" />
              {errors.nickname && <span className="error-text">{errors.nickname}</span>}
              {nicknameChecking && formData.nickname.length >= 3 && (
                <span className="helper-text">&#9203; {t('auth:register.nicknameChecking')}</span>
              )}
              {!errors.nickname && nicknameAvailable && formData.nickname && (
                <span className="helper-text">&#10003; {t('auth:register.nicknameAvailable')}</span>
              )}
            </div>
          </>
        )}

        {stepContent === 'email' && (
          <>
            <h3 className="step-title">{t('auth:register.emailTitle')}</h3>
            <p className="step-description">{t('auth:register.emailDescription')}</p>

            <div className={`input-group ${focusedInput === 'email' || formData.email ? 'focused' : ''} ${errors.email ? 'error' : ''}`}>
              <input
                type="email"
                name="email"
                id="email"
                value={formData.email}
                onChange={onInputChange}
                onFocus={() => onFocus('email')}
                onBlur={onBlur}
                autoFocus
                required
                placeholder=" "
              />
              <label htmlFor="email">
                <span className="label-text">{t('labels.email', { ns: 'common' })}</span>
              </label>
              <div className="input-line" />
              {errors.email && <span className="error-text">{errors.email}</span>}
            </div>
          </>
        )}

        {stepContent === 'password' && (
          <>
            <h3 className="step-title">{t('auth:register.passwordTitle')}</h3>
            <p className="step-description">{t('auth:register.passwordDescription')}</p>

            <div className={`input-group has-toggle ${focusedInput === 'password' || formData.password ? 'focused' : ''} ${errors.password ? 'error' : ''}`}>
              <input
                type={showPassword ? 'text' : 'password'}
                name="password"
                id="password"
                value={formData.password}
                onChange={onInputChange}
                onFocus={() => onFocus('password')}
                onBlur={onBlur}
                autoFocus
                required
                autoComplete="new-password"
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

              {formData.password && (
                <div className="password-requirements">
                  <div className={`requirement ${formData.password.length >= 8 ? 'met' : ''}`}>
                    {formData.password.length >= 8 ? '\u2713' : '\u25CB'} {requirements[0]}
                  </div>
                  <div className={`requirement ${/[A-Z]/.test(formData.password) ? 'met' : ''}`}>
                    {/[A-Z]/.test(formData.password) ? '\u2713' : '\u25CB'} {t('auth:validation.passwordUpper')}
                  </div>
                  <div className={`requirement ${/[a-z]/.test(formData.password) ? 'met' : ''}`}>
                    {/[a-z]/.test(formData.password) ? '\u2713' : '\u25CB'} {t('auth:validation.passwordLower')}
                  </div>
                  <div className={`requirement ${/[0-9]/.test(formData.password) ? 'met' : ''}`}>
                    {/[0-9]/.test(formData.password) ? '\u2713' : '\u25CB'} {t('auth:validation.passwordNumber')}
                  </div>
                  <div className={`requirement ${/[!@#$%^&*(),.?\":{}|<>]/.test(formData.password) ? 'met' : ''}`}>
                    {/[!@#$%^&*(),.?\":{}|<>]/.test(formData.password) ? '\u2713' : '\u25CB'} {t('auth:validation.passwordSpecial')}
                  </div>
                </div>
              )}

              {!errors.password && formData.password && formData.password.length >= 8 && (
                <div className="password-strength">
                  <div className="strength-bar">
                    <div
                      className="strength-fill"
                      style={{
                        width: `${Math.min((formData.password.length / 12) * 100, 100)}%`,
                        backgroundColor: formData.password.length < 10 ? '#f7931e' : '#10b981',
                      }}
                    />
                  </div>
                  <span className="strength-text">
                    {formData.password.length < 10
                      ? t('auth:register.passwordStrengthMedium')
                      : t('auth:register.passwordStrengthStrong')}
                  </span>
                </div>
              )}
            </div>

            <div className={`input-group has-toggle ${focusedInput === 'confirmPassword' || formData.confirmPassword ? 'focused' : ''} ${errors.confirmPassword ? 'error' : ''}`}>
              <input
                type={showConfirmPassword ? 'text' : 'password'}
                name="confirmPassword"
                id="confirmPassword"
                value={formData.confirmPassword}
                onChange={onInputChange}
                onFocus={() => onFocus('confirmPassword')}
                onBlur={onBlur}
                required
                autoComplete="new-password"
                placeholder=" "
              />
              <label htmlFor="confirmPassword">
                <span className="label-text">{t('labels.confirmPassword', { ns: 'common' })}</span>
              </label>
              <button
                type="button"
                className="password-toggle"
                onClick={() => setShowConfirmPassword((v) => !v)}
                tabIndex={-1}
                aria-label={showConfirmPassword ? 'Hide password' : 'Show password'}
              >
                <EyeIcon visible={showConfirmPassword} />
              </button>
              <div className="input-line" />
              {errors.confirmPassword && <span className="error-text">{errors.confirmPassword}</span>}
              {!errors.confirmPassword && formData.confirmPassword && formData.password === formData.confirmPassword && (
                <span className="helper-text">&#10003; {t('auth:validation.passwordMatch')}</span>
              )}
            </div>
          </>
        )}
      </div>

      <div className="form-navigation">
        {currentStep > 1 && (
          <button type="button" className="nav-btn prev-btn" onClick={onPrevStep}>
            <span className="btn-arrow">&larr;</span>
            <span>{t('actions.back', { ns: 'common' })}</span>
          </button>
        )}

        <button
          type="submit"
          className={`submit-btn next-btn ${loading ? 'loading' : ''}`}
          onClick={currentStep < totalSteps ? onNextStep : onSubmit}
          disabled={loading}
        >
          {loading ? (
            <>
              <span className="spinner" />
              <span>{t('status.loading', { ns: 'common' })}</span>
            </>
          ) : (
            <>
              <span>{currentStep < totalSteps ? t('auth:register.next') : t('auth:register.submit')}</span>
              <span className="btn-arrow">&rarr;</span>
            </>
          )}
        </button>
      </div>

      {currentStep === 1 && (
        <>
          {googleButton && (
            <>
              <div className="divider">
                <span>{t('auth:common.or')}</span>
              </div>
              <div className="social-auth">{googleButton}</div>
            </>
          )}

          <div className="auth-footer">
            <p>
              {t('auth:page.switchToLogin')}{' '}
              <button type="button" className="link-btn" onClick={onSwitchToLogin}>
                {t('auth:login.title')}
              </button>
            </p>
          </div>
        </>
      )}
    </>
  );
};

export default RegisterCard;
