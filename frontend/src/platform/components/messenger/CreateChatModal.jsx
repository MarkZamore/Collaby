import React, { useState, useRef, useEffect, useCallback } from 'react';
import { X, Search, MessageSquare, Users, Hash, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { useMessenger } from '../../context/MessengerContext';
import { usePlatform } from '../../context/PlatformContext';
import { useAuth } from '../../../shared/context/AuthContext';
import { resolveUserAvatar } from '../../../shared/utils/avatar';
import { searchUsers } from '../../../shared/utils/usersApi';

const CreateChatModal = ({ onClose }) => {
  const { t } = useTranslation('platform');
  const { createChat } = useMessenger();
  const { currentUser, activeWorkspace } = usePlatform();
  const { accessToken } = useAuth();
  const projects = activeWorkspace?.projects || [];

  const [step, setStep] = useState('type');
  const [query, setQuery] = useState('');
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(false);
  const [channelName, setChannelName] = useState('');
  const inputRef = useRef(null);
  const searchTimeoutRef = useRef(null);

  useEffect(() => {
    const timer = setTimeout(() => inputRef.current?.focus(), 100);
    return () => clearTimeout(timer);
  }, [step]);

  const handleTypeSelect = (type) => {
    setStep(type === 'private' ? 'user' : type === 'project' ? 'project' : 'channel');
  };

  const handleSearchUsers = useCallback((value) => {
    setQuery(value);
    clearTimeout(searchTimeoutRef.current);

    if (!value.trim()) {
      setResults([]);
      return;
    }

    searchTimeoutRef.current = setTimeout(async () => {
      try {
        setLoading(true);
        const data = await searchUsers(accessToken, value);
        setResults((data || []).filter((user) => user.userId !== currentUser?.id));
      } catch {
        setResults([]);
      } finally {
        setLoading(false);
      }
    }, 300);
  }, [accessToken, currentUser?.id]);

  const handleCreatePrivate = async (userId) => {
    try {
      await createChat('private', { participantId: userId });
      onClose();
    } catch (error) {
      console.error('Failed to create private chat', error);
    }
  };

  const handleCreateProject = async (projectId, projectName) => {
    try {
      const memberIds = (activeWorkspace?.members || []).map((member) => member.userId).filter(Boolean);
      await createChat('project', {
        projectId,
        name: projectName,
        workspaceId: activeWorkspace?.id,
        memberIds,
      });
      onClose();
    } catch (error) {
      console.error('Failed to create project chat', error);
    }
  };

  const handleCreateChannel = async () => {
    if (!channelName.trim()) {
      return;
    }

    try {
      const memberIds = (activeWorkspace?.members || []).map((member) => member.userId).filter(Boolean);
      await createChat('channel', {
        name: channelName,
        workspaceId: activeWorkspace?.id,
        memberIds,
      });
      onClose();
    } catch (error) {
      console.error('Failed to create channel', error);
    }
  };

  return (
    <div
      className="fixed inset-0 z-[999] flex items-center justify-center"
      style={{ backgroundColor: 'rgba(0,0,0,0.55)', backdropFilter: 'blur(4px)' }}
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <div
        className="w-full max-w-md rounded-2xl shadow-2xl flex flex-col overflow-hidden"
        style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)' }}
      >
        <div
          className="flex items-center justify-between px-5 py-4"
          style={{ borderBottom: '1px solid var(--app-border)' }}
        >
          <h3 className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>
            {step === 'type' && t('messenger.create.titles.type')}
            {step === 'user' && t('messenger.create.titles.user')}
            {step === 'project' && t('messenger.create.titles.project')}
            {step === 'channel' && t('messenger.create.titles.channel')}
          </h3>
          <button onClick={onClose} className="opacity-50 hover:opacity-100 transition-all">
            <X size={18} style={{ color: 'var(--app-text-main)' }} />
          </button>
        </div>

        <div className="px-5 py-4 flex flex-col gap-3 max-h-[60vh] overflow-y-auto custom-scrollbar">
          {step === 'type' && (
            <div className="space-y-2">
              <TypeOption
                icon={MessageSquare}
                label={t('messenger.create.typePrivateLabel')}
                desc={t('messenger.create.typePrivateDesc')}
                onClick={() => handleTypeSelect('private')}
              />
              <TypeOption
                icon={Users}
                label={t('messenger.create.typeProjectLabel')}
                desc={t('messenger.create.typeProjectDesc')}
                onClick={() => handleTypeSelect('project')}
              />
              <TypeOption
                icon={Hash}
                label={t('messenger.create.typeChannelLabel')}
                desc={t('messenger.create.typeChannelDesc')}
                onClick={() => handleTypeSelect('channel')}
              />
            </div>
          )}

          {step === 'user' && (
            <>
              <div className="relative">
                <Search
                  size={14}
                  className="absolute left-3 top-1/2 -translate-y-1/2 opacity-50"
                  style={{ color: 'var(--app-text-secondary)' }}
                />
                <input
                  ref={inputRef}
                  value={query}
                  onChange={(event) => handleSearchUsers(event.target.value)}
                  placeholder={t('messenger.create.searchUsersPlaceholder')}
                  className="w-full pl-8 pr-3 py-2 rounded-xl text-sm outline-none"
                  style={{
                    backgroundColor: 'var(--app-layer-base)',
                    border: '1px solid var(--app-border)',
                    color: 'var(--app-text-main)',
                  }}
                />
              </div>

              {loading && (
                <div className="text-xs text-center opacity-50" style={{ color: 'var(--app-text-secondary)' }}>
                  {t('messenger.create.searching')}
                </div>
              )}

              {results.map((user) => (
                <button
                  key={user.userId}
                  onClick={() => handleCreatePrivate(user.userId)}
                  className="flex items-center gap-3 w-full px-3 py-2.5 rounded-xl hover:opacity-80 transition-all"
                  style={{ backgroundColor: 'var(--app-layer-base)' }}
                >
                  <img
                    src={resolveUserAvatar({
                      avatarUrl: user.avatarUrl,
                      userId: user.userId,
                      displayName: user.displayName,
                      username: user.username,
                    })}
                    alt=""
                    className="w-9 h-9 rounded-full object-cover"
                  />
                  <div className="text-left flex-1 min-w-0">
                    <div className="text-sm font-medium truncate" style={{ color: 'var(--app-text-main)' }}>
                      {user.displayName || user.username}
                    </div>
                    <div className="text-xs opacity-60 truncate" style={{ color: 'var(--app-text-secondary)' }}>
                      @{user.username}
                    </div>
                  </div>
                </button>
              ))}

              {query && !loading && results.length === 0 && (
                <div className="text-xs text-center py-4 opacity-50" style={{ color: 'var(--app-text-secondary)' }}>
                  {t('messenger.create.noUserFound')}
                </div>
              )}
            </>
          )}

          {step === 'project' && (
            <>
              <p className="text-xs opacity-60" style={{ color: 'var(--app-text-secondary)' }}>
                {t('messenger.create.selectProject')}
              </p>
              {projects.map((project) => (
                <button
                  key={project.id}
                  onClick={() => handleCreateProject(project.id, project.name)}
                  className="flex items-center gap-3 w-full px-3 py-2.5 rounded-xl hover:opacity-80 transition-all"
                  style={{ backgroundColor: 'var(--app-layer-base)' }}
                >
                  <div
                    className="w-9 h-9 rounded-full flex items-center justify-center"
                    style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text, #fff)' }}
                  >
                    <Users size={16} />
                  </div>
                  <span className="text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>
                    {project.name}
                  </span>
                </button>
              ))}
              {projects.length === 0 && (
                <div className="text-xs text-center py-4 opacity-50" style={{ color: 'var(--app-text-secondary)' }}>
                  {t('messenger.create.noProjects')}
                </div>
              )}
            </>
          )}

          {step === 'channel' && (
            <>
              <input
                ref={inputRef}
                value={channelName}
                onChange={(event) => setChannelName(event.target.value)}
                placeholder={t('messenger.create.channelNamePlaceholder')}
                className="w-full px-3 py-2.5 rounded-xl text-sm outline-none"
                style={{
                  backgroundColor: 'var(--app-layer-base)',
                  border: '1px solid var(--app-border)',
                  color: 'var(--app-text-main)',
                }}
                onKeyDown={(event) => event.key === 'Enter' && handleCreateChannel()}
              />
              <button
                onClick={handleCreateChannel}
                disabled={!channelName.trim()}
                className="w-full py-2.5 rounded-xl text-sm font-medium transition-all hover:opacity-90 disabled:opacity-40"
                style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text, #fff)' }}
              >
                {t('messenger.create.createChannel')}
              </button>
            </>
          )}
        </div>

        {step !== 'type' && (
          <div className="px-5 py-3" style={{ borderTop: '1px solid var(--app-border)' }}>
            <button
              onClick={() => {
                setStep('type');
                setQuery('');
                setResults([]);
                setChannelName('');
              }}
              className="text-xs font-medium opacity-60 hover:opacity-100 transition-all"
              style={{ color: 'var(--app-text-secondary)' }}
            >
              ← {t('messenger.create.back')}
            </button>
          </div>
        )}
      </div>
    </div>
  );
};

const TypeOption = ({ icon: Icon, label, desc, onClick }) => (
  <button
    onClick={onClick}
    className="flex items-center gap-3 w-full px-4 py-3 rounded-xl hover:opacity-80 transition-all"
    style={{ backgroundColor: 'var(--app-layer-base)' }}
  >
    <div
      className="w-10 h-10 rounded-full flex items-center justify-center shrink-0"
      style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text, #fff)' }}
    >
      <Icon size={18} />
    </div>
    <div className="text-left flex-1">
      <div className="text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>
        {label}
      </div>
      <div className="text-xs opacity-60" style={{ color: 'var(--app-text-secondary)' }}>
        {desc}
      </div>
    </div>
    <ChevronRight size={16} className="opacity-40" style={{ color: 'var(--app-text-secondary)' }} />
  </button>
);

export default CreateChatModal;
