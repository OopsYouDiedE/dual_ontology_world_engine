using DuelView.Compiled;
public static partial class DVProgram {
static object F_dv_pair(object v_a,object v_b) {
return new System.Collections.Generic.List<object>(2) {v_a,v_b};
}
static object F_dv_single(object v_a) {
return new System.Collections.Generic.List<object>(1) {v_a};
}
static object F_dv_list3(object v_a,object v_b,object v_c) {
return new System.Collections.Generic.List<object>(3) {v_a,v_b,v_c};
}
static object F_dv_and(object v_a,object v_b) {
if (R.Bool(v_a)) {
return v_b;
} else {
return false;
}
}
static object F_dv_or(object v_a,object v_b) {
if (R.Bool(v_a)) {
return true;
} else {
return v_b;
}
}
static object F_dv_require(object v_condition,object v_message) {
if (R.Bool(v_condition)) {
return true;
} else {
return R.fail(v_message);
}
}
static object F_dv_clone(object v_value) {
return R.copy_value(v_value);
}
static object F_dv_same(object v_a,object v_b) {
return R.same_value(v_a,v_b);
}
static object F_dv_append(object v_items,object v_value) {
object v_r__616 = F_dv_clone(v_items);
_ = R.push(v_r__616,F_dv_clone(v_value));
return v_r__616;
}
static object F_dv_has(object v_items,object v_value) {
return R.has_value(v_items,v_value);
}
static object F_dv_find(object v_record,object v_key,object v_fallback) {
return R.record_find(v_record,v_key,v_fallback);
}
static object F_dv_has_key(object v_record,object v_key) {
return R.not(R.lt(R.record_index(v_record,v_key),R.number("0")));
}
static object F_dv_record_like(object v_value) {
if (R.Bool(R.is_list(v_value))) {
object v_i__930 = R.number("0");
object v_valid__948 = true;
while (R.Bool(F_dv_and(v_valid__948,R.lt(v_i__930,R.count(v_value))))) {
object v_entry__1029 = R.at(v_value,v_i__930);
object v_local_1__1062 = false;
if (R.Bool(R.is_list(v_entry__1029))) {
v_local_1__1062 = R.eq(R.count(v_entry__1029),R.number("2"));
} else {
v_local_1__1062 = false;
}
v_valid__948 = v_local_1__1062;
v_i__930 = R.add(v_i__930,R.number("1"));
}
return v_valid__948;
} else {
return false;
}
}
static object F_dv_get(object v_record,object v_key) {
return R.record_find(v_record,v_key,"__dv_missing_field_sentinel__");
}
static object F_dv_with(object v_record,object v_key,object v_value) {
return R.record_with(v_record,v_key,v_value);
}
static object F_dv_m2(object v_k1,object v_v1,object v_k2,object v_v2) {
return F_dv_pair(F_dv_pair(v_k1,v_v1),F_dv_pair(v_k2,v_v2));
}
static object F_dv_without(object v_record,object v_key) {
object v_result__1582 = new System.Collections.Generic.List<object>(0) {};
object v_i__1602 = R.number("0");
while (R.Bool(R.lt(v_i__1602,R.count(v_record)))) {
object v_entry__1654 = R.at(v_record,v_i__1602);
if (R.Bool(R.eq(R.at(v_entry__1654,R.number("0")),v_key))) {
_ = false;
} else {
_ = R.push(v_result__1582,F_dv_clone(v_entry__1654));
}
v_i__1602 = R.add(v_i__1602,R.number("1"));
}
return v_result__1582;
}
static object F_dv_m3(object v_k1,object v_v1,object v_k2,object v_v2,object v_k3,object v_v3) {
return F_dv_with_many(new System.Collections.Generic.List<object>(0) {},F_dv_list3(F_dv_pair(v_k1,v_v1),F_dv_pair(v_k2,v_v2),F_dv_pair(v_k3,v_v3)));
}
static object F_dv_m4(object v_k1,object v_v1,object v_k2,object v_v2,object v_k3,object v_v3,object v_k4,object v_v4) {
object v_updates__1992 = F_dv_list3(F_dv_pair(v_k1,v_v1),F_dv_pair(v_k2,v_v2),F_dv_pair(v_k3,v_v3));
_ = R.push(v_updates__1992,F_dv_pair(v_k4,v_v4));
return F_dv_with_many(new System.Collections.Generic.List<object>(0) {},v_updates__1992);
}
static object F_dv_unit(object v_x) {
_ = F_dv_require(F_dv_and(R.not(R.lt(v_x,R.number("0"))),R.not(R.lt(R.number("1"),v_x))),"Expected weight in [0,1]");
return v_x;
}
static object F_dv_unknown() {
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("known",false),F_dv_pair("value",false),F_dv_pair("confidence",R.number("0")),F_dv_pair("time",R.sub(R.number("0"),R.number("1")))},new System.Collections.Generic.List<object>(2) {F_dv_pair("claims",new System.Collections.Generic.List<object>(0) {}),F_dv_pair("conflicted",false)});
}
static object F_dv_min(object v_a,object v_b) {
if (R.Bool(R.lt(v_a,v_b))) {
return v_a;
} else {
return v_b;
}
}
static object F_dv_max(object v_a,object v_b) {
if (R.Bool(R.lt(v_a,v_b))) {
return v_b;
} else {
return v_a;
}
}
static object F_dv_cell(object v_value,object v_confidence,object v_time,object v_claims) {
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("known",true),F_dv_pair("value",F_dv_clone(v_value)),F_dv_pair("confidence",F_dv_unit(v_confidence)),F_dv_pair("time",v_time)},new System.Collections.Generic.List<object>(3) {F_dv_pair("claims",new System.Collections.Generic.List<object>(0) {}),F_dv_pair("basis",v_claims),F_dv_pair("conflicted",false)});
}
static object F_dv_entity_ref(object v_id) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("type","entity-ref"),F_dv_pair("id",v_id)};
}
static object F_dv_ref(object v_object_id,object v_facet,object v_field) {
return new System.Collections.Generic.List<object>(4) {F_dv_pair("type","ref"),F_dv_pair("object",v_object_id),F_dv_pair("facet",v_facet),F_dv_pair("field",v_field)};
}
static object F_dv_scoped_ref(object v_subject,object v_object_id,object v_facet,object v_field) {
return F_dv_with(F_dv_ref(v_object_id,v_facet,v_field),"subject",v_subject);
}
static object F_dv_same_local_ref(object v_subject,object v_left,object v_right) {
if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_left,"subject",v_subject),v_subject))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_right,"subject",v_subject),v_subject)))))) {
return false;
} else {
}
return (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_left,"type"),F_dv_get(v_right,"type"))) ? (object)(R.eq(F_dv_get(v_left,"object"),F_dv_get(v_right,"object"))) : (object)(false))) ? (object)(R.eq(F_dv_get(v_left,"facet"),F_dv_get(v_right,"facet"))) : (object)(false))) ? (object)(R.eq(F_dv_get(v_left,"field"),F_dv_get(v_right,"field"))) : (object)(false));
}
static object F_dv_ref_key_text(object v_value) {
if (R.Bool(R.not(R.is_text(v_value)))) {
return false;
} else {
}
object v_i__3587 = R.number("0");
while (R.Bool(R.lt(v_i__3587,R.length(v_value)))) {
object v_unit__3637 = R.code(R.chr(v_value,v_i__3587));
if (R.Bool((R.Bool((R.Bool(R.not(R.lt(v_unit__3637,R.number("55296")))) ? (object)(R.not(R.lt(R.number("57343"),v_unit__3637))) : (object)(false))) ? (object)(true) : (object)(R.eq(v_unit__3637,R.number("65533")))))) {
return false;
} else {
}
v_i__3587 = R.add(v_i__3587,R.number("1"));
}
return true;
}
static object F_dv_ref_lookup_key(object v_ref) {
if (R.Bool(R.not(R.is_list(v_ref)))) {
return R.cat("J",R.json_stringify(v_ref));
} else {
}
object v_count__3983 = R.count(v_ref);
if (R.Bool((R.Bool(R.not(R.eq(v_count__3983,R.number("4")))) ? (object)(R.not(R.eq(v_count__3983,R.number("5")))) : (object)(false)))) {
return R.cat("J",R.json_stringify(v_ref));
} else {
}
object v_fields__4098 = new System.Collections.Generic.List<object>(5) {"type","object","facet","field","subject"};
object v_parts__4163 = new System.Collections.Generic.List<object>(0) {};
object v_i__4182 = R.number("0");
while (R.Bool(R.lt(v_i__4182,v_count__3983))) {
object v_pair__4221 = R.at(v_ref,v_i__4182);
if (R.Bool(R.not(R.is_list(v_pair__4221)))) {
return R.cat("J",R.json_stringify(v_ref));
} else {
}
if (R.Bool(R.not(R.eq(R.count(v_pair__4221),R.number("2"))))) {
return R.cat("J",R.json_stringify(v_ref));
} else {
}
if (R.Bool((R.Bool(R.not(R.eq(R.at(v_pair__4221,R.number("0")),R.at(v_fields__4098,v_i__4182)))) ? (object)(true) : (object)(R.not(F_dv_ref_key_text(R.at(v_pair__4221,R.number("1")))))))) {
return R.cat("J",R.json_stringify(v_ref));
} else {
}
_ = R.push(v_parts__4163,R.at(v_pair__4221,R.number("1")));
v_i__4182 = R.add(v_i__4182,R.number("1"));
}
if (R.Bool(R.not(R.eq(R.at(v_parts__4163,R.number("0")),"ref")))) {
return R.cat("J",R.json_stringify(v_ref));
} else {
}
object v_result__4665 = R.cat("R",R.cat(R.show(v_count__3983),":"));
v_i__4182 = R.number("0");
while (R.Bool(R.lt(v_i__4182,v_count__3983))) {
v_result__4665 = R.cat(v_result__4665,R.cat(R.show(R.length(R.at(v_parts__4163,v_i__4182))),R.cat(":",R.at(v_parts__4163,v_i__4182))));
v_i__4182 = R.add(v_i__4182,R.number("1"));
}
return v_result__4665;
}
static object F_dv_world(object v_id,object v_domain,object v_subject) {
_ = F_dv_require(F_dv_or(R.eq(v_domain,"physical"),R.eq(v_domain,"mind")),"Invalid world domain");
object v_local_1__5008 = false;
if (R.Bool(R.eq(v_domain,"physical"))) {
v_local_1__5008 = R.eq(v_subject,"");
} else {
v_local_1__5008 = R.lt(R.number("0"),R.length(v_subject));
}
_ = F_dv_require(v_local_1__5008,"World subject mismatch");
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",v_id),F_dv_pair("domain",v_domain),F_dv_pair("subject",v_subject),F_dv_pair("revision",R.number("0"))},new System.Collections.Generic.List<object>(4) {F_dv_pair("objects",new System.Collections.Generic.List<object>(0) {}),F_dv_pair("adoptions",new System.Collections.Generic.List<object>(0) {}),F_dv_pair("history",new System.Collections.Generic.List<object>(0) {}),F_dv_pair("seen",new System.Collections.Generic.List<object>(0) {})});
}
static object F_dv_domain(object v_world,object v_domain) {
return F_dv_require(R.eq(F_dv_get(v_world,"domain"),v_domain),"Wrong world domain");
}
static object F_dv_object(object v_id,object v_kind,object v_facets) {
object v_local_1__5483 = false;
if (R.Bool(R.eq(v_kind,"perceived"))) {
v_local_1__5483 = F_dv_unknown();
} else {
v_local_1__5483 = F_dv_field(v_kind);
}
return new System.Collections.Generic.List<object>(3) {F_dv_pair("id",v_id),F_dv_pair("kind",v_kind),F_dv_pair("facets",F_dv_with(v_facets,"identity",F_dv_with(F_dv_find(v_facets,"identity",new System.Collections.Generic.List<object>(0) {}),"kind",v_local_1__5483)))};
}
static object F_dv_add_object(object v_world,object v_object) {
object v_objects__5779 = F_dv_get(v_world,"objects");
object v_id__5811 = F_dv_get(v_object,"id");
_ = F_dv_require(R.not(F_dv_has_key(v_objects__5779,v_id__5811)),"Duplicate object identity");
return F_dv_with_many(v_world,new System.Collections.Generic.List<object>(2) {F_dv_pair("objects",F_dv_with(v_objects__5779,v_id__5811,v_object)),F_dv_pair("revision",R.add(F_dv_get(v_world,"revision"),R.number("1")))});
}
static object F_dv_read(object v_world,object v_ref) {
if (R.Bool(F_dv_has_key(v_ref,"subject"))) {
object v_subj__6081 = F_dv_get(v_ref,"subject");
if (R.Bool(R.not(R.eq(v_subj__6081,"")))) {
_ = F_dv_require(R.eq(v_subj__6081,F_dv_get(v_world,"subject")),"Cross-subject reference access");
} else {
}
} else {
}
object v_objects__6221 = F_dv_get(v_world,"objects");
object v_id__6253 = F_dv_get(v_ref,"object");
object v_key__6436 = new System.Collections.Generic.List<object>(2) {"dv_read",v_ref};
object v_cached__6467 = R.memo_get(v_world,v_key__6436);
if (R.Bool(R.at(v_cached__6467,R.number("0")))) {
return R.at(v_cached__6467,R.number("1"));
} else {
}
return R.memo_put(v_world,v_key__6436,F_dv_read_fields(v_objects__6221,v_id__6253,v_ref));
}
static object F_dv_read_fields(object v_objects,object v_id,object v_ref) {
if (R.Bool(F_dv_has_key(v_objects,v_id))) {
object v_facets__6694 = F_dv_get(F_dv_get(v_objects,v_id),"facets");
object v_facet__6742 = F_dv_get(v_ref,"facet");
if (R.Bool(F_dv_has_key(v_facets__6694,v_facet__6742))) {
object v_fields__6814 = F_dv_get(v_facets__6694,v_facet__6742);
object v_field_key__6861 = F_dv_get(v_ref,"field");
if (R.Bool(F_dv_has_key(v_fields__6814,v_field_key__6861))) {
return F_dv_clone(F_dv_get(v_fields__6814,v_field_key__6861));
} else {
return F_dv_unknown();
}
} else {
return F_dv_unknown();
}
} else {
return F_dv_unknown();
}
}
static object F_dv_store(object v_world,object v_ref,object v_cell) {
object v_objects__7166 = F_dv_get(v_world,"objects");
object v_id__7198 = F_dv_get(v_ref,"object");
_ = F_dv_require(F_dv_has_key(v_objects__7166,v_id__7198),"Unknown object identity");
object v_object__7289 = F_dv_get(v_objects__7166,v_id__7198);
object v_facets__7326 = F_dv_get(v_object__7289,"facets");
object v_facet__7357 = F_dv_get(v_ref,"facet");
object v_fields__7383 = F_dv_find(v_facets__7326,v_facet__7357,new System.Collections.Generic.List<object>(0) {});
object v_updated_fields__7427 = F_dv_with(v_fields__7383,F_dv_get(v_ref,"field"),v_cell);
object v_updated_object__7485 = F_dv_with(v_object__7289,"facets",F_dv_with(v_facets__7326,v_facet__7357,v_updated_fields__7427));
return F_dv_with(v_world,"objects",F_dv_with(v_objects__7166,v_id__7198,v_updated_object__7485));
}
static object F_dv_slice(object v_world,object v_object_id,object v_facet,object v_fields) {
object v_result__7698 = new System.Collections.Generic.List<object>(0) {};
object v_i__7718 = R.number("0");
while (R.Bool(R.lt(v_i__7718,R.count(v_fields)))) {
object v_field__7770 = R.at(v_fields,v_i__7718);
_ = R.push(v_result__7698,F_dv_pair(v_field__7770,F_dv_read(v_world,F_dv_ref(v_object_id,v_facet,v_field__7770))));
v_i__7718 = R.add(v_i__7718,R.number("1"));
}
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("world",F_dv_get(v_world,"id")),F_dv_pair("domain",F_dv_get(v_world,"domain")),F_dv_pair("subject",F_dv_get(v_world,"subject")),F_dv_pair("revision",F_dv_get(v_world,"revision"))},new System.Collections.Generic.List<object>(3) {F_dv_pair("object",v_object_id),F_dv_pair("facet",v_facet),F_dv_pair("fields",v_result__7698)});
}
static object F_dv_field(object v_value) {
return F_dv_cell(v_value,R.number("1"),R.number("0"),new System.Collections.Generic.List<object>(0) {});
}
static object F_dv_uncontested(object v_cell) {
return F_dv_and(F_dv_get(v_cell,"known"),R.not(F_dv_get(v_cell,"conflicted")));
}
static object F_dv_door(object v_id,object v_from,object v_to,object v_closed,object v_held,object v_inside,object v_outside,object v_automatic,object v_gap) {
return F_dv_object(v_id,"door",new System.Collections.Generic.List<object>(2) {F_dv_pair("link",new System.Collections.Generic.List<object>(2) {F_dv_pair("from",F_dv_field(F_dv_entity_ref(v_from))),F_dv_pair("to",F_dv_field(F_dv_entity_ref(v_to)))}),F_dv_pair("mechanical",F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("closed",F_dv_field(v_closed)),F_dv_pair("held",F_dv_field(v_held)),F_dv_pair("inside_latch",F_dv_field(v_inside)),F_dv_pair("outside_latch",F_dv_field(v_outside))},new System.Collections.Generic.List<object>(3) {F_dv_pair("automatic",F_dv_field(v_automatic)),F_dv_pair("gap",F_dv_field(v_gap)),F_dv_pair("sealed",F_dv_field(false))}))});
}
static object F_dv_channel(object v_id,object v_from,object v_to,object v_medium,object v_enabled,object v_gain,object v_gate) {
_ = F_dv_unit(v_gain);
return F_dv_object(v_id,"channel",new System.Collections.Generic.List<object>(2) {F_dv_pair("link",new System.Collections.Generic.List<object>(4) {F_dv_pair("from",F_dv_field(F_dv_entity_ref(v_from))),F_dv_pair("to",F_dv_field(F_dv_entity_ref(v_to))),F_dv_pair("medium",F_dv_field(v_medium)),F_dv_pair("gate",F_dv_field(v_gate))}),F_dv_pair("transport",new System.Collections.Generic.List<object>(3) {F_dv_pair("enabled",F_dv_field(v_enabled)),F_dv_pair("gain",F_dv_field(v_gain)),F_dv_pair("bands",F_dv_field(new System.Collections.Generic.List<object>(0) {}))})});
}
static object F_dv_with_many(object v_record,object v_updates) {
return R.record_with_many(v_record,v_updates);
}
static object F_dv_with_path(object v_record,object v_keys,object v_depth,object v_value) {
object v_key__9273 = R.at(v_keys,v_depth);
object v_result__9299 = new System.Collections.Generic.List<object>(0) {};
object v_found__9319 = false;
object v_i__9341 = R.number("0");
while (R.Bool(R.lt(v_i__9341,R.count(v_record)))) {
object v_entry__9391 = R.at(v_record,v_i__9341);
if (R.Bool(R.eq(R.at(v_entry__9391,R.number("0")),v_key__9273))) {
object v_next__9453 = false;
if (R.Bool(R.eq(R.add(v_depth,R.number("1")),R.count(v_keys)))) {
v_next__9453 = F_dv_clone(v_value);
} else {
v_next__9453 = F_dv_with_path(F_dv_get(v_record,v_key__9273),v_keys,R.add(v_depth,R.number("1")),v_value);
}
_ = R.push(v_result__9299,F_dv_pair(F_dv_clone(R.at(v_entry__9391,R.number("0"))),v_next__9453));
v_found__9319 = true;
} else {
_ = R.push(v_result__9299,R.snapshot(v_entry__9391));
}
v_i__9341 = R.add(v_i__9341,R.number("1"));
}
if (R.Bool(R.not(v_found__9319))) {
object v_next__9846 = false;
if (R.Bool(R.eq(R.add(v_depth,R.number("1")),R.count(v_keys)))) {
v_next__9846 = F_dv_clone(v_value);
} else {
v_next__9846 = F_dv_with_path(new System.Collections.Generic.List<object>(0) {},v_keys,R.add(v_depth,R.number("1")),v_value);
}
_ = R.push(v_result__9299,F_dv_pair(F_dv_clone(v_key__9273),v_next__9846));
} else {
}
return v_result__9299;
}
static object F_dv_stage_store_owned(object v_owned,object v_ref,object v_cell) {
object v_objects__10296 = F_dv_get(v_owned,"objects");
_ = F_dv_require(F_dv_has_key(v_objects__10296,F_dv_get(v_ref,"object")),"Unknown object identity");
object v_object__10403 = F_dv_get(v_objects__10296,F_dv_get(v_ref,"object"));
object v_facets__10448 = F_dv_get(v_object__10403,"facets");
object v_fields__10479 = F_dv_find(v_facets__10448,F_dv_get(v_ref,"facet"),new System.Collections.Generic.List<object>(0) {});
object v_updated__10527 = F_dv_with(v_object__10403,"facets",F_dv_with(v_facets__10448,F_dv_get(v_ref,"facet"),F_dv_with(v_fields__10479,F_dv_get(v_ref,"field"),v_cell)));
object v_i__10633 = R.number("0");
while (R.Bool(R.lt(v_i__10633,R.count(v_objects__10296)))) {
if (R.Bool(R.eq(R.at(R.at(v_objects__10296,v_i__10633),R.number("0")),F_dv_get(v_ref,"object")))) {
_ = R.replace(v_objects__10296,v_i__10633,F_dv_pair(F_dv_clone(R.at(R.at(v_objects__10296,v_i__10633),R.number("0"))),F_dv_clone(v_updated__10527)));
} else {
}
v_i__10633 = R.add(v_i__10633,R.number("1"));
}
return v_owned;
}
static object F_dv_true() {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("op","constant"),F_dv_pair("value","true")};
}
static object F_dv_fact(object v_ref,object v_expected) {
return new System.Collections.Generic.List<object>(3) {F_dv_pair("op","equals"),F_dv_pair("ref",v_ref),F_dv_pair("value",v_expected)};
}
static object F_dv_at_least(object v_ref,object v_minimum) {
return new System.Collections.Generic.List<object>(3) {F_dv_pair("op","at_least"),F_dv_pair("ref",v_ref),F_dv_pair("value",v_minimum)};
}
static object F_dv_all(object v_conditions) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("op","all"),F_dv_pair("items",v_conditions)};
}
static object F_dv_not(object v_condition) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("op","not"),F_dv_pair("item",v_condition)};
}
static object F_dv_matches(object v_object_id,object v_facet,object v_fields) {
return F_dv_all(R.Block(() => { object comp_source_11469 = v_fields;var comp_result_11469 = new System.Collections.Generic.List<object>();for (int comp_i_11469 = 0; comp_i_11469 < R.Items(comp_source_11469).Count; comp_i_11469++) {object v_entry__comp_11469 = R.at(comp_source_11469, (double)comp_i_11469);if (R.Bool(true)) comp_result_11469.Add(F_dv_fact(F_dv_ref(v_object_id,v_facet,R.at(v_entry__comp_11469,R.number("0"))),R.at(v_entry__comp_11469,R.number("1"))));}return (object)comp_result_11469;}));
}
static object F_dv_writes(object v_object_id,object v_facet,object v_fields) {
return R.Block(() => { object comp_source_11586 = v_fields;var comp_result_11586 = new System.Collections.Generic.List<object>();for (int comp_i_11586 = 0; comp_i_11586 < R.Items(comp_source_11586).Count; comp_i_11586++) {object v_entry__comp_11586 = R.at(comp_source_11586, (double)comp_i_11586);if (R.Bool(true)) comp_result_11586.Add(F_dv_assign(F_dv_ref(v_object_id,v_facet,R.at(v_entry__comp_11586,R.number("0"))),R.at(v_entry__comp_11586,R.number("1"))));}return (object)comp_result_11586;});
}
static object F_dv_condition_inputs(object v_condition) {
object v_unsupported__11782 = new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",false),F_dv_pair("reads",new System.Collections.Generic.List<object>(0) {})};
if (R.Bool((R.Bool(R.not(F_dv_record_like(v_condition))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition,"op")))))) {
return v_unsupported__11782;
} else {
}
object v_op__11937 = F_dv_get(v_condition,"op");
if (R.Bool(R.eq(v_op__11937,"constant"))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",(R.Bool(F_dv_has_key(v_condition,"value")) ? (object)(F_dv_has(new System.Collections.Generic.List<object>(3) {"true","false","unknown"},F_dv_find(v_condition,"value",""))) : (object)(false))),F_dv_pair("reads",new System.Collections.Generic.List<object>(0) {})};
} else {
}
if (R.Bool(R.eq(v_op__11937,"not"))) {
if (R.Bool(R.not(F_dv_has_key(v_condition,"item")))) {
return v_unsupported__11782;
} else {
}
return F_dv_condition_inputs(F_dv_get(v_condition,"item"));
} else {
}
if (R.Bool(R.eq(v_op__11937,"all"))) {
if (R.Bool((R.Bool(R.not(F_dv_has_key(v_condition,"items"))) ? (object)(true) : (object)(R.not(R.is_list(F_dv_get(v_condition,"items"))))))) {
return v_unsupported__11782;
} else {
}
object v_reads__12420 = new System.Collections.Generic.List<object>(0) {};
object v_complete__12443 = true;
object v_i__12471 = R.number("0");
while (R.Bool(R.lt(v_i__12471,R.count(F_dv_get(v_condition,"items"))))) {
object v_part__12538 = F_dv_condition_inputs(R.at(F_dv_get(v_condition,"items"),v_i__12471));
v_complete__12443 = (R.Bool(v_complete__12443) ? (object)(F_dv_get(v_part__12538,"complete")) : (object)(false));
object v_j__12651 = R.number("0");
while (R.Bool(R.lt(v_j__12651,R.count(F_dv_get(v_part__12538,"reads"))))) {
if (R.Bool(R.not(F_dv_has(v_reads__12420,R.at(F_dv_get(v_part__12538,"reads"),v_j__12651))))) {
_ = R.push(v_reads__12420,F_dv_clone(R.at(F_dv_get(v_part__12538,"reads"),v_j__12651)));
} else {
}
v_j__12651 = R.add(v_j__12651,R.number("1"));
}
v_i__12471 = R.add(v_i__12471,R.number("1"));
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",v_complete__12443),F_dv_pair("reads",v_reads__12420)};
} else {
}
if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(v_op__11937,"equals"))) ? (object)(R.not(R.eq(v_op__11937,"at_least"))) : (object)(false))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition,"ref"))))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition,"value")))))) {
return v_unsupported__11782;
} else {
}
object v_ref__13071 = F_dv_get(v_condition,"ref");
if (R.Bool((R.Bool(R.not(F_dv_record_like(v_ref__13071))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_ref__13071,"type",""),"ref")))))) {
return v_unsupported__11782;
} else {
}
if (R.Bool((R.Bool((R.Bool(R.not(R.is_text(F_dv_find(v_ref__13071,"object",false)))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_ref__13071,"facet",false)))))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_ref__13071,"field",false))))))) {
return v_unsupported__11782;
} else {
}
if (R.Bool(R.not(R.is_text(F_dv_find(v_ref__13071,"subject",""))))) {
return v_unsupported__11782;
} else {
}
if (R.Bool((R.Bool(R.eq(v_op__11937,"at_least")) ? (object)(R.not(R.is_numeric_value(F_dv_get(v_condition,"value")))) : (object)(false)))) {
return v_unsupported__11782;
} else {
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",true),F_dv_pair("reads",new System.Collections.Generic.List<object>(1) {F_dv_clone(v_ref__13071)})};
}
static object F_dv_time_ref_relation(object v_ref,object v_clock) {
if (R.Bool((R.Bool(R.not(F_dv_record_like(v_clock))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_clock,"type",""),"ref")))))) {
return "unknown";
} else {
}
if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.is_text(F_dv_find(v_clock,"object",false)))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_clock,"facet",false)))))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_clock,"field",false)))))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_clock,"subject",""))))))) {
return "unknown";
} else {
}
if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_ref,"object"),F_dv_get(v_clock,"object")))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_ref,"facet"),F_dv_get(v_clock,"facet")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_ref,"field"),F_dv_get(v_clock,"field"))))))) {
return "different";
} else {
}
if (R.Bool(R.not(R.eq(F_dv_find(v_ref,"subject",""),F_dv_find(v_clock,"subject",""))))) {
return "unknown";
} else {
}
return "same";
}
static object F_dv_time_boundary(object v_condition,object v_clock,object v_now,object v_until,object v_policy) {
_ = F_dv_require((R.Bool(R.eq(v_policy,"invalidate")) ? (object)(true) : (object)(R.eq(v_policy,"minute"))),"Unknown time analysis policy");
_ = F_dv_require((R.Bool(R.is_numeric_value(v_now)) ? (object)(R.is_numeric_value(v_until)) : (object)(false)),"Invalid time analysis interval");
if (R.Bool(R.lt(v_until,v_now))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",false),F_dv_pair("next",v_now)};
} else {
}
object v_fallback__14828 = new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",false),F_dv_pair("next",F_dv_min(v_until,R.add(v_now,R.number("1"))))};
if (R.Bool((R.Bool(R.not(F_dv_record_like(v_condition))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition,"op")))))) {
return v_fallback__14828;
} else {
}
object v_op__14996 = F_dv_get(v_condition,"op");
if (R.Bool(R.eq(v_op__14996,"constant"))) {
if (R.Bool((R.Bool(R.not(F_dv_has_key(v_condition,"value"))) ? (object)(true) : (object)(R.not(F_dv_has(new System.Collections.Generic.List<object>(3) {"true","false","unknown"},F_dv_get(v_condition,"value"))))))) {
return v_fallback__14828;
} else {
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",true),F_dv_pair("next",v_until)};
} else {
}
if (R.Bool(R.eq(v_op__14996,"not"))) {
if (R.Bool(R.not(F_dv_has_key(v_condition,"item")))) {
return v_fallback__14828;
} else {
}
return F_dv_time_boundary(F_dv_get(v_condition,"item"),v_clock,v_now,v_until,v_policy);
} else {
}
if (R.Bool(R.eq(v_op__14996,"all"))) {
if (R.Bool((R.Bool(R.not(F_dv_has_key(v_condition,"items"))) ? (object)(true) : (object)(R.not(R.is_list(F_dv_get(v_condition,"items"))))))) {
return v_fallback__14828;
} else {
}
object v_next__15535 = v_until;
object v_i__15560 = R.number("0");
while (R.Bool(R.lt(v_i__15560,R.count(F_dv_get(v_condition,"items"))))) {
object v_part__15627 = F_dv_time_boundary(R.at(F_dv_get(v_condition,"items"),v_i__15560),v_clock,v_now,v_until,v_policy);
if (R.Bool(R.not(F_dv_get(v_part__15627,"complete")))) {
return v_fallback__14828;
} else {
}
v_next__15535 = F_dv_min(v_next__15535,F_dv_get(v_part__15627,"next"));
v_i__15560 = R.add(v_i__15560,R.number("1"));
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",true),F_dv_pair("next",v_next__15535)};
} else {
}
object v_inputs__15885 = F_dv_condition_inputs(v_condition);
if (R.Bool(R.not(F_dv_get(v_inputs__15885,"complete")))) {
return v_fallback__14828;
} else {
}
if (R.Bool(R.eq(v_clock,false))) {
if (R.Bool(R.eq(v_policy,"minute"))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",true),F_dv_pair("next",v_until)};
} else {
}
return v_fallback__14828;
} else {
}
object v_relation__16116 = F_dv_time_ref_relation(F_dv_get(v_condition,"ref"),v_clock);
if (R.Bool(R.eq(v_relation__16116,"unknown"))) {
return v_fallback__14828;
} else {
}
if (R.Bool(R.eq(v_relation__16116,"different"))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",true),F_dv_pair("next",v_until)};
} else {
}
if (R.Bool(R.not(R.is_numeric_value(F_dv_get(v_condition,"value"))))) {
return v_fallback__14828;
} else {
}
if (R.Bool(R.eq(v_policy,"minute"))) {
if (R.Bool(R.not(R.eq(v_op__14996,"at_least")))) {
return v_fallback__14828;
} else {
}
if (R.Bool(R.lt(v_until,F_dv_get(v_condition,"value")))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",true),F_dv_pair("next",v_until)};
} else {
}
if (R.Bool(R.lt(v_now,F_dv_get(v_condition,"value")))) {
object v_delta__16601 = R.sub(F_dv_get(v_condition,"value"),v_now);
object v_steps__16647 = R.number("1");
while (R.Bool(R.lt(v_steps__16647,v_delta__16601))) {
v_steps__16647 = R.add(v_steps__16647,R.number("1"));
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",true),F_dv_pair("next",F_dv_min(v_until,R.add(v_now,v_steps__16647)))};
} else {
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",true),F_dv_pair("next",v_until)};
} else {
}
if (R.Bool(R.lt(v_now,F_dv_get(v_condition,"value")))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",true),F_dv_pair("next",F_dv_min(v_until,F_dv_get(v_condition,"value")))};
} else {
}
if (R.Bool((R.Bool(R.eq(v_op__14996,"equals")) ? (object)(R.eq(v_now,F_dv_get(v_condition,"value"))) : (object)(false)))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",true),F_dv_pair("next",v_now)};
} else {
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("complete",true),F_dv_pair("next",v_until)};
}
static object F_dv_analyze_time(object v_condition,object v_clock,object v_now,object v_until,object v_policy) {
object v_boundary__17160 = F_dv_time_boundary(v_condition,v_clock,v_now,v_until,v_policy);
object v_inputs__17234 = F_dv_condition_inputs(v_condition);
object v_solver__17282 = "minute-step";
object v_reason__17313 = "unsupported-predicate";
if (R.Bool(F_dv_get(v_boundary__17160,"complete"))) {
v_solver__17282 = "event-boundary";
v_reason__17313 = "declared-boundary";
} else {
}
if (R.Bool((R.Bool((R.Bool(R.not(F_dv_get(v_boundary__17160,"complete"))) ? (object)(F_dv_get(v_inputs__17234,"complete")) : (object)(false))) ? (object)((R.Bool(R.eq(F_dv_find(v_condition,"op",""),"equals")) ? (object)(true) : (object)(R.eq(F_dv_find(v_condition,"op",""),"at_least")))) : (object)(false)))) {
if (R.Bool(R.eq(v_clock,false))) {
v_reason__17313 = "clock-not-declared";
} else {
if (R.Bool(R.eq(F_dv_time_ref_relation(F_dv_get(v_condition,"ref"),v_clock),"unknown"))) {
v_reason__17313 = "ambiguous-clock-reference";
} else {
if (R.Bool((R.Bool(R.eq(v_policy,"minute")) ? (object)(R.eq(F_dv_get(v_condition,"op"),"equals")) : (object)(false)))) {
v_reason__17313 = "clock-equality-needs-sampling";
} else {
}
}
}
} else {
}
if (R.Bool(R.lt(v_until,v_now))) {
v_solver__17282 = "none";
v_reason__17313 = "expired-interval";
} else {
}
return F_dv_with_many(v_boundary__17160,new System.Collections.Generic.List<object>(9) {F_dv_pair("solver",v_solver__17282),F_dv_pair("reason",v_reason__17313),F_dv_pair("policy",v_policy),F_dv_pair("from",v_now),F_dv_pair("until",v_until),F_dv_pair("reads",F_dv_get(v_inputs__17234,"reads")),F_dv_pair("scope","declaration"),F_dv_pair("assumptions",new System.Collections.Generic.List<object>(2) {"non-clock inputs remain stable","caller rechecks state changes and ordered effects"}),F_dv_pair("numeric_semantics","runtime-values")});
}
static object F_dv_analyze_law(object v_world,object v_name) {
object v_adoption__18525 = F_dv_get(F_dv_get(v_world,"adoptions"),v_name);
object v_law__18574 = F_dv_get(v_adoption__18525,"law");
object v_inputs__18601 = F_dv_condition_inputs(F_dv_get(v_law__18574,"premise"));
object v_unknown__18651 = new System.Collections.Generic.List<object>(0) {};
object v_conflicted__18672 = new System.Collections.Generic.List<object>(0) {};
object v_i__18696 = R.number("0");
while (R.Bool(R.lt(v_i__18696,R.count(F_dv_get(v_inputs__18601,"reads"))))) {
object v_ref__18752 = R.at(F_dv_get(v_inputs__18601,"reads"),v_i__18696);
object v_cell__18786 = F_dv_read(v_world,v_ref__18752);
if (R.Bool(R.not(F_dv_get(v_cell__18786,"known")))) {
_ = R.push(v_unknown__18651,v_ref__18752);
} else {
}
if (R.Bool(F_dv_get(v_cell__18786,"conflicted"))) {
_ = R.push(v_conflicted__18672,v_ref__18752);
} else {
}
v_i__18696 = R.add(v_i__18696,R.number("1"));
}
object v_truth__18965 = "unknown";
if (R.Bool(F_dv_get(v_inputs__18601,"complete"))) {
v_truth__18965 = F_dv_truth(v_world,F_dv_get(v_law__18574,"premise"));
} else {
}
return new System.Collections.Generic.List<object>(15) {F_dv_pair("world",F_dv_get(v_world,"id")),F_dv_pair("subject",F_dv_get(v_world,"subject")),F_dv_pair("revision",F_dv_get(v_world,"revision")),F_dv_pair("law",v_name),F_dv_pair("law_version",F_dv_get(v_law__18574,"version")),F_dv_pair("mode",F_dv_get(v_adoption__18525,"mode")),F_dv_pair("active",F_dv_get(v_adoption__18525,"active")),F_dv_pair("premise",v_truth__18965),F_dv_pair("complete",F_dv_get(v_inputs__18601,"complete")),F_dv_pair("reads",F_dv_get(v_inputs__18601,"reads")),F_dv_pair("unknown",v_unknown__18651),F_dv_pair("conflicted",v_conflicted__18672),F_dv_pair("effects_verified",false),F_dv_pair("scope","input-snapshot"),F_dv_pair("numeric_semantics","runtime-values")};
}
static object F_dv_truth(object v_world,object v_proposition) {
if (R.Bool(R.is_list(v_world))) {
if (R.Bool(R.eq(F_dv_get(v_proposition,"op"),"constant"))) {
object v_value__19790 = F_dv_get(v_proposition,"value");
if (R.Bool(R.not(R.is_list(v_value__19790)))) {
return v_value__19790;
} else {
}
} else {
}
} else {
}
object v_key__19891 = new System.Collections.Generic.List<object>(2) {"dv_truth",v_proposition};
object v_cached__19931 = R.memo_get(v_world,v_key__19891);
if (R.Bool(R.at(v_cached__19931,R.number("0")))) {
return R.at(v_cached__19931,R.number("1"));
} else {
}
return R.memo_put(v_world,v_key__19891,F_dv_truth_uncached(v_world,v_proposition));
}
static object F_dv_truth_uncached(object v_world,object v_proposition) {
object v_op__20132 = F_dv_get(v_proposition,"op");
if (R.Bool(R.eq(v_op__20132,"constant"))) {
return F_dv_get(v_proposition,"value");
} else {
if (R.Bool(R.eq(v_op__20132,"not"))) {
object v_inner__20264 = F_dv_truth(v_world,F_dv_get(v_proposition,"item"));
if (R.Bool(R.eq(v_inner__20264,"unknown"))) {
return "unknown";
} else {
if (R.Bool(R.eq(v_inner__20264,"true"))) {
return "false";
} else {
return "true";
}
}
} else {
if (R.Bool(R.eq(v_op__20132,"all"))) {
object v_items__20587 = F_dv_get(v_proposition,"items");
object v_i__20633 = R.number("0");
object v_result__20659 = "true";
while (R.Bool(R.lt(v_i__20633,R.count(v_items__20587)))) {
object v_truth__20744 = F_dv_truth(v_world,R.at(v_items__20587,v_i__20633));
if (R.Bool(R.eq(v_truth__20744,"false"))) {
return "false";
} else {
if (R.Bool(F_dv_and(R.eq(v_truth__20744,"unknown"),R.eq(v_result__20659,"true")))) {
v_result__20659 = "unknown";
} else {
}
}
v_i__20633 = R.add(v_i__20633,R.number("1"));
}
return v_result__20659;
} else {
object v_cell__21110 = F_dv_read(v_world,F_dv_get(v_proposition,"ref"));
if (R.Bool(F_dv_or(R.not(F_dv_get(v_cell__21110,"known")),F_dv_get(v_cell__21110,"conflicted")))) {
return "unknown";
} else {
if (R.Bool(R.eq(v_op__20132,"equals"))) {
if (R.Bool(F_dv_same(F_dv_get(v_cell__21110,"value"),F_dv_get(v_proposition,"value")))) {
return "true";
} else {
return "false";
}
} else {
if (R.Bool(R.eq(v_op__20132,"at_least"))) {
if (R.Bool(R.lt(F_dv_get(v_cell__21110,"value"),F_dv_get(v_proposition,"value")))) {
return "false";
} else {
return "true";
}
} else {
return R.fail("Unknown proposition operator");
}
}
}
}
}
}
}
static object F_dv_assign(object v_ref,object v_value) {
return new System.Collections.Generic.List<object>(3) {F_dv_pair("op","assign"),F_dv_pair("ref",v_ref),F_dv_pair("value",v_value)};
}
static object F_dv_increment(object v_ref,object v_amount) {
return new System.Collections.Generic.List<object>(3) {F_dv_pair("op","increment"),F_dv_pair("ref",v_ref),F_dv_pair("value",v_amount)};
}
static object F_dv_axiom(object v_name,object v_version,object v_premise,object v_effects) {
return new System.Collections.Generic.List<object>(4) {F_dv_pair("name",v_name),F_dv_pair("version",v_version),F_dv_pair("premise",F_dv_clone(v_premise)),F_dv_pair("effects",F_dv_clone(v_effects))};
}
static object F_dv_contract(object v_name,object v_version,object v_premise,object v_effects,object v_postcondition) {
return F_dv_with(F_dv_axiom(v_name,v_version,v_premise,v_effects),"postcondition",v_postcondition);
}
static object F_dv_instance(object v_id,object v_law) {
return F_dv_with_many(v_law,new System.Collections.Generic.List<object>(2) {F_dv_pair("schema",F_dv_get(v_law,"name")),F_dv_pair("name",v_id)});
}
static object F_dv_checked_adoption(object v_domain,object v_adoptions,object v_law,object v_mode,object v_confidence,object v_basis) {
_ = F_dv_unit(v_confidence);
object v_local_1__22660 = false;
if (R.Bool(R.eq(v_domain,"physical"))) {
v_local_1__22660 = R.eq(v_confidence,R.number("1"));
} else {
v_local_1__22660 = true;
}
_ = F_dv_require(v_local_1__22660,"Physical laws require certainty");
_ = F_dv_require(R.lt(R.number("0"),R.count(v_basis)),"Adoption needs a basis");
_ = F_dv_require(R.not(F_dv_has_key(v_adoptions,F_dv_get(v_law,"name"))),"Duplicate law instance: provide a distinct instance identity");
object v_local_2__23025 = false;
if (R.Bool(R.eq(v_domain,"physical"))) {
v_local_2__23025 = R.eq(v_mode,"enforce");
} else {
v_local_2__23025 = F_dv_and(R.eq(v_domain,"mind"),F_dv_or(R.eq(v_mode,"predict"),F_dv_or(R.eq(v_mode,"infer"),R.eq(v_mode,"norm"))));
}
_ = F_dv_require(v_local_2__23025,"Adoption mode is incompatible with world");
return F_dv_with(new System.Collections.Generic.List<object>(4) {F_dv_pair("law",v_law),F_dv_pair("mode",v_mode),F_dv_pair("confidence",v_confidence),F_dv_pair("basis",v_basis)},"active",true);
}
static object F_dv_adopt(object v_world,object v_law,object v_mode,object v_confidence,object v_basis) {
object v_adoption__23469 = F_dv_checked_adoption(F_dv_get(v_world,"domain"),F_dv_get(v_world,"adoptions"),v_law,v_mode,v_confidence,v_basis);
return F_dv_with_many(v_world,new System.Collections.Generic.List<object>(2) {F_dv_pair("adoptions",F_dv_with(F_dv_get(v_world,"adoptions"),F_dv_get(v_law,"name"),v_adoption__23469)),F_dv_pair("revision",R.add(F_dv_get(v_world,"revision"),R.number("1")))});
}
static object F_dv_adopt_into(object v_domain,object v_owned_adoptions,object v_law,object v_mode,object v_confidence,object v_basis) {
object v_adoption__23858 = F_dv_checked_adoption(v_domain,v_owned_adoptions,v_law,v_mode,v_confidence,v_basis);
_ = R.push(v_owned_adoptions,F_dv_pair(F_dv_get(v_law,"name"),v_adoption__23858));
return true;
}
static object F_dv_finish_owned_adoptions(object v_world,object v_owned_adoptions) {
object v_previous_count__24081 = R.count(F_dv_get(v_world,"adoptions"));
_ = F_dv_require(R.not(R.lt(R.count(v_owned_adoptions),v_previous_count__24081)),"Adoption builder removed inherited entries");
object v_i__24238 = R.number("0");
while (R.Bool(R.lt(v_i__24238,v_previous_count__24081))) {
_ = F_dv_require(F_dv_same(R.at(F_dv_get(v_world,"adoptions"),v_i__24238),R.at(v_owned_adoptions,v_i__24238)),"Adoption builder changed inherited entries");
v_i__24238 = R.add(v_i__24238,R.number("1"));
}
if (R.Bool(R.eq(v_previous_count__24081,R.count(v_owned_adoptions)))) {
return v_world;
} else {
}
return F_dv_with_many(v_world,new System.Collections.Generic.List<object>(2) {F_dv_pair("adoptions",v_owned_adoptions),F_dv_pair("revision",R.sub(R.add(F_dv_get(v_world,"revision"),R.count(v_owned_adoptions)),v_previous_count__24081))});
}
static object F_dv_suspend(object v_world,object v_name) {
object v_adoptions__24652 = F_dv_get(v_world,"adoptions");
return F_dv_with_many(v_world,new System.Collections.Generic.List<object>(2) {F_dv_pair("adoptions",F_dv_with(v_adoptions__24652,v_name,F_dv_with(F_dv_get(v_adoptions__24652,v_name),"active",false))),F_dv_pair("revision",R.add(F_dv_get(v_world,"revision"),R.number("1")))});
}
static object F_dv_reconsider(object v_world,object v_law,object v_mode,object v_confidence,object v_basis) {
object v_name__24901 = F_dv_get(v_law,"name");
object v_previous__24925 = F_dv_get(F_dv_get(v_world,"adoptions"),v_name__24901);
object v_updated__24974 = F_dv_adopt(F_dv_with(v_world,"adoptions",F_dv_without(F_dv_get(v_world,"adoptions"),v_name__24901)),v_law,v_mode,v_confidence,v_basis);
return F_dv_with(v_updated__24974,"history",F_dv_append(F_dv_get(v_world,"history"),new System.Collections.Generic.List<object>(4) {F_dv_pair("kind","reconsideration"),F_dv_pair("previous",v_previous__24925),F_dv_pair("current",F_dv_get(F_dv_get(v_updated__24974,"adoptions"),v_name__24901)),F_dv_pair("basis",v_basis)}));
}
static object F_dv_premise_confidence(object v_world,object v_proposition) {
object v_op__25318 = F_dv_get(v_proposition,"op");
if (R.Bool(R.eq(v_op__25318,"constant"))) {
return R.number("1");
} else {
if (R.Bool(R.eq(v_op__25318,"not"))) {
return F_dv_premise_confidence(v_world,F_dv_get(v_proposition,"item"));
} else {
if (R.Bool(R.eq(v_op__25318,"all"))) {
object v_items__25548 = F_dv_get(v_proposition,"items");
object v_i__25594 = R.number("0");
object v_confidence__25620 = R.number("1");
while (R.Bool(R.lt(v_i__25594,R.count(v_items__25548)))) {
v_confidence__25620 = F_dv_min(v_confidence__25620,F_dv_premise_confidence(v_world,R.at(v_items__25548,v_i__25594)));
v_i__25594 = R.add(v_i__25594,R.number("1"));
}
return v_confidence__25620;
} else {
return F_dv_get(F_dv_read(v_world,F_dv_get(v_proposition,"ref")),"confidence");
}
}
}
}
static object F_dv_result(object v_status,object v_world,object v_changes,object v_basis) {
return new System.Collections.Generic.List<object>(4) {F_dv_pair("status",v_status),F_dv_pair("world",F_dv_clone(v_world)),F_dv_pair("changes",F_dv_clone(v_changes)),F_dv_pair("basis",F_dv_clone(v_basis))};
}
static object F_dv_stage(object v_world,object v_effects,object v_time,object v_basis,object v_confidence) {
object v_stage__26143 = F_dv_clone(v_world);
object v_changes__26175 = new System.Collections.Generic.List<object>(0) {};
object v_touched__26196 = new System.Collections.Generic.List<object>(0) {};
object v_i__26217 = R.number("0");
object v_status__26231 = "ready";
while (R.Bool(F_dv_and(R.eq(v_status__26231,"ready"),R.lt(v_i__26217,R.count(v_effects))))) {
object v_effect__26324 = R.at(v_effects,v_i__26217);
object v_ref__26356 = F_dv_get(v_effect__26324,"ref");
object v_op__26385 = F_dv_get(v_effect__26324,"op");
object v_before__26412 = F_dv_read(v_stage__26143,v_ref__26356);
if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_stage__26143,"objects"),F_dv_get(v_ref__26356,"object"))))) {
v_status__26231 = "unknown-object";
} else {
if (R.Bool(F_dv_has(v_touched__26196,v_ref__26356))) {
v_status__26231 = "duplicate-write";
} else {
if (R.Bool(F_dv_and(R.eq(v_op__26385,"increment"),F_dv_or(R.not(F_dv_get(v_before__26412,"known")),F_dv_get(v_before__26412,"conflicted"))))) {
v_status__26231 = "conflicted-state";
} else {
_ = F_dv_require(F_dv_or(R.eq(v_op__26385,"assign"),R.eq(v_op__26385,"increment")),"Unknown effect operator");
object v_local_1__26939 = false;
if (R.Bool(R.eq(v_op__26385,"assign"))) {
v_local_1__26939 = F_dv_get(v_effect__26324,"value");
} else {
v_local_1__26939 = R.add(F_dv_get(v_before__26412,"value"),F_dv_get(v_effect__26324,"value"));
}
object v_value__27157 = v_local_1__26939;
v_stage__26143 = F_dv_stage_store_owned(v_stage__26143,v_ref__26356,F_dv_cell(v_value__27157,v_confidence,v_time,v_basis));
_ = R.push(v_changes__26175,new System.Collections.Generic.List<object>(3) {F_dv_pair("ref",v_ref__26356),F_dv_pair("before",v_before__26412),F_dv_pair("after",F_dv_read(v_stage__26143,v_ref__26356))});
_ = R.push(v_touched__26196,F_dv_clone(v_ref__26356));
}
}
}
v_i__26217 = R.add(v_i__26217,R.number("1"));
}
if (R.Bool(R.eq(v_status__26231,"ready"))) {
return new System.Collections.Generic.List<object>(4) {F_dv_pair("status","ready"),F_dv_pair("world",v_stage__26143),F_dv_pair("changes",v_changes__26175),F_dv_pair("basis",v_basis)};
} else {
return F_dv_result(v_status__26231,v_world,new System.Collections.Generic.List<object>(0) {},v_basis);
}
}
static object F_dv_infer_stage(object v_world,object v_effects,object v_time,object v_basis,object v_confidence,object v_law_name,object v_event_id) {
object v_stage__27711 = F_dv_clone(v_world);
object v_changes__27743 = new System.Collections.Generic.List<object>(0) {};
object v_touched__27764 = new System.Collections.Generic.List<object>(0) {};
object v_i__27785 = R.number("0");
object v_status__27799 = "ready";
object v_profile__27824 = F_dv_find(v_world,"profile",F_dv_observation_profile(new System.Collections.Generic.List<object>(4) {F_dv_pair("perception",R.number("1")),F_dv_pair("testimony",R.number("1")),F_dv_pair("recall",R.number("1")),F_dv_pair("inference",R.number("1"))},R.number("0.5")));
while (R.Bool(F_dv_and(R.eq(v_status__27799,"ready"),R.lt(v_i__27785,R.count(v_effects))))) {
object v_effect__28029 = R.at(v_effects,v_i__27785);
object v_ref__28061 = F_dv_get(v_effect__28029,"ref");
object v_op__28090 = F_dv_get(v_effect__28029,"op");
object v_before__28117 = F_dv_read(v_stage__27711,v_ref__28061);
if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_stage__27711,"objects"),F_dv_get(v_ref__28061,"object"))))) {
v_status__27799 = "unknown-object";
} else {
if (R.Bool(F_dv_has(v_touched__27764,v_ref__28061))) {
v_status__27799 = "duplicate-write";
} else {
if (R.Bool(F_dv_and(R.eq(v_op__28090,"increment"),F_dv_or(R.not(F_dv_get(v_before__28117,"known")),F_dv_get(v_before__28117,"conflicted"))))) {
v_status__27799 = "conflicted-state";
} else {
_ = F_dv_require(F_dv_or(R.eq(v_op__28090,"assign"),R.eq(v_op__28090,"increment")),"Unknown effect operator");
object v_local_1__28644 = false;
if (R.Bool(R.eq(v_op__28090,"assign"))) {
v_local_1__28644 = F_dv_get(v_effect__28029,"value");
} else {
v_local_1__28644 = R.add(F_dv_get(v_before__28117,"value"),F_dv_get(v_effect__28029,"value"));
}
object v_value__28862 = v_local_1__28644;
object v_claim__28902 = F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",F_dv_list3(v_event_id,v_law_name,v_ref__28061)),F_dv_pair("subject",F_dv_get(v_stage__27711,"subject")),F_dv_pair("ref",v_ref__28061),F_dv_pair("value",F_dv_clone(v_value__28862))},new System.Collections.Generic.List<object>(5) {F_dv_pair("source","inference"),F_dv_pair("origin",v_law_name),F_dv_pair("time",v_time),F_dv_pair("trust",v_confidence),F_dv_pair("dependency",F_dv_list3("inference",v_law_name,v_ref__28061))});
object v_old_claims__29186 = F_dv_get(v_before__28117,"claims");
object v_local_2__29237 = false;
if (R.Bool(F_dv_and(F_dv_get(v_before__28117,"known"),R.eq(R.count(v_old_claims__29186),R.number("0"))))) {
v_local_2__29237 = F_dv_single(F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",F_dv_pair("initial",v_ref__28061)),F_dv_pair("subject",F_dv_get(v_stage__27711,"subject")),F_dv_pair("ref",v_ref__28061),F_dv_pair("value",F_dv_get(v_before__28117,"value"))},new System.Collections.Generic.List<object>(5) {F_dv_pair("source","initial"),F_dv_pair("origin","initial"),F_dv_pair("time",F_dv_get(v_before__28117,"time")),F_dv_pair("trust",F_dv_get(v_before__28117,"confidence")),F_dv_pair("dependency",F_dv_pair("initial",v_ref__28061))}));
} else {
v_local_2__29237 = v_old_claims__29186;
}
object v_claims__29706 = v_local_2__29237;
object v_new_claims__29747 = F_dv_replace_claim(v_claims__29706,v_claim__28902);
object v_new_cell__29816 = F_dv_aggregate(v_new_claims__29747,v_profile__27824,v_time);
v_stage__27711 = F_dv_stage_store_owned(v_stage__27711,v_ref__28061,v_new_cell__29816);
_ = R.push(v_changes__27743,new System.Collections.Generic.List<object>(3) {F_dv_pair("ref",v_ref__28061),F_dv_pair("before",v_before__28117),F_dv_pair("after",F_dv_read(v_stage__27711,v_ref__28061))});
_ = R.push(v_touched__27764,F_dv_clone(v_ref__28061));
}
}
}
v_i__27785 = R.add(v_i__27785,R.number("1"));
}
if (R.Bool(R.eq(v_status__27799,"ready"))) {
return new System.Collections.Generic.List<object>(4) {F_dv_pair("status","ready"),F_dv_pair("world",v_stage__27711),F_dv_pair("changes",v_changes__27743),F_dv_pair("basis",v_basis)};
} else {
return F_dv_result(v_status__27799,v_world,new System.Collections.Generic.List<object>(0) {},v_basis);
}
}
static object F_dv_evaluate_law_value(object v_world,object v_name,object v_mode,object v_event_id,object v_time,object v_expected_revision,object v_operation) {
_ = F_dv_require((R.Bool(R.eq(v_operation,"verify")) ? (object)(true) : (object)(R.eq(v_operation,"run"))),"Expected verify or run");
_ = F_dv_require(R.eq(F_dv_get(v_world,"revision"),v_expected_revision),"Stale world revision");
_ = F_dv_require(R.not(F_dv_has(F_dv_get(v_world,"seen"),v_event_id)),"Repeated world event");
_ = F_dv_require(R.not(R.lt(v_time,F_dv_find(v_world,"time",R.number("0")))),"World time cannot move backwards");
_ = F_dv_require((R.Bool((R.Bool(R.eq(F_dv_get(v_world,"domain"),"physical")) ? (object)(R.eq(v_mode,"enforce")) : (object)(false))) ? (object)(true) : (object)((R.Bool(R.eq(F_dv_get(v_world,"domain"),"mind")) ? (object)((R.Bool(R.eq(v_mode,"infer")) ? (object)(true) : (object)(R.eq(v_mode,"predict")))) : (object)(false)))),"Evaluation mode is incompatible with world");
object v_adoption__31142 = F_dv_get(F_dv_get(v_world,"adoptions"),v_name);
object v_law__31191 = F_dv_get(v_adoption__31142,"law");
object v_basis__31218 = F_dv_get(v_adoption__31142,"basis");
if (R.Bool(R.not(F_dv_get(v_adoption__31142,"active")))) {
return F_dv_result("not-adopted",v_world,new System.Collections.Generic.List<object>(0) {},v_basis__31218);
} else {
if (R.Bool(R.not(R.eq(F_dv_get(v_adoption__31142,"mode"),v_mode)))) {
return F_dv_result("wrong-adoption-mode",v_world,new System.Collections.Generic.List<object>(0) {},v_basis__31218);
} else {
object v_truth__31481 = F_dv_truth(v_world,F_dv_get(v_law__31191,"premise"));
if (R.Bool(R.not(R.eq(v_truth__31481,"true")))) {
return F_dv_result(R.cat("premise-",v_truth__31481),v_world,new System.Collections.Generic.List<object>(0) {},v_basis__31218);
} else {
object v_local_1__31674 = false;
if (R.Bool(R.eq(v_mode,"infer"))) {
object v_infer_confidence__31752 = F_dv_min(F_dv_premise_confidence(v_world,F_dv_get(v_law__31191,"premise")),F_dv_get(v_adoption__31142,"confidence"));
v_local_1__31674 = F_dv_infer_stage(v_world,F_dv_get(v_law__31191,"effects"),v_time,v_basis__31218,v_infer_confidence__31752,v_name,v_event_id);
} else {
v_local_1__31674 = F_dv_stage(v_world,F_dv_get(v_law__31191,"effects"),v_time,v_basis__31218,F_dv_get(v_adoption__31142,"confidence"));
}
object v_staged__32089 = v_local_1__31674;
if (R.Bool(R.not(R.eq(F_dv_get(v_staged__32089,"status"),"ready")))) {
return v_staged__32089;
} else {
if (R.Bool(F_dv_has_key(v_law__31191,"postcondition"))) {
object v_post_truth__32300 = F_dv_truth(F_dv_get(v_staged__32089,"world"),F_dv_get(v_law__31191,"postcondition"));
if (R.Bool(R.not(R.eq(v_post_truth__32300,"true")))) {
return F_dv_result(R.cat("postcondition-",v_post_truth__32300),v_world,new System.Collections.Generic.List<object>(0) {},v_basis__31218);
} else {
}
} else {
}
if (R.Bool(R.eq(v_operation,"verify"))) {
object v_candidate__32579 = F_dv_with_many(F_dv_get(v_staged__32089,"world"),new System.Collections.Generic.List<object>(3) {F_dv_pair("domain","verification"),F_dv_pair("source_domain",F_dv_get(v_world,"domain")),F_dv_pair("source_revision",v_expected_revision)});
object v_assessment__32743 = new System.Collections.Generic.List<object>(10) {F_dv_pair("world",F_dv_get(v_world,"id")),F_dv_pair("subject",F_dv_get(v_world,"subject")),F_dv_pair("revision",v_expected_revision),F_dv_pair("law",v_name),F_dv_pair("law_version",F_dv_get(v_law__31191,"version")),F_dv_pair("mode",v_mode),F_dv_pair("event",v_event_id),F_dv_pair("time",v_time),F_dv_pair("precision","runtime-discrete"),F_dv_pair("scope","input-snapshot")};
return F_dv_with_many(F_dv_result("verified",v_world,F_dv_clone(F_dv_get(v_staged__32089,"changes")),F_dv_clone(v_basis__31218)),new System.Collections.Generic.List<object>(2) {F_dv_pair("candidate",v_candidate__32579),F_dv_pair("assessment",v_assessment__32743)});
} else {
}
object v_next__33174 = F_dv_get(v_staged__32089,"world");
object v_revision__33218 = R.add(v_expected_revision,R.number("1"));
object v_event__33277 = F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",v_event_id),F_dv_pair("time",v_time),F_dv_pair("law",v_name),F_dv_pair("law_version",F_dv_get(v_law__31191,"version"))},new System.Collections.Generic.List<object>(2) {F_dv_pair("changes",F_dv_get(v_staged__32089,"changes")),F_dv_pair("basis",v_basis__31218)});
_ = R.push(F_dv_get(v_next__33174,"seen"),F_dv_clone(v_event_id));
_ = R.push(F_dv_get(v_next__33174,"history"),v_event__33277);
v_next__33174 = F_dv_with_many(v_next__33174,new System.Collections.Generic.List<object>(2) {F_dv_pair("revision",v_revision__33218),F_dv_pair("time",v_time)});
return new System.Collections.Generic.List<object>(6) {F_dv_pair("status","applied"),F_dv_pair("world",v_next__33174),F_dv_pair("changes",F_dv_clone(F_dv_get(v_staged__32089,"changes"))),F_dv_pair("basis",F_dv_clone(v_basis__31218)),F_dv_pair("event",v_event_id),F_dv_pair("time",v_time)};
}
}
}
}
}
static object F_dv_evaluate_law(object v_world,object v_name,object v_mode,object v_event_id,object v_time,object v_expected_revision,object v_operation) {
object v_result__34022 = F_dv_evaluate_law_value(v_world,v_name,v_mode,v_event_id,v_time,v_expected_revision,v_operation);
if (R.Bool((R.Bool(R.eq(v_operation,"run")) ? (object)(R.eq(v_mode,"predict")) : (object)(false)))) {
return F_dv_with_many(v_result__34022,new System.Collections.Generic.List<object>(2) {F_dv_pair("world",F_dv_with_many(F_dv_get(v_result__34022,"world"),new System.Collections.Generic.List<object>(2) {F_dv_pair("domain","prediction"),F_dv_pair("source_revision",v_expected_revision)})),F_dv_pair("source_revision",v_expected_revision)});
} else {
}
return v_result__34022;
}
static object F_dv_apply_law(object v_world,object v_name,object v_mode,object v_event_id,object v_time,object v_expected_revision) {
return F_dv_evaluate_law(v_world,v_name,v_mode,v_event_id,v_time,v_expected_revision,"run");
}
static object F_dv_verify(object v_world,object v_name,object v_mode,object v_event_id,object v_time,object v_expected_revision) {
return F_dv_evaluate_law(v_world,v_name,v_mode,v_event_id,v_time,v_expected_revision,"verify");
}
static object F_dv_evaluate(object v_world,object v_name,object v_request) {
return F_dv_evaluate_law(v_world,v_name,F_dv_get(v_request,"mode"),F_dv_get(v_request,"event"),F_dv_get(v_request,"time"),F_dv_get(v_request,"revision"),F_dv_get(v_request,"operation"));
}
static object F_dv_execute(object v_physical,object v_name,object v_event_id,object v_time,object v_expected_revision) {
_ = F_dv_domain(v_physical,"physical");
return F_dv_apply_law(v_physical,v_name,"enforce",v_event_id,v_time,v_expected_revision);
}
static object F_dv_predict(object v_mind,object v_name,object v_event_id,object v_time,object v_expected_revision) {
_ = F_dv_domain(v_mind,"mind");
return F_dv_apply_law(v_mind,v_name,"predict",v_event_id,v_time,v_expected_revision);
}
static object F_dv_infer(object v_mind,object v_name,object v_event_id,object v_time,object v_expected_revision) {
_ = F_dv_domain(v_mind,"mind");
return F_dv_apply_law(v_mind,v_name,"infer",v_event_id,v_time,v_expected_revision);
}
static object F_dv_norm(object v_mind,object v_name) {
_ = F_dv_domain(v_mind,"mind");
object v_adoption__35557 = F_dv_get(F_dv_get(v_mind,"adoptions"),v_name);
_ = F_dv_require(R.eq(F_dv_get(v_adoption__35557,"mode"),"norm"),"Not a normative adoption");
if (R.Bool(F_dv_get(v_adoption__35557,"active"))) {
return F_dv_truth(v_mind,F_dv_get(F_dv_get(v_adoption__35557,"law"),"premise"));
} else {
return "unknown";
}
}
static object F_door_open(object v_door) {
object v_law__35919;
v_law__35919 = F_dv_axiom("door_open",R.number("1"),F_dv_matches(v_door,"mechanical",new System.Collections.Generic.List<object>(2) {F_dv_pair("inside_latch",false),F_dv_pair("outside_latch",false)}),F_dv_writes(v_door,"mechanical",new System.Collections.Generic.List<object>(1) {F_dv_pair("closed",false)}));
return v_law__35919;
}
static object F_door_self_close(object v_door) {
return F_dv_axiom("door_self_close",R.number("1"),F_dv_matches(v_door,"mechanical",new System.Collections.Generic.List<object>(2) {F_dv_pair("automatic",true),F_dv_pair("held",false)}),F_dv_writes(v_door,"mechanical",new System.Collections.Generic.List<object>(2) {F_dv_pair("closed",true),F_dv_pair("gap",R.number("0"))}));
}
static object F_door_latch(object v_door) {
return F_dv_axiom("door_latch",R.number("1"),F_dv_matches(v_door,"mechanical",new System.Collections.Generic.List<object>(1) {F_dv_pair("closed",true)}),F_dv_writes(v_door,"mechanical",new System.Collections.Generic.List<object>(1) {F_dv_pair("inside_latch",true)}));
}
static object F_door_seal(object v_door) {
return F_dv_axiom("door_seal",R.number("1"),F_dv_matches(v_door,"mechanical",new System.Collections.Generic.List<object>(1) {F_dv_pair("closed",true)}),F_dv_writes(v_door,"mechanical",new System.Collections.Generic.List<object>(1) {F_dv_pair("sealed",true)}));
}
static object F_channel_reconfigure(object v_channel,object v_gain) {
return F_dv_axiom("channel_reconfigure",R.number("1"),F_dv_true(),F_dv_writes(v_channel,"transport",new System.Collections.Generic.List<object>(1) {F_dv_pair("gain",F_dv_unit(v_gain))}));
}
static object F_channel_flow(object v_channel) {
return F_dv_axiom("channel_flow",R.number("1"),F_dv_matches(v_channel,"transport",new System.Collections.Generic.List<object>(1) {F_dv_pair("enabled",true)}),new System.Collections.Generic.List<object>(0) {});
}
static object F_dv_binding(object v_physical,object v_local,object v_subject) {
return new System.Collections.Generic.List<object>(3) {F_dv_pair("physical",v_physical),F_dv_pair("local",v_local),F_dv_pair("subject",v_subject)};
}
static object F_dv_bind(object v_physical,object v_mind,object v_aliases) {
_ = F_dv_domain(v_physical,"physical");
_ = F_dv_domain(v_mind,"mind");
_ = F_dv_require(F_dv_record_like(v_aliases),"Bindings require physical-to-local identity pairs");
object v_next__37391 = F_dv_clone(v_mind);
object v_bindings__37421 = new System.Collections.Generic.List<object>(0) {};
object v_i__37443 = R.number("0");
while (R.Bool(R.lt(v_i__37443,R.count(v_aliases)))) {
object v_source__37494 = R.at(R.at(v_aliases,v_i__37443),R.number("0"));
object v_local__37529 = R.at(R.at(v_aliases,v_i__37443),R.number("1"));
_ = F_dv_require(F_dv_has_key(F_dv_get(v_physical,"objects"),v_source__37494),"Cannot bind an unknown physical object");
_ = F_dv_require((R.Bool(R.lt(R.number("0"),R.length(v_source__37494))) ? (object)(R.lt(R.number("0"),R.length(v_local__37529))) : (object)(false)),"Binding identities must be nonempty");
_ = R.push(v_bindings__37421,F_dv_binding(v_source__37494,v_local__37529,F_dv_get(v_mind,"subject")));
if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_next__37391,"objects"),v_local__37529)))) {
v_next__37391 = F_dv_add_object(v_next__37391,F_dv_object(v_local__37529,"perceived",new System.Collections.Generic.List<object>(0) {}));
} else {
}
v_i__37443 = R.add(v_i__37443,R.number("1"));
}
_ = F_dv_validate_bindings(v_bindings__37421,F_dv_get(v_mind,"subject"));
return new System.Collections.Generic.List<object>(5) {F_dv_pair("mind",v_next__37391),F_dv_pair("bindings",v_bindings__37421),F_dv_pair("subject",F_dv_get(v_mind,"subject")),F_dv_pair("source_world",F_dv_get(v_physical,"id")),F_dv_pair("source_revision",F_dv_get(v_physical,"revision"))};
}
static object F_dv_view(object v_physical,object v_subject,object v_aliases) {
return F_dv_bind(v_physical,F_dv_world(R.cat("mind:",v_subject),"mind",v_subject),v_aliases);
}
static object F_dv_local(object v_bindings,object v_physical,object v_subject) {
object v_lookup_key__38481 = new System.Collections.Generic.List<object>(2) {R.cat("projection-local:",v_subject),v_physical};
object v_lookup__38551 = R.memo_get(v_bindings,v_lookup_key__38481);
if (R.Bool(R.at(v_lookup__38551,R.number("0")))) {
_ = F_dv_require(R.not(F_dv_get(R.at(v_lookup__38551,R.number("1")),"ambiguous")),"Ambiguous projection binding");
return F_dv_get(R.at(v_lookup__38551,R.number("1")),"local");
} else {
}
object v_key__38727 = new System.Collections.Generic.List<object>(2) {"projection-index",v_subject};
object v_cached__38771 = R.memo_get(v_bindings,v_key__38727);
object v_index__38815 = new System.Collections.Generic.List<object>(0) {};
if (R.Bool(R.at(v_cached__38771,R.number("0")))) {
v_index__38815 = R.at(v_cached__38771,R.number("1"));
} else {
object v_i__38892 = R.number("0");
while (R.Bool(R.lt(v_i__38892,R.count(v_bindings)))) {
object v_binding__38952 = R.at(v_bindings,v_i__38892);
if (R.Bool(R.eq(F_dv_get(v_binding__38952,"subject"),v_subject))) {
object v_previous__39037 = F_dv_find(v_index__38815,F_dv_get(v_binding__38952,"physical"),new System.Collections.Generic.List<object>(2) {F_dv_pair("local",""),F_dv_pair("ambiguous",false)});
object v_entry__39136 = new System.Collections.Generic.List<object>(2) {F_dv_pair("local",F_dv_get(v_binding__38952,"local")),F_dv_pair("ambiguous",(R.Bool(F_dv_get(v_previous__39037,"ambiguous")) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_previous__39037,"local"),"")))))};
object v_slot__39246 = R.record_index(v_index__38815,F_dv_get(v_binding__38952,"physical"));
if (R.Bool(R.lt(v_slot__39246,R.number("0")))) {
_ = R.push(v_index__38815,F_dv_pair(F_dv_get(v_binding__38952,"physical"),v_entry__39136));
} else {
_ = R.replace(v_index__38815,v_slot__39246,F_dv_pair(F_dv_get(v_binding__38952,"physical"),v_entry__39136));
}
} else {
}
v_i__38892 = R.add(v_i__38892,R.number("1"));
}
_ = R.memo_put(v_bindings,v_key__38727,v_index__38815);
}
object v_found__39564 = F_dv_find(v_index__38815,v_physical,new System.Collections.Generic.List<object>(2) {F_dv_pair("local",""),F_dv_pair("ambiguous",false)});
_ = R.memo_put(v_bindings,v_lookup_key__38481,v_found__39564);
_ = F_dv_require(R.not(F_dv_get(v_found__39564,"ambiguous")),"Ambiguous projection binding");
return F_dv_get(v_found__39564,"local");
}
static object F_dv_validate_bindings(object v_bindings,object v_subject) {
object v_key__39821 = new System.Collections.Generic.List<object>(2) {"validated-projection",v_subject};
if (R.Bool(R.at(R.memo_get(v_bindings,v_key__39821),R.number("0")))) {
return true;
} else {
}
object v_i__39927 = R.number("0");
object v_locals__39941 = new System.Collections.Generic.List<object>(0) {};
object v_physicals__39961 = new System.Collections.Generic.List<object>(0) {};
while (R.Bool(R.lt(v_i__39927,R.count(v_bindings)))) {
object v_b__40024 = R.at(v_bindings,v_i__39927);
if (R.Bool(R.eq(F_dv_get(v_b__40024,"subject"),v_subject))) {
_ = F_dv_require(R.lt(R.number("0"),R.length(F_dv_get(v_b__40024,"local"))),"Empty local identity");
_ = F_dv_require(R.not(F_dv_has(v_locals__39941,F_dv_get(v_b__40024,"local"))),"Local projection cannot bind two objects");
_ = F_dv_require(R.not(F_dv_has(v_physicals__39961,F_dv_get(v_b__40024,"physical"))),"Ambiguous physical binding");
_ = R.push(v_locals__39941,F_dv_get(v_b__40024,"local"));
_ = R.push(v_physicals__39961,F_dv_get(v_b__40024,"physical"));
} else {
}
v_i__39927 = R.add(v_i__39927,R.number("1"));
}
return R.memo_put(v_bindings,v_key__39821,true);
}
static object F_dv_project_value(object v_value,object v_bindings,object v_subject) {
if (R.Bool(R.is_list(v_value))) {
if (R.Bool(F_dv_record_like(v_value))) {
if (R.Bool(F_dv_same(F_dv_find(v_value,"type",""),"entity-ref"))) {
object v_local__40674 = F_dv_local(v_bindings,F_dv_get(v_value,"id"),v_subject);
if (R.Bool(R.eq(v_local__40674,""))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",false),F_dv_pair("value",false)};
} else {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",true),F_dv_pair("value",F_dv_entity_ref(v_local__40674))};
}
} else {
if (R.Bool(F_dv_or(F_dv_same(F_dv_find(v_value,"type",""),"ref"),F_dv_and(F_dv_has_key(v_value,"object"),F_dv_and(F_dv_has_key(v_value,"facet"),F_dv_has_key(v_value,"field")))))) {
object v_local__41123 = F_dv_local(v_bindings,F_dv_get(v_value,"object"),v_subject);
if (R.Bool(R.eq(v_local__41123,""))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",false),F_dv_pair("value",false)};
} else {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",true),F_dv_pair("value",F_dv_scoped_ref(v_subject,v_local__41123,F_dv_get(v_value,"facet"),F_dv_get(v_value,"field")))};
}
} else {
if (R.Bool(F_dv_has_key(v_value,"op"))) {
object v_op__41512 = F_dv_get(v_value,"op");
if (R.Bool(R.eq(v_op__41512,"constant"))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",true),F_dv_pair("value",v_value)};
} else {
if (R.Bool(F_dv_or(R.eq(v_op__41512,"equals"),R.eq(v_op__41512,"at_least")))) {
object v_p_ref__41782 = F_dv_project_value(F_dv_get(v_value,"ref"),v_bindings,v_subject);
object v_p_val__41873 = F_dv_project_value(F_dv_get(v_value,"value"),v_bindings,v_subject);
if (R.Bool(F_dv_and(F_dv_get(v_p_ref__41782,"known"),F_dv_get(v_p_val__41873,"known")))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",true),F_dv_pair("value",F_dv_with_many(v_value,new System.Collections.Generic.List<object>(2) {F_dv_pair("ref",F_dv_get(v_p_ref__41782,"value")),F_dv_pair("value",F_dv_get(v_p_val__41873,"value"))}))};
} else {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",false),F_dv_pair("value",false)};
}
} else {
if (R.Bool(R.eq(v_op__41512,"not"))) {
object v_p_item__42370 = F_dv_project_value(F_dv_get(v_value,"item"),v_bindings,v_subject);
if (R.Bool(F_dv_get(v_p_item__42370,"known"))) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",true),F_dv_pair("value",F_dv_with(v_value,"item",F_dv_get(v_p_item__42370,"value")))};
} else {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",false),F_dv_pair("value",false)};
}
} else {
if (R.Bool(R.eq(v_op__41512,"all"))) {
object v_items__42848 = F_dv_get(v_value,"items");
object v_i__42912 = R.number("0");
object v_p_items__42962 = new System.Collections.Generic.List<object>(0) {};
object v_all_known__43019 = true;
while (R.Bool(R.lt(v_i__42912,R.count(v_items__42848)))) {
object v_p_sub__43153 = F_dv_project_value(R.at(v_items__42848,v_i__42912),v_bindings,v_subject);
v_all_known__43019 = F_dv_and(v_all_known__43019,F_dv_get(v_p_sub__43153,"known"));
_ = R.push(v_p_items__42962,F_dv_get(v_p_sub__43153,"value"));
v_i__42912 = R.add(v_i__42912,R.number("1"));
}
if (R.Bool(v_all_known__43019)) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",true),F_dv_pair("value",F_dv_with(v_value,"items",v_p_items__42962))};
} else {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",false),F_dv_pair("value",false)};
}
} else {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",false),F_dv_pair("value",false)};
}
}
}
}
} else {
object v_i__43894 = R.number("0");
object v_items__43928 = new System.Collections.Generic.List<object>(0) {};
object v_known__43967 = true;
while (R.Bool(R.lt(v_i__43894,R.count(v_value)))) {
object v_projected__44065 = F_dv_project_value(R.at(v_value,v_i__43894),v_bindings,v_subject);
v_known__43967 = F_dv_and(v_known__43967,F_dv_get(v_projected__44065,"known"));
_ = R.push(v_items__43928,F_dv_get(v_projected__44065,"value"));
v_i__43894 = R.add(v_i__43894,R.number("1"));
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",v_known__43967),F_dv_pair("value",v_items__43928)};
}
}
}
} else {
object v_i__44384 = R.number("0");
object v_items__44406 = new System.Collections.Generic.List<object>(0) {};
object v_known__44433 = true;
while (R.Bool(R.lt(v_i__44384,R.count(v_value)))) {
object v_projected__44507 = F_dv_project_value(R.at(v_value,v_i__44384),v_bindings,v_subject);
v_known__44433 = F_dv_and(v_known__44433,F_dv_get(v_projected__44507,"known"));
_ = R.push(v_items__44406,F_dv_get(v_projected__44507,"value"));
v_i__44384 = R.add(v_i__44384,R.number("1"));
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",v_known__44433),F_dv_pair("value",v_items__44406)};
}
} else {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("known",true),F_dv_pair("value",v_value)};
}
}
static object F_dv_observation(object v_id,object v_subject,object v_ref,object v_value,object v_source,object v_origin,object v_time,object v_trust,object v_dependency) {
_ = F_dv_unit(v_trust);
_ = F_dv_require(R.not(R.lt(v_time,R.number("0"))),"Negative observation time");
_ = F_dv_require(F_dv_has(F_dv_append(F_dv_list3("perception","testimony","recall"),F_dv_pair("inference","initial")),v_source),"Observations use perception, testimony, recall, inference or initial");
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",v_id),F_dv_pair("subject",v_subject),F_dv_pair("ref",v_ref),F_dv_pair("value",F_dv_clone(v_value))},new System.Collections.Generic.List<object>(5) {F_dv_pair("source",v_source),F_dv_pair("origin",v_origin),F_dv_pair("time",v_time),F_dv_pair("trust",v_trust),F_dv_pair("dependency",v_dependency)});
}
static object F_dv_perceive(object v_physical,object v_mind,object v_bindings,object v_grants,object v_event_id,object v_time) {
_ = F_dv_domain(v_physical,"physical");
_ = F_dv_domain(v_mind,"mind");
_ = F_dv_validate_bindings(v_bindings,F_dv_get(v_mind,"subject"));
object v_observations__45542 = new System.Collections.Generic.List<object>(0) {};
object v_i__45568 = R.number("0");
object v_subject__45582 = F_dv_get(v_mind,"subject");
while (R.Bool(R.lt(v_i__45568,R.count(v_grants)))) {
object v_ref__45651 = R.at(v_grants,v_i__45568);
object v_local__45679 = F_dv_local(v_bindings,F_dv_get(v_ref__45651,"object"),v_subject__45582);
object v_cell__45739 = F_dv_read(v_physical,v_ref__45651);
if (R.Bool(F_dv_and(R.not(R.eq(v_local__45679,"")),F_dv_get(v_cell__45739,"known")))) {
object v_mapped__45837 = F_dv_project_value(F_dv_get(v_cell__45739,"value"),v_bindings,v_subject__45582);
object v_local_ref__45910 = F_dv_scoped_ref(v_subject__45582,v_local__45679,F_dv_get(v_ref__45651,"facet"),F_dv_get(v_ref__45651,"field"));
if (R.Bool(F_dv_get(v_mapped__45837,"known"))) {
_ = R.push(v_observations__45542,F_dv_with(F_dv_observation(F_dv_list3(v_event_id,v_subject__45582,v_local_ref__45910),v_subject__45582,v_local_ref__45910,F_dv_get(v_mapped__45837,"value"),"perception","direct",v_time,R.number("1"),F_dv_pair(v_event_id,v_local_ref__45910)),"world_version",new System.Collections.Generic.List<object>(2) {F_dv_pair("id",F_dv_get(v_physical,"id")),F_dv_pair("revision",F_dv_get(v_physical,"revision"))}));
} else {
}
} else {
}
v_i__45568 = R.add(v_i__45568,R.number("1"));
}
return v_observations__45542;
}
static object F_dv_perceive_changes(object v_receipt,object v_mind,object v_bindings,object v_grants) {
_ = F_dv_require(R.eq(F_dv_get(v_receipt,"status"),"applied"),"Only applied world changes can be perceived");
object v_changes__46475 = F_dv_get(v_receipt,"changes");
object v_exposed__46509 = new System.Collections.Generic.List<object>(0) {};
object v_i__46530 = R.number("0");
while (R.Bool(R.lt(v_i__46530,R.count(v_changes__46475)))) {
object v_ref__46583 = F_dv_get(R.at(v_changes__46475,v_i__46530),"ref");
if (R.Bool(F_dv_has(v_grants,v_ref__46583))) {
_ = R.push(v_exposed__46509,v_ref__46583);
} else {
}
v_i__46530 = R.add(v_i__46530,R.number("1"));
}
return F_dv_perceive(F_dv_get(v_receipt,"world"),v_mind,v_bindings,v_exposed__46509,F_dv_get(v_receipt,"event"),F_dv_get(v_receipt,"time"));
}
static object F_dv_observation_profile(object v_source_weights,object v_threshold) {
return new System.Collections.Generic.List<object>(2) {F_dv_pair("weights",v_source_weights),F_dv_pair("threshold",F_dv_unit(v_threshold))};
}
static object F_dv_weight(object v_observation,object v_profile) {
object v_weights__46954 = F_dv_get(v_profile,"weights");
object v_source__46988 = F_dv_get(v_observation,"source");
object v_weight__47024 = F_dv_find(v_weights__46954,v_source__46988,R.number("1"));
_ = F_dv_require(R.not(R.lt(v_weight__47024,R.number("0"))),"Negative evidence weight");
return R.mul(v_weight__47024,F_dv_unit(F_dv_get(v_observation,"trust")));
}
static object F_dv_replace_claim(object v_claims,object v_observation) {
object v_result__47224 = new System.Collections.Generic.List<object>(0) {};
object v_i__47244 = R.number("0");
while (R.Bool(R.lt(v_i__47244,R.count(v_claims)))) {
object v_old__47296 = R.at(v_claims,v_i__47244);
if (R.Bool(F_dv_same(F_dv_get(v_old__47296,"dependency"),F_dv_get(v_observation,"dependency")))) {
_ = false;
} else {
_ = R.push(v_result__47224,F_dv_clone(v_old__47296));
}
v_i__47244 = R.add(v_i__47244,R.number("1"));
}
_ = R.push(v_result__47224,F_dv_clone(v_observation));
return v_result__47224;
}
static object F_dv_aggregate(object v_claims,object v_profile,object v_time) {
object v_alternatives__47577 = new System.Collections.Generic.List<object>(0) {};
object v_i__47603 = R.number("0");
object v_total__47617 = R.number("0");
object v_best__47635 = R.number("0");
object v_chosen__47652 = false;
object v_best_rel__47675 = R.number("0");
while (R.Bool(R.lt(v_i__47603,R.count(v_claims)))) {
object v_claim__47734 = R.at(v_claims,v_i__47603);
object v_weight__47764 = F_dv_weight(v_claim__47734,v_profile);
object v_value__47811 = F_dv_get(v_claim__47734,"value");
object v_j__47843 = R.number("0");
object v_found__47861 = false;
object v_new_items__47887 = new System.Collections.Generic.List<object>(0) {};
while (R.Bool(R.lt(v_j__47843,R.count(v_alternatives__47577)))) {
object v_alternative__47962 = R.at(v_alternatives__47577,v_j__47843);
if (R.Bool(F_dv_same(R.at(v_alternative__47962,R.number("0")),v_value__47811))) {
_ = R.push(v_new_items__47887,F_dv_list3(v_value__47811,R.add(R.at(v_alternative__47962,R.number("1")),v_weight__47764),F_dv_max(R.at(v_alternative__47962,R.number("2")),F_dv_unit(F_dv_get(v_claim__47734,"trust")))));
v_found__47861 = true;
} else {
_ = R.push(v_new_items__47887,v_alternative__47962);
}
v_j__47843 = R.add(v_j__47843,R.number("1"));
}
if (R.Bool(F_dv_or(v_found__47861,R.eq(v_weight__47764,R.number("0"))))) {
_ = false;
} else {
_ = R.push(v_new_items__47887,F_dv_list3(v_value__47811,v_weight__47764,F_dv_unit(F_dv_get(v_claim__47734,"trust"))));
}
v_alternatives__47577 = v_new_items__47887;
v_total__47617 = R.add(v_total__47617,v_weight__47764);
v_i__47603 = R.add(v_i__47603,R.number("1"));
}
v_i__47603 = R.number("0");
object v_distribution__48537 = new System.Collections.Generic.List<object>(0) {};
while (R.Bool(R.lt(v_i__47603,R.count(v_alternatives__47577)))) {
object v_alternative__48607 = R.at(v_alternatives__47577,v_i__47603);
object v_weight__48649 = R.at(v_alternative__48607,R.number("1"));
object v_rel__48685 = R.at(v_alternative__48607,R.number("2"));
if (R.Bool(R.lt(v_best__47635,v_weight__48649))) {
v_best__47635 = v_weight__48649;
v_chosen__47652 = R.at(v_alternative__48607,R.number("0"));
v_best_rel__47675 = v_rel__48685;
} else {
}
object v_local_1__48835 = false;
if (R.Bool(R.eq(v_total__47617,R.number("0")))) {
v_local_1__48835 = R.number("0");
} else {
v_local_1__48835 = R.div(v_weight__48649,v_total__47617);
}
_ = R.push(v_distribution__48537,F_dv_pair(R.at(v_alternative__48607,R.number("0")),v_local_1__48835));
v_i__47603 = R.add(v_i__47603,R.number("1"));
}
object v_local_2__49045 = false;
if (R.Bool(R.eq(v_total__47617,R.number("0")))) {
v_local_2__49045 = R.number("0");
} else {
v_local_2__49045 = R.div(v_best__47635,v_total__47617);
}
object v_support_share__49153 = v_local_2__49045;
object v_epistemic_confidence__49344 = R.mul(v_support_share__49153,v_best_rel__47675);
object v_cell__49402 = F_dv_cell(v_chosen__47652,v_epistemic_confidence__49344,v_time,v_claims);
return F_dv_with_many(v_cell__49402,new System.Collections.Generic.List<object>(6) {F_dv_pair("known",F_dv_and(R.lt(R.number("0"),v_total__47617),R.not(R.lt(v_epistemic_confidence__49344,F_dv_get(v_profile,"threshold"))))),F_dv_pair("conflicted",R.lt(R.number("1"),R.count(v_alternatives__47577))),F_dv_pair("claims",v_claims),F_dv_pair("alternatives",v_distribution__48537),F_dv_pair("support_share",v_support_share__49153),F_dv_pair("source_reliability",v_best_rel__47675)});
}
static object F_dv_update_mind(object v_mind,object v_observations,object v_expected_revision,object v_profile) {
_ = F_dv_domain(v_mind,"mind");
_ = F_dv_require(R.eq(F_dv_get(v_mind,"revision"),v_expected_revision),"Stale cognition revision");
object v_next__49914 = F_dv_clone(v_mind);
object v_i__49944 = R.number("0");
object v_accepted__49958 = new System.Collections.Generic.List<object>(0) {};
object v_dispositions__49980 = new System.Collections.Generic.List<object>(0) {};
object v_latest_time__50006 = F_dv_find(v_mind,"time",R.number("0"));
while (R.Bool(R.lt(v_i__49944,R.count(v_observations)))) {
object v_observation__50097 = R.at(v_observations,v_i__49944);
object v_id__50139 = F_dv_get(v_observation__50097,"id");
object v_ref__50171 = F_dv_get(v_observation__50097,"ref");
object v_object_id__50205 = F_dv_get(v_ref__50171,"object");
_ = F_dv_require(R.eq(F_dv_get(v_observation__50097,"subject"),F_dv_get(v_mind,"subject")),"Foreign observation");
_ = F_dv_require(R.not(F_dv_has(F_dv_get(v_next__49914,"seen"),v_id__50139)),"Repeated observation");
_ = F_dv_weight(v_observation__50097,v_profile);
if (R.Bool(F_dv_has_key(F_dv_get(v_next__49914,"objects"),v_object_id__50205))) {
_ = false;
} else {
v_next__49914 = F_dv_add_object(v_next__49914,F_dv_object(v_object_id__50205,"perceived",new System.Collections.Generic.List<object>(0) {}));
}
object v_old__50591 = F_dv_read(v_next__49914,v_ref__50171);
object v_time__50628 = F_dv_get(v_observation__50097,"time");
v_latest_time__50006 = F_dv_max(v_latest_time__50006,v_time__50628);
if (R.Bool(R.lt(v_time__50628,F_dv_get(v_old__50591,"time")))) {
_ = R.push(v_dispositions__49980,F_dv_pair(v_id__50139,"older-than-current"));
} else {
object v_is_perception__50829 = R.eq(F_dv_get(v_observation__50097,"source"),"perception");
object v_version__50898 = F_dv_find(v_observation__50097,"world_version",false);
object v_previous_version__50969 = F_dv_find(v_old__50591,"world_version",false);
object v_later_version__51041 = false;
if (R.Bool((R.Bool(R.not(R.eq(v_version__50898,false))) ? (object)(R.not(R.eq(v_previous_version__50969,false))) : (object)(false)))) {
v_later_version__51041 = (R.Bool(R.eq(F_dv_get(v_version__50898,"id"),F_dv_get(v_previous_version__50969,"id"))) ? (object)(R.lt(F_dv_get(v_previous_version__50969,"revision"),F_dv_get(v_version__50898,"revision"))) : (object)(false));
} else {
}
object v_replaces_current__51261 = (R.Bool(v_is_perception__50829) ? (object)((R.Bool(R.lt(F_dv_get(v_old__50591,"time"),v_time__50628)) ? (object)(true) : (object)(v_later_version__51041))) : (object)(false));
object v_local_1__51349 = false;
if (R.Bool(v_replaces_current__51261)) {
v_local_1__51349 = new System.Collections.Generic.List<object>(0) {};
} else {
v_local_1__51349 = F_dv_get(v_old__50591,"claims");
}
object v_claims__51498 = v_local_1__51349;
if (R.Bool(F_dv_and(F_dv_get(v_old__50591,"known"),R.eq(R.count(v_claims__51498),R.number("0"))))) {
if (R.Bool(R.not(v_replaces_current__51261))) {
v_claims__51498 = F_dv_single(F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",F_dv_pair("initial",v_ref__50171)),F_dv_pair("subject",F_dv_get(v_next__49914,"subject")),F_dv_pair("ref",v_ref__50171),F_dv_pair("value",F_dv_get(v_old__50591,"value"))},new System.Collections.Generic.List<object>(5) {F_dv_pair("source","initial"),F_dv_pair("origin","initial"),F_dv_pair("time",F_dv_get(v_old__50591,"time")),F_dv_pair("trust",F_dv_get(v_old__50591,"confidence")),F_dv_pair("dependency",F_dv_pair("initial",v_ref__50171))}));
} else {
}
} else {
}
object v_new_time__51902 = F_dv_max(F_dv_get(v_old__50591,"time"),v_time__50628);
object v_updated__51952 = F_dv_aggregate(F_dv_replace_claim(v_claims__51498,v_observation__50097),v_profile,v_new_time__51902);
if (R.Bool((R.Bool(v_is_perception__50829) ? (object)(R.not(R.eq(v_version__50898,false))) : (object)(false)))) {
v_updated__51952 = F_dv_with(v_updated__51952,"world_version",v_version__50898);
} else {
if (R.Bool(R.not(R.eq(v_previous_version__50969,false)))) {
v_updated__51952 = F_dv_with(v_updated__51952,"world_version",v_previous_version__50969);
} else {
}
}
v_next__49914 = F_dv_stage_store_owned(v_next__49914,v_ref__50171,v_updated__51952);
_ = R.push(v_dispositions__49980,F_dv_pair(v_id__50139,"updated"));
}
_ = R.push(F_dv_get(v_next__49914,"seen"),F_dv_clone(v_id__50139));
_ = R.push(v_accepted__49958,F_dv_clone(v_observation__50097));
v_i__49944 = R.add(v_i__49944,R.number("1"));
}
object v_local_2__52539 = false;
if (R.Bool(R.eq(R.count(v_observations),R.number("0")))) {
v_local_2__52539 = v_expected_revision;
} else {
v_local_2__52539 = R.add(v_expected_revision,R.number("1"));
}
_ = R.push(F_dv_get(v_next__49914,"history"),new System.Collections.Generic.List<object>(3) {F_dv_pair("kind","observation-batch"),F_dv_pair("observations",v_accepted__49958),F_dv_pair("dispositions",v_dispositions__49980)});
v_next__49914 = F_dv_with_many(v_next__49914,new System.Collections.Generic.List<object>(2) {F_dv_pair("time",v_latest_time__50006),F_dv_pair("revision",v_local_2__52539)});
return new System.Collections.Generic.List<object>(3) {F_dv_pair("world",v_next__49914),F_dv_pair("dispositions",v_dispositions__49980),F_dv_pair("revision",F_dv_get(v_next__49914,"revision"))};
}
static object F_dv_segment(object v_content,object v_strength,object v_band) {
return new System.Collections.Generic.List<object>(3) {F_dv_pair("content",v_content),F_dv_pair("strength",v_strength),F_dv_pair("band",v_band)};
}
static object F_dv_signal(object v_location,object v_medium,object v_segments) {
return new System.Collections.Generic.List<object>(3) {F_dv_pair("location",F_dv_entity_ref(v_location)),F_dv_pair("medium",v_medium),F_dv_pair("segments",v_segments)};
}
static object F_dv_filter_signal(object v_segments,object v_gain,object v_threshold,object v_bands) {
object v_result__53383 = new System.Collections.Generic.List<object>(0) {};
object v_i__53403 = R.number("0");
while (R.Bool(R.lt(v_i__53403,R.count(v_segments)))) {
object v_segment__53457 = R.at(v_segments,v_i__53403);
object v_strength__53491 = R.mul(F_dv_get(v_segment__53457,"strength"),v_gain);
if (R.Bool(F_dv_and(R.not(R.lt(v_strength__53491,v_threshold)),F_dv_or(R.eq(R.count(v_bands),R.number("0")),F_dv_has(v_bands,F_dv_get(v_segment__53457,"band")))))) {
_ = R.push(v_result__53383,F_dv_segment(F_dv_get(v_segment__53457,"content"),v_strength__53491,F_dv_get(v_segment__53457,"band")));
} else {
}
v_i__53403 = R.add(v_i__53403,R.number("1"));
}
return v_result__53383;
}
static object F_dv_transport(object v_world,object v_path,object v_signal,object v_threshold) {
_ = F_dv_require(R.not(R.lt(v_threshold,R.number("0"))),"Negative perception threshold");
object v_i__53885 = R.number("0");
object v_location__53899 = F_dv_get(v_signal,"location");
object v_segments__53934 = F_dv_clone(F_dv_get(v_signal,"segments"));
object v_status__53979 = "delivered";
object v_trace__54008 = new System.Collections.Generic.List<object>(0) {};
while (R.Bool(F_dv_and(R.eq(v_status__53979,"delivered"),R.lt(v_i__53885,R.count(v_path))))) {
object v_channel__54096 = R.at(v_path,v_i__53885);
object v_objects__54126 = F_dv_get(v_world,"objects");
_ = F_dv_require(F_dv_has_key(v_objects__54126,v_channel__54096),"Unknown channel");
_ = F_dv_require(R.eq(F_dv_truth(v_world,F_dv_fact(F_dv_ref(v_channel__54096,"identity","kind"),"channel")),"true"),"Transport object must be known as a channel");
object v_from__54382 = F_dv_read(v_world,F_dv_ref(v_channel__54096,"link","from"));
object v_to__54449 = F_dv_read(v_world,F_dv_ref(v_channel__54096,"link","to"));
object v_medium__54512 = F_dv_read(v_world,F_dv_ref(v_channel__54096,"link","medium"));
object v_gate__54583 = F_dv_read(v_world,F_dv_ref(v_channel__54096,"link","gate"));
object v_enabled__54650 = F_dv_read(v_world,F_dv_ref(v_channel__54096,"transport","enabled"));
object v_gain__54728 = F_dv_read(v_world,F_dv_ref(v_channel__54096,"transport","gain"));
if (R.Bool(F_dv_and(F_dv_and(F_dv_uncontested(v_from__54382),F_dv_uncontested(v_to__54449)),F_dv_and(F_dv_and(F_dv_uncontested(v_medium__54512),F_dv_uncontested(v_gate__54583)),F_dv_and(F_dv_uncontested(v_enabled__54650),F_dv_uncontested(v_gain__54728)))))) {
_ = F_dv_require(F_dv_same(v_location__53899,F_dv_get(v_from__54382,"value")),"Discontinuous channel path");
_ = F_dv_require(R.eq(F_dv_get(v_signal,"medium"),F_dv_get(v_medium__54512,"value")),"Channel medium mismatch");
object v_gate_truth__55157 = F_dv_truth(v_world,F_dv_get(v_gate__54583,"value"));
if (R.Bool(F_dv_and(F_dv_get(v_enabled__54650,"value"),R.eq(v_gate_truth__55157,"true")))) {
object v_bands_cell__55280 = F_dv_read(v_world,F_dv_ref(v_channel__54096,"transport","bands"));
if (R.Bool(F_dv_uncontested(v_bands_cell__55280))) {
v_segments__53934 = F_dv_filter_signal(v_segments__53934,F_dv_unit(F_dv_get(v_gain__54728,"value")),v_threshold,F_dv_get(v_bands_cell__55280,"value"));
v_location__53899 = F_dv_get(v_to__54449,"value");
_ = R.push(v_trace__54008,v_channel__54096);
} else {
v_status__53979 = "unknown";
}
} else {
object v_local_1__55685 = false;
if (R.Bool(R.eq(v_gate_truth__55157,"unknown"))) {
v_local_1__55685 = "unknown";
} else {
v_local_1__55685 = "blocked";
}
v_status__53979 = v_local_1__55685;
}
} else {
v_status__53979 = "unknown";
}
v_i__53885 = R.add(v_i__53885,R.number("1"));
}
object v_local_2__55955 = false;
if (R.Bool(R.eq(v_status__53979,"delivered"))) {
v_local_2__55955 = v_segments__53934;
} else {
v_local_2__55955 = new System.Collections.Generic.List<object>(0) {};
}
return F_dv_with(new System.Collections.Generic.List<object>(4) {F_dv_pair("status",v_status__53979),F_dv_pair("location",v_location__53899),F_dv_pair("segments",v_local_2__55955),F_dv_pair("trace",v_trace__54008)},"world_revision",F_dv_get(v_world,"revision"));
}
static object F_dv_hear(object v_delivery,object v_subject,object v_local_ref,object v_perceived_origin,object v_event_id,object v_time) {
_ = F_dv_require(R.eq(F_dv_get(v_delivery,"status"),"delivered"),"No delivered signal");
_ = F_dv_require(R.eq(F_dv_find(v_delivery,"hypothetical",true),false),"Predicted or unauthorised transmission cannot become perception");
return F_dv_observation(v_event_id,v_subject,v_local_ref,F_dv_get(v_delivery,"segments"),"perception",v_perceived_origin,v_time,R.number("1"),v_event_id);
}
static object F_dv_transport_theory(object v_law) {
return F_dv_with(v_law,"operation","attenuate-filter");
}
static object F_dv_transmit(object v_world,object v_law_name,object v_path,object v_signal,object v_threshold) {
object v_adoption__56756 = F_dv_get(F_dv_get(v_world,"adoptions"),v_law_name);
object v_law__56809 = F_dv_get(v_adoption__56756,"law");
_ = F_dv_require(F_dv_get(v_adoption__56756,"active"),"Transmission law is not active");
_ = F_dv_require(R.eq(F_dv_get(v_law__56809,"operation"),"attenuate-filter"),"Not a transport theory");
object v_local_1__56982 = false;
if (R.Bool(R.eq(F_dv_get(v_world,"domain"),"physical"))) {
v_local_1__56982 = R.eq(F_dv_get(v_adoption__56756,"mode"),"enforce");
} else {
v_local_1__56982 = F_dv_and(R.eq(F_dv_get(v_world,"domain"),"mind"),R.eq(F_dv_get(v_adoption__56756,"mode"),"predict"));
}
_ = F_dv_require(v_local_1__56982,"Wrong transmission adoption");
_ = F_dv_require(R.eq(F_dv_truth(v_world,F_dv_get(v_law__56809,"premise")),"true"),"Transmission premise is not established");
return F_dv_with_many(F_dv_transport(v_world,v_path,v_signal,v_threshold),new System.Collections.Generic.List<object>(2) {F_dv_pair("basis",F_dv_get(v_adoption__56756,"basis")),F_dv_pair("hypothetical",R.not(R.eq(F_dv_get(v_world,"domain"),"physical")))});
}
static object F_dv_criterion(object v_id,object v_ref,object v_mode,object v_target,object v_weight) {
_ = F_dv_require(F_dv_or(R.eq(v_mode,"numeric"),R.eq(v_mode,"matches")),"Unsupported value criterion");
return F_dv_with(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",v_id),F_dv_pair("ref",v_ref),F_dv_pair("mode",v_mode),F_dv_pair("target",v_target)},"weight",v_weight);
}
static object F_dv_measure(object v_value,object v_criterion) {
if (R.Bool(R.eq(F_dv_get(v_criterion,"mode"),"numeric"))) {
return R.mul(v_value,F_dv_get(v_criterion,"weight"));
} else {
if (R.Bool(F_dv_same(v_value,F_dv_get(v_criterion,"target")))) {
return F_dv_get(v_criterion,"weight");
} else {
return R.number("0");
}
}
}
static object F_dv_expected_measure(object v_cell,object v_criterion) {
object v_alternatives__58090 = F_dv_find(v_cell,"alternatives",new System.Collections.Generic.List<object>(0) {});
object v_sum__58147 = R.number("0");
object v_i__58163 = R.number("0");
if (R.Bool(R.eq(R.count(v_alternatives__58090),R.number("0")))) {
return R.mul(F_dv_measure(F_dv_get(v_cell,"value"),v_criterion),F_dv_get(v_cell,"confidence"));
} else {
while (R.Bool(R.lt(v_i__58163,R.count(v_alternatives__58090)))) {
object v_alternative__58346 = R.at(v_alternatives__58090,v_i__58163);
v_sum__58147 = R.add(v_sum__58147,R.mul(F_dv_measure(R.at(v_alternative__58346,R.number("0")),v_criterion),R.at(v_alternative__58346,R.number("1"))));
v_i__58163 = R.add(v_i__58163,R.number("1"));
}
return R.mul(v_sum__58147,F_dv_get(v_cell,"confidence"));
}
}
static object F_dv_value(object v_world,object v_criteria,object v_subjective) {
object v_i__58573 = R.number("0");
object v_score__58587 = R.number("0");
object v_details__58605 = new System.Collections.Generic.List<object>(0) {};
object v_unknown__58626 = new System.Collections.Generic.List<object>(0) {};
object v_ids__58647 = new System.Collections.Generic.List<object>(0) {};
while (R.Bool(R.lt(v_i__58573,R.count(v_criteria)))) {
object v_criterion__58704 = R.at(v_criteria,v_i__58573);
object v_id__58740 = F_dv_get(v_criterion__58704,"id");
object v_cell__58770 = F_dv_read(v_world,F_dv_get(v_criterion__58704,"ref"));
_ = F_dv_require(R.not(F_dv_has(v_ids__58647,v_id__58740)),"Duplicate value criterion");
_ = R.push(v_ids__58647,v_id__58740);
if (R.Bool(F_dv_get(v_cell__58770,"known"))) {
object v_local_1__58942 = false;
if (R.Bool(v_subjective)) {
v_local_1__58942 = F_dv_expected_measure(v_cell__58770,v_criterion__58704);
} else {
v_local_1__58942 = F_dv_measure(F_dv_get(v_cell__58770,"value"),v_criterion__58704);
}
object v_amount__59142 = v_local_1__58942;
v_score__58587 = R.add(v_score__58587,v_amount__59142);
_ = R.push(v_details__58605,new System.Collections.Generic.List<object>(4) {F_dv_pair("criterion",v_id__58740),F_dv_pair("contribution",v_amount__59142),F_dv_pair("confidence",F_dv_get(v_cell__58770,"confidence")),F_dv_pair("conflicted",F_dv_get(v_cell__58770,"conflicted"))});
} else {
_ = R.push(v_unknown__58626,v_id__58740);
}
v_i__58573 = R.add(v_i__58573,R.number("1"));
}
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("score",v_score__58587),F_dv_pair("details",v_details__58605),F_dv_pair("unknown",v_unknown__58626),F_dv_pair("complete",R.eq(R.count(v_unknown__58626),R.number("0")))},new System.Collections.Generic.List<object>(3) {F_dv_pair("world",F_dv_get(v_world,"id")),F_dv_pair("revision",F_dv_get(v_world,"revision")),F_dv_pair("subject",F_dv_get(v_world,"subject"))});
}
static object F_dv_value_subjective(object v_mind,object v_criteria) {
_ = F_dv_require(F_dv_or(R.eq(F_dv_get(v_mind,"domain"),"mind"),R.eq(F_dv_get(v_mind,"domain"),"prediction")),"Subjective value requires a mind or prediction");
return F_dv_value(v_mind,v_criteria,true);
}
static object F_dv_value_objective(object v_physical,object v_criteria) {
_ = F_dv_domain(v_physical,"physical");
return F_dv_value(v_physical,v_criteria,false);
}
static object F_dv_value_change(object v_before,object v_after,object v_criteria) {
_ = F_dv_require(F_dv_and(R.eq(F_dv_get(v_before,"id"),F_dv_get(v_after,"id")),R.eq(F_dv_get(v_before,"subject"),F_dv_get(v_after,"subject"))),"Value comparison crosses worlds");
object v_subjective__60102 = R.not(R.eq(F_dv_get(v_before,"domain"),"physical"));
object v_local_1__60159 = false;
if (R.Bool(v_subjective__60102)) {
v_local_1__60159 = F_dv_and(R.eq(F_dv_get(v_before,"domain"),"mind"),F_dv_or(R.eq(F_dv_get(v_after,"domain"),"mind"),R.eq(F_dv_get(v_after,"domain"),"prediction")));
} else {
v_local_1__60159 = R.eq(F_dv_get(v_after,"domain"),"physical");
}
_ = F_dv_require(v_local_1__60159,"Value domain mismatch");
object v_a__60425 = F_dv_value(v_before,v_criteria,v_subjective__60102);
object v_b__60476 = F_dv_value(v_after,v_criteria,v_subjective__60102);
return new System.Collections.Generic.List<object>(4) {F_dv_pair("delta",R.sub(F_dv_get(v_b__60476,"score"),F_dv_get(v_a__60425,"score"))),F_dv_pair("before",v_a__60425),F_dv_pair("after",v_b__60476),F_dv_pair("complete",F_dv_and(F_dv_get(v_a__60425,"complete"),F_dv_get(v_b__60476,"complete")))};
}
static object F_dv_request(object v_id,object v_requester,object v_self) {
return new System.Collections.Generic.List<object>(3) {F_dv_pair("id",v_id),F_dv_pair("requester",v_requester),F_dv_pair("self",v_self)};
}
static object F_dv_feature_refs(object v_request) {
object v_speaker__60947 = F_dv_get(v_request,"requester");
object v_self__60983 = F_dv_get(v_request,"self");
object v_id__61011 = F_dv_get(v_request,"id");
object v_refs__61035 = new System.Collections.Generic.List<object>(0) {};
_ = R.push(v_refs__61035,F_dv_pair("trust",F_dv_ref(v_speaker__60947,"relationship","trust")));
_ = R.push(v_refs__61035,F_dv_pair("closeness",F_dv_ref(v_speaker__60947,"relationship","closeness")));
_ = R.push(v_refs__61035,F_dv_pair("respect",F_dv_ref(v_speaker__60947,"relationship","respect")));
_ = R.push(v_refs__61035,F_dv_pair("reciprocity",F_dv_ref(v_speaker__60947,"relationship","reciprocity")));
_ = R.push(v_refs__61035,F_dv_pair("valence",F_dv_ref(v_self__60983,"affect","valence")));
_ = R.push(v_refs__61035,F_dv_pair("arousal",F_dv_ref(v_self__60983,"affect","arousal")));
_ = R.push(v_refs__61035,F_dv_pair("fatigue",F_dv_ref(v_self__60983,"resources","fatigue")));
_ = R.push(v_refs__61035,F_dv_pair("stress",F_dv_ref(v_self__60983,"resources","stress")));
_ = R.push(v_refs__61035,F_dv_pair("attention",F_dv_ref(v_self__60983,"resources","attention")));
_ = R.push(v_refs__61035,F_dv_pair("threat",F_dv_ref(v_id__61011,"appraisal","threat")));
_ = R.push(v_refs__61035,F_dv_pair("fairness",F_dv_ref(v_id__61011,"appraisal","fairness")));
_ = R.push(v_refs__61035,F_dv_pair("control",F_dv_ref(v_id__61011,"appraisal","control")));
_ = R.push(v_refs__61035,F_dv_pair("certainty",F_dv_ref(v_id__61011,"appraisal","certainty")));
_ = R.push(v_refs__61035,F_dv_pair("importance",F_dv_ref(v_id__61011,"appraisal","importance")));
return v_refs__61035;
}
static object F_dv_appraise_request(object v_mind,object v_request) {
_ = F_dv_domain(v_mind,"mind");
object v_refs__62229 = F_dv_feature_refs(v_request);
object v_features__62269 = new System.Collections.Generic.List<object>(0) {};
object v_unknown__62291 = new System.Collections.Generic.List<object>(0) {};
object v_basis__62312 = new System.Collections.Generic.List<object>(0) {};
object v_i__62331 = R.number("0");
while (R.Bool(R.lt(v_i__62331,R.count(v_refs__62229)))) {
object v_entry__62381 = R.at(v_refs__62229,v_i__62331);
object v_key__62409 = R.at(v_entry__62381,R.number("0"));
object v_cell__62436 = F_dv_read(v_mind,R.at(v_entry__62381,R.number("1")));
if (R.Bool(F_dv_and(F_dv_get(v_cell__62436,"known"),R.not(F_dv_get(v_cell__62436,"conflicted"))))) {
_ = R.push(v_features__62269,F_dv_pair(v_key__62409,F_dv_get(v_cell__62436,"value")));
_ = R.push(v_basis__62312,new System.Collections.Generic.List<object>(3) {F_dv_pair("feature",v_key__62409),F_dv_pair("ref",R.at(v_entry__62381,R.number("1"))),F_dv_pair("cell",v_cell__62436)});
} else {
_ = R.push(v_unknown__62291,v_key__62409);
}
v_i__62331 = R.add(v_i__62331,R.number("1"));
}
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("features",v_features__62269),F_dv_pair("unknown",v_unknown__62291),F_dv_pair("basis",v_basis__62312),F_dv_pair("revision",F_dv_get(v_mind,"revision"))},new System.Collections.Generic.List<object>(2) {F_dv_pair("subject",F_dv_get(v_mind,"subject")),F_dv_pair("request",F_dv_get(v_request,"id"))});
}
static object F_dv_dot(object v_features,object v_weights) {
object v_i__62921 = R.number("0");
object v_score__62935 = R.number("0");
object v_terms__62953 = new System.Collections.Generic.List<object>(0) {};
object v_unknown__62972 = new System.Collections.Generic.List<object>(0) {};
while (R.Bool(R.lt(v_i__62921,R.count(v_weights)))) {
object v_entry__63032 = R.at(v_weights,v_i__62921);
object v_key__63063 = R.at(v_entry__63032,R.number("0"));
if (R.Bool(F_dv_has_key(v_features,v_key__63063))) {
object v_contribution__63132 = R.mul(F_dv_get(v_features,v_key__63063),R.at(v_entry__63032,R.number("1")));
v_score__62935 = R.add(v_score__62935,v_contribution__63132);
_ = R.push(v_terms__62953,F_dv_pair(v_key__63063,v_contribution__63132));
} else {
_ = R.push(v_unknown__62972,v_key__63063);
}
v_i__62921 = R.add(v_i__62921,R.number("1"));
}
return new System.Collections.Generic.List<object>(3) {F_dv_pair("score",v_score__62935),F_dv_pair("terms",v_terms__62953),F_dv_pair("unknown",v_unknown__62972)};
}
static object F_dv_clamp_signed(object v_value) {
if (R.Bool(R.lt(v_value,R.sub(R.number("0"),R.number("1"))))) {
return R.sub(R.number("0"),R.number("1"));
} else {
if (R.Bool(R.lt(R.number("1"),v_value))) {
return R.number("1");
} else {
return v_value;
}
}
}
static object F_dv_expression(object v_appraisal,object v_expression_policy) {
object v_axes__63630 = new System.Collections.Generic.List<object>(0) {};
object v_i__63648 = R.number("0");
while (R.Bool(R.lt(v_i__63648,R.count(v_expression_policy)))) {
object v_axis__63711 = R.at(v_expression_policy,v_i__63648);
object v_evaluation__63751 = F_dv_dot(F_dv_get(v_appraisal,"features"),R.at(v_axis__63711,R.number("1")));
_ = R.push(v_axes__63630,F_dv_pair(R.at(v_axis__63711,R.number("0")),F_dv_with_many(v_evaluation__63751,new System.Collections.Generic.List<object>(2) {F_dv_pair("value",F_dv_clamp_signed(F_dv_get(v_evaluation__63751,"score"))),F_dv_pair("complete",R.eq(R.count(F_dv_get(v_evaluation__63751,"unknown")),R.number("0")))})));
v_i__63648 = R.add(v_i__63648,R.number("1"));
}
return v_axes__63630;
}
static object F_dv_no_change_prediction(object v_mind) {
_ = F_dv_domain(v_mind,"mind");
return F_dv_with_many(v_mind,new System.Collections.Generic.List<object>(2) {F_dv_pair("domain","prediction"),F_dv_pair("source_revision",F_dv_get(v_mind,"revision"))});
}
static object F_dv_response_option(object v_id,object v_intent,object v_feasible,object v_prediction,object v_effort,object v_feature_weights) {
_ = F_dv_require(R.not(R.lt(v_effort,R.number("0"))),"Negative effort");
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",v_id),F_dv_pair("intent",v_intent),F_dv_pair("feasible",v_feasible),F_dv_pair("prediction",v_prediction)},new System.Collections.Generic.List<object>(2) {F_dv_pair("effort",v_effort),F_dv_pair("feature_weights",v_feature_weights)});
}
static object F_dv_validate_expression_policy(object v_expression_policy) {
_ = F_dv_require(F_dv_record_like(v_expression_policy),"Expression policy must be an axis-to-weight-record matrix");
object v_i__64618 = R.number("0");
while (R.Bool(R.lt(v_i__64618,R.count(v_expression_policy)))) {
object v_axis__64681 = R.at(v_expression_policy,v_i__64618);
object v_key__64721 = R.at(R.at(v_expression_policy,v_i__64618),R.number("0"));
object v_weights__64763 = R.at(R.at(v_expression_policy,v_i__64618),R.number("1"));
object v_j__64809 = R.number("0");
_ = F_dv_require(R.eq(R.slice(R.json_stringify(v_key__64721),R.number("0"),R.number("1")),"\""),"Expression policy axis name must be Text");
_ = F_dv_require(F_dv_record_like(v_weights__64763),R.cat("Expression policy axis weights must be a record: ",v_key__64721));
while (R.Bool(R.lt(v_j__64809,R.count(v_weights__64763)))) {
object v_weight__65093 = R.at(v_weights__64763,v_j__64809);
object v_feature__65129 = R.at(R.at(v_weights__64763,v_j__64809),R.number("0"));
_ = F_dv_require(R.eq(R.slice(R.json_stringify(v_feature__65129),R.number("0"),R.number("1")),"\""),"Expression policy feature name must be Text");
_ = F_dv_require(R.is_numeric_value(R.at(v_weight__65093,R.number("1"))),R.cat(R.cat(R.cat("Expression policy weight must be a finite Number: ",v_key__64721),"."),v_feature__65129));
v_j__64809 = R.add(v_j__64809,R.number("1"));
}
v_i__64618 = R.add(v_i__64618,R.number("1"));
}
return true;
}
static object F_dv_person_policy(object v_subject,object v_values,object v_effort_cost,object v_uncertainty_cost,object v_expression_policy) {
_ = F_dv_validate_expression_policy(v_expression_policy);
_ = F_dv_require(F_dv_and(R.not(R.lt(v_effort_cost,R.number("0"))),R.not(R.lt(v_uncertainty_cost,R.number("0")))),"Negative policy cost");
return F_dv_with(new System.Collections.Generic.List<object>(4) {F_dv_pair("subject",v_subject),F_dv_pair("values",v_values),F_dv_pair("effort_cost",v_effort_cost),F_dv_pair("uncertainty_cost",v_uncertainty_cost)},"expression",v_expression_policy);
}
static object F_dv_accept_policy(object v_mind,object v_id,object v_policy,object v_basis) {
_ = F_dv_domain(v_mind,"mind");
_ = F_dv_require(R.eq(F_dv_get(v_mind,"subject"),F_dv_get(v_policy,"subject")),"Foreign value policy");
_ = F_dv_require(R.lt(R.number("0"),R.count(v_basis)),"Value policy needs an acceptance basis");
object v_policies__66127 = F_dv_find(v_mind,"policies",new System.Collections.Generic.List<object>(0) {});
_ = F_dv_require(R.not(F_dv_has_key(v_policies__66127,v_id)),"Duplicate accepted policy");
return F_dv_with_many(v_mind,new System.Collections.Generic.List<object>(2) {F_dv_pair("policies",F_dv_with(v_policies__66127,v_id,new System.Collections.Generic.List<object>(3) {F_dv_pair("policy",v_policy),F_dv_pair("basis",v_basis),F_dv_pair("active",true)})),F_dv_pair("revision",R.add(F_dv_get(v_mind,"revision"),R.number("1")))});
}
static object F_dv_respond_adopted(object v_mind,object v_request,object v_options,object v_policy_id) {
object v_accepted__66463 = F_dv_get(F_dv_get(v_mind,"policies"),v_policy_id);
_ = F_dv_require(F_dv_get(v_accepted__66463,"active"),"Response policy is not adopted");
return F_dv_with(F_dv_respond(v_mind,v_request,v_options,F_dv_get(v_accepted__66463,"policy")),"policy_basis",F_dv_get(v_accepted__66463,"basis"));
}
static object F_dv_response_score(object v_mind,object v_option,object v_appraisal,object v_policy) {
object v_prediction__66741 = F_dv_get(v_option,"prediction");
_ = F_dv_domain(v_prediction__66741,"prediction");
_ = F_dv_require(R.eq(F_dv_get(v_prediction__66741,"source_revision"),F_dv_get(v_mind,"revision")),"Stale response prediction");
object v_value__66911 = F_dv_value_change(v_mind,v_prediction__66741,F_dv_get(v_policy,"values"));
object v_relation_and_state__66976 = F_dv_dot(F_dv_get(v_appraisal,"features"),F_dv_get(v_option,"feature_weights"));
object v_effort__67056 = R.mul(F_dv_get(v_option,"effort"),F_dv_get(v_policy,"effort_cost"));
object v_uncertainty__67110 = R.mul(R.add(R.count(F_dv_get(v_relation_and_state__66976,"unknown")),R.count(F_dv_get(F_dv_get(v_value__66911,"after"),"unknown"))),F_dv_get(v_policy,"uncertainty_cost"));
return F_dv_with(new System.Collections.Generic.List<object>(4) {F_dv_pair("score",R.sub(R.add(F_dv_get(v_value__66911,"delta"),F_dv_get(v_relation_and_state__66976,"score")),R.add(v_effort__67056,v_uncertainty__67110))),F_dv_pair("value",v_value__66911),F_dv_pair("relationship_and_state",v_relation_and_state__66976),F_dv_pair("effort",v_effort__67056)},"uncertainty_cost",v_uncertainty__67110);
}
static object F_dv_respond(object v_mind,object v_request,object v_options,object v_policy) {
_ = F_dv_domain(v_mind,"mind");
_ = F_dv_require(R.eq(F_dv_get(v_mind,"subject"),F_dv_get(v_policy,"subject")),"Foreign person policy");
object v_appraisal__67593 = F_dv_appraise_request(v_mind,v_request);
object v_i__67648 = R.number("0");
object v_found__67662 = false;
object v_best__67684 = R.number("0");
object v_chosen__67701 = false;
object v_evaluated__67724 = new System.Collections.Generic.List<object>(0) {};
object v_rejected__67747 = new System.Collections.Generic.List<object>(0) {};
object v_ids__67769 = new System.Collections.Generic.List<object>(0) {};
while (R.Bool(R.lt(v_i__67648,R.count(v_options)))) {
object v_option__67825 = R.at(v_options,v_i__67648);
object v_id__67857 = F_dv_get(v_option__67825,"id");
object v_legal__67884 = F_dv_truth(v_mind,F_dv_get(v_option__67825,"feasible"));
_ = F_dv_require(R.not(F_dv_has(v_ids__67769,v_id__67857)),"Duplicate response identity");
_ = R.push(v_ids__67769,v_id__67857);
if (R.Bool(R.eq(v_legal__67884,"true"))) {
object v_evaluation__68068 = F_dv_response_score(v_mind,v_option__67825,v_appraisal__67593,v_policy);
_ = R.push(v_evaluated__67724,F_dv_pair(v_id__67857,v_evaluation__68068));
object v_local_1__68204 = false;
if (R.Bool(v_found__67662)) {
v_local_1__68204 = R.lt(v_best__67684,F_dv_get(v_evaluation__68068,"score"));
} else {
v_local_1__68204 = true;
}
if (R.Bool(v_local_1__68204)) {
v_found__67662 = true;
v_best__67684 = F_dv_get(v_evaluation__68068,"score");
v_chosen__67701 = v_option__67825;
} else {
}
} else {
_ = R.push(v_rejected__67747,F_dv_pair(v_id__67857,v_legal__67884));
}
v_i__67648 = R.add(v_i__67648,R.number("1"));
}
object v_local_2__68560 = false;
if (R.Bool(v_found__67662)) {
v_local_2__68560 = "selected";
} else {
v_local_2__68560 = "no-response";
}
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("status",v_local_2__68560),F_dv_pair("choice",v_chosen__67701),F_dv_pair("appraisal",v_appraisal__67593),F_dv_pair("evaluated",v_evaluated__67724)},new System.Collections.Generic.List<object>(3) {F_dv_pair("rejected",v_rejected__67747),F_dv_pair("expression",F_dv_expression(v_appraisal__67593,F_dv_get(v_policy,"expression"))),F_dv_pair("committed",false)});
}
static object F_dv_relax(object v_current,object v_baseline,object v_retention) {
return R.add(v_baseline,R.mul(R.sub(v_current,v_baseline),F_dv_unit(v_retention)));
}
static object F_dv_affect_proposal(object v_mind,object v_request,object v_policy) {
object v_appraisal__69042 = F_dv_appraise_request(v_mind,v_request);
object v_features__69097 = F_dv_get(v_appraisal__69042,"features");
object v_self__69135 = F_dv_get(v_request,"self");
object v_valence_ref__69163 = F_dv_ref(v_self__69135,"affect","valence");
object v_arousal_ref__69219 = F_dv_ref(v_self__69135,"affect","arousal");
object v_valence__69275 = F_dv_read(v_mind,v_valence_ref__69163);
object v_arousal__69320 = F_dv_read(v_mind,v_arousal_ref__69219);
_ = F_dv_require(F_dv_and(F_dv_get(v_valence__69275,"known"),F_dv_get(v_arousal__69320,"known")),"Unknown current affect");
object v_v_delta__69444 = F_dv_dot(v_features__69097,F_dv_get(v_policy,"valence_weights"));
object v_a_delta__69503 = F_dv_dot(v_features__69097,F_dv_get(v_policy,"arousal_weights"));
return new System.Collections.Generic.List<object>(3) {F_dv_pair("source_revision",F_dv_get(v_mind,"revision")),F_dv_pair("basis",v_appraisal__69042),F_dv_pair("effects",F_dv_pair(F_dv_assign(v_valence_ref__69163,F_dv_clamp_signed(R.add(F_dv_relax(F_dv_get(v_valence__69275,"value"),F_dv_get(v_policy,"baseline_valence"),F_dv_get(v_policy,"retention")),F_dv_get(v_v_delta__69444,"score")))),F_dv_assign(v_arousal_ref__69219,F_dv_clamp_signed(R.add(F_dv_relax(F_dv_get(v_arousal__69320,"value"),F_dv_get(v_policy,"baseline_arousal"),F_dv_get(v_policy,"retention")),F_dv_get(v_a_delta__69503,"score"))))))};
}
static object F_dv_acceptance_classify(object v_degree,object v_levels) {
_ = F_dv_unit(v_degree);
_ = F_dv_require(R.lt(R.number("0"),R.count(v_levels)),"Acceptance requires levels");
_ = F_dv_require(R.eq(R.at(R.at(v_levels,R.number("0")),R.number("1")),R.number("0")),"Acceptance levels must start at zero");
object v_previous__70224 = R.sub(R.number("0"),R.number("1"));
object v_selected__70246 = "";
object v_i__70268 = R.number("0");
while (R.Bool(R.lt(v_i__70268,R.count(v_levels)))) {
object v_threshold__70318 = F_dv_unit(R.at(R.at(v_levels,v_i__70268),R.number("1")));
_ = F_dv_require(R.lt(v_previous__70224,v_threshold__70318),"Acceptance thresholds must increase");
if (R.Bool(R.not(R.lt(v_degree,v_threshold__70318)))) {
v_selected__70246 = R.at(R.at(v_levels,v_i__70268),R.number("0"));
} else {
}
v_previous__70224 = v_threshold__70318;
v_i__70268 = R.add(v_i__70268,R.number("1"));
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("degree",v_degree),F_dv_pair("level",v_selected__70246)};
}
static object F_dv_threshold_acceptance(object v_value,object v_threshold) {
return F_dv_acceptance_classify(v_value,new System.Collections.Generic.List<object>(2) {new System.Collections.Generic.List<object>(2) {"held",R.number("0")},new System.Collections.Generic.List<object>(2) {"accepted",v_threshold}});
}
static object F_dv_acceptance_transition(object v_previous,object v_current,object v_source) {
object v_transition__70812 = "unchanged";
if (R.Bool(R.not(R.eq(F_dv_get(v_previous,"level"),F_dv_get(v_current,"level"))))) {
v_transition__70812 = "changed";
if (R.Bool((R.Bool(R.eq(F_dv_get(v_previous,"level"),"accepted")) ? (object)(R.not(R.eq(F_dv_get(v_current,"level"),"accepted"))) : (object)(false)))) {
v_transition__70812 = "withdrawn";
} else {
}
} else {
}
return new System.Collections.Generic.List<object>(4) {F_dv_pair("before",v_previous),F_dv_pair("after",v_current),F_dv_pair("transition",v_transition__70812),F_dv_pair("source",v_source)};
}
static object F_dv_balance_dot(object v_a,object v_b) {
_ = F_dv_require(R.eq(R.count(v_a),R.count(v_b)),"Balance dimensions differ");
object v_result__71371 = R.exact("0");
object v_i__71402 = R.number("0");
while (R.Bool(R.lt(v_i__71402,R.count(v_a)))) {
v_result__71371 = R.add(v_result__71371,R.mul(R.exact(R.at(v_a,v_i__71402)),R.exact(R.at(v_b,v_i__71402))));
v_i__71402 = R.add(v_i__71402,R.number("1"));
}
return v_result__71371;
}
static object F_dv_balance_extent(object v_variables) {
object v_total__71571 = R.number("1");
object v_i__71589 = R.number("0");
while (R.Bool(R.lt(v_i__71589,R.count(v_variables)))) {
object v_count__71642 = R.count(F_dv_get(R.at(v_variables,v_i__71589),"options"));
_ = F_dv_require(R.lt(R.number("0"),v_count__71642),"Balance variable has an empty domain");
v_total__71571 = R.mul(v_total__71571,v_count__71642);
_ = F_dv_require(R.not(R.lt(R.number("9007199254740991"),v_total__71571)),"Finite domain index exceeds exact integer range");
v_i__71589 = R.add(v_i__71589,R.number("1"));
}
return v_total__71571;
}
static object F_dv_balance_candidate(object v_index,object v_goal,object v_variables,object v_laws,object v_limits,object v_prices) {
object v_cursor__71993 = v_index;
object v_values__72016 = new System.Collections.Generic.List<object>(0) {};
object v_costs__72036 = new System.Collections.Generic.List<object>(0) {};
object v_choices__72055 = new System.Collections.Generic.List<object>(0) {};
object v_j__72076 = R.number("0");
while (R.Bool(R.lt(v_j__72076,R.count(F_dv_get(v_limits,"balances"))))) {
_ = R.push(v_costs__72036,R.exact("0"));
_ = F_dv_require(R.not(R.lt(R.exact(R.at(F_dv_get(v_limits,"balances"),v_j__72076)),R.number("0"))),"Negative resource balance");
_ = F_dv_require(R.not(R.lt(R.exact(R.at(v_prices,v_j__72076)),R.number("0"))),"Negative resource price");
v_j__72076 = R.add(v_j__72076,R.number("1"));
}
object v_i__72342 = R.number("0");
while (R.Bool(R.lt(v_i__72342,R.count(v_variables)))) {
object v_count__72395 = R.count(F_dv_get(R.at(v_variables,v_i__72342),"options"));
object v_quotient__72446 = R.floor(R.div(v_cursor__71993,v_count__72395));
object v_choice__72494 = R.sub(v_cursor__71993,R.mul(v_quotient__72446,v_count__72395));
v_cursor__71993 = v_quotient__72446;
object v_option__72567 = R.at(F_dv_get(R.at(v_variables,v_i__72342),"options"),v_choice__72494);
_ = R.push(v_choices__72055,v_choice__72494);
_ = R.push(v_values__72016,R.exact(F_dv_get(v_option__72567,"value")));
_ = F_dv_require(R.eq(R.count(F_dv_get(v_option__72567,"cost")),R.count(v_costs__72036)),"Resource cost dimensions differ");
v_j__72076 = R.number("0");
while (R.Bool(R.lt(v_j__72076,R.count(v_costs__72036)))) {
object v_charge__72847 = R.exact(R.at(F_dv_get(v_option__72567,"cost"),v_j__72076));
_ = F_dv_require(R.not(R.lt(v_charge__72847,R.number("0"))),"Negative imprint charge");
_ = R.replace(v_costs__72036,v_j__72076,R.add(R.at(v_costs__72036,v_j__72076),v_charge__72847));
v_j__72076 = R.add(v_j__72076,R.number("1"));
}
v_i__72342 = R.add(v_i__72342,R.number("1"));
}
object v_degree__73044 = F_dv_balance_dot(F_dv_get(v_goal,"coefficients"),v_values__72016);
object v_feasible__73103 = (R.Bool((R.Bool(R.not(R.lt(v_degree__73044,R.exact(F_dv_get(v_goal,"minimum"))))) ? (object)(R.not(R.lt(v_degree__73044,R.number("0")))) : (object)(false))) ? (object)(R.not(R.lt(R.number("1"),v_degree__73044))) : (object)(false));
v_i__72342 = R.number("0");
while (R.Bool(R.lt(v_i__72342,R.count(v_laws)))) {
if (R.Bool(R.not(R.eq(F_dv_balance_dot(F_dv_get(R.at(v_laws,v_i__72342),"coefficients"),v_values__72016),R.exact(F_dv_get(R.at(v_laws,v_i__72342),"total")))))) {
v_feasible__73103 = false;
} else {
}
v_i__72342 = R.add(v_i__72342,R.number("1"));
}
v_j__72076 = R.number("0");
while (R.Bool(R.lt(v_j__72076,R.count(v_costs__72036)))) {
if (R.Bool(R.lt(R.exact(R.at(F_dv_get(v_limits,"balances"),v_j__72076)),R.at(v_costs__72036,v_j__72076)))) {
v_feasible__73103 = false;
} else {
}
v_j__72076 = R.add(v_j__72076,R.number("1"));
}
return new System.Collections.Generic.List<object>(7) {F_dv_pair("index",v_index),F_dv_pair("choices",v_choices__72055),F_dv_pair("values",v_values__72016),F_dv_pair("degree",v_degree__73044),F_dv_pair("charges",v_costs__72036),F_dv_pair("cost",F_dv_balance_dot(v_costs__72036,v_prices)),F_dv_pair("feasible",v_feasible__73103)};
}
static object F_dv_balance_search_limit(object v_total,object v_limits) {
_ = F_dv_require((R.Bool(R.not(R.lt(F_dv_get(v_limits,"search_budget"),R.number("0")))) ? (object)(R.eq(R.floor(F_dv_get(v_limits,"search_budget")),F_dv_get(v_limits,"search_budget"))) : (object)(false)),"Invalid search budget");
_ = F_dv_require(R.not(R.lt(F_dv_get(v_limits,"work_remaining"),R.number("0"))),"Negative imprint work budget");
object v_covered__74067 = F_dv_min(v_total,F_dv_get(v_limits,"search_budget"));
if (R.Bool(R.lt(F_dv_get(v_limits,"work_remaining"),R.mul(v_covered__74067,R.number("2"))))) {
object v_lower__74332 = R.number("0");
object v_upper__74354 = v_covered__74067;
while (R.Bool(R.lt(v_lower__74332,v_upper__74354))) {
object v_middle__74415 = R.add(v_lower__74332,R.floor(R.div(R.add(R.sub(v_upper__74354,v_lower__74332),R.number("1")),R.number("2"))));
if (R.Bool(R.not(R.lt(F_dv_get(v_limits,"work_remaining"),R.mul(v_middle__74415,R.number("2")))))) {
v_lower__74332 = v_middle__74415;
} else {
v_upper__74354 = R.sub(v_middle__74415,R.number("1"));
}
}
v_covered__74067 = v_lower__74332;
} else {
}
return v_covered__74067;
}
static object F_dv_balance_stop_reason(object v_visited,object v_total,object v_limits) {
if (R.Bool(R.eq(v_visited,v_total))) {
return "domain-covered";
} else {
}
object v_search_stop__74762 = R.not(R.lt(v_visited,F_dv_get(v_limits,"search_budget")));
object v_work_stop__74816 = R.lt(F_dv_get(v_limits,"work_remaining"),R.mul(R.add(v_visited,R.number("1")),R.number("2")));
if (R.Bool((R.Bool(v_search_stop__74762) ? (object)(v_work_stop__74816) : (object)(false)))) {
return "search-and-work-budget";
} else {
}
if (R.Bool(v_search_stop__74762)) {
return "search-budget";
} else {
}
if (R.Bool(v_work_stop__74816)) {
return "work-budget";
} else {
}
return "prefix-stop";
}
static object F_dv_imprint_analyze(object v_goal,object v_variables,object v_laws,object v_limits,object v_prices) {
_ = F_dv_require(R.eq(R.count(v_prices),R.count(F_dv_get(v_limits,"balances"))),"Resource price dimensions differ");
object v_total__75407 = F_dv_balance_extent(v_variables);
_ = F_dv_require(R.eq(R.count(F_dv_get(v_goal,"coefficients")),R.count(v_variables)),"Balance dimensions differ");
object v_i__75548 = R.number("0");
while (R.Bool(R.lt(v_i__75548,R.count(v_laws)))) {
_ = F_dv_require(R.eq(R.count(F_dv_get(R.at(v_laws,v_i__75548),"coefficients")),R.count(v_variables)),"Balance dimensions differ");
v_i__75548 = R.add(v_i__75548,R.number("1"));
}
object v_option_count__75713 = R.number("0");
v_i__75548 = R.number("0");
while (R.Bool(R.lt(v_i__75548,R.count(v_variables)))) {
v_option_count__75713 = R.add(v_option_count__75713,R.count(F_dv_get(R.at(v_variables,v_i__75548),"options")));
v_i__75548 = R.add(v_i__75548,R.number("1"));
}
object v_candidate_limit__75870 = F_dv_balance_search_limit(v_total__75407,v_limits);
object v_branch_applicable__75935 = R.lt(R.number("0"),R.count(v_variables));
object v_recommended__75987 = "enumerate";
object v_reason__76021 = "reference-path-has-lower-setup-cost";
if (R.Bool((R.Bool(v_branch_applicable__75935) ? (object)(R.lt(R.mul(v_option_count__75713,R.number("2")),v_candidate_limit__75870)) : (object)(false)))) {
v_recommended__75987 = "branch_bound";
v_reason__76021 = "cartesian-prefix-exceeds-linear-bound-preparation";
} else {
}
if (R.Bool(R.not(v_branch_applicable__75935))) {
v_reason__76021 = "zero-dimensional-reference-domain";
} else {
}
return new System.Collections.Generic.List<object>(16) {F_dv_pair("family","finite-linear-cartesian"),F_dv_pair("arithmetic","exact-rational"),F_dv_pair("branch_bound_applicable",v_branch_applicable__75935),F_dv_pair("recommended",v_recommended__75987),F_dv_pair("selection_reason",v_reason__76021),F_dv_pair("total",v_total__75407),F_dv_pair("candidate_limit",v_candidate_limit__75870),F_dv_pair("option_count",v_option_count__75713),F_dv_pair("budget_covers_domain",R.eq(v_candidate_limit__75870,v_total__75407)),F_dv_pair("stop_reason",F_dv_balance_stop_reason(v_candidate_limit__75870,v_total__75407,v_limits)),F_dv_pair("budget_unit","covered-candidate-prefix"),F_dv_pair("work_per_covered_candidate",R.number("2")),F_dv_pair("bound_scope","separable-linear-intervals-may-be-loose"),F_dv_pair("requirements","finite-nonempty-options; matching-linear-dimensions; exact-numeric-values; nonnegative-charges-and-prices"),F_dv_pair("validation","shape-and-budget; selected-solver-validates-numeric-options"),F_dv_pair("time_guarantee","none")};
}
static object F_dv_balance_finish(object v_best,object v_visited,object v_total,object v_limits,object v_algorithm,object v_evaluated,object v_bound_checks,object v_preprocessed,object v_coverage,object v_analysis) {
object v_status__77169 = "Unresolved";
if (R.Bool(R.not(R.eq(v_best,false)))) {
v_status__77169 = "Feasible";
if (R.Bool(R.eq(v_visited,v_total))) {
v_status__77169 = "Optimal";
} else {
}
} else {
if (R.Bool(R.eq(v_visited,v_total))) {
v_status__77169 = "Infeasible";
} else {
}
}
object v_proof_kind__77382 = "enumerated-prefix-v1";
if (R.Bool(R.eq(v_algorithm,"branch_bound"))) {
v_proof_kind__77382 = "linear-interval-prefix-v1";
} else {
}
return new System.Collections.Generic.List<object>(20) {F_dv_pair("status",v_status__77169),F_dv_pair("best",v_best),F_dv_pair("visited",v_visited),F_dv_pair("total",v_total),F_dv_pair("revision",F_dv_get(v_limits,"revision")),F_dv_pair("subject",F_dv_get(v_limits,"subject")),F_dv_pair("algorithm",v_algorithm),F_dv_pair("proof_kind",v_proof_kind__77382),F_dv_pair("coverage",v_coverage),F_dv_pair("arithmetic","exact-rational"),F_dv_pair("complete",R.eq(v_visited,v_total)),F_dv_pair("stop_reason",F_dv_balance_stop_reason(v_visited,v_total,v_limits)),F_dv_pair("covered_candidates",v_visited),F_dv_pair("charged_work",R.mul(v_visited,R.number("2"))),F_dv_pair("candidate_evaluations",v_evaluated),F_dv_pair("pruned_candidates",R.sub(v_visited,v_evaluated)),F_dv_pair("bound_checks",v_bound_checks),F_dv_pair("preprocessed_options",v_preprocessed),F_dv_pair("counter_scope","search-diagnostics-not-total-runtime-or-certificate-authority"),F_dv_pair("analysis",v_analysis)};
}
static object F_dv_imprint_enumerate(object v_goal,object v_variables,object v_laws,object v_limits,object v_prices,object v_analysis) {
object v_total__78279 = F_dv_get(v_analysis,"total");
object v_visited__78310 = R.number("0");
object v_best__78330 = false;
while (R.Bool(R.lt(v_visited__78310,F_dv_get(v_analysis,"candidate_limit")))) {
object v_candidate__78401 = F_dv_balance_candidate(v_visited__78310,v_goal,v_variables,v_laws,v_limits,v_prices);
if (R.Bool(F_dv_get(v_candidate__78401,"feasible"))) {
if (R.Bool(R.eq(v_best__78330,false))) {
v_best__78330 = v_candidate__78401;
} else {
if (R.Bool(R.lt(F_dv_get(v_candidate__78401,"cost"),F_dv_get(v_best__78330,"cost")))) {
v_best__78330 = v_candidate__78401;
} else {
}
}
} else {
}
v_visited__78310 = R.add(v_visited__78310,R.number("1"));
}
return F_dv_balance_finish(v_best__78330,v_visited__78310,v_total__78279,v_limits,"enumerate",v_visited__78310,R.number("0"),R.number("0"),new System.Collections.Generic.List<object>(0) {},v_analysis);
}
static object F_dv_balance_zeroes(object v_count) {
object v_result__78847 = new System.Collections.Generic.List<object>(0) {};
object v_i__78867 = R.number("0");
while (R.Bool(R.lt(v_i__78867,v_count))) {
_ = R.push(v_result__78847,R.exact("0"));
v_i__78867 = R.add(v_i__78867,R.number("1"));
}
return v_result__78847;
}
static object F_dv_balance_interval_model(object v_goal,object v_variables,object v_laws,object v_limits,object v_prices) {
_ = F_dv_require(R.eq(R.count(v_prices),R.count(F_dv_get(v_limits,"balances"))),"Resource price dimensions differ");
_ = F_dv_require(R.eq(R.count(F_dv_get(v_goal,"coefficients")),R.count(v_variables)),"Balance dimensions differ");
object v_field_count__79405 = R.add(R.add(R.number("2"),R.count(v_laws)),R.count(v_prices));
object v_exact_prices__79465 = new System.Collections.Generic.List<object>(0) {};
object v_balances__79491 = new System.Collections.Generic.List<object>(0) {};
object v_law_totals__79513 = new System.Collections.Generic.List<object>(0) {};
object v_i__79537 = R.number("0");
while (R.Bool(R.lt(v_i__79537,R.count(v_prices)))) {
object v_price__79587 = R.exact(R.at(v_prices,v_i__79537));
object v_balance__79627 = R.exact(R.at(F_dv_get(v_limits,"balances"),v_i__79537));
_ = F_dv_require(R.not(R.lt(v_price__79587,R.number("0"))),"Negative resource price");
_ = F_dv_require(R.not(R.lt(v_balance__79627,R.number("0"))),"Negative resource balance");
_ = R.push(v_exact_prices__79465,v_price__79587);
_ = R.push(v_balances__79491,v_balance__79627);
v_i__79537 = R.add(v_i__79537,R.number("1"));
}
v_i__79537 = R.number("0");
while (R.Bool(R.lt(v_i__79537,R.count(v_laws)))) {
_ = F_dv_require(R.eq(R.count(F_dv_get(R.at(v_laws,v_i__79537),"coefficients")),R.count(v_variables)),"Balance dimensions differ");
_ = R.push(v_law_totals__79513,R.exact(F_dv_get(R.at(v_laws,v_i__79537),"total")));
v_i__79537 = R.add(v_i__79537,R.number("1"));
}
object v_domains__80098 = new System.Collections.Generic.List<object>(0) {};
object v_widths__80119 = new System.Collections.Generic.List<object>(1) {R.number("1")};
object v_lower__80140 = new System.Collections.Generic.List<object>(1) {F_dv_balance_zeroes(v_field_count__79405)};
object v_upper__80189 = new System.Collections.Generic.List<object>(1) {F_dv_balance_zeroes(v_field_count__79405)};
object v_option_count__80238 = R.number("0");
v_i__79537 = R.number("0");
while (R.Bool(R.lt(v_i__79537,R.count(v_variables)))) {
object v_domain__80312 = new System.Collections.Generic.List<object>(0) {};
object v_minimum__80336 = new System.Collections.Generic.List<object>(0) {};
object v_maximum__80361 = new System.Collections.Generic.List<object>(0) {};
object v_choice__80386 = R.number("0");
while (R.Bool(R.lt(v_choice__80386,R.count(F_dv_get(R.at(v_variables,v_i__79537),"options"))))) {
object v_option__80468 = R.at(F_dv_get(R.at(v_variables,v_i__79537),"options"),v_choice__80386);
object v_value__80522 = R.exact(F_dv_get(v_option__80468,"value"));
_ = F_dv_require(R.eq(R.count(F_dv_get(v_option__80468,"cost")),R.count(v_prices)),"Resource cost dimensions differ");
object v_terms__80670 = new System.Collections.Generic.List<object>(1) {R.mul(R.exact(R.at(F_dv_get(v_goal,"coefficients"),v_i__79537)),v_value__80522)};
object v_j__80735 = R.number("0");
while (R.Bool(R.lt(v_j__80735,R.count(v_laws)))) {
_ = R.push(v_terms__80670,R.mul(R.exact(R.at(F_dv_get(R.at(v_laws,v_j__80735),"coefficients"),v_i__79537)),v_value__80522));
v_j__80735 = R.add(v_j__80735,R.number("1"));
}
object v_cost__80895 = R.exact("0");
v_j__80735 = R.number("0");
while (R.Bool(R.lt(v_j__80735,R.count(v_prices)))) {
object v_charge__80994 = R.exact(R.at(F_dv_get(v_option__80468,"cost"),v_j__80735));
_ = F_dv_require(R.not(R.lt(v_charge__80994,R.number("0"))),"Negative imprint charge");
_ = R.push(v_terms__80670,v_charge__80994);
v_cost__80895 = R.add(v_cost__80895,R.mul(v_charge__80994,R.at(v_exact_prices__79465,v_j__80735)));
v_j__80735 = R.add(v_j__80735,R.number("1"));
}
_ = R.push(v_terms__80670,v_cost__80895);
_ = R.push(v_domain__80312,v_terms__80670);
if (R.Bool(R.eq(v_choice__80386,R.number("0")))) {
v_minimum__80336 = F_dv_clone(v_terms__80670);
v_maximum__80361 = F_dv_clone(v_terms__80670);
} else {
v_j__80735 = R.number("0");
while (R.Bool(R.lt(v_j__80735,v_field_count__79405))) {
_ = R.replace(v_minimum__80336,v_j__80735,F_dv_min(R.at(v_minimum__80336,v_j__80735),R.at(v_terms__80670,v_j__80735)));
_ = R.replace(v_maximum__80361,v_j__80735,F_dv_max(R.at(v_maximum__80361,v_j__80735),R.at(v_terms__80670,v_j__80735)));
v_j__80735 = R.add(v_j__80735,R.number("1"));
}
}
v_choice__80386 = R.add(v_choice__80386,R.number("1"));
v_option_count__80238 = R.add(v_option_count__80238,R.number("1"));
}
object v_next_lower__81738 = new System.Collections.Generic.List<object>(0) {};
object v_next_upper__81766 = new System.Collections.Generic.List<object>(0) {};
object v_field_index__81794 = R.number("0");
while (R.Bool(R.lt(v_field_index__81794,v_field_count__79405))) {
_ = R.push(v_next_lower__81738,R.add(R.at(R.at(v_lower__80140,v_i__79537),v_field_index__81794),R.at(v_minimum__80336,v_field_index__81794)));
_ = R.push(v_next_upper__81766,R.add(R.at(R.at(v_upper__80189,v_i__79537),v_field_index__81794),R.at(v_maximum__80361,v_field_index__81794)));
v_field_index__81794 = R.add(v_field_index__81794,R.number("1"));
}
_ = R.push(v_domains__80098,v_domain__80312);
_ = R.push(v_widths__80119,R.mul(R.at(v_widths__80119,v_i__79537),R.count(v_domain__80312)));
_ = R.push(v_lower__80140,v_next_lower__81738);
_ = R.push(v_upper__80189,v_next_upper__81766);
v_i__79537 = R.add(v_i__79537,R.number("1"));
}
return new System.Collections.Generic.List<object>(9) {F_dv_pair("domains",v_domains__80098),F_dv_pair("widths",v_widths__80119),F_dv_pair("lower",v_lower__80140),F_dv_pair("upper",v_upper__80189),F_dv_pair("field_count",v_field_count__79405),F_dv_pair("goal_minimum",R.exact(F_dv_get(v_goal,"minimum"))),F_dv_pair("law_totals",v_law_totals__79513),F_dv_pair("balances",v_balances__79491),F_dv_pair("option_count",v_option_count__80238)};
}
static object F_dv_balance_interval_kind(object v_model,object v_depth,object v_fixed,object v_best) {
object v_lower__82499 = R.at(F_dv_get(v_model,"lower"),v_depth);
object v_upper__82534 = R.at(F_dv_get(v_model,"upper"),v_depth);
object v_goal_lower__82569 = R.add(R.at(v_fixed,R.number("0")),R.at(v_lower__82499,R.number("0")));
object v_goal_upper__82610 = R.add(R.at(v_fixed,R.number("0")),R.at(v_upper__82534,R.number("0")));
if (R.Bool((R.Bool((R.Bool(R.lt(v_goal_upper__82610,F_dv_get(v_model,"goal_minimum"))) ? (object)(true) : (object)(R.lt(v_goal_upper__82610,R.number("0"))))) ? (object)(true) : (object)(R.lt(R.number("1"),v_goal_lower__82569))))) {
return "goal-bound";
} else {
}
object v_j__82755 = R.number("0");
while (R.Bool(R.lt(v_j__82755,R.count(F_dv_get(v_model,"law_totals"))))) {
object v_field__82815 = R.add(R.number("1"),v_j__82755);
if (R.Bool((R.Bool(R.lt(R.at(F_dv_get(v_model,"law_totals"),v_j__82755),R.add(R.at(v_fixed,v_field__82815),R.at(v_lower__82499,v_field__82815)))) ? (object)(true) : (object)(R.lt(R.add(R.at(v_fixed,v_field__82815),R.at(v_upper__82534,v_field__82815)),R.at(F_dv_get(v_model,"law_totals"),v_j__82755)))))) {
return "law-bound";
} else {
}
v_j__82755 = R.add(v_j__82755,R.number("1"));
}
v_j__82755 = R.number("0");
while (R.Bool(R.lt(v_j__82755,R.count(F_dv_get(v_model,"balances"))))) {
object v_field__83055 = R.add(R.add(R.number("1"),R.count(F_dv_get(v_model,"law_totals"))),v_j__82755);
if (R.Bool(R.lt(R.at(F_dv_get(v_model,"balances"),v_j__82755),R.add(R.at(v_fixed,v_field__83055),R.at(v_lower__82499,v_field__83055))))) {
return "resource-bound";
} else {
}
v_j__82755 = R.add(v_j__82755,R.number("1"));
}
if (R.Bool(R.not(R.eq(v_best,false)))) {
object v_field__83246 = R.sub(F_dv_get(v_model,"field_count"),R.number("1"));
if (R.Bool(R.not(R.lt(R.add(R.at(v_fixed,v_field__83246),R.at(v_lower__82499,v_field__83246)),F_dv_get(v_best,"cost"))))) {
return "cost-bound";
} else {
}
} else {
}
return "";
}
static object F_dv_balance_interval_choices(object v_model,object v_index) {
object v_choices__83430 = new System.Collections.Generic.List<object>(0) {};
object v_cursor__83451 = v_index;
object v_i__83474 = R.number("0");
while (R.Bool(R.lt(v_i__83474,R.count(F_dv_get(v_model,"domains"))))) {
object v_count__83531 = R.count(R.at(F_dv_get(v_model,"domains"),v_i__83474));
object v_quotient__83578 = R.floor(R.div(v_cursor__83451,v_count__83531));
_ = R.push(v_choices__83430,R.sub(v_cursor__83451,R.mul(v_quotient__83578,v_count__83531)));
v_cursor__83451 = v_quotient__83578;
v_i__83474 = R.add(v_i__83474,R.number("1"));
}
return v_choices__83430;
}
static object F_dv_balance_interval_probe(object v_model,object v_start,object v_best) {
object v_choices__83872 = F_dv_balance_interval_choices(v_model,v_start);
object v_fixed__83932 = F_dv_balance_zeroes(F_dv_get(v_model,"field_count"));
object v_depth__83985 = R.count(F_dv_get(v_model,"domains"));
object v_checks__84025 = R.number("0");
while (R.Bool(R.not(R.lt(v_depth__83985,R.number("0"))))) {
object v_width__84070 = R.at(F_dv_get(v_model,"widths"),v_depth__83985);
if (R.Bool(R.eq(R.mul(R.floor(R.div(v_start,v_width__84070)),v_width__84070),v_start))) {
v_checks__84025 = R.add(v_checks__84025,R.number("1"));
object v_kind__84199 = F_dv_balance_interval_kind(v_model,v_depth__83985,v_fixed__83932,v_best);
if (R.Bool(R.not(R.eq(v_kind__84199,"")))) {
return new System.Collections.Generic.List<object>(3) {F_dv_pair("depth",v_depth__83985),F_dv_pair("kind",v_kind__84199),F_dv_pair("bound_checks",v_checks__84025)};
} else {
}
} else {
}
if (R.Bool(R.lt(R.number("0"),v_depth__83985))) {
object v_terms__84395 = R.at(R.at(F_dv_get(v_model,"domains"),R.sub(v_depth__83985,R.number("1"))),R.at(v_choices__83872,R.sub(v_depth__83985,R.number("1"))));
object v_j__84464 = R.number("0");
while (R.Bool(R.lt(v_j__84464,F_dv_get(v_model,"field_count")))) {
_ = R.replace(v_fixed__83932,v_j__84464,R.add(R.at(v_fixed__83932,v_j__84464),R.at(v_terms__84395,v_j__84464)));
v_j__84464 = R.add(v_j__84464,R.number("1"));
}
} else {
}
v_depth__83985 = R.sub(v_depth__83985,R.number("1"));
}
return new System.Collections.Generic.List<object>(3) {F_dv_pair("depth",R.number("0")),F_dv_pair("kind","candidate"),F_dv_pair("bound_checks",v_checks__84025)};
}
static object F_dv_imprint_branch_bound(object v_goal,object v_variables,object v_laws,object v_limits,object v_prices,object v_analysis) {
if (R.Bool(R.eq(F_dv_get(v_analysis,"candidate_limit"),R.number("0")))) {
return F_dv_balance_finish(false,R.number("0"),F_dv_get(v_analysis,"total"),v_limits,"branch_bound",R.number("0"),R.number("0"),R.number("0"),new System.Collections.Generic.List<object>(0) {},v_analysis);
} else {
}
object v_model__84914 = F_dv_balance_interval_model(v_goal,v_variables,v_laws,v_limits,v_prices);
object v_visited__84995 = R.number("0");
object v_evaluated__85015 = R.number("0");
object v_bound_checks__85037 = R.number("0");
object v_coverage__85062 = new System.Collections.Generic.List<object>(0) {};
object v_best__85084 = false;
while (R.Bool(R.lt(v_visited__84995,F_dv_get(v_analysis,"candidate_limit")))) {
object v_probe__85155 = F_dv_balance_interval_probe(v_model__84914,v_visited__84995,v_best__85084);
object v_count__85223 = F_dv_min(R.at(F_dv_get(v_model__84914,"widths"),F_dv_get(v_probe__85155,"depth")),R.sub(F_dv_get(v_analysis,"candidate_limit"),v_visited__84995));
v_bound_checks__85037 = R.add(v_bound_checks__85037,F_dv_get(v_probe__85155,"bound_checks"));
if (R.Bool(R.eq(F_dv_get(v_probe__85155,"kind"),"candidate"))) {
object v_candidate__85412 = F_dv_balance_candidate(v_visited__84995,v_goal,v_variables,v_laws,v_limits,v_prices);
v_evaluated__85015 = R.add(v_evaluated__85015,R.number("1"));
if (R.Bool(F_dv_get(v_candidate__85412,"feasible"))) {
if (R.Bool(R.eq(v_best__85084,false))) {
v_best__85084 = v_candidate__85412;
} else {
if (R.Bool(R.lt(F_dv_get(v_candidate__85412,"cost"),F_dv_get(v_best__85084,"cost")))) {
v_best__85084 = v_candidate__85412;
} else {
}
}
} else {
}
} else {
}
_ = R.push(v_coverage__85062,new System.Collections.Generic.List<object>(4) {F_dv_pair("start",v_visited__84995),F_dv_pair("depth",F_dv_get(v_probe__85155,"depth")),F_dv_pair("count",v_count__85223),F_dv_pair("kind",F_dv_get(v_probe__85155,"kind"))});
v_visited__84995 = R.add(v_visited__84995,v_count__85223);
}
return F_dv_balance_finish(v_best__85084,v_visited__84995,F_dv_get(v_analysis,"total"),v_limits,"branch_bound",v_evaluated__85015,v_bound_checks__85037,F_dv_get(v_model__84914,"option_count"),v_coverage__85062,v_analysis);
}
static object F_dv_imprint_solve_with(object v_capability,object v_goal,object v_variables,object v_laws,object v_limits,object v_prices,object v_algorithm) {
_ = F_dv_require(v_capability,"Imprint capability is required");
_ = F_dv_require((R.Bool((R.Bool(R.eq(v_algorithm,"auto")) ? (object)(true) : (object)(R.eq(v_algorithm,"enumerate")))) ? (object)(true) : (object)(R.eq(v_algorithm,"branch_bound"))),"Unknown imprint solver");
object v_base_analysis__86395 = F_dv_imprint_analyze(v_goal,v_variables,v_laws,v_limits,v_prices);
object v_selected__86477 = v_algorithm;
if (R.Bool(R.eq(v_selected__86477,"auto"))) {
v_selected__86477 = F_dv_get(v_base_analysis__86395,"recommended");
} else {
}
object v_fallback_reason__86578 = "";
if (R.Bool((R.Bool(R.eq(v_selected__86477,"branch_bound")) ? (object)(R.not(F_dv_get(v_base_analysis__86395,"branch_bound_applicable"))) : (object)(false)))) {
v_selected__86477 = "enumerate";
v_fallback_reason__86578 = "unsupported-bound-model-reference-fallback";
} else {
}
object v_analysis__86790 = F_dv_with_many(v_base_analysis__86395,new System.Collections.Generic.List<object>(3) {F_dv_pair("requested_algorithm",v_algorithm),F_dv_pair("selected_algorithm",v_selected__86477),F_dv_pair("fallback_reason",v_fallback_reason__86578)});
if (R.Bool(R.eq(v_selected__86477,"branch_bound"))) {
return F_dv_imprint_branch_bound(v_goal,v_variables,v_laws,v_limits,v_prices,v_analysis__86790);
} else {
}
return F_dv_imprint_enumerate(v_goal,v_variables,v_laws,v_limits,v_prices,v_analysis__86790);
}
static object F_dv_imprint_solve(object v_capability,object v_goal,object v_variables,object v_laws,object v_limits,object v_prices) {
return F_dv_imprint_solve_with(v_capability,v_goal,v_variables,v_laws,v_limits,v_prices,"auto");
}
static object F_dv_balance_same_numbers(object v_left,object v_right) {
if (R.Bool(R.not(R.eq(R.count(v_left),R.count(v_right))))) {
return false;
} else {
}
object v_i__87498 = R.number("0");
while (R.Bool(R.lt(v_i__87498,R.count(v_left)))) {
if (R.Bool(R.not(R.eq(R.exact(R.at(v_left,v_i__87498)),R.exact(R.at(v_right,v_i__87498)))))) {
return false;
} else {
}
v_i__87498 = R.add(v_i__87498,R.number("1"));
}
return true;
}
static object F_dv_balance_same_witness(object v_left,object v_right) {
return (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_left,"index"),F_dv_get(v_right,"index"))) ? (object)(R.eq(F_dv_get(v_left,"feasible"),F_dv_get(v_right,"feasible"))) : (object)(false))) ? (object)(F_dv_same(F_dv_get(v_left,"choices"),F_dv_get(v_right,"choices"))) : (object)(false))) ? (object)(F_dv_balance_same_numbers(F_dv_get(v_left,"values"),F_dv_get(v_right,"values"))) : (object)(false))) ? (object)(F_dv_balance_same_numbers(F_dv_get(v_left,"charges"),F_dv_get(v_right,"charges"))) : (object)(false))) ? (object)(R.eq(R.exact(F_dv_get(v_left,"degree")),F_dv_get(v_right,"degree"))) : (object)(false))) ? (object)(R.eq(R.exact(F_dv_get(v_left,"cost")),F_dv_get(v_right,"cost"))) : (object)(false));
}
static object F_dv_balance_interval_fixed(object v_model,object v_start,object v_depth) {
object v_choices__88140 = F_dv_balance_interval_choices(v_model,v_start);
object v_fixed__88200 = F_dv_balance_zeroes(F_dv_get(v_model,"field_count"));
object v_i__88253 = v_depth;
while (R.Bool(R.lt(v_i__88253,R.count(F_dv_get(v_model,"domains"))))) {
object v_terms__88314 = R.at(R.at(F_dv_get(v_model,"domains"),v_i__88253),R.at(v_choices__88140,v_i__88253));
object v_j__88363 = R.number("0");
while (R.Bool(R.lt(v_j__88363,F_dv_get(v_model,"field_count")))) {
_ = R.replace(v_fixed__88200,v_j__88363,R.add(R.at(v_fixed__88200,v_j__88363),R.at(v_terms__88314,v_j__88363)));
v_j__88363 = R.add(v_j__88363,R.number("1"));
}
v_i__88253 = R.add(v_i__88253,R.number("1"));
}
return v_fixed__88200;
}
static object F_dv_balance_check_coverage(object v_result,object v_goal,object v_variables,object v_laws,object v_limits,object v_prices,object v_verified) {
object v_coverage__88613 = F_dv_find(v_result,"coverage",false);
if (R.Bool(R.not(R.is_list(v_coverage__88613)))) {
return false;
} else {
}
if (R.Bool(R.eq(F_dv_get(v_result,"visited"),R.number("0")))) {
return R.eq(R.count(v_coverage__88613),R.number("0"));
} else {
}
object v_model__88788 = F_dv_balance_interval_model(v_goal,v_variables,v_laws,v_limits,v_prices);
object v_cursor__88869 = R.number("0");
object v_i__88888 = R.number("0");
while (R.Bool(R.lt(v_i__88888,R.count(v_coverage__88613)))) {
object v_block__88940 = R.at(v_coverage__88613,v_i__88888);
if (R.Bool(R.not(F_dv_record_like(v_block__88940)))) {
return false;
} else {
}
object v_start__89035 = F_dv_find(v_block__88940,"start",R.sub(R.number("0"),R.number("1")));
object v_depth__89083 = F_dv_find(v_block__88940,"depth",R.sub(R.number("0"),R.number("1")));
object v_count__89131 = F_dv_find(v_block__88940,"count",R.sub(R.number("0"),R.number("1")));
object v_kind__89179 = F_dv_find(v_block__88940,"kind","");
if (R.Bool((R.Bool((R.Bool(R.not(R.is_numeric_value(v_start__89035))) ? (object)(true) : (object)(R.not(R.is_numeric_value(v_depth__89083))))) ? (object)(true) : (object)(R.not(R.is_numeric_value(v_count__89131)))))) {
return false;
} else {
}
if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(v_start__89035,v_cursor__88869))) ? (object)(true) : (object)(R.not(R.eq(R.floor(v_start__89035),v_start__89035))))) ? (object)(true) : (object)(R.lt(v_depth__89083,R.number("0"))))) ? (object)(true) : (object)(R.lt(R.count(v_variables),v_depth__89083)))) ? (object)(true) : (object)(R.not(R.eq(R.floor(v_depth__89083),v_depth__89083)))))) {
return false;
} else {
}
object v_width__89514 = R.at(F_dv_get(v_model__88788,"widths"),v_depth__89083);
if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.lt(R.number("0"),v_count__89131))) ? (object)(true) : (object)(R.lt(v_width__89514,v_count__89131)))) ? (object)(true) : (object)(R.not(R.eq(R.floor(v_count__89131),v_count__89131))))) ? (object)(true) : (object)(R.lt(F_dv_get(v_result,"visited"),R.add(v_start__89035,v_count__89131)))))) {
return false;
} else {
}
if (R.Bool(R.not(R.eq(R.mul(R.floor(R.div(v_start__89035,v_width__89514)),v_width__89514),v_start__89035)))) {
return false;
} else {
}
if (R.Bool((R.Bool(R.lt(v_count__89131,v_width__89514)) ? (object)(R.not(R.eq(R.add(v_start__89035,v_count__89131),F_dv_get(v_result,"visited")))) : (object)(false)))) {
return false;
} else {
}
if (R.Bool(R.eq(v_kind__89179,"candidate"))) {
if (R.Bool((R.Bool(R.not(R.eq(v_depth__89083,R.number("0")))) ? (object)(true) : (object)(R.not(R.eq(v_count__89131,R.number("1"))))))) {
return false;
} else {
}
object v_candidate__89952 = v_verified;
if (R.Bool(R.eq(v_verified,false))) {
v_candidate__89952 = F_dv_balance_candidate(v_start__89035,v_goal,v_variables,v_laws,v_limits,v_prices);
} else {
if (R.Bool(R.not(R.eq(v_start__89035,F_dv_get(v_verified,"index"))))) {
v_candidate__89952 = F_dv_balance_candidate(v_start__89035,v_goal,v_variables,v_laws,v_limits,v_prices);
} else {
}
}
if (R.Bool(F_dv_get(v_candidate__89952,"feasible"))) {
if (R.Bool(R.eq(v_verified,false))) {
return false;
} else {
}
if (R.Bool(R.lt(F_dv_get(v_candidate__89952,"cost"),F_dv_get(v_verified,"cost")))) {
return false;
} else {
}
} else {
}
} else {
if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(v_kind__89179,"goal-bound"))) ? (object)(R.not(R.eq(v_kind__89179,"law-bound"))) : (object)(false))) ? (object)(R.not(R.eq(v_kind__89179,"resource-bound"))) : (object)(false))) ? (object)(R.not(R.eq(v_kind__89179,"cost-bound"))) : (object)(false)))) {
return false;
} else {
}
object v_fixed__90627 = F_dv_balance_interval_fixed(v_model__88788,v_start__89035,v_depth__89083);
if (R.Bool(R.not(R.eq(F_dv_balance_interval_kind(v_model__88788,v_depth__89083,v_fixed__90627,v_verified),v_kind__89179)))) {
return false;
} else {
}
}
v_cursor__88869 = R.add(v_start__89035,v_count__89131);
v_i__88888 = R.add(v_i__88888,R.number("1"));
}
return R.eq(v_cursor__88869,F_dv_get(v_result,"visited"));
}
static object F_dv_imprint_check(object v_result,object v_goal,object v_variables,object v_laws,object v_limits,object v_prices) {
if (R.Bool(R.not(F_dv_record_like(v_result)))) {
return false;
} else {
}
object v_total__91187 = F_dv_balance_extent(v_variables);
if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_result,"total"),v_total__91187))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_result,"revision"),F_dv_get(v_limits,"revision")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_result,"subject"),F_dv_get(v_limits,"subject"))))))) {
return false;
} else {
}
if (R.Bool(R.not(R.is_numeric_value(F_dv_get(v_result,"visited"))))) {
return false;
} else {
}
if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.lt(F_dv_get(v_result,"visited"),R.number("0"))) ? (object)(true) : (object)(R.lt(v_total__91187,F_dv_get(v_result,"visited"))))) ? (object)(true) : (object)(R.lt(F_dv_get(v_limits,"search_budget"),F_dv_get(v_result,"visited"))))) ? (object)(true) : (object)(R.lt(F_dv_get(v_limits,"work_remaining"),R.mul(F_dv_get(v_result,"visited"),R.number("2")))))) ? (object)(true) : (object)(R.not(R.eq(R.floor(F_dv_get(v_result,"visited")),F_dv_get(v_result,"visited"))))))) {
return false;
} else {
}
object v_best__91633 = F_dv_get(v_result,"best");
object v_verified__91660 = false;
if (R.Bool(R.not(R.eq(v_best__91633,false)))) {
if (R.Bool(R.not(F_dv_record_like(v_best__91633)))) {
return false;
} else {
}
if (R.Bool(R.not(R.is_numeric_value(F_dv_find(v_best__91633,"index",false))))) {
return false;
} else {
}
if (R.Bool((R.Bool((R.Bool(R.not(R.is_list(F_dv_find(v_best__91633,"choices",false)))) ? (object)(true) : (object)(R.not(R.is_list(F_dv_find(v_best__91633,"values",false)))))) ? (object)(true) : (object)(R.not(R.is_list(F_dv_find(v_best__91633,"charges",false))))))) {
return false;
} else {
}
if (R.Bool((R.Bool(R.not(F_dv_has_key(v_best__91633,"degree"))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_best__91633,"cost")))))) {
return false;
} else {
}
if (R.Bool((R.Bool((R.Bool(R.lt(F_dv_get(v_best__91633,"index"),R.number("0"))) ? (object)(true) : (object)(R.not(R.lt(F_dv_get(v_best__91633,"index"),F_dv_get(v_result,"visited")))))) ? (object)(true) : (object)(R.not(R.eq(R.floor(F_dv_get(v_best__91633,"index")),F_dv_get(v_best__91633,"index"))))))) {
return false;
} else {
}
v_verified__91660 = F_dv_balance_candidate(F_dv_get(v_best__91633,"index"),v_goal,v_variables,v_laws,v_limits,v_prices);
if (R.Bool((R.Bool(R.not(F_dv_get(v_verified__91660,"feasible"))) ? (object)(true) : (object)(R.not(F_dv_balance_same_witness(v_best__91633,v_verified__91660)))))) {
return false;
} else {
}
} else {
}
object v_proof_kind__92466 = F_dv_find(v_result,"proof_kind","enumerated-prefix-v1");
if (R.Bool(R.eq(v_proof_kind__92466,"linear-interval-prefix-v1"))) {
if (R.Bool(R.not(F_dv_balance_check_coverage(v_result,v_goal,v_variables,v_laws,v_limits,v_prices,v_verified__91660)))) {
return false;
} else {
}
} else {
if (R.Bool(R.not(R.eq(v_proof_kind__92466,"enumerated-prefix-v1")))) {
return false;
} else {
}
object v_i__92889 = R.number("0");
while (R.Bool(R.lt(v_i__92889,F_dv_get(v_result,"visited")))) {
object v_alternative__92945 = v_verified__91660;
if (R.Bool(R.eq(v_best__91633,false))) {
v_alternative__92945 = F_dv_balance_candidate(v_i__92889,v_goal,v_variables,v_laws,v_limits,v_prices);
} else {
if (R.Bool(R.not(R.eq(v_i__92889,F_dv_get(v_best__91633,"index"))))) {
v_alternative__92945 = F_dv_balance_candidate(v_i__92889,v_goal,v_variables,v_laws,v_limits,v_prices);
} else {
}
}
if (R.Bool(F_dv_get(v_alternative__92945,"feasible"))) {
if (R.Bool(R.eq(v_best__91633,false))) {
return false;
} else {
}
if (R.Bool(R.lt(F_dv_get(v_alternative__92945,"cost"),F_dv_get(v_best__91633,"cost")))) {
return false;
} else {
}
} else {
}
v_i__92889 = R.add(v_i__92889,R.number("1"));
}
}
if (R.Bool(R.eq(F_dv_get(v_result,"visited"),v_total__91187))) {
if (R.Bool(R.eq(v_best__91633,false))) {
return R.eq(F_dv_get(v_result,"status"),"Infeasible");
} else {
}
return R.eq(F_dv_get(v_result,"status"),"Optimal");
} else {
}
if (R.Bool(R.eq(v_best__91633,false))) {
return R.eq(F_dv_get(v_result,"status"),"Unresolved");
} else {
}
return R.eq(F_dv_get(v_result,"status"),"Feasible");
}
static object F_dv_imprint_commit(object v_state,object v_result,object v_goal,object v_variables,object v_laws,object v_limits,object v_prices,object v_source) {
_ = F_dv_require((R.Bool(R.eq(F_dv_get(v_state,"subject"),F_dv_get(v_limits,"subject"))) ? (object)(R.eq(F_dv_get(v_state,"revision"),F_dv_get(v_limits,"revision"))) : (object)(false)),"Imprint source state changed");
_ = F_dv_require(F_dv_same(F_dv_get(v_state,"balances"),F_dv_get(v_limits,"balances")),"Imprint resource snapshot changed");
_ = F_dv_require(F_dv_imprint_check(v_result,v_goal,v_variables,v_laws,v_limits,v_prices),"Invalid balance certificate");
_ = F_dv_require(R.eq(F_dv_get(v_state,"work_remaining"),F_dv_get(v_limits,"work_remaining")),"Imprint work snapshot changed");
object v_work_spent__94216 = R.mul(F_dv_get(v_result,"visited"),R.number("2"));
object v_paid__94256 = F_dv_with_many(v_state,new System.Collections.Generic.List<object>(2) {new System.Collections.Generic.List<object>(2) {"work_remaining",R.sub(F_dv_get(v_state,"work_remaining"),v_work_spent__94216)},new System.Collections.Generic.List<object>(2) {"revision",R.add(F_dv_get(v_state,"revision"),R.number("1"))}});
if (R.Bool(R.not(R.eq(F_dv_get(v_result,"status"),"Optimal")))) {
return new System.Collections.Generic.List<object>(4) {F_dv_pair("applied",false),F_dv_pair("state",v_paid__94256),F_dv_pair("result",v_result),F_dv_pair("work_spent",v_work_spent__94216)};
} else {
}
object v_best__94502 = F_dv_get(v_result,"best");
object v_balances__94529 = new System.Collections.Generic.List<object>(0) {};
object v_i__94551 = R.number("0");
while (R.Bool(R.lt(v_i__94551,R.count(F_dv_get(v_state,"balances"))))) {
object v_before__94609 = R.exact(R.at(F_dv_get(v_state,"balances"),v_i__94551));
object v_after__94658 = R.sub(v_before__94609,R.at(F_dv_get(v_best__94502,"charges"),v_i__94551));
_ = F_dv_require(R.eq(v_before__94609,R.add(v_after__94658,R.at(F_dv_get(v_best__94502,"charges"),v_i__94551))),"Resource conservation failed");
_ = R.push(v_balances__94529,v_after__94658);
v_i__94551 = R.add(v_i__94551,R.number("1"));
}
object v_degree__94836 = F_dv_balance_dot(F_dv_get(v_goal,"coefficients"),F_dv_get(v_best__94502,"values"));
object v_accepted__94900 = F_dv_acceptance_classify(v_degree__94836,F_dv_get(v_state,"levels"));
object v_previous__94964 = F_dv_acceptance_classify(F_dv_balance_dot(F_dv_get(v_goal,"coefficients"),F_dv_get(v_state,"values")),F_dv_get(v_state,"levels"));
object v_transition__95069 = F_dv_acceptance_transition(v_previous__94964,v_accepted__94900,v_source);
object v_receipt__95143 = new System.Collections.Generic.List<object>(7) {F_dv_pair("kind","Imprint"),F_dv_pair("source",v_source),F_dv_pair("before",F_dv_get(v_state,"values")),F_dv_pair("after",F_dv_get(v_best__94502,"values")),F_dv_pair("charges",F_dv_get(v_best__94502,"charges")),F_dv_pair("proof",v_result),F_dv_pair("transition",v_transition__95069)};
object v_next__95299 = F_dv_with_many(v_paid__94256,new System.Collections.Generic.List<object>(5) {new System.Collections.Generic.List<object>(2) {"values",F_dv_get(v_best__94502,"values")},new System.Collections.Generic.List<object>(2) {"balances",v_balances__94529},new System.Collections.Generic.List<object>(2) {"revision",R.add(F_dv_get(v_state,"revision"),R.number("1"))},new System.Collections.Generic.List<object>(2) {"acceptance",v_accepted__94900},new System.Collections.Generic.List<object>(2) {"imprint_receipt",v_receipt__95143}});
return new System.Collections.Generic.List<object>(4) {F_dv_pair("applied",true),F_dv_pair("state",v_next__95299),F_dv_pair("result",v_result),F_dv_pair("work_spent",v_work_spent__94216)};
}
static object F_dv_imprint_prepare(object v_mind,object v_refs,object v_ledger,object v_work_remaining,object v_levels,object v_search_budget,object v_capability) {
_ = F_dv_domain(v_mind,"mind");
object v_values__95758 = new System.Collections.Generic.List<object>(0) {};
object v_i__95778 = R.number("0");
while (R.Bool(R.lt(v_i__95778,R.count(v_refs)))) {
object v_cell__95826 = F_dv_read(v_mind,R.at(v_refs,v_i__95778));
_ = F_dv_require((R.Bool(F_dv_get(v_cell__95826,"known")) ? (object)(R.not(F_dv_get(v_cell__95826,"conflicted"))) : (object)(false)),"Imprint structural coordinate is not established");
_ = R.push(v_values__95758,R.exact(F_dv_get(v_cell__95826,"value")));
v_i__95778 = R.add(v_i__95778,R.number("1"));
}
return new System.Collections.Generic.List<object>(12) {F_dv_pair("subject",F_dv_get(v_mind,"subject")),F_dv_pair("revision",F_dv_get(v_mind,"revision")),F_dv_pair("mind_id",F_dv_get(v_mind,"id")),F_dv_pair("refs",v_refs),F_dv_pair("values",v_values__95758),F_dv_pair("balances",new System.Collections.Generic.List<object>(3) {F_dv_get(F_dv_get(v_ledger,"balances"),"time"),F_dv_get(F_dv_get(v_ledger,"balances"),"computation"),F_dv_get(F_dv_get(v_ledger,"balances"),"stamina")}),F_dv_pair("ledger_id",F_dv_get(v_ledger,"id")),F_dv_pair("ledger_revision",F_dv_get(v_ledger,"revision")),F_dv_pair("work_remaining",v_work_remaining),F_dv_pair("levels",v_levels),F_dv_pair("search_budget",v_search_budget),F_dv_pair("imprint_allowed",v_capability)};
}
static object F_dv_imprint_apply(object v_mind,object v_physical,object v_ledger,object v_state,object v_result,object v_goal,object v_variables,object v_laws,object v_prices,object v_event) {
_ = F_dv_domain(v_mind,"mind");
_ = F_dv_require(F_dv_get(v_state,"imprint_allowed"),"Imprint capability is required");
_ = F_dv_require((R.Bool((R.Bool(R.eq(F_dv_get(v_mind,"id"),F_dv_get(v_state,"mind_id"))) ? (object)(R.eq(F_dv_get(v_mind,"subject"),F_dv_get(v_state,"subject"))) : (object)(false))) ? (object)(R.eq(F_dv_get(v_mind,"revision"),F_dv_get(v_state,"revision"))) : (object)(false)),"Imprint target snapshot changed");
_ = F_dv_require((R.Bool(R.eq(F_dv_get(v_ledger,"id"),F_dv_get(v_state,"ledger_id"))) ? (object)(R.eq(F_dv_get(v_ledger,"revision"),F_dv_get(v_state,"ledger_revision"))) : (object)(false)),"Imprint ledger snapshot changed");
object v_current__96862 = F_dv_imprint_prepare(v_mind,F_dv_get(v_state,"refs"),v_ledger,F_dv_get(v_state,"work_remaining"),F_dv_get(v_state,"levels"),F_dv_get(v_state,"search_budget"),F_dv_get(v_state,"imprint_allowed"));
_ = F_dv_require(F_dv_balance_same_numbers(F_dv_get(v_current__96862,"values"),F_dv_get(v_state,"values")),"Imprint structural input changed");
object v_committed__97111 = F_dv_imprint_commit(v_current__96862,v_result,v_goal,v_variables,v_laws,v_state,v_prices,v_event);
object v_next_mind__97211 = v_mind;
object v_actual__97236 = F_dv_action_cost_vector(R.number("0"),R.number("0"),R.number("0"));
object v_outcome__97284 = "rejected";
if (R.Bool(F_dv_get(v_committed__97111,"applied"))) {
object v_charges__97343 = F_dv_get(F_dv_get(F_dv_get(v_committed__97111,"state"),"imprint_receipt"),"charges");
v_actual__97236 = F_dv_action_cost_vector(R.at(v_charges__97343,R.number("0")),R.at(v_charges__97343,R.number("1")),R.at(v_charges__97343,R.number("2")));
v_outcome__97284 = "applied";
object v_origin__97508 = new System.Collections.Generic.List<object>(4) {F_dv_pair("kind","Imprint"),F_dv_pair("event",v_event),F_dv_pair("subject",F_dv_get(v_mind,"subject")),F_dv_pair("proof",v_result)};
object v_i__97599 = R.number("0");
while (R.Bool(R.lt(v_i__97599,R.count(F_dv_get(v_state,"refs"))))) {
v_next_mind__97211 = F_dv_store(v_next_mind__97211,R.at(F_dv_get(v_state,"refs"),v_i__97599),F_dv_cell(R.at(F_dv_get(F_dv_get(v_committed__97111,"state"),"values"),v_i__97599),R.number("1"),R.add(F_dv_find(v_physical,"time",R.number("0")),F_dv_get(v_actual__97236,"time")),new System.Collections.Generic.List<object>(1) {v_origin__97508}));
v_i__97599 = R.add(v_i__97599,R.number("1"));
}
v_next_mind__97211 = F_dv_with_many(v_next_mind__97211,new System.Collections.Generic.List<object>(3) {new System.Collections.Generic.List<object>(2) {"revision",F_dv_get(F_dv_get(v_committed__97111,"state"),"revision")},new System.Collections.Generic.List<object>(2) {"history",F_dv_append(F_dv_get(v_mind,"history"),F_dv_get(F_dv_get(v_committed__97111,"state"),"imprint_receipt"))},new System.Collections.Generic.List<object>(2) {"imprint_acceptance",F_dv_get(F_dv_get(v_committed__97111,"state"),"acceptance")}});
object v_observed__98040 = F_dv_imprint_prepare(v_next_mind__97211,F_dv_get(v_state,"refs"),v_ledger,F_dv_get(F_dv_get(v_committed__97111,"state"),"work_remaining"),F_dv_get(v_state,"levels"),F_dv_get(v_state,"search_budget"),F_dv_get(v_state,"imprint_allowed"));
_ = F_dv_require(R.not(R.lt(F_dv_balance_dot(F_dv_get(v_goal,"coefficients"),F_dv_get(v_observed__98040,"values")),R.exact(F_dv_get(v_goal,"minimum")))),"Stored cognition did not achieve imprint goal");
} else {
}
object v_bounds__98345 = F_dv_action_cost_bounds(v_actual__97236,v_actual__97236);
object v_quote__98400 = F_dv_action_cost_quote(v_ledger,v_physical,"cognitive-imprint",v_event,v_bounds__98345);
object v_settlement__98491 = F_dv_action_cost_finish(v_ledger,v_physical,v_quote__98400,v_actual__97236,v_outcome__97284);
return new System.Collections.Generic.List<object>(8) {F_dv_pair("applied",F_dv_get(v_committed__97111,"applied")),F_dv_pair("mind",v_next_mind__97211),F_dv_pair("physical",F_dv_get(F_dv_get(v_settlement__98491,"execution"),"world")),F_dv_pair("ledger",F_dv_get(v_settlement__98491,"ledger")),F_dv_pair("work_remaining",F_dv_get(F_dv_get(v_committed__97111,"state"),"work_remaining")),F_dv_pair("work_spent",F_dv_get(v_committed__97111,"work_spent")),F_dv_pair("proof",v_result),F_dv_pair("cost_receipt",F_dv_get(F_dv_get(F_dv_get(v_settlement__98491,"ledger"),"receipts"),v_event))};
}
static object F_dv_proof_step(object v_id,object v_kind,object v_conclusion,object v_parents,object v_law) {
return F_dv_with(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",v_id),F_dv_pair("kind",v_kind),F_dv_pair("conclusion",v_conclusion),F_dv_pair("parents",v_parents)},"law",v_law);
}
static object F_dv_cost_policy(object v_step,object v_evidence,object v_assumption,object v_conflict,object v_reference,object v_maximum,object v_allow_assumptions,object v_budget) {
_ = F_dv_require(F_dv_and(F_dv_and(R.not(R.lt(v_step,R.number("0"))),R.not(R.lt(v_evidence,R.number("0")))),F_dv_and(F_dv_and(R.lt(R.number("0"),v_assumption),R.not(R.lt(v_conflict,R.number("0")))),F_dv_and(R.lt(R.number("0"),v_reference),R.not(R.lt(v_maximum,R.number("0")))))),"Invalid reasoning costs");
_ = F_dv_require(R.lt(R.number("0"),v_budget),"Invalid proof budget");
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("step",v_step),F_dv_pair("evidence",v_evidence),F_dv_pair("assumption",v_assumption),F_dv_pair("conflict",v_conflict)},new System.Collections.Generic.List<object>(4) {F_dv_pair("reference",v_reference),F_dv_pair("maximum",v_maximum),F_dv_pair("allow_assumptions",v_allow_assumptions),F_dv_pair("budget",v_budget)});
}
static object F_dv_rule_concludes(object v_law,object v_conclusion) {
object v_effects__99710 = F_dv_get(v_law,"effects");
object v_i__99740 = R.number("0");
object v_found__99754 = false;
while (R.Bool(R.lt(v_i__99740,R.count(v_effects__99710)))) {
object v_effect__99815 = R.at(v_effects__99710,v_i__99740);
if (R.Bool(R.eq(F_dv_get(v_effect__99815,"op"),"assign"))) {
if (R.Bool(F_dv_same(v_conclusion,F_dv_fact(F_dv_get(v_effect__99815,"ref"),F_dv_get(v_effect__99815,"value"))))) {
v_found__99754 = true;
} else {
}
} else {
}
v_i__99740 = R.add(v_i__99740,R.number("1"));
}
return v_found__99754;
}
static object F_dv_match_premises(object v_premise,object v_supplied) {
if (R.Bool(F_dv_same(v_premise,F_dv_true()))) {
return R.eq(R.count(v_supplied),R.number("0"));
} else {
if (R.Bool(R.eq(F_dv_get(v_premise,"op"),"all"))) {
object v_required__100187 = F_dv_get(v_premise,"items");
object v_i__100228 = R.number("0");
object v_valid__100250 = R.eq(R.count(v_supplied),R.count(v_required__100187));
while (R.Bool(R.lt(v_i__100228,R.count(v_required__100187)))) {
v_valid__100250 = F_dv_and(v_valid__100250,F_dv_has(v_supplied,R.at(v_required__100187,v_i__100228)));
v_i__100228 = R.add(v_i__100228,R.number("1"));
}
return v_valid__100250;
} else {
return F_dv_same(v_supplied,F_dv_single(v_premise));
}
}
}
static object F_dv_check_step(object v_world,object v_step,object v_checked,object v_policy) {
object v_kind__100595 = F_dv_get(v_step,"kind");
object v_conclusion__100620 = F_dv_get(v_step,"conclusion");
object v_parents__100657 = F_dv_get(v_step,"parents");
object v_supplied__100688 = new System.Collections.Generic.List<object>(0) {};
object v_confidence__100710 = R.number("1");
object v_i__100733 = R.number("0");
object v_hypothetical__100747 = false;
while (R.Bool(R.lt(v_i__100733,R.count(v_parents__100657)))) {
object v_parent__100815 = F_dv_get(v_checked,R.at(v_parents__100657,v_i__100733));
_ = R.push(v_supplied__100688,F_dv_get(v_parent__100815,"conclusion"));
v_confidence__100710 = F_dv_min(v_confidence__100710,F_dv_get(v_parent__100815,"confidence"));
v_hypothetical__100747 = F_dv_or(v_hypothetical__100747,F_dv_get(v_parent__100815,"hypothetical"));
v_i__100733 = R.add(v_i__100733,R.number("1"));
}
object v_assumptions__101048 = R.number("0");
object v_conflicts__101072 = R.number("0");
object v_local_uncertainty__101094 = R.number("0");
if (R.Bool(R.eq(v_kind__100595,"known"))) {
_ = F_dv_require(R.eq(R.count(v_parents__100657),R.number("0")),"Known premise cannot hide parents");
_ = F_dv_require(R.eq(F_dv_truth(v_world,v_conclusion__100620),"true"),"Conclusion is not available in this view");
v_confidence__100710 = F_dv_premise_confidence(v_world,v_conclusion__100620);
v_local_uncertainty__101094 = R.sub(R.number("1"),v_confidence__100710);
} else {
if (R.Bool(R.eq(v_kind__100595,"assume"))) {
_ = F_dv_require(R.eq(R.count(v_parents__100657),R.number("0")),"Assumption cannot hide parents");
_ = F_dv_require(F_dv_and(F_dv_get(v_policy,"allow_assumptions"),R.not(R.eq(F_dv_get(v_world,"domain"),"physical"))),"Assumption not permitted");
v_hypothetical__100747 = true;
v_assumptions__101048 = R.number("1");
v_confidence__100710 = R.number("0");
if (R.Bool(R.eq(F_dv_truth(v_world,v_conclusion__100620),"false"))) {
v_conflicts__101072 = R.number("1");
} else {
}
} else {
if (R.Bool(R.eq(v_kind__100595,"axiom"))) {
object v_adoption__101922 = F_dv_get(F_dv_get(v_world,"adoptions"),F_dv_get(v_step,"law"));
object v_law__101987 = F_dv_get(v_adoption__101922,"law");
_ = F_dv_require(F_dv_get(v_adoption__101922,"active"),"Proof uses a suspended axiom");
_ = F_dv_require(R.eq(F_dv_get(v_adoption__101922,"mode"),"infer"),"Operational effects do not prove that an event occurred");
_ = F_dv_require(F_dv_rule_concludes(v_law__101987,v_conclusion__100620),"Axiom does not conclude this proposition");
_ = F_dv_require(F_dv_match_premises(F_dv_get(v_law__101987,"premise"),v_supplied__100688),"Axiom premises are not established");
v_confidence__100710 = F_dv_min(v_confidence__100710,F_dv_get(v_adoption__101922,"confidence"));
v_local_uncertainty__101094 = R.sub(R.number("1"),F_dv_get(v_adoption__101922,"confidence"));
} else {
_ = R.fail("Unsupported proof step");
}
}
}
return F_dv_with_many(F_dv_clone(v_step),new System.Collections.Generic.List<object>(5) {F_dv_pair("confidence",v_confidence__100710),F_dv_pair("hypothetical",v_hypothetical__100747),F_dv_pair("assumptions",v_assumptions__101048),F_dv_pair("conflicts",v_conflicts__101072),F_dv_pair("local_uncertainty",v_local_uncertainty__101094)});
}
static object F_dv_judge_conclusion(object v_world,object v_conclusion,object v_proof,object v_policy) {
_ = F_dv_require(R.lt(R.number("0"),R.count(v_proof)),"Empty derivation");
_ = F_dv_require(R.not(R.lt(F_dv_get(v_policy,"budget"),R.count(v_proof))),"Derivation budget exhausted");
object v_checked__103011 = new System.Collections.Generic.List<object>(0) {};
object v_i__103032 = R.number("0");
while (R.Bool(R.lt(v_i__103032,R.count(v_proof)))) {
object v_step__103083 = R.at(v_proof,v_i__103032);
object v_id__103111 = F_dv_get(v_step__103083,"id");
_ = F_dv_require(R.not(F_dv_has_key(v_checked__103011,v_id__103111)),"Repeated proof node identity");
v_checked__103011 = F_dv_with(v_checked__103011,v_id__103111,F_dv_check_step(v_world,v_step__103083,v_checked__103011,v_policy));
v_i__103032 = R.add(v_i__103032,R.number("1"));
}
object v_root__103318 = F_dv_get(v_checked__103011,F_dv_get(R.at(v_proof,R.sub(R.count(v_proof),R.number("1"))),"id"));
object v_queue__103382 = F_dv_single(F_dv_get(v_root__103318,"id"));
object v_visited__103417 = new System.Collections.Generic.List<object>(0) {};
object v_cursor__103438 = R.number("0");
object v_steps__103457 = R.number("0");
object v_uncertainty__103475 = R.number("0");
object v_assumptions__103499 = R.number("0");
object v_conflicts__103523 = R.number("0");
_ = F_dv_require(F_dv_same(v_conclusion,F_dv_get(v_root__103318,"conclusion")),"Derivation concludes another proposition");
while (R.Bool(R.lt(v_cursor__103438,R.count(v_queue__103382)))) {
object v_id__103684 = R.at(v_queue__103382,v_cursor__103438);
if (R.Bool(F_dv_has(v_visited__103417,v_id__103684))) {
_ = false;
} else {
object v_step__103783 = F_dv_get(v_checked__103011,v_id__103684);
object v_parents__103826 = F_dv_get(v_step__103783,"parents");
object v_p__103865 = R.number("0");
_ = R.push(v_visited__103417,v_id__103684);
v_steps__103457 = R.add(v_steps__103457,R.number("1"));
v_uncertainty__103475 = R.add(v_uncertainty__103475,F_dv_get(v_step__103783,"local_uncertainty"));
v_assumptions__103499 = R.add(v_assumptions__103499,F_dv_get(v_step__103783,"assumptions"));
v_conflicts__103523 = R.add(v_conflicts__103523,F_dv_get(v_step__103783,"conflicts"));
while (R.Bool(R.lt(v_p__103865,R.count(v_parents__103826)))) {
_ = R.push(v_queue__103382,R.at(v_parents__103826,v_p__103865));
v_p__103865 = R.add(v_p__103865,R.number("1"));
}
}
v_cursor__103438 = R.add(v_cursor__103438,R.number("1"));
}
object v_cost__104265 = R.add(R.add(R.mul(v_steps__103457,F_dv_get(v_policy,"step")),R.mul(v_uncertainty__103475,F_dv_get(v_policy,"evidence"))),R.add(R.mul(v_assumptions__103499,F_dv_get(v_policy,"assumption")),R.mul(v_conflicts__103523,F_dv_get(v_policy,"conflict"))));
object v_components__104410 = new System.Collections.Generic.List<object>(4) {F_dv_pair("steps",v_steps__103457),F_dv_pair("evidence_uncertainty",v_uncertainty__103475),F_dv_pair("assumptions",v_assumptions__103499),F_dv_pair("conflicts",v_conflicts__103523)};
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("valid",true),F_dv_pair("cost",v_cost__104265),F_dv_pair("components",v_components__104410),F_dv_pair("reasonable",R.not(R.lt(F_dv_get(v_policy,"maximum"),v_cost__104265)))},new System.Collections.Generic.List<object>(5) {F_dv_pair("reasonableness",R.div(R.number("1"),R.add(R.number("1"),R.div(v_cost__104265,F_dv_get(v_policy,"reference"))))),F_dv_pair("confidence",F_dv_get(v_root__103318,"confidence")),F_dv_pair("hypothetical",F_dv_or(F_dv_get(v_root__103318,"hypothetical"),R.eq(F_dv_get(v_world,"domain"),"prediction"))),F_dv_pair("scope",new System.Collections.Generic.List<object>(3) {F_dv_pair("world",F_dv_get(v_world,"id")),F_dv_pair("subject",F_dv_get(v_world,"subject")),F_dv_pair("revision",F_dv_get(v_world,"revision"))}),F_dv_pair("basis",v_visited__103417)});
}
static object F_dv_checkpoint(object v_world,object v_time) {
return new System.Collections.Generic.List<object>(3) {F_dv_pair("time",v_time),F_dv_pair("revision",F_dv_get(v_world,"revision")),F_dv_pair("world",F_dv_clone(v_world))};
}
static object F_dv_session(object v_worlds,object v_time,object v_time_unit) {
object v_catalog__105205 = new System.Collections.Generic.List<object>(0) {};
object v_history__105226 = new System.Collections.Generic.List<object>(0) {};
object v_subjects__105247 = new System.Collections.Generic.List<object>(0) {};
object v_i__105269 = R.number("0");
_ = F_dv_require(R.not(R.lt(v_time,R.number("0"))),"Negative session time");
while (R.Bool(R.lt(v_i__105269,R.count(v_worlds)))) {
object v_world__105379 = R.at(v_worlds,v_i__105269);
object v_id__105409 = F_dv_get(v_world__105379,"id");
object v_domain__105435 = F_dv_get(v_world__105379,"domain");
_ = F_dv_require(F_dv_or(R.eq(v_domain__105435,"physical"),R.eq(v_domain__105435,"mind")),"Predictions cannot be committed as worlds");
_ = F_dv_require(R.not(F_dv_has_key(v_catalog__105205,v_id__105409)),"Duplicate world identity");
if (R.Bool(R.eq(v_domain__105435,"mind"))) {
_ = F_dv_require(R.not(F_dv_has(v_subjects__105247,F_dv_get(v_world__105379,"subject"))),"Duplicate subject world");
_ = R.push(v_subjects__105247,F_dv_get(v_world__105379,"subject"));
} else {
}
v_catalog__105205 = F_dv_with(v_catalog__105205,v_id__105409,v_world__105379);
_ = R.push(v_history__105226,F_dv_checkpoint(v_world__105379,v_time));
v_i__105269 = R.add(v_i__105269,R.number("1"));
}
return new System.Collections.Generic.List<object>(4) {F_dv_pair("worlds",v_catalog__105205),F_dv_pair("history",v_history__105226),F_dv_pair("time",v_time),F_dv_pair("time_unit",v_time_unit)};
}
static object F_dv_session_commit(object v_session,object v_expected_revision,object v_world,object v_time) {
object v_catalog__106095 = F_dv_get(v_session,"worlds");
object v_id__106128 = F_dv_get(v_world,"id");
object v_old__106150 = F_dv_get(v_catalog__106095,v_id__106128);
_ = F_dv_require(R.eq(F_dv_get(v_old__106150,"revision"),v_expected_revision),"Stale session commit");
_ = F_dv_require(R.lt(v_expected_revision,F_dv_get(v_world,"revision")),"Commit needs a newer world revision");
_ = F_dv_require(F_dv_and(R.eq(F_dv_get(v_old__106150,"domain"),F_dv_get(v_world,"domain")),R.eq(F_dv_get(v_old__106150,"subject"),F_dv_get(v_world,"subject"))),"World identity changed");
_ = F_dv_require(R.not(R.lt(v_time,F_dv_get(v_session,"time"))),"Session time cannot move backwards");
return F_dv_with_many(v_session,new System.Collections.Generic.List<object>(3) {F_dv_pair("worlds",F_dv_with(v_catalog__106095,v_id__106128,v_world)),F_dv_pair("history",F_dv_append(F_dv_get(v_session,"history"),F_dv_checkpoint(v_world,v_time))),F_dv_pair("time",v_time)});
}
static object F_dv_session_advance(object v_session,object v_time) {
_ = F_dv_require(R.not(R.lt(v_time,F_dv_get(v_session,"time"))),"Session time cannot move backwards");
return F_dv_with(v_session,"time",v_time);
}
static object F_dv_active_worldview(object v_world) {
object v_adoptions__106895 = F_dv_get(v_world,"adoptions");
object v_result__106931 = new System.Collections.Generic.List<object>(0) {};
object v_i__106951 = R.number("0");
object v_policies__106965 = F_dv_find(v_world,"policies",new System.Collections.Generic.List<object>(0) {});
object v_accepted_policies__107015 = new System.Collections.Generic.List<object>(0) {};
while (R.Bool(R.lt(v_i__106951,R.count(v_adoptions__106895)))) {
object v_entry__107087 = R.at(v_adoptions__106895,v_i__106951);
if (R.Bool(F_dv_get(R.at(v_entry__107087,R.number("1")),"active"))) {
_ = R.push(v_result__106931,F_dv_clone(v_entry__107087));
} else {
}
v_i__106951 = R.add(v_i__106951,R.number("1"));
}
v_i__106951 = R.number("0");
while (R.Bool(R.lt(v_i__106951,R.count(v_policies__106965)))) {
object v_entry__107259 = R.at(v_policies__106965,v_i__106951);
if (R.Bool(F_dv_get(R.at(v_entry__107259,R.number("1")),"active"))) {
_ = R.push(v_accepted_policies__107015,F_dv_clone(v_entry__107259));
} else {
}
v_i__106951 = R.add(v_i__106951,R.number("1"));
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("laws",v_result__106931),F_dv_pair("value_policies",v_accepted_policies__107015)};
}
static object F_dv_inspect_content(object v_world,object v_command,object v_object_id,object v_facet) {
if (R.Bool(R.eq(v_command,"worldview"))) {
return F_dv_active_worldview(v_world);
} else {
if (R.Bool(R.eq(v_command,"state"))) {
return F_dv_clone(F_dv_get(v_world,"objects"));
} else {
if (R.Bool(R.eq(v_command,"slice"))) {
object v_objects__107738 = F_dv_get(v_world,"objects");
object v_fields__107782 = new System.Collections.Generic.List<object>(0) {};
if (R.Bool(F_dv_has_key(v_objects__107738,v_object_id))) {
object v_facets__107869 = F_dv_get(F_dv_get(v_objects__107738,v_object_id),"facets");
object v_values__107936 = F_dv_find(v_facets__107869,v_facet,new System.Collections.Generic.List<object>(0) {});
object v_i__107996 = R.number("0");
while (R.Bool(R.lt(v_i__107996,R.count(v_values__107936)))) {
_ = R.push(v_fields__107782,R.at(R.at(v_values__107936,v_i__107996),R.number("0")));
v_i__107996 = R.add(v_i__107996,R.number("1"));
}
} else {
}
return F_dv_slice(v_world,v_object_id,v_facet,v_fields__107782);
} else {
return R.fail("Inspector command must be worldview, state or slice");
}
}
}
}
static object F_dv_inspect_at(object v_session,object v_subject,object v_time,object v_command,object v_object_id,object v_facet) {
_ = F_dv_require(R.not(R.lt(F_dv_get(v_session,"time"),v_time)),"Requested time has not been reached");
object v_history__108472 = F_dv_get(v_session,"history");
object v_i__108506 = R.number("0");
object v_found__108520 = false;
object v_chosen__108542 = false;
while (R.Bool(R.lt(v_i__108506,R.count(v_history__108472)))) {
object v_checkpoint__108604 = R.at(v_history__108472,v_i__108506);
object v_world__108640 = F_dv_get(v_checkpoint__108604,"world");
if (R.Bool(F_dv_and(R.eq(F_dv_get(v_world__108640,"domain"),"mind"),F_dv_and(R.eq(F_dv_get(v_world__108640,"subject"),v_subject),R.not(R.lt(v_time,F_dv_get(v_checkpoint__108604,"time"))))))) {
v_found__108520 = true;
v_chosen__108542 = v_checkpoint__108604;
} else {
}
v_i__108506 = R.add(v_i__108506,R.number("1"));
}
if (R.Bool(v_found__108520)) {
object v_world__108881 = F_dv_get(v_chosen__108542,"world");
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("status","snapshot"),F_dv_pair("subject",v_subject),F_dv_pair("requested_time",v_time),F_dv_pair("checkpoint_time",F_dv_get(v_chosen__108542,"time"))},new System.Collections.Generic.List<object>(3) {F_dv_pair("time_unit",F_dv_get(v_session,"time_unit")),F_dv_pair("revision",F_dv_get(v_world__108881,"revision")),F_dv_pair("content",F_dv_inspect_content(v_world__108881,v_command,v_object_id,v_facet))});
} else {
return new System.Collections.Generic.List<object>(3) {F_dv_pair("status","no-checkpoint"),F_dv_pair("subject",v_subject),F_dv_pair("requested_time",v_time)};
}
}
static object F_dv_inspector_main() {
object v_argc__109287 = R.arg_count();
_ = F_dv_require(F_dv_or(R.eq(v_argc__109287,R.number("4")),R.eq(v_argc__109287,R.number("6"))),"Usage: Inspector session.json subject time worldview|state|slice [object facet]");
object v_command__109448 = R.arg(R.number("3"));
object v_local_1__109476 = false;
if (R.Bool(R.eq(v_command__109448,"slice"))) {
v_local_1__109476 = R.eq(v_argc__109287,R.number("6"));
} else {
v_local_1__109476 = R.eq(v_argc__109287,R.number("4"));
}
_ = F_dv_require(v_local_1__109476,"Wrong inspector argument count");
object v_local_2__109657 = false;
if (R.Bool(R.eq(v_argc__109287,R.number("6")))) {
v_local_2__109657 = R.arg(R.number("4"));
} else {
v_local_2__109657 = "";
}
object v_local_3__109760 = false;
if (R.Bool(R.eq(v_argc__109287,R.number("6")))) {
v_local_3__109760 = R.arg(R.number("5"));
} else {
v_local_3__109760 = "";
}
return R.print(R.json_stringify(F_dv_inspect_at(R.json_parse(R.read(R.arg(R.number("0")))),R.arg(R.number("1")),R.number(R.arg(R.number("2"))),v_command__109448,v_local_2__109657,v_local_3__109760)));
}
static object F_dv_action_cost_vector(object v_time,object v_computation,object v_stamina) {
_ = F_dv_require(F_dv_and(R.not(R.lt(v_time,R.number("0"))),F_dv_and(R.not(R.lt(v_computation,R.number("0"))),R.not(R.lt(v_stamina,R.number("0"))))),"Negative action cost");
return new System.Collections.Generic.List<object>(3) {F_dv_pair("time",v_time),F_dv_pair("computation",v_computation),F_dv_pair("stamina",v_stamina)};
}
static object F_dv_action_cost_validate(object v_v) {
return F_dv_action_cost_vector(F_dv_get(v_v,"time"),F_dv_get(v_v,"computation"),F_dv_get(v_v,"stamina"));
}
static object F_dv_action_cost_le(object v_a,object v_b) {
return F_dv_and(R.not(R.lt(F_dv_get(v_b,"time"),F_dv_get(v_a,"time"))),F_dv_and(R.not(R.lt(F_dv_get(v_b,"computation"),F_dv_get(v_a,"computation"))),R.not(R.lt(F_dv_get(v_b,"stamina"),F_dv_get(v_a,"stamina")))));
}
static object F_dv_action_cost_add(object v_a,object v_b) {
return F_dv_action_cost_vector(R.add(F_dv_get(v_a,"time"),F_dv_get(v_b,"time")),R.add(F_dv_get(v_a,"computation"),F_dv_get(v_b,"computation")),R.add(F_dv_get(v_a,"stamina"),F_dv_get(v_b,"stamina")));
}
static object F_dv_action_cost_sub(object v_a,object v_b) {
return F_dv_action_cost_vector(R.sub(F_dv_get(v_a,"time"),F_dv_get(v_b,"time")),R.sub(F_dv_get(v_a,"computation"),F_dv_get(v_b,"computation")),R.sub(F_dv_get(v_a,"stamina"),F_dv_get(v_b,"stamina")));
}
static object F_dv_action_cost_bounds(object v_minimum,object v_maximum) {
_ = F_dv_action_cost_validate(v_minimum);
_ = F_dv_action_cost_validate(v_maximum);
_ = F_dv_require(F_dv_action_cost_le(v_minimum,v_maximum),"Action minimum exceeds maximum");
return new System.Collections.Generic.List<object>(2) {F_dv_pair("minimum",v_minimum),F_dv_pair("maximum",v_maximum)};
}
static object F_dv_action_cost_ledger(object v_id,object v_balances) {
_ = F_dv_action_cost_validate(v_balances);
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",v_id),F_dv_pair("revision",R.number("0")),F_dv_pair("balances",v_balances),F_dv_pair("reserved",F_dv_action_cost_vector(R.number("0"),R.number("0"),R.number("0")))},new System.Collections.Generic.List<object>(3) {F_dv_pair("receipts",new System.Collections.Generic.List<object>(0) {}),F_dv_pair("events",new System.Collections.Generic.List<object>(0) {}),F_dv_pair("spent",F_dv_action_cost_vector(R.number("0"),R.number("0"),R.number("0")))});
}
static object F_dv_action_cost_quote(object v_ledger,object v_world,object v_action,object v_event,object v_bounds) {
_ = F_dv_domain(v_world,"physical");
_ = F_dv_action_cost_bounds(F_dv_get(v_bounds,"minimum"),F_dv_get(v_bounds,"maximum"));
_ = F_dv_require(R.not(F_dv_has(F_dv_get(v_ledger,"events"),v_event)),"Repeated cost event");
_ = F_dv_require(R.not(F_dv_has(F_dv_get(v_world,"seen"),v_event)),"Repeated world event");
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("ledger",F_dv_get(v_ledger,"id")),F_dv_pair("revision",F_dv_get(v_ledger,"revision")),F_dv_pair("world",F_dv_get(v_world,"id")),F_dv_pair("world_revision",F_dv_get(v_world,"revision"))},new System.Collections.Generic.List<object>(3) {F_dv_pair("action",v_action),F_dv_pair("event",v_event),F_dv_pair("bounds",v_bounds)});
}
static object F_dv_action_cost_admit(object v_ledger,object v_world,object v_quote) {
object v_maximum__111988 = F_dv_get(F_dv_get(v_quote,"bounds"),"maximum");
object v_event__112027 = F_dv_get(v_quote,"event");
_ = F_dv_require(F_dv_and(R.eq(F_dv_get(v_ledger,"id"),F_dv_get(v_quote,"ledger")),R.eq(F_dv_get(v_ledger,"revision"),F_dv_get(v_quote,"revision"))),"Stale cost quote");
_ = F_dv_require(F_dv_and(R.eq(F_dv_get(v_world,"id"),F_dv_get(v_quote,"world")),R.eq(F_dv_get(v_world,"revision"),F_dv_get(v_quote,"world_revision"))),"Stale cost world");
_ = F_dv_action_cost_quote(v_ledger,v_world,F_dv_get(v_quote,"action"),v_event__112027,F_dv_get(v_quote,"bounds"));
_ = F_dv_require(F_dv_action_cost_le(v_maximum__111988,F_dv_get(v_ledger,"balances")),"Insufficient action resources");
object v_receipt__112444 = F_dv_with(v_quote,"state","reserved");
object v_next__112498 = F_dv_with(v_ledger,"revision",R.add(F_dv_get(v_ledger,"revision"),R.number("1")));
v_next__112498 = F_dv_with(v_next__112498,"balances",F_dv_action_cost_sub(F_dv_get(v_ledger,"balances"),v_maximum__111988));
v_next__112498 = F_dv_with(v_next__112498,"reserved",F_dv_action_cost_add(F_dv_get(v_ledger,"reserved"),v_maximum__111988));
v_next__112498 = F_dv_with(v_next__112498,"events",F_dv_append(F_dv_get(v_ledger,"events"),v_event__112027));
v_next__112498 = F_dv_with(v_next__112498,"receipts",F_dv_with(F_dv_get(v_ledger,"receipts"),v_event__112027,v_receipt__112444));
return new System.Collections.Generic.List<object>(2) {F_dv_pair("ledger",v_next__112498),F_dv_pair("receipt",v_receipt__112444)};
}
static object F_dv_action_cost_actual(object v_receipt,object v_actual) {
_ = F_dv_action_cost_validate(v_actual);
_ = F_dv_require(F_dv_and(F_dv_action_cost_le(F_dv_get(F_dv_get(v_receipt,"bounds"),"minimum"),v_actual),F_dv_action_cost_le(v_actual,F_dv_get(F_dv_get(v_receipt,"bounds"),"maximum"))),"Actual action cost outside bounds");
return v_actual;
}
static object F_dv_action_cost_settle(object v_ledger,object v_receipt,object v_actual,object v_outcome) {
object v_event__113246 = F_dv_get(v_receipt,"event");
object v_stored__113276 = F_dv_get(F_dv_get(v_ledger,"receipts"),v_event__113246);
object v_maximum__113324 = F_dv_get(F_dv_get(v_receipt,"bounds"),"maximum");
_ = F_dv_require(F_dv_and(R.eq(F_dv_get(v_ledger,"id"),F_dv_get(v_receipt,"ledger")),F_dv_same(v_stored__113276,v_receipt)),"Receipt mismatch or already settled");
_ = F_dv_require(R.eq(F_dv_get(v_stored__113276,"state"),"reserved"),"Receipt already settled");
_ = F_dv_require(F_dv_or(R.eq(v_outcome,"applied"),R.eq(v_outcome,"rejected")),"Unknown settlement outcome");
_ = F_dv_action_cost_actual(v_receipt,v_actual);
object v_next__113700 = F_dv_with(v_ledger,"revision",R.add(F_dv_get(v_ledger,"revision"),R.number("1")));
object v_settled__113766 = F_dv_with_many(v_receipt,new System.Collections.Generic.List<object>(3) {F_dv_pair("state","settled"),F_dv_pair("actual",v_actual),F_dv_pair("outcome",v_outcome)});
v_next__113700 = F_dv_with(v_next__113700,"balances",F_dv_action_cost_add(F_dv_get(v_ledger,"balances"),F_dv_action_cost_sub(v_maximum__113324,v_actual)));
v_next__113700 = F_dv_with(v_next__113700,"reserved",F_dv_action_cost_sub(F_dv_get(v_ledger,"reserved"),v_maximum__113324));
v_next__113700 = F_dv_with(v_next__113700,"spent",F_dv_action_cost_add(F_dv_get(v_ledger,"spent"),v_actual));
return F_dv_with(v_next__113700,"receipts",F_dv_with(F_dv_get(v_ledger,"receipts"),v_event__113246,v_settled__113766));
}
static object F_dv_action_cost_completion_time(object v_physical,object v_receipt,object v_actual) {
object v_now__114442 = F_dv_find(v_physical,"time",R.number("0"));
if (R.Bool(F_dv_has_key(v_receipt,"scheduled_start"))) {
object v_due__114536 = R.add(F_dv_get(v_receipt,"scheduled_start"),F_dv_get(v_actual,"time"));
_ = F_dv_require(R.not(R.lt(v_now__114442,v_due__114536)),"Scheduled action has not finished waiting");
return v_now__114442;
} else {
}
return R.add(v_now__114442,F_dv_get(v_actual,"time"));
}
static object F_dv_action_cost_clock_commit(object v_physical,object v_receipt,object v_actual,object v_status) {
object v_event__114780 = F_dv_get(v_receipt,"event");
object v_time__114810 = F_dv_action_cost_completion_time(v_physical,v_receipt,v_actual);
object v_history__114883 = F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",v_event__114780),F_dv_pair("time",v_time__114810),F_dv_pair("kind","action-cost"),F_dv_pair("status",v_status)},new System.Collections.Generic.List<object>(2) {F_dv_pair("receipt",v_receipt),F_dv_pair("actual",v_actual)});
return F_dv_with_many(v_physical,new System.Collections.Generic.List<object>(4) {F_dv_pair("time",v_time__114810),F_dv_pair("revision",R.add(F_dv_get(v_physical,"revision"),R.number("1"))),F_dv_pair("seen",F_dv_append(F_dv_get(v_physical,"seen"),v_event__114780)),F_dv_pair("history",F_dv_append(F_dv_get(v_physical,"history"),v_history__114883))});
}
static object F_dv_action_cost_execution_metadata(object v_execution,object v_receipt,object v_actual) {
return F_dv_with_many(v_execution,new System.Collections.Generic.List<object>(3) {F_dv_pair("event",F_dv_get(v_receipt,"event")),F_dv_pair("time",F_dv_get(F_dv_get(v_execution,"world"),"time")),F_dv_pair("actual_cost",v_actual)});
}
static object F_dv_action_cost_execute(object v_ledger,object v_physical,object v_quote,object v_actual) {
object v_admitted__115443 = F_dv_action_cost_admit(v_ledger,v_physical,v_quote);
object v_receipt__115508 = F_dv_get(v_admitted__115443,"receipt");
_ = F_dv_action_cost_actual(v_receipt__115508,v_actual);
object v_execution__115586 = F_dv_execute(v_physical,F_dv_get(v_receipt__115508,"action"),F_dv_get(v_receipt__115508,"event"),F_dv_action_cost_completion_time(v_physical,v_receipt__115508,v_actual),F_dv_get(v_receipt__115508,"world_revision"));
if (R.Bool(R.eq(F_dv_get(v_execution__115586,"status"),"applied"))) {
_ = false;
} else {
v_execution__115586 = F_dv_with(v_execution__115586,"world",F_dv_action_cost_clock_commit(v_physical,v_receipt__115508,v_actual,F_dv_get(v_execution__115586,"status")));
}
object v_local_1__115927 = false;
if (R.Bool(R.eq(F_dv_get(v_execution__115586,"status"),"applied"))) {
v_local_1__115927 = "applied";
} else {
v_local_1__115927 = "rejected";
}
return new System.Collections.Generic.List<object>(3) {F_dv_pair("ledger",F_dv_action_cost_settle(F_dv_get(v_admitted__115443,"ledger"),v_receipt__115508,v_actual,v_local_1__115927)),F_dv_pair("execution",F_dv_action_cost_execution_metadata(v_execution__115586,v_receipt__115508,v_actual)),F_dv_pair("receipt",v_receipt__115508)};
}
static object F_dv_action_cost_finish(object v_ledger,object v_physical,object v_quote,object v_actual,object v_outcome) {
object v_admitted__116311 = F_dv_action_cost_admit(v_ledger,v_physical,v_quote);
object v_receipt__116376 = F_dv_get(v_admitted__116311,"receipt");
_ = F_dv_action_cost_actual(v_receipt__116376,v_actual);
object v_next__116454 = F_dv_action_cost_clock_commit(v_physical,v_receipt__116376,v_actual,v_outcome);
return new System.Collections.Generic.List<object>(3) {F_dv_pair("ledger",F_dv_action_cost_settle(F_dv_get(v_admitted__116311,"ledger"),v_receipt__116376,v_actual,v_outcome)),F_dv_pair("execution",F_dv_action_cost_execution_metadata(F_dv_result(v_outcome,v_next__116454,new System.Collections.Generic.List<object>(0) {},F_dv_single(F_dv_get(v_receipt__116376,"event"))),v_receipt__116376,v_actual)),F_dv_pair("receipt",v_receipt__116376)};
}
static object F_dv_explanation_model_complete(object v_model,object v_status,object v_revision) {
return F_dv_and(R.eq(F_dv_find(v_model,"status","unknown"),v_status),F_dv_and(F_dv_or(R.eq(F_dv_find(v_model,"source_revision",R.sub(R.number("0"),R.number("1"))),v_revision),F_dv_find(v_model,"validated_by_inputs",false)),F_dv_and(F_dv_find(v_model,"plan_complete",false),F_dv_and(R.not(R.lt(F_dv_find(v_model,"remaining",R.sub(R.number("0"),R.number("1"))),R.number("0"))),F_dv_and(R.not(R.lt(F_dv_find(v_model,"work_remaining",R.sub(R.number("0"),R.number("1"))),R.number("0"))),F_dv_and(R.eq(R.count(F_dv_find(v_model,"failures",new System.Collections.Generic.List<object>(0) {})),R.number("0")),F_dv_and(R.lt(R.number("0"),R.count(F_dv_find(v_model,"predictions",new System.Collections.Generic.List<object>(0) {}))),F_dv_and(R.eq(R.count(F_dv_find(v_model,"predictions",new System.Collections.Generic.List<object>(0) {})),R.count(F_dv_find(v_model,"comparisons",new System.Collections.Generic.List<object>(0) {}))),F_dv_has_key(v_model,"event")))))))));
}
static object F_dv_explanation_observation_sources(object v_mind,object v_comparison,object v_sources) {
object v_observed__117571 = F_dv_get(v_comparison,"observed_cells");
object v_i__117616 = R.number("0");
object v_valid__117630 = true;
object v_ids__117651 = new System.Collections.Generic.List<object>(0) {};
object v_inputs__117668 = new System.Collections.Generic.List<object>(0) {};
if (R.Bool(R.eq(R.count(v_observed__117571),R.number("0")))) {
v_valid__117630 = false;
} else {
}
while (R.Bool(R.lt(v_i__117616,R.count(v_observed__117571)))) {
object v_item__117784 = R.at(v_observed__117571,v_i__117616);
object v_ref__117815 = F_dv_get(R.at(v_observed__117571,v_i__117616),"ref");
object v_cell__117849 = F_dv_read(v_mind,F_dv_get(R.at(v_observed__117571,v_i__117616),"ref"));
object v_j__117899 = R.number("0");
object v_found__117917 = false;
if (R.Bool(F_dv_same(v_cell__117849,F_dv_get(v_item__117784,"cell")))) {
_ = false;
} else {
v_valid__117630 = false;
}
while (R.Bool(R.lt(v_j__117899,R.count(v_sources)))) {
object v_source__118081 = R.at(v_sources,v_j__117899);
object v_observation__118117 = F_dv_get(R.at(v_sources,v_j__117899),"observation");
if (R.Bool(F_dv_and(R.eq(F_dv_get(v_observation__118117,"subject"),F_dv_get(v_mind,"subject")),F_dv_and(F_dv_same_local_ref(F_dv_get(v_mind,"subject"),F_dv_get(v_observation__118117,"ref"),v_ref__117815),F_dv_same(F_dv_get(v_observation__118117,"value"),F_dv_get(v_cell__117849,"value")))))) {
if (R.Bool(F_dv_has(F_dv_find(v_cell__117849,"claims",new System.Collections.Generic.List<object>(0) {}),v_observation__118117))) {
v_found__117917 = true;
if (R.Bool(F_dv_has(v_ids__117651,F_dv_get(v_source__118081,"event_id")))) {
_ = false;
} else {
v_ids__117651 = F_dv_append(v_ids__117651,F_dv_get(v_source__118081,"event_id"));
}
} else {
}
} else {
}
v_j__117899 = R.add(v_j__117899,R.number("1"));
}
if (R.Bool(v_found__117917)) {
_ = false;
} else {
v_valid__117630 = false;
}
_ = R.push(v_inputs__117668,v_item__117784);
v_i__117616 = R.add(v_i__117616,R.number("1"));
}
return new System.Collections.Generic.List<object>(3) {F_dv_pair("valid",v_valid__117630),F_dv_pair("source_event_ids",v_ids__117651),F_dv_pair("inputs",v_inputs__117668)};
}
static object F_dv_explanation_trace_changes(object v_step) {
if (R.Bool(F_dv_has_key(v_step,"changes"))) {
return F_dv_get(v_step,"changes");
} else {
if (R.Bool(F_dv_has_key(v_step,"response"))) {
object v_response__118984 = F_dv_get(v_step,"response");
if (R.Bool(F_dv_has_key(v_response__118984,"bridge"))) {
return F_dv_find(F_dv_get(v_response__118984,"bridge"),"changes",new System.Collections.Generic.List<object>(0) {});
} else {
return new System.Collections.Generic.List<object>(0) {};
}
} else {
return new System.Collections.Generic.List<object>(0) {};
}
}
}
static object F_dv_explanation_slot(object v_model,object v_slot) {
object v_predictions__119245 = F_dv_get(v_model,"predictions");
object v_index__119285 = F_dv_get(v_slot,"feature_index");
object v_valid__119320 = false;
object v_value__119342 = false;
object v_confidence__119364 = R.number("0");
object v_trace_step__119387 = R.sub(R.number("0"),R.number("1"));
object v_trace__119411 = F_dv_get(v_model,"trace");
object v_j__119439 = R.number("0");
if (R.Bool(F_dv_and(R.not(R.lt(v_index__119285,R.number("0"))),R.lt(v_index__119285,R.count(v_predictions__119245))))) {
object v_prediction__119524 = R.at(v_predictions__119245,v_index__119285);
while (R.Bool(R.lt(v_j__119439,R.count(v_trace__119411)))) {
object v_step__119609 = R.at(v_trace__119411,v_j__119439);
object v_changes__119641 = F_dv_explanation_trace_changes(R.at(v_trace__119411,v_j__119439));
object v_k__119706 = R.number("0");
if (R.Bool(R.eq(F_dv_get(v_step__119609,"at"),F_dv_get(v_slot,"trace_at")))) {
while (R.Bool(R.lt(v_k__119706,R.count(v_changes__119641)))) {
object v_change__119826 = R.at(v_changes__119641,v_k__119706);
if (R.Bool(F_dv_and(F_dv_same(F_dv_get(v_change__119826,"ref"),F_dv_get(v_slot,"ref")),F_dv_and(F_dv_uncontested(F_dv_get(v_change__119826,"after")),F_dv_and(F_dv_get(v_prediction__119524,"covered"),F_dv_find(v_slot,"value_from_prediction",false)))))) {
v_valid__119320 = true;
v_value__119342 = F_dv_get(F_dv_get(v_change__119826,"after"),"value");
v_confidence__119364 = F_dv_min(F_dv_get(v_prediction__119524,"confidence"),F_dv_get(F_dv_get(v_change__119826,"after"),"confidence"));
v_trace_step__119387 = v_j__119439;
} else {
}
v_k__119706 = R.add(v_k__119706,R.number("1"));
}
} else {
}
v_j__119439 = R.add(v_j__119439,R.number("1"));
}
} else {
}
return F_dv_with(new System.Collections.Generic.List<object>(4) {F_dv_pair("valid",v_valid__119320),F_dv_pair("value",v_value__119342),F_dv_pair("confidence",v_confidence__119364),F_dv_pair("trace_step",v_trace_step__119387)},"slot",v_slot);
}
static object F_dv_explanation_model_inputs(object v_mind,object v_model) {
object v_trace__120469 = F_dv_find(v_model,"trace",new System.Collections.Generic.List<object>(0) {});
object v_result__120513 = new System.Collections.Generic.List<object>(0) {};
object v_i__120533 = R.number("0");
while (R.Bool(R.lt(v_i__120533,R.count(v_trace__120469)))) {
object v_refs__120584 = F_dv_find(R.at(v_trace__120469,v_i__120533),"inputs",new System.Collections.Generic.List<object>(0) {});
object v_j__120635 = R.number("0");
while (R.Bool(R.lt(v_j__120635,R.count(v_refs__120584)))) {
object v_ref__120693 = F_dv_get(R.at(v_refs__120584,v_j__120635),"ref");
v_result__120513 = F_dv_explanation_merge(v_result__120513,F_dv_single(new System.Collections.Generic.List<object>(2) {F_dv_pair("ref",v_ref__120693),F_dv_pair("cell",F_dv_read(v_mind,v_ref__120693))}));
v_j__120635 = R.add(v_j__120635,R.number("1"));
}
v_i__120533 = R.add(v_i__120533,R.number("1"));
}
return v_result__120513;
}
static object F_dv_explanation_model_current(object v_mind,object v_model) {
object v_inputs__120931 = F_dv_find(v_model,"validation_inputs",new System.Collections.Generic.List<object>(0) {});
object v_valid__120988 = F_dv_same(F_dv_find(v_model,"source_adoptions",false),F_dv_get(v_mind,"adoptions"));
object v_i__121071 = R.number("0");
while (R.Bool(R.lt(v_i__121071,R.count(v_inputs__120931)))) {
object v_input__121123 = R.at(v_inputs__120931,v_i__121071);
if (R.Bool(F_dv_same(F_dv_without(F_dv_get(v_input__121123,"cell"),"time"),F_dv_without(F_dv_read(v_mind,F_dv_get(v_input__121123,"ref")),"time")))) {
_ = false;
} else {
v_valid__120988 = false;
}
v_i__121071 = R.add(v_i__121071,R.number("1"));
}
return v_valid__120988;
}
static object F_dv_accept_explanation(object v_mind,object v_model,object v_competitors,object v_slots,object v_sources,object v_policy,object v_budget,object v_time,object v_id) {
object v_valid__121437 = true;
object v_reason__121458 = "accepted-estimated";
object v_comparisons__121496 = F_dv_find(v_model,"comparisons",new System.Collections.Generic.List<object>(0) {});
object v_i__121552 = R.number("0");
object v_source_ids__121566 = new System.Collections.Generic.List<object>(0) {};
object v_inputs__121590 = new System.Collections.Generic.List<object>(0) {};
object v_competitor_ids__121610 = new System.Collections.Generic.List<object>(0) {};
object v_claims__121638 = new System.Collections.Generic.List<object>(0) {};
object v_effects__121658 = new System.Collections.Generic.List<object>(0) {};
object v_remaining__121679 = v_budget;
object v_confidence__121706 = F_dv_find(v_policy,"confidence_cap",R.number("0"));
_ = F_dv_domain(v_mind,"mind");
if (R.Bool(F_dv_and(F_dv_find(v_policy,"active",false),F_dv_and(R.lt(R.number("0"),v_confidence__121706),F_dv_and(R.lt(v_confidence__121706,R.number("1")),R.lt(R.number("0"),R.count(F_dv_find(v_policy,"basis",new System.Collections.Generic.List<object>(0) {})))))))) {
_ = false;
} else {
v_valid__121437 = false;
v_reason__121458 = "abduction-strategy-not-adopted";
}
object v_local_1__122035 = false;
if (R.Bool(F_dv_find(v_model,"validated_by_inputs",false))) {
v_local_1__122035 = F_dv_explanation_model_current(v_mind,v_model);
} else {
v_local_1__122035 = true;
}
if (R.Bool(F_dv_and(F_dv_explanation_model_complete(v_model,"explained",F_dv_get(v_mind,"revision")),v_local_1__122035))) {
_ = false;
} else {
v_valid__121437 = false;
v_reason__121458 = "model-incomplete-or-stale";
}
while (R.Bool(R.lt(v_i__121552,R.count(v_comparisons__121496)))) {
object v_comparison__122429 = R.at(v_comparisons__121496,v_i__121552);
object v_observed__122469 = F_dv_explanation_observation_sources(v_mind,R.at(v_comparisons__121496,v_i__121552),v_sources);
if (R.Bool(F_dv_and(R.eq(F_dv_get(v_comparison__122429,"result"),"match"),F_dv_get(v_observed__122469,"valid")))) {
_ = false;
} else {
v_valid__121437 = false;
v_reason__121458 = "unobserved-or-unmatched-feature";
}
v_confidence__121706 = F_dv_min(v_confidence__121706,F_dv_get(v_comparison__122429,"confidence"));
v_source_ids__121566 = F_dv_explanation_merge(v_source_ids__121566,F_dv_get(v_observed__122469,"source_event_ids"));
v_inputs__121590 = F_dv_explanation_merge(v_inputs__121590,F_dv_get(v_observed__122469,"inputs"));
v_i__121552 = R.add(v_i__121552,R.number("1"));
}
v_inputs__121590 = F_dv_explanation_merge(v_inputs__121590,F_dv_explanation_model_inputs(v_mind,v_model));
v_i__121552 = R.number("0");
while (R.Bool(R.lt(v_i__121552,R.count(v_competitors)))) {
object v_other__123098 = R.at(v_competitors,v_i__121552);
object v_j__123133 = R.number("0");
object v_other_comparisons__123151 = F_dv_find(R.at(v_competitors,v_i__121552),"comparisons",new System.Collections.Generic.List<object>(0) {});
object v_contradiction__123226 = false;
object v_local_2__123260 = false;
if (R.Bool(F_dv_find(v_other__123098,"validated_by_inputs",false))) {
v_local_2__123260 = F_dv_explanation_model_current(v_mind,v_other__123098);
} else {
v_local_2__123260 = true;
}
if (R.Bool(F_dv_and(F_dv_explanation_model_complete(v_other__123098,"inconsistent",F_dv_get(v_mind,"revision")),v_local_2__123260))) {
_ = false;
} else {
v_valid__121437 = false;
v_reason__121458 = "unresolved-competitor";
}
while (R.Bool(R.lt(v_j__123133,R.count(v_other_comparisons__123151)))) {
object v_comparison__123703 = R.at(v_other_comparisons__123151,v_j__123133);
object v_observed__123753 = F_dv_explanation_observation_sources(v_mind,R.at(v_other_comparisons__123151,v_j__123133),v_sources);
if (R.Bool(F_dv_and(R.not(R.eq(F_dv_get(v_comparison__123703,"result"),"unknown")),F_dv_get(v_observed__123753,"valid")))) {
_ = false;
} else {
v_valid__121437 = false;
v_reason__121458 = "competitor-feature-unresolved";
}
if (R.Bool(R.eq(F_dv_get(v_comparison__123703,"result"),"contradiction"))) {
v_contradiction__123226 = true;
} else {
}
v_source_ids__121566 = F_dv_explanation_merge(v_source_ids__121566,F_dv_get(v_observed__123753,"source_event_ids"));
v_inputs__121590 = F_dv_explanation_merge(v_inputs__121590,F_dv_get(v_observed__123753,"inputs"));
v_j__123133 = R.add(v_j__123133,R.number("1"));
}
v_inputs__121590 = F_dv_explanation_merge(v_inputs__121590,F_dv_explanation_model_inputs(v_mind,v_other__123098));
if (R.Bool(v_contradiction__123226)) {
_ = false;
} else {
v_valid__121437 = false;
v_reason__121458 = "competitor-not-refuted";
}
if (R.Bool(F_dv_has_key(v_other__123098,"event"))) {
v_competitor_ids__121610 = F_dv_append(v_competitor_ids__121610,F_dv_get(v_other__123098,"event"));
} else {
}
v_i__121552 = R.add(v_i__121552,R.number("1"));
}
if (R.Bool(R.eq(R.count(v_slots),R.number("0")))) {
v_valid__121437 = false;
v_reason__121458 = "no-public-claim-slots";
} else {
}
v_i__121552 = R.number("0");
while (R.Bool(F_dv_and(v_valid__121437,R.lt(v_i__121552,R.count(v_slots))))) {
if (R.Bool(R.lt(v_remaining__121679,R.number("1")))) {
v_valid__121437 = false;
v_reason__121458 = "argument-reason-budget";
} else {
object v_slot__124937 = R.at(v_slots,v_i__121552);
object v_resolved__124969 = F_dv_explanation_slot(v_model,R.at(v_slots,v_i__121552));
v_remaining__121679 = R.sub(v_remaining__121679,R.number("1"));
if (R.Bool(F_dv_get(v_resolved__124969,"valid"))) {
object v_ref__125108 = F_dv_get(v_slot__124937,"ref");
object v_existing__125143 = F_dv_read(v_mind,F_dv_get(v_slot__124937,"ref"));
if (R.Bool(F_dv_has_key(F_dv_get(v_mind,"objects"),F_dv_get(v_ref__125108,"object")))) {
_ = false;
} else {
v_valid__121437 = false;
v_reason__121458 = "unknown-local-claim-object";
}
if (R.Bool(F_dv_and(F_dv_uncontested(v_existing__125143),R.not(F_dv_same(F_dv_get(v_existing__125143,"value"),F_dv_get(v_resolved__124969,"value")))))) {
v_valid__121437 = false;
v_reason__121458 = "claim-conflicts-with-current-evidence";
} else {
}
v_confidence__121706 = F_dv_min(v_confidence__121706,F_dv_get(v_resolved__124969,"confidence"));
_ = R.push(v_claims__121638,F_dv_with(new System.Collections.Generic.List<object>(4) {F_dv_pair("slot_id",F_dv_get(v_slot__124937,"slot_id")),F_dv_pair("ref",v_ref__125108),F_dv_pair("value",F_dv_get(v_resolved__124969,"value")),F_dv_pair("trace_step",F_dv_get(v_resolved__124969,"trace_step"))},"feature_index",F_dv_get(v_slot__124937,"feature_index")));
if (R.Bool(F_dv_uncontested(v_existing__125143))) {
_ = false;
} else {
_ = R.push(v_effects__121658,F_dv_assign(v_ref__125108,F_dv_get(v_resolved__124969,"value")));
}
} else {
v_valid__121437 = false;
v_reason__121458 = "claim-not-covered-by-actual-predictive-trace";
}
v_i__121552 = R.add(v_i__121552,R.number("1"));
}
}
if (R.Bool(R.lt(R.number("0"),v_confidence__121706))) {
_ = false;
} else {
v_valid__121437 = false;
v_reason__121458 = "unsupported-zero-confidence";
}
object v_basis__126256 = F_dv_explanation_merge(F_dv_find(v_policy,"basis",new System.Collections.Generic.List<object>(0) {}),F_dv_explanation_merge(v_source_ids__121566,F_dv_append(v_competitor_ids__121610,F_dv_find(v_model,"event",""))));
object v_world__126413 = v_mind;
object v_changes__126434 = new System.Collections.Generic.List<object>(0) {};
if (R.Bool(v_valid__121437)) {
object v_stage__126473 = F_dv_stage(v_mind,v_effects__121658,v_time,F_dv_append(v_basis__126256,v_id),v_confidence__121706);
object v_j__126557 = R.number("0");
if (R.Bool(R.eq(F_dv_get(v_stage__126473,"status"),"ready"))) {
v_world__126413 = F_dv_get(v_stage__126473,"world");
v_changes__126434 = F_dv_get(v_stage__126473,"changes");
while (R.Bool(R.lt(v_j__126557,R.count(v_changes__126434)))) {
object v_ref__126732 = F_dv_get(R.at(v_changes__126434,v_j__126557),"ref");
v_world__126413 = F_dv_store(v_world__126413,v_ref__126732,F_dv_with_many(F_dv_read(v_world__126413,v_ref__126732),new System.Collections.Generic.List<object>(2) {F_dv_pair("source","abduction"),F_dv_pair("estimated",true)}));
v_j__126557 = R.add(v_j__126557,R.number("1"));
}
if (R.Bool(R.lt(R.number("0"),R.count(v_changes__126434)))) {
v_world__126413 = F_dv_with(v_world__126413,"revision",R.add(F_dv_get(v_mind,"revision"),R.number("1")));
} else {
}
} else {
v_valid__121437 = false;
v_reason__121458 = F_dv_get(v_stage__126473,"status");
}
} else {
}
object v_argument__127099 = F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("id",v_id),F_dv_pair("estimated",true),F_dv_pair("confidence",v_confidence__121706),F_dv_pair("claims",v_claims__121638)},new System.Collections.Generic.List<object>(8) {F_dv_pair("model_event",F_dv_find(v_model,"event","")),F_dv_pair("source_event_ids",v_source_ids__121566),F_dv_pair("competitor_events",v_competitor_ids__121610),F_dv_pair("source_revision",F_dv_get(v_mind,"revision")),F_dv_pair("mind_revision_after",F_dv_get(v_world__126413,"revision")),F_dv_pair("inputs",v_inputs__121590),F_dv_pair("changes",v_changes__126434),F_dv_pair("basis",v_basis__126256)});
if (R.Bool(v_valid__121437)) {
v_world__126413 = F_dv_with(v_world__126413,"history",F_dv_append(F_dv_get(v_world__126413,"history"),new System.Collections.Generic.List<object>(3) {F_dv_pair("kind","estimated-model-argument"),F_dv_pair("argument",v_argument__127099),F_dv_pair("time",v_time)}));
} else {
}
return F_dv_with_many(new System.Collections.Generic.List<object>(4) {F_dv_pair("accepted",v_valid__121437),F_dv_pair("reason",v_reason__121458),F_dv_pair("world",v_world__126413),F_dv_pair("argument",v_argument__127099)},new System.Collections.Generic.List<object>(2) {F_dv_pair("remaining",v_remaining__121679),F_dv_pair("expanded",R.sub(v_budget,v_remaining__121679))});
}
static object F_dv_explanation_merge(object v_a,object v_b) {
object v_result__127797 = v_a;
object v_i__127816 = R.number("0");
while (R.Bool(R.lt(v_i__127816,R.count(v_b)))) {
if (R.Bool(F_dv_has(v_result__127797,R.at(v_b,v_i__127816)))) {
_ = false;
} else {
v_result__127797 = F_dv_append(v_result__127797,R.at(v_b,v_i__127816));
}
v_i__127816 = R.add(v_i__127816,R.number("1"));
}
return v_result__127797;
}
static object F_dv_debug_event(object v_event) {
object v_data__128159 = F_dv_get(v_event,"data");
object v_raw_action__128185 = F_dv_find(v_data__128159,"action",new System.Collections.Generic.List<object>(0) {});
object v_action__128234 = new System.Collections.Generic.List<object>(0) {};
object v_action_id__128254 = F_dv_find(v_data__128159,"action_id","");
if (R.Bool(F_dv_record_like(v_raw_action__128185))) {
v_action__128234 = v_raw_action__128185;
v_action_id__128254 = F_dv_find(v_action__128234,"id",v_action_id__128254);
} else {
if (R.Bool(R.not(R.eq(v_raw_action__128185,false)))) {
if (R.Bool(R.is_list(v_raw_action__128185))) {
v_action_id__128254 = R.json_stringify(v_raw_action__128185);
} else {
v_action_id__128254 = R.show(v_raw_action__128185);
}
} else {
}
}
object v_detail__128626 = new System.Collections.Generic.List<object>(0) {};
object v_fields__128646 = new System.Collections.Generic.List<object>(36) {"due","until","waiting","duration","status","reason","score","plan_expansions","plan_cache_hit","reused_plan_expansions","stop","root_best","best_after_successor_layer","completed_layers","actual","observations","judgment","accepted","literal_truth","epistemic_support","source_event","execution_id","chapter","movement","process","revision_before","revision_after","suspended","dependency_expansions","dependency_stop","reason_reused","cognition_reused","reasons","steps","remaining","mind_revision"};
object v_i__129201 = R.number("0");
while (R.Bool(R.lt(v_i__129201,R.count(v_fields__128646)))) {
if (R.Bool(F_dv_has_key(v_data__128159,R.at(v_fields__128646,v_i__129201)))) {
_ = R.push(v_detail__128626,F_dv_pair(R.at(v_fields__128646,v_i__129201),F_dv_get(v_data__128159,R.at(v_fields__128646,v_i__129201))));
} else {
}
v_i__129201 = R.add(v_i__129201,R.number("1"));
}
if (R.Bool((R.Bool(R.eq(F_dv_get(v_event,"type"),"mind_simulation")) ? (object)(true) : (object)(R.eq(F_dv_get(v_event,"type"),"model_recheck"))))) {
v_detail__128626 = F_dv_with_many(v_detail__128626,new System.Collections.Generic.List<object>(4) {F_dv_pair("hypothesis",F_dv_debug_reference(F_dv_find(v_data__128159,"hypothesis",false))),F_dv_pair("result",F_dv_debug_model_result(F_dv_find(v_data__128159,"result",false))),F_dv_pair("source_adoptions",F_dv_debug_reference(F_dv_find(F_dv_find(F_dv_find(v_data__128159,"actor_before",new System.Collections.Generic.List<object>(0) {}),"mind",new System.Collections.Generic.List<object>(0) {}),"adoptions",new System.Collections.Generic.List<object>(0) {}))),F_dv_pair("scope","subjective-model-not-actual-execution; references-resolve-to-full-raw-values")});
object v_hypothesis__129999 = F_dv_find(v_data__128159,"hypothesis",new System.Collections.Generic.List<object>(0) {});
object v_calculation__130056 = F_dv_find(v_data__128159,"result",new System.Collections.Generic.List<object>(0) {});
v_detail__128626 = F_dv_with_many(v_detail__128626,new System.Collections.Generic.List<object>(2) {F_dv_pair("hypothesis_id",F_dv_find(v_hypothesis__129999,"id","")),F_dv_pair("calculation_scope",F_dv_find(v_calculation__130056,"evaluation_scope",F_dv_find(v_hypothesis__129999,"evaluation_scope",new System.Collections.Generic.List<object>(2) {F_dv_pair("kind","declared-plan"),F_dv_pair("plan_complete",F_dv_find(v_calculation__130056,"plan_complete",false))})))});
if (R.Bool(F_dv_has_key(v_data__128159,"model_before"))) {
v_detail__128626 = F_dv_with(v_detail__128626,"model_before",F_dv_debug_model_reference(F_dv_get(v_data__128159,"model_before")));
} else {
}
} else {
}
if (R.Bool(R.eq(F_dv_get(v_event,"type"),"factor_prune"))) {
v_detail__128626 = F_dv_with_many(v_detail__128626,F_dv_debug_without_snapshot(v_data__128159));
v_detail__128626 = F_dv_with(v_detail__128626,"source_adoptions",F_dv_debug_reference(F_dv_find(F_dv_find(F_dv_find(v_data__128159,"actor_before",new System.Collections.Generic.List<object>(0) {}),"mind",new System.Collections.Generic.List<object>(0) {}),"adoptions",new System.Collections.Generic.List<object>(0) {})));
v_detail__128626 = F_dv_with(v_detail__128626,"calculation_scope",new System.Collections.Generic.List<object>(2) {F_dv_pair("kind","factor-prune"),F_dv_pair("meaning","constraint-refutation-not-complete-model-explanation")});
} else {
}
if (R.Bool((R.Bool(R.eq(F_dv_get(v_event,"type"),"model_argument_unavailable")) ? (object)(true) : (object)(R.eq(F_dv_get(v_event,"type"),"model_argument_accepted"))))) {
object v_competitors__131039 = new System.Collections.Generic.List<object>(0) {};
object v_competitor_index__131068 = R.number("0");
object v_original__131101 = F_dv_find(v_data__128159,"competitors",new System.Collections.Generic.List<object>(0) {});
while (R.Bool(R.lt(v_competitor_index__131068,R.count(v_original__131101)))) {
_ = R.push(v_competitors__131039,F_dv_debug_model_reference(R.at(v_original__131101,v_competitor_index__131068)));
v_competitor_index__131068 = R.add(v_competitor_index__131068,R.number("1"));
}
v_detail__128626 = F_dv_with_many(v_detail__128626,new System.Collections.Generic.List<object>(4) {F_dv_pair("hypothesis",F_dv_debug_reference(F_dv_find(v_data__128159,"hypothesis",false))),F_dv_pair("competitors",v_competitors__131039),F_dv_pair("result",F_dv_debug_model_result(F_dv_find(v_data__128159,"result",false))),F_dv_pair("scope","subjective-argument; references-resolve-to-full-raw-values")});
v_detail__128626 = F_dv_with(v_detail__128626,"calculation_scope",new System.Collections.Generic.List<object>(2) {F_dv_pair("kind","argument-comparison"),F_dv_pair("meaning","uses-recorded-models-not-a-new-causal-replay")});
} else {
}
if (R.Bool(R.eq(F_dv_get(v_event,"type"),"action_execution"))) {
v_detail__128626 = F_dv_with(v_detail__128626,"status",F_dv_get(F_dv_get(F_dv_get(v_data__128159,"receipt"),"execution"),"status"));
} else {
}
return R.snapshot(new System.Collections.Generic.List<object>(11) {F_dv_pair("event_id",F_dv_get(v_event,"id")),F_dv_pair("type",F_dv_get(v_event,"type")),F_dv_pair("actor",F_dv_get(v_event,"actor")),F_dv_pair("game_time",F_dv_get(v_event,"time")),F_dv_pair("parents",F_dv_get(v_event,"parents")),F_dv_pair("action_id",v_action_id__128254),F_dv_pair("action_kind",F_dv_find(v_action__128234,"kind","")),F_dv_pair("label",F_dv_find(v_action__128234,"label",F_dv_find(v_action__128234,"surface",""))),F_dv_pair("purpose_sources",F_dv_find(v_action__128234,"purpose_basis",new System.Collections.Generic.List<object>(0) {})),F_dv_pair("detail",v_detail__128626),F_dv_pair("raw_event_ref",R.raw_ref(v_event))});
}
static object F_dv_debug_reference(object v_value) {
object v_reference__132297 = R.raw_ref(v_value);
if (R.Bool(R.eq(v_reference__132297,false))) {
return v_value;
} else {
}
return new System.Collections.Generic.List<object>(2) {F_dv_pair("raw_ref",v_reference__132297),F_dv_pair("meaning","complete-original-value-in-raw-journal")};
}
static object F_dv_debug_model_reference(object v_model) {
object v_reference__132507 = R.raw_ref(v_model);
if (R.Bool(R.eq(v_reference__132507,false))) {
return F_dv_debug_model_result(v_model);
} else {
}
return new System.Collections.Generic.List<object>(7) {F_dv_pair("raw_ref",v_reference__132507),F_dv_pair("event",F_dv_find(v_model,"event","")),F_dv_pair("status",F_dv_find(v_model,"status","")),F_dv_pair("fit",F_dv_find(v_model,"fit",false)),F_dv_pair("plan_complete",F_dv_find(v_model,"plan_complete",false)),F_dv_pair("evaluation_scope",F_dv_find(v_model,"evaluation_scope","declared-plan")),F_dv_pair("stop_reason",F_dv_find(v_model,"stop_reason",""))};
}
static object F_dv_debug_without_snapshot(object v_data) {
object v_result__132982 = new System.Collections.Generic.List<object>(0) {};
object v_i__133002 = R.number("0");
while (R.Bool(R.lt(v_i__133002,R.count(v_data)))) {
object v_entry__133050 = R.at(v_data,v_i__133002);
if (R.Bool(R.not(R.eq(R.at(v_entry__133050,R.number("0")),"actor_before")))) {
_ = R.push(v_result__132982,v_entry__133050);
} else {
}
v_i__133002 = R.add(v_i__133002,R.number("1"));
}
return v_result__132982;
}
static object F_dv_debug_model_result(object v_result) {
if (R.Bool(R.not(F_dv_record_like(v_result)))) {
return v_result;
} else {
}
object v_projected__133358 = new System.Collections.Generic.List<object>(0) {};
object v_i__133381 = R.number("0");
while (R.Bool(R.lt(v_i__133381,R.count(v_result)))) {
object v_entry__133431 = R.at(v_result,v_i__133381);
object v_key__133461 = R.at(v_entry__133431,R.number("0"));
if (R.Bool((R.Bool(R.not(R.eq(v_key__133461,"predicted"))) ? (object)(R.not(R.eq(v_key__133461,"world"))) : (object)(false)))) {
object v_value__133542 = R.at(v_entry__133431,R.number("1"));
if (R.Bool(F_dv_has(new System.Collections.Generic.List<object>(5) {"source_adoptions","source_response_models","source_hypothesis","trace","argument"},v_key__133461))) {
v_value__133542 = F_dv_debug_reference(v_value__133542);
} else {
}
_ = R.push(v_projected__133358,F_dv_pair(v_key__133461,v_value__133542));
} else {
}
v_i__133381 = R.add(v_i__133381,R.number("1"));
}
return v_projected__133358;
}
static object F_dv_debug_events(object v_events,object v_start,object v_config) {
_ = F_dv_require((R.Bool(R.not(R.lt(v_start,R.number("0")))) ? (object)(R.not(R.lt(R.count(v_events),v_start))) : (object)(false)),"Invalid debug event cursor");
object v_actors__133957 = F_dv_find(v_config,"actors",new System.Collections.Generic.List<object>(0) {});
object v_kinds__134004 = F_dv_find(v_config,"event_types",new System.Collections.Generic.List<object>(0) {});
object v_result__134055 = new System.Collections.Generic.List<object>(0) {};
object v_i__134075 = v_start;
while (R.Bool(R.lt(v_i__134075,R.count(v_events)))) {
object v_event__134129 = R.at(v_events,v_i__134075);
if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(R.count(v_actors__133957),R.number("0"))) ? (object)(true) : (object)(R.eq(F_dv_get(v_event__134129,"actor"),"")))) ? (object)(true) : (object)(F_dv_has(v_actors__133957,F_dv_get(v_event__134129,"actor"))))) ? (object)((R.Bool(R.eq(R.count(v_kinds__134004),R.number("0"))) ? (object)(true) : (object)(F_dv_has(v_kinds__134004,F_dv_get(v_event__134129,"type"))))) : (object)(false)))) {
_ = R.push(v_result__134055,F_dv_debug_event(v_event__134129));
} else {
}
v_i__134075 = R.add(v_i__134075,R.number("1"));
}
return new System.Collections.Generic.List<object>(4) {F_dv_pair("from",v_start),F_dv_pair("next",R.count(v_events)),F_dv_pair("events",v_result__134055),F_dv_pair("scope","independent debug projection; full snapshots remain in live session; file completeness is reported separately")};
}
static object F_raw_text() {
return "\u7B2C\u4E00\u884C\u4FDD\u7559\u4E2D\u6587\u6807\u70B9\uFF1A\uFF0C\u3002\uFF01\uFF1F\nfn pretend() = \u0022unterminated\n#include deliberately-missing.dv\n#define \u5F53 while\n// \u539F\u6587\u4E0D\u662F\u6CE8\u91CA\uFF1B\u672A\u914D\u5BF9\u62EC\u53F7 { [ (\n\u8DEF\u5F84 C:\\story\\new\\file.dv\uFF1B\u539F\u6837\u7684 \\n \u4E0E \\\u0022 \u90FD\u662F\u6587\u672C\u3002\n\u5F15\u53F7\u201C\u7532\u201D\u3001\u2018\u4E59\u2019\u3001\u300C\u4E19\u300D\u4E0E ASCII \u0022quoted\u0022\u3002\n\u975E BMP\uFF1A\uD83D\uDE00\u3001\uD842\uDFB7\uFF1B\u884C\u5206\u9694\u5B57\u7B26\uFF1A\u7532\u2028\u4E59\u3002\n\n    \u989D\u5916\u56DB\u683C\u7F29\u8FDB\u4FDD\u7559\u3002\n\u6700\u540E\u4E00\u884C\u3002";
}
static object F_raw_dialogue() {
return new System.Collections.Generic.List<object>(5) {"\u7B2C\u4E00\u53E5\uFF1A\u300C\u8BF7\u6838\u5BF9\u3002\u300D","\u0022\u0022","    \u4FDD\u7559\u989D\u5916\u7F29\u8FDB\u7684\u7B2C\u4E8C\u53E5\u3002","#include deliberately-missing-dialogue.dv","\u6700\u540E\u4E00\u53E5\uD83D\uDE00\u3002"};
}
static object F_crlf_text() {
return "\u7B2C\u4E00\u884C\u3002\n\n\u7B2C\u4E8C\u884C\uD83D\uDE00\u3002";
}
static object F_terminal_text() {
return "\u6587\u4EF6\u672B\u5C3E\u6CA1\u6709\u6362\u884C\u3002";
}
static object F_alias_scope(object v_样本) {
return v_样本;
}
static object F_conditional_text(object v_flag) {
if (R.Bool(v_flag)) {
return "\u6761\u4EF6\u6210\u7ACB\u65F6\u7684\u6B63\u6587\u3002\n\u5373\u4F7F\u5199\u4E0B return false\uFF0C\u4E5F\u4FDD\u7559\u4E3A\u6B63\u6587\u3002";
} else {
}
return "\u6761\u4EF6\u672A\u6210\u7ACB\u65F6\u7684\u6B63\u6587\u3002";
}
static object F_conditional_dialogue(object v_flag) {
if (R.Bool(v_flag)) {
return new System.Collections.Generic.List<object>(2) {"\u7B2C\u4E00\u58F0\u3002","\u7B2C\u4E8C\u58F0\u3002"};
} else {
}
return new System.Collections.Generic.List<object>(1) {"\u5B89\u9759\u3002"};
}
static object F_natural_rule(object v_actor,object v_amount) {
return F_dv_axiom(R.cat("law.",v_actor),R.number("2"),F_dv_all(new System.Collections.Generic.List<object>(3) {F_dv_fact(F_dv_ref(v_actor,"state","location"),"room"),F_dv_not(F_dv_fact(F_dv_ref(v_actor,"body","asleep"),true)),F_dv_at_least(F_dv_ref(v_actor,"state","value"),R.add(R.div(v_amount,R.number("2")),R.number("1")))}),new System.Collections.Generic.List<object>(2) {F_dv_assign(F_dv_ref(v_actor,"state","value"),R.add(v_amount,R.number("1"))),F_dv_increment(F_dv_ref(v_actor,"body","stamina"),R.sub(R.number("0"),R.number("2")))});
}
static object F_legacy_rule(object v_actor,object v_amount) {
return F_dv_axiom(R.cat("law.",v_actor),R.number("2"),F_dv_all(new System.Collections.Generic.List<object>(3) {F_dv_fact(F_dv_ref(v_actor,"state","location"),"room"),F_dv_not(F_dv_fact(F_dv_ref(v_actor,"body","asleep"),true)),F_dv_at_least(F_dv_ref(v_actor,"state","value"),R.add(R.div(v_amount,R.number("2")),R.number("1")))}),new System.Collections.Generic.List<object>(2) {F_dv_assign(F_dv_ref(v_actor,"state","value"),R.add(v_amount,R.number("1"))),F_dv_increment(F_dv_ref(v_actor,"body","stamina"),R.sub(R.number("0"),R.number("2")))});
}
static object F_natural_mixed(object v_actor,object v_amount) {
return F_dv_axiom("natural_mixed",R.number("1"),F_dv_all(new System.Collections.Generic.List<object>(2) {F_dv_fact(F_dv_ref(v_actor,"state","location"),"room"),F_dv_fact(F_dv_ref(v_actor,"body","asleep"),false)}),new System.Collections.Generic.List<object>(2) {F_dv_assign(F_dv_ref(v_actor,"state","value"),R.mul(v_amount,R.number("2"))),F_dv_increment(F_dv_ref(v_actor,"body","stamina"),R.sub(R.number("0"),R.number("1")))});
}
static object F_natural_unknown() {
return F_dv_axiom("natural_unknown",R.number("1"),F_dv_not(F_dv_fact(F_dv_ref("sample","state","missing"),true)),new System.Collections.Generic.List<object>(0) {});
}
static object F_natural_and() {
return F_dv_axiom("natural_and",R.number("1"),F_dv_all(new System.Collections.Generic.List<object>(2) {F_dv_fact(F_dv_ref("sample","state","missing"),true),F_dv_fact(F_dv_ref("sample","state","value"),R.number("5"))}),new System.Collections.Generic.List<object>(0) {});
}
static object F_natural_or() {
return F_dv_axiom("natural_or",R.number("1"),F_dv_not(F_dv_all(new System.Collections.Generic.List<object>(2) {F_dv_not(F_dv_fact(F_dv_ref("sample","state","missing"),true)),F_dv_not(F_dv_fact(F_dv_ref("sample","state","value"),R.number("5")))})),new System.Collections.Generic.List<object>(0) {});
}
static object F_natural_false_or() {
return F_dv_axiom("natural_false_or",R.number("1"),F_dv_not(F_dv_all(new System.Collections.Generic.List<object>(2) {F_dv_not(F_dv_fact(F_dv_ref("sample","state","missing"),true)),F_dv_not(F_dv_fact(F_dv_ref("sample","state","value"),R.number("9")))})),new System.Collections.Generic.List<object>(0) {});
}
static object F_natural_grouping() {
return F_dv_axiom("natural_grouping",R.number("1"),F_dv_all(new System.Collections.Generic.List<object>(2) {F_dv_not(F_dv_all(new System.Collections.Generic.List<object>(2) {F_dv_not(F_dv_fact(F_dv_ref("sample","state","missing"),true)),F_dv_not(F_dv_fact(F_dv_ref("sample","state","value"),R.number("5")))})),F_dv_not(F_dv_fact(F_dv_ref("sample","body","asleep"),true))}),new System.Collections.Generic.List<object>(0) {});
}
static object F_natural_alias() {
return F_dv_axiom("natural_alias",R.number("1"),F_dv_fact(F_dv_ref("sample","state","location"),"room"),new System.Collections.Generic.List<object>(0) {});
}
static object F_natural_unconditional() {
return F_dv_axiom("natural_unconditional",R.number("1"),F_dv_true(),new System.Collections.Generic.List<object>(0) {});
}
static object F_natural_minimal(object v_id,object v_kind,object v_known,object v_physical,object v_law,object v_grants,object v_basis) {
return F_dv_clone(new System.Collections.Generic.List<object>(7) {F_dv_pair("id",v_id),F_dv_pair("kind",v_kind),F_dv_pair("precondition",v_known),F_dv_pair("authority_precondition",v_physical),F_dv_pair("law",v_law),F_dv_pair("grants",v_grants),F_dv_pair("source_refs",v_basis)});
}
static object F_natural_action(object v_name,object v_known,object v_physical,object v_grants,object v_source) {
return F_dv_clone(new System.Collections.Generic.List<object>(9) {F_dv_pair("id",v_name),F_dv_pair("kind","inspect"),F_dv_pair("precondition",v_known),F_dv_pair("authority_precondition",v_physical),F_dv_pair("law","law.sample"),F_dv_pair("grants",v_grants),F_dv_pair("source_refs",new System.Collections.Generic.List<object>(1) {v_source}),F_dv_pair("once",true),F_dv_pair("surface","\u62FF\u8D77\u6837\u672C\uFF0C\u6838\u5BF9\u523B\u75D5\u3002\n#include \u8FD9\u4E00\u884C\u53EA\u4F5C\u6B63\u6587.dv")});
}
static object F_legacy_action(object v_name,object v_known,object v_physical,object v_grants,object v_source) {
object v_base__137213 = new System.Collections.Generic.List<object>(4) {F_dv_pair("id",v_name),F_dv_pair("kind","inspect"),F_dv_pair("precondition",v_known),F_dv_pair("authority_precondition",v_physical)};
object v_action__137319 = F_dv_with(F_dv_with(F_dv_with(v_base__137213,"law","law.sample"),"grants",v_grants),"source_refs",new System.Collections.Generic.List<object>(1) {v_source});
return F_dv_with(F_dv_with(v_action__137319,"once",true),"surface","拿起样本，核对刻痕。\n#include 这一行只作正文.dv");
}
static object F_neutral_world() {
return F_dv_add_object(F_dv_world("neutral","physical",""),F_dv_object("sample","fixture",new System.Collections.Generic.List<object>(2) {F_dv_pair("state",new System.Collections.Generic.List<object>(2) {F_dv_pair("location",F_dv_field("room")),F_dv_pair("value",F_dv_field(R.number("5")))}),F_dv_pair("body",new System.Collections.Generic.List<object>(2) {F_dv_pair("asleep",F_dv_field(false)),F_dv_pair("stamina",F_dv_field(R.number("10")))})}));
}
static object F_check_text() {
_ = R.print(R.cat("RAW ",R.json_stringify(F_raw_text())));
_ = R.print(R.cat("DIALOGUE ",R.json_stringify(F_raw_dialogue())));
_ = R.print(R.cat("CRLF ",R.json_stringify(F_crlf_text())));
_ = R.print(R.cat("TERMINAL ",R.json_stringify(F_terminal_text())));
_ = F_dv_require(R.eq(F_conditional_text(true),"条件成立时的正文。\n即使写下 return false，也保留为正文。"),"Nested narrative block changed");
_ = F_dv_require(R.eq(F_conditional_text(false),"条件未成立时的正文。"),"Narrative return changed conditional branch");
_ = F_dv_require(F_dv_same(F_conditional_dialogue(true),new System.Collections.Generic.List<object>(2) {"第一声。","第二声。"}),"Dialogue lines or blank separator changed");
_ = F_dv_require(F_dv_same(F_conditional_dialogue(false),new System.Collections.Generic.List<object>(1) {"安静。"}),"Dialogue false branch changed");
_ = R.print("SCENARIO literal-text-and-conditional-dialogue completed");
return true;
}
static object F_check_rules() {
object v_world__138573 = F_neutral_world();
object v_snapshot__138605 = F_dv_clone(v_world__138573);
object v_natural__138640 = F_natural_rule("sample",R.number("8"));
_ = F_dv_require(F_dv_same(v_natural__138640,F_legacy_rule("sample",R.number("8"))),"Natural law differs from legacy dv_axiom structure");
object v_mixed__138797 = F_natural_mixed("sample",R.number("3"));
object v_expected_mixed__138840 = F_dv_axiom("natural_mixed",R.number("1"),F_dv_all(new System.Collections.Generic.List<object>(2) {F_dv_fact(F_dv_ref("sample","state","location"),"room"),F_dv_fact(F_dv_ref("sample","body","asleep"),false)}),new System.Collections.Generic.List<object>(2) {F_dv_assign(F_dv_ref("sample","state","value"),R.number("6")),F_dv_increment(F_dv_ref("sample","body","stamina"),R.sub(R.number("0"),R.number("1")))});
_ = F_dv_require(F_dv_same(v_mixed__138797,v_expected_mixed__138840),"Mixed natural and legacy expression lowering differs");
_ = F_dv_require(R.eq(F_dv_truth(v_world__138573,F_dv_get(v_natural__138640,"premise")),"true"),"Natural law lost an alternate-facet reference or arithmetic");
_ = F_dv_require(R.eq(F_dv_truth(v_world__138573,F_dv_get(F_natural_unknown(),"premise")),"unknown"),"Natural negation converted unknown to false");
_ = F_dv_require(R.eq(F_dv_truth(v_world__138573,F_dv_get(F_natural_and(),"premise")),"unknown"),"Natural conjunction changed three-valued unknown");
_ = F_dv_require(R.eq(F_dv_truth(v_world__138573,F_dv_get(F_natural_or(),"premise")),"true"),"Natural disjunction changed unknown or true");
_ = F_dv_require(R.eq(F_dv_truth(v_world__138573,F_dv_get(F_natural_false_or(),"premise")),"unknown"),"Natural disjunction changed unknown or false");
_ = F_dv_require(R.eq(F_dv_truth(v_world__138573,F_dv_get(F_natural_grouping(),"premise")),"true"),"Parenthesized natural proposition precedence differs");
_ = F_dv_require(F_dv_same(F_dv_get(F_natural_alias(),"premise"),F_dv_fact(F_dv_ref("sample","state","location"),"room")),"Declared appellations changed their literal values");
_ = F_dv_require(R.eq(F_alias_scope(R.number("7")),R.number("7")),"A file-local appellation leaked into an included module");
_ = F_dv_require(F_dv_same(F_dv_get(F_natural_unconditional(),"premise"),F_dv_true()),"Unconditional clause was lowered to a Boolean value");
_ = F_dv_require(F_dv_same(v_world__138573,v_snapshot__138605),"Declaring or evaluating law data mutated the world");
object v_adopted__140413 = F_dv_adopt(v_world__138573,v_natural__138640,"enforce",R.number("1"),new System.Collections.Generic.List<object>(1) {"neutral-fixture"});
object v_before__140491 = F_dv_clone(v_adopted__140413);
object v_request__140526 = new System.Collections.Generic.List<object>(5) {F_dv_pair("operation","verify"),F_dv_pair("mode","enforce"),F_dv_pair("event","neutral-event"),F_dv_pair("time",R.number("1")),F_dv_pair("revision",F_dv_get(v_adopted__140413,"revision"))};
object v_verified__140644 = F_dv_evaluate(v_adopted__140413,"law.sample",v_request__140526);
_ = F_dv_require((R.Bool((R.Bool(R.eq(F_dv_get(v_verified__140644,"status"),"verified")) ? (object)(F_dv_same(F_dv_get(v_verified__140644,"world"),v_before__140491)) : (object)(false))) ? (object)(F_dv_same(v_adopted__140413,v_before__140491)) : (object)(false)),"Natural rule verification committed its effects");
_ = F_dv_require((R.Bool(R.eq(R.count(F_dv_get(F_dv_get(v_verified__140644,"world"),"history")),R.number("0"))) ? (object)(R.eq(R.count(F_dv_get(F_dv_get(v_verified__140644,"world"),"seen")),R.number("0"))) : (object)(false)),"Verification published an event");
object v_executed__140997 = F_dv_evaluate(v_adopted__140413,"law.sample",F_dv_with(v_request__140526,"operation","run"));
_ = F_dv_require(R.eq(F_dv_get(v_executed__140997,"status"),"applied"),"Natural law could not use the existing execution path");
_ = F_dv_require(R.eq(F_dv_get(F_dv_read(F_dv_get(v_executed__140997,"world"),F_dv_ref("sample","state","value")),"value"),R.number("9")),"Natural assignment changed its amount");
_ = F_dv_require(R.eq(F_dv_get(F_dv_read(F_dv_get(v_executed__140997,"world"),F_dv_ref("sample","body","stamina")),"value"),R.number("8")),"Natural increment changed its facet or amount");
_ = F_dv_require(F_dv_same(v_adopted__140413,v_before__140491),"Natural execution mutated its input snapshot");
_ = R.print("SCENARIO law-structure-three-valued-logic-and-explicit-execution completed");
return true;
}
static object F_check_actions() {
object v_world__141673 = F_neutral_world();
object v_world_before__141705 = F_dv_clone(v_world__141673);
object v_known__141744 = F_dv_all(new System.Collections.Generic.List<object>(1) {F_dv_fact(F_dv_ref("sample","state","location"),"room")});
object v_physical__141825 = F_dv_fact(F_dv_ref("sample","body","asleep"),false);
object v_grants__141895 = new System.Collections.Generic.List<object>(1) {F_dv_ref("sample","state","value")};
object v_source__141949 = new System.Collections.Generic.List<object>(2) {F_dv_pair("id","fixture-source"),F_dv_pair("path","neutral")};
object v_declared__142006 = F_natural_action("neutral.inspect",v_known__141744,v_physical__141825,v_grants__141895,v_source__141949);
_ = F_dv_require(F_dv_same(v_declared__142006,F_legacy_action("neutral.inspect",v_known__141744,v_physical__141825,v_grants__141895,v_source__141949)),"Natural action differs from legacy fields, once, or surface");
object v_minimal__142256 = F_natural_minimal("minimal","inspect",v_known__141744,v_physical__141825,"law.sample",v_grants__141895,new System.Collections.Generic.List<object>(1) {v_source__141949});
object v_expected_minimal__142361 = new System.Collections.Generic.List<object>(7) {F_dv_pair("id","minimal"),F_dv_pair("kind","inspect"),F_dv_pair("precondition",v_known__141744),F_dv_pair("authority_precondition",v_physical__141825),F_dv_pair("law","law.sample"),F_dv_pair("grants",v_grants__141895),F_dv_pair("source_refs",new System.Collections.Generic.List<object>(1) {v_source__141949})};
_ = F_dv_require(F_dv_same(v_minimal__142256,v_expected_minimal__142361),"Minimal natural action did not retain its seven declared fields");
_ = F_dv_require((R.Bool(F_dv_same(v_world__141673,v_world_before__141705)) ? (object)(R.eq(R.count(F_dv_get(v_world__141673,"history")),R.number("0"))) : (object)(false)),"Action declaration executed a world change");
object v_saved__142776 = F_dv_clone(v_declared__142006);
object v_saved_minimal__142811 = F_dv_clone(v_minimal__142256);
_ = R.push(v_grants__141895,F_dv_ref("sample","body","stamina"));
_ = R.push(F_dv_get(v_known__141744,"items"),F_dv_true());
_ = R.replace(v_source__141949,R.number("0"),new System.Collections.Generic.List<object>(2) {"id","mutated-source"});
_ = R.replace(v_physical__141825,R.number("2"),new System.Collections.Generic.List<object>(2) {"value",true});
_ = F_dv_require((R.Bool(F_dv_same(v_declared__142006,v_saved__142776)) ? (object)(F_dv_same(v_minimal__142256,v_saved_minimal__142811)) : (object)(false)),"Natural action shares mutable input fields");
_ = R.print("SCENARIO action-data-once-surface-and-independent-ownership completed");
return true;
}
static object F_checks_main() {
_ = F_check_text();
_ = F_check_rules();
_ = F_check_actions();
return true;
}
public static int Main(string[] args) { R.Arguments=args; try { _ = F_checks_main(); return 0; } catch(System.Exception e) { System.Console.Error.WriteLine(e.ToString()); return 1; } }
}
