from pathlib import Path
import json
HERE=Path(__file__).resolve().parent
RUN=HERE/'frozen/standard-yi'
def rec(value):
 if isinstance(value,list):
  if value and all(isinstance(p,list) and len(p)==2 and isinstance(p[0],str) for p in value):return {p[0]:rec(p[1]) for p in value}
  return [rec(p) for p in value]
 return value
last=None; reused=[]; selected=[]; executions=[]; motives=[]; end=None; starts=[]
with (RUN/'stdout.jsonl').open(encoding='utf-8-sig') as source:
 for lineno,line in enumerate(source,1):
  try: packet=rec(json.loads(line))
  except Exception: continue
  if packet.get('type')=='debug_start':starts.append(packet)
  if packet.get('type')=='debug_end':end=packet
  if packet.get('type')!='debug_events':continue
  for event in packet.get('batch',{}).get('events',[]):
   last=event
   item={k:event.get(k) for k in ['event_id','type','actor','game_time','action_id','action_kind','label','status']}
   item['source_line']=lineno
   if event.get('type')=='planning_stop_reused':reused.append(event)
   if event.get('type')=='action_selected':selected.append(item)
   if event.get('type')=='action_execution':
    executions.append(item)
    if '.motive.' in str(event.get('action_id')):motives.append(event)
result=json.loads((RUN/'result.json').read_text())
observations={'scope':'actual flushed projection; raw tail and unobserved branches separate','last_projected_event':last,'debug_end':end,'runtime_result':result,'idle_reuse_events':reused,'actual_motive_executions':motives,'actual_execution_timeline':executions,'starts':starts}
(HERE/'ACTUAL-OBSERVATIONS.json').write_text(json.dumps(observations,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'last':{k:last.get(k) for k in ['event_id','type','game_time','actor']} if last else None,'debug_end':end,'idle_reuse_observed':bool(reused),'motive_actions':[{k:e.get(k) for k in ['event_id','actor','game_time','action_id']} for e in motives],'result':{k:result.get(k) for k in ['elapsed_seconds','exit_code','timeout_stage','source_unchanged','evidence_complete','world_horizon_observed','stage_checkpoint_locations','raw_committed_events_not_yet_projected']}},ensure_ascii=False,indent=2))
