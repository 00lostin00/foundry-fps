"""Black-box API checks against a live isolated local lobby."""
import concurrent.futures
import json
import os
from pathlib import Path
import socket
import subprocess
import time
import urllib.error
import urllib.request

ROOT=Path(__file__).resolve().parents[1]
checks=0
def require(value, message):
    global checks
    checks+=1
    if not value: raise AssertionError(message)

with socket.socket() as s:
    s.bind(('127.0.0.1',0));port=s.getsockname()[1]
base=f'http://127.0.0.1:{port}'
env=dict(os.environ,FOUNDRY_LOBBY_URL=base)
logdir=ROOT/'artifacts';logdir.mkdir(exist_ok=True)
log=(logdir/'lobby-checks.log').open('w',encoding='utf-8')
server=subprocess.Popen(['dotnet',str(ROOT/'server/bin/Release/net8.0/Foundry.Lobby.dll')],env=env,stdout=log,stderr=log)
def api(method,path,body=None,key=None):
    data=None if body is None else json.dumps(body).encode()
    request=urllib.request.Request(base+path,data=data,method=method,headers={'Content-Type':'application/json',**({'X-Host-Key':key} if key else {})})
    try:
        with urllib.request.urlopen(request,timeout=3) as r:
            raw=r.read();return r.status,json.loads(raw) if raw else {}
    except urllib.error.HTTPError as e:return e.code,{}
try:
    for _ in range(60):
        try:
            if api('GET','/health')[0]==200:break
        except OSError:time.sleep(.1)
    require(api('GET','/health')[0]==200,'Health check')
    require(api('POST','/api/rooms',{'name':'','capacity':8,'port':7777})[0]==400,'Reject empty room name')
    require(api('POST','/api/rooms',{'name':'bad','capacity':99,'port':7777})[0]==400,'Reject oversized capacity')
    status,room=api('POST','/api/rooms',{'name':'test','capacity':3,'port':7777,'password':'test-pass'})
    require(status==200,'Create room')
    path='/api/rooms/'+room['id'];key=room['hostKey']
    status,listing=api('GET','/api/rooms')
    require(status==200 and len(listing['rooms'])==1,'List rooms')
    require('hostKey' not in listing['rooms'][0] and 'passwordHash' not in listing['rooms'][0],'No secret leak')
    require(api('POST',path+'/heartbeat',{'players':1})[0]==401,'Protect host heartbeat')
    require(api('DELETE',path)[0]==401,'Protect room deletion')
    require(api('POST',path+'/join',{'playerName':'A','password':'wrong'})[0]==401,'Check room password')
    def join(_):return api('POST',path+'/join',{'playerName':'Player','password':'test-pass'})
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:results=list(pool.map(join,range(8)))
    accepted=[r[1]['ticket'] for r in results if r[0]==200]
    require(len(accepted)==2,'Concurrent reservation enforces capacity')
    require(sum(r[0]==409 for r in results)==6,'Full room conflicts')
    require(api('POST',path+'/admit',{'ticket':accepted[0]})[0]==401,'Protect admission API')
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        admitted=list(pool.map(lambda _:api('POST',path+'/admit',{'ticket':accepted[0]},key)[0],range(2)))
    require(sorted(admitted)==[200,401],'Ticket is single-use under concurrency')
    require(api('POST',path+'/heartbeat',{'players':2},key)[0]==200,'Authorized heartbeat')
    print('Waiting for admission ticket expiry (16 seconds)...',flush=True)
    time.sleep(16)
    require(api('POST',path+'/admit',{'ticket':accepted[1]},key)[0]==401,'Reject expired ticket')
    require(api('DELETE',path,key=key)[0]==204,'Delete room')
    require(api('POST',path+'/join',{'playerName':'A','password':'test-pass'})[0]==404,'Cannot join deleted room')
    require(api('GET','/api/rooms')[1]['rooms']==[],'Deleted room removed from list')
    print(f'PASS: {checks} lobby integration assertions')
finally:
    server.terminate()
    try:server.wait(timeout=5)
    except subprocess.TimeoutExpired:server.kill();server.wait()
    log.close()
