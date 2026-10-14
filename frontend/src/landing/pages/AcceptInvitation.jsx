import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import toast, { Toaster } from 'react-hot-toast';
import { usePreferences } from '../../platform/context/PreferencesContext';
import { useAuth } from '../../shared/context/AuthContext';
import { acceptInvitation, getInvitationByToken } from '../../shared/utils/workspaceApi';
import GlassCard from '../components/GlassCard';
import '../styles/VerifyEmail.css';

const resolveInvitationMessage = (message, t) => {
  const normalizedMessage = String(message || '').trim().toLowerCase();
  if (!normalizedMessage) {
    return t('errors:generic.tryAgain');
  }

  if (normalizedMessage.includes('already a member')) {
    return t('auth:acceptInvitation.alreadyMember', {
      defaultValue: 'Вы уже состоите в этом рабочем пространстве.',
    });
  }

  if (normalizedMessage.includes('already been accepted') || normalizedMessage.includes('invitation is accepted')) {
    return t('auth:acceptInvitation.alreadyAccepted', {
      defaultValue: 'Это приглашение уже было принято.',
    });
  }

  if (normalizedMessage.includes('expired')) {
    return t('auth:acceptInvitation.expired', {
      defaultValue: 'Срок действия приглашения истёк.',
    });
  }

  if (normalizedMessage.includes('revoked')) {
    return t('auth:acceptInvitation.revoked', {
      defaultValue: 'Приглашение было отозвано.',
    });
  }

  return message;
};

const AcceptInvitation = () => {
  const { t } = useTranslation(['auth', 'common', 'errors']);
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') || '';
  const { resolvedTheme } = usePreferences();
  const isDark = resolvedTheme === 'dark';
  const { accessToken, currentUser, isAuthenticated, isInitialized } = useAuth();

  const [loading, setLoading] = useState(true);
  const [accepting, setAccepting] = useState(false);
  const [status, setStatus] = useState('loading');
  const [message, setMessage] = useState('');
  const [invitation, setInvitation] = useState(null);

  const returnTo = useMemo(
    () => (token ? `/accept-invitation?token=${encodeURIComponent(token)}` : '/accept-invitation'),
    [token],
  );

  useEffect(() => {
    let cancelled = false;

    const loadInvitation = async () => {
      if (!isInitialized) {
        return;
      }

      if (!token) {
        setStatus('error');
        setMessage(t('auth:acceptInvitation.missingToken', { defaultValue: 'В ссылке отсутствует токен приглашения.' }));
        setLoading(false);
        return;
      }

      setLoading(true);
      setMessage('');
      try {
        const payload = await getInvitationByToken(isAuthenticated ? accessToken : null, token);
        if (cancelled) return;
        setInvitation(payload);

        if (payload?.alreadyMember) {
          setStatus('accepted');
          setMessage(t('auth:acceptInvitation.alreadyMember', {
            defaultValue: 'Вы уже состоите в этом рабочем пространстве.',
          }));
        } else if (payload?.status === 'Accepted') {
          setStatus('accepted');
          setMessage(t('auth:acceptInvitation.alreadyAccepted', { defaultValue: 'Это приглашение уже было принято.' }));
        } else if (payload?.status === 'Expired') {
          setStatus('error');
          setMessage(t('auth:acceptInvitation.expired', { defaultValue: 'Срок действия приглашения истёк.' }));
        } else if (payload?.status === 'Revoked') {
          setStatus('error');
          setMessage(t('auth:acceptInvitation.revoked', { defaultValue: 'Приглашение было отозвано.' }));
        } else {
          setStatus('ready');
        }
      } catch (error) {
        if (cancelled) return;
        setStatus('error');
        setMessage(resolveInvitationMessage(error.message, t));
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    loadInvitation();
    return () => {
      cancelled = true;
    };
  }, [accessToken, isAuthenticated, isInitialized, t, token]);

  const goToAuth = (mode) => {
    navigate(`/auth?mode=${mode}&returnTo=${encodeURIComponent(returnTo)}`);
  };

  const handleAccept = async () => {
    if (!token) {
      return;
    }

    if (!isAuthenticated) {
      goToAuth('login');
      return;
    }

    setAccepting(true);
    setMessage('');

    try {
      await acceptInvitation(accessToken, token);
      setStatus('accepted');
      setMessage(t('auth:acceptInvitation.success', { defaultValue: 'Приглашение принято. Рабочее пространство уже доступно в панели.' }));
      toast.success(t('auth:acceptInvitation.successTitle', { defaultValue: 'Приглашение принято' }));
    } catch (error) {
      const nextMessage = resolveInvitationMessage(error.message, t) || t('errors:generic.tryAgain');
      const alreadyMember = String(error?.message || '').toLowerCase().includes('already a member');
      setStatus(alreadyMember ? 'accepted' : 'ready');
      setMessage(nextMessage);
      if (alreadyMember) {
        toast.success(nextMessage);
      } else {
        toast.error(nextMessage);
      }
    } finally {
      setAccepting(false);
    }
  };

  return (
    <div className={`verify-email-page ${isDark ? 'dark' : 'light'}`}>
      <Toaster position="top-center" />

      <div className="verify-email-container">
        <GlassCard>
          {(loading || !isInitialized) && (
            <div className="verify-content">
              <div className="verify-spinner">
                <div className="spinner-large" />
              </div>
              <h1 className="verify-title">{t('auth:acceptInvitation.loadingTitle', { defaultValue: 'Загружаем приглашение' })}</h1>
              <p className="verify-description">{t('auth:acceptInvitation.loadingDescription', { defaultValue: 'Проверяем ссылку и подготавливаем приглашение к принятию.' })}</p>
            </div>
          )}

          {!loading && status === 'error' && (
            <div className="verify-content">
              <div className="verify-icon error">
                <svg width="80" height="80" viewBox="0 0 80 80" fill="none">
                  <circle cx="40" cy="40" r="38" stroke="#EF4444" strokeWidth="4" />
                  <path d="M30 30L50 50M50 30L30 50" stroke="#EF4444" strokeWidth="4" strokeLinecap="round" />
                </svg>
              </div>
              <h1 className="verify-title error">{t('auth:acceptInvitation.errorTitle', { defaultValue: 'Не удалось открыть приглашение' })}</h1>
              <p className="verify-description error">{message}</p>
              <div className="verify-actions">
                <button className="primary-btn" onClick={() => navigate('/')}>
                  {t('auth:acceptInvitation.goHome', { defaultValue: 'На главную' })}
                </button>
                <button className="secondary-btn" onClick={() => goToAuth('login')}>
                  {t('auth:acceptInvitation.goToLogin', { defaultValue: 'Войти' })}
                </button>
              </div>
            </div>
          )}

          {!loading && status === 'accepted' && (
            <div className="verify-content">
              <div className="verify-icon success">
                <svg width="80" height="80" viewBox="0 0 80 80" fill="none">
                  <circle cx="40" cy="40" r="38" stroke="#ff6b35" strokeWidth="4" />
                  <path d="M20 40L35 55L60 25" stroke="#ff6b35" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </div>
              <h1 className="verify-title success">{t('auth:acceptInvitation.acceptedTitle', { defaultValue: 'Приглашение готово' })}</h1>
              <p className="verify-description">{message || t('auth:acceptInvitation.alreadyAccepted', { defaultValue: 'Это приглашение уже было принято.' })}</p>
              <button className="primary-btn" onClick={() => navigate('/dashboard')}>
                {t('auth:acceptInvitation.openWorkspace', { defaultValue: 'Открыть панель' })}
              </button>
            </div>
          )}

          {!loading && status === 'ready' && (
            <div className="verify-content">
              <div className="verify-icon success">
                <svg width="80" height="80" viewBox="0 0 80 80" fill="none">
                  <circle cx="40" cy="40" r="38" stroke="#ff6b35" strokeWidth="4" />
                  <path d="M24 28H56V52H24V28Z" stroke="#ff6b35" strokeWidth="4" strokeLinejoin="round" />
                  <path d="M24 34L40 44L56 34" stroke="#ff6b35" strokeWidth="4" strokeLinejoin="round" />
                </svg>
              </div>
              <h1 className="verify-title">{t('auth:acceptInvitation.title', { defaultValue: 'Приглашение в рабочее пространство' })}</h1>
              <p className="verify-description">
                {t('auth:acceptInvitation.description', {
                  defaultValue: 'Проверьте детали приглашения и примите его, чтобы присоединиться к рабочему пространству.',
                })}
              </p>

              <div
                className="w-full rounded-2xl text-left px-4 py-3 flex flex-col gap-2"
                style={{
                  backgroundColor: 'rgba(255,255,255,0.04)',
                  border: '1px solid rgba(255,255,255,0.08)',
                }}
              >
                <div className="text-sm font-semibold">{invitation?.workspaceName || invitation?.workspaceId || 'Workspace'}</div>
                <div className="text-xs opacity-80">
                  {t('auth:acceptInvitation.roleLabel', { defaultValue: 'Роль' })}: {invitation?.role || 'Member'}
                </div>
                <div className="text-xs opacity-80">
                  {t('auth:acceptInvitation.emailLabel', { defaultValue: 'Приглашённый email' })}: {invitation?.email || '—'}
                </div>
                {isAuthenticated && currentUser?.email && (
                  <div className="text-xs opacity-80">
                    {t('auth:acceptInvitation.signedInAs', { defaultValue: 'Вы вошли как' })}: {currentUser.email}
                  </div>
                )}
              </div>

              {message && (
                <p className="verify-description error" style={{ marginTop: '1rem' }}>
                  {message}
                </p>
              )}

              {isAuthenticated ? (
                <div className="verify-actions">
                  <button className="primary-btn" onClick={handleAccept} disabled={accepting || invitation?.canAccept === false}>
                    {accepting
                      ? t('auth:acceptInvitation.accepting', { defaultValue: 'Принятие...' })
                      : t('auth:acceptInvitation.acceptAction', { defaultValue: 'Принять приглашение' })}
                  </button>
                  <button className="secondary-btn" onClick={() => navigate('/dashboard')}>
                    {t('auth:acceptInvitation.notNow', { defaultValue: 'Не сейчас' })}
                  </button>
                </div>
              ) : (
                <div className="verify-actions">
                  <button className="primary-btn" onClick={() => goToAuth('login')}>
                    {t('auth:acceptInvitation.signInAction', { defaultValue: 'Войти и принять' })}
                  </button>
                  <button className="secondary-btn" onClick={() => goToAuth('register')}>
                    {t('auth:acceptInvitation.registerAction', { defaultValue: 'Создать аккаунт' })}
                  </button>
                </div>
              )}
            </div>
          )}
        </GlassCard>
      </div>
    </div>
  );
};

export default AcceptInvitation;
