import React, { createContext, useContext, useState, useEffect } from 'react';
import i18n from '../i18n/config';

const TaskContext = createContext();

export const useTaskContext = () => {
  const context = useContext(TaskContext);
  if (!context) {
    throw new Error('useTaskContext must be used within a TaskProvider');
  }
  return context;
};

// Базовое пустое состояние для demo/local режима.
const initialData = {
  columns: {},
  tasks: {},
  columnOrder: [],
  notifications: [],
  users: [],
  availableTags: [],
  currentUser: {
    id: '',
    name: '',
    avatar: '',
    role: ''
  },
  mockUsers: []
};

export const TaskProvider = ({ children }) => {
  const [data, setData] = useState(() => {
    const savedData = localStorage.getItem('collaby-data');
    if (savedData) {
      return JSON.parse(savedData);
    }
    return initialData;
  });
  
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedAssignee, setSelectedAssignee] = useState('All');

  useEffect(() => {
    localStorage.setItem('collaby-data', JSON.stringify(data));
  }, [data]);

  // --- Уведомления и комментарии ---

  const addNotification = (notification) => {
    setData(prev => ({
      ...prev,
      notifications: [notification, ...(prev.notifications || [])]
    }));
  };

  const markAllRead = () => {
    setData(prev => ({
      ...prev,
      notifications: (prev.notifications || []).map(n => ({ ...n, isRead: true }))
    }));
  };

  const addComment = (taskId, text) => {
    const newComment = {
      id: `c-${Date.now()}`,
      text,
      authorId: data.currentUser.id,
      createdAt: new Date().toISOString()
    };

    setData(prev => ({
      ...prev,
      tasks: {
        ...prev.tasks,
        [taskId]: {
          ...prev.tasks[taskId],
          comments: [...(prev.tasks[taskId].comments || []), newComment]
        }
      }
    }));
  };

  // --- Demo-симуляция активности ---
  useEffect(() => {
    const timer = setTimeout(() => {
      // Для demo-режима имитируем одно действие после загрузки сессии.
      
      const currentMockUsers = data.mockUsers || initialData.mockUsers;
      if (!currentMockUsers || currentMockUsers.length === 0) return;

      const randomTaskKeys = Object.keys(data.tasks);
      if (randomTaskKeys.length === 0) return;
      
      const randomTaskId = randomTaskKeys[Math.floor(Math.random() * randomTaskKeys.length)];
      const randomUser = currentMockUsers[Math.floor(Math.random() * currentMockUsers.length)];
      const taskTitle = data.tasks[randomTaskId].title;

      const fakeComment = {
        id: `c-sim-${Date.now()}`,
        text: i18n.t('platform:demo.commentPraise'),
        authorId: randomUser.id,
        createdAt: new Date().toISOString()
      };

      // Добавляем демонстрационный комментарий.
      setData(prev => ({
        ...prev,
        tasks: {
          ...prev.tasks,
          [randomTaskId]: {
            ...prev.tasks[randomTaskId],
            comments: [...(prev.tasks[randomTaskId].comments || []), fakeComment]
          }
        },
        notifications: [
          {
            id: `n-sim-${Date.now()}`,
            type: 'comment',
            text: i18n.t('platform:demo.commentOnTask', { name: randomUser.name, task: taskTitle }),
            time: i18n.t('platform:notifications.time.justNow'),
            isRead: false
          },
          ...(prev.notifications || [])
        ]
      }));

      // При желании сюда можно вернуть demo-toast, но пока оставляем сценарий без награды.

    }, 10000);

    return () => clearTimeout(timer);
  }, []); // Эффект нужен один раз при монтировании.

  const addTask = (newTask) => {
    const taskId = `task-${Date.now()}`;
    const task = { ...newTask, id: taskId };
    
    // Подбираем колонку по статусу задачи или используем первую по умолчанию.
    const columnId = Object.keys(data.columns).find(key => 
      data.columns[key].title === task.status
    ) || data.columnOrder[0];

    setData(prev => ({
      ...prev,
      tasks: { ...prev.tasks, [taskId]: task },
      columns: {
        ...prev.columns,
        [columnId]: {
          ...prev.columns[columnId],
          taskIds: [...prev.columns[columnId].taskIds, taskId]
        }
      }
    }));
  };

  const updateTask = (taskId, updatedFields) => {
    setData(prev => {
      const oldTask = prev.tasks[taskId];
      const newTask = { ...oldTask, ...updatedFields };
      
      // Если статус сменился, переносим задачу между колонками.
      if (oldTask.status !== newTask.status) {
        const sourceColumnId = Object.keys(prev.columns).find(key => 
          prev.columns[key].title === oldTask.status
        );
        const destColumnId = Object.keys(prev.columns).find(key => 
          prev.columns[key].title === newTask.status
        );

        if (sourceColumnId && destColumnId && sourceColumnId !== destColumnId) {
          const sourceTaskIds = prev.columns[sourceColumnId].taskIds.filter(id => id !== taskId);
          const destTaskIds = [...prev.columns[destColumnId].taskIds, taskId];

          return {
            ...prev,
            tasks: { ...prev.tasks, [taskId]: newTask },
            columns: {
              ...prev.columns,
              [sourceColumnId]: { ...prev.columns[sourceColumnId], taskIds: sourceTaskIds },
              [destColumnId]: { ...prev.columns[destColumnId], taskIds: destTaskIds }
            }
          };
        }
      }

      return {
        ...prev,
        tasks: { ...prev.tasks, [taskId]: newTask }
      };
    });
  };

  const moveTask = (activeId, overId) => {
    const sourceColumnId = Object.keys(data.columns).find(key => 
      data.columns[key].taskIds.includes(activeId)
    );
    
    let destColumnId = overId;
    // Если пользователь бросил карточку поверх другой задачи, определяем колонку по цели.
    if (data.tasks[overId]) {
      destColumnId = Object.keys(data.columns).find(key => 
        data.columns[key].taskIds.includes(overId)
      );
    } else if (!data.columns[overId]) {
        // Если цель не колонка и не задача, перенос отменяем.
        return;
    }

    if (!sourceColumnId || !destColumnId || sourceColumnId === destColumnId) {
      // Внутри одной колонки поддерживаем только простую перестановку по списку.
      if (sourceColumnId === destColumnId) {
          const column = data.columns[sourceColumnId];
          const oldIndex = column.taskIds.indexOf(activeId);
          const newIndex = data.tasks[overId] ? column.taskIds.indexOf(overId) : column.taskIds.length;
          
          if (oldIndex !== newIndex) {
             const newTaskIds = [...column.taskIds];
             newTaskIds.splice(oldIndex, 1);
             newTaskIds.splice(newIndex, 0, activeId);
             
             setData(prev => ({
                 ...prev,
                 columns: {
                     ...prev.columns,
                     [sourceColumnId]: { ...prev.columns[sourceColumnId], taskIds: newTaskIds }
                 }
             }));
          }
      }
      return;
    }

    setData(prev => {
      const sourceTaskIds = [...prev.columns[sourceColumnId].taskIds];
      const destTaskIds = [...prev.columns[destColumnId].taskIds];
      
      // Убираем задачу из исходной колонки.
      const taskIndex = sourceTaskIds.indexOf(activeId);
      sourceTaskIds.splice(taskIndex, 1);
      
      // Добавляем задачу в колонку назначения.
      if (data.tasks[overId]) {
         const overIndex = destTaskIds.indexOf(overId);
         destTaskIds.splice(overIndex, 0, activeId);
      } else {
         destTaskIds.push(activeId);
      }

      // Синхронно обновляем статус в самой задаче.
      const updatedTask = {
        ...prev.tasks[activeId],
        status: prev.columns[destColumnId].title
      };

      return {
        ...prev,
        columns: {
          ...prev.columns,
          [sourceColumnId]: { ...prev.columns[sourceColumnId], taskIds: sourceTaskIds },
          [destColumnId]: { ...prev.columns[destColumnId], taskIds: destTaskIds },
        },
        tasks: {
          ...prev.tasks,
          [activeId]: updatedTask
        }
      };
    });
  };

  const deleteTask = (taskId) => {
    setData(prev => {
      const columnId = Object.keys(prev.columns).find(key => 
        prev.columns[key].taskIds.includes(taskId)
      );
      
      const newTasks = { ...prev.tasks };
      delete newTasks[taskId];

      const newColumns = { ...prev.columns };
      if (columnId) {
        newColumns[columnId] = {
          ...newColumns[columnId],
          taskIds: newColumns[columnId].taskIds.filter(id => id !== taskId)
        };
      }

      return {
        ...prev,
        tasks: newTasks,
        columns: newColumns
      };
    });
  };

  // Локально фильтруем задачи по строке поиска и исполнителю.
  const filteredTasks = Object.values(data.tasks).filter(task => {
    const matchesSearch = task.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (task.description && task.description.toLowerCase().includes(searchQuery.toLowerCase()));
    
    const matchesAssignee = selectedAssignee === 'All' || 
      (task.assignee && task.assignee.name === selectedAssignee);

    return matchesSearch && matchesAssignee;
  });

  const value = {
    data,
    tasks: data.tasks,
    columns: data.columns,
    columnOrder: data.columnOrder,
    users: data.users,
    availableTags: data.availableTags || initialData.availableTags,
    searchQuery,
    setSearchQuery,
    selectedAssignee,
    setSelectedAssignee,
    addTask,
    updateTask,
    moveTask,
    deleteTask,
    filteredTasks,
    currentUser: data.currentUser || initialData.currentUser,
    mockUsers: data.mockUsers || initialData.mockUsers,
    notifications: data.notifications || initialData.notifications || [],
    addComment,
    addNotification,
    markAllRead
  };

  return (
    <TaskContext.Provider value={value}>
      {children}
    </TaskContext.Provider>
  );
};
