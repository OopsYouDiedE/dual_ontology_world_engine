"""Typed Core AST expressions, using the existing story expression parser."""
from story_base import StoryError

CONDITIONS = {'exists': ('s',), 'tag': ('s','s','i'), 'at': ('s','s'),
              'related': ('s','s','s'), 'kind': ('s','s'), 'any_tag': ('s','s','i'),
              'observed': ('s','s','i','s'), 'param': ('s',), 'option': ('s','s')}
EFFECTS = {'set': ('s','s','i'), 'change': ('s','s','i'), 'spend': ('s','s','i'),
           'travel': ('s',), 'inspect': ('s',), 'take': ('s',), 'destroy': ('s',),
           'place': ('s','s'), 'unlock': ('s','s'), 'require': ('c',),
           'when': ('c','e'), 'wait': ('n','e'), 'wait_until': ('c','e'),
           'think': ('c',), 'procedure': ('s',), 'relate': ('s','s','s'),
           'temporary': ('s','s','i','n'), 'say': ('s','s','e')}

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
            signature = (CONDITIONS if typ == 'c' else EFFECTS if typ == 'e' else {}).get(name)
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
    if not {'procedures','actions'} <= set(data) or set(data)-{'procedures','actions','mediation','commitments','receipt_attitudes'}: raise StoryError('Execution requires procedures and actions.',line)
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
    return {'procedures':procedures,'actions':actions,**{k:data[k] for k in ('mediation','commitments','receipt_attitudes') if k in data}}
