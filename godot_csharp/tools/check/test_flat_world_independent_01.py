import unittest,sys,tempfile,pathlib
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]))
import world_script as w
BASE='chapter(id="a")\nr=room(id="r",name="房",scope="world")\np=character(id="p",name="人",room=r,scope="world")\n'
OPT='@option(id="x",label="选")\ndef x():\n    ```\n人：中文台词\n```\n'
class FlatFrontend(unittest.TestCase):
 def compile(self,*texts):
  with tempfile.TemporaryDirectory() as d:
   ps=[]
   for i,t in enumerate(texts):
    p=pathlib.Path(d)/f'{i}.world';p.write_text(t,encoding='utf-8');ps.append(p)
   return w.compile_paths(ps)
 def reject(self,t):
  with self.assertRaises(w.ScriptError):self.compile(t)
 def test_order(self):
  d=self.compile(BASE+OPT,'chapter(id="b",requires=["a"])\nr=ref(id="r")\n');self.assertEqual([x['id'] for x in d['chapters']],['a','b'])
 def test_chinese_source(self):
  d=self.compile(BASE+OPT);s=d['chapters'][0]['candidates'][0]['response'][0];self.assertEqual(s['text'],'中文台词');self.assertEqual(d['source_map'][s['source_id']]['line'],7)
 def test_unclosed(self):self.reject(BASE+OPT[:-4])
 def test_unknown_speaker(self):self.reject(BASE+OPT.replace('人：','陌生人：'))
 def test_duplicate(self):self.reject(BASE+OPT+OPT)
 def test_response_reference(self):self.reject(BASE+'@reaction(id="y",actor=p,response_to=["missing"],goal=True)\ndef y():\n    say(speaker=p,text="答")\n')
 def test_unknown_property(self):self.reject(BASE+'extend(target=p,properties={"mystery":1})\n')
 def test_dependency(self):self.reject('chapter(id="b",requires=["a"])\n')
 def test_nested(self):self.reject(BASE+'@option(id="x",label="选")\ndef x():\n    if True:\n        say(speaker=p,text="答")\n')
 def test_author_not_executed(self):
  self.reject(BASE+'__import__("builtins").print("EXECUTED")\n')
 def test_legacy(self):self.reject(BASE+'@behavior(id="x")\ndef x():\n    pass\n')
 def test_backslash(self):
  d=self.compile(BASE+OPT.replace('中文台词',r'C:\new\test'));self.assertEqual(d['chapters'][0]['candidates'][0]['response'][0]['text'],r'C:\new\test')
 def test_comment_fence(self):self.compile(BASE+'# ``` ordinary comment\n'+OPT)
 def test_string_fence(self):self.compile(BASE.replace('name="房"','name="房```间"')+OPT)
 def test_runtime_integer_boundary(self):self.reject(BASE.replace('room=r,scope="world"','room=r,scope="world",state={"large":2147483648}')+OPT)
if __name__=='__main__':unittest.main(verbosity=2)
