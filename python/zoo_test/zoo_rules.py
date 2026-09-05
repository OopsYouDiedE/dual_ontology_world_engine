"""动物园怪谈全部九套规则出版物与客观规则补丁。

以《动物园规则怪谈》（A市动物园规则怪谈）原始文本构建 AST 规则树与客观 Patch。
"""

from __future__ import annotations

from ..core.entity_system import Entity
from ..core.soul import (
    Directive,
    E,
    Effect,
    Expr,
    F,
    Mode,
    Rule,
    RulePatch,
    RulePublication,
)


def build_zoo_rule_publications(zoo_authority: Entity, aquarium_authority: Entity) -> dict[str, Entity]:
    """生成动物园规则怪谈全部九份规则文本出版物。"""
    true_expr = E("true")

    # 1. 动物园园区游客守则 (15条)
    visitor_directives = (
        Directive(Mode.FORBID, F("touch_escaped_rabbit")),
        Directive(Mode.REQUIRE, F("report_escaped_rabbit")),
        Directive(Mode.REQUIRE, F("choose_left_ape_street")),
        Directive(Mode.FORBID, F("choose_right_ape_street")),
        Directive(Mode.FORBID, F("observe_white_elephant")),
        Directive(Mode.FORBID, F("buy_rabbit_blood")),
        Directive(Mode.FORBID, F("stay_alone_under_rabbit_shade")),
        Directive(Mode.FORBID, F("buy_aquarium_ticket")),
        Directive(Mode.REQUIRE, F("leave_aquarium_view")),
        Directive(Mode.FORBID, F("feed_rabbit")),
        Directive(Mode.PERMIT, F("feed_other_animals")),
        Directive(Mode.REQUIRE, F("tear_and_hold_dotted_line")),
        Directive(Mode.REQUIRE, F("seek_blue_staff_to_lion_rock")),
        Directive(Mode.FORBID, F("interact_with_black_staff")),
        Directive(Mode.FORBID, F("leave_lion_zone_if_more_than_4_lions")),
        Directive(Mode.FORBID, F("adult_buy_rabbit_toy")),
        Directive(Mode.FORBID, F("enter_elephant_zone_with_rabbit_ears")),
        Directive(Mode.PREFER, F("flee_to_lion_zone")),
        Directive(Mode.FORBID, F("follow_suspicious_companion")),
    )
    visitor_rule = Rule(true_expr, visitor_directives, source="动物园园区游客守则")
    pub_visitor = RulePublication(visitor_rule, zoo_authority, guarantee_tag="Authority")
    pub_visitor.name = "《动物园园区游客守则》"

    # 2. 动物园员工守则 (蓝衣, 15条)
    blue_directives = (
        Directive(Mode.FORBID, F("catch_rabbit_directly")),
        Directive(Mode.REQUIRE, F("lead_rabbit_to_lion_zone")),
        Directive(Mode.REQUIRE, F("evacuate_ape_left_and_lock")),
        Directive(Mode.REQUIRE, F("correct_elephant_cognition")),
        Directive(Mode.REQUIRE, F("store_rabbit_blood")),
        Directive(Mode.REQUIRE, F("trim_rabbit_bushes")),
        Directive(Mode.FORBID, F("talk_with_aquarium_believer")),
        Directive(Mode.FORBID, F("enter_aquarium")),
        Directive(Mode.FORBID, F("feed_rabbit_irregularly")),
        Directive(Mode.REQUIRE, F("throw_dotted_line_to_elephant_lawn")),
        Directive(Mode.REQUIRE, F("escort_lost_visitor_to_lion_zone")),
        Directive(Mode.FORBID, F("cooperate_with_black_staff")),
        Directive(Mode.REQUIRE, F("splash_rabbit_blood_on_fifth_lion")),
        Directive(Mode.FORBID, F("use_rabbit_merch")),
        Directive(Mode.REQUIRE, F("guard_elephant_gate_until_scream")),
        Directive(Mode.REQUIRE, F("flee_to_lion_zone_when_corrupted")),
        Directive(Mode.PREFER, F("treat_white_lions_kindly")),
    )
    blue_rule = Rule(true_expr, blue_directives, source="员工守则（蓝衣）")
    pub_blue = RulePublication(blue_rule, zoo_authority, guarantee_tag="Authority")
    pub_blue.name = "《动物园员工守则》"

    # 3. 海洋馆门口张贴告示 (5条)
    aquarium_gate_directives = (
        Directive(Mode.REQUIRE, F("wear_black_uniform")),
        Directive(Mode.FORBID, F("flee_to_lion_zone")),
        Directive(Mode.REQUIRE, F("keep_rabbit_signal")),
        Directive(Mode.FORBID, F("look_at_elephant_sign")),
        Directive(Mode.REQUIRE, F("feed_dotted_line_to_rabbit_and_go_ape_right")),
    )
    aquarium_gate_rule = Rule(true_expr, aquarium_gate_directives, source="海洋馆门口张贴告示")
    pub_aquarium_gate = RulePublication(aquarium_gate_rule, aquarium_authority, guarantee_tag="Guarantee")
    pub_aquarium_gate.name = "《海洋馆门口张贴告示》"

    # 4. 游客捡到的纸条 (9条)
    found_note_directives = (
        Directive(Mode.PREFER, F("eat_goat_meat")),
        Directive(Mode.FORBID, F("believe_rabbit_blood_exists")),
        Directive(Mode.FORBID, F("enter_ocean_hall_when_empty")),
        Directive(Mode.PREFER, F("sleep_in_aquarium_at_night")),
        Directive(Mode.REQUIRE, F("remember_you_are_elephant")),
        Directive(Mode.REQUIRE, F("remember_i_am_goat")),
    )
    found_note_rule = Rule(true_expr, found_note_directives, source="游客捡到的纸条")
    pub_found_note = RulePublication(found_note_rule, zoo_authority, guarantee_tag="Rumor")
    pub_found_note.name = "《游客捡到的纸条》"

    # 5. 海洋馆内部张贴告示 (9条)
    aquarium_inside_directives = (
        Directive(Mode.FORBID, F("stay_if_greeted")),
        Directive(Mode.FORBID, F("eat_goat_meat")),
        Directive(Mode.REQUIRE, F("leave_within_half_hour_before_1600")),
        Directive(Mode.REQUIRE, F("stay_over_4_hours_after_1601")),
        Directive(Mode.PREFER, F("treat_whale_elephant_as_3d")),
        Directive(Mode.REQUIRE, F("turn_off_jellyfish_lamp_before_sleep")),
        Directive(Mode.FORBID, F("give_dotted_line_to_black_staff")),
        Directive(Mode.REQUIRE, F("believe_aquarium_independent")),
    )
    aquarium_inside_rule = Rule(true_expr, aquarium_inside_directives, source="海洋馆内部张贴告示")
    pub_aquarium_inside = RulePublication(aquarium_inside_rule, aquarium_authority, guarantee_tag="Authority")
    pub_aquarium_inside.name = "《海洋馆内部张贴告示》"

    # 6. 海洋馆员工守则 (红衣, 12条)
    red_directives = (
        Directive(Mode.REQUIRE, F("work_night_shift_strictly")),
        Directive(Mode.REQUIRE, F("force_turn_off_lamp_by_0115")),
        Directive(Mode.FORBID, F("investigate_whale_elephant_switch")),
        Directive(Mode.REQUIRE, F("patrol_hourly_and_rest_in_jellyfish")),
        Directive(Mode.FORBID, F("answer_aquarium_location")),
        Directive(Mode.REQUIRE, F("charge_jellyfish_lamps_on_sunday")),
        Directive(Mode.REQUIRE, F("store_goat_meat_in_whale_box")),
        Directive(Mode.PREFER, F("dissuade_early_exit")),
        Directive(Mode.REQUIRE, F("repel_black_coat_guests")),
        Directive(Mode.REQUIRE, F("confiscate_guest_rabbit_merch")),
        Directive(Mode.FORBID, F("mention_zoo_to_guests")),
        Directive(Mode.REQUIRE, F("believe_no_zoo_outside")),
    )
    red_rule = Rule(true_expr, red_directives, source="海洋馆员工守则（红衣）")
    pub_red = RulePublication(red_rule, aquarium_authority, guarantee_tag="Authority")
    pub_red.name = "《海洋馆员工守则》"

    # 7. 便签条 (保安室, 5条)
    security_directives = (
        Directive(Mode.REQUIRE, F("keep_uniform_color_cognition")),
        Directive(Mode.PREFER, F("trust_fellow_humans")),
        Directive(Mode.REQUIRE, F("flee_from_entity_crying_or_laughing")),
        Directive(Mode.PREFER, F("consume_food_normally_without_hesitation")),
    )
    security_rule = Rule(true_expr, security_directives, source="大象园区保安便签条")
    pub_security = RulePublication(security_rule, zoo_authority, guarantee_tag="Authority")
    pub_security.name = "《保安室便签条》"

    # 8. 动物园园长办公室文件 (15条)
    director_directives = (
        Directive(Mode.REQUIRE, F("accept_all_staff_colors")),
        Directive(Mode.FORBID, F("show_agitation_to_strange_noises")),
        Directive(Mode.FORBID, F("bring_pets_to_zoo")),
        Directive(Mode.REQUIRE, F("record_white_lions_in_it_table")),
        Directive(Mode.REQUIRE, F("maintain_three_dotted_maps")),
        Directive(Mode.PREFER, F("treat_security_generously")),
        Directive(Mode.PERMIT, F("permit_staff_anomaly_cleaning")),
        Directive(Mode.REQUIRE, F("office_lights_and_blackout_protocol")),
        Directive(Mode.REQUIRE, F("follow_notice_by_physical_location")),
        Directive(Mode.REQUIRE, F("leave_office_if_doll_anomaly")),
        Directive(Mode.FORBID, F("doze_during_night_shift")),
        Directive(Mode.FORBID, F("fix_furry_camera_within_month")),
        Directive(Mode.PERMIT, F("notify_cleaner_about_jellyfish")),
        Directive(Mode.REQUIRE, F("quarantine_13_hours_in_office")),
        Directive(Mode.PREFER, F("prioritize_human_life_over_animals")),
    )
    director_rule = Rule(true_expr, director_directives, source="动物园园长办公室文件")
    pub_director = RulePublication(director_rule, zoo_authority, guarantee_tag="Authority")
    pub_director.name = "《动物园园长办公室文件》"

    # 9. 遗留的纸张 (水母区客房, 12条)
    abandoned_directives = (
        Directive(Mode.PREFER, F("trust_white_lions")),
        Directive(Mode.FORBID, F("change_uniform_color")),
        Directive(Mode.REQUIRE, F("seek_security_before_0100")),
        Directive(Mode.REQUIRE, F("rest_in_dark_closed_room")),
        Directive(Mode.PREFER, F("stand_with_apes")),
        Directive(Mode.REQUIRE, F("remember_two_eyes_of_human")),
        Directive(Mode.PREFER, F("team_up_with_scared_visitors")),
        Directive(Mode.REQUIRE, F("remember_you_are_human_not_animal")),
    )
    abandoned_rule = Rule(true_expr, abandoned_directives, source="水母区客房遗留的纸张")
    pub_abandoned = RulePublication(abandoned_rule, aquarium_authority, guarantee_tag="Warning")
    pub_abandoned.name = "《遗留的纸张》"

    return {
        "visitor": pub_visitor,
        "blue_staff": pub_blue,
        "aquarium_gate": pub_aquarium_gate,
        "found_note": pub_found_note,
        "aquarium_inside": pub_aquarium_inside,
        "red_staff": pub_red,
        "security": pub_security,
        "director": pub_director,
        "abandoned": pub_abandoned,
    }


def build_zoo_rule_patches(world_root: Entity) -> list[Entity]:
    """构建直接约束现实执行路径的客观规则补丁。"""
    true_expr = E("true")

    # 补丁1: 虚线护身符。持有撕下虚线时，阻止对目标的直接认知侵蚀。
    has_dotted_line = E("has_item", "actor", "dotted_line")
    protect_from_it_rule = Rule(
        has_dotted_line,
        (Directive(Mode.FORBID, F("inflict_cognition_corruption")),),
        source="客观规律：虚线防护效应",
    )
    patch_protection = RulePatch("虚线防护法则", protect_from_it_rule, world_root)

    # 补丁2: 衣服颜色认知锚定。
    has_color_anchor = E("has_trait", "actor", "uniform_color_anchored")
    anchor_rule = Rule(
        has_color_anchor,
        (Directive(Mode.FORBID, F("force_identity_mutation")),),
        source="客观规律：自我认知坚定",
    )
    patch_anchor = RulePatch("认知锚定法则", anchor_rule, world_root)

    # 补丁3: 人类生命绝对优先（园长意志与世界客观倾向）。
    human_priority_rule = Rule(
        true_expr,
        (Directive(Mode.PREFER, F("sacrifice_animal_for_human")),),
        source="客观规律：人类存续第一优先",
    )
    patch_priority = RulePatch("人类优先法则", human_priority_rule, world_root)

    return [patch_protection, patch_anchor, patch_priority]
