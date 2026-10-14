import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Bot,
  ChevronDown,
  ChevronRight,
  Compass,
  ExternalLink,
  FolderKanban,
  Globe,
  RefreshCw,
  Search,
  Send,
  Sparkles,
} from 'lucide-react';
import DashboardLayout from '../components/DashboardLayout';
import { usePlatform } from '../context/PlatformContext';
import { useAuth } from '../../shared/context/AuthContext';
import { getAiRuntime, searchAssistantKnowledge, sendAssistantChat } from '../../shared/utils/aiApi';

const createSessionId = () => {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
};

/* ── Chat bubble ── */
const ChatBubble = ({ item }) => {
  const isUser = item.role === 'user';

  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'}`}>
      {!isUser && (
        <div
          className="mr-2.5 mt-1 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl"
          style={{ background: 'var(--gradient-orange)', boxShadow: '0 2px 8px rgba(255,107,53,0.18)' }}
        >
          <Bot size={15} className="text-white" />
        </div>
      )}
      <div className={`max-w-[80%] ${isUser ? '' : 'flex-1'}`}>
        <div
          className={`rounded-2xl px-4 py-3 text-sm leading-relaxed ${isUser ? 'rounded-br-md' : 'rounded-bl-md'}`}
          style={{
            backgroundColor: isUser ? 'var(--app-accent)' : 'var(--app-layer-surface)',
            color: isUser ? 'var(--app-accent-text)' : 'var(--app-text-main)',
            border: isUser ? 'none' : '1px solid var(--app-border)',
          }}
        >
          <div className="whitespace-pre-wrap">{item.content}</div>
        </div>
      </div>
    </div>
  );
};

/* ── Collapsible source list ── */
const SourceChip = ({ item }) => (
  <div
    className="flex items-center gap-2 rounded-xl px-3 py-2 text-xs"
    style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)' }}
  >
    <span
      className="rounded-md px-1.5 py-0.5 text-[10px] font-bold uppercase"
      style={{
        backgroundColor: 'color-mix(in srgb, var(--app-accent) 12%, transparent)',
        color: 'var(--app-accent)',
      }}
    >
      {item.kind || 'source'}
    </span>
    <span className="flex-1 truncate font-medium" style={{ color: 'var(--app-text-main)' }}>
      {item.title || item.urlOrRef}
    </span>
    {item.urlOrRef?.startsWith('http') && (
      <a href={item.urlOrRef} target="_blank" rel="noreferrer" className="shrink-0 hover:opacity-70">
        <ExternalLink size={12} style={{ color: 'var(--app-text-secondary)' }} />
      </a>
    )}
  </div>
);

const CollapsibleSources = ({ items, t }) => {
  const [open, setOpen] = useState(false);
  if (!items?.length) return null;

  return (
    <div className="mt-3">
      <button
        type="button"
        onClick={() => setOpen((p) => !p)}
        className="flex items-center gap-1.5 text-xs font-semibold"
        style={{ color: 'var(--app-text-secondary)' }}
      >
        {open ? <ChevronDown size={13} /> : <ChevronRight size={13} />}
        {t('aiAssistant.workspace.sourcesUsed', { count: items.length, defaultValue: `${items.length} sources used` })}
      </button>
      {open && (
        <div className="mt-2 space-y-1.5">
          {items.map((item, i) => (
            <SourceChip key={`${item.urlOrRef || item.title}-${i}`} item={item} />
          ))}
        </div>
      )}
    </div>
  );
};

/* ── Structured answer card ── */
const StructuredAnswer = ({ sections, t }) => {
  if (!sections) return null;

  const cards = [
    {
      key: 'summary',
      icon: Sparkles,
      title: t('aiAssistant.workspace.summaryTitle', { defaultValue: 'Summary' }),
      content: sections.summary,
    },
    {
      key: 'nextSteps',
      icon: Compass,
      title: t('aiAssistant.workspace.nextStepsTitle', { defaultValue: 'Next steps' }),
      items: sections.nextSteps,
    },
    {
      key: 'relatedTasks',
      icon: FolderKanban,
      title: t('aiAssistant.workspace.relatedTasksTitle', { defaultValue: 'Related tasks' }),
      items: sections.relatedTasks,
    },
  ].filter((c) => c.content || c.items?.length);

  if (!cards.length) return null;

  return (
    <div className="mt-4 grid gap-3 sm:grid-cols-3">
      {cards.map((card) => (
        <div
          key={card.key}
          className="rounded-2xl border p-4"
          style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)' }}
        >
          <div className="mb-2 flex items-center gap-2">
            <card.icon size={14} style={{ color: 'var(--app-accent)' }} />
            <span className="text-xs font-bold" style={{ color: 'var(--app-text-main)' }}>{card.title}</span>
          </div>
          {card.content && (
            <p className="text-sm leading-relaxed" style={{ color: 'var(--app-text-secondary)' }}>{card.content}</p>
          )}
          {card.items && (
            <ul className="space-y-1.5 text-sm" style={{ color: 'var(--app-text-secondary)' }}>
              {card.items.map((item, i) => (
                <li key={i} className="flex gap-2">
                  <span className="mt-0.5 shrink-0" style={{ color: 'var(--app-accent)' }}>-</span>
                  <span>{item}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      ))}
    </div>
  );
};

/* ── Suggestion pill ── */
const SuggestionPill = ({ text, onClick, disabled = false }) => (
  <button
    type="button"
    onClick={onClick}
    disabled={disabled}
    className="rounded-xl px-3.5 py-2 text-left text-xs transition-all hover:translate-y-[-1px] disabled:cursor-not-allowed disabled:opacity-60"
    style={{
      backgroundColor: 'var(--app-layer-surface)',
      border: '1px solid var(--app-border)',
      color: 'var(--app-text-main)',
    }}
  >
    {text}
  </button>
);

/* ── Search panel (sidebar) ── */
const SearchPanel = ({ t, accessToken, activeWorkspaceId, activeProjectId, assignedTaskIds, includeExternalSources }) => {
  const [query, setQuery] = useState('');
  const [pending, setPending] = useState(false);
  const [results, setResults] = useState([]);

  const handleSearch = async () => {
    const q = query.trim();
    if (!q || !accessToken || !activeWorkspaceId) return;
    setPending(true);
    try {
      const response = await searchAssistantKnowledge(accessToken, {
        workspaceId: activeWorkspaceId,
        projectId: activeProjectId,
        query: q,
        assignedTaskIds,
        includeExternalSources,
      });
      setResults(response.items || []);
    } catch {
      setResults([]);
    } finally {
      setPending(false);
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex gap-2">
        <div
          className="flex flex-1 items-center gap-2 rounded-xl border px-3 py-2"
          style={{ backgroundColor: 'var(--app-layer-base)', borderColor: 'var(--app-border)' }}
        >
          <Search size={14} style={{ color: 'var(--app-text-secondary)' }} />
          <input
            type="text"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
            placeholder={t('aiAssistant.workspace.searchPlaceholder', { defaultValue: 'Search knowledge base...' })}
            className="flex-1 bg-transparent text-sm outline-none"
            style={{ color: 'var(--app-text-main)' }}
          />
        </div>
        <button
          type="button"
          onClick={handleSearch}
          disabled={pending || !query.trim()}
          className="shrink-0 rounded-xl px-3 py-2 text-xs font-bold disabled:opacity-50"
          style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text)' }}
        >
          {pending ? <RefreshCw size={13} className="animate-spin" /> : t('aiAssistant.workspace.searchAction', { defaultValue: 'Search' })}
        </button>
      </div>

      {results.length > 0 && (
        <div className="space-y-2">
          {results.map((item, i) => (
            <SourceChip key={`${item.urlOrRef || item.title}-${i}`} item={item} />
          ))}
        </div>
      )}

      {results.length === 0 && (
        <p className="py-4 text-center text-xs" style={{ color: 'var(--app-text-secondary)' }}>
          {t('aiAssistant.workspace.searchEmpty', { defaultValue: 'Search results will appear here.' })}
        </p>
      )}
    </div>
  );
};

/* ── Task list (sidebar) ── */
const TaskList = ({ tasks, t }) => {
  if (!tasks.length) {
    return (
      <p className="py-4 text-center text-xs" style={{ color: 'var(--app-text-secondary)' }}>
        {t('aiAssistant.workspace.assignedEmpty', { defaultValue: 'No assigned tasks found.' })}
      </p>
    );
  }

  return (
    <div className="space-y-1.5">
      {tasks.map((task) => (
        <div
          key={task.id}
          className="flex items-center gap-2.5 rounded-xl px-3 py-2.5"
          style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)' }}
        >
          <FolderKanban size={14} style={{ color: 'var(--app-accent)' }} />
          <span className="flex-1 truncate text-sm" style={{ color: 'var(--app-text-main)' }}>
            {task.title}
          </span>
        </div>
      ))}
    </div>
  );
};

/* ── Main page ── */
const WorkspaceAssistantPageContent = () => {
  const { t } = useTranslation('platform');
  const { accessToken, currentUser } = useAuth();
  const { activeWorkspaceId, activeProjectId, activeProject, inboxTasksAll, setActivePage } = usePlatform();
  const sessionIdRef = useRef(createSessionId());
  const chatEndRef = useRef(null);

  const [messages, setMessages] = useState([]);
  const [question, setQuestion] = useState('');
  const [chatPending, setChatPending] = useState(false);
  const [chatSources, setChatSources] = useState([]);
  const [chatSections, setChatSections] = useState(null);
  const [includeExternalSources, setIncludeExternalSources] = useState(false);
  const [activeSideTab, setActiveSideTab] = useState('search'); // search | tasks

  useEffect(() => { getAiRuntime(accessToken).catch(() => {}); }, [accessToken]);

  const projectTasks = useMemo(
    () => Object.values(activeProject?.boardData?.tasks || {}),
    [activeProject],
  );

  const assignedTaskIds = useMemo(() => {
    const ids = new Set();
    (inboxTasksAll || []).forEach((task) => {
      if (!activeProjectId || task.projectId === activeProjectId) ids.add(String(task.id));
    });
    projectTasks.forEach((task) => {
      if (String(task.assigneeId || '') === String(currentUser?.id || '')) ids.add(String(task.id));
    });
    return Array.from(ids);
  }, [activeProjectId, currentUser?.id, inboxTasksAll, projectTasks]);

  const assignedTaskSummaries = useMemo(
    () => projectTasks
      .filter((task) => assignedTaskIds.includes(String(task.id)))
      .slice(0, 8)
      .map((task) => ({ id: task.id, title: task.title || task.content || task.id })),
    [assignedTaskIds, projectTasks],
  );

  useEffect(() => { setActivePage('assistant'); }, [setActivePage]);
  useEffect(() => { chatEndRef.current?.scrollIntoView({ behavior: 'smooth' }); }, [messages]);

  const handleAsk = async (rawQuestion = question) => {
    const candidate = typeof rawQuestion === 'string' ? rawQuestion : question;
    const text = candidate.trim();
    if (!text || !accessToken || !activeWorkspaceId) return;

    const history = messages.map((m) => ({ role: m.role, content: m.content }));
    setMessages((prev) => [...prev, { id: `${Date.now()}-user`, role: 'user', content: text }]);
    setQuestion('');
    setChatPending(true);

    try {
      const response = await sendAssistantChat(accessToken, {
        workspaceId: activeWorkspaceId,
        projectId: activeProjectId,
        question: text,
        assignedTaskIds,
        includeExternalSources,
        history,
        sessionId: sessionIdRef.current,
      });
      setMessages((prev) => [...prev, { id: `${Date.now()}-assistant`, role: 'assistant', content: response.answer }]);
      setChatSources(response.sources || []);
      setChatSections(response.sections || null);
    } catch (error) {
      setMessages((prev) => [...prev, {
        id: `${Date.now()}-error`,
        role: 'assistant',
        content: `Error: ${error.message || 'Failed to get response'}`,
      }]);
      setChatSources([]);
      setChatSections(null);
    } finally {
      setChatPending(false);
    }
  };

  const suggestions = [
    t('aiAssistant.workspace.suggestion1', { defaultValue: 'What should I focus on today?' }),
    t('aiAssistant.workspace.suggestion2', { defaultValue: 'Summarize my current tasks' }),
    t('aiAssistant.workspace.suggestion3', { defaultValue: 'What are the project risks?' }),
  ];

  /* ── No workspace selected ── */
  if (!activeWorkspaceId) {
    return (
      <div className="flex flex-1 items-center justify-center p-8">
        <div className="max-w-md text-center">
          <div
            className="mx-auto mb-5 flex h-16 w-16 items-center justify-center rounded-2xl"
            style={{ background: 'var(--gradient-orange)', boxShadow: '0 6px 20px rgba(255,107,53,0.2)' }}
          >
            <Bot size={28} className="text-white" />
          </div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--app-text-main)' }}>
            {t('aiAssistant.workspace.title', { defaultValue: 'AI Assistant' })}
          </h1>
          <p className="mt-2 text-sm" style={{ color: 'var(--app-text-secondary)' }}>
            {t('aiAssistant.workspace.selectWorkspace', { defaultValue: 'Open a workspace to start asking questions about your tasks and project.' })}
          </p>
        </div>
      </div>
    );
  }

  return (
    <main className="flex flex-1 overflow-hidden">
      {/* ── Chat area (primary) ── */}
      <div className="flex flex-1 flex-col overflow-hidden">
        {/* Header */}
        <div
          className="flex flex-col gap-3 border-b px-4 py-3 sm:flex-row sm:items-center sm:gap-4 sm:px-6 sm:py-4"
          style={{ borderBottomColor: 'var(--app-border)', backgroundColor: 'var(--app-layer-surface)' }}
        >
          <div
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl"
            style={{ background: 'var(--gradient-orange)', boxShadow: '0 3px 10px rgba(255,107,53,0.2)' }}
          >
            <Bot size={18} className="text-white" />
          </div>
          <div className="flex-1 min-w-0">
            <h1 className="text-lg font-bold" style={{ color: 'var(--app-text-main)' }}>
              {t('aiAssistant.workspace.title', { defaultValue: 'AI Assistant' })}
            </h1>
            <p className="truncate text-xs" style={{ color: 'var(--app-text-secondary)' }}>
              {activeProject?.name || t('aiAssistant.workspace.allProjects', { defaultValue: 'All projects' })}
              {assignedTaskIds.length > 0 && ` \u00b7 ${assignedTaskIds.length} ${t('aiAssistant.workspace.tasksAssigned', { defaultValue: 'tasks assigned' })}`}
            </p>
          </div>
          <label
            className="flex items-center gap-2 rounded-xl border px-3 py-2 text-xs font-medium cursor-pointer select-none"
            style={{ borderColor: 'var(--app-border)', color: 'var(--app-text-secondary)' }}
          >
            <Globe size={13} style={{ color: includeExternalSources ? 'var(--app-accent)' : undefined }} />
            <input
              type="checkbox"
              checked={includeExternalSources}
              onChange={(e) => setIncludeExternalSources(e.target.checked)}
              className="sr-only"
            />
            <span style={{ color: includeExternalSources ? 'var(--app-accent)' : undefined }}>
              {t('aiAssistant.workspace.externalToggle', { defaultValue: 'External sources' })}
            </span>
            <div
              className="h-4 w-7 rounded-full transition-colors"
              style={{ backgroundColor: includeExternalSources ? 'var(--app-accent)' : 'var(--app-layer-base)' }}
            >
              <div
                className="h-4 w-4 rounded-full bg-white shadow-sm transition-transform"
                style={{ transform: includeExternalSources ? 'translateX(12px)' : 'translateX(0)' }}
              />
            </div>
          </label>
        </div>

        {/* Messages */}
        <div className="custom-scrollbar flex-1 overflow-y-auto px-6 py-5" style={{ minHeight: 0 }}>
          {messages.length === 0 ? (
            <div className="flex h-full flex-col items-center justify-center gap-5">
              <div
                className="flex h-20 w-20 items-center justify-center rounded-3xl"
                style={{
                  background: 'color-mix(in srgb, var(--app-accent) 10%, transparent)',
                  border: '1px solid color-mix(in srgb, var(--app-accent) 15%, transparent)',
                }}
              >
                <Sparkles size={32} style={{ color: 'var(--app-accent)' }} />
              </div>
              <div className="text-center">
                <p className="text-base font-bold" style={{ color: 'var(--app-text-main)' }}>
                  {t('aiAssistant.workspace.emptyTitle', { defaultValue: 'Ask me anything about your project' })}
                </p>
                <p className="mt-1 max-w-sm text-sm" style={{ color: 'var(--app-text-secondary)' }}>
                  {t('aiAssistant.workspace.emptyDescription', { defaultValue: 'I can help with tasks, project context, next steps, and more.' })}
                </p>
              </div>
              <div className="flex flex-wrap justify-center gap-2">
                {suggestions.map((s, i) => (
                  <SuggestionPill
                    key={i}
                    text={s}
                    disabled={chatPending}
                    onClick={() => handleAsk(s)}
                  />
                ))}
              </div>
            </div>
          ) : (
            <div className="mx-auto max-w-3xl space-y-4">
              {messages.map((m) => (
                <ChatBubble key={m.id} item={m} />
              ))}
              {chatPending && (
                <div className="flex justify-start">
                  <div className="ml-[42px] flex items-center gap-2 rounded-2xl rounded-bl-md border px-4 py-3"
                    style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)' }}>
                    <RefreshCw size={13} className="animate-spin" style={{ color: 'var(--app-accent)' }} />
                    <span className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                      {t('aiAssistant.workspace.thinking', { defaultValue: 'Thinking...' })}
                    </span>
                  </div>
                </div>
              )}
              <div ref={chatEndRef} />
            </div>
          )}

          {/* Structured answer + sources below chat */}
          {messages.length > 0 && (
            <div className="mx-auto max-w-3xl">
              <StructuredAnswer sections={chatSections} t={t} />
              <CollapsibleSources items={chatSources} t={t} />
            </div>
          )}
        </div>

        {/* Input */}
        <div className="shrink-0 border-t px-6 py-4" style={{ borderTopColor: 'var(--app-border)' }}>
          <div
            className="mx-auto flex max-w-3xl items-end gap-3 rounded-2xl border px-4 py-3"
            style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)' }}
          >
            <textarea
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleAsk(); } }}
              onInput={(e) => { e.target.style.height = 'auto'; e.target.style.height = `${Math.min(e.target.scrollHeight, 140)}px`; }}
              placeholder={t('aiAssistant.workspace.chatPlaceholder', { defaultValue: 'Ask a question about your project...' })}
              className="flex-1 resize-none bg-transparent text-sm outline-none placeholder-current"
              rows={1}
              style={{ minHeight: 24, maxHeight: 140, color: 'var(--app-text-main)' }}
            />
            <button
              type="button"
              onClick={() => handleAsk()}
              disabled={chatPending || !question.trim()}
              className="shrink-0 rounded-xl p-2.5 transition-all disabled:opacity-30"
              style={{
                background: question.trim() ? 'var(--gradient-orange)' : 'transparent',
                boxShadow: question.trim() ? '0 2px 8px rgba(255,107,53,0.2)' : 'none',
              }}
            >
              <Send size={16} className="text-white" />
            </button>
          </div>
        </div>
      </div>

      {/* ── Right sidebar ── */}
      <div
        className="hidden w-[320px] shrink-0 flex-col overflow-hidden border-l 2xl:flex"
        style={{ borderLeftColor: 'var(--app-border)', backgroundColor: 'var(--app-layer-surface)' }}
      >
        {/* Tabs */}
        <div className="flex border-b" style={{ borderBottomColor: 'var(--app-border)' }}>
          {[
            { id: 'search', icon: Search, label: t('aiAssistant.workspace.tabSearch', { defaultValue: 'Search' }) },
            { id: 'tasks', icon: FolderKanban, label: t('aiAssistant.workspace.tabTasks', { defaultValue: 'My tasks' }) },
          ].map((tab) => (
            <button
              key={tab.id}
              type="button"
              onClick={() => setActiveSideTab(tab.id)}
              className="flex flex-1 items-center justify-center gap-2 py-3 text-xs font-bold transition-colors"
              style={{
                color: activeSideTab === tab.id ? 'var(--app-accent)' : 'var(--app-text-secondary)',
                borderBottom: activeSideTab === tab.id ? '2px solid var(--app-accent)' : '2px solid transparent',
              }}
            >
              <tab.icon size={13} />
              {tab.label}
            </button>
          ))}
        </div>

        {/* Tab content */}
        <div className="custom-scrollbar flex-1 overflow-y-auto p-4">
          {activeSideTab === 'search' ? (
            <SearchPanel
              t={t}
              accessToken={accessToken}
              activeWorkspaceId={activeWorkspaceId}
              activeProjectId={activeProjectId}
              assignedTaskIds={assignedTaskIds}
              includeExternalSources={includeExternalSources}
            />
          ) : (
            <TaskList tasks={assignedTaskSummaries} t={t} />
          )}
        </div>
      </div>
    </main>
  );
};

const WorkspaceAssistantPage = () => (
  <DashboardLayout>
    <WorkspaceAssistantPageContent />
  </DashboardLayout>
);

export default WorkspaceAssistantPage;
