import json
import shutil

SRC = r'C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\entities\sceneinfo_full\000_000.json'
DSTS = [
    SRC,
    r'C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\skillv2\client_root\data\source\maps\龙门寻宝_s\entities\sceneinfo_full\000_000.json',
]
GUID = '{aaaaaaaa-1111-2222-3333-444444444444}'
SPAWN = (23334.0, 761.0, 24224.0)

b = open(SRC, 'rb').read()
d = json.loads(b.decode('gbk'))
wo = d['worldObjects']
sample = wo[list(wo.keys())[0]]
rec = json.loads(json.dumps(sample))  # deep copy

rec['_tableName'] = 'f1_3094_body_hd.Mesh'
cb = rec['comBasic']
cb['actorLocalMatrix'] = [1.0, 0.0, 0.0, 0.0,
                          0.0, 1.0, 0.0, 0.0,
                          0.0, 0.0, 1.0, 0.0,
                          SPAWN[0], SPAWN[1], SPAWN[2], 1.0]
cb['actorBoundBoxMax'] = [SPAWN[0] + 1.0, SPAWN[1] + 2.0, SPAWN[2] + 1.0]
cb['actorBoundBoxMin'] = [SPAWN[0] - 1.0, SPAWN[1], SPAWN[2] - 1.0]
cb['uuid'] = GUID
cr = rec['comRender']
cr['actorModel'] = 'data\\source\\player\\f1\\部件\\f1_3094_body_hd.mesh'
cr['actorAni'] = ''
cr['actorStateMachine'] = ''
cr['enableViewAngleCull'] = 0
cr['disableCull'] = 1
rec['comCustomInfo']['tagPlayDefaultAni'] = 1

wo[GUID] = rec

out = json.dumps(d, ensure_ascii=False).encode('gbk')
for dst in DSTS:
    open(dst, 'wb').write(out)
    print('wrote', len(out), 'bytes ->', dst.encode('gbk', 'replace').decode('gbk'))
print('worldObjects now:', len(wo))
