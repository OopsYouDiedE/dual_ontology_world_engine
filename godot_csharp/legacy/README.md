# 历史实现

本目录完整保留迁移前的实现，供后续动物园语义迁移对照。

- `core`：旧 Entity / Belief 核心，9,917 行 C#。现有动物园项目及旧审计项目仍引用此版本。
- `temp_core`：最初的 Essence–Projection 实验核心。
- `tests/temp_core_scenario`：实验核心原有的钥匙场景。

正式入口已经切换到 `../core`。新核心验收见 `../tests/core_acceptance`。
历史测试通过只证明历史基线仍可运行，不证明新核心的能力。
动物园迁移完成前保留此目录，不把其体积算作已经删除的代码。
