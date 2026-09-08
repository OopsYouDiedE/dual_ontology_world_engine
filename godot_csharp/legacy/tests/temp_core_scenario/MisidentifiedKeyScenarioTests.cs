using DualOntologyWorldEngine.TempCore;

namespace DualOntologyWorldEngine.TempCore.ScenarioTests;

public static class Program
{
    public static int Main()
    {
        Console.WriteLine("=== 开始执行测试场景：被误认的钥匙 ===");

        // ==========================================
        // 1. 构建客观真实本体（Essence 层）
        // ==========================================
        var hall = new SpaceEssence("Hall");
        var archive = new SpaceEssence("Archive");

        var door = new BoundaryEssence("LockedDoor");
        door.Connections[hall] = archive;
        door.Connections[archive] = hall;
        door.Tags[LockTag.Locked] = 1.0;
        door.Tags[LockTag.LockLevel] = 3.0;

        var lockProperty = new LockProperty();
        door.AddProperty(lockProperty);

        var archiveKeyIdentity = new ConceptEssence("ArchiveKeyIdentity");
        var ordinaryKeyIdentity = new ConceptEssence("OrdinaryKeyIdentity");
        var confidentialRecordIdentity = new ConceptEssence("ConfidentialRecord");
        var trashIdentity = new ConceptEssence("Trash");

        door.Relations.Add((RelationNames.AcceptsKey, archiveKeyIdentity));

        var keyA = new PhysicalEssence("KeyA");
        keyA.Relations.Add((RelationNames.HasIdentity, archiveKeyIdentity));
        var surfacePropA = new SurfaceProperty { Symbol = "ArchiveSymbol" };
        surfacePropA.SurfaceTags[SurfaceTag.ArchiveSymbol] = 1.0;
        keyA.AddProperty(surfacePropA);

        var keyB = new PhysicalEssence("KeyB");
        keyB.Relations.Add((RelationNames.HasIdentity, ordinaryKeyIdentity));

        var document = new PhysicalEssence("Document");
        document.Relations.Add((RelationNames.IsA, confidentialRecordIdentity));

        var aliceBody = new BodyEssence("AliceBody");
        var aliceSpatial = new SpatialProperty { Space = hall };
        aliceBody.AddProperty(aliceSpatial);

        var aliceSoul = new SoulEssence("AliceSoul");
        aliceSoul.Tags[MindTag.Sanity] = 100.0;
        aliceSoul.Tags[MindTag.Corruption] = 0.0;
        aliceSoul.Relations.Add((RelationNames.Controls, aliceBody));

        var confusionField = new AreaEssence("ConfusionField");
        confusionField.Relations.Add((RelationNames.AttachedTo, archive));
        var corruptionProp = new CorruptionProperty { Intensity = 1.0 };
        confusionField.AddProperty(corruptionProp);

        Console.WriteLine("[1/6] 真实 Essence 本体与关系构建完成。");

        // ==========================================
        // 2. 构建现实投影世界与引擎绑定映射
        // ==========================================
        var realityWorld = new ProjectionWorld();
        var binding = new ProjectionBinding();

        var realityHall = new SpaceProjection("Hall", hall);
        var realityArchive = new SpaceProjection("Archive", archive);
        var realityDoor = new BoundaryProjection("LockedDoor", door);
        realityDoor.Tags[LockTag.Locked] = 1.0;

        var realityKeyA = new PhysicalProjection("KeyA", keyA);
        realityKeyA.Tags[SurfaceTag.ArchiveSymbol] = 1.0; // 真实物体的表面特征反映于现实投影

        var realityKeyB = new PhysicalProjection("KeyB", keyB);
        var realityDoc = new PhysicalProjection("Document", document);
        var realityAliceBody = new BodyProjection("AliceBody", aliceBody);
        var realityConfusionField = new AreaProjection("ConfusionField", confusionField);

        realityWorld.Add(realityHall);
        realityWorld.Add(realityArchive);
        realityWorld.Add(realityDoor);
        realityWorld.Add(realityKeyA);
        realityWorld.Add(realityKeyB);
        realityWorld.Add(realityDoc);
        realityWorld.Add(realityAliceBody);
        realityWorld.Add(realityConfusionField);

        binding.Bind(realityHall, hall, realityHall);
        binding.Bind(realityArchive, archive, realityArchive);
        binding.Bind(realityDoor, door, realityDoor);
        binding.Bind(realityKeyA, keyA, realityKeyA);
        binding.Bind(realityKeyB, keyB, realityKeyB);
        binding.Bind(realityDoc, document, realityDoc);
        binding.Bind(realityAliceBody, aliceBody, realityAliceBody);

        Console.WriteLine("[2/6] 现实 ProjectionWorld 与 ProjectionBinding 构建完成。");

        // ==========================================
        // 3. 构建 Alice 主观世界（AliceSoul.World）
        // 注入初始错误信念：Alice 误信 KeyB 才是档案室钥匙
        // ==========================================
        var mentalArchiveKey = new ConceptProjection("ArchiveKeyIdentity", source: null);
        var mentalOrdinaryKey = new ConceptProjection("OrdinaryKeyIdentity", source: null);
        var mentalConfidentialDoc = new ConceptProjection("ConfidentialRecord", source: null);
        var mentalTrash = new ConceptProjection("Trash", source: null);

        var mentalDoor = new BoundaryProjection("LockedDoor", source: null);
        mentalDoor.Tags[LockTag.Locked] = 1.0;

        var mentalKeyA = new PhysicalProjection("KeyA", source: null);
        mentalKeyA.Relations.Add((RelationNames.HasIdentity, mentalOrdinaryKey)); // 误认为普通钥匙

        var mentalKeyB = new PhysicalProjection("KeyB", source: null);
        mentalKeyB.Relations.Add((RelationNames.HasIdentity, mentalArchiveKey)); // 误认为档案室钥匙

        var mentalDoc = new PhysicalProjection("Document", source: null);
        mentalDoc.Relations.Add((RelationNames.IsA, mentalConfidentialDoc));

        var mentalAliceBody = new BodyProjection("AliceBody", source: null);

        aliceSoul.World.Add(mentalArchiveKey);
        aliceSoul.World.Add(mentalOrdinaryKey);
        aliceSoul.World.Add(mentalConfidentialDoc);
        aliceSoul.World.Add(mentalTrash);
        aliceSoul.World.Add(mentalDoor);
        aliceSoul.World.Add(mentalKeyA);
        aliceSoul.World.Add(mentalKeyB);
        aliceSoul.World.Add(mentalDoc);
        aliceSoul.World.Add(mentalAliceBody);

        // 注册主观投影到现实对象的 Binding
        binding.BindMental(aliceSoul, mentalDoor, door);
        binding.BindMental(aliceSoul, mentalKeyA, keyA);
        binding.BindMental(aliceSoul, mentalKeyB, keyB);
        binding.BindMental(aliceSoul, mentalDoc, document);
        binding.BindMental(aliceSoul, mentalAliceBody, aliceBody);

        Console.WriteLine("[3/6] Alice 主观世界构建完成（初始认知偏差：KeyB 具有 ArchiveKeyIdentity）。");

        // ==========================================
        // 4. 自主决策第 1 轮：依据主观世界检索并尝试开锁
        // ==========================================
        var chosenKey1 = DecideKeyToUnlock(aliceSoul.World, mentalArchiveKey);
        Assert(chosenKey1 == mentalKeyB, "Alice 应根据自身认知自主选择 KeyB。");
        Console.WriteLine($"[决策 1] Alice 自主从脑内世界查询钥匙，选择尝试使用: {chosenKey1!.Name}");

        var unlockIntent1 = new UnlockIntent(mentalAliceBody, mentalDoor, chosenKey1);
        var event1 = ActionRuntime.Execute(unlockIntent1, binding);
        Assert(!event1.Success, "现实开锁应判定失败（KeyB 真实身份并非档案室钥匙）。");
        Assert(door.Tags[LockTag.Locked] > 0, "真实 Door 仍处于锁定状态。");
        Assert(realityDoor.Tags[LockTag.Locked] > 0, "现实 DoorProjection 仍处于锁定状态。");
        Console.WriteLine($"[结算 1] 真实物理结算：开锁失败（事件: {event1.TrueName} - {event1.Details}）。");

        // ==========================================
        // 5. 观察 KeyA 并发生主观认知更新
        // ==========================================
        Console.WriteLine("[观察] Alice 对走廊中的 KeyA 执行观察...");
        var obsEvent = realityKeyA.ObserveBy(aliceSoul, realityHall, binding);
        Assert(obsEvent != null, "观察行为生成事件。");
        Assert(mentalKeyA.Tags.ContainsKey(SurfaceTag.ArchiveSymbol), "主观认知中 KeyA 摄入了表面刻痕标签。");

        // Alice 的主观思维推论：识别到刻痕符号 -> 认定该钥匙具有档案室钥匙语义身份
        if (mentalKeyA.Tags.TryGetValue(SurfaceTag.ArchiveSymbol, out var symbolVal) && symbolVal > 0)
        {
            mentalKeyA.Relations.RemoveWhere(r => r.Relation == StandardRelations.HasIdentity);
            aliceSoul.World.AddRelation(mentalKeyA, StandardRelations.HasIdentity, mentalArchiveKey);
        }

        // 验证主观世界的 KeyA 认知关系是否被更新
        var keyARelations = mentalKeyA.Relations.ToList();
        var hasArchiveKeyRel = keyARelations.Any(r => r.Relation == StandardRelations.HasIdentity && ReferenceEquals(r.Target, mentalArchiveKey));
        Assert(hasArchiveKeyRel, "Alice 观察到刻痕并推论后，脑内 KeyA 语义身份应更新为 ArchiveKeyIdentity。");
        Console.WriteLine($"[认知更新] Alice 主观世界已更新：KeyA 现具有 ArchiveKeyIdentity 身份。");

        // ==========================================
        // 6. 自主决策第 2 轮：依据更新后的认知再次开锁
        // ==========================================
        var chosenKey2 = DecideKeyToUnlock(aliceSoul.World, mentalArchiveKey);
        Assert(chosenKey2 == mentalKeyA, "Alice 认知更新后，应自主改选 KeyA 开锁。");
        Console.WriteLine($"[决策 2] Alice 再次自主查询钥匙，本次选择使用: {chosenKey2!.Name}");

        var unlockIntent2 = new UnlockIntent(mentalAliceBody, mentalDoor, chosenKey2);
        var event2 = ActionRuntime.Execute(unlockIntent2, binding);
        Assert(event2.Success, "现实开锁应成功（KeyA 真实身份匹配）。");
        Assert(door.Tags[LockTag.Locked] == 0, "真实 Door 锁定状态清零。");

        // 通过 RealitySynchronizer 同步现实表现层
        RealitySynchronizer.Sync(realityDoor, door);
        Assert(realityDoor.Tags[LockTag.Locked] == 0, "现实 DoorProjection 锁定状态经同步后清零。");
        Console.WriteLine($"[结算 2] 真实物理结算：开锁成功（事件: {event2.TrueName} - {event2.Details}）。");

        // ==========================================
        // 7. 空间通行：穿过门进入 Archive
        // ==========================================
        var travelIntent = new TravelIntent(mentalAliceBody, mentalDoor);
        var travelEvent = ActionRuntime.Execute(travelIntent, binding, hall);
        Assert(travelEvent.Success, "Alice 应成功穿过门。");
        Assert(aliceSpatial.Space == archive, "Alice 身体当前空间已转移至 Archive。");
        Console.WriteLine($"[通行] Alice 成功穿越边界门，当前空间: {aliceSpatial.Space.TrueName}。");

        // ==========================================
        // 8. 档案室异常场侵蚀：扭曲主观认知
        // ==========================================
        Console.WriteLine("[异常侵蚀] 档案室中的 ConfusionField 触发认知侵蚀...");
        corruptionProp.Corrupt(aliceSoul, new CorruptionTransform(mentalTrash), realityArchive);

        // 决策验证：Alice 试图在档案室查找机密文件
        var targetDoc = aliceSoul.World
            .FindByRelation(RelationNames.IsA, mentalConfidentialDoc)
            .FirstOrDefault();

        Assert(targetDoc == null, "受认知侵蚀影响，Alice 脑内不应再存在被识别为 ConfidentialRecord 的对象。");

        var recognizedAsTrash = aliceSoul.World
            .FindByRelation(RelationNames.IsA, mentalTrash)
            .FirstOrDefault();
        Assert(recognizedAsTrash != null, "Alice 脑内的 Document 已被误认为 Trash。");
        Console.WriteLine($"[主观迷失] Alice 无法识别出机密文件（眼前的 Document 被其视作: {recognizedAsTrash!.Relations.First().Target.Name}）。");

        // 真实性校验：现实世界中的 Document 未受任何破坏
        var realDocIdentity = document.Relations.First(r => r.Relation == RelationNames.IsA).Target;
        Assert(realDocIdentity == confidentialRecordIdentity, "现实中的 Document 真实身份依然完好且为 ConfidentialRecord。");
        Console.WriteLine($"[现实核验] 客观现实不受主观扭曲影响：Document 真实身份依然是 {realDocIdentity.TrueName}。");

        Console.WriteLine("\n=== 测试全部通过！完整跑通物理事实、主观世界、语义匹配、空间通行、Property 与异常认知闭环 ===");
        return 0;
    }

    /// <summary>
    /// 自主决策推导：在主观世界中寻找被认为具有指定语义身份的钥匙投影。
    /// 严禁测试代演，必须由主观世界当前状态自动产出。
    /// </summary>
    private static PhysicalProjection? DecideKeyToUnlock(ProjectionWorld soulWorld, ConceptProjection targetIdentity)
    {
        return soulWorld.FindByRelation(RelationNames.HasIdentity, targetIdentity)
            .OfType<PhysicalProjection>()
            .FirstOrDefault();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[断言失败] {message}");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }
}
