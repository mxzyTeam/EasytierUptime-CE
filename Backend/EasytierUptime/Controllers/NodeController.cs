using EasytierUptime.Data;
using EasytierUptime.DTOs;
using EasytierUptime_Entities.Entities;
using EasytierUptime.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;
using System.Diagnostics;

namespace EasytierUptime.Controllers;

[ApiController]
[Route("api")]
public class NodeController : ControllerBase
{
    private static bool IsAdmin(ClaimsPrincipal u) => u.IsInRole("admin") || u.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "admin");
    private static string GetUserName(ClaimsPrincipal u) => u.Identity?.Name ?? u.FindFirstValue(ClaimTypes.Name) ?? u.FindFirstValue("name") ?? string.Empty;

    /// <summary>
    /// 公开：获取所有公开+已审批+启用的节点，附带最近一次健康状态
    /// </summary>
    // GET /api/public/nodes (anonymous)
    [HttpGet("public/nodes")]
    [AllowAnonymous]
    public async Task<IResult> GetPublicNodes([FromQuery] int page = 1, [FromQuery(Name = "per_page")] int perPage = 50,  [FromQuery(Name = "debug")] bool debug = false)
    {
        if (page < 1) page = 1;
        if (perPage <= 0 || perPage > 200) perPage = 50;

        var timings = debug ? new Dictionary<string, double>(8) : null;
        static void Record(Dictionary<string, double>? t, string key, Stopwatch sw)
        {
            if (t is null) return;
            sw.Stop();
            t[key] = sw.Elapsed.TotalMilliseconds;
        }

        var sw = Stopwatch.StartNew();
        var q = FreeSqlDb.Orm.Select<SharedNode>()
            .Where(x => x.IsPublic && x.IsApproved)
            .OrderByDescending(x => x.UpdatedAt);
        var total = (int)await q.CountAsync();
        Record(timings, "count_shared_nodes", sw);

        sw = Stopwatch.StartNew();
        var nodes = await q
            .Page(page, perPage)
            .ToListAsync(x => new SharedNode
            {
                Id = x.Id,
                Name = x.Name,
                Host = x.Host,
                Port = x.Port,
                Protocol = x.Protocol,
                Description = x.Description,
                IsActive = x.IsActive,
                CurrentConnections = x.CurrentConnections,
                MaxConnections = x.MaxConnections,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                Version = x.Version,
                AllowRelay = x.AllowRelay,
                IsApproved = x.IsApproved,
                IsPublic = x.IsPublic,
                Owner = x.Owner,
                LastCheckedAt = x.LastCheckedAt,
                LastResponseTimeUs = x.LastResponseTimeUs,
                CurrentHealthStatus = x.CurrentHealthStatus,
                CurrentConnectionCount = x.CurrentConnectionCount,
                HealthPercentage24h = x.HealthPercentage24h
            });
        Record(timings, "list_shared_nodes", sw);

        if (nodes.Count == 0)
        {
            var empty = new
            {
                success = true,
                data = new Dictionary<string, object>
                {
                    ["items"] = Array.Empty<object>(),
                    ["page"] = page,
                    ["per_page"] = perPage,
                    ["total_pages"] = 0,
                    ["total"] = "0"
                },
                error = (object?)null,
                latest_update = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                diagnostics = debug ? new
                {
                    timings = timings!,
                    slowest = timings!.OrderByDescending(kv => kv.Value).FirstOrDefault()
                } : null
            };
            return Results.Ok(empty);
        }

        var ids = nodes.Select(n => n.Id).ToArray();

        // 标签一次性查询（仅选择必要字段）
        sw = Stopwatch.StartNew();
        var tags = await FreeSqlDb.Orm.Select<NodeTag>()
            .Where(t => ids.Contains(t.NodeId))
            .ToListAsync(t => new NodeTag { NodeId = t.NodeId, Tag = t.Tag });
        Record(timings, "load_tags", sw);
        var tagMap = tags.GroupBy(t => t.NodeId).ToDictionary(g => g.Key, g => g.Select(x => x.Tag).ToArray());

        static string IsoUtc(DateTime dt) => dt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        var currentUser = (User?.Identity?.IsAuthenticated ?? false) ? GetUserName(User) : string.Empty;

        var list = new List<Dictionary<string, object?>>(nodes.Count);
        foreach (var n in nodes)
        {
            var max = n.MaxConnections;
            var cur = n.CurrentConnections;
            double perc = max > 0 ? (cur * 100.0 / max) : 0.0;
            perc = Math.Round(perc, 1, MidpointRounding.AwayFromZero);

            string loadLevel, loadText;
            if (!n.IsActive || max <= 0)
            {
                loadLevel = "none";
                loadText = "--";
            }
            else if (perc < 50)
            {
                loadLevel = "low";
                loadText = $"低负载({perc:F1}%)";
            }
            else if (perc < 100)
            {
                loadLevel = "medium";
                loadText = $"中负载({perc:F1}%)";
            }
            else
            {
                loadLevel = "high";
                loadText = $"高负载({perc:F1}%)";
            }
            //下面字段中很多都是兼容前端的字段
            var item = new Dictionary<string, object?>(32)
            {
                ["server_id"] = n.Id.ToString(CultureInfo.InvariantCulture),
                ["name"] = n.Name,
                ["host"] = n.Host,
                ["port"] = n.Port,
                ["protocol"] = n.Protocol,
                ["description"] = n.Description,
                ["is_active"] = n.IsActive ? 1 : 0,
                ["current_connections"] = n.CurrentConnections,
                ["max_connections"] = n.MaxConnections,
                ["usage_percentage"] = perc,
                ["load_level"] = loadLevel,
                ["load_text"] = loadText,
                ["tags"] = tagMap.TryGetValue(n.Id, out var arr) ? arr : Array.Empty<string>(),
                ["created_at"] = IsoUtc(n.CreatedAt),
                ["updated_at"] = IsoUtc(n.UpdatedAt),
                ["last_checked_at"] = n.LastCheckedAt,
                ["id"] = n.Id,
                ["version"] = n.Version,
                ["allow_relay"] = n.AllowRelay ? 1 : 0,
                ["is_approved"] = n.IsApproved ? 1 : 0,
                ["is_public"] = n.IsPublic ? 1 : 0,
                ["last_check_time"] = n.LastCheckedAt,
                ["last_response_time"] = n.LastResponseTimeUs,
                ["current_health_status"] = string.IsNullOrEmpty(n.CurrentHealthStatus) ? "Unknown" : n.CurrentHealthStatus,
                ["health_percentage_24h"] = n.HealthPercentage24h,
                ["health_record_total_counter_ring"] = Array.Empty<int>(),
                ["health_record_healthy_counter_ring"] = Array.Empty<int>(),
                ["is_owner"] = !string.IsNullOrEmpty(currentUser) && string.Equals(n.Owner, currentUser, StringComparison.Ordinal) ? 1 : 0
            };
            list.Add(item);
        }

        var totalPages = (int)Math.Ceiling(total / (double)perPage);
        var nowUtcIso = IsoUtc(DateTime.UtcNow);
        var data = new Dictionary<string, object>() {
            { "items", list },
            { "page", page },
            { "per_page", perPage },
            { "total_pages", totalPages },
            { "total", total.ToString(CultureInfo.InvariantCulture) }
        };
        var result = new
        {
            success = true,
            data = data,
            error = (object?)null,
            latest_update = nowUtcIso,
            diagnostics = debug ? new
            {
                timings = timings!,
                slowest = timings!.OrderByDescending(kv => kv.Value).FirstOrDefault()
            } : null
        };
        return Results.Ok(result);
    }

    // NEW: GET /api/public/nodes/{id} (anonymous) - single public node details
    [HttpGet("public/nodes/{id:int}")]
    [AllowAnonymous]
    public async Task<IResult> GetPublicNodeById([FromRoute] int id, [FromQuery(Name = "akhjlfds")] bool isSecretName = true)
    {
        var n = await FreeSqlDb.Orm.Select<SharedNode>()
            .Where(x => x.Id == id && x.IsPublic && x.IsApproved)
            .FirstAsync();
        if (n is null) return Results.NotFound();

        static string IsoUtc(DateTime dt) => dt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var currentUser = (User?.Identity?.IsAuthenticated ?? false) ? GetUserName(User) : string.Empty;

        var tags = await FreeSqlDb.Orm.Select<NodeTag>().Where(t => t.NodeId == id).ToListAsync();
        var tagArr = tags.Select(t => t.Tag).ToArray();

        var lastHealthRecord = await FreeSqlDb.Orm.Select<HealthRecord>()
            .Where(h => h.NodeId == id)
            .OrderByDescending(h => h.CheckedAt)
            .FirstAsync();

        var since24h = DateTime.UtcNow.AddHours(-24);
        var list = await FreeSqlDb.Orm.Select<HealthRecord>()
            .Where(h => h.NodeId == id && h.CheckedAt >= since24h)
            .ToListAsync();
        var total = list.Count;
        var healthy = list.Count(x => x.Status == "Healthy");
        var healthPercent24h = total == 0 ? 0 : Math.Round((double)healthy * 100.0 / total, 2, MidpointRounding.AwayFromZero);

        var max = n.MaxConnections;
        var cur = n.CurrentConnections;
        double perc = max > 0 ? (cur * 100.0 / max) : 0.0;
        perc = Math.Round(perc, 1, MidpointRounding.AwayFromZero);
        string loadLevel, loadText;
        if (!n.IsActive || max <= 0) { loadLevel = "none"; loadText = "--"; }
        else if (perc < 50) { loadLevel = "low"; loadText = $"低负载({perc:F1}%)"; }
        else if (perc < 100) { loadLevel = "medium"; loadText = $"中负载({perc:F1}%)"; }
        else { loadLevel = "high"; loadText = $"高负载({perc:F1}%)"; }

        var payload = new Dictionary<string, object?>
        {
            ["server_id"] = n.Id.ToString(CultureInfo.InvariantCulture),
            ["name"] = n.Name,
            ["host"] = isSecretName ? "****" : n.Host,
            ["port"] = n.Port,
            ["protocol"] = n.Protocol,
            ["description"] = n.Description,
            ["is_active"] = n.IsActive ? 1 : 0,
            ["current_connections"] = n.CurrentConnections,
            ["max_connections"] = n.MaxConnections,
            ["usage_percentage"] = perc,
            ["load_level"] = loadLevel,
            ["load_text"] = loadText,
            ["tags"] = tagArr,
            ["created_at"] = IsoUtc(n.CreatedAt),
            ["updated_at"] = IsoUtc(n.UpdatedAt),
            ["last_checked_at"] = lastHealthRecord?.CheckedAt,
            ["id"] = n.Id,
            ["version"] = n.Version,
            ["allow_relay"] = n.AllowRelay ? 1 : 0,
            ["is_approved"] = n.IsApproved ? 1 : 0,
            ["is_public"] = n.IsPublic ? 1 : 0,
            ["last_check_time"] = lastHealthRecord?.CheckedAt,
            ["last_response_time"] = lastHealthRecord?.ResponseTime ?? 0,
            ["current_health_status"] = lastHealthRecord?.Status ?? "Unknown",
            ["health_percentage_24h"] = healthPercent24h,
            ["is_owner"] = !string.IsNullOrEmpty(currentUser) && string.Equals(n.Owner, currentUser, StringComparison.Ordinal) ? 1 : 0
        };
        return Results.Ok(payload);
    }

    /// <summary>
    /// 获取节点列表
    /// - 管理员：返回全部节点
    /// - 普通用户：仅返回自己创建的节点（不再包含其他人的公开节点）
    /// </summary>
    // GET /api/nodes
    [HttpGet("nodes")]
    [Authorize]
    public async Task<IResult> GetNodes()
    {
        var user = User;
        if (IsAdmin(user))
        {
            var all = await FreeSqlDb.Orm.Select<SharedNode>().ToListAsync();
            return Results.Ok(all);
        }
        var name = GetUserName(user);
        var list = await FreeSqlDb.Orm.Select<SharedNode>()
            .Where(x => x.Owner == name)
            .ToListAsync();
        return Results.Ok(list);
    }

    /// <summary>
    /// 创建节点（归属当前登录用户），普通用户默认未审批，管理员创建自动审批
    /// </summary>
    // POST /api/nodes
    [HttpPost("nodes")]
    [Authorize]
    public async Task<IResult> CreateNode([FromBody] SharedNode input, [FromServices] NodeService svc)
    {
        var name = GetUserName(User);
        input.Owner = name;
        input.IsActive = true;
        input.IsApproved = IsAdmin(User);
        var saved = await svc.UpsertAsync(input);
        return Results.Ok(saved);
    }

    /// <summary>
    /// 删除节点（仅 owner 或 admin）
    /// </summary>
    // DELETE /api/nodes/{id}
    [HttpDelete("nodes/{id:int}")]
    [Authorize]
    public async Task<IResult> DeleteNode([FromRoute] int id)
    {
        var node = await FreeSqlDb.Orm.Select<SharedNode>().Where(x => x.Id == id).FirstAsync();
        if (node is null) return Results.NotFound();
        if (!IsAdmin(User) && !string.Equals(node.Owner, GetUserName(User), StringComparison.Ordinal))
            return Results.Forbid();
        var aff = await FreeSqlDb.Orm.Delete<SharedNode>(id).ExecuteAffrowsAsync();
        return aff > 0 ? Results.NoContent() : Results.NotFound();
    }

    /// <summary>
    /// 设置节点是否公开（仅 owner 或 admin）。
    /// 如果管理员将节点设为公开，会自动将其审批通过并启用，以便前台可见。
    /// </summary>
    // PUT /api/nodes/{id}/visibility
    [HttpPut("nodes/{id:int}/visibility")]
    [Authorize]
    public async Task<IResult> SetVisibility([FromRoute] int id, [FromBody] VisibilityDto dto)
    {
        var node = await FreeSqlDb.Orm.Select<SharedNode>().Where(x => x.Id == id).FirstAsync();
        if (node is null) return Results.NotFound();
        var isOwner = string.Equals(node.Owner, GetUserName(User), StringComparison.Ordinal);
        var isAdmin = IsAdmin(User);
        if (!isAdmin && !isOwner) return Results.Forbid();

        // Admin convenience: publishing also approves and activates to show on public list
        if (dto.IsPublic && isAdmin)
        {
            node.IsApproved = true;
            node.IsActive = true;
        }
        node.IsPublic = dto.IsPublic;
        node.UpdatedAt = DateTime.UtcNow;

        var rows = await FreeSqlDb.Orm.Update<SharedNode>(id)
            .Set(x => x.IsPublic, node.IsPublic)
            .Set(x => x.IsApproved, node.IsApproved)
            .Set(x => x.IsActive, node.IsActive)
            .Set(x => x.UpdatedAt, node.UpdatedAt)
            .ExecuteAffrowsAsync();
        if (rows == 0) return Results.NotFound();
        return Results.Ok(node);
    }

    /// <summary>
    /// 审批节点（仅管理员）
    /// </summary>
    // POST /api/nodes/{id}/approve (admin)
    [HttpPost("nodes/{id:int}/approve")]
    [Authorize(Roles = "admin")]
    public async Task<IResult> Approve([FromRoute] int id)
    {
        var rows = await FreeSqlDb.Orm.Update<SharedNode>(id).Set(x => x.IsApproved, true).ExecuteAffrowsAsync();
        if (rows == 0) return Results.NotFound();
        var node = await FreeSqlDb.Orm.Select<SharedNode>().Where(x => x.Id == id).FirstAsync();
        return Results.Ok(node);
    }

    /// <summary>
    /// 拒绝节点（仅管理员）
    /// </summary>
    // POST /api/nodes/{id}/reject (admin)
    [HttpPost("nodes/{id:int}/reject")]
    [Authorize(Roles = "admin")]
    public async Task<IResult> Reject([FromRoute] int id)
    {
        var rows = await FreeSqlDb.Orm.Update<SharedNode>(id).Set(x => x.IsApproved, false).ExecuteAffrowsAsync();
        if (rows == 0) return Results.NotFound();
        var node = await FreeSqlDb.Orm.Select<SharedNode>().Where(x => x.Id == id).FirstAsync();
        return Results.Ok(node);
    }

    /// <summary>
    /// 启用节点（owner 或管理员）
    /// </summary>
    // POST /api/nodes/{id}/activate
    [HttpPost("nodes/{id:int}/activate")]
    [Authorize]
    public async Task<IResult> Activate([FromRoute] int id)
    {
        var node = await FreeSqlDb.Orm.Select<SharedNode>().Where(x => x.Id == id).FirstAsync();
        if (node is null) return Results.NotFound();
        if (!IsAdmin(User) && node.Owner != GetUserName(User)) return Results.Forbid();
        var rows = await FreeSqlDb.Orm.Update<SharedNode>(id).Set(x => x.IsActive, true).ExecuteAffrowsAsync();
        if (rows == 0) return Results.NotFound();
        node.IsActive = true; return Results.Ok(node);
    }

    /// <summary>
    /// 停用节点（owner 或管理员）
    /// </summary>
    // POST /api/nodes/{id}/deactivate
    [HttpPost("nodes/{id:int}/deactivate")]
    [Authorize]
    public async Task<IResult> Deactivate([FromRoute] int id)
    {
        var node = await FreeSqlDb.Orm.Select<SharedNode>().Where(x => x.Id == id).FirstAsync();
        if (node is null) return Results.NotFound();
        if (!IsAdmin(User) && node.Owner != GetUserName(User)) return Results.Forbid();
        var rows = await FreeSqlDb.Orm.Update<SharedNode>(id).Set(x => x.IsActive, false).ExecuteAffrowsAsync();
        if (rows == 0) return Results.NotFound();
        node.IsActive = false; return Results.Ok(node);
    }

    /// <summary>
    /// 获取所有标签（公开）
    /// </summary>
    // GET /api/tags (public)
    [HttpGet("tags")]
    [AllowAnonymous]
    public async Task<IResult> GetTags()
    {
        var tags = await FreeSqlDb.Orm.Select<NodeTag>().ToListAsync();
        return Results.Ok(tags);
    }

    /// <summary>
    /// 测试连通性（公开）
    /// </summary>
    // POST /api/test_connection (public)
    [HttpPost("test_connection")]
    [AllowAnonymous]
    public async Task<IResult> TestConnection([FromBody] SharedNode node, [FromServices] ProbeService probeSvc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(node.Host) || node.Port <= 0)
            return Results.BadRequest(new { error = "host/port invalid" });
        var (status, rtUs, err, version, connCount) = await probeSvc.ProbeAndDispose(node, ct);
        return Results.Ok(new { status, response_time = rtUs, error_message = err, version, conn_count = connCount });
    }

    /// <summary>
    /// 获取节点最近健康记录（公开）
    /// </summary>
    // GET /api/nodes/{id}/health (public)
    [HttpGet("nodes/{id:int}/health")]
    [AllowAnonymous]
    public async Task<IResult> GetNodeHealth([FromRoute] int id)
    {
        var list = await FreeSqlDb.Orm.Select<HealthRecord>()
            .Where(x => x.NodeId == id && x.CheckedAt >= DateTime.UtcNow.AddDays(-1))
            .OrderByDescending(x => x.CheckedAt)
            .Limit(100)
            .ToListAsync(a=>new
            {
                a.Status,
                a.ConnectionCount,
                a.NodeId,
                a.CheckedAt,
                a.ErrorMessage,
                ResponseTime = a.ResponseTime / 1000
            });
        return Results.Ok(list);
    }

    /// <summary>
    /// 获取节点健康统计（公开，近24小时）
    /// </summary>
    // GET /api/nodes/{id}/health/stats (public)
    [HttpGet("nodes/{id:int}/health/stats")]
    [AllowAnonymous]
    public async Task<IResult> GetNodeHealthStats([FromRoute] int id)
    {
        var last24h = DateTime.UtcNow.AddDays(-1);
        var list = await FreeSqlDb.Orm.Select<HealthRecord>()
            .Where(x => x.NodeId == id && x.CheckedAt >= last24h)
            .ToListAsync();
        var total = list.Count;
        var healthy = list.Count(x => x.Status == "Healthy");
        var avg = list.Any() ? list.Average(x => x.ResponseTime) : 0;
        return Results.Ok(new
        {
            total_checks = total,
            healthy_count = healthy,
            unhealthy_count = total - healthy,
            health_percentage = total == 0 ? 0 : (double)healthy / total * 100.0,
            average_response_time = avg,
            uptime_percentage = total == 0 ? 0 : (double)healthy / total * 100.0
        });
    }

    /// <summary>
    /// 更新节点信息（仅管理员或所有者）
    /// </summary>
    // PUT /api/nodes/{id:int}
    [HttpPut("nodes/{id:int}")]
    [Authorize]
    public async Task<IResult> UpdateNode([FromRoute] int id, [FromBody] NodeUpdateRequest dto)
    {
        var node = await FreeSqlDb.Orm.Select<SharedNode>().Where(x => x.Id == id).FirstAsync();
        if (node is null) return Results.NotFound();

        var isOwner = string.Equals(node.Owner, GetUserName(User), StringComparison.Ordinal);
        var isAdmin = IsAdmin(User);
        if (!isOwner && !isAdmin) return Results.Forbid();

        // Apply editable fields
        node.Name = dto.Name.Trim();
        node.Protocol = dto.Protocol.Trim();
        node.Host = dto.Host.Trim();
        node.Port = dto.Port;
        node.NetworkName = dto.NetworkName.Trim();
        node.NetworkSecret = dto.NetworkSecret.Trim();
        node.MaxConnections = dto.MaxConnections;
        node.AllowRelay = dto.AllowRelay;
        node.Description = dto.Description?.Trim() ?? string.Empty;

        // Visibility logic reused: if admin sets public -> auto approve & activate
        if (dto.IsPublic != node.IsPublic)
        {
            node.IsPublic = dto.IsPublic;
            if (dto.IsPublic && isAdmin)
            {
                node.IsApproved = true;
                node.IsActive = true;
            }
        }

        node.UpdatedAt = DateTime.UtcNow;

        var aff = await FreeSqlDb.Orm.Update<SharedNode>(id)
            .Set(x => x.Name, node.Name)
            .Set(x => x.Protocol, node.Protocol)
            .Set(x => x.Host, node.Host)
            .Set(x => x.Port, node.Port)
            .Set(x => x.NetworkName, node.NetworkName)
            .Set(x => x.NetworkSecret, node.NetworkSecret)
            .Set(x => x.MaxConnections, node.MaxConnections)
            .Set(x => x.AllowRelay, node.AllowRelay)
            .Set(x => x.Description, node.Description)
            .Set(x => x.IsPublic, node.IsPublic)
            .Set(x => x.IsApproved, node.IsApproved)
            .Set(x => x.IsActive, node.IsActive)
            .Set(x => x.UpdatedAt, node.UpdatedAt)
            .ExecuteAffrowsAsync();
        if (aff == 0) return Results.NotFound();
        return Results.Ok(node);
    }
}
