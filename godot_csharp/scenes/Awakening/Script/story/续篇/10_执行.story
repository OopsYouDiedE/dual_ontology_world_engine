续篇 chapter2

    执行
        procedures:
            NightTakeSelf: wait(2, seq(travel("route.Logistics"), when(tag("object.Granary", "Locked", 1), seq(when(tag("$actor", "GranaryKey", 1), set("object.Granary", "Locked", 0)), when(tag("$actor", "Lockpick", 1), set("object.Granary", "Locked", 0)), when(tag("object.Granary", "LatchLoose", 1), set("object.Granary", "Locked", 0)))), require((非 tag("object.Granary", "Locked", 1))), spend("object.Granary", "Ration", 1), change("$actor", "OweNote", 1), set("object.Granary", "Locked", 1), wait(3, seq(change("$actor", "Fed", 1), change("$actor", "NightAte", 1)))))
            NightTakeSelfRec: wait(2, seq(travel("route.Logistics"), when(tag("object.Granary", "Locked", 1), seq(when(tag("$actor", "GranaryKey", 1), set("object.Granary", "Locked", 0)), when(tag("$actor", "Lockpick", 1), set("object.Granary", "Locked", 0)), when(tag("object.Granary", "LatchLoose", 1), set("object.Granary", "Locked", 0)))), require((非 tag("object.Granary", "Locked", 1))), spend("object.Granary", "Ration", 1), change("object.Ledger", "NightRecorded", 1), set("object.Granary", "Locked", 1), wait(3, seq(change("$actor", "Fed", 1), change("$actor", "NightAte", 1)))))
            NightTakeSick: wait(2, seq(travel("route.Logistics"), when(tag("object.Granary", "Locked", 1), seq(when(tag("$actor", "GranaryKey", 1), set("object.Granary", "Locked", 0)), when(tag("$actor", "Lockpick", 1), set("object.Granary", "Locked", 0)), when(tag("object.Granary", "LatchLoose", 1), set("object.Granary", "Locked", 0)))), require((非 tag("object.Granary", "Locked", 1))), spend("object.Granary", "Ration", 1), change("$actor", "OweNote", 1), set("object.Granary", "Locked", 1), wait(1.5, seq(travel("route.Clinic"), change("object.Bowl", "Porridge", 1)))))
            NightTakeSickRec: wait(2, seq(travel("route.Logistics"), when(tag("object.Granary", "Locked", 1), seq(when(tag("$actor", "GranaryKey", 1), set("object.Granary", "Locked", 0)), when(tag("$actor", "Lockpick", 1), set("object.Granary", "Locked", 0)), when(tag("object.Granary", "LatchLoose", 1), set("object.Granary", "Locked", 0)))), require((非 tag("object.Granary", "Locked", 1))), spend("object.Granary", "Ration", 1), change("object.Ledger", "NightRecorded", 1), set("object.Granary", "Locked", 1), wait(1.5, seq(travel("route.Clinic"), change("object.Bowl", "Porridge", 1)))))
            Inform: seq(travel("route.Logistics"), inspect("object.Ledger"), change("object.Ledger", "NightRecorded", 1), change("$actor", "OweNote", -1))
            Collect: seq(travel("route.Logistics"), wait_until(tag("object.Desk", "Open", 1), seq(require(tag("object.Granary", "Ration", 1)), spend("object.Granary", "Ration", 1), change("$actor", "Fed", 1), change("object.Ledger", "Handed", 1))))
            CountLedger: seq(travel("route.Logistics"), inspect("object.Granary"), inspect("object.Ledger"), change("object.Ledger", "Counted", 1))
            AnswerName: seq(inspect("object.ArchiveDoor"), change("$actor", "Answered", 1))
            Accuse: seq(travel("route.Hall"), change("Board.Hall", "Accusation", 1))
            Defend: seq(travel("route.Hall"), inspect("Board.Hall"))
            GiveKey: seq(require(tag("$actor", "GranaryKey", 1)), change("$actor", "GranaryKey", -1), change("person.Shen", "GranaryKey", 1))
            SeizePattern: change("object.ArchiveDoor", "Work", 1)
            RepairLatch: seq(travel("route.Logistics"), require(tag("object.Granary", "LatchLoose", 1)), set("object.Granary", "LatchLoose", 0), set("object.Granary", "ToolMarks", 1))
            Care: seq(travel("route.Clinic"), inspect("object.Bed"), when(tag("$actor", "MedKey", 1), seq(require(tag("object.MedCab", "Medicine", 1)), spend("object.MedCab", "Medicine", 1))), change("object.Bed", "Treated", 1))
            GuardArchive: seq(travel("route.ArchiveFront"), inspect("object.ArchiveDoor"), wait(0.3, seq(require((非 tag("object.WeightRoom", "Occupied", 1))), set("object.WeightRoom", "Occupied", 1), set("$actor", "Guarding", 1), wait(8, seq(inspect("object.ArchiveDoor"), set("object.WeightRoom", "Occupied", 0), set("$actor", "Guarding", 0))))))
            FeedWeak: seq(travel("route.Logistics"), wait_until(tag("object.Desk", "Open", 1), seq(require(tag("object.Granary", "Ration", 1)), spend("object.Granary", "Ration", 1), change("object.Ledger", "Handed", 1), change("$target", "Fed", 1), change("$actor", "DidFeedWeak", 1))))
            Work: seq(travel("$work_route"), inspect("$work_room"), wait(2.5, inspect("$work_room")))
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
                effect: seq(travel("route.Greenhouse"), inspect("object.Gauge"), change("object.Valve", "Work", 1))
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
