import importlib.util,json,tempfile,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('render_local',ROOT/'scripts/render-local.py');runtime=importlib.util.module_from_spec(spec);spec.loader.exec_module(runtime)

class RuntimeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.directory=tempfile.TemporaryDirectory()
        cls.output=Path(cls.directory.name)
        runtime.render(cls.output,test=True)
        cls.resources=[]
        for name in ['config','secrets','storage','apps','panels','topics']:
            cls.resources.extend(json.loads((cls.output/f'{name}.json').read_text(encoding='utf-8')))
    @classmethod
    def tearDownClass(cls):
        cls.directory.cleanup()
    def resource(self,kind,name):
        return next(x for x in self.resources if x['kind']==kind and x['metadata']['name']==name)
    def test_expansion_does_not_replace_password_with_default(self):
        self.assertEqual(runtime.expand('${PASSWORD:-fallback}',{'PASSWORD':'private'}),'private')
        self.assertEqual(runtime.expand('${PORT:-8080}',{}),'8080')
        with self.assertRaises(ValueError):runtime.expand('${PASSWORD}',{})
    def test_local_credentials_survive_repeated_render(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'local.json'
            spec={'db':{'environment':{'PASSWORD':'${DATABASE_PASSWORD}','USER':'${DATABASE_USER}'}}}
            first=runtime.environment(spec,path);second=runtime.environment(spec,path)
            self.assertEqual(first['DATABASE_PASSWORD'],second['DATABASE_PASSWORD'])
            self.assertEqual(first['DEMO_PASSWORD'],second['DEMO_PASSWORD'])
            self.assertGreaterEqual(len(first['DATABASE_PASSWORD']),32)
    def test_loopback_ports_are_unique(self):
        self.assertEqual(len(runtime.PUBLIC_PORTS.values()),len(set(runtime.PUBLIC_PORTS.values())))
    def test_fingerprint_prunes_ignored_directories_before_file_access(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            (root/'source.txt').write_text('source')
            ignored=root/'.local'/'unreadable'
            ignored.mkdir(parents=True)
            (ignored/'secret.txt').write_text('private')
            build={'context':str(root)}
            first=runtime.fingerprint(build)
            (ignored/'secret.txt').write_text('changed private')
            self.assertEqual(first,runtime.fingerprint(build))
            (root/'source.txt').write_text('changed source')
            self.assertNotEqual(first,runtime.fingerprint(build))
    def test_all_persistent_database_mounts_are_stateful_claims(self):
        for name in runtime.STATEFUL:
            workload=self.resource('StatefulSet',name)
            claimed={x['metadata']['name'] for x in workload['spec']['volumeClaimTemplates']}
            mounts=workload['spec']['template']['spec']['containers'][0]['volumeMounts']
            self.assertTrue(claimed)
            self.assertTrue(all(x['name'] in claimed for x in mounts))
        self.assertEqual(len(self.resource('StatefulSet','mongo-db')['spec']['volumeClaimTemplates']),2)
    def test_credentials_are_not_in_configmaps(self):
        env=json.loads((self.output/'local.json').read_text())
        for item in self.resources:
            if item['kind']!='ConfigMap':continue
            content=json.dumps(item['data'])
            for key,value in env.items():
                if value and any(x in key for x in ['PASSWORD','SIGNING_KEY','JWT_KEY','STREAM_KEY']):
                    self.assertNotIn(value,content)
    def test_kafka_controller_and_topic_registry_are_complete(self):
        self.assertIn(9093,[x['port'] for x in self.resource('Service','events')['spec']['ports']])
        registry=json.loads((ROOT/'platform/contracts/events/topic-registry.json').read_text())
        topics=self.resource('ConfigMap','events-bootstrap')['data']['topics.txt'].splitlines()
        for topic in registry['topics']:
            self.assertIn(topic['name'],topics)
            if topic.get('dlq'):self.assertIn(topic['dlq'],topics)
        job=next(x for x in self.resources if x['kind']=='Job')
        self.assertGreater(job['spec']['ttlSecondsAfterFinished'],0)
    def test_netdata_parent_has_persistent_state_and_child_collects_node(self):
        parent=self.resource('Deployment','server')['spec']['template']['spec']
        child=self.resource('DaemonSet','netdata-child')['spec']['template']['spec']
        self.assertTrue(any('persistentVolumeClaim' in x for x in parent['volumes']))
        self.assertTrue(child['hostPID'])
        paths={x['hostPath']['path'] for x in child['volumes'] if 'hostPath' in x}
        self.assertTrue({'/proc','/sys','/sys/fs/cgroup'}.issubset(paths))
        self.assertFalse(any(x.get('securityContext',{}).get('privileged') for x in child['containers']))
    def test_portainer_namespace_and_service_account(self):
        workload=self.resource('Deployment','portainer')
        self.assertEqual(workload['metadata']['namespace'],'portainer')
        self.assertEqual(workload['spec']['template']['spec']['serviceAccountName'],'portainer')
    def test_pgadmin_bootstrap_email_is_not_reserved_local_domain(self):
        email=self.resource('ConfigMap','pg')['data']['PGADMIN_DEFAULT_EMAIL']
        self.assertEqual(email,'admin@collaby.org')
    def test_pgadmin_connections_use_generated_users_and_private_password_file(self):
        env=json.loads((self.output/'local.json').read_text())
        servers=json.loads(self.resource('ConfigMap','pg-files-1')['data']['servers.json'])['Servers']
        self.assertEqual(len(servers),5)
        pgpass=self.resource('Secret','pg-connections')['stringData']['pgpass']
        for server in servers.values():
            logical=server['MaintenanceDB'].removeprefix('collaby_').upper()
            self.assertEqual(server['Username'],env[f'POSTGRES_{logical}_USER'])
            self.assertEqual(server['ConnectionParameters']['passfile'],'.pgpass')
            self.assertIn(env[f'POSTGRES_{logical}_PASSWORD'],pgpass)
        init=self.resource('Deployment','pg')['spec']['template']['spec']['initContainers'][0]
        self.assertEqual(init['securityContext']['runAsUser'],5050)
        self.assertIn('chmod 600',init['command'][-1])
    def test_local_images_cannot_fall_back_to_public_registry(self):
        for name in runtime.BACKEND:
            container=self.resource('Deployment',name)['spec']['template']['spec']['containers'][0]
            self.assertEqual(container['imagePullPolicy'],'Never')
        ai=self.resource('ConfigMap','assistant')['data']
        self.assertEqual(ai['AI_REQUIRE_ADAPTER'],'true')
        self.assertEqual(ai['AI_LLM_GPU_LAYERS'],'0')
    def test_spring_panel_does_not_inherit_service_link_server_port(self):
        pod=self.resource('Deployment','kafka')['spec']['template']['spec']
        self.assertFalse(pod['enableServiceLinks'])
    def test_auth_gateway_waits_for_valid_loaded_configuration(self):
        config=self.resource('Secret','panel-gateway')['stringData']['Caddyfile']
        self.assertNotIn('basic_auth { admin',config)
        container=self.resource('Deployment','panel-gateway')['spec']['template']['spec']['containers'][0]
        self.assertEqual(container['readinessProbe']['tcpSocket']['port'],5540)
if __name__=='__main__':unittest.main()
