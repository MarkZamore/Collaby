"""Публикация будущих GGUF-адаптеров в GitHub Releases без изменения runtime."""
import argparse,json,os,re,urllib.error,urllib.parse,urllib.request
from pathlib import Path
from export_adapter_manifest import build_manifest

def api(url, token, method='GET', data=None, content_type='application/json'):
    headers={'Authorization':'Bearer '+token,'Accept':'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28','Content-Type':content_type}
    request=urllib.request.Request(url,headers=headers,method=method,data=data)
    with urllib.request.urlopen(request,timeout=600) as response:return json.load(response)

def publish(file, version, repo, token, output):
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]*/[A-Za-z0-9][A-Za-z0-9_.-]*', repo):
        raise ValueError('Repository должен иметь формат owner/name')
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]*', version):
        raise ValueError('Версия должна содержать только буквы, цифры, точку, дефис и подчёркивание')
    build_manifest(file,version,'https://github.com/'+repo+'/releases')
    base='https://api.github.com/repos/'+repo
    tag='adapter-'+version
    try:release=api(base+'/releases/tags/'+urllib.parse.quote(tag,safe=''),token)
    except urllib.error.HTTPError as exc:
        if exc.code!=404:raise
        release=api(base+'/releases',token,'POST',json.dumps({'tag_name':tag,'name':'Collaby AI adapter '+version,'draft':False,'prerelease':False}).encode())
    existing={a['name']:a for a in release['assets']}
    if file.name in existing or 'adapter-manifest.json' in existing:raise ValueError('Артефакт уже опубликован. Используйте новую версию.')
    upload=release['upload_url'].split('{')[0]
    asset=api(upload+'?name='+urllib.parse.quote(file.name),token,'POST',file.read_bytes(),'application/octet-stream')
    manifest=build_manifest(file,version,asset['browser_download_url'])
    output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    api(upload+'?name=adapter-manifest.json',token,'POST',output.read_bytes(),'application/json')
    return manifest

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--adapter-file',type=Path,required=True);parser.add_argument('--version',required=True);parser.add_argument('--repo',default='MarkZamore/Collaby');parser.add_argument('--output-manifest',type=Path,required=True)
    args=parser.parse_args();token=os.environ.get('GITHUB_TOKEN')
    if not token:raise ValueError('Задайте GITHUB_TOKEN только для ручной публикации Release.')
    publish(args.adapter_file,args.version,args.repo,token,args.output_manifest)
    print('Адаптер и манифест опубликованы в GitHub Releases.')
if __name__=='__main__':main()
