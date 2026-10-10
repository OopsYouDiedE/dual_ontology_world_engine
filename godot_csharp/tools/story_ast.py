"""Typed Core AST expressions, using the existing story expression parser."""
from story_base import StoryError

CONDITIONS = {'exists': ('s',), 'tag': ('s','s','i'), 'at': ('s','s'),
              'related': ('s','s','s'), 'kind': ('s','s'), 'any_tag': ('s','s','i'),
              'observed': ('s','s','i','s'), 'param': ('s',), 'option': ('s','s'), 'gte': ('i','i')}
EFFECTS = {'set': ('s','s','i'), 'change': ('s','s','i'), 'spend': ('s','s','i'),
           'travel': ('s',), 'inspect': ('s',), 'take': ('s',), 'destroy': ('s',),
           'place': ('s','s'), 'unlock': ('s','s'), 'require': ('c',),
           'when': ('c','e'), 'wait': ('n','e'), 'wait_until': ('c','e'),
           'think': ('c',), 'procedure': ('s',), 'relate': ('s','s','s'),
           'temporary': ('s','s','i','n'), 'say': ('s','s','e'),
           'occupy': ('s','s','i'), 'transport': ('s','s'), 'tell': ('s','s','c')}
INTEGERS = {'value': ('s','s'), 'add': ('i','i'), 'sub': ('i','i'), 'min': ('i','i'), 'max': ('i','i'), 'div': ('i','i'), 'int_when': ('c','i','i')}

def expression(text, wanted, line=0):
    from story_chapter1 import parse_expr
    node = parse_expr(str(text), line).json()
    def check(e, typ):
        op = e['op']
        args = e.get('args', [])
        if op == 'literal':
            v = e['value']
            if (typ == 's' and isinstance(v,str) or typ == 'i' and type(v) is int
                or typ == 'n' and type(v) in (int,float) or typ == 'c' and type(v) is bool): return
        if typ == 'c' and op in ('not','and','or'):
            if len(args) != (1 if op == 'not' else 2): raise StoryError('Invalid boolean AST.',line)
            for a in args: check(a,'c')
            return
        if op == 'call':
            name = e['name']
            if name in ('true','false') and typ == 'c' and not args: return
            if name == 'seq' and typ == 'e':
                for a in args: check(a,'e')
                return
            if name == 'choose' and typ in ('c','e') and len(args)==3:
                check(args[0],'c'); check(args[1],typ); check(args[2],typ); return
            signature = (CONDITIONS if typ == 'c' else EFFECTS if typ == 'e' else INTEGERS if typ == 'i' else {}).get(name)
            if signature is not None and len(args)==len(signature):
                for a,t in zip(args,signature): check(a,t)
                return
        raise StoryError('Unsupported or ill-typed Core AST: '+str(e),line)
    check(node,wanted)
    return node

def rules(data, line=0):
    if data == '': data = {}
    result=[]
    for key,r in data.items():
        if set(r)-{'select','condition','effect','mode','weight'} or not {'condition','effect','mode','weight'}<=set(r):
            raise StoryError('Rule requires condition, effect, mode and weight: '+key,line)
        if r['mode'] not in ('Prefer','Permit','Forbid','Require','Infer') or type(r['weight']) is not int:
            raise StoryError('Invalid rule mode or weight: '+key,line)
        result.append({**r,'condition':expression(r['condition'],'c',line), 'effect':expression(r['effect'],'e',line),
                       'select':expression(r.get('select','true()'),'c',line)})
    return result

def program(data,line=0):
    if not {'procedures','actions'} <= set(data) or set(data)-{'procedures','actions','mediation','commitments','receipt_attitudes','laws','autonomous','recipes','schedule','bindings','body_defaults','belief_parameters','constants','stimuli'}: raise StoryError('Execution requires procedures and actions.',line)
    policies=data.get('receipt_attitudes', {})
    if not isinstance(policies,dict): raise StoryError('Receipt attitudes require a policy mapping.',line)
    for policy, outcomes in policies.items():
        if not isinstance(outcomes,dict) or set(outcomes)-{'accepted','rejected'}:
            raise StoryError('Unknown receipt attitude outcome: '+policy,line)
        for tags in outcomes.values():
            if not isinstance(tags,dict) or any(not isinstance(tag,str) or not tag or type(value) is not int for tag,value in tags.items()):
                raise StoryError('Receipt attitude requires integer tag values: '+policy,line)
    if 'mediation' in data:
        from story_base import split_list
        data['mediation']['participants'] = split_list(data['mediation']['participants'])
    procedures={k:expression(v,'e',line) for k,v in data['procedures'].items()}
    actions={}
    for k,r in data['actions'].items():
        if set(r)-{'condition','condition_world','goal','effect','flags','job','cost','text','actor','target','target_self','promise','send_cost','travel_cost'} or not {'condition','goal','effect','cost','text','actor','target'}<=set(r):
            raise StoryError('Incomplete social action: '+k,line)
        if type(r['cost']) not in (int,float) or r['cost']<0 or r['actor'] not in ('sender','recipient'):
            raise StoryError('Invalid social action cost or actor: '+k,line)
        if r.get('condition_world','subjective') not in ('physical','subjective'):
            raise StoryError('Invalid social action condition world: '+k,line)
        for field in ('send_cost','travel_cost'):
            if field in r and (type(r[field]) not in (int,float) or r[field]<0):
                raise StoryError('Invalid social action '+field+': '+k,line)
        for field in ('target_self','promise'):
            if field in r and type(r[field]) is not bool:
                raise StoryError('Invalid social action '+field+': '+k,line)
        actions[k]={**r,'target':r['target'] if r['target']=='none' else expression(r['target'],'c',line),'condition':expression(r['condition'],'c',line),'goal':expression(r['goal'],'c',line),'effect':expression(r['effect'],'e',line)}
    def physical(e, key):
        name=e.get('name')
        if e.get('op')!='call' or name not in ('seq','set','change','spend','require','when'):
            raise StoryError('World law cannot perform a character action: '+key,line)
        if name=='seq':
            for a in e.get('args',[]): physical(a,key)
        elif name=='when': physical(e['args'][1],key)
    defaults=data.get('body_defaults',{})
    if not isinstance(defaults,dict) or any(not isinstance(k,str) or not k or type(v) is not int for k,v in defaults.items()):
        raise StoryError('Body defaults require integer property values.',line)
    constants=data.get('constants',{})
    if not isinstance(constants,dict) or any(not isinstance(k,str) or not k or type(v) is not int for k,v in constants.items()):
        raise StoryError('Constants require integer property values.',line)
    for key,row in data.get('belief_parameters',{}).items():
        if set(row) not in ({'rule','accepted'},{'claim'}) or ('accepted' in row and type(row['accepted']) is not bool):
            raise StoryError('Belief parameter requires a rule acceptance or a known claim: '+key,line)
    laws={}
    for key,r in data.get('laws',{}).items():
        if set(r)-{'hook','condition','effect','once','text','each'} or not {'hook','condition','effect','once'}<=set(r):
            raise StoryError('World law requires hook, condition, effect and once: '+key,line)
        if r['once'] not in ('world','day','slot') or r['hook'] not in ('begin','end','dawn','night'):
            raise StoryError('Invalid world law lifecycle: '+key,line)
        if 'each' in r and r['each']!='characters':
            raise StoryError('Unsupported world law collection: '+key,line)
        node=expression(r['effect'],'e',line)
        physical(node,key)
        laws[key]={**r,'condition':expression(r['condition'],'c',line),'effect':node}
    autonomous=rules(data.get('autonomous',{}),line)
    stimuli={}
    for key,r in data.get('stimuli',{}).items():
        if set(r)!={'hook','source','condition','rank','trust','history_job','request','title','text','log'} or r['hook']!='tick':
            raise StoryError('Incomplete authored stimulus: '+key,line)
        stimuli[key]={**r,'condition':expression(r['condition'],'c',line),'rank':expression(r['rank'],'i',line),
                      'trust':expression(r['trust'],'i',line),'request':expression(r['request'],'e',line)}
    recipes=data.get('recipes',{})
    for key,r in recipes.items():
        from story_base import split_list
        if set(r)-{'stations','actor_tags','duration','condition','effect','knowledge'} or not {'stations','actor_tags','duration','condition','effect'}<=set(r):
            raise StoryError('Incomplete work recipe: '+key,line)
        r['stations']=split_list(r['stations']); r['actor_tags']=split_list(r['actor_tags'])
        if len(r['stations'])!=2 or len(r['actor_tags'])!=2 or len(set(r['stations']))!=2 or len(set(r['actor_tags']))!=2 or type(r['duration']) not in (int,float) or r['duration']<=0:
            raise StoryError('Recipe requires two stations, actor tags and positive duration: '+key,line)
        r['condition']=expression(r['condition'],'c',line); r['effect']=expression(r['effect'],'e',line)
        physical(r['effect'],key)
        if 'knowledge' in r: r['knowledge']=expression(r['knowledge'],'c',line)
    return {'procedures':procedures,'actions':actions,'laws':laws,'autonomous':autonomous,'recipes':recipes,'stimuli':stimuli,
            **{k:data[k] for k in ('mediation','commitments','receipt_attitudes','schedule','bindings','body_defaults','belief_parameters','constants') if k in data}}
