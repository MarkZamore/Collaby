"""Проверки повторного запуска и восстановления seed через API-контракт."""
import importlib.util
import unittest
from pathlib import Path
from unittest.mock import MagicMock, patch

spec = importlib.util.spec_from_file_location('seed_local', Path(__file__).parents[1] / 'seed-local.py')
seed = importlib.util.module_from_spec(spec)
spec.loader.exec_module(seed)


def response(value=None, status=200):
    result = MagicMock()
    result.status_code = status
    result.ok = status < 400
    result.content = b'json' if value is not None else b''
    result.json.return_value = value
    return result


class DemoApi:
    def __init__(self):
        self.users = {}
        self.workspaces = []
        self.projects = []
        self.tasks = []
        self.members = []
        self.invites = []
        self.mail = []

    def send_mail(self, email, route, token):
        self.mail.insert(0, {'ID': str(len(self.mail)), 'To': [{'Address': email}], 'HTML': f'<a href="http://localhost:8080/{route}?token={token}&amp;email={email}">Link</a>'})

    def get_mail(self, url, **kwargs):
        if '/api/v1/messages' in url:
            return response({'messages': self.mail})
        return response(next(m for m in self.mail if m['ID'] == url.rsplit('/', 1)[1]))

    def session(self):
        api = self

        class Session:
            headers = {}

            def post(self, url, **kwargs):
                return self.request('POST', url, **kwargs)

            def get(self, url, **kwargs):
                return self.request('GET', url, **kwargs)

            def request(self, method, url, json=None, **kwargs):
                path = url.split('/api/')[-1]
                if path == 'auth/login':
                    user = api.users.get(json['emailOrUsername'])
                    return response({'accessToken': 'access', 'user': user}, 200 if user and user['verified'] else 401)
                if path == 'auth/register':
                    email = json['email']
                    if email in api.users:
                        return response({'code': 'email_exists'}, 400)
                    api.users[email] = {'id': email, 'verified': False, 'isSystemAdmin': email.startswith('admin')}
                    api.send_mail(email, 'verify-email', email)
                    return response({}, 201)
                if path == 'auth/resend-verification-email':
                    api.send_mail(json['email'], 'verify-email', json['email'])
                    return response({})
                if path == 'auth/verify-email':
                    api.users[json['token']]['verified'] = True
                    return response({})
                if path == 'auth/refresh':
                    return response({'accessToken': 'refreshed'})
                if path == 'auth/logout':
                    return response({})
                if path == 'management/workspaces':
                    if method == 'POST':
                        api.workspaces.append({'id': 'w1', **json})
                        return response(api.workspaces[-1])
                    return response(api.workspaces)
                if path.endswith('/members'):
                    return response(api.members)
                if path == 'management/workspaces/w1/invitations':
                    if method == 'POST':
                        invitation = {'id': 'i1', 'workspaceId': 'w1', 'token': 'invite', 'status': 'Pending', 'canAccept': True, **json}
                        api.invites.append(invitation)
                        api.send_mail(json['email'], 'accept-invitation', 'invite')
                        return response(invitation)
                    return response(api.invites)
                if path == 'management/invitations/invite':
                    return response(api.invites[0])
                if path == 'management/invitations/accept':
                    api.members.append({'userId': 'tester@collaby.local'})
                    api.invites[0]['status'] = 'Accepted'
                    return response({})
                if path.endswith('/projects'):
                    if method == 'POST':
                        api.projects.append({'id': 'p1', **json})
                        return response(api.projects[-1])
                    return response(api.projects)
                if path.endswith('/tasks'):
                    if method == 'POST':
                        api.tasks.append(json)
                        return response(json)
                    return response(api.tasks)
                raise AssertionError(f'Unexpected API: {method} {path}')

        return Session()


class SeedTests(unittest.TestCase):
    def run_seed(self, api):
        with patch.object(seed.requests, 'Session', api.session), patch.object(seed.requests, 'get', api.get_mail), patch.object(seed.time, 'sleep'):
            seed.seed({'DEMO_PASSWORD': 'test-only'})

    def test_first_and_repeated_run_preserve_users_workspace_project_tasks(self):
        api = DemoApi()
        self.run_seed(api)
        self.run_seed(api)
        self.assertEqual((2, 1, 1, 3, 1), (len(api.users), len(api.workspaces), len(api.projects), len(api.tasks), len(api.invites)))
        self.assertTrue(all(u['verified'] for u in api.users.values()))

    def test_interrupted_registration_and_invitation_resume(self):
        api = DemoApi()
        api.users['admin@collaby.local'] = {'id': 'admin@collaby.local', 'verified': False, 'isSystemAdmin': True}
        api.workspaces.append({'id': 'w1', 'name': 'Collaby Demo'})
        api.invites.append({'id': 'i1', 'email': 'tester@collaby.local', 'workspaceId': 'w1', 'status': 'Pending', 'canAccept': True})
        api.send_mail('tester@collaby.local', 'accept-invitation', 'invite')
        self.run_seed(api)
        self.assertEqual(1, len(api.invites))
        self.assertEqual('Accepted', api.invites[0]['status'])

    def test_portainer_reuses_local_kubernetes_endpoint_and_checks_api(self):
        session = MagicMock()
        session.post.return_value = response({'jwt': 'test'})
        session.get.side_effect = [response([{'Id': 7, 'Name': 'Existing', 'Type': 5, 'URL': 'https://kubernetes.default.svc'}]), response({'items': []})]
        with patch.object(seed.requests, 'Session', return_value=session):
            seed.portainer({'PANEL_PASSWORD': 'test-only'})
        self.assertEqual(1, session.post.call_count)
        self.assertIn('/api/endpoints/7/kubernetes/api/v1/namespaces', session.get.call_args.args[0])

    def test_portainer_new_install_creates_admin_then_kubernetes_type5(self):
        session = MagicMock()
        session.post.side_effect = [response({}, 422), response({}), response({'jwt': 'test'}), response({'Id': 1})]
        session.get.side_effect = [response([]), response({'items': []})]
        with patch.object(seed.requests, 'Session', return_value=session):
            seed.portainer({'PANEL_PASSWORD': 'test-only'})
        self.assertEqual((None, '5'), session.post.call_args.kwargs['files']['EndpointCreationType'])
        self.assertEqual((None, 'false'), session.post.call_args.kwargs['files']['TLS'])


if __name__ == '__main__':
    unittest.main()
