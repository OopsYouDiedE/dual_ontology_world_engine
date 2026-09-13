'use strict';
function createMindState(){return {ruleActive:false,status:'calm',trials:0};}
function resolveUtterance(state,word){
 const before=state.status;
 if(word==='seli'){
  state.trials++;
  if(state.ruleActive&&before==='wet'){
   state.status='calm';
   return {effect:'steam',triggered:true,title:'规则命中 · 火焰化为蒸汽',explanation:'薇洛原本处于潮湿状态。火焰到达时，脑内律则将它转为蒸汽；潮湿被消耗，薇洛没有燃烧。',input:'潮湿 + 火焰',rule:'潮湿遇火 → 蒸汽',output:'平静 · 未点燃'};
  }
  state.status='burning';
  return {effect:'fire',triggered:false,title:'言灵生效 · 火焰燃起',explanation:state.ruleActive?'潮湿前提不成立，固定规则未触发。火焰按言灵直接生效。':before==='wet'?'这层心象尚未写入水火相遇的规则。火焰直接覆盖潮湿，薇洛燃起火焰。':'当前没有附加规则，火焰言灵直接作用于薇洛的心象。',input:({calm:'平静',wet:'潮湿',burning:'燃烧',cold:'寒冷'}[before])+' + 火焰',rule:state.ruleActive?'前提不成立':'未写入规则',output:'燃烧'};
 }
 if(word==='telo'){
  state.trials++;state.status='wet';
  return {effect:'water',triggered:false,title:'言灵生效 · 潮湿已附着',explanation:'水覆盖薇洛的心象，目标变为潮湿。接下来施放火焰，可检查固定规则是否改变结果。',input:'水',rule:state.ruleActive?'等待火焰触发':'未写入规则',output:'潮湿'};
 }
 if(word==='lete'){
  state.trials++;state.status='cold';
  return {effect:'frost',triggered:false,title:'言灵生效 · 心象结霜',explanation:'寒霜覆盖了先前的状态。当前固定规则只响应“潮湿遇火”，这次施放没有触发它。',input:'冷',rule:'无匹配触发',output:'寒冷'};
 }
 throw new Error('请选择字典中的效果词。');
}
