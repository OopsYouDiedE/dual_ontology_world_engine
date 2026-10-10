续篇 chapter2

    规则有效期
        daily_habits：world
        access：chapter

    # 稳定 ID 声明权威边界；每条通路需两端所在边界均开放。
    # visible_from 只声明可当场复查的通路；远处的旧认知不随开关刷新。
    通行
        boundaries:
            north:
                kind: corridor
                initial: false
                rooms: world:room.Hall、world:room.Lecture、world:room.Oath
            east:
                kind: corridor
                initial: false
                rooms: world:room.Logistics、world:room.Clinic、world:room.Greenhouse
            archive:
                kind: corridor
                initial: false
                rooms: world:room.ArchiveFront、world:room.Resonance
            DormLife:
                kind: dorm
                initial: true
                rooms: world:room.DormLife
            DormW:
                kind: dorm
                initial: true
                rooms: world:room.DormW
            DormS:
                kind: dorm
                initial: false
                rooms: world:room.DormS
            DormL:
                kind: dorm
                initial: false
                rooms: world:room.DormL
        routes:
            route.Hall:
                name: 往中央厅
                to: world:room.Hall
                from: all
                visible_from: world:room.Living、world:room.Hall、world:room.Lecture、world:room.Oath
            route.Lecture:
                name: 往讲堂
                to: world:room.Lecture
                from: all
                visible_from: world:room.Hall、world:room.Lecture
            route.Logistics:
                name: 往后勤院
                to: world:room.Logistics
                from: all
                visible_from: world:room.Living、world:room.Logistics、world:room.Clinic、world:room.Greenhouse
            route.Clinic:
                name: 往诊疗所
                to: world:room.Clinic
                from: all
                visible_from: world:room.Logistics、world:room.Clinic
            route.Greenhouse:
                name: 往温室
                to: world:room.Greenhouse
                from: all
                visible_from: world:room.Logistics、world:room.Greenhouse
            route.ArchiveFront:
                name: 往名录库前厅
                to: world:room.ArchiveFront
                from: all
                visible_from: world:room.Living、world:room.Hall、world:room.ArchiveFront、world:room.Resonance
            route.Oath:
                name: 往誓约庭
                to: world:room.Oath
                from: all
                visible_from: world:room.Hall、world:room.Oath
            route.DormLife:
                name: 往乙区
                to: world:room.DormLife
                from: all
                visible_from: world:room.DormLife、world:room.Living
            route.DormW:
                name: 往甲区
                to: world:room.DormW
                from: all
                visible_from: world:room.DormW、world:room.Living
            route.DormS:
                name: 往丙区
                to: world:room.DormS
                from: all
                visible_from: world:room.DormS、world:room.Living
            route.DormL:
                name: 往丁区
                to: world:room.DormL
                from: all
                visible_from: world:room.DormL、world:room.Living
            route.Resonance:
                name: 往共鸣厅
                to: world:room.Resonance
                from: all
                visible_from: world:room.ArchiveFront、world:room.Resonance
            route.Living:
                name: 往起居厅
                to: world:room.Living
                from: all
                visible_from: world:room.Living、world:room.DormLife、world:room.DormW、world:room.DormS、world:room.DormL
        arrivals:
            person.Det: world:room.Living
            person.Huo: world:room.Living
            person.Shen: world:room.Living
            person.Su: world:room.Living
            person.Lucian: world:room.Living
            person.Gu: world:room.Living
            person.Quer: world:room.Living
            person.Ward: world:room.Living
            person.Selma: world:room.DormS
            person.Ruan: world:room.DormS
            person.Elena: world:room.DormS
            person.Moran: world:room.DormS
            person.Veronica: world:room.DormL
            person.Bertrand: world:room.DormL
            person.Margaret: world:room.DormL
            person.Mora: world:room.DormL
        summons:
            bing:
                sender: world:person.Det
                people: world:person.Selma、world:person.Ruan、world:person.Elena、world:person.Moran
                to: world:room.Living
                text: 丙区的各位，外闩已抬起。做完手头的事，请到起居厅来。
                compatibility: 8
            ding:
                sender: world:person.Det
                people: world:person.Veronica、world:person.Bertrand、world:person.Margaret、world:person.Mora
                to: world:room.Living
                text: 丁区的各位，外闩已抬起。做完手头的事，请到起居厅来。
                compatibility: 8
            gather:
                sender: world:person.Det
                people: world:person.Huo、world:person.Shen、world:person.Su、world:person.Lucian、world:person.Gu、world:person.Quer、world:person.Ward、world:person.Selma、world:person.Ruan、world:person.Elena、world:person.Moran、world:person.Veronica、world:person.Bertrand、world:person.Margaret、world:person.Mora
                to: world:room.Hall
                text: 北廊已开。请到中央厅集合，随后去讲堂。
                compatibility: 8
