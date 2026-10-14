import pathlib,json
r=pathlib.Path(__file__).parent
result={}
for name in ('detective-x4','blind-x4','detective-x4-readable'):
 p=r/name/'measurement.jsonl'
 if not p.exists(): continue
 rows=[json.loads(x) for x in p.read_text(encoding='utf-8').splitlines()]
 game=[x for x in rows if 'game_time' in x['snapshot']]
 found={}
 for threshold in (480,482,486,492,500,517,532,549):
  hit=next((x for x in game if x['snapshot']['game_time']>=threshold),None)
  if hit:
   index=rows.index(hit); prev=rows[index-1]['real_ms'] if index else 0
   found[str(threshold)]={'sample_interval_ms':[prev,hit['real_ms']],'observed_game_time':hit['snapshot']['game_time']}
 window=[x for x in game if x['real_ms']<=90000]
 result[name]={'first_game_sample':{'real_ms':game[0]['real_ms'],'game_time':game[0]['snapshot']['game_time']} if game else None,'last_sample_in_90s':{'real_ms':window[-1]['real_ms'],'game_time':window[-1]['snapshot']['game_time'],'chapter':window[-1]['snapshot']['chapter'],'active':window[-1]['snapshot']['active']} if window else None,'first_inactive':next(({'real_ms':x['real_ms'],'game_time':x['snapshot']['game_time']} for x in game if not x['snapshot']['active']),None),'clock_sample_intervals':found}
(r/'comparison-measurements.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
