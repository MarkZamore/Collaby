import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import toast, { Toaster } from 'react-hot-toast';
import { usePreferences } from '../../platform/context/PreferencesContext';
import { useAuth } from '../../shared/context/AuthContext';
import AppLoadingScreen from '../../shared/components/AppLoadingScreen';
import CloudflareAccessRedirect from '../../shared/components/CloudflareAccessRedirect';
import { isDedicatedAdminHost } from '../../shared/utils/panelHosts';
import { checkNickname, getGoogleAuthConfig } from '../../shared/utils/authApi';
import { loadGoogleIdentityScript } from '../../shared/utils/googleIdentity';
import { LoginCard, RegisterCard } from '../components/auth';
import GoogleIdentityButton from '../components/auth/GoogleIdentityButton';
import '../styles/Auth.css';

const REGISTRATION_STEPS = 3;

const emptyFormData = {
  nickname: '',
  email: '',
  password: '',
  confirmPassword: '',
};

const Auth = () => {
  const { t } = useTranslation(['auth', 'common', 'errors']);
  const { resolvedTheme, language } = usePreferences();
  const isDark = resolvedTheme === 'dark';
  const location = useLocation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const {
    login: authLogin,
    register: authRegister,
    authenticateWithGoogle,
    finishGoogleRegistration,
    currentUser,
    isAuthenticated,
    isInitialized,
    loading: authLoading,
  } = useAuth();
  const dedicatedAdminHost = isDedicatedAdminHost();
  const defaultReturnTo = dedicatedAdminHost ? '/admin' : '/dashboard';

  const initialMode = searchParams.get('mode') || 'login';
  const getSafeReturnTo = useCallback(() => {
    const candidates = [searchParams.get('returnTo'), location.state?.returnTo];
    const match = candidates.find((value) => typeof value === 'string' && value.startsWith('/') && !value.startsWith('//'));
    return match || defaultReturnTo;
  }, [defaultReturnTo, location.state, searchParams]);

  const [mode, setMode] = useState(initialMode);
  const [isTransitioning, setIsTransitioning] = useState(false);
  const [currentStep, setCurrentStep] = useState(1);
  const [stepDirection, setStepDirection] = useState('forward');
  const [formData, setFormData] = useState(emptyFormData);
  const [errors, setErrors] = useState({});
  const [loading, setLoading] = useState(false);
  const [focusedInput, setFocusedInput] = useState(null);
  const [nicknameChecking, setNicknameChecking] = useState(false);
  const [nicknameAvailable, setNicknameAvailable] = useState(null);
  const [googleConfig, setGoogleConfig] = useState({ enabled: false, clientId: null });
  const [googleReady, setGoogleReady] = useState(false);
  const [googleRegistrationPending, setGoogleRegistrationPending] = useState(null);

  const formRef = useRef(null);
  const nicknameDebounceRef = useRef(null);
  const isGoogleNicknameStep = mode === 'register' && !!googleRegistrationPending;
  const totalRegistrationSteps = isGoogleNicknameStep ? 1 : REGISTRATION_STEPS;

  useEffect(() => {
    setMode(initialMode);
  }, [initialMode]);

  useEffect(() => {
    if (!dedicatedAdminHost || !isInitialized || authLoading || !isAuthenticated) return;
    navigate(currentUser?.isSystemAdmin ? getSafeReturnTo() : '/dashboard', { replace: true });
  }, [authLoading, currentUser?.isSystemAdmin, dedicatedAdminHost, getSafeReturnTo, isAuthenticated, isInitialized, navigate]);

  useEffect(() => {
    if (dedicatedAdminHost) {
      setGoogleConfig({ enabled: false, clientId: null });
      setGoogleReady(false);
      return undefined;
    }

    let cancelled = false;

    const bootstrapGoogle = async () => {
      try {
        const config = await getGoogleAuthConfig();
        if (cancelled) return;
        if (!config?.enabled || !config?.clientId) {
          setGoogleConfig({ enabled: false, clientId: null });
          setGoogleReady(false);
          return;
        }
        await loadGoogleIdentityScript();
        if (cancelled) return;
        setGoogleConfig(config);
        setGoogleReady(true);
      } catch {
        if (!cancelled) {
          setGoogleConfig({ enabled: false, clientId: null });
          setGoogleReady(false);
        }
      }
    };

    bootstrapGoogle();
    return () => { cancelled = true; };
  }, [dedicatedAdminHost]);

  useEffect(() => {
    if (mode !== 'register' || currentStep !== 1) {
      setNicknameChecking(false);
      return undefined;
    }

    const nickname = formData.nickname.trim();
    if (nickname.length < 3 || !/^[a-zA-Z0-9_]+$/.test(nickname)) {
      setNicknameAvailable(null);
      return undefined;
    }

    clearTimeout(nicknameDebounceRef.current);
    nicknameDebounceRef.current = setTimeout(async () => {
      setNicknameChecking(true);
      try {
        const result = await checkNickname(nickname);
        setNicknameAvailable(result.available);
        setErrors((prev) => {
          if (result.available) {
            const { nickname: _nickname, ...rest } = prev;
            return rest;
          }
          return { ...prev, nickname: t('auth:validation.nicknameTaken') };
        });
      } catch (error) {
        toast.error(error.message || t('errors:generic.tryAgain'));
      } finally {
        setNicknameChecking(false);
      }
    }, 500);

    return () => clearTimeout(nicknameDebounceRef.current);
  }, [currentStep, formData.nickname, mode, t]);

  const getPasswordValidationError = (password) => {
    if (!password) return t('auth:validation.passwordRequired');
    if (password.length < 8) return t('auth:validation.passwordMin');
    if (!/[A-Z]/.test(password)) return t('auth:validation.passwordUpper');
    if (!/[a-z]/.test(password)) return t('auth:validation.passwordLower');
    if (!/[0-9]/.test(password)) return t('auth:validation.passwordNumber');
    if (!/[!@#$%^&*(),.?\":{}|<>]/.test(password)) return t('auth:validation.passwordSpecial');
    return null;
  };

  const updateModeQuery = useCallback((nextMode) => {
    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.set('mode', nextMode);
    navigate(`/auth?${nextSearchParams.toString()}`, { replace: true });
  }, [navigate, searchParams]);

  const handleModeChange = (nextMode) => {
    if (nextMode === mode || isTransitioning) return;
    setIsTransitioning(true);
    updateModeQuery(nextMode);

    setTimeout(() => {
      setMode(nextMode);
      setCurrentStep(1);
      setFormData(emptyFormData);
      setErrors({});
      setNicknameAvailable(null);
      setGoogleRegistrationPending(null);
      setIsTransitioning(false);
    }, 200);
  };

  const validateNicknameStep = () => {
    const nextErrors = {};
    const nickname = formData.nickname.trim();
    if (nickname.length < 3) nextErrors.nickname = t('auth:validation.nicknameMin');
    else if (!/^[a-zA-Z0-9_]+$/.test(nickname)) nextErrors.nickname = t('auth:validation.nicknameFormat');
    else if (nicknameAvailable === false) nextErrors.nickname = t('auth:validation.nicknameTaken');
    setErrors((prev) => ({ ...prev, ...nextErrors }));
    return Object.keys(nextErrors).length === 0;
  };

  const validateCurrentStep = () => {
    const nextErrors = {};
    if (currentStep === 1) return validateNicknameStep();
    if (currentStep === 2) {
      if (!formData.email) nextErrors.email = t('auth:validation.emailRequired');
      else if (!/\S+@\S+\.\S+/.test(formData.email)) nextErrors.email = t('auth:validation.emailInvalid');
    }
    if (currentStep === 3) {
      const passwordError = getPasswordValidationError(formData.password);
      if (passwordError) nextErrors.password = passwordError;
      if (!formData.confirmPassword) nextErrors.confirmPassword = t('auth:validation.confirmPasswordRequired');
      else if (formData.password !== formData.confirmPassword) nextErrors.confirmPassword = t('auth:validation.passwordMatch');
    }
    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const validateForm = () => {
    if (isGoogleNicknameStep) return validateNicknameStep();
    const nextErrors = {};
    if (!formData.email) nextErrors.email = t('auth:validation.emailRequired');
    else if (!/\S+@\S+\.\S+/.test(formData.email)) nextErrors.email = t('auth:validation.emailInvalid');
    const passwordError = getPasswordValidationError(formData.password);
    if (passwordError) nextErrors.password = passwordError;
    if (mode === 'register') {
      if (!formData.confirmPassword) nextErrors.confirmPassword = t('auth:validation.confirmPasswordRequired');
      else if (formData.password !== formData.confirmPassword) nextErrors.confirmPassword = t('auth:validation.passwordMatch');
    }
    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const goToNextStep = () => {
    if (!validateCurrentStep()) return;
    setStepDirection('forward');
    setCurrentStep((prev) => prev + 1);
  };

  const goToPrevStep = () => {
    setStepDirection('backward');
    setCurrentStep((prev) => prev - 1);
  };

  const handleInputChange = (event) => {
    const { name, value } = event.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    setErrors((prev) => ({ ...prev, [name]: '', general: '' }));
    if (name === 'nickname') setNicknameAvailable(null);
    if (name === 'password' && mode === 'register' && !isGoogleNicknameStep) {
      const passwordError = getPasswordValidationError(value);
      setErrors((prev) => ({
        ...prev,
        password: value ? passwordError || '' : '',
        confirmPassword: formData.confirmPassword && value !== formData.confirmPassword ? t('auth:validation.passwordMatch') : '',
      }));
    }
    if (name === 'confirmPassword') {
      setErrors((prev) => ({
        ...prev,
        confirmPassword: value && value !== formData.password ? t('auth:validation.passwordMatch') : '',
      }));
    }
  };

  const handleGoogleCredential = useCallback(async (credential) => {
    setLoading(true);
    setErrors({});
    try {
      const response = await authenticateWithGoogle(credential);
      if (response?.requiresNickname) {
        setMode('register');
        setCurrentStep(1);
        setStepDirection('forward');
        setGoogleRegistrationPending({ registrationToken: response.registrationToken, profile: response.profile });
        setFormData({ ...emptyFormData, nickname: response.suggestedNickname || '', email: response.profile?.email || '' });
        setNicknameAvailable(null);
        updateModeQuery('register');
        return;
      }
      toast.success(t('auth:login.success'));
      navigate(getSafeReturnTo(), { replace: true });
    } catch (error) {
      const errorMessage = error.message || t('errors:auth.loginFailed');
      toast.error(errorMessage);
      setErrors({ general: errorMessage });
    } finally {
      setLoading(false);
    }
  }, [authenticateWithGoogle, getSafeReturnTo, navigate, t, updateModeQuery]);

  const handleSubmit = async (event, forceSubmit = false) => {
    if (event?.preventDefault) event.preventDefault();
    if (mode === 'register' && !isGoogleNicknameStep && !forceSubmit && currentStep < REGISTRATION_STEPS) {
      goToNextStep();
      return;
    }
    if (!validateForm()) return;
    setLoading(true);
    try {
      if (mode === 'login') {
        await authLogin(formData.email, formData.password);
        toast.success(t('auth:login.success'));
        navigate(getSafeReturnTo(), { replace: true });
        return;
      }
      if (isGoogleNicknameStep) {
        await finishGoogleRegistration(googleRegistrationPending.registrationToken, formData.nickname.trim());
        setGoogleRegistrationPending(null);
        toast.success(t('auth:login.success'));
        navigate(getSafeReturnTo(), { replace: true });
        return;
      }
      await authRegister({
        nickname: formData.nickname,
        email: formData.email,
        password: formData.password,
        confirmPassword: formData.confirmPassword,
        enableTwoFactor: false,
        language,
      });
      sessionStorage.setItem('pendingVerificationEmail', formData.email);
      toast.success(t('auth:register.success'));
      navigate(`/verify-email-sent?email=${encodeURIComponent(formData.email)}`);
    } catch (error) {
      let errorMessage = error.message;
      if (error.status === 429) errorMessage = t('errors:generic.rateLimited');
      else if (error.status === 400) errorMessage = error.message || t('errors:generic.invalidInput');
      else if (error.status === 401) errorMessage = t('errors:auth.invalidCredentials');
      else if (error.message?.includes('Email not verified')) errorMessage = t('errors:auth.emailNotVerified');
      if (isGoogleNicknameStep && error.code === 'nickname_taken') {
        setNicknameAvailable(false);
        setErrors({ nickname: t('auth:validation.nicknameTaken'), general: errorMessage });
      } else {
        setErrors({ general: errorMessage });
      }
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyPress = (event) => {
    if (event.key !== 'Enter' || mode !== 'register') return;
    event.preventDefault();
    if (!isGoogleNicknameStep && currentStep < REGISTRATION_STEPS) {
      goToNextStep();
      return;
    }
    handleSubmit(event, true);
  };

  const googleButton =
    googleReady && googleConfig.enabled && googleConfig.clientId ? (
      <GoogleIdentityButton clientId={googleConfig.clientId} dark={isDark} onCredential={handleGoogleCredential} />
    ) : (
      <button type="button" className="google-fallback-btn" disabled>
        <svg width="18" height="18" viewBox="0 0 24 24">
          <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 0 1-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4" />
          <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853" />
          <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18A11.96 11.96 0 0 0 1 12c0 1.94.46 3.77 1.18 5.07l3.66-2.84v-.14z" fill="#FBBC05" />
          <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335" />
        </svg>
        <span>{t('auth:common.continueWithGoogle', { defaultValue: 'Continue with Google' })}</span>
      </button>
    );

  if (dedicatedAdminHost) {
    if (!isInitialized || authLoading || isAuthenticated) {
      return <AppLoadingScreen label="Loading admin panel..." />;
    }
    return <CloudflareAccessRedirect label="Redirecting to Cloudflare Access..." />;
  }

  return (
    <div className={`auth-page ${isDark ? 'dark-theme' : ''}`}>
      <Toaster
        position="top-right"
        toastOptions={{
          duration: 4000,
          style: {
            background: isDark ? '#1e293b' : '#fff',
            color: isDark ? '#e2e8f0' : '#1e293b',
            border: `1px solid ${isDark ? '#334155' : '#e2e8f0'}`,
          },
        }}
      />

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
          <div className="auth-card" ref={formRef}>
            <form onSubmit={handleSubmit} className="auth-form" onKeyPress={handleKeyPress}>
              {mode === 'login' ? (
                <LoginCard
                  formData={formData}
                  errors={errors}
                  loading={loading}
                  focusedInput={focusedInput}
                  onInputChange={handleInputChange}
                  onFocus={setFocusedInput}
                  onBlur={() => setFocusedInput(null)}
                  onSubmit={handleSubmit}
                  onSwitchToRegister={() => handleModeChange('register')}
                  googleButton={googleButton}
                />
              ) : (
                <RegisterCard
                  formData={formData}
                  errors={errors}
                  loading={loading}
                  focusedInput={focusedInput}
                  currentStep={currentStep}
                  totalSteps={totalRegistrationSteps}
                  stepDirection={stepDirection}
                  nicknameAvailable={nicknameAvailable}
                  nicknameChecking={nicknameChecking}
                  onInputChange={handleInputChange}
                  onFocus={setFocusedInput}
                  onBlur={() => setFocusedInput(null)}
                  onSubmit={handleSubmit}
                  onNextStep={goToNextStep}
                  onPrevStep={goToPrevStep}
                  onSwitchToLogin={() => handleModeChange('login')}
                  googleButton={isGoogleNicknameStep ? null : googleButton}
                />
              )}
            </form>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Auth;
