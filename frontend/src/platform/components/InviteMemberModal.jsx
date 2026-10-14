import React, { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { X, Search, UserPlus, Check, AlertCircle, ChevronDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { usePlatform } from '../context/PlatformContext';
import { useAuth } from '../../shared/context/AuthContext';
import { resolveUserAvatar } from '../../shared/utils/avatar';
import { searchUsers } from '../../shared/utils/usersApi';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const normalizeIdentity = (value) =>
  String(value || '')
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9а-яё]+/giu, '');

const extractEmailLocalPart = (value) => String(value || '').trim().split('@')[0] || '';

const matchesInviteQuery = (user, normalizedEmailQuery) => {
  if (!normalizedEmailQuery) {
    return false;
  }

  const localPart = extractEmailLocalPart(normalizedEmailQuery);
  const normalizedLocalPart = normalizeIdentity(localPart);

  const userEmail = String(user.email || '').trim().toLowerCase();
  const userMaskedEmail = String(user.emailMasked || '').trim().toLowerCase();
  const candidates = [
    userEmail,
    userMaskedEmail,
    extractEmailLocalPart(userEmail),
    extractEmailLocalPart(userMaskedEmail),
    user.username,
    user.displayName,
  ]
    .map(normalizeIdentity)
    .filter(Boolean);

  return candidates.includes(normalizeIdentity(normalizedEmailQuery)) || candidates.includes(normalizedLocalPart);
};

const InviteMemberModal = ({ isOpen, onClose, workspaceId, workspaceName }) => {
  const { t } = useTranslation(['platform', 'common']);
  const { inviteMember, activeWorkspace, workspaces, currentUser } = usePlatform();
  const { accessToken } = useAuth();

  const roles = [
    { id: 'Member', label: t('platform:team.roles.member.label'), desc: t('platform:team.roles.member.description') },
    { id: 'Admin', label: t('platform:team.roles.admin.label'), desc: t('platform:team.roles.admin.description') },
    { id: 'Viewer', label: t('platform:team.roles.viewer.label'), desc: t('platform:team.roles.viewer.description') },
  ];

  const targetWorkspaceId = workspaceId || activeWorkspace?.id;
  const targetWorkspace = workspaceId
    ? workspaces.find((workspace) => workspace.id === workspaceId)
    : activeWorkspace;
  const existingMemberIds = new Set((targetWorkspace?.members || []).map((member) => member.userId));
  const existingMemberEmails = new Set(
    (targetWorkspace?.members || [])
      .map((member) => String(member.email || '').trim().toLowerCase())
      .filter(Boolean),
  );

  const [query, setQuery] = useState('');
  const [results, setResults] = useState([]);
  const [selected, setSelected] = useState(null);
  const [role, setRole] = useState('Member');
  const [roleOpen, setRoleOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [sending, setSending] = useState(false);
  const [successText, setSuccessText] = useState('');
  const [error, setError] = useState('');
  const [roleDropdownRect, setRoleDropdownRect] = useState(null);

  const inputRef = useRef(null);
  const roleBtnRef = useRef(null);
  const rolePortalRef = useRef(null);
  const searchTimeout = useRef(null);

  useEffect(() => {
    if (!isOpen) return;

    setQuery('');
    setResults([]);
    setSelected(null);
    setRole('Member');
    setError('');
    setSuccessText('');
    setTimeout(() => inputRef.current?.focus(), 80);
  }, [isOpen]);

  useEffect(() => {
    const handler = (event) => {
      const inButton = roleBtnRef.current?.contains(event.target);
      const inPortal = rolePortalRef.current?.contains(event.target);
      if (!inButton && !inPortal) {
        setRoleOpen(false);
        setRoleDropdownRect(null);
      }
    };

    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const handleQueryChange = useCallback(
    (event) => {
      const nextQuery = event.target.value;
      setQuery(nextQuery);
      setSelected(null);
      setError('');

      if (searchTimeout.current) clearTimeout(searchTimeout.current);
      if (nextQuery.trim().length < 2) {
        setResults([]);
        return;
      }

      searchTimeout.current = setTimeout(async () => {
        setLoading(true);
        try {
          const data = await searchUsers(accessToken, nextQuery.trim(), 8);
          const list = Array.isArray(data) ? data : data?.users || [];
          setResults(list.filter((user) => user.userId !== currentUser?.id));
        } catch {
          setResults([]);
        } finally {
          setLoading(false);
        }
      }, 300);
    },
    [accessToken, currentUser?.id],
  );

  const handleSelect = (user) => {
    setSelected(user);
    setQuery(user.displayName || user.username || user.emailMasked || '');
    setResults([]);
    setError('');
  };

  const handleInvite = async () => {
    const normalizedQuery = query.trim();
    const normalizedEmail = normalizedQuery.toLowerCase();
    const canInviteByEmail = !selected && EMAIL_RE.test(normalizedQuery);

    if (!selected && !canInviteByEmail) {
      setError(t('platform:inviteMember.errors.enterEmailOrSelectUser', { defaultValue: 'Выберите пользователя из списка или введите email для приглашения.' }));
      return;
    }

    if (selected && existingMemberIds.has(selected.userId)) {
      setError(t('platform:inviteMember.errors.alreadyMember'));
      return;
    }

    if (!selected && existingMemberEmails.has(normalizedEmail)) {
      setError(t('platform:inviteMember.errors.alreadyMember'));
      return;
    }

    setSending(true);
    setError('');

    try {
      const result = selected
        ? await inviteMember(targetWorkspaceId, selected.userId, role)
        : await inviteMember(targetWorkspaceId, { email: normalizedEmail }, role);
      setSuccessText(
        result?.kind === 'invitation'
          ? t('platform:inviteMember.successInvitation', { defaultValue: 'Приглашение отправлено' })
          : t('platform:inviteMember.success'),
      );
      setTimeout(() => {
        setSuccessText('');
        onClose();
      }, 1400);
    } catch (inviteError) {
      const normalizedMessage = String(inviteError?.message || '').toLowerCase();
      if (normalizedMessage.includes('already') && normalizedMessage.includes('member')) {
        setError(t('platform:inviteMember.errors.alreadyMember'));
      } else if (normalizedMessage.includes('active invitation already exists')) {
        setError(t('platform:inviteMember.errors.alreadyInvited'));
      } else {
        setError(inviteError?.message || t('platform:inviteMember.errors.inviteFailed'));
      }
    } finally {
      setSending(false);
    }
  };

  const handleKeyDown = (event) => {
    if (event.key === 'Escape') onClose();
    if (event.key === 'Enter' && (selected || EMAIL_RE.test(query.trim()))) handleInvite();
  };

  if (!isOpen) return null;

  const selectedRole = roles.find((item) => item.id === role) || roles[0];
  const normalizedQuery = query.trim();
  const canInviteByEmail = !selected && EMAIL_RE.test(normalizedQuery);
  const normalizedQueryLower = normalizedQuery.toLowerCase();
  const matchingCatalogUser = !selected
    ? results.find((user) => {
      return matchesInviteQuery(user, normalizedQueryLower);
    })
    : null;
  const selectedAlreadyMember = Boolean(selected && existingMemberIds.has(selected.userId));
  const inviteByEmailBlocked = canInviteByEmail && existingMemberEmails.has(normalizedQueryLower);
  const inviteBlockedByCatalogMember = canInviteByEmail && Boolean(matchingCatalogUser && existingMemberIds.has(matchingCatalogUser.userId));
  const inviteBlocked = selectedAlreadyMember || inviteByEmailBlocked || inviteBlockedByCatalogMember;

  return (
    <div
      className="fixed inset-0 z-[999] flex items-center justify-center"
      style={{ backgroundColor: 'rgba(0,0,0,0.55)', backdropFilter: 'blur(4px)' }}
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <div
        className="w-full max-w-md rounded-2xl shadow-2xl flex flex-col"
        style={{
          backgroundColor: 'var(--app-layer-surface)',
          border: '1px solid var(--app-border)',
        }}
      >
        <div className="flex items-center justify-between px-5 py-4" style={{ borderBottom: '1px solid var(--app-border)' }}>
          <div>
            <p className="text-[15px] font-bold" style={{ color: 'var(--app-text-main)' }}>
              {t('platform:inviteMember.title')}
            </p>
            <p className="text-xs mt-0.5" style={{ color: 'var(--app-text-secondary)' }}>
              {targetWorkspace?.name || workspaceName || t('platform:inviteMember.workspaceFallback')}
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:opacity-70 transition-opacity"
            style={{ color: 'var(--app-text-secondary)' }}
          >
            <X size={16} />
          </button>
        </div>

        <div className="px-5 py-4 flex flex-col gap-4">
          <div>
            <label className="text-xs font-semibold uppercase tracking-wide mb-2 block" style={{ color: 'var(--app-text-secondary)' }}>
              {t('platform:inviteMember.searchLabel')}
            </label>
            <div className="relative">
              <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: 'var(--app-text-secondary)' }} />
              <input
                ref={inputRef}
                value={query}
                onChange={handleQueryChange}
                onKeyDown={handleKeyDown}
                placeholder={t('platform:inviteMember.searchPlaceholder')}
                className="w-full pl-9 pr-4 py-2 rounded-xl text-sm outline-none transition-all"
                style={{
                  backgroundColor: 'var(--app-layer-base)',
                  border: '1px solid var(--app-border)',
                  color: 'var(--app-text-main)',
                }}
              />
              {loading && (
                <div
                  className="absolute right-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 rounded-full border-2 border-t-transparent animate-spin"
                  style={{ borderColor: 'var(--app-accent)', borderTopColor: 'transparent' }}
                />
              )}
            </div>

            {results.length > 0 && (
              <div
                className="mt-1 rounded-xl overflow-hidden shadow-xl"
                style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)' }}
              >
                {results.map((user) => {
                  const name = user.displayName || user.username || user.email || user.emailMasked || t('platform:inviteMember.userFallback');
                  const isAlready = existingMemberIds.has(user.userId);
                  return (
                    <button
                      key={user.userId}
                      disabled={isAlready}
                      onClick={() => handleSelect(user)}
                      className="w-full flex items-center gap-3 px-3 py-2.5 text-left transition-colors hover:bg-white/5 disabled:opacity-40 disabled:cursor-not-allowed"
                    >
                      <img
                        src={resolveUserAvatar({
                          avatarUrl: user.avatarUrl,
                          userId: user.userId,
                          displayName: user.displayName,
                          username: user.username,
                        })}
                        alt={name}
                        className="w-8 h-8 rounded-full object-cover shrink-0"
                      />
                      <div className="flex-1 min-w-0">
                        <div className="text-sm font-medium truncate" style={{ color: 'var(--app-text-main)' }}>
                          {name}
                        </div>
                        <div className="text-xs truncate" style={{ color: 'var(--app-text-secondary)' }}>
                          {user.username ? `@${user.username}` : (user.email || user.emailMasked)}
                          {isAlready && ` · ${t('platform:inviteMember.alreadyMember')}`}
                        </div>
                      </div>
                      {selected?.userId === user.userId && <Check size={14} style={{ color: 'var(--app-accent)' }} />}
                    </button>
                  );
                })}
              </div>
            )}

            {!selected && normalizedQuery.length >= 2 && (
              <div className="mt-2 text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                {canInviteByEmail
                  ? inviteByEmailBlocked || inviteBlockedByCatalogMember
                    ? t('platform:inviteMember.alreadyMember')
                    : t('platform:inviteMember.emailInviteHint', { defaultValue: 'Пользователь не найден в каталоге — отправим приглашение по email.' })
                  : t('platform:inviteMember.searchHint', { defaultValue: 'Можно выбрать найденного пользователя или ввести email для приглашения.' })}
              </div>
            )}

            {selected && (
              <div
                className="mt-2 flex items-center gap-2 px-3 py-2 rounded-xl"
                style={{
                  backgroundColor: 'color-mix(in srgb, var(--app-accent) 12%, transparent)',
                  border: '1px solid color-mix(in srgb, var(--app-accent) 35%, transparent)',
                }}
              >
                <img
                  src={resolveUserAvatar({
                    avatarUrl: selected.avatarUrl,
                    userId: selected.userId,
                    displayName: selected.displayName,
                    username: selected.username,
                  })}
                  alt=""
                  className="w-6 h-6 rounded-full object-cover shrink-0"
                />
                <span className="text-sm font-medium flex-1 truncate" style={{ color: 'var(--app-text-main)' }}>
                  {selected.displayName || selected.username || selected.email || selected.emailMasked}
                </span>
                <button
                  onClick={() => {
                    setSelected(null);
                    setQuery('');
                  }}
                  className="p-0.5 rounded hover:opacity-70"
                  style={{ color: 'var(--app-text-secondary)' }}
                >
                  <X size={12} />
                </button>
              </div>
            )}
          </div>

          <div>
            <label className="text-xs font-semibold uppercase tracking-wide mb-2 block" style={{ color: 'var(--app-text-secondary)' }}>
              {t('platform:inviteMember.roleLabel')}
            </label>
            <div className="relative">
              <button
                ref={roleBtnRef}
                onClick={() => {
                  if (roleOpen) {
                    setRoleOpen(false);
                    setRoleDropdownRect(null);
                    return;
                  }

                  const rect = roleBtnRef.current?.getBoundingClientRect();
                  setRoleDropdownRect(rect || null);
                  setRoleOpen(true);
                }}
                className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-left transition-all hover:opacity-90"
                style={{
                  backgroundColor: 'var(--app-layer-base)',
                  border: '1px solid var(--app-border)',
                }}
              >
                <div className="flex-1">
                  <div className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>
                    {selectedRole.label}
                  </div>
                  <div className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                    {selectedRole.desc}
                  </div>
                </div>
                <ChevronDown
                  size={14}
                  className={`transition-transform shrink-0 ${roleOpen ? 'rotate-180' : ''}`}
                  style={{ color: 'var(--app-text-secondary)' }}
                />
              </button>

              {roleOpen &&
                roleDropdownRect &&
                createPortal(
                  <div
                    ref={rolePortalRef}
                    className="rounded-xl overflow-hidden shadow-2xl"
                    style={{
                      position: 'fixed',
                      top: roleDropdownRect.bottom + 4,
                      left: roleDropdownRect.left,
                      width: roleDropdownRect.width,
                      zIndex: 9999,
                      backgroundColor: 'var(--app-layer-base)',
                      border: '1px solid var(--app-border)',
                    }}
                  >
                    {roles.map((item) => (
                      <button
                        key={item.id}
                        onClick={() => {
                          setRole(item.id);
                          setRoleOpen(false);
                          setRoleDropdownRect(null);
                        }}
                        className="w-full flex items-center gap-3 px-3 py-2.5 text-left hover:bg-white/5 transition-colors"
                      >
                        <div className="flex-1">
                          <div className="text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>
                            {item.label}
                          </div>
                          <div className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                            {item.desc}
                          </div>
                        </div>
                        {role === item.id && <Check size={13} style={{ color: 'var(--app-accent)' }} />}
                      </button>
                    ))}
                  </div>,
                  document.body,
                )}
            </div>
          </div>

          {error && (
            <div className="flex items-center gap-2 px-3 py-2 rounded-lg text-sm" style={{ backgroundColor: 'rgba(239,68,68,0.1)', color: '#EF4444' }}>
              <AlertCircle size={14} />
              {error}
            </div>
          )}
        </div>

        <div className="px-5 pb-5 flex gap-3">
          <button
            onClick={onClose}
            className="flex-1 py-2.5 rounded-xl text-sm font-medium transition-all hover:opacity-80"
            style={{
              backgroundColor: 'var(--app-layer-base)',
              color: 'var(--app-text-secondary)',
              border: '1px solid var(--app-border)',
            }}
          >
            {t('common:actions.cancel')}
          </button>
          <button
            onClick={handleInvite}
            disabled={((!selected && !canInviteByEmail) || inviteBlocked || sending || Boolean(successText))}
            className="flex-1 py-2.5 rounded-xl text-sm font-bold transition-all flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed hover:opacity-90"
            style={{ backgroundColor: successText ? '#22C55E' : 'var(--app-accent)', color: 'var(--app-accent-text)' }}
          >
            {successText ? (
              <>
                <Check size={15} /> {successText}
              </>
            ) : sending ? (
              <div className="w-4 h-4 rounded-full border-2 border-white/30 border-t-white animate-spin" />
            ) : (
              <>
                <UserPlus size={15} /> {t('platform:team.invite')}
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};

export default InviteMemberModal;
