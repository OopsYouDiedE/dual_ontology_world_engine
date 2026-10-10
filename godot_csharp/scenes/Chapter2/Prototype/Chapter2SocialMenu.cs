namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Chapter2InteractionSession
{
    private void Talk(P who,List<Dictionary<string,object?>> output)
    {
        if (!Local(who)) throw new InvalidOperationException("The actor is not nearby.");
        _game.BeginInteractionLifetime("talk:" + _game.SlotIndex + ":" + who);
        _narrative.BeginInteraction();
        var name=Names.Of(who); var he=_game[who].Pro.Pronoun;
        var candidates=new List<(string Id,string Label,PlayerAction Action,int Score)>();
        void Offer(string id,string label,PlayerAction action)
        {
            var score=_game.HostSocialScore(who,action);
            if(score>0)candidates.Add((id,label,action,score));
        }
        foreach(var thing in _examined.Values.Select(c=>Text(c,"thing")).Where(t=>t!="").Distinct())
            Offer("escort:"+thing,_content.Format("menu_escort",("name",name),("thing",thing)),new(Cmd.Escort){Who=who,Thing=thing,Place=_game.Things_[thing].Place});
        foreach(var request in _content.Requests)
            Offer("request:"+request.Id,request.Label,new(Cmd.Ask){Who=who,ActionId=request.Id});
        foreach(var rule in _game.HostTellable().Where(_game.HostRuleRelevant))
            Offer("rule:"+rule.Id,_content.Format("menu_rule",("he",he),("rule",rule.Text)),new(Cmd.TellRule){Who=who,Spec=rule,Card=rule.Card});
        Offer("help",_content.Format("menu_help",("he",he)),new(Cmd.Help){Who=who});
        if(Game.Get(_game.Det.Body,T.Fed)>0)Offer("share",_content.Format("menu_share",("he",he)),new(Cmd.ShareFood){Who=who});
        var choices=new List<(string,string,Func<bool>,Action<List<Dictionary<string,object?>>>)>
        {
            ("ask",_content.Format("menu_ask",("name",name)),()=>Local(who),o=>{AskReaction(who,o);_continuations.Enqueue(next=>ResumeTalk(who,next));})
        };
        choices.AddRange(candidates.OrderByDescending(c=>c.Score).ThenBy(c=>c.Id,StringComparer.Ordinal).Take(5)
            .Select(c=>(c.Id,c.Label,(Func<bool>)(()=>Local(who)&&_game.HostSocialScore(who,c.Action)>0),(Action<List<Dictionary<string,object?>> >)(o=>Commit(c.Action,o)))));
        choices.Add(("cancel",_content.Text("menu_cancel"),()=>true,o=> { _game.EndInteractionLifetime(); _narrative.EndLifetime(DualOntologyWorldEngine.Core.LifetimeScope.Interaction); }));
        Menu(_content.Format("menu_talk",("name",name)),choices,output);
    }
}
