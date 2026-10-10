续篇 chapter2

    执行
        procedures:
            NightTakeSelf: wait(2, seq(travel("route.Logistics"), when(tag("object.Granary", "Locked", 1), seq(when(tag("$actor", "GranaryKey", 1), set("object.Granary", "Locked", 0)), when(tag("$actor", "Lockpick", 1), set("object.Granary", "Locked", 0)), when(tag("object.Granary", "LatchLoose", 1), set("object.Granary", "Locked", 0)))), require((((非 tag("object.Granary", "Locked", 1))))), spend("object.Granary", "Ration", 1), change("$actor", "OweNote", 1), set("object.Granary", "Locked", 1), wait(3, seq(change("$actor", "Fed", 1), change("$actor", "NightAte", 1)))))
            NightTakeSelfRec: wait(2, seq(travel("route.Logistics"), when(tag("object.Granary", "Locked", 1), seq(when(tag("$actor", "GranaryKey", 1), set("object.Granary", "Locked", 0)), when(tag("$actor", "Lockpick", 1), set("object.Granary", "Locked", 0)), when(tag("object.Granary", "LatchLoose", 1), set("object.Granary", "Locked", 0)))), require((((非 tag("object.Granary", "Locked", 1))))), spend("object.Granary", "Ration", 1), change("object.Ledger", "NightRecorded", 1), set("object.Granary", "Locked", 1), wait(3, seq(change("$actor", "Fed", 1), change("$actor", "NightAte", 1)))))
            NightTakeSick: wait(2, seq(travel("route.Logistics"), when(tag("object.Granary", "Locked", 1), seq(when(tag("$actor", "GranaryKey", 1), set("object.Granary", "Locked", 0)), when(tag("$actor", "Lockpick", 1), set("object.Granary", "Locked", 0)), when(tag("object.Granary", "LatchLoose", 1), set("object.Granary", "Locked", 0)))), require((((非 tag("object.Granary", "Locked", 1))))), spend("object.Granary", "Ration", 1), change("$actor", "OweNote", 1), set("object.Granary", "Locked", 1), wait(1.5, seq(travel("route.Clinic"), change("object.Bowl", "Porridge", 1)))))
            NightTakeSickRec: wait(2, seq(travel("route.Logistics"), when(tag("object.Granary", "Locked", 1), seq(when(tag("$actor", "GranaryKey", 1), set("object.Granary", "Locked", 0)), when(tag("$actor", "Lockpick", 1), set("object.Granary", "Locked", 0)), when(tag("object.Granary", "LatchLoose", 1), set("object.Granary", "Locked", 0)))), require((((非 tag("object.Granary", "Locked", 1))))), spend("object.Granary", "Ration", 1), change("object.Ledger", "NightRecorded", 1), set("object.Granary", "Locked", 1), wait(1.5, seq(travel("route.Clinic"), change("object.Bowl", "Porridge", 1)))))
            Inform: seq(travel("route.Logistics"), inspect("object.Ledger"), change("object.Ledger", "NightRecorded", 1), change("$actor", "OweNote", -1))
            Collect: seq(travel("route.Logistics"), wait_until(tag("object.Desk", "Open", 1), seq(require(tag("object.Granary", "Ration", 1)), spend("object.Granary", "Ration", 1), change("$actor", "Fed", 1), change("object.Ledger", "Handed", 1))))
            CountLedger: seq(travel("route.Logistics"), inspect("object.Granary"), inspect("object.Ledger"), change("object.Ledger", "Counted", 1))
            AnswerName: seq(inspect("object.ArchiveDoor"), change("$actor", "Answered", 1))
            Accuse: seq(travel("route.Hall"), change("Board.Hall", "Accusation", 1))
            Defend: seq(travel("route.Hall"), inspect("Board.Hall"))
            GiveKey: seq(require(tag("$actor", "GranaryKey", 1)), change("$actor", "GranaryKey", -1), change("person.Shen", "GranaryKey", 1))
            SeizePattern: change("object.ArchiveDoor", "Work", 1)
            RepairLatch: seq(travel("route.Logistics"), require(tag("object.Granary", "LatchLoose", 1)), set("object.Granary", "LatchLoose", 0), set("object.Granary", "ToolMarks", 1))
            Care: seq(travel("route.Clinic"), inspect("object.Bed"), when(tag("$actor", "MedKey", 1), spend("object.MedCab", "Medicine", 1)), change("object.Bed", "Treated", 1))
            GuardArchive: seq(travel("route.ArchiveFront"), inspect("object.ArchiveDoor"), wait(0.3, seq(require((((非 tag("object.WeightRoom", "Occupied", 1))))), set("object.WeightRoom", "Occupied", 1), set("$actor", "Guarding", 1), wait(8, seq(inspect("object.ArchiveDoor"), set("object.WeightRoom", "Occupied", 0), set("$actor", "Guarding", 0))))))
            FeedWeak: seq(travel("route.Logistics"), wait_until(tag("object.Desk", "Open", 1), seq(require(tag("object.Granary", "Ration", 1)), spend("object.Granary", "Ration", 1), change("object.Ledger", "Handed", 1), change("$target", "Fed", 1), change("$actor", "DidFeedWeak", 1))))
            Work: seq(travel("$work_route"), inspect("$work_room"), wait(2.5, inspect("$work_room")))
            Sleep: seq(travel("$dorm_route"), inspect("$dorm_room"))
            Rest: seq(travel("$dorm_route"), inspect("$dorm_room"), wait(3, inspect("$dorm_room")))
            Class: seq(travel("route.Lecture"), inspect("object.Blackboard"))
            Distribute: seq(travel("route.Logistics"), require(tag("$actor", "GranaryKey", 1)), set("object.Granary", "Locked", 0), set("object.Desk", "Open", 1), inspect("object.Ledger"))
            Cook: seq(travel("route.Logistics"), spend("object.Jar", "Water", 1), set("object.Stove", "Cooked", 1))
            FetchWater: seq(travel("route.Logistics"), change("object.Jar", "Water", 3))
            InspectStructure: seq(travel("route.Logistics"), inspect("object.Granary"), wait(5, inspect("object.Granary")))
            VisitArchive: seq(travel("route.ArchiveFront"), inspect("object.ArchiveDoor"), wait(2, inspect("object.ArchiveDoor")))
            Greenhouse: seq(travel("route.Greenhouse"), inspect("object.Gauge"), when(tag("object.Valve", "Occupied", 1), seq(require(((非 tag("object.Gauge", "Occupied", 1)))), occupy("object.Gauge", "Occupied", 1), occupy("$actor", "AtGauge", 1))), when(((非 tag("object.Valve", "Occupied", 1))), seq(occupy("object.Valve", "Occupied", 1), occupy("$actor", "AtValve", 1))), wait(2.5, seq(require(at("$actor", "room.Greenhouse")), inspect("object.Gauge"))))
            Socialize: seq(travel("route.Hall"), inspect("Board.Hall"))
            Stele: seq(travel("route.Oath"), inspect("object.Stele"))
            Scavenge: wait(2, seq(travel("route.Logistics"), inspect("object.Granary"), wait(2, inspect("object.Granary"))))
            OpenGranary: when(tag("object.Granary", "Locked", 1), seq(when(tag("$actor", "GranaryKey", 1), set("object.Granary", "Locked", 0)), when(tag("$actor", "Lockpick", 1), set("object.Granary", "Locked", 0)), when(tag("object.Granary", "LatchLoose", 1), set("object.Granary", "Locked", 0))))
            Hoard: wait(2, seq(travel("route.Logistics"), procedure("OpenGranary"), require(((非 tag("object.Granary", "Locked", 1)))), spend("object.Granary", "Ration", 2), set("object.Granary", "Locked", 1), change("$actor", "Stash", 2)))
            PostRules: seq(travel("route.Hall"), change("Board.Hall", "Posted", 1))
            Seize: seq(travel("$station_route"), inspect("object.ArchiveDoor"), wait(0.3, seq(change("object.ArchiveDoor", "Work", 1), require(((非 tag("$station", "Occupied", 1)))), occupy("$station", "Occupied", 1), occupy("$actor", "Prying", 1), wait(3, seq(inspect("object.ArchiveDoor"), set("$station", "Worked", 1), think(((非 related("$actor", "Promised", "object.ArchiveDoor")))))))))
            Share: seq(spend("$actor", "Fed", 1), change("$target", "Fed", 1), change("$actor", "DidShare", 1))
            Tear: seq(travel("route.Hall"), inspect("Board.Hall"), change("Board.Hall", "Torn", 1))
            Watch: seq(travel("route.Logistics"), inspect("object.Granary"), wait(8, inspect("object.Granary")))
            Treat: seq(travel("route.Clinic"), inspect("object.Bed"), wait(1, seq(spend("object.MedCab", "Medicine", 1), inspect("object.Bed"))))
            Retreat: seq(travel("route.Oath"), inspect("object.Stele"), wait(3, inspect("object.Stele")))
            Report: seq(travel("route.Hall"), inspect("Board.Hall"), wait(1, inspect("Board.Hall")))
            Sample: seq(travel("$sample_route"), inspect("$sample_object"), wait(1.5, inspect("$sample_object")))
            NightSketch: seq(travel("route.Hall"), inspect("room.Hall"), wait(6, inspect("room.Hall")))
            Sketch: seq(travel("route.DormS"), inspect("room.DormS"), wait(2, inspect("room.DormS")))
            Tidy: seq(travel("$dorm_route"), inspect("$litter"), set("$litter", "Crumbs", 0), set("$litter", "Emptied", 1))
            Patrol: seq(travel("route.Hall"), wait(2.5, seq(travel("route.ArchiveFront"), wait(2.5, seq(travel("route.Hall"), wait(2.5, seq(travel("route.Hall"), wait(2.5, travel("route.DormL")))))))))
        actions:
            RepairLatch:
                condition: tag("route.Logistics", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: procedure("RepairLatch")
                flags: Safety
                job: RepairLatch
                cost: 0.8
                travel_cost: 2
                send_cost: 0.01
                text: social.request
                actor: recipient
                target: none
            Inform:
                condition: tag("route.Logistics", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: procedure("Inform")
                flags: Records
                job: Inform
                cost: 0.8
                travel_cost: 2
                send_cost: 0.01
                text: social.request
                actor: recipient
                target: none
            FeedWeak:
                condition: tag("route.Logistics", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: procedure("FeedWeak")
                flags: SickPriority
                job: FeedWeak
                cost: 0.8
                travel_cost: 2
                send_cost: 0.01
                text: social.request
                actor: recipient
                target: kind("$target", "Body") 且 tag("$target", "Hungry", 1)
                target_self: false
            Care:
                condition: tag("route.Clinic", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: procedure("Care")
                flags: SickPriority
                job: Care
                cost: 0.8
                travel_cost: 2
                send_cost: 0.01
                text: social.request
                actor: recipient
                target: none
            GuardArchive:
                condition: tag("route.ArchiveFront", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: procedure("GuardArchive")
                flags: SealArchive
                job: GuardArchive
                cost: 0.8
                travel_cost: 2
                send_cost: 0.01
                text: social.request
                actor: recipient
                target: none
            CountLedger:
                condition: tag("route.Logistics", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: procedure("CountLedger")
                flags: Records
                job: CountLedger
                cost: 0.8
                travel_cost: 2
                send_cost: 0.01
                text: social.request
                actor: recipient
                target: none
            Escort:
                condition: tag("$destination", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: seq(travel("$destination"), inspect("$thing"))
                flags: EvidenceFirst
                cost: 0.8
                travel_cost: 2
                send_cost: 0.01
                text: social.escort
                actor: recipient
                target: none
            Help:
                condition: tag("$work_route", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: procedure("Work")
                flags: 
                job: Work
                cost: 0.8
                text: social.help
                actor: sender
                target: none
            ShareFood:
                condition: tag("$sender", "Fed", 1)
                condition_world: physical
                goal: true()
                effect: seq(require(tag("$actor", "Fed", 1)), spend("$actor", "Fed", 1), change("$recipient", "Fed", 1))
                flags: SickPriority,Rations
                cost: 0.8
                text: social.share
                actor: sender
                target: none
            GiveKey:
                condition: tag("$actor", "GranaryKey", 1)
                condition_world: physical
                goal: true()
                effect: seq(require(tag("$actor", "GranaryKey", 1)), change("$actor", "GranaryKey", -1), change("$about", "GranaryKey", 1))
                flags: -
                cost: 0.8
                travel_cost: 2
                send_cost: 0.01
                text: social.give_key
                actor: recipient
                target: none
            Greenhouse:
                condition: tag("route.Greenhouse", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: procedure("Greenhouse")
                flags: -
                cost: 0.8
                travel_cost: 2
                send_cost: 0.01
                text: social.greenhouse
                actor: recipient
                target: none
            Archive:
                condition: tag("route.ArchiveFront", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: seq(travel("route.ArchiveFront"), inspect("object.ArchiveDoor"))
                flags: -
                cost: 0.8
                travel_cost: 2
                send_cost: 0.01
                text: social.archive
                actor: recipient
                target: none
            Rub:
                condition: tag("route.Oath", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: seq(travel("route.Oath"), inspect("object.Stele"))
                flags: -
                cost: 0.8
                travel_cost: 2
                send_cost: 0.01
                text: social.rub
                actor: recipient
                target: none
        receipt_attitudes:
            rules:
                accepted:
                    Trust: 1
                rejected:
                    Trust: -1
            requests:
                rejected:
                    Trust: -1
            mediation:
                accepted:
                    Trust: 1
                rejected:
                    Trust: -1
        mediation:
            participants: Mora、Ruan
            wordings:
                sick_first:
                    flags: SickPriority
                    opposed: Records
                sign_first:
                    flags: Records
                    opposed: SickPriority
                i_carry:
                    flags: SickPriority,Records
                    opposed: -
        commitments:
            due_days: 1
            deadline_slot: Night
            fulfill_action: Help
            text: social.promise_text
            accepted: social.promise_accepted
            failed: social.promise_failed

        constants:
            FirstDay: 2
            WeakAfterDays: 2
            GreenhouseNeed: 6
            GreenhouseYield: 6
        belief_parameters:
            ignores_pressure_warning:
                rule: Warning2
                accepted: false
            knows_pressure_relief:
                claim: 温室泄压法
        body_defaults:
            OilAllocated: 0
            InjuredDays: 0
            HungerStreak: 0
            MaxHunger: 0
            SkillStructure: 0
            SkillGreenhouse: 0
        bindings:
            Sample:
                Selma:
                    $sample_route: route.Greenhouse
                    $sample_object: object.Gauge
                default:
                    $sample_route: route.Logistics
                    $sample_object: object.Stove
        laws:
            illness:
                hook: begin
                once: world
                condition: tag("object.Clock", "Day", 4) 且 tag("object.Clock", "SlotAfternoon", 1)
                effect: set("person.Moran", "Sick", 1)
                text: physical.illness
            hunger:
                hook: dawn
                once: day
                each: characters
                condition: true()
                effect: seq(set("$body", "HungerStreak", int_when((tag("$body", "Fed", 1) 或 (非 tag("object.Clock", "Day", add(value("object.Clock", "FirstDay"), 1)))), 0, add(value("$body", "HungerStreak"), 1))), set("$body", "MaxHunger", max(value("$body", "MaxHunger"), int_when((tag("$body", "Fed", 1) 或 (非 tag("object.Clock", "Day", add(value("object.Clock", "FirstDay"), 1)))), 0, add(value("$body", "HungerStreak"), 1)))), set("$body", "Hungry", int_when((tag("$body", "Fed", 1) 或 (非 tag("object.Clock", "Day", add(value("object.Clock", "FirstDay"), 1)))), 0, 1)), when(tag("$body", "HungerStreak", value("object.Clock", "WeakAfterDays")), set("$body", "Weak", 1)), when((非 tag("$body", "HungerStreak", value("object.Clock", "WeakAfterDays"))), set("$body", "Weak", 0)), set("$body", "Fed", 0))
            breakfast:
                hook: dawn
                once: day
                condition: tag("person.Moran", "Sick", 1) 且 tag("object.Bowl", "Porridge", 1)
                effect: seq(spend("object.Bowl", "Porridge", 1), set("person.Moran", "Fed", 1), set("object.Bowl", "Residue", 1))
            recovery:
                hook: end
                once: day
                condition: tag("object.Clock", "SlotEvening", 1) 且 tag("person.Moran", "Sick", 1) 且 tag("object.Bed", "Treated", 3)
                effect: set("person.Moran", "Sick", 0)
                text: physical.recovery
            night_food:
                hook: dawn
                once: day
                each: characters
                condition: tag("$body", "NightAte", 1)
                effect: seq(change("$litter", "Crumbs", 1), set("$body", "NightAte", 0))
            cooked_meal:
                hook: end
                once: slot
                condition: (tag("object.Clock", "SlotNoon", 1) 或 tag("object.Clock", "SlotEvening", 1)) 且 tag("object.Stove", "Cooked", 1)
                effect: seq(change("object.Granary", "Ration", div(sub(value("object.Ledger", "Handed"), value("object.Ledger", "LastHanded")), 5)), set("object.Ledger", "LastHanded", value("object.Ledger", "Handed")))
            raw_meal:
                hook: end
                once: slot
                condition: (tag("object.Clock", "SlotNoon", 1) 或 tag("object.Clock", "SlotEvening", 1)) 且 (非 tag("object.Stove", "Cooked", 1))
                effect: seq(spend("object.Granary", "Ration", min(value("object.Granary", "Ration"), div(add(sub(value("object.Ledger", "Handed"), value("object.Ledger", "LastHanded")), 4), 8))), set("object.Ledger", "LastHanded", value("object.Ledger", "Handed")))
            meal_close:
                hook: end
                once: slot
                condition: tag("object.Clock", "SlotNoon", 1) 或 tag("object.Clock", "SlotEvening", 1)
                effect: seq(set("object.Stove", "Cooked", 0), set("object.Desk", "Open", 0), set("object.Granary", "Locked", 1))
            stash_meal:
                hook: end
                once: day
                each: characters
                condition: tag("object.Clock", "SlotEvening", 1) 且 (非 tag("$body", "Fed", 1)) 且 tag("$body", "Stash", 1)
                effect: seq(spend("$body", "Stash", 1), set("$body", "Fed", 1))
            injuries:
                hook: end
                once: day
                each: characters
                condition: tag("object.Clock", "SlotEvening", 1) 且 tag("$body", "InjuredDays", 1)
                effect: spend("$body", "InjuredDays", 1)
            greenhouse_crop:
                hook: end
                once: day
                condition: tag("object.Clock", "SlotEvening", 1) 且 tag("object.Valve", "Repaired", 1) 且 tag("object.Clock", "Day", add(value("object.Valve", "RepairedDay"), 1))
                effect: change("object.Granary", "Ration", value("object.Clock", "GreenhouseYield"))
            oil_base:
                hook: night
                once: day
                condition: true()
                effect: spend("object.OilStore", "Oil", min(1, value("object.OilStore", "Oil")))
            oil_reset:
                hook: dawn
                once: day
                each: characters
                condition: true()
                effect: set("$body", "OilAllocated", 0)
        autonomous:
            open_window:
                condition: (非 param("is_player")) 且 at("$actor", "room.Greenhouse") 且 tag("object.Gauge", "Pressure", 2) 且 param("ignores_pressure_warning") 且 (非 tag("object.Valve", "Open", 1))
                mode: Prefer
                weight: 6
                effect: wait(0.5, seq(require(at("$actor", "room.Greenhouse")), require(tag("object.Gauge", "Pressure", 2)), set("object.Valve", "Open", 1), set("$actor", "InjuredDays", 2), set("object.Valve", "GlassBroken", 1), set("object.Valve", "Work", max(0, sub(value("object.Valve", "Work"), 2)))))
            allocate_oil:
                condition: tag("$actor", "OilKey", 1) 且 (tag("object.Clock", "SlotEvening", 1) 或 tag("object.Clock", "SlotNight", 1)) 且 (非 tag("$actor", "OilAllocated", 1))
                mode: Prefer
                weight: 6
                effect: seq(travel("route.DormS"), require(tag("$actor", "OilKey", 1)), spend("object.OilStore", "Oil", min(value("object.OilStore", "Oil"), add(1, add(int_when(tag("person.Bertrand", "Trust", 2), 1, 0), add(int_when(tag("person.Mora", "Trust", 2), 1, 0), int_when(tag("person.Moran", "Sick", 1), 1, 0)))))), set("$actor", "OilAllocated", 1))
            observe_distrust:
                condition: tag("$actor", "IsWard", 1) 且 tag("object.Clock", "Day", 4) 且 (非 tag("object.Clock", "Day", 5)) 且 tag("object.Clock", "SlotEvening", 1) 且 (((非 tag("person.Quer", "Trust", -1)))) 且 at("person.Quer", "$scene") 且 (((非 tag("person.Quer", "Shamed", 1))))
                mode: Prefer
                weight: 6
                effect: seq(require(at("person.Quer", "$scene")), inspect("person.Quer"), wait(0.5, seq(require(at("person.Quer", "$scene")), set("person.Quer", "Shamed", 1))))
            escort_patient:
                condition: tag("$actor", "IsSelma", 1) 且 tag("person.Moran", "Sick", 1) 且 at("person.Moran", "$scene") 且 (((非 at("person.Moran", "room.Clinic"))))
                mode: Prefer
                weight: 6
                effect: wait(0.5, seq(require(tag("person.Moran", "Sick", 1)), transport("person.Moran", "route.Clinic")))
            inform_carer:
                condition: tag("$actor", "IsSelma", 1) 且 tag("person.Moran", "Sick", 1) 且 at("person.Mora", "$scene") 且 (((非 related("person.Mora", "ToldIllness", "person.Moran"))))
                mode: Prefer
                weight: 5
                effect: seq(tell("person.Mora", "莫兰在发烧。", tag("person.Moran", "Sick", 1)), think(related("person.Mora", "ToldIllness", "person.Moran")))
        stimuli:
            name_call:
                hook: tick
                source: object.NameVoice
                condition: tag("object.Clock", "Day", 5) 且 tag("object.Clock", "SlotNight", 1) 且 (非 param("is_player")) 且 at("$actor", "room.ArchiveFront") 且 (gte(value("$actor", "Hooks"), 1) 或 gte(value("$actor", "Whispered"), 1) 或 param("prior_action"))
                rank: add(add(value("$actor", "Hooks"), value("$actor", "Hooks")), value("$actor", "Desire"))
                trust: sub(add(add(value("$actor", "Hooks"), value("$actor", "Hooks")), value("$actor", "Desire")), 2)
                history_job: VisitArchive
                request: procedure("AnswerName")
                title: physical.name_call_title
                text: physical.name_call
                log: physical.name_call_log
        recipes:
            Greenhouse:
                stations: object.Valve、object.Gauge
                actor_tags: AtValve、AtGauge
                duration: 1.5
                knowledge: tag("$actor", "SkillStructure", 3) 或 param("knows_pressure_relief")
                condition: (非 tag("object.Valve", "Repaired", 1)) 且 ((非 tag("object.Valve", "Work", 3)) 或 param("first_competent") 或 param("second_competent"))
                effect: seq(require((非 tag("object.Valve", "Work", 6))), set("object.Valve", "Work", int_when((param("first_competent") 或 param("second_competent")), add(value("object.Valve", "Work"), add(max(1, add(value("$first", "SkillGreenhouse"), div(min(value("$first", "SkillStructure"), 2), 2))), max(1, add(value("$second", "SkillGreenhouse"), div(min(value("$second", "SkillStructure"), 2), 2))))), min(3, add(value("object.Valve", "Work"), add(max(1, add(value("$first", "SkillGreenhouse"), div(min(value("$first", "SkillStructure"), 2), 2))), max(1, add(value("$second", "SkillGreenhouse"), div(min(value("$second", "SkillStructure"), 2), 2)))))))), set("object.Gauge", "Pressure", 1), when(tag("object.Valve", "Work", 3), set("object.Gauge", "Pressure", 2)), when(tag("object.Valve", "Work", value("object.Clock", "GreenhouseNeed")), seq(set("object.Valve", "Repaired", 1), set("object.Valve", "RepairedDay", value("object.Clock", "Day")))))
        schedule:
            lessons:
                11:
                    day: 2
                    title: 授课.第十一课.题名
                    text: 授课.第十一课.内容
                12:
                    day: 3
                    title: 授课.第十二课.题名
                    text: 授课.第十二课.内容
                13:
                    day: 0
                    title: 授课.第十三课.题名
                    text: 授课.第十三课.内容
                14:
                    day: 4
                    title: 授课.第十四课.题名
                    text: 授课.第十四课.内容
            broadcasts:
                first: broadcast.first
                warning: broadcast.warning
                shortage: broadcast.shortage
                quiet: broadcast.quiet
                council: broadcast.council
                lesson: broadcast.lesson
    文本
        physical.greenhouse_warning: 压力表后面传来那个声音：“温室水压表指针过红线时，勿开顶窗。”就这一句。
        physical.greenhouse_repaired: 温室修好（{人们}）
        physical.name_call_title: 有人以你之名唤你
        physical.name_call: “{人}。”
        physical.name_call_log: 名录库前厅有声音唤 {人}
        physical.hungry: 你昨天没吃上晚饭。今天少一个能做事的时段（下午）。
        physical.illness: 莫兰发烧了。
        physical.recovery: 莫兰退烧了。
        broadcast.first: 诸位，请到讲堂来。现在讲守则的读法。
        broadcast.warning：另：入夜后，名录库中若有人以你之名唤你，勿应。
        broadcast.shortage: 诸位晨安。台账上，少了{数}份。昨夜，有人进过后勤院。
        broadcast.quiet: 诸位晨安。今日无新课。
        broadcast.council: 今日，诸位要决定名录库的事。我只旁听。
        broadcast.lesson：诸位晨安。今日上午讲堂有课：《{课}》。愿来的请来。
