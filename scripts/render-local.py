"""Материализация декларативного Kubernetes runtime; секреты остаются в .local."""
from __future__ import annotations
import argparse, copy, hashlib, json, os, re, secrets, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NS = 'collaby'
PORTS = {'postgres':5432,'mongo-db':27017,'cache':6379,'events':9092,'access':5001,'workspace':5003,'realtime':5004,'engagement':5006,'assistant':8100,'frontend':80,'pg':80,'mongo':8081,'redis':5540,'portainer':9000,'loki':3100,'tempo':3200,'otel':4317,'prometheus':9090,'grafana':3000,'kafka':8080,'server':19999,'mailpit':8025,'alloy':12345}
STATEFUL = {'postgres':'4Gi','mongo-db':'4Gi','cache':'1Gi','events':'4Gi'}
BACKEND = ['access','workspace','realtime','engagement','assistant','frontend']
PUBLIC_PORTS = {'frontend':8080,'mailpit':8025,'grafana':3001,'portainer':9000,'pg':5050,'mongo':8081,'redis':5540,'kafka':8082,'server':19999}

def obj(kind, name, **kw):
    api = 'apps/v1' if kind in ['Deployment','StatefulSet','DaemonSet'] else 'batch/v1' if kind=='Job' else 'v1'
    return {'apiVersion':api,'kind':kind,'metadata':{'name':name,'namespace':NS,'labels':{'app.kubernetes.io/part-of':'collaby','app':name}},**kw}

def expand(value, env):
    def repl(m):
        key, default = (m[1].split(':-',1)+[''])[:2] if ':-' in m[1] else (m[1],None)
        if key in env:return str(env[key])
        if default is not None:return default
        raise ValueError(f'Не задана переменная {key}')
    return re.sub(r'\$\{([^}]+)\}',repl,str(value))

def fingerprint(build):
    context=ROOT/build['context']
    h=hashlib.sha256()
    excluded={'.git','.local','.tools','node_modules','bin','obj','__pycache__','dist','TestResults','.idea','.vs','.vscode','.claude','.playwright-mcp','build','coverage','logs','ci-reports','qa-artifacts','output','.terraform'}
    excluded_files={'.gitignore','.env','.env.local','secrets.json','server-connect.md','panel-credentials.local.md','docker-compose.override.yml','gl-secret-detection-report.json','Thumbs.db','.DS_Store'}
    paths=[]
    for directory,folders,files in os.walk(context,topdown=True,followlinks=False):
        # Исключённые каталоги не обходятся: внутри могут быть недоступные WSL symlink.
        folders[:]=sorted(folder for folder in folders if folder not in excluded)
        for name in sorted(files):
            if name in excluded_files or name.endswith(('.pyc','.log','.local','.local.json','.local.yml','.user','.suo','.userosscache','.sln.docstates','.DotSettings.user')) or name.startswith('.coverage'):continue
            p=Path(directory)/name
            if p.is_symlink():continue
            paths.append(p)
    for p in sorted(paths):
        h.update(str(p.relative_to(context)).replace('\\','/').encode())
        with p.open('rb') as stream:
            for chunk in iter(lambda:stream.read(1024*1024),b''):h.update(chunk)
    return h.hexdigest()[:16]

def environment(spec, target, external=True):
    if target.exists():env=json.loads(target.read_text(encoding='utf-8-sig'))
    else:env={}
    for s in spec.values():
        for value in s.get('environment',{}).values():
            for m in re.finditer(r'\$\{([^}:]+)(?::-([^}]*))?\}',str(value)):
                key,default=m.groups()
                if key in env:continue
                if default == '':env[key]=''
                elif any(t in key for t in ['PASSWORD','KEY','TOKEN']):env[key]=secrets.token_hex(32)
                elif key.endswith('_USER'):env[key]=key.lower()
                elif default is not None:env[key]=default
                else:env[key]=''
    env.setdefault('NETDATA_STREAM_KEY',str(uuid.uuid4()))
    env.setdefault('PANEL_PASSWORD',secrets.token_hex(20))
    env.setdefault('DEMO_PASSWORD',secrets.token_hex(16)+'Aa1!')
    env['SYSTEM_ADMIN_EMAILS']='admin@collaby.local'
    env['POSTGRES_USER']='collaby_admin'
    env['MONGO_INITDB_ROOT_USERNAME']='collaby_admin'
    env['REDIS_ADMIN_USER']='redis-admin'
    env['SENDER_EMAIL']='noreply@collaby.local'
    env['PGADMIN_DEFAULT_EMAIL']='admin@collaby.org'
    extern=ROOT/'.local/external.json'
    if external and extern.exists():env.update(json.loads(extern.read_text(encoding='utf-8-sig')))
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(env,indent=2),encoding='utf-8')
    return env

def render(output, test=False):
    global NS
    spec=json.loads((ROOT/'kubernetes/runtime-spec.json').read_text())
    env=environment(spec,output/'local.json',external=not test)
    configs=[]; secretobjects=[];storage=[];apps=[];panels=[];builds=[]
    for name,s in spec.items():
        NS = 'portainer' if name == 'portainer' else 'collaby'
        build=s.get('build')
        if build:
            if name=='assistant':build['dockerfile']='Dockerfile.cpu'
            tag=f'collaby/{name}:{fingerprint(build) if not test else "validation"}'
            builds.append({'name':name,'image':tag,**build})
            image=tag
        else:image=expand(s['image'],env)
        values={k:expand(v,env) for k,v in s.get('environment',{}).items()}
        confidential={k:v for k,v in values.items() if any(x in k.upper() for x in ['PASSWORD','KEY','TOKEN','CONNECTIONSTRINGS','MONGODB_URL','USER_PASSWORD'])}
        public={k:v for k,v in values.items() if k not in confidential}
        configs.append(obj('ConfigMap',name,data=public))
        secretobjects.append(obj('Secret',name,type='Opaque',stringData=confidential))
        container={'name':name,'image':image,'imagePullPolicy':'Never' if build else 'IfNotPresent','ports':[{'containerPort':PORTS[name]}],'envFrom':[{'configMapRef':{'name':name}},{'secretRef':{'name':name}}],'resources':{'requests':{'cpu':'50m','memory':'128Mi'},'limits':{'memory':'768Mi'}}}
        if name in ['assistant']:container['resources']={'requests':{'cpu':'500m','memory':'1Gi'},'limits':{'cpu':'6','memory':'4Gi'}}
        if name in BACKEND and name!='assistant':container['resources']['limits']['memory']='768Mi'
        if name=='mongo-db':container['resources']['limits']['memory']='768Mi'
        if name=='events':container['resources']['limits']['memory']='1Gi'
        if s.get('command'):
            import shlex
            container['args']=shlex.split(s['command']) if isinstance(s['command'],str) else s['command']
        volumes=[];mounts=[];claims=[]
        for i,volume in enumerate(s.get('volumes',[])):
            # Разделители внутри ${...} не являются разделителями volume.
            volume=expand(volume,env);source,path,*mode=volume.split(':')
            vname=f'volume-{i}'
            if source.startswith('.'):
                p=ROOT/source
                data={p.name:p.read_text(encoding='utf-8')} if p.is_file() else {q.relative_to(p).as_posix().replace('/','__'):q.read_text(encoding='utf-8') for q in p.rglob('*') if q.is_file()}
                if name=='pg' and p.name=='servers.json':
                    servers=json.loads(data['servers.json'])
                    for server in servers['Servers'].values():
                        logical=server['MaintenanceDB'].removeprefix('collaby_').upper()
                        server['Username']=env[f'POSTGRES_{logical}_USER']
                        server['PassFile']='.pgpass'
                        server['ConnectionParameters']={'sslmode':'prefer','connect_timeout':10,'passfile':'.pgpass'}
                    data['servers.json']=json.dumps(servers)
                cm=f'{name}-files-{i}';configs.append(obj('ConfigMap',cm,data=data))
                volumes.append({'name':vname,'configMap':{'name':cm}})
                mounts.append({'name':vname,'mountPath':path,**({'subPath':p.name} if p.is_file() else {}),'readOnly':True})
            else:
                pvc=f'{name}-data-{i}'
                claim={'accessModes':['ReadWriteOnce'],'resources':{'requests':{'storage':STATEFUL.get(name,'8Gi' if name=='assistant' else '1Gi')}}}
                if name in STATEFUL:claims.append({'metadata':{'name':vname},'spec':claim})
                else:configs.append(obj('PersistentVolumeClaim',pvc,spec=claim));volumes.append({'name':vname,'persistentVolumeClaim':{'claimName':pvc}})
                mounts.append({'name':vname,'mountPath':path})
        if mounts:container['volumeMounts']=mounts
        pod={'securityContext':{'fsGroup':1000},'enableServiceLinks':False,'containers':[container],'terminationGracePeriodSeconds':30,'automountServiceAccountToken':False}
        if volumes:pod['volumes']=volumes
        if name in ['postgres','mongo-db','cache']:pod.pop('securityContext')
        if name=='pg':
            pod['securityContext']['fsGroup']=5050
            escape=lambda value:str(value).replace('\\','\\\\').replace(':','\\:')
            pgpass=''.join(f"postgres:5432:collaby_{logical}:{escape(env[f'POSTGRES_{logical.upper()}_USER'])}:{escape(env[f'POSTGRES_{logical.upper()}_PASSWORD'])}\n" for logical in ['identity','users','management','gamification','analytics'])
            secretobjects.append(obj('Secret','pg-connections',type='Opaque',stringData={'pgpass':pgpass}))
            pod['volumes'].append({'name':'pgpass-source','secret':{'secretName':'pg-connections','defaultMode':288}})
            user_directory=env['PGADMIN_DEFAULT_EMAIL'].replace('@','_')
            pod['initContainers']=[{'name':'prepare-connection-passwords','image':image,'command':['sh','-ec',f'mkdir -p /var/lib/pgadmin/storage/{user_directory}; cp /credentials/pgpass /var/lib/pgadmin/storage/{user_directory}/.pgpass; chmod 600 /var/lib/pgadmin/storage/{user_directory}/.pgpass'],'securityContext':{'runAsUser':5050,'runAsGroup':5050,'allowPrivilegeEscalation':False},'resources':{'requests':{'cpu':'10m','memory':'32Mi'},'limits':{'memory':'128Mi'}},'volumeMounts':[{'name':'volume-0','mountPath':'/var/lib/pgadmin'},{'name':'pgpass-source','mountPath':'/credentials','readOnly':True}]}]
        if name=='grafana':pod['securityContext']['fsGroup']=472
        if name=='loki':pod['securityContext']['fsGroup']=10001
        if name=='prometheus':pod['securityContext']['fsGroup']=65534
        if name=='workspace':pod.update(serviceAccountName='workspace',automountServiceAccountToken=True)
        if name=='alloy':pod.update(serviceAccountName='alloy',automountServiceAccountToken=True)
        if name=='portainer':pod.update(serviceAccountName='portainer',automountServiceAccountToken=True)
        if name=='server':pod.update(serviceAccountName='netdata',automountServiceAccountToken=True)
        probe={'tcpSocket':{'port':PORTS[name]},'periodSeconds':10,'timeoutSeconds':5}
        if name in BACKEND:
            path='/health/ready' if name in ['access','workspace','realtime'] else '/api/gamification/health/ready' if name=='engagement' else '/api/ai/health' if name=='assistant' else '/healthz'
            probe={'httpGet':{'path':path,'port':PORTS[name]},'periodSeconds':10,'timeoutSeconds':5}
        if name=='postgres':probe={'exec':{'command':['sh','-ec','pg_isready -U "$POSTGRES_USER" -d postgres']},'periodSeconds':10,'timeoutSeconds':5}
        if name=='cache':probe={'exec':{'command':['sh','-ec','redis-cli --user "$REDIS_ADMIN_USER" --pass "$REDIS_ADMIN_PASSWORD" ping | grep -q PONG']},'periodSeconds':10,'timeoutSeconds':5}
        if name in ['loki','tempo','prometheus','otel','grafana','alloy']:
            path,port={'loki':('/ready',3100),'tempo':('/ready',3200),'prometheus':('/-/ready',9090),'otel':('/',13133),'grafana':('/api/health',3000),'alloy':('/-/ready',12345)}[name]
            probe={'httpGet':{'path':path,'port':port},'periodSeconds':10,'timeoutSeconds':5}
        container['readinessProbe']=probe
        container['startupProbe']={**probe,'failureThreshold':240 if name=='assistant' else 60}
        container['livenessProbe']={'tcpSocket':{'port':PORTS[name]},'periodSeconds':30,'failureThreshold':6}
        workload=obj('StatefulSet' if name in STATEFUL else 'Deployment',name,spec={'replicas':1,'selector':{'matchLabels':{'app':name}},'template':{'metadata':{'labels':{'app':name,'app.kubernetes.io/part-of':'collaby'}},'spec':pod}})
        if name in STATEFUL:workload['spec'].update(serviceName=name,volumeClaimTemplates=claims)
        else:workload['spec']['strategy']={'type':'Recreate'}
        workload['spec']['template']['metadata']['annotations'] = {'collaby.io/config-hash':hashlib.sha256(json.dumps([values,[c for c in configs if c['metadata']['namespace']==NS and c['metadata']['name'].startswith(name+'-files-')]],sort_keys=True).encode()).hexdigest()}
        serviceports=[{'name':'http' if name not in STATEFUL else 'db','port':PORTS[name],'targetPort':PORTS[name]}]
        if name=='mailpit':serviceports.append({'name':'smtp','port':1025,'targetPort':1025})
        if name=='events':serviceports.append({'name':'controller','port':9093,'targetPort':9093})
        if name=='otel':serviceports.extend([{'name':'metrics','port':9464,'targetPort':9464},{'name':'otlp-http','port':4318,'targetPort':4318}])
        if name=='tempo':serviceports.extend([{'name':'otlp','port':4317,'targetPort':4317}])
        service=obj('Service',name,spec={'selector':{'app':name},'ports':serviceports})
        if name=='events':service['spec']['publishNotReadyAddresses']=True
        group=storage if name in STATEFUL or name=='mailpit' else apps if name in BACKEND else panels
        group.extend([service,workload])
    NS = 'collaby'
    import bcrypt
    password_hash = env.get('PANEL_PASSWORD_HASH')
    if not password_hash or not bcrypt.checkpw(env['PANEL_PASSWORD'].encode(),password_hash.encode()):
        password_hash = bcrypt.hashpw(env['PANEL_PASSWORD'].encode(), bcrypt.gensalt()).decode()
        env['PANEL_PASSWORD_HASH']=password_hash
        (output/'local.json').write_text(json.dumps(env,indent=2),encoding='utf-8')
    gateway_config = '\n'.join(f':{port} {{\n basic_auth {{\n  admin {password_hash}\n }}\n reverse_proxy {name}:{port}\n}}' for name,port in [('redis',5540),('server',19999),('mailpit',8025)])
    secretobjects.append(obj('Secret','panel-gateway',type='Opaque',stringData={'Caddyfile':gateway_config}))
    panels.extend([
        obj('Deployment','panel-gateway',spec={'replicas':1,'selector':{'matchLabels':{'app':'panel-gateway'}},'template':{'metadata':{'labels':{'app':'panel-gateway'}},'spec':{'automountServiceAccountToken':False,'containers':[{'name':'gateway','image':'caddy:2.11.4-alpine@sha256:77c07d5ebfa5be9fd6c820d2094ae662c9e7eeb9bf98346b7f639900263ee2a2','resources':{'requests':{'memory':'32Mi'},'limits':{'memory':'128Mi'}},'volumeMounts':[{'name':'config','mountPath':'/etc/caddy','readOnly':True}]}],'volumes':[{'name':'config','secret':{'secretName':'panel-gateway'}}]}}}),
        obj('Service','panel-gateway',spec={'selector':{'app':'panel-gateway'},'ports':[{'name':name,'port':port,'targetPort':port} for name,port in [('redis',5540),('server',19999),('mailpit',8025)]]})
    ])
    gateway=next(x for x in panels if x['kind']=='Deployment' and x['metadata']['name']=='panel-gateway')
    gateway['spec']['template']['metadata']['annotations']={'collaby.io/config-hash':hashlib.sha256(gateway_config.encode()).hexdigest()}
    gateway_container=gateway['spec']['template']['spec']['containers'][0]
    gateway_container['readinessProbe']={'tcpSocket':{'port':5540},'periodSeconds':5}
    gateway_container['startupProbe']={'tcpSocket':{'port':5540},'periodSeconds':5,'failureThreshold':30}
    gateway_container['livenessProbe']={'tcpSocket':{'port':5540},'periodSeconds':30,'failureThreshold':3}
    registry=json.loads((ROOT/'platform/contracts/events/topic-registry.json').read_text())
    topic_names=[name for topic in registry['topics'] for name in [topic['name'],topic.get('dlq')] if name]
    topic_script='set -eu\nwhile IFS= read -r topic; do\n /opt/bitnami/kafka/bin/kafka-topics.sh --bootstrap-server events:9092 --create --if-not-exists --topic "$topic" --partitions 3 --replication-factor 1\ndone < /topics/topics.txt\n'
    configs.append(obj('ConfigMap','events-bootstrap',data={'ensure-topics.sh':topic_script,'topics.txt':'\n'.join(topic_names)+'\n'}))
    topic_image=next(x for x in storage if x['kind']=='StatefulSet' and x['metadata']['name']=='events')['spec']['template']['spec']['containers'][0]['image']
    topics=[obj('Job','events-bootstrap-'+secrets.token_hex(4),spec={'ttlSecondsAfterFinished':3600,'backoffLimit':2,'activeDeadlineSeconds':300,'template':{'metadata':{'labels':{'app.kubernetes.io/part-of':'collaby'}},'spec':{'restartPolicy':'Never','automountServiceAccountToken':False,'containers':[{'name':'topics','image':topic_image,'command':['sh','/topics/ensure-topics.sh'],'resources':{'requests':{'memory':'128Mi'},'limits':{'memory':'384Mi'}},'volumeMounts':[{'name':'topics','mountPath':'/topics'}]}],'volumes':[{'name':'topics','configMap':{'name':'events-bootstrap'}}]}}})]
    parent_stream=f"[{env['NETDATA_STREAM_KEY']}]\n enabled = yes\n allow from = *\n"
    child_stream=f"[stream]\n enabled = yes\n destination = server:19999\n api key = {env['NETDATA_STREAM_KEY']}\n"
    secretobjects.extend([obj('Secret','netdata-parent',type='Opaque',stringData={'stream.conf':parent_stream}),obj('Secret','netdata-child',type='Opaque',stringData={'stream.conf':child_stream})])
    parent=next(x for x in panels if x['kind']=='Deployment' and x['metadata']['name']=='server')
    parentpod=parent['spec']['template']['spec'];parentcontainer=parentpod['containers'][0]
    parentpod['volumes'].append({'name':'stream','secret':{'secretName':'netdata-parent'}})
    parentcontainer['volumeMounts'].append({'name':'stream','mountPath':'/etc/netdata/stream.conf','subPath':'stream.conf','readOnly':True})
    parentcontainer['env']=[{'name':'NETDATA_CLAIM_TOKEN','value':''}]
    parentpod['volumes'].append({'name':'retention','configMap':{'name':'netdata-retention'}})
    parentcontainer['volumeMounts'].append({'name':'retention','mountPath':'/etc/netdata/netdata.conf','subPath':'netdata.conf','readOnly':True})
    configs.append(obj('ConfigMap','netdata-retention',data={'netdata.conf':'[db]\n mode = dbengine\n dbengine tier 0 retention size = 256MiB\n dbengine tier 0 retention time = 3d\n dbengine tier 1 retention size = 128MiB\n dbengine tier 2 retention size = 64MiB\n[cloud]\n enabled = no\n'}))
    configs.append(obj('ConfigMap','netdata-kubernetes',data={'go.d.conf':'modules:\n  k8s_state: yes\n','k8s_state.conf':'jobs:\n  - name: kubernetes\n    update_every: 5\n'}))
    parentpod['volumes'].append({'name':'kubernetes-state','configMap':{'name':'netdata-kubernetes'}})
    parentcontainer['volumeMounts'].extend([{'name':'kubernetes-state','mountPath':'/etc/netdata/go.d.conf','subPath':'go.d.conf','readOnly':True},{'name':'kubernetes-state','mountPath':'/etc/netdata/go.d/k8s_state.conf','subPath':'k8s_state.conf','readOnly':True}])
    childcontainer=copy.deepcopy(parentcontainer)
    childcontainer['name']='netdata-child'
    childcontainer['resources']={'requests':{'cpu':'50m','memory':'128Mi'},'limits':{'memory':'512Mi'}}
    childcontainer['env']=[{'name':'NETDATA_HOST_PREFIX','value':'/host'},{'name':'DO_NOT_TRACK','value':'1'},{'name':'NETDATA_HOSTNAME','valueFrom':{'fieldRef':{'fieldPath':'spec.nodeName'}}}]
    childcontainer['volumeMounts']=[{'name':'host-proc','mountPath':'/host/proc','readOnly':True},{'name':'host-sys','mountPath':'/host/sys','readOnly':True},{'name':'host-cgroup','mountPath':'/host/sys/fs/cgroup','readOnly':True},{'name':'stream','mountPath':'/etc/netdata/stream.conf','subPath':'stream.conf','readOnly':True},{'name':'lib','mountPath':'/var/lib/netdata'},{'name':'cache','mountPath':'/var/cache/netdata'},{'name':'retention','mountPath':'/etc/netdata/netdata.conf','subPath':'netdata.conf','readOnly':True}]
    childpod={'serviceAccountName':'netdata','hostPID':True,'automountServiceAccountToken':True,'containers':[childcontainer],'volumes':[{'name':'host-proc','hostPath':{'path':'/proc','type':'Directory'}},{'name':'host-sys','hostPath':{'path':'/sys','type':'Directory'}},{'name':'host-cgroup','hostPath':{'path':'/sys/fs/cgroup','type':'Directory'}},{'name':'stream','secret':{'secretName':'netdata-child'}},{'name':'lib','emptyDir':{}},{'name':'cache','emptyDir':{}},{'name':'retention','configMap':{'name':'netdata-retention'}}]}
    panels.append(obj('DaemonSet','netdata-child',spec={'selector':{'matchLabels':{'app':'netdata-child'}},'template':{'metadata':{'labels':{'app':'netdata-child','app.kubernetes.io/part-of':'collaby'}},'spec':childpod}}))
    output.mkdir(parents=True,exist_ok=True)
    for file,data in [('config',configs),('secrets',secretobjects),('storage',storage),('apps',apps),('panels',panels),('topics',topics)]:
        (output/f'{file}.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    controller={'name':'controller','context':'.','dockerfile':'ansible/Dockerfile'}
    controller['image']='collaby/controller:'+('validation' if test else fingerprint(controller))
    builds.append(controller)
    (output/'builds.json').write_text(json.dumps(builds,indent=2),encoding='utf-8')
    (output/'ports.json').write_text(json.dumps({n:{'local':p,'remote':PORTS[n],'namespace':'portainer' if n=='portainer' else 'collaby','service':'panel-gateway' if n in ['redis','server','mailpit'] else n} for n,p in PUBLIC_PORTS.items()}),encoding='utf-8')
    print('Конфигурация Kubernetes создана. Секреты сохранены только в исключённом локальном каталоге.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--validate',action='store_true');args=parser.parse_args()
    render(ROOT/('.local/validated' if args.validate else '.local/rendered'),args.validate)
