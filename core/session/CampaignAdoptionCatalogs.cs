using System.Collections.Generic;
using DuelView.Compiled;
using DuelView.Core;
using static DuelView.Core.DVKernel;

public static partial class CampaignRuntime
{
    private static object ApplyAdoptionDeclarations(object domain, object ownedAdoptions, object declarations)
    {
        foreach (var declaration in R.Items(declarations))
            _ = F_dv_adopt_into(domain, ownedAdoptions, F_dv_get(declaration, "law"), F_dv_get(declaration, "mode"),
                F_dv_get(declaration, "confidence"), F_dv_get(declaration, "basis"));
        return true;
    }

    public static object F_c2_install_actor_laws_into(object v_domain, object v_owned_adoptions, object v_actor, object v_mode)
        => ApplyAdoptionDeclarations(v_domain, v_owned_adoptions, DvValueTemplate.Instantiate("template.c2_install_actor_laws_into_declarations", v_actor, v_mode));

    public static object F_c2_install_teacher_laws_into(object v_domain, object v_owned_adoptions, object v_mode)
        => ApplyAdoptionDeclarations(v_domain, v_owned_adoptions, DvValueTemplate.Instantiate("template.c2_install_teacher_laws_into_declarations", v_mode));

    public static object F_c2i_install_laws(object v_domain, object v_adoptions, object v_actor, object v_mode)
        => ApplyAdoptionDeclarations(v_domain, v_adoptions, DvValueTemplate.Instantiate("template.c2i_install_laws_declarations", v_actor, v_mode));

    public static object F_c2i_record_install(object v_domain, object v_adoptions, object v_actor, object v_mode)
        => ApplyAdoptionDeclarations(v_domain, v_adoptions, DvValueTemplate.Instantiate("template.c2i_record_install_declarations", v_actor, v_mode));

    public static object F_c2_safety_install(object v_domain, object v_adoptions, object v_actor, object v_mode)
        => ApplyAdoptionDeclarations(v_domain, v_adoptions, DvValueTemplate.Instantiate("template.c2_safety_install_declarations", v_actor, v_mode));

    public static object F_c2m_install(object v_domain, object v_adoptions, object v_actor, object v_mode)
        => ApplyAdoptionDeclarations(v_domain, v_adoptions, DvValueTemplate.Instantiate("template.c2m_install_declarations", v_actor, v_mode));

    public static object F_c2_extend_world(object v_world)
    {
        var result = F_c2_world_objects(v_world);
        var owned = F_dv_clone(F_dv_get(result, "adoptions"));
        var domain = F_dv_get(result, "domain");
        _ = ApplyAdoptionDeclarations(domain, owned, DvValueTemplate.Instantiate("template.c2_extend_world_before_declarations"));
        var people = F_campaign_people();
        foreach (var person in R.Items(people))
        {
            result = F_c2_inherit_finale_meal(result, person);
            _ = ApplyAdoptionDeclarations(domain, owned, DvValueTemplate.Instantiate("template.c2_extend_world_actor_declarations", person));
        }
        _ = ApplyAdoptionDeclarations(domain, owned, DvValueTemplate.Instantiate("template.c2_extend_world_after_declarations"));
        return F_dv_finish_owned_adoptions(result, owned);
    }
}
