"""Проверка публикуемого набора и декларативного runtime без раскрытия секретов."""
import hashlib, importlib.util, json, re, subprocess
from pathlib import Path
import yaml

ROOT=Path(__file__).resolve().parents[1]
def scan_secrets(files, local_values=()):
    patterns=[rb'glpat-[A-Za-z0-9_.-]{15,}',rb'gh[pousr]_[A-Za-z0-9]{30,}',rb'github_pat_[A-Za-z0-9_]{50,}',rb'AIza[0-9A-Za-z_-]{35}',rb'AKIA[0-9A-Z]{16}',rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----']
    violations=[]
    for path in files:
        if not path.is_file():continue
        relative=path.relative_to(ROOT).as_posix()
        data=path.read_bytes()
        if relative.startswith(('.local/','.tools/')) or path.name in ['.env','secrets.json'] or any(re.search(pattern,data) for pattern in patterns) or any(value in data for value in local_values):
            violations.append(relative)
    return violations

def private_values():
    values=set()
    for path in [ROOT/'.local/external.json', ROOT/'.local/rendered/local.json', ROOT/'.local/cloudflare-state-backup/original.tfstate']:
        try: document=json.loads(path.read_text(encoding='utf-8-sig'))
        except (UnicodeError,ValueError,OSError):continue
        def visit(item):
            if isinstance(item,dict):
                for key,value in item.items():
                    if isinstance(value,str) and len(value)>=16 and re.search('password|secret|token|api.key|unsplash',key,re.I) and not value.startswith(('${','http','file:')):values.add(value.encode())
                    elif isinstance(value,(dict,list)):visit(value)
            elif isinstance(item,list):
                for value in item:visit(value)
        visit(document)
    return values

def verify():
    spec=json.loads((ROOT/'kubernetes/runtime-spec.json').read_text())
    assert {'access','workspace','realtime','engagement','assistant','frontend','postgres','mongo-db','cache','events','alloy','mailpit'} <= spec.keys()
    assert 'promtail' not in spec and 'github' not in spec
    for name,s in spec.items():
        assert not any('GITLAB' in k for k in s.get('environment',{})),name
    adapter=json.loads((ROOT/'services/AgentService/adapter-manifest.json').read_text())['adapter']
    asset=ROOT/'services/AgentService/assets'/adapter['filename']
    assert asset.stat().st_size==adapter['sizeBytes']==36956896
    assert hashlib.sha256(asset.read_bytes()).hexdigest()==adapter['sha256']
    assert adapter['downloadUrl'].startswith('file:///app/assets/') and 'auth' not in adapter
    for p in [ROOT/'kubernetes/bootstrap.yml',ROOT/'ansible/local.yml',ROOT/'.github/workflows/ci.yml']:
        list(yaml.safe_load_all(p.read_text(encoding='utf-8')))
    bootstrap=list(yaml.safe_load_all((ROOT/'kubernetes/bootstrap.yml').read_text(encoding='utf-8')))
    role=next(x for x in bootstrap if x['kind']=='Role' and x['metadata']['name']=='restart-applications')
    assert role['metadata']['namespace']=='collaby' and len(role['rules'])==1
    rule=role['rules'][0]
    assert rule['apiGroups']==['apps'] and rule['resources']==['deployments']
    assert set(rule['verbs'])=={'get','patch'}
    assert set(rule['resourceNames'])=={'access','workspace','realtime','engagement','assistant'}
    files=subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','-z'],cwd=ROOT).decode().split('\0')
    violations=scan_secrets([ROOT/f for f in files if f],private_values())
    assert not violations, 'Обнаружены возможные секреты: '+', '.join(violations)
    ci=yaml.load((ROOT/'.github/workflows/ci.yml').read_text(),Loader=yaml.BaseLoader)
    assert {'push','pull_request','workflow_dispatch'} <= ci['on'].keys()
    assert ci['permissions']=={'contents':'read'}
    for name,job in ci['jobs'].items():
        assert job['runs-on']=='ubuntu-latest',name
        for step in job['steps']:
            assert step.get('with',{}).get('push','false')=='false',name
            assert not re.search(r'git\s+push|kubectl\s+apply|terraform\s+apply|gh\s+release',step.get('run','')),name
    images=ci['jobs']['images']['strategy']['matrix']['include']
    assert {x['name'] for x in images}=={'access','workspace','realtime','engagement','assistant','frontend','postgres','mongo-db','cache','controller'}
    for image in images:assert (ROOT/image['file']).is_file(),image['name']
    print('Контракт runtime, встроенный адаптер и публикуемый набор проверены.')
if __name__=='__main__':verify()
