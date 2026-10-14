// Контекст мессенджера объединяет чат, presence и сценарии звонков.
import React, { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import toast from 'react-hot-toast';
import { useAuth } from '../../shared/context/AuthContext';
import * as messengerApi from '../../shared/utils/messengerApi';
import * as callsApi from '../../shared/utils/callsApi';
import { logRealtimeIssue, shouldIgnoreRealtimeError } from '../../shared/utils/realtimeErrors';
import i18n from '../../shared/i18n/config';
import { playSoundEffect } from '../../shared/utils/soundEffects';

const MessengerContext = createContext(null);

export const useMessenger = () => {
  return useContext(MessengerContext);
};

const isSameId = (a, b) => (
  a != null
  && b != null
  && String(a).toLowerCase() === String(b).toLowerCase()
);

export const MessengerProvider = ({ children }) => {
  const { accessToken, currentUser } = useAuth();
  const apiBase = import.meta.env.VITE_API_BASE_URL || '/api';

  // ─── Основное состояние мессенджера ─────────────────────────────────
  const [chats, setChats] = useState([]);
  const [activeChat, setActiveChat] = useState(null);
  const [messages, setMessages] = useState({});        // { chatId: MessageResponse[] }
  const [typingUsers, setTypingUsers] = useState({});   // { chatId: Set<userId> }
  const [onlineUsers, setOnlineUsers] = useState({});   // { userId: boolean }
  const [unreadTotal, setUnreadTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [messengerOpen, setMessengerOpen] = useState(false);

  // Состояние звонка и связанных media-ресурсов.
  const [activeCallSession, setActiveCallSession] = useState(null);
  const [incomingCall, setIncomingCall] = useState(null);
  const [callParticipants, setCallParticipants] = useState([]);
  const [localStream, setLocalStream] = useState(null);
  const [remoteStreams, setRemoteStreams] = useState({});  // { userId: MediaStream }
  const [isScreenSharing, setIsScreenSharing] = useState(false);
  const [isPiP, setIsPiP] = useState(false);
  const [callNotif, setCallNotif] = useState(null); // { message, type } | null

  // Refs нужны для актуального состояния внутри callbacks и WebRTC-обработчиков.
  const chatHubRef = useRef(null);
  const callHubRef = useRef(null);
  const activeCallSessionRef = useRef(null); // Всегда актуальная ссылка на активную сессию для замыканий.
  const activeChatRef = useRef(null);        // Всегда актуальный id выбранного чата.
  const localStreamRef = useRef(null);       // Всегда актуальная ссылка на локальный media stream.
  const remoteMediaStreamsRef = useRef({});  // { userId: MediaStream } для объединения разрозненных track events.
  const peerConnectionsRef = useRef({});     // { userId: RTCPeerConnection }
  const joinedChatsRef = useRef(new Set());
  const processedMessageIdsRef = useRef(new Set());
  const typingTimeoutsRef = useRef({});
  const callNotifTimerRef = useRef(null);
  const chatHubStoppingRef = useRef(false);
  const callHubStoppingRef = useRef(false);
  const incomingCallRef = useRef(null);
  const recentlyEndedCallIdsRef = useRef(new Map());

  useEffect(() => {
    incomingCallRef.current = incomingCall;
  }, [incomingCall]);

  const markCallAsRecentlyEnded = useCallback((callId) => {
    if (!callId) {
      return;
    }

    const normalizedCallId = String(callId);
    recentlyEndedCallIdsRef.current.set(normalizedCallId, Date.now());

    const threshold = Date.now() - 90_000;
    for (const [knownCallId, endedAt] of recentlyEndedCallIdsRef.current.entries()) {
      if (endedAt < threshold) {
        recentlyEndedCallIdsRef.current.delete(knownCallId);
      }
    }
  }, []);

  const isCallRecentlyEnded = useCallback((callId) => {
    if (!callId) {
      return false;
    }

    const endedAt = recentlyEndedCallIdsRef.current.get(String(callId));
    return typeof endedAt === 'number' && Date.now() - endedAt < 90_000;
  }, []);

  // ─── Вспомогательные функции: объявляем до useEffect, которые их используют ───
  const updateUnreadTotal = useCallback((chatList) => {
    const list = chatList || chats;
    const total = list.reduce((sum, c) => sum + (c.unreadCount || 0), 0);
    setUnreadTotal(total);
  }, [chats]);

  const showCallNotif = useCallback((message, type = 'info') => {
    clearTimeout(callNotifTimerRef.current);
    setCallNotif({ message, type });
    callNotifTimerRef.current = setTimeout(() => setCallNotif(null), 4500);
  }, []);

  const reportMessengerIssue = useCallback((errorCode, error, {
    silent = false,
    isShuttingDown = false,
  } = {}) => {
    logRealtimeIssue('Messenger', errorCode, error, {
      silent,
      isShuttingDown,
    });

    if (silent || shouldIgnoreRealtimeError(error, { isShuttingDown })) {
      return;
    }

  }, []);

  const clearMessengerIssue = useCallback(() => {}, []);

  const dismissCallNotif = useCallback(() => {
    clearTimeout(callNotifTimerRef.current);
    setCallNotif(null);
  }, []);

  // ─── Chat Hub (SignalR) ────────────────────────────────────────────
  useEffect(() => {
    if (!accessToken) {
      chatHubStoppingRef.current = true;
      return undefined;
    }

    chatHubStoppingRef.current = false;

    const hubUrl = `${apiBase}/messenger/hubs/chat`;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => accessToken })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.None)
      .build();

    // События чата приходят через SignalR и обновляют локальное состояние без полного refresh.
    connection.on('NewMessage', (message) => {
      if (!message?.id) {
        return;
      }

      if (processedMessageIdsRef.current.has(message.id)) {
        return;
      }

      processedMessageIdsRef.current.add(message.id);
      if (processedMessageIdsRef.current.size > 1000) {
        const oldestMessageId = processedMessageIdsRef.current.values().next().value;
        processedMessageIdsRef.current.delete(oldestMessageId);
      }

      const isOwnMessage = message.senderId === currentUser?.id;
      const isActiveChat = activeChatRef.current === message.chatId;
      if (!isOwnMessage && !isActiveChat) {
        const senderName =
          message.senderDisplayName
          || message.senderUsername
          || i18n.t('platform:messenger.userFallback');
        const text = typeof message.text === 'string' ? message.text.trim() : '';

        toast(text
          ? i18n.t('platform:notifications.toast.newMessage', { sender: senderName, text })
          : i18n.t('platform:notifications.toast.newMessageFallback'),
        {
          id: `chat-message-${message.id}`,
          icon: '💬',
        });
      }

      // Складываем сообщение в локальный кэш сообщений по chatId.
      setMessages(prev => {
        const chatMsgs = prev[message.chatId] || [];
        if (chatMsgs.some(m => m.id === message.id)) return prev;
        return { ...prev, [message.chatId]: [...chatMsgs, message] };
      });
      // Обновляем превью последнего сообщения и счётчик непрочитанных в списке чатов.
      setChats(prev => {
        const chatExists = prev.some(c => c.id === message.chatId);
        if (!chatExists) {
          // Если пришёл чат, которого локально ещё нет, перечитываем список чатов целиком.
          messengerApi.getChats(accessToken).then(data => {
            setChats(data || []);
            const total = (data || []).reduce((sum, c) => sum + (c.unreadCount || 0), 0);
            setUnreadTotal(total);
            clearMessengerIssue();
          }).catch((error) => {
            reportMessengerIssue('messenger-chatlist-refresh-failed', error);
          });
          return prev;
        }
        const updated = prev.map(c =>
          c.id === message.chatId
            ? {
                ...c,
                lastMessage: { text: message.text, sentAt: message.createdAt, senderId: message.senderId },
                // Счётчик непрочитанных увеличиваем только для неактивного чата.
                unreadCount: activeChatRef.current === message.chatId
                  ? (c.unreadCount || 0)
                  : (c.unreadCount || 0) + 1,
              }
            : c
        );
        const total = updated.reduce((sum, c) => sum + (c.unreadCount || 0), 0);
        setUnreadTotal(total);
        return updated;
      });
    });

    connection.on('ChatUpdated', ({ chatId, lastMessage, unreadCount }) => {
      setChats(prev => {
        const updated = prev.map(c =>
          c.id === chatId ? { ...c, lastMessage, unreadCount } : c
        );
        const total = updated.reduce((sum, c) => sum + (c.unreadCount || 0), 0);
        setUnreadTotal(total);
        return updated;
      });
    });

    connection.on('ChatCleared', ({ chatId }) => {
      if (!chatId) {
        return;
      }

      setMessages((prev) => ({ ...prev, [chatId]: [] }));
      setTypingUsers((prev) => {
        if (!(chatId in prev)) {
          return prev;
        }

        const next = { ...prev };
        delete next[chatId];
        return next;
      });
      setChats((prev) => {
        const updated = prev.map((chat) => (
          chat.id === chatId
            ? {
                ...chat,
                lastMessage: null,
                unreadCount: 0,
              }
            : chat
        ));
        const total = updated.reduce((sum, chat) => sum + (chat.unreadCount || 0), 0);
        setUnreadTotal(total);
        return updated;
      });
    });

    connection.on('UserTyping', ({ chatId, userId }) => {
      if (userId === currentUser?.id) return;
      setTypingUsers(prev => {
        const set = new Set(prev[chatId] || []);
        set.add(userId);
        return { ...prev, [chatId]: set };
      });
      clearTimeout(typingTimeoutsRef.current[`${chatId}-${userId}`]);
      typingTimeoutsRef.current[`${chatId}-${userId}`] = setTimeout(() => {
        setTypingUsers(prev => {
          const set = new Set(prev[chatId] || []);
          set.delete(userId);
          return { ...prev, [chatId]: set };
        });
      }, 3000);
    });

    connection.on('UserStoppedTyping', ({ chatId, userId }) => {
      setTypingUsers(prev => {
        const set = new Set(prev[chatId] || []);
        set.delete(userId);
        return { ...prev, [chatId]: set };
      });
    });

    connection.on('UserOnline', ({ userId }) => {
      setOnlineUsers(prev => ({ ...prev, [userId]: true }));
    });

    connection.on('UserOffline', ({ userId }) => {
      setOnlineUsers(prev => ({ ...prev, [userId]: false }));
    });

    connection.on('ReactionUpdated', (message) => {
      setMessages(prev => {
        const chatMsgs = prev[message.chatId] || [];
        return {
          ...prev,
          [message.chatId]: chatMsgs.map(m => m.id === message.id ? message : m),
        };
      });
    });

    // refreshPresence объявляем до onreconnected, чтобы не ловить temporal dead zone.
    const refreshPresence = async () => {
      try {
        const data = await messengerApi.getChats(accessToken);
        const partnerIds = [];
        (data || []).forEach(c => {
          if (c.members) c.members.forEach(m => { if (m.userId !== currentUser?.id) partnerIds.push(m.userId); });
        });
        if (partnerIds.length > 0) {
          const presenceList = await messengerApi.getPresenceBatch(accessToken, partnerIds);
          if (Array.isArray(presenceList)) {
            const dict = {};
            presenceList.forEach(p => { dict[p.userId] = p.isOnline; });
            setOnlineUsers(dict);
          }
        }
      } catch (error) {
        reportMessengerIssue('messenger-presence-refresh-failed', error);
      }
    };

    connection.onreconnected(async () => {
      for (const chatId of joinedChatsRef.current) {
        try {
          await connection.invoke('JoinChat', chatId);
          clearMessengerIssue();
        } catch (error) {
          reportMessengerIssue('messenger-chat-rejoin-failed', error);
        }
      }
      // После переподключения перечитываем presence, потому что online-state мог устареть.
      void refreshPresence();
    });

    connection.onreconnecting((error) => {
      reportMessengerIssue('messenger-chat-reconnecting', error, {
        silent: true,
        isShuttingDown: chatHubStoppingRef.current,
      });
    });

    connection.onclose((error) => {
      if (!error) {
        return;
      }

      reportMessengerIssue('messenger-chat-closed', error, {
        isShuttingDown: chatHubStoppingRef.current,
      });
    });

    connection.start().then(async () => {
      chatHubRef.current = connection;
      // После старта повторно входим в нужные чаты, если пользователь выбрал их раньше готовности хаба.
      for (const chatId of joinedChatsRef.current) {
        try {
          await connection.invoke('JoinChat', chatId);
          clearMessengerIssue();
        } catch (error) {
          reportMessengerIssue('messenger-chat-join-failed', error);
        }
      }
      // На первом подключении сразу загружаем актуальный presence.
      await refreshPresence();
    }).catch(err => reportMessengerIssue('messenger-chat-connect-failed', err));

    // Heartbeat поддерживает presence и помогает вовремя замечать деградацию соединения.
    const heartbeat = setInterval(() => {
      if (connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke('Heartbeat')
          .then(() => clearMessengerIssue())
          .catch((error) => reportMessengerIssue('messenger-heartbeat-failed', error));
      }
    }, 30000);

    return () => {
      clearInterval(heartbeat);
      chatHubStoppingRef.current = true;
      void connection.stop().catch((error) => {
        reportMessengerIssue('messenger-chat-stop-failed', error, {
          isShuttingDown: true,
        });
      });
      chatHubRef.current = null;
    };
  }, [accessToken, apiBase, clearMessengerIssue, currentUser?.id, reportMessengerIssue]);

  // ─── Call Hub (SignalR) ────────────────────────────────────────────
  useEffect(() => {
    if (!accessToken) {
      callHubStoppingRef.current = true;
      return undefined;
    }

    callHubStoppingRef.current = false;

    const hubUrl = `${apiBase}/calls/hubs/call`;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => accessToken })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.None)
      .build();

    connection.on('IncomingCall', (callSession) => {
      if (!callSession?.id) {
        return;
      }

      if (isCallRecentlyEnded(callSession.id)) {
        return;
      }

      setIncomingCall(callSession);
    });

    connection.on('CallStarted', (callSession) => {
      activeCallSessionRef.current = callSession;
      setActiveCallSession(callSession);
      setCallParticipants(callSession.participants || []);
    });

    connection.on('CallAnswered', (callSession) => {
      activeCallSessionRef.current = callSession;
      setActiveCallSession(callSession);
      setCallParticipants(callSession.participants || []);
      setIncomingCall(null); // После ответа убираем входящий модальный сценарий у ответившего пользователя.
    });

    connection.on('ParticipantJoined', ({ userId, callId, hasVideo, hasAudio }) => {
      setCallParticipants(prev => {
        if (prev.some(p => isSameId(p.userId, userId))) {
          return prev.map(p => isSameId(p.userId, userId) ? { ...p, hasVideo, hasAudio, status: 'connected' } : p);
        }
        return [...prev, { userId, hasVideo, hasAudio, status: 'connected' }];
      });
      // Для нового участника инициатор создаёт WebRTC offer.
      if (userId !== currentUser?.id) {
        createOffer(userId);
      }
    });

    connection.on('ParticipantLeft', ({ userId }) => {
      closePeerConnection(userId);
      setCallParticipants(prev => {
        const updated = prev.filter(p => !isSameId(p.userId, userId));
        // В личном звонке локально завершаем сессию, если собеседник ушёл.
        if (!activeCallSessionRef.current?.isGroup && updated.length <= 1 && activeCallSessionRef.current) {
          showCallNotif(i18n.t('platform:messenger.context.remoteEnded'), 'ended');
          setTimeout(() => cleanupCall({ playEndSound: true }), 0);
        }
        return updated;
      });
    });

    connection.on('ParticipantDeclined', ({ userId }) => {
      showCallNotif(i18n.t('platform:messenger.context.declined'), 'declined');
      setCallParticipants(prev => {
        const updated = prev.filter(p => !isSameId(p.userId, userId));
        // Для личного звонка очищаем состояние, если собеседник отклонил вызов.
        if (!activeCallSessionRef.current?.isGroup && updated.length <= 1 && activeCallSessionRef.current) {
          setTimeout(() => cleanupCall({ playEndSound: true }), 0);
        }
        return updated;
      });
    });

    connection.on('CallEnded', (callSession) => {
      const endedCallId = callSession?.id || activeCallSessionRef.current?.id || incomingCallRef.current?.id;
      markCallAsRecentlyEnded(endedCallId);
      cleanupCall({ playEndSound: true });
    });

    connection.on('ReceiveOffer', async ({ callId, fromUserId, sdp }) => {
      await handleReceiveOffer(fromUserId, sdp);
    });

    connection.on('ReceiveAnswer', async ({ callId, fromUserId, sdp }) => {
      await handleReceiveAnswer(fromUserId, sdp);
    });

    connection.on('ReceiveIceCandidate', async ({ callId, fromUserId, candidate }) => {
      await handleReceiveIceCandidate(fromUserId, candidate);
    });

    connection.on('ParticipantVideoToggled', ({ userId, enabled }) => {
      setCallParticipants(prev => prev.map(p => p.userId === userId ? { ...p, hasVideo: enabled } : p));
    });

    connection.on('ParticipantAudioToggled', ({ userId, enabled }) => {
      setCallParticipants(prev => prev.map(p => p.userId === userId ? { ...p, hasAudio: enabled } : p));
    });

    connection.on('ParticipantScreenShareToggled', ({ userId, enabled }) => {
      setCallParticipants(prev => prev.map(p => p.userId === userId ? { ...p, isScreenSharing: enabled } : p));
    });

    connection.onreconnecting((error) => {
      reportMessengerIssue('messenger-call-reconnecting', error, {
        silent: true,
        isShuttingDown: callHubStoppingRef.current,
      });
    });

    connection.onclose((error) => {
      if (!error) {
        return;
      }

      reportMessengerIssue('messenger-call-closed', error, {
        isShuttingDown: callHubStoppingRef.current,
      });
    });

    connection.start().then(() => {
      callHubRef.current = connection;
      clearMessengerIssue();
    }).catch((error) => reportMessengerIssue('messenger-call-connect-failed', error, {
      isShuttingDown: callHubStoppingRef.current,
    }));

    return () => {
      callHubStoppingRef.current = true;
      void connection.stop().catch((error) => {
        reportMessengerIssue('messenger-call-stop-failed', error, {
          isShuttingDown: true,
        });
      });
      callHubRef.current = null;
    };
  }, [
    accessToken,
    apiBase,
    clearMessengerIssue,
    currentUser?.id,
    isCallRecentlyEnded,
    markCallAsRecentlyEnded,
    reportMessengerIssue,
  ]);

  // ─── Первичная загрузка чатов ─────────────────────────────────────
  const loadChats = useCallback(async () => {
    if (!accessToken) return;
    try {
      setLoading(true);
      const data = await messengerApi.getChats(accessToken);
      setChats(data || []);
      // Считаем общий счётчик непрочитанных по всем чатам.
      const total = (data || []).reduce((sum, c) => sum + (c.unreadCount || 0), 0);
      setUnreadTotal(total);
      // Для приватных чатов отдельно подгружаем online-статусы собеседников.
      const privatePartnerIds = [];
      (data || []).forEach(c => {
        if (c.type === 'private' && c.members) {
          c.members.forEach(m => {
            if (m.userId !== currentUser?.id) privatePartnerIds.push(m.userId);
          });
        }
      });
      if (privatePartnerIds.length > 0) {
        try {
          const presenceList = await messengerApi.getPresenceBatch(accessToken, privatePartnerIds);
          if (Array.isArray(presenceList)) {
            const dict = {};
            presenceList.forEach(p => { dict[p.userId] = p.isOnline; });
            setOnlineUsers(dict);
          }
          clearMessengerIssue();
        } catch (error) {
          reportMessengerIssue('messenger-presence-load-failed', error);
        }
      }
    } catch (err) {
      console.error('[Messenger] failed to load chats', err);
    } finally {
      setLoading(false);
    }
  }, [accessToken, currentUser?.id]);

  useEffect(() => {
    if (!accessToken) return;
    loadChats();
  }, [accessToken]);

  // ─── Действия с чатами ────────────────────────────────────────────
  const removeChatLocally = useCallback((chatId) => {
    if (chatHubRef.current?.state === signalR.HubConnectionState.Connected && joinedChatsRef.current.has(chatId)) {
      chatHubRef.current.invoke('LeaveChat', chatId).catch((error) => {
        reportMessengerIssue('messenger-chat-leave-failed', error);
      });
    }

    joinedChatsRef.current.delete(chatId);
    if (activeChatRef.current === chatId) {
      activeChatRef.current = null;
    }

    Object.keys(typingTimeoutsRef.current).forEach((key) => {
      if (!key.startsWith(`${chatId}-`)) {
        return;
      }

      clearTimeout(typingTimeoutsRef.current[key]);
      delete typingTimeoutsRef.current[key];
    });

    setActiveChat((prev) => (prev === chatId ? null : prev));
    setMessages((prev) => {
      if (!(chatId in prev)) {
        return prev;
      }

      const next = { ...prev };
      delete next[chatId];
      return next;
    });
    setTypingUsers((prev) => {
      if (!(chatId in prev)) {
        return prev;
      }

      const next = { ...prev };
      delete next[chatId];
      return next;
    });
    setChats((prev) => {
      const updated = prev.filter((chat) => chat.id !== chatId);
      const total = updated.reduce((sum, chat) => sum + (chat.unreadCount || 0), 0);
      setUnreadTotal(total);
      return updated;
    });
  }, [reportMessengerIssue]);

  const selectChat = useCallback(async (chatId) => {
    activeChatRef.current = chatId;
    setActiveChat(chatId);

    // Всегда запоминаем желаемый чат, чтобы reconnect-хендлер знал, куда вернуться.
    // Из старых чатов выходим заранее, если соединение уже активно.
    if (chatHubRef.current?.state === signalR.HubConnectionState.Connected) {
      for (const id of joinedChatsRef.current) {
        if (id !== chatId) {
          chatHubRef.current.invoke('LeaveChat', id).catch((error) => {
            reportMessengerIssue('messenger-chat-leave-failed', error);
          });
        }
      }
    }
    joinedChatsRef.current.clear();
    joinedChatsRef.current.add(chatId);

    // Если хаб уже подключён, сразу входим в комнату выбранного чата.
    if (chatHubRef.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await chatHubRef.current.invoke('JoinChat', chatId);
        await chatHubRef.current.invoke('MarkAsRead', chatId);
        clearMessengerIssue();
      } catch (error) {
        reportMessengerIssue('messenger-chat-join-failed', error);
      }
    }

    // Дополнительно подтверждаем прочтение через REST, чтобы состояние не зависело только от хаба.
    messengerApi.markChatAsRead(accessToken, chatId).catch((error) => {
      reportMessengerIssue('messenger-mark-read-failed', error);
    });
    setChats(prev => {
      const updated = prev.map(c => c.id === chatId ? { ...c, unreadCount: 0 } : c);
      const total = updated.reduce((sum, c) => sum + (c.unreadCount || 0), 0);
      setUnreadTotal(total);
      return updated;
    });

    // Если сообщения ещё не закэшированы локально, загружаем их отдельным запросом.
    if (!messages[chatId]) {
      try {
        const msgs = await messengerApi.getMessages(accessToken, chatId);
        setMessages(prev => ({ ...prev, [chatId]: msgs || [] }));
      } catch (err) {
        console.error('[Messenger] failed to load messages', err);
      }
    }
  }, [accessToken, clearMessengerIssue, messages, reportMessengerIssue, updateUnreadTotal]);

  const sendMessage = useCallback(async (chatId, text, replyToMessageId) => {
    if (!accessToken || !text?.trim()) return;
    try {
      const msg = await messengerApi.sendMessage(accessToken, chatId, { text, replyToMessageId });
      setMessages(prev => {
        const chatMsgs = prev[chatId] || [];
        if (chatMsgs.some(m => m.id === msg.id)) return prev;
        return { ...prev, [chatId]: [...chatMsgs, msg] };
      });
      // Обновляем превью последнего сообщения в списке чатов.
      setChats(prev => prev.map(c =>
        c.id === chatId ? { ...c, lastMessage: { messageId: msg.id, text: msg.text, senderId: msg.senderId, sentAt: msg.createdAt } } : c
      ));
      return msg;
    } catch (err) {
      console.error('[Messenger] failed to send message', err);
      throw err;
    }
  }, [accessToken]);

  const loadMoreMessages = useCallback(async (chatId) => {
    const chatMsgs = messages[chatId] || [];
    if (chatMsgs.length === 0) return;
    const oldestId = chatMsgs[0]?.id;
    try {
      const older = await messengerApi.getMessages(accessToken, chatId, { before: oldestId });
      if (older?.length) {
        setMessages(prev => ({ ...prev, [chatId]: [...older, ...(prev[chatId] || [])] }));
      }
      return older?.length || 0;
    } catch (err) {
      console.error('[Messenger] failed to load more messages', err);
      return 0;
    }
  }, [accessToken, messages]);

  const startTyping = useCallback((chatId) => {
    if (chatHubRef.current?.state === signalR.HubConnectionState.Connected) {
      chatHubRef.current.invoke('StartTyping', chatId).catch((error) => {
        reportMessengerIssue('messenger-start-typing-failed', error);
      });
    }
  }, [reportMessengerIssue]);

  const stopTyping = useCallback((chatId) => {
    if (chatHubRef.current?.state === signalR.HubConnectionState.Connected) {
      chatHubRef.current.invoke('StopTyping', chatId).catch((error) => {
        reportMessengerIssue('messenger-stop-typing-failed', error);
      });
    }
  }, [reportMessengerIssue]);

  const handleToggleReaction = useCallback(async (chatId, messageId, emoji) => {
    try {
      await messengerApi.toggleReaction(accessToken, chatId, messageId, emoji);
    } catch (err) {
      console.error('[Messenger] reaction failed', err);
    }
  }, [accessToken]);

  const editMessage = useCallback(async (chatId, messageId, newText) => {
    if (!accessToken || !newText?.trim()) return;
    try {
      await messengerApi.updateMessage(accessToken, chatId, messageId, newText);
      setMessages(prev => {
        const chatMsgs = prev[chatId] || [];
        return {
          ...prev,
          [chatId]: chatMsgs.map(m =>
            m.id === messageId ? { ...m, text: newText, isEdited: true } : m
          ),
        };
      });
    } catch (err) {
      console.error('[Messenger] edit message failed', err);
    }
  }, [accessToken]);

  const removeMessage = useCallback(async (chatId, messageId) => {
    if (!accessToken) return;
    try {
      await messengerApi.deleteMessage(accessToken, chatId, messageId);
      setMessages(prev => {
        const chatMsgs = prev[chatId] || [];
        return { ...prev, [chatId]: chatMsgs.filter(m => m.id !== messageId) };
      });
    } catch (err) {
      console.error('[Messenger] delete message failed', err);
    }
  }, [accessToken]);

  const createChat = useCallback(async (type, data) => {
    if (!accessToken) return;
    let chat;
    if (type === 'private') {
      chat = await messengerApi.createPrivateChat(accessToken, data.participantId);
    } else if (type === 'project') {
      chat = await messengerApi.createProjectChat(accessToken, data);
    } else if (type === 'channel') {
      chat = await messengerApi.createChannel(accessToken, data);
    }
    if (chat) {
      setChats(prev => [chat, ...prev]);
      setActiveChat(chat.id);
    }
    return chat;
  }, [accessToken]);

  const leaveChat = useCallback(async (chatId) => {
    if (!accessToken) return;
    try {
      await messengerApi.leaveChat(accessToken, chatId);
      clearMessengerIssue();
      removeChatLocally(chatId);
    } catch (err) {
      console.error('[Messenger] failed to leave chat', err);
      reportMessengerIssue('messenger-leave-chat-failed', err);
      if (err?.status === 404) {
        removeChatLocally(chatId);
      }
    }
  }, [accessToken, clearMessengerIssue, removeChatLocally, reportMessengerIssue]);

  const deleteChat = useCallback(async (chatId) => {
    if (!accessToken) return;
    try {
      await messengerApi.deleteChat(accessToken, chatId);
      clearMessengerIssue();
      removeChatLocally(chatId);
    } catch (err) {
      console.error('[Messenger] failed to delete chat', err);
      reportMessengerIssue('messenger-delete-chat-failed', err);
      if (err?.status === 404) {
        removeChatLocally(chatId);
      }
    }
  }, [accessToken, clearMessengerIssue, removeChatLocally, reportMessengerIssue]);

  const clearChat = useCallback(async (chatId) => {
    if (!accessToken) return;
    try {
      await messengerApi.clearChat(accessToken, chatId);
      clearMessengerIssue();

      setMessages((prev) => ({ ...prev, [chatId]: [] }));
      setTypingUsers((prev) => {
        if (!(chatId in prev)) {
          return prev;
        }

        const next = { ...prev };
        delete next[chatId];
        return next;
      });

      setChats((prev) => {
        const updated = prev.map((chat) => (
          chat.id === chatId
            ? {
                ...chat,
                lastMessage: null,
                unreadCount: 0,
                updatedAt: new Date().toISOString(),
              }
            : chat
        ));
        const total = updated.reduce((sum, chat) => sum + (chat.unreadCount || 0), 0);
        setUnreadTotal(total);
        return updated;
      });
    } catch (err) {
      console.error('[Messenger] failed to clear chat', err);
      reportMessengerIssue('messenger-clear-chat-failed', err);
      if (err?.status === 404) {
        removeChatLocally(chatId);
      }
    }
  }, [accessToken, clearMessengerIssue, removeChatLocally, reportMessengerIssue]);

  // ─── Call actions ─────────────────────────────────────────────────
  const startCall = useCallback(async (participantIds, type = 'audio', chatId = null) => {
    if (!accessToken) return;
    let stream;
    try {
      // Сначала запрашиваем media с теми ограничениями, которые нужны сценарию звонка.
      stream = await navigator.mediaDevices.getUserMedia({
        audio: true,
        video: type === 'video' ? { width: { ideal: 1280 }, height: { ideal: 720 } } : false,
      });
    } catch (mediaErr) {
      console.warn('[Call] getUserMedia failed, retrying audio-only', mediaErr);
      if (type === 'video') {
        // Если камера недоступна, пытаемся деградировать сценарий до audio-only.
        try {
          stream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
          showCallNotif(i18n.t('platform:messenger.context.cameraUnavailableAudio'), 'info');
        } catch (audioErr) {
          console.error('[Call] audio fallback also failed', audioErr);
          if (audioErr.name === 'NotAllowedError') {
            showCallNotif(i18n.t('platform:messenger.context.microphonePermission'), 'error');
          } else {
            showCallNotif(i18n.t('platform:messenger.context.microphoneMissing'), 'error');
          }
          return;
        }
      } else {
        if (mediaErr.name === 'NotAllowedError') {
          showCallNotif(i18n.t('platform:messenger.context.microphonePermission'), 'error');
        } else {
          showCallNotif(i18n.t('platform:messenger.context.microphoneMissing'), 'error');
        }
        return;
      }
    }
    try {
      localStreamRef.current = stream;
      setLocalStream(stream);
      const callHub = callHubRef.current;
      if (callHub?.state !== signalR.HubConnectionState.Connected) {
        stream?.getTracks().forEach((track) => track.stop());
        localStreamRef.current = null;
        setLocalStream(null);
        showCallNotif(i18n.t('platform:messenger.context.connectFailed'), 'error');
        return;
      }

      // Через SignalR инициатор сразу попадает в call-группу и получает realtime-события завершения/отклонения.
      await callHub.invoke('StartCall', {
        chatId: chatId || null,
        type: type || 'audio',
        participantIds,
      });
      // Итоговое состояние сессии выставит обработчик события CallStarted.
    } catch (err) {
      console.error('[Call] failed to start session', err);
      stream?.getTracks().forEach(t => t.stop());
      localStreamRef.current = null;
      setLocalStream(null);
      showCallNotif(i18n.t('platform:messenger.context.connectFailed'), 'error');
    }
  }, [accessToken, showCallNotif]);

  const answerCall = useCallback(async (callId, hasVideo = false) => {
    if (!accessToken) return false;
    let stream;
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true, video: hasVideo });
    } catch (mediaErr) {
      console.warn('[Call] getUserMedia failed on answer, retrying audio-only', mediaErr);
      if (hasVideo) {
        try {
          stream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
          showCallNotif(i18n.t('platform:messenger.context.cameraUnavailableAudio'), 'info');
        } catch (audioErr) {
          console.error('[Call] answer audio fallback failed', audioErr);
          if (audioErr.name === 'NotAllowedError') {
            showCallNotif(i18n.t('platform:messenger.context.microphonePermission'), 'error');
          } else {
            showCallNotif(i18n.t('platform:messenger.context.microphoneMissing'), 'error');
          }
          setIncomingCall(null);
          return false;
        }
      } else {
        if (mediaErr.name === 'NotAllowedError') {
          showCallNotif(i18n.t('platform:messenger.context.microphonePermission'), 'error');
        } else {
          showCallNotif(i18n.t('platform:messenger.context.microphoneMissing'), 'error');
        }
        setIncomingCall(null);
        return false;
      }
    }
    try {
      localStreamRef.current = stream;
      setLocalStream(stream);
      const actualHasVideo = stream.getVideoTracks().length > 0;
      const callHub = callHubRef.current;
      if (callHub?.state !== signalR.HubConnectionState.Connected) {
        stream?.getTracks().forEach((track) => track.stop());
        localStreamRef.current = null;
        setLocalStream(null);
        setIncomingCall(null);
        showCallNotif(i18n.t('platform:messenger.context.connectFailed'), 'error');
        return false;
      }

      // Через SignalR отвечающий сразу входит в call-группу и начинает получать media-события.
      await callHub.invoke('AnswerCall', callId, {
        hasVideo: actualHasVideo,
        hasAudio: true,
      });
      // Состояние сессии и закрытие входящего модального окна обработает CallAnswered.
      setIsPiP(false);
      return true;
    } catch (err) {
      console.error('[Call] failed to answer session', err);
      stream?.getTracks().forEach(t => t.stop());
      localStreamRef.current = null;
      setLocalStream(null);
      setIncomingCall(null);
      showCallNotif(i18n.t('platform:messenger.context.connectFailed'), 'error');
      return false;
    }
  }, [accessToken, showCallNotif]);

  const closePeerConnection = useCallback((userId) => {
    const pc = peerConnectionsRef.current[userId];
    if (pc) {
      pc.close();
      delete peerConnectionsRef.current[userId];
    }

    delete remoteMediaStreamsRef.current[userId];

    setRemoteStreams(prev => {
      const next = { ...prev };
      delete next[userId];
      return next;
    });
  }, []);

  const cleanupCall = useCallback(({ playEndSound = false } = {}) => {
    const hadCallState = Boolean(
      activeCallSessionRef.current
      || incomingCallRef.current
      || localStreamRef.current
      || Object.keys(remoteMediaStreamsRef.current).length > 0,
    );

    if (playEndSound && hadCallState) {
      void playSoundEffect('callEnded', { volume: 0.9 });
    }

    Object.keys(peerConnectionsRef.current).forEach(id => closePeerConnection(id));
    const stream = localStreamRef.current;
    stream?.getTracks().forEach(t => t.stop());
    localStreamRef.current = null;
    activeCallSessionRef.current = null;
    incomingCallRef.current = null;
    remoteMediaStreamsRef.current = {};
    setLocalStream(null);
    setRemoteStreams({});
    setActiveCallSession(null);
    setCallParticipants([]);
    setIsScreenSharing(false);
    setIsPiP(false);
    setIncomingCall(null);
  }, [closePeerConnection]);

  const declineCall = useCallback(async (callId) => {
    try {
      // Используем SignalR, чтобы остальные участники сразу увидели отказ от звонка.
      if (callHubRef.current?.state === signalR.HubConnectionState.Connected) {
        await callHubRef.current.invoke('DeclineCall', callId);
      } else {
        await callsApi.declineCall(accessToken, callId);
      }
      setIncomingCall(null);
    } catch (err) {
      console.error('[Call] failed to decline', err);
      setIncomingCall(null);
    }
  }, [accessToken]);

  const endCall = useCallback(async () => {
    const session = activeCallSessionRef.current;
    if (!session) return;
    try {
      // Используем SignalR, чтобы остальные участники мгновенно получили CallEnded.
      if (callHubRef.current?.state === signalR.HubConnectionState.Connected) {
        await callHubRef.current.invoke('EndCall', session.id);
      } else {
        await callsApi.endCall(accessToken, session.id);
      }
      clearMessengerIssue();
    } catch (error) {
      reportMessengerIssue('messenger-end-call-failed', error);
    }
    markCallAsRecentlyEnded(session.id);
    cleanupCall({ playEndSound: true });
  }, [accessToken, cleanupCall, clearMessengerIssue, markCallAsRecentlyEnded, reportMessengerIssue]);

  const leaveCall = useCallback(async () => {
    const session = activeCallSessionRef.current;
    if (!session) return;
    try {
      // Используем SignalR, чтобы остальные участники мгновенно получили ParticipantLeft.
      if (callHubRef.current?.state === signalR.HubConnectionState.Connected) {
        await callHubRef.current.invoke('LeaveCall', session.id);
      } else {
        await callsApi.leaveCall(accessToken, session.id);
      }
      clearMessengerIssue();
    } catch (error) {
      reportMessengerIssue('messenger-leave-call-failed', error);
    }
    if (!session.isGroup) {
      markCallAsRecentlyEnded(session.id);
    }
    cleanupCall({ playEndSound: true });
  }, [accessToken, cleanupCall, clearMessengerIssue, markCallAsRecentlyEnded, reportMessengerIssue]);

  const renegotiatePeerConnection = useCallback(async (remoteUserId, pc) => {
    const session = activeCallSessionRef.current;
    if (!session || !pc || callHubRef.current?.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    if (pc.signalingState !== 'stable') {
      return;
    }

    try {
      const offer = await pc.createOffer();
      await pc.setLocalDescription(offer);
      await callHubRef.current.invoke('SendOffer', session.id, remoteUserId, offer);
    } catch (error) {
      reportMessengerIssue('messenger-webrtc-renegotiation-failed', error);
    }
  }, [reportMessengerIssue]);

  const toggleVideo = useCallback(async () => {
    if (!localStream) return;
    const videoTrack = localStream.getVideoTracks()[0];
    if (videoTrack) {
      videoTrack.enabled = !videoTrack.enabled;
      const session = activeCallSessionRef.current;
      if (callHubRef.current?.state === signalR.HubConnectionState.Connected && session) {
        callHubRef.current.invoke('ToggleVideo', session.id, videoTrack.enabled).catch((error) => {
          reportMessengerIssue('messenger-toggle-video-failed', error);
        });
      }
      // Создаём новый объект MediaStream, чтобы React гарантированно перерисовал видеоэлементы.
      const newStream = new MediaStream(localStream.getTracks());
      localStreamRef.current = newStream;
      setLocalStream(newStream);
      void playSoundEffect(videoTrack.enabled ? 'cameraOn' : 'cameraOff', { volume: 0.85 });
    } else {
      // Если видео-трека ещё нет, пытаемся добавить камеру в текущую сессию.
      try {
        const videoStream = await navigator.mediaDevices.getUserMedia({ video: { width: { ideal: 1280 }, height: { ideal: 720 } } });
        const newTrack = videoStream.getVideoTracks()[0];
        localStream.addTrack(newTrack);
        // Новый видеотрек нужно заменить во всех активных peer connections.
        const renegotiationTasks = [];
        Object.entries(peerConnectionsRef.current).forEach(([remoteUserId, pc]) => {
          const sender = pc.getSenders().find(s => s.track?.kind === 'video');
          if (sender) sender.replaceTrack(newTrack);
          else {
            pc.addTrack(newTrack, localStream);
            renegotiationTasks.push(renegotiatePeerConnection(remoteUserId, pc));
          }
        });
        if (renegotiationTasks.length > 0) {
          await Promise.all(renegotiationTasks);
        }
        const session = activeCallSessionRef.current;
        if (callHubRef.current?.state === signalR.HubConnectionState.Connected && session) {
          callHubRef.current.invoke('ToggleVideo', session.id, true).catch((error) => {
            reportMessengerIssue('messenger-toggle-video-failed', error);
          });
        }
        // Новый MediaStream нужен, чтобы UI увидел обновление и перерисовался.
        const newStream = new MediaStream(localStream.getTracks());
        localStreamRef.current = newStream;
        setLocalStream(newStream);
        void playSoundEffect('cameraOn', { volume: 0.85 });
      } catch (err) {
        console.error('[Call] failed to enable camera', err);
      }
    }
  }, [localStream, reportMessengerIssue]);

  const toggleAudio = useCallback(() => {
    const stream = localStreamRef.current;
    if (!stream) return;
    const audioTrack = stream.getAudioTracks()[0];
    if (audioTrack) {
      audioTrack.enabled = !audioTrack.enabled;
      const session = activeCallSessionRef.current;
      if (callHubRef.current?.state === signalR.HubConnectionState.Connected && session) {
        callHubRef.current.invoke('ToggleAudio', session.id, audioTrack.enabled).catch((error) => {
          reportMessengerIssue('messenger-toggle-audio-failed', error);
        });
      }
      // Новый MediaStream заставляет UI обновить состояние микрофона.
      const newStream = new MediaStream(stream.getTracks());
      localStreamRef.current = newStream;
      setLocalStream(newStream);
      if (!audioTrack.enabled) {
        void playSoundEffect('micMuted', { volume: 0.85 });
      } else {
        void playSoundEffect('micUnmuted', { volume: 0.85 });
      }
    }
  }, [reportMessengerIssue]);

  const toggleScreenShare = useCallback(async () => {
    const session = activeCallSessionRef.current;
    if (!session) return;

    if (isScreenSharing) {
      // При остановке screen share берём поток из ref, чтобы не попасть в устаревшее замыкание.
      const stream = localStreamRef.current;
      stream?.getVideoTracks().forEach(t => {
        if (t.label.includes('screen') || t.label.includes('monitor') || t.label.includes('window')) {
          t.stop();
          stream.removeTrack(t);
        }
      });
      // Для видео-звонка после screen share пытаемся вернуть камеру.
      if (session.type === 'video') {
        try {
          const camStream = await navigator.mediaDevices.getUserMedia({ video: true });
          const camTrack = camStream.getVideoTracks()[0];
          stream?.addTrack(camTrack);
          Object.values(peerConnectionsRef.current).forEach(pc => {
            const sender = pc.getSenders().find(s => s.track?.kind === 'video');
            if (sender) sender.replaceTrack(camTrack);
          });
        } catch (error) {
          reportMessengerIssue('messenger-camera-restore-failed', error);
        }
      }
      // Создаём новый поток, чтобы React увидел обновлённое media-состояние.
      const newStream = new MediaStream(localStreamRef.current?.getTracks() || []);
      localStreamRef.current = newStream;
      setLocalStream(newStream);
      setIsScreenSharing(false);
      callHubRef.current?.invoke('ToggleScreenShare', session.id, false).catch((error) => {
        reportMessengerIssue('messenger-screen-share-toggle-failed', error);
      });
    } else {
      // Запускаем трансляцию экрана.
      try {
        const displayStream = await navigator.mediaDevices.getDisplayMedia({
          video: { cursor: 'always' }, audio: true,
        });
        const screenTrack = displayStream.getVideoTracks()[0];

        // Экранный видеотрек заменяет обычный camera track во всех соединениях.
        const currentVideoTrack = localStream?.getVideoTracks()[0];
        if (currentVideoTrack) {
          localStream.removeTrack(currentVideoTrack);
          currentVideoTrack.stop();
        }
        localStream?.addTrack(screenTrack);

        const renegotiationTasks = [];
        Object.entries(peerConnectionsRef.current).forEach(([remoteUserId, pc]) => {
          const sender = pc.getSenders().find(s => s.track?.kind === 'video');
          if (sender) sender.replaceTrack(screenTrack);
          else {
            pc.addTrack(screenTrack, localStream);
            renegotiationTasks.push(renegotiatePeerConnection(remoteUserId, pc));
          }
        });
        if (renegotiationTasks.length > 0) {
          await Promise.all(renegotiationTasks);
        }

        screenTrack.onended = () => {
          toggleScreenShare(); // Автоматически выключаем шаринг, когда пользователь нажимает Stop sharing.
        };

        setIsScreenSharing(true);
        callHubRef.current?.invoke('ToggleScreenShare', session.id, true).catch((error) => {
          reportMessengerIssue('messenger-screen-share-toggle-failed', error);
        });
      } catch (err) {
        console.error('[Call] screen share error', err);
      }
    }
  }, [isScreenSharing, localStream, renegotiatePeerConnection, reportMessengerIssue]);

  // ─── WebRTC ───────────────────────────────────────────────────────
  const ICE_SERVERS = {
    iceServers: [
      { urls: 'stun:stun.l.google.com:19302' },
      { urls: 'stun:stun1.l.google.com:19302' },
    ],
  };

  const createPeerConnection = useCallback((remoteUserId) => {
    const existing = peerConnectionsRef.current[remoteUserId];
    if (existing && existing.connectionState !== 'closed') {
      return existing;
    }

    const pc = new RTCPeerConnection(ICE_SERVERS);

    // Берём поток из ref, чтобы избежать устаревшего замыкания при долгоживущих callbacks.
    const stream = localStreamRef.current;
    if (stream) {
      stream.getTracks().forEach(track => pc.addTrack(track, stream));
    }

    pc.ontrack = (event) => {
      const receivedStream = event.streams?.[0];
      const previousStream = remoteMediaStreamsRef.current[remoteUserId];
      const targetStream = receivedStream || previousStream || new MediaStream();

      if (event.track && !targetStream.getTracks().some((track) => track.id === event.track.id)) {
        targetStream.addTrack(event.track);
      }

      remoteMediaStreamsRef.current[remoteUserId] = targetStream;
      setRemoteStreams(prev => ({ ...prev, [remoteUserId]: targetStream }));
    };

    pc.onicecandidate = (event) => {
      const session = activeCallSessionRef.current;
      if (event.candidate && callHubRef.current?.state === signalR.HubConnectionState.Connected && session) {
        callHubRef.current.invoke('SendIceCandidate', session.id, remoteUserId, event.candidate).catch((error) => {
          reportMessengerIssue('messenger-ice-candidate-send-failed', error);
        });
      }
    };

    pc.onconnectionstatechange = () => {
      if (pc.connectionState === 'disconnected' || pc.connectionState === 'failed') {
        closePeerConnection(remoteUserId);
      }
    };

    peerConnectionsRef.current[remoteUserId] = pc;
    return pc;
  }, [closePeerConnection, reportMessengerIssue]);

  const createOffer = useCallback(async (remoteUserId) => {
    const pc = createPeerConnection(remoteUserId);
    const offer = await pc.createOffer();
    await pc.setLocalDescription(offer);
    const session = activeCallSessionRef.current;
    if (callHubRef.current?.state === signalR.HubConnectionState.Connected && session) {
      callHubRef.current.invoke('SendOffer', session.id, remoteUserId, offer).catch((error) => {
        reportMessengerIssue('messenger-webrtc-offer-failed', error);
      });
    }
  }, [createPeerConnection, reportMessengerIssue]);

  const handleReceiveOffer = useCallback(async (fromUserId, sdp) => {
    const pc = createPeerConnection(fromUserId);
    await pc.setRemoteDescription(new RTCSessionDescription(sdp));
    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    const session = activeCallSessionRef.current;
    if (callHubRef.current?.state === signalR.HubConnectionState.Connected && session) {
      callHubRef.current.invoke('SendAnswer', session.id, fromUserId, answer).catch((error) => {
        reportMessengerIssue('messenger-webrtc-answer-failed', error);
      });
    }
  }, [createPeerConnection, reportMessengerIssue]);

  const handleReceiveAnswer = useCallback(async (fromUserId, sdp) => {
    const pc = peerConnectionsRef.current[fromUserId];
    if (pc) await pc.setRemoteDescription(new RTCSessionDescription(sdp));
  }, []);

  const handleReceiveIceCandidate = useCallback(async (fromUserId, candidate) => {
    const pc = peerConnectionsRef.current[fromUserId];
    if (pc) await pc.addIceCandidate(new RTCIceCandidate(candidate));
  }, []);

  // ─── Значение контекста для всего мессенджер-слоя ─────────────────
  const value = {
    // состояние чатов
    chats, activeChat, messages, typingUsers, onlineUsers, unreadTotal, loading, messengerOpen,
    // действия с чатами
    selectChat, sendMessage, loadMoreMessages, startTyping, stopTyping, handleToggleReaction, editMessage, removeMessage, createChat, loadChats, leaveChat, clearChat, deleteChat,
    setMessengerOpen,
    // состояние звонков
    activeCallSession, incomingCall, callParticipants, localStream, remoteStreams, isScreenSharing, isPiP,
    callNotif, dismissCallNotif,
    // действия со звонками
    startCall, answerCall, declineCall, endCall, leaveCall, toggleVideo, toggleAudio, toggleScreenShare, setIsPiP,
  };

  return (
    <MessengerContext.Provider value={value}>
      {children}
    </MessengerContext.Provider>
  );
};
