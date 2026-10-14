"""Приёмочная проверка живого локального API без прямого изменения БД."""
import argparse, json, secrets, time
from pathlib import Path
from urllib.parse import parse_qs, urlsplit
import requests

def checked(response, operation):
    if not response.ok:
        raise RuntimeError(f'{operation}: HTTP {response.status_code}')
    return response.json() if response.content else None

ROOT = Path(__file__).resolve().parents[1]

def run(base, config):
    evidence = {}
    qa = requests.Session()
    suffix = secrets.token_hex(4)
    email = f'qa-{suffix}@collaby.local'
    password = secrets.token_hex(16) + 'Aa1!'
    mail = requests.Session()
    mail.auth = ('admin', config['PANEL_PASSWORD'])
    def api(method, path, body=None, session=qa):
        response = session.request(method, base + path, json=body, timeout=120)
        return checked(response, method + ' ' + path)
    def token_from_mail(route):
        for _ in range(30):
            messages = checked(mail.get('http://localhost:8025/api/v1/messages?limit=200', timeout=15), 'Почта')
            for message in messages.get('messages', []):
                if not any(r.get('Address', '').lower() == email for r in message.get('To', [])): continue
                detail = checked(mail.get('http://localhost:8025/api/v1/message/' + message['ID'], timeout=15), 'Письмо')
                import html, re
                for link in re.findall(r'https?://[^\s"\'<>]+', html.unescape(detail.get('HTML', '') + '\n' + detail.get('Text', ''))):
                    parsed = urlsplit(link)
                    if parsed.path == route:
                        assert parsed.scheme + '://' + parsed.netloc == 'http://localhost:8080'
                        values = parse_qs(parsed.query).get('token', [])
                        if values: return values[0]
            time.sleep(1)
        raise RuntimeError('Ожидаемое письмо не получено.')
    api('POST', '/api/auth/register', {'email':email,'nickname':'QA'+suffix,'password':password,'confirmPassword':password})
    response = qa.post(base + '/api/auth/login', json={'emailOrUsername':email,'password':password}, timeout=30)
    assert response.status_code != 200, 'Вход разрешён до подтверждения почты.'
    verification = token_from_mail('/verify-email')
    api('POST', '/api/auth/verify-email', {'token':verification})
    login = api('POST', '/api/auth/login', {'emailOrUsername':email,'password':password})
    user_id = login['user']['id']
    qa.headers['Authorization'] = 'Bearer ' + login['accessToken']
    cookie = next(c for c in qa.cookies if 'refresh' in c.name.lower())
    assert cookie.path == '/' and not cookie.secure
    refreshed = api('POST', '/api/auth/refresh', {})
    assert refreshed['accessToken']
    api('POST', '/api/auth/logout', {})
    assert qa.post(base + '/api/auth/refresh', json={}, timeout=30).status_code != 200
    api('POST', '/api/auth/forgot-password', {'email':email})
    reset = token_from_mail('/reset-password')
    api('POST', '/api/auth/validate-reset-password-token', {'email':email,'token':reset})
    password += 'B2!'
    api('POST', '/api/auth/reset-password', {'email':email,'token':reset,'newPassword':password,'confirmPassword':password})
    assert qa.post(base + '/api/auth/validate-reset-password-token', json={'email':email,'token':reset}, timeout=30).status_code != 200
    login = api('POST', '/api/auth/login', {'emailOrUsername':email,'password':password})
    qa.headers['Authorization'] = 'Bearer ' + login['accessToken']
    evidence['registration_confirmation_reset_login_refresh_logout'] = True
    workspace = api('POST','/api/management/workspaces',{'name':'Проверки Collaby '+suffix})
    wid = workspace['id']
    project = api('POST',f'/api/management/workspaces/{wid}/projects',{'name':'Приёмочная проверка'})
    pid = project['id']
    statuses = api('GET',f'/api/management/projects/{pid}/statuses')
    done = next(s for s in statuses if s.get('isFinal'))
    task = api('POST',f'/api/management/projects/{pid}/tasks',{'title':'Проверить локальную работу','assigneeId':user_id,'priority':1})
    tid = task['id']
    comment = api('POST',f'/api/management/tasks/{tid}/comments',{'text':'Проверка настоящего API и Kafka.'})
    assert comment.get('id')
    api('PUT',f'/api/management/tasks/{tid}/move',{'statusId':done['id'],'order':0})
    overview = api('GET',f'/api/analytics/projects/{pid}/overview?period=week')
    assert overview
    evidence['projects_tasks_comments_analytics'] = True
    items = api('GET','/api/gamification/shop')
    item = min((i for i in items if not i['isOwned'] and i['price'] > 0), key=lambda i:i['price'])
    for count in range(25):
        xp = api('GET','/api/gamification/xp/global')
        if xp['coins'] >= item['price']: break
        if count:
            task = api('POST',f'/api/management/projects/{pid}/tasks',{'title':f'Заработать монеты {count}','assigneeId':user_id,'priority':1})
            api('PUT',f'/api/management/tasks/{task["id"]}/move',{'statusId':done['id'],'order':count})
        time.sleep(2)
    else: raise RuntimeError('Kafka не начислил достаточно монет за завершение задач.')
    before = xp['coins']
    assert before > 0
    api('POST','/api/gamification/shop/buy',{'itemId':item['id']})
    inventory = api('GET','/api/gamification/shop/inventory')
    assert any(i['itemId'] == item['id'] for i in inventory)
    assert api('GET','/api/gamification/xp/global')['coins'] == before-item['price']
    evidence['earned_coins_and_shop_purchase'] = True
    evidence['workspace_id'] = wid
    evidence['project_id'] = pid
    return evidence

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--base',default='http://localhost:8080')
    args = parser.parse_args()
    config = json.loads((ROOT/'.local/rendered/local.json').read_text(encoding='utf-8'))
    result = run(args.base, config)
    (ROOT/'.local/api-acceptance.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Живой API: почта, auth, проекты, задачи, комментарии, аналитика, монеты и магазин проверены.')
