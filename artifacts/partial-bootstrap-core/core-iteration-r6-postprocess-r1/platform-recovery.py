import json,hashlib,shutil,zipfile,importlib.util
from pathlib import Path
spec=importlib.util.spec_from_file_location('dv_io','language_core/tools/run_verification.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
old=Path('artifacts/partial-bootstrap-core/core-iteration-r6-enabled-600-r1').resolve();out=Path('artifacts/partial-bootstrap-core/core-iteration-r6-postprocess-r1').resolve();out.mkdir(exist_ok=False)
prior=json.loads((old/'result.json').read_text());root=Path(prior['source_root']);seal=json.loads((old/'sources.json').read_text())
assert prior['sources_unchanged']
assert all(mod.sha(root/k)==v for k,v in seal.items())
assert all(mod.sha(old/k)==v for k,v in prior['outputs'].items())
plan=json.loads((root/'language_core/verification/run-plan.json').read_text(encoding='utf-8-sig'))
for f in ['sources.json','sources.zip','.gitattributes']:shutil.copyfile(old/f,out/f)
shutil.copytree(old/'inputs',out/'inputs');shutil.copyfile(old/'trace-enabled.json',out/'trace-enabled.json')
for package in ['c3','session-runner']:shutil.copytree(old/package,out/package,ignore=shutil.ignore_patterns('obj'))
shutil.copyfile('language_core/tools/run_verification.py',out/'platform-orchestrator.py')
inputs=out/'inputs'
def expand(v):
 if isinstance(v,str):return v.replace('{inputs}',str(inputs)).replace('{out}',str(out)).replace('{root}',str(root))
 if isinstance(v,list):return [expand(x) for x in v]
 if isinstance(v,dict):return {k:expand(x) for k,x in v.items()}
 return v
for name,reference in plan.get('path_documents',{}).items():(inputs/name).write_text(json.dumps(expand(json.loads(Path(expand(reference)).read_text(encoding='utf-8-sig'))),ensure_ascii=False,indent=2),encoding='utf-8')
for name,reference in plan.get('platform_manifests',{}).items():
 value=expand(json.loads(Path(expand(reference)).read_text(encoding='utf-8-sig')))
 meta=[['integrity','verified'],['source_fingerprints',list(seal.items())],['runtime_fingerprints',mod.pair_record(prior['programs'])],['export_fingerprints',mod.pair_record(prior['exports'])],['scope','Read-only platform recovery over completed r6 producer trace']]
 assert not any(x[0] in {p[0] for p in meta} for x in value)
 (inputs/name).write_text(json.dumps(value+meta,ensure_ascii=False,indent=2),encoding='utf-8')
assert mod.sha(out/'trace-enabled.json')==prior['outputs']['trace-enabled.json']
result=dict(prior);result.update(status='sessions_executed_judgment_deferred',scope='Read-only platform metadata recovery; no producer process executed',platform_recovery_from=str(old),platform_orchestrator_sha256=mod.sha(out/'platform-orchestrator.py'));result.pop('error',None)
result['outputs']={p.relative_to(out).as_posix():mod.sha(p) for p in out.rglob('*') if p.is_file()}
(out/'result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(out)
