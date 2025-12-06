# EasyTier Uptime

一个使用 .NET 9 开发的服务与探测程序，用于监控 EasyTier 节点的在线状态、聚合统计并提供 API 接口与后台任务调度。

## 重要说明（easytier_ffi 依赖）
- 由于 GitHub 平台限制，`easytier_ffi` 相关二进制/库文件无法随源代码一并提交。
- 使用本项目前，请在本地自行编译并提供 `easytier_ffi` 所需的产物（如 `.dll`/`.so`/`.dylib`）。
- 将编译生成的库文件放置到本项目的`debug/net9.0`目录下。

本地编译示例流程：
1. 获取 `easytier_ffi` 源码（参考上游或官方仓库）。
2. 按其文档在目标平台编译生成对应库文件。
3. 将库文件复制到运行目录下。

## 功能概览
- 定时调度与结果聚合
- 基于 JWT 的身份认证与授权
- 邮件通知（SMTP）
- 数据持久化（支持 MySql/Sqlite，使用 FreeSql）
- REST API（节点管理、用户管理、认证）

## 代码结构
- `EasytierUptime`：Web API 项目
  - `Controllers`：`AuthController`、`UserController`、`NodeController`
  - `appsettings.json`（本地私密配置，已在 `.gitignore` 中忽略）
  - `appsettings.example.json`（示例配置，安全可提交）
- `EasytierUptime_Detection`：探测与调度服务（控制台）
  - `Services/ProbeService.cs`：探测逻辑
  - `Services/Scheduler.cs`：任务调度
  - `Services/AggregationService.cs`：结果聚合
  - `Services/EasyTierNative.cs`：与 EasyTier FFI 交互
  - `appsettings.json`（本地私密配置，已在 `.gitignore` 中忽略）
  - `appsettings.example.json`（示例配置，安全可提交）
- `EasytierUptime_Entities`：共享实体与数据库模型
  - `Entities/SharedNode.cs` 等

## 配置说明
- 请勿提交含敏感信息的 `appsettings.json`（已通过 `.gitignore` 忽略）。
- 提交 `appsettings.example.json` 并根据需要填充占位符：
  - `Jwt:Key`：设置安全的密钥（建议使用环境变量或 Secret Manager）
  - `Smtp:*`：邮件服务器配置
  - `Database:Provider`：`MySql` 或 `Sqlite`
  - `Database:ConnectionString`：对应数据库连接串

## 构建与运行
- 要求：.NET 9 SDK
- 构建解决方案：`dotnet build`
- 运行 Web API：在 `EasytierUptime` 目录执行 `dotnet run`
- 运行探测服务：在 `EasytierUptime_Detection` 目录执行 `dotnet run`

## 数据库
- 默认提供 FreeSql 以及 Sqlite/MySql Provider：
  - Sqlite 适合本地开发与轻量化部署
  - MySql 适合正式环境
- 如使用 Sqlite，可将连接串留空并让程序在 `./data/detection.db` 自动创建（视实现而定）。


