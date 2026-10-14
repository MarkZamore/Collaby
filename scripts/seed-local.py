"""Идемпотентное создание демоданных через публичные API и Mailpit."""
import html
import json
import os
import re
import time
from pathlib import Path
from urllib.parse import parse_qs, urlsplit
import requests


def checked(response, operation):
    if not response.ok:
        # Ответы могут содержать токены, поэтому диагностика содержит только статус.
        raise RuntimeError(f'{operation}: HTTP {response.status_code}')
    return response.json() if response.content else None


def mail_tokens(mail, email, route):
    messages = checked(requests.get(mail + '/api/v1/messages?limit=200', timeout=15), 'Mailpit messages')
    for message in messages.get('messages', []):
        if not any(r.get('Address', '').lower() == email.lower() for r in message.get('To', [])):
            continue
        detail = checked(requests.get(mail + '/api/v1/message/' + message['ID'], timeout=15), 'Mailpit message')
        body = html.unescape(detail.get('HTML', '') + '\n' + detail.get('Text', ''))
        for link in re.findall(r'https?://[^\s"\'<>]+', body):
            parsed = urlsplit(link)
            if parsed.path.rstrip('/') == route:
                yield from parse_qs(parsed.query).get('token', [])


def user_session(base, mail, email, nickname, password):
    session = requests.Session()
    payload = {'emailOrUsername': email, 'password': password}
    login = session.post(base + '/api/auth/login', json=payload, timeout=30)
    if login.status_code != 200:
        registered = session.post(base + '/api/auth/register', json={'email': email, 'nickname': nickname, 'password': password, 'confirmPassword': password}, timeout=30)
        if registered.status_code not in (200, 201, 400, 409):
            checked(registered, 'Register demo user')
        if registered.status_code in (400, 409):
            resend = session.post(base + '/api/auth/resend-verification-email', json={'email': email}, timeout=30)
            if not resend.ok and resend.json().get('code') != 'email_already_verified':
                checked(resend, 'Resend verification')
        for _ in range(30):
            for token in mail_tokens(mail, email, '/verify-email'):
                if session.post(base + '/api/auth/verify-email', json={'token': token}, timeout=30).ok:
                    break
            login = session.post(base + '/api/auth/login', json=payload, timeout=30)
            if login.ok:
                break
            time.sleep(2)
    result = checked(login, 'Login demo user')
    session.headers['Authorization'] = 'Bearer ' + result['accessToken']
    return session, result['user']


def seed(config):
    base = os.getenv('COLLABY_SEED_URL', 'http://frontend.collaby').rstrip('/')
    mail = os.getenv('COLLABY_MAIL_URL', 'http://mailpit.collaby:8025').rstrip('/')

    def call(session, method, path, body=None):
        return checked(session.request(method, base + path, json=body, timeout=30), method + ' demo API')

    admin, admin_user = user_session(base, mail, 'admin@collaby.local', 'CollabyAdmin', config['DEMO_PASSWORD'])
    tester, tester_user = user_session(base, mail, 'tester@collaby.local', 'CollabyTester', config['DEMO_PASSWORD'])
    if not admin_user.get('isSystemAdmin'):
        raise RuntimeError('SYSTEM_ADMIN_EMAILS must include admin@collaby.local')
    workspaces = call(admin, 'GET', '/api/management/workspaces')
    workspace = next((w for w in workspaces if w['name'] == 'Collaby Demo'), None)
    if workspace is None:
        workspace = call(admin, 'POST', '/api/management/workspaces', {'name': 'Collaby Demo', 'description': 'Локальное тестирование Collaby'})
    wid = workspace['id']
    members = call(admin, 'GET', f'/api/management/workspaces/{wid}/members')
    tester_id = tester_user['id']
    if not any(m.get('userId') == tester_id for m in members):
        pending = call(admin, 'GET', f'/api/management/workspaces/{wid}/invitations')
        pending = [i for i in pending if i['email'].lower() == 'tester@collaby.local' and i['status'] == 'Pending']
        accepted = False
        # Продолжаем прерванное принятие уже доставленного приглашения.
        for token in mail_tokens(mail, 'tester@collaby.local', '/accept-invitation'):
            preview = tester.get(base + '/api/management/invitations/' + token, timeout=30)
            if preview.ok and preview.json().get('workspaceId') == wid and preview.json().get('canAccept'):
                call(tester, 'POST', '/api/management/invitations/accept', {'token': token})
                accepted = True
                break
        if not accepted:
            for invitation in pending:
                call(admin, 'DELETE', f'/api/management/workspaces/{wid}/invitations/{invitation["id"]}')
            invite = call(admin, 'POST', f'/api/management/workspaces/{wid}/invitations', {'email': 'tester@collaby.local', 'role': 'Member', 'expiresInDays': 7})
            for _ in range(30):
                if invite['token'] in set(mail_tokens(mail, 'tester@collaby.local', '/accept-invitation')):
                    call(tester, 'POST', '/api/management/invitations/accept', {'token': invite['token']})
                    accepted = True
                    break
                time.sleep(2)
            if not accepted:
                raise RuntimeError('Invitation email did not arrive in Mailpit')
    projects = call(admin, 'GET', f'/api/management/workspaces/{wid}/projects')
    project = next((p for p in projects if p['name'] == 'Локальный запуск'), None)
    if project is None:
        project = call(admin, 'POST', f'/api/management/workspaces/{wid}/projects', {'name': 'Локальный запуск', 'description': 'Задачи, комментарии и realtime'})
    pid = project['id']
    tasks = call(admin, 'GET', f'/api/management/projects/{pid}/tasks')
    for title in ['Проверить локальный сайт', 'Проверить чат и звонок', 'Проверить магазин и награды']:
        if not any(t['title'] == title for t in tasks):
            call(admin, 'POST', f'/api/management/projects/{pid}/tasks', {'title': title, 'description': 'Демонстрационная задача', 'priority': 1, 'assigneeId': tester_id})
    refreshed = call(admin, 'POST', '/api/auth/refresh', {})
    admin.headers['Authorization'] = 'Bearer ' + refreshed['accessToken']
    call(admin, 'POST', '/api/auth/logout', {})
    print('Демоданные, подтверждение почты, приглашения и refresh/logout готовы.')


def portainer(config):
    base = os.getenv('PORTAINER_URL', 'http://portainer.portainer:9000').rstrip('/')
    session = requests.Session()
    credentials = {'Username': 'admin', 'Password': config['PANEL_PASSWORD']}
    result = session.post(base + '/api/auth', json=credentials, timeout=30)
    if result.status_code != 200:
        initialized = session.post(base + '/api/users/admin/init', json=credentials, timeout=30)
        if not initialized.ok:
            raise RuntimeError('Portainer admin init failed: check retained PVC credentials or first-start timeout')
        result = session.post(base + '/api/auth', json=credentials, timeout=30)
    session.headers['Authorization'] = 'Bearer ' + checked(result, 'Portainer auth')['jwt']
    endpoints = checked(session.get(base + '/api/endpoints', timeout=30), 'Portainer endpoints')
    endpoint = next((e for e in endpoints if e.get('Type') == 5 and e.get('URL') == 'https://kubernetes.default.svc'), None)
    if endpoint is None:
        if any(e['Name'] == 'Collaby Kubernetes' for e in endpoints):
            raise RuntimeError('Portainer Collaby Kubernetes has an unexpected endpoint type or URL')
        # Local Kubernetes использует native InClusterConfig с projected CA/token.
        # TLS-поля Portainer относятся к внешним Docker/Agent подключениям.
        fields = {'Name': 'Collaby Kubernetes', 'EndpointCreationType': '5', 'URL': 'https://kubernetes.default.svc', 'TLS': 'false'}
        endpoint = checked(session.post(base + '/api/endpoints', files={key: (None, value) for key, value in fields.items()}, timeout=60), 'Portainer Kubernetes endpoint')
    checked(session.get(base + f'/api/endpoints/{endpoint["Id"]}/kubernetes/api/v1/namespaces', timeout=30), 'Portainer Kubernetes API')
    print('Portainer: обычная авторизация и доступ к Kubernetes готовы.')


if __name__ == '__main__':
    config = json.loads(Path(os.environ['COLLABY_SEED_CONFIG']).read_text())
    seed(config)
    portainer(config)
