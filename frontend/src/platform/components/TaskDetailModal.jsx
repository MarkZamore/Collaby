import React, { useState, useEffect, useRef } from 'react';
import {
  X, 
  Calendar, 
  User, 
  Paperclip, 
  MessageSquare, 
  CheckSquare,
  MoreHorizontal,
  Clock,
  Link as LinkIcon,
  Trash2,
  Send,
  Pencil,
  Plus,
  Tag
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../shared/utils/cn';
import { formatDate, formatDateTime, formatTime } from '../../shared/utils/locale';
import { usePlatform } from '../context/PlatformContext';
import { usePreferences } from '../context/PreferencesContext';
import {
  getDefaultRawStatus,
  getStatusDisplayName,
  isDoneStatus,
  isInProgressStatus,
} from '../utils/statuses';

const TaskDetailModal = ({ task, onClose, readOnly }) => {
  const { t } = useTranslation('platform');
  const { language } = usePreferences();
  const {
    updateTask, deleteTask, moveTask, users, availableTags, currentUser, activeProject,
    addComment, deleteComment, updateComment,
    addSubTask, updateSubTask, deleteSubTask,
    assignLabel, removeLabel, createLabel, deleteLabel,
  } = usePlatform();

  const projectColumns = activeProject?.boardData?.columns ? Object.values(activeProject.boardData.columns) : [];
  const [title, setTitle] = useState(task?.title || task?.content || '');
  const [description, setDescription] = useState(task?.description || '');
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [newComment, setNewComment] = useState('');
  const [newSubtaskTitle, setNewSubtaskTitle] = useState('');
  const [editingCommentId, setEditingCommentId] = useState(null);
  const [editingCommentText, setEditingCommentText] = useState('');
  const [showNewLabel, setShowNewLabel] = useState(false);
  const [newLabelName, setNewLabelName] = useState('');
  const [newLabelColor, setNewLabelColor] = useState('#6366F1');
  const [showActionsMenu, setShowActionsMenu] = useState(false);
  const commentsEndRef = useRef(null);

  const PlusIcon = ({ className }) => (
    <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
    </svg>
  );

  // Небольшой inline-компонент заголовка подзадачи с локальным редактированием и сохранением по blur.
  const SubtaskTitleInput = ({ subtask, isDone, onSave }) => {
    const [val, setVal] = useState(subtask.title);
    useEffect(() => { setVal(subtask.title); }, [subtask.title]);
    return (
      <input
        type="text"
        value={val}
        onChange={(e) => setVal(e.target.value)}
        onBlur={() => { if (val !== subtask.title) onSave(val); }}
        onKeyDown={(e) => { if (e.key === 'Enter') e.target.blur(); }}
        placeholder={t('taskDetail.subtaskPlaceholder')}
        className={cn(
          "flex-1 bg-transparent border-none p-0 text-sm focus:ring-0 placeholder:text-slate-400 outline-none",
          isDone ? "text-slate-400 line-through" : "text-slate-700 dark:text-slate-200"
        )}
      />
    );
  };

  useEffect(() => {
    if (task) {
      setTitle(task.title || task.content);
      setDescription(task.description || '');
      setShowActionsMenu(false);
    }
  }, [task]);

  // После добавления нового комментария прокручиваем список вниз.
  useEffect(() => {
    if (commentsEndRef.current) {
      commentsEndRef.current.scrollIntoView({ behavior: 'smooth' });
    }
  }, [task?.comments]);

  const handleUpdate = (field, value) => {
    if (readOnly) return;
    if (field === 'status') {
      // При смене статуса используем moveTask, чтобы карточка сразу попала в правильную колонку.
      const targetCol = projectColumns.find(c => (c.rawTitle || c.title) === value || c.title === value);
      if (targetCol && task.statusId !== targetCol.id) {
        moveTask(task.id, targetCol.id);
      }
      return;
    }
    updateTask(task.id, { [field]: value });
  };

  const handleAddComment = () => {
    if (readOnly) return;
    if (!newComment.trim()) return;
    addComment(task.id, newComment);
    setNewComment('');
  };

  const handleKeyDown = (e) => {
    if (e.ctrlKey && e.key === 'Enter') {
      handleAddComment();
    }
  };

  const getUserById = (id) => {
    if (id === currentUser?.id) return currentUser;
    return users.find(u => u.id === id) || { name: t('sidebar.defaultUser'), avatar: null, initials: '??' };
  };

  const toggleTag = (label) => {
    // Метки из API приходят как объекты вида {id, name, color}.
    const taskLabels = task.tags || [];
    const isAssigned = taskLabels.some(t => {
      if (typeof t === 'object') return t.id === label.id;
      return t === label.name;
    });
    if (isAssigned) {
      removeLabel(task.id, label.id);
    } else {
      assignLabel(task.id, label.id);
    }
  };

  const handleDelete = () => {
    if (readOnly) return;
    deleteTask(task.id);
    onClose();
  };

  const handleCopyLink = async () => {
    const taskUrl = new URL(window.location.pathname, window.location.origin);
    taskUrl.searchParams.set('taskId', task.id);
    taskUrl.hash = `task-${task.id}`;

    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(taskUrl.toString());
      } else {
        const textarea = document.createElement('textarea');
        textarea.value = taskUrl.toString();
        textarea.style.position = 'fixed';
        textarea.style.left = '-9999px';
        document.body.appendChild(textarea);
        textarea.select();
        document.execCommand('copy');
        document.body.removeChild(textarea);
      }
    } catch (error) {
      console.error('Failed to copy task link:', error);
    } finally {
      setShowActionsMenu(false);
    }
  };

  const handleAssigneeChange = (userId) => {
    if (readOnly) return;
    const user = users.find(u => u.id === userId);
    updateTask(task.id, {
      assigneeId: user ? user.id : null,
    });
  };

  // Обработчики подзадач работают поверх реального API.
  const subtasks = task?.subtasks || [];
  const completedSubtasks = subtasks.filter(t => t.isCompleted || t.completed).length;
  const totalSubtasks = subtasks.length;

  const handleAddSubtask = async () => {
    if (readOnly) return;
    const title = newSubtaskTitle.trim();
    if (!title) return;
    await addSubTask(task.id, title);
    setNewSubtaskTitle('');
  };

  const handleToggleSubtask = async (subtask) => {
    if (readOnly) return;
    await updateSubTask(subtask.id, {
      title: subtask.title,
      isCompleted: !(subtask.isCompleted || subtask.completed),
    });
  };

  const handleUpdateSubtaskTitle = async (subtask, newTitle) => {
    if (readOnly) return;
    await updateSubTask(subtask.id, { title: newTitle, isCompleted: subtask.isCompleted || subtask.completed });
  };

  const handleDeleteSubtask = async (subtaskId) => {
    if (readOnly) return;
    await deleteSubTask(subtaskId);
  };

  if (!task) return null;

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 sm:p-6">
      {/* Затемняющий backdrop. */}
      <div 
        className="absolute inset-0 bg-slate-900/20 dark:bg-black/40 backdrop-blur-sm transition-opacity pointer-events-none"
      />

      {/* Основное содержимое модального окна. */}
      <div className="relative w-full max-w-4xl max-h-[90vh] bg-white dark:bg-[#16181D] rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 flex flex-col overflow-hidden animate-in fade-in zoom-in-95 duration-200 z-10">
        
        {/* Заголовок модального окна. */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 dark:border-slate-800/50">
          <div className="flex items-center gap-3">
            <span className="text-sm font-mono text-slate-500 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 px-2 py-1 rounded">
              {task.id}
            </span>
            <select
              value={task.statusRaw || task.status}
              onChange={(e) => handleUpdate('status', e.target.value)}
              className={cn(
                "px-2.5 py-0.5 rounded-full text-xs font-medium border appearance-none cursor-pointer outline-none focus:ring-2 focus:ring-offset-1 focus:ring-blue-500",
                isDoneStatus(task.statusRaw || task.status) ? "bg-green-50 text-green-700 border-green-200 dark:bg-green-900/20 dark:text-green-400 dark:border-green-900/30" :
                isInProgressStatus(task.statusRaw || task.status) ? "bg-blue-50 text-blue-700 border-blue-200 dark:bg-blue-900/20 dark:text-blue-400 dark:border-blue-900/30" :
                "bg-slate-50 text-slate-700 border-slate-200 dark:bg-slate-800 dark:text-slate-400 dark:border-slate-700"
              )}
            >
              {projectColumns.length > 0 ? (
                projectColumns.map((col) => (
                  <option key={col.id} value={col.rawTitle || col.title}>
                    {getStatusDisplayName(t, col.rawTitle || col.title)}
                  </option>
                ))
              ) : (
                <>
                  <option value={getDefaultRawStatus('todo')}>{t('analytics.statuses.todo')}</option>
                  <option value={getDefaultRawStatus('inProgress')}>{t('analytics.statuses.inProgress')}</option>
                  <option value={getDefaultRawStatus('done')}>{t('analytics.statuses.done')}</option>
                </>
              )}
            </select>
          </div>
          <div className="relative flex items-center gap-2">
            <button
              type="button"
              onClick={() => setShowActionsMenu((prev) => !prev)}
              className="p-2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
            >
              <MoreHorizontal size={20} />
            </button>
            {showActionsMenu && (
              <div
                className="absolute right-12 top-full z-20 mt-2 w-48 rounded-xl border p-2 shadow-xl"
                style={{
                  backgroundColor: 'var(--app-layer-surface)',
                  borderColor: 'var(--app-border)',
                }}
              >
                <button
                  type="button"
                  onClick={handleCopyLink}
                  className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors hover:bg-slate-100 dark:hover:bg-slate-800"
                  style={{ color: 'var(--app-text-main)' }}
                >
                  <LinkIcon size={15} />
                  {t('taskDetail.copyLink')}
                </button>
                {!readOnly && (
                  <button
                    type="button"
                    onClick={() => {
                      setShowActionsMenu(false);
                      setShowDeleteConfirm(true);
                    }}
                    className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-red-500 transition-colors hover:bg-red-500/10"
                  >
                    <Trash2 size={15} />
                    {t('taskDetail.deleteTask')}
                  </button>
                )}
              </div>
            )}
            <button 
              onClick={onClose}
              className="p-2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
            >
              <X size={20} />
            </button>
          </div>
        </div>

        {/* Основное тело окна. */}
        <div className="flex-1 overflow-y-auto">
          <div className="grid grid-cols-1 lg:grid-cols-[1fr_300px]">
            
            {/* Главная колонка с содержимым задачи. */}
            <div className="p-6 lg:p-8 space-y-8">
              {/* Заголовок и описание задачи. */}
              <div>
                <input
                  type="text"
                  value={title}
                  onChange={(e) => !readOnly && setTitle(e.target.value)}
                  onBlur={() => handleUpdate('title', title)}
                  readOnly={readOnly}
                  className="w-full text-2xl font-bold text-slate-900 dark:text-white mb-4 leading-tight bg-transparent border-none focus:ring-0 p-0 placeholder:text-slate-400"
                  placeholder={t('taskDetail.titlePlaceholder')}
                />
                <div className="prose prose-slate dark:prose-invert max-w-none">
                  <textarea
                    value={description}
                    onChange={(e) => !readOnly && setDescription(e.target.value)}
                    onBlur={() => handleUpdate('description', description)}
                    readOnly={readOnly}
                    placeholder={t('taskDetail.descriptionPlaceholder')}
                    className="w-full min-h-[100px] bg-transparent border-none focus:ring-0 p-0 text-slate-600 dark:text-slate-300 leading-relaxed resize-none placeholder:text-slate-400"
                  />
                </div>
              </div>

              {/* Блок подзадач. */}
              <div>
                <div className="flex items-center justify-between mb-3">
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white uppercase tracking-wider flex items-center gap-2">
                    <CheckSquare size={16} />
                    {t('taskDetail.subtasksTitle')}
                  </h3>
                  <span className="text-xs text-slate-500">{t('taskDetail.subtasksProgress', { done: completedSubtasks, total: totalSubtasks })}</span>
                </div>

                {/* Индикатор выполнения подзадач. */}
                {totalSubtasks > 0 && (
                  <div className="w-full h-1.5 bg-slate-100 dark:bg-slate-800 rounded-full mb-4 overflow-hidden">
                    <div 
                      className="h-full bg-blue-500 transition-all duration-300 ease-out"
                      style={{ width: `${(completedSubtasks / totalSubtasks) * 100}%` }}
                    />
                  </div>
                )}

                <div className="custom-scrollbar max-h-[260px] space-y-2 overflow-y-auto pr-1">
                  {subtasks.map((subtask) => {
                    const isDone = subtask.isCompleted || subtask.completed;
                    return (
                      <div key={subtask.id} className="flex items-start gap-3 p-2 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors group border border-transparent hover:border-slate-200 dark:hover:border-slate-800">
                        <button 
                          onClick={() => handleToggleSubtask(subtask)}
                          className={cn(
                            "mt-1 w-4 h-4 rounded border flex items-center justify-center transition-colors shrink-0",
                            isDone 
                              ? "bg-blue-500 border-blue-500 text-white" 
                              : "border-slate-400 dark:border-slate-500 hover:border-blue-500 bg-white dark:bg-transparent"
                          )}
                        >
                          {isDone && <CheckSquare size={10} />}
                        </button>
                        
                        <SubtaskTitleInput
                          subtask={subtask}
                          isDone={isDone}
                          onSave={(newTitle) => handleUpdateSubtaskTitle(subtask, newTitle)}
                        />

                        <button 
                          onClick={() => handleDeleteSubtask(subtask.id)}
                          className="opacity-0 group-hover:opacity-100 p-1 text-slate-400 hover:text-red-500 transition-all"
                        >
                          <X size={14} />
                        </button>
                      </div>
                    );
                  })}
                  
                  {/* Inline-поле добавления новой подзадачи. */}
                  {!readOnly && (
                  <div className="flex items-center gap-2 px-2 py-1">
                    <input
                      type="text"
                      value={newSubtaskTitle}
                      onChange={(e) => setNewSubtaskTitle(e.target.value)}
                      onKeyDown={(e) => { if (e.key === 'Enter') handleAddSubtask(); }}
                      placeholder={t('taskDetail.newSubtaskPlaceholder')}
                      className="flex-1 bg-transparent border-none p-0 text-sm focus:ring-0 placeholder:text-slate-400 text-slate-700 dark:text-slate-200 outline-none"
                    />
                    <button 
                      onClick={handleAddSubtask}
                      disabled={!newSubtaskTitle.trim()}
                      className="text-sm text-blue-500 hover:text-blue-600 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                    >
                      <PlusIcon className="w-4 h-4" />
                    </button>
                  </div>
                  )}
                </div>
              </div>

              {/* Активность и комментарии. */}
              <div>
                <h3 className="text-sm font-bold text-slate-900 dark:text-white uppercase tracking-wider flex items-center gap-2 mb-4">
                  <MessageSquare size={16} />
                  {t('taskDetail.activity')}
                </h3>
                
                {/* Список комментариев. */}
                <div className="custom-scrollbar mb-6 max-h-[320px] space-y-6 overflow-y-auto pr-1">
                  {(Array.isArray(task.comments) ? task.comments : []).map((comment) => {
                    const author = getUserById(comment.authorId);
                    const isMe = comment.authorId === currentUser.id;
                    const isEditing = editingCommentId === comment.id;
                    
                    return (
                      <div key={comment.id} className="flex gap-3 group">
                        <div className="flex-shrink-0">
                          {author.avatar ? (
                            <img src={author.avatar} alt={author.name} className="w-8 h-8 rounded-full bg-slate-100 dark:bg-slate-800" />
                          ) : (
                            <div className="w-8 h-8 rounded-full bg-indigo-100 dark:bg-indigo-900/50 flex items-center justify-center text-xs font-bold text-indigo-600 dark:text-indigo-400">
                              {author.initials || author.name[0]}
                            </div>
                          )}
                        </div>
                        <div className="flex-1">
                          <div className="flex items-center gap-2 mb-1">
                            <span className="text-sm font-bold text-slate-900 dark:text-white">
                              {author.name} {isMe && t('taskDetail.you')}
                            </span>
                            <span className="text-xs text-slate-500 dark:text-slate-400">
                              {formatTime(comment.createdAt, language, { hour: '2-digit', minute: '2-digit' })}
                            </span>
                            {isMe && !isEditing && (
                              <>
                                <button
                                  onClick={() => { setEditingCommentId(comment.id); setEditingCommentText(comment.text); }}
                                  className="opacity-0 group-hover:opacity-100 p-0.5 text-slate-400 hover:text-blue-500 transition-all"
                                  title={t('taskDetail.editComment')}
                                >
                                  <Pencil size={12} />
                                </button>
                                <button
                                  onClick={() => deleteComment(comment.id)}
                                  className="opacity-0 group-hover:opacity-100 p-0.5 text-slate-400 hover:text-red-500 transition-all"
                                  title={t('taskDetail.deleteComment')}
                                >
                                  <Trash2 size={12} />
                                </button>
                              </>
                            )}
                          </div>
                          {isEditing ? (
                            <div className="space-y-2">
                              <textarea
                                value={editingCommentText}
                                onChange={(e) => setEditingCommentText(e.target.value)}
                                className="w-full min-h-[60px] p-2 rounded-lg bg-white dark:bg-[#16181D] border border-slate-200 dark:border-slate-700 text-sm text-slate-900 dark:text-white resize-none outline-none focus:ring-2 focus:ring-blue-500/20"
                              />
                              <div className="flex gap-2">
                                <button
                                  onClick={async () => {
                                    if (editingCommentText.trim()) {
                                      await updateComment(comment.id, editingCommentText.trim());
                                    }
                                    setEditingCommentId(null);
                                  }}
                                  className="px-3 py-1 bg-blue-600 hover:bg-blue-700 text-white text-xs font-medium rounded transition-colors"
                                >
                                  {t('common:actions.save')}
                                </button>
                                <button
                                  onClick={() => setEditingCommentId(null)}
                                  className="px-3 py-1 bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 text-xs font-medium rounded transition-colors"
                                >
                                  {t('common:actions.cancel')}
                                </button>
                              </div>
                            </div>
                          ) : (
                            <div className="text-sm text-slate-700 dark:text-slate-300 leading-relaxed bg-slate-50 dark:bg-slate-800/50 p-3 rounded-r-xl rounded-bl-xl">
                              {comment.text}
                            </div>
                          )}
                        </div>
                      </div>
                    );
                  })}
                  <div ref={commentsEndRef} />
                </div>

                {/* Поле добавления нового комментария. */}
                <div className="flex gap-4">
                  <div className="w-8 h-8 rounded-full bg-indigo-100 dark:bg-indigo-900/50 flex items-center justify-center text-xs font-bold text-indigo-600 dark:text-indigo-400 flex-shrink-0 overflow-hidden">
                    {currentUser.avatar ? (
                      <img src={currentUser.avatar} alt={currentUser.name || t('sidebar.defaultUser')} className="w-full h-full object-cover" />
                    ) : (
                      currentUser.initials || currentUser.name?.slice(0, 1)?.toUpperCase() || '?'
                    )}
                  </div>
                  <div className="flex-1">
                    <div className="relative">
                      <textarea 
                        value={newComment}
                        onChange={(e) => setNewComment(e.target.value)}
                        onKeyDown={handleKeyDown}
                        placeholder={t('taskDetail.commentPlaceholder')} 
                        className="w-full min-h-[80px] p-3 pr-12 rounded-lg bg-white dark:bg-[#16181D] border border-slate-200 dark:border-slate-700 focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 outline-none text-sm text-slate-900 dark:text-white resize-none transition-all shadow-sm"
                      />
                      <button 
                        onClick={handleAddComment}
                        disabled={!newComment.trim()}
                        className="absolute bottom-3 right-3 p-1.5 bg-blue-600 hover:bg-blue-700 disabled:bg-slate-300 dark:disabled:bg-slate-700 disabled:cursor-not-allowed text-white rounded-md transition-colors"
                      >
                        <Send size={14} />
                      </button>
                    </div>
                    <div className="text-[10px] text-slate-400 mt-1 text-right">
                      {t('taskDetail.commentHint')}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Правая боковая колонка со свойствами. */}
            <div className="bg-slate-50/50 dark:bg-[#0F1117]/50 border-l border-slate-200 dark:border-slate-800 p-6 space-y-6">
              
              {/* Группа свойств задачи. */}
              <div className="space-y-4">
                <div className="space-y-1">
                  <label className="text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">{t('taskDetail.assignee')}</label>
                  <div className="relative">
                    <select
                      value={task.assigneeId || task.assignee?.id || ''}
                      onChange={(e) => handleAssigneeChange(e.target.value)}
                      className="w-full appearance-none bg-transparent p-2 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg cursor-pointer transition-colors text-sm font-medium text-slate-700 dark:text-slate-200 outline-none focus:ring-2 focus:ring-blue-500/20"
                    >
                      <option value="">{t('taskDetail.unassigned')}</option>
                      {users.map(user => (
                        <option key={user.id} value={user.id}>{user.name}</option>
                      ))}
                    </select>
                    <div className="absolute inset-y-0 right-2 flex items-center pointer-events-none">
                      {task.assignee ? (
                        <div className="w-6 h-6 rounded-full bg-indigo-100 dark:bg-indigo-900/50 flex items-center justify-center text-[10px] font-bold text-indigo-600 dark:text-indigo-400">
                          {task.assignee.initials}
                        </div>
                      ) : (
                        <User size={16} className="text-slate-400" />
                      )}
                    </div>
                  </div>
                </div>

                <div className="space-y-1">
                  <label className="text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">{t('taskDetail.dueDate')}</label>
                  {(() => {
                    const d = task.dueDate ? new Date(task.dueDate) : null;
                    const now = new Date();
                    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
                    const isOverdue = d && d < today;
                    const isToday = d && d.toDateString() === now.toDateString();
                    const isSoon = d && !isOverdue && !isToday && (d - today) < 3 * 86400000;
                    const statusColor = isOverdue ? 'text-red-500' : isToday ? 'text-amber-500' : isSoon ? 'text-yellow-500' : 'text-slate-400';
                    const statusLabel = isOverdue ? t('taskDetail.overdue') : isToday ? t('taskDetail.today') : isSoon ? t('taskDetail.soon') : null;
                    return (
                      <div className="space-y-1.5">
                        <div className="flex items-center gap-2 p-2 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg cursor-pointer transition-colors relative group">
                          <Calendar size={16} className={statusColor} />
                          <input
                            type="date"
                            value={task.dueDate || ''}
                            onChange={(e) => handleUpdate('dueDate', e.target.value || null)}
                            className="bg-transparent border-none text-sm font-medium text-slate-700 dark:text-slate-200 outline-none focus:ring-0 p-0 w-full cursor-pointer"
                          />
                        </div>
                        {statusLabel && (
                          <span className={cn("text-[11px] font-medium px-2 py-0.5 rounded-full inline-block",
                            isOverdue ? "bg-red-500/10 text-red-500" :
                            isToday ? "bg-amber-500/10 text-amber-500" :
                            "bg-yellow-500/10 text-yellow-500"
                          )}>
                            {statusLabel}
                            {d && ` — ${formatDate(d, language, { day: 'numeric', month: 'short' })}`}
                          </span>
                        )}
                      </div>
                    );
                  })()}
                </div>

                <div className="space-y-1">
                  <div className="flex items-center justify-between">
                    <label className="text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">{t('taskDetail.tags')}</label>
                    <button
                      onClick={() => setShowNewLabel(!showNewLabel)}
                      className="text-xs text-blue-500 hover:text-blue-600 transition-colors"
                    >
                      <Plus size={14} />
                    </button>
                  </div>

                  {/* Inline-создание новой метки. */}
                  {showNewLabel && (
                    <div className="flex items-center gap-2 pt-1">
                      <input
                        type="color"
                        value={newLabelColor}
                        onChange={(e) => setNewLabelColor(e.target.value)}
                        className="w-6 h-6 rounded cursor-pointer border-0 p-0"
                      />
                      <input
                        type="text"
                        value={newLabelName}
                        onChange={(e) => setNewLabelName(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' && newLabelName.trim()) {
                            createLabel(newLabelName.trim(), newLabelColor);
                            setNewLabelName('');
                            setShowNewLabel(false);
                          }
                        }}
                        placeholder={t('taskDetail.newLabelPlaceholder')}
                        className="flex-1 bg-transparent border-b border-slate-300 dark:border-slate-600 text-sm text-slate-700 dark:text-slate-200 outline-none p-0 pb-0.5 placeholder:text-slate-400"
                        autoFocus
                      />
                      <button
                        onClick={() => {
                          if (newLabelName.trim()) {
                            createLabel(newLabelName.trim(), newLabelColor);
                            setNewLabelName('');
                            setShowNewLabel(false);
                          }
                        }}
                        disabled={!newLabelName.trim()}
                        className="text-blue-500 hover:text-blue-600 disabled:opacity-30 transition-colors"
                      >
                        <CheckSquare size={14} />
                      </button>
                    </div>
                  )}

                  <div className="custom-scrollbar flex max-h-[180px] flex-wrap gap-2 overflow-y-auto pt-1 pr-1">
                    {availableTags.map(label => {
                      const taskLabels = task.tags || [];
                      const isSelected = taskLabels.some(t =>
                        typeof t === 'object' ? t.id === label.id : t === label.name
                      );
                      return (
                        <div key={label.id || label.name} className="group/label relative">
                          <button
                            onClick={() => toggleTag(label)}
                            className={cn(
                              "px-2 py-1 rounded text-xs font-medium transition-all border",
                              isSelected
                                ? "border-transparent shadow-sm text-white"
                                : "bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 border-slate-200 dark:border-slate-700 hover:bg-slate-200 dark:hover:bg-slate-700"
                            )}
                            style={isSelected ? { backgroundColor: label.color || '#6366f1' } : undefined}
                          >
                            {label.displayName || label.name}
                          </button>
                          <button
                            onClick={() => deleteLabel(label.id)}
                            className="absolute -top-1.5 -right-1.5 w-4 h-4 rounded-full bg-red-500 text-white items-center justify-center text-[8px] hidden group-hover/label:flex transition-all"
                            title={t('taskCard.menu.delete')}
                          >
                            <X size={8} />
                          </button>
                        </div>
                      );
                    })}
                    {availableTags.length === 0 && !showNewLabel && (
                      <button
                        onClick={() => setShowNewLabel(true)}
                        className="text-xs text-blue-500 hover:text-blue-600 flex items-center gap-1 transition-colors"
                      >
                        <Tag size={12} /> {t('taskDetail.createFirstLabel')}
                      </button>
                    )}
                  </div>
                </div>
              </div>

              <div className="h-px bg-slate-200 dark:bg-slate-800" />

              {/* Служебная метаинформация. */}
              <div className="space-y-3">
                <div className="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
                  <span className="flex items-center gap-2"><Clock size={14} /> {t('taskDetail.createdAt')}</span>
                  <span>{task.createdAt ? formatDateTime(task.createdAt, language, { day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' }) : '—'}</span>
                </div>
                <div className="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
                  <span className="flex items-center gap-2"><Clock size={14} /> {t('taskDetail.updatedAt')}</span>
                  <span>{task.updatedAt ? formatDateTime(task.updatedAt, language, { day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' }) : '—'}</span>
                </div>
              </div>

              {/* Действия над задачей. */}
              <div className="space-y-2 pt-4">
                <button
                  type="button"
                  disabled
                  className="w-full flex items-center gap-2 px-3 py-2 text-sm font-medium rounded-lg opacity-50 cursor-not-allowed"
                  style={{ cursor: 'not-allowed' }}
                >
                  <Paperclip size={16} /> {t('taskDetail.attachFile')}
                </button>
                <button
                  type="button"
                  onClick={handleCopyLink}
                  className="w-full flex items-center gap-2 px-3 py-2 text-sm font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
                >
                  <LinkIcon size={16} /> {t('taskDetail.copyLink')}
                </button>
                
                <div className="pt-4 border-t border-slate-200 dark:border-slate-800 mt-4">
                  {readOnly ? null : !showDeleteConfirm ? (
                    <button 
                      onClick={() => setShowDeleteConfirm(true)}
                      className="w-full flex items-center gap-2 px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-colors"
                    >
                      <Trash2 size={16} /> {t('taskDetail.deleteTask')}
                    </button>
                  ) : (
                    <div className="p-3 bg-red-50 dark:bg-red-900/20 rounded-lg space-y-2">
                      <p className="text-xs text-red-600 dark:text-red-400 font-medium">{t('taskDetail.deleteConfirm')}</p>
                      <div className="flex gap-2">
                        <button 
                          onClick={handleDelete}
                          className="flex-1 px-2 py-1 bg-red-600 hover:bg-red-700 text-white text-xs font-medium rounded transition-colors"
                        >
                          {t('taskDetail.deleteApprove')}
                        </button>
                        <button 
                          onClick={() => setShowDeleteConfirm(false)}
                          className="flex-1 px-2 py-1 bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-300 text-xs font-medium rounded border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
                        >
                          {t('common:actions.cancel')}
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default TaskDetailModal;
