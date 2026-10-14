import { useEffect, useRef, useCallback, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { logRealtimeIssue, shouldIgnoreRealtimeError } from '../utils/realtimeErrors';

/**
 * Хук для realtime-подключения к SignalR-хабу рабочего пространства.
 *
 * @param {string|null} accessToken JWT для аутентификации.
 * @param {object} handlers отображение event name -> callback.
 *
 * Возвращает helper-методы join/leave и явные метаданные о состоянии соединения.
 */
export default function useSignalR(accessToken, handlers = {}) {
  const connectionRef = useRef(null);
  const handlersRef = useRef(handlers);
  const isStoppingRef = useRef(false);
  handlersRef.current = handlers;

  const apiBase = import.meta.env.VITE_API_BASE_URL || '/api';
  const joinedProjectsRef = useRef(new Set());
  const joinedWorkspacesRef = useRef(new Set());

  const [connectionState, setConnectionState] = useState(signalR.HubConnectionState.Disconnected);
  const markDegraded = useCallback((errorCode, error, { silent = false } = {}) => {
    logRealtimeIssue('SignalR', errorCode, error, {
      isShuttingDown: isStoppingRef.current,
      silent,
    });
  }, []);

  const clearRealtimeError = useCallback(() => {}, []);

  useEffect(() => {
    if (!accessToken) {
      isStoppingRef.current = true;
      setConnectionState(signalR.HubConnectionState.Disconnected);
      clearRealtimeError();
      return undefined;
    }

    isStoppingRef.current = false;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${apiBase}/management/hubs/workspace`, {
        accessTokenFactory: () => accessToken,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.None)
      .build();

    connectionRef.current = connection;
    setConnectionState(connection.state);

    const events = [
      'WorkspaceChanged', 'ProjectChanged', 'StatusChanged',
      'TaskCreated', 'TaskUpdated', 'TaskDeleted', 'TaskMoved',
      'SubTaskChanged', 'CommentChanged', 'LabelChanged', 'MemberChanged',
      'NotificationReceived', 'UnreadCountChanged',
    ];

    events.forEach((eventName) => {
      connection.on(eventName, (payload) => {
        const handler = handlersRef.current[eventName];
        if (handler) {
          handler(payload);
        }
      });
    });

    connection.onreconnecting((error) => {
      setConnectionState(signalR.HubConnectionState.Reconnecting);
      markDegraded('workspace-realtime-reconnecting', error, { silent: true });
    });

    connection.onreconnected(async () => {
      setConnectionState(signalR.HubConnectionState.Connected);
      clearRealtimeError();

      for (const workspaceId of joinedWorkspacesRef.current) {
        try {
          await connection.invoke('JoinWorkspace', workspaceId);
        } catch (error) {
          markDegraded('workspace-realtime-rejoin-workspace-failed', error);
        }
      }

      for (const projectId of joinedProjectsRef.current) {
        try {
          await connection.invoke('JoinProject', projectId);
        } catch (error) {
          markDegraded('workspace-realtime-rejoin-project-failed', error);
        }
      }
    });

    connection.onclose((error) => {
      setConnectionState(signalR.HubConnectionState.Disconnected);
      if (error && !shouldIgnoreRealtimeError(error, { isShuttingDown: isStoppingRef.current })) {
        markDegraded('workspace-realtime-closed', error);
      }
    });

    connection.start()
      .then(() => {
        setConnectionState(signalR.HubConnectionState.Connected);
        clearRealtimeError();

        for (const workspaceId of joinedWorkspacesRef.current) {
          connection.invoke('JoinWorkspace', workspaceId).catch((error) => {
            markDegraded('workspace-realtime-join-workspace-failed', error);
          });
        }

        for (const projectId of joinedProjectsRef.current) {
          connection.invoke('JoinProject', projectId).catch((error) => {
            markDegraded('workspace-realtime-join-project-failed', error);
          });
        }
      })
      .catch((error) => {
        setConnectionState(signalR.HubConnectionState.Disconnected);
        markDegraded('workspace-realtime-connect-failed', error);
      });

    return () => {
      isStoppingRef.current = true;
      void connection.stop().catch((error) => {
        logRealtimeIssue('SignalR', 'workspace-realtime-stop-failed', error, {
          isShuttingDown: true,
        });
      });

      connectionRef.current = null;
      setConnectionState(signalR.HubConnectionState.Disconnected);
    };
  }, [accessToken, apiBase, clearRealtimeError, markDegraded]);

  const joinProject = useCallback((projectId) => {
    if (!projectId) return;

    joinedProjectsRef.current.add(projectId);
    const connection = connectionRef.current;
    if (connection?.state === signalR.HubConnectionState.Connected) {
      connection.invoke('JoinProject', projectId)
        .then(() => clearRealtimeError())
        .catch((error) => markDegraded('workspace-realtime-join-project-failed', error));
    }
  }, [clearRealtimeError, markDegraded]);

  const leaveProject = useCallback((projectId) => {
    if (!projectId) return;

    joinedProjectsRef.current.delete(projectId);
    const connection = connectionRef.current;
    if (connection?.state === signalR.HubConnectionState.Connected) {
      connection.invoke('LeaveProject', projectId)
        .then(() => clearRealtimeError())
        .catch((error) => markDegraded('workspace-realtime-leave-project-failed', error));
    }
  }, [clearRealtimeError, markDegraded]);

  const joinWorkspace = useCallback((workspaceId) => {
    if (!workspaceId) return;

    joinedWorkspacesRef.current.add(workspaceId);
    const connection = connectionRef.current;
    if (connection?.state === signalR.HubConnectionState.Connected) {
      connection.invoke('JoinWorkspace', workspaceId)
        .then(() => clearRealtimeError())
        .catch((error) => markDegraded('workspace-realtime-join-workspace-failed', error));
    }
  }, [clearRealtimeError, markDegraded]);

  const leaveWorkspace = useCallback((workspaceId) => {
    if (!workspaceId) return;

    joinedWorkspacesRef.current.delete(workspaceId);
    const connection = connectionRef.current;
    if (connection?.state === signalR.HubConnectionState.Connected) {
      connection.invoke('LeaveWorkspace', workspaceId)
        .then(() => clearRealtimeError())
        .catch((error) => markDegraded('workspace-realtime-leave-workspace-failed', error));
    }
  }, [clearRealtimeError, markDegraded]);

  return {
    joinProject,
    leaveProject,
    joinWorkspace,
    leaveWorkspace,
    connectionState,
  };
}
