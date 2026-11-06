// @mechanism: perception_and_erosion
// @layer: core/cognition
// @contracts: ConversationMind

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVCognitionKernel;
using static DuelView.Core.DVKernel;

// --- Source: ConversationExecution.cs ---
namespace DuelView.Core
{


public static partial class DVCognitionKernel
{
    public static object F_dvs_presentation_timing()
    {
        var timing = F_dv_get(R.content("data", "host.runtime.presentation_timing"), "value");
        var scale = R.Num(F_dv_get(timing, "game_minutes_per_real_second"));
        var minimum = R.Num(F_dv_get(timing, "minimum_seconds"));
        if (!double.IsFinite(scale) || scale <= 0 || !double.IsFinite(minimum) || minimum < 0)
            throw new InvalidDataException("Invalid DV presentation timing");
        return timing;
    }

    // Host instructions contain only the prepared participants and authored geometry.
    public static object F_dvs_conversation_execution(object pending, object state)
    {
        var action = F_dv_get(pending, "action");
        var speech = F_dv_find(action, "speech", false);
        if (speech is false) return false;
        var speaker = F_dv_get(pending, "actor");
        var physical = F_dv_get(state, "physical");
        var location = F_dvs_emission_location(speaker, speech, physical);
        if (R.Text(location).Length == 0) return false;
        var declared = R.Items(F_dv_find(action, "listeners", new List<object>()));
        var listeners = new List<object>();
        foreach (var actor in R.Items(F_dv_get(state, "actors")))
        {
            var identity = F_dv_get(actor, "id");
            if (R.Bool(F_dv_same(identity, F_dv_get(speaker, "id"))) || !declared.Any(id => R.Bool(F_dv_same(id, identity)))) continue;
            var cell = F_dv_read(physical, F_dv_get(actor, "location_ref"));
            if (R.Bool(F_dv_uncontested(cell)) && R.Bool(F_dv_same(F_dv_get(cell, "value"), location))) listeners.Add(identity);
        }
        // Remote recipients still receive through Core's declared channels;
        // only physically co-located peers participate in the native staging.
        if (listeners.Count == 0) return false;
        var profile = F_dv_find(F_dv_get(F_dv_get(pending, "actor"), "policy"), "sims_autonomy", false);
        if (profile is false) return false;
        var specification = F_dv_find(profile, "conversation", false);
        if (specification is false) return false;
        return F_dv_with_many(specification, new List<object> {
            F_dv_pair("participants", listeners),
            F_dv_pair("duration_seconds", R.mul(F_dv_get(F_dv_get(F_dv_get(pending, "model"), "actual"), "time"), 60d))
        });
    }
}
}
