using DuelView.Core;

public static partial class CampaignRuntime
{
    public static object F_c2_move_law(object actor, object origin, object target, object gate)
        => DvValueTemplate.Instantiate("template.c2_move_law", actor, origin, target, gate);

    public static object F_c2_move_action(object actor, object origin, object target, object gate)
        => DvValueTemplate.Instantiate("template.c2_move_action", actor, origin, target, gate);

    public static object F_c2_carry_moran_law(object origin, object target, object gate)
        => DvValueTemplate.Instantiate("template.c2_carry_moran_law", origin, target, gate);

    public static object F_c2_carry_moran_action(object origin, object target, object gate)
        => DvValueTemplate.Instantiate("template.c2_carry_moran_action", origin, target, gate);

    private static object DispatchMovementAction(object definition, DispatchContext context)
        => DvValueTemplate.Instantiate("template.bridge_movement_action", definition,
            context.ActorId, context.Origin, context.Target, context.Gate);
}
