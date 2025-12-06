using EasytierUptime_Entities.Entities;
using System.Diagnostics;

namespace EasytierUptime.Services;

/// <summary>
/// 负责定期执行探测任务并写入健康记录的服务。
/// </summary>
public class ProbeService
{
    /// <summary>
    /// 执行一次探测，返回状态、响应时间、错误信息、版本、连接数。
    /// </summary>
    public Task<(string status, long responseTimeUs, string? error, string version, int connCount)> ProbeAsync(SharedNode node, CancellationToken ct) => Task.FromResult(ProbeNative(node));

    private static long ElapsedUs(long startTicks, long endTicks)
        => (long)((endTicks - startTicks) * 1_000_000L / Stopwatch.Frequency);

    private (string, long, string?, string, int) ProbeNative(SharedNode node)
    {
        // 使用原生探测，解析版本、连接数和延迟（微秒）
        var (ok, version, conn, latencyUs, err) = EasyTierNative.Probe(node, TimeSpan.FromSeconds(2));

        string status = ok ? "Healthy" : "Unhealthy";
        string? error = ok ? null : err;
        var us = latencyUs > 0 ? latencyUs : 0;
        return (status, us, error, version ?? string.Empty, conn);
    }

}
