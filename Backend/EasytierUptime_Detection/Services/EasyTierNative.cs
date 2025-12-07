using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Text.RegularExpressions;
using EasytierUptime_Entities.Entities;

namespace EasytierUptime.Services;

internal static class EasyTierNative
{
    #if WINDOWS
        private const string DllName = "easytier_ffi.dll"; // Windows PE DLL
    #elif LINUX
        private const string DllName = "libeasytier_ffi.so"; // Linux shared object
    #elif OSX
        private const string DllName = "libeasytier_ffi.dylib"; // macOS dynamic library
    #else
        private const string DllName = "easytier_ffi"; // Fallback
    #endif

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeKv { public IntPtr Key; public IntPtr Value; }

    // FFI 接口：解析配置字符串（TOML）。返回 <0 表示错误，错误信息由 get_error_msg 提供。
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "parse_config")]
    private static extern int parse_config([MarshalAs(UnmanagedType.LPStr)] string cfgStr);

    // FFI 接口：运行一个网络实例。返回实例句柄或代号（由 FFI 定义）。返回 <0 表示错误。
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "run_network_instance")]
    private static extern int run_network_instance([MarshalAs(UnmanagedType.LPStr)] string cfgStr);

    // FFI 接口：保留/释放网络实例（此处未使用）。
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "retain_network_instance")]
    private static extern int retain_network_instance(IntPtr instNames, int length);

    // FFI 接口：收集当前网络信息，返回键值对（key -> string/JSON）。
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "collect_network_infos")]
    private static extern int collect_network_infos(IntPtr infos, int maxLength);

    // FFI 接口：获取最近一次错误消息（FFI 内部维护）。
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_error_msg")]
    private static extern void get_error_msg(out IntPtr errorMsg);

    // FFI 接口：释放由 FFI 分配的字符串内存，防止泄漏。
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "free_string")]
    private static extern void free_string(IntPtr str);

    // 从 FFI 取出错误消息的安全封装。
    private static string GetErrorMessage()
    {
        get_error_msg(out var ptr);
        if (ptr == IntPtr.Zero) return "Unknown error";
        try { return Marshal.PtrToStringAnsi(ptr) ?? "Unknown error"; }
        finally { free_string(ptr); }
    }

    // 调用 FFI 把所有键值对抓取到托管 Dictionary。
    private static Dictionary<string, string> CollectAll(int max = 1024)
    {
        var size = Marshal.SizeOf<NativeKv>();
        var buffer = Marshal.AllocHGlobal(size * max);
        try
        {
            var count = collect_network_infos(buffer, max);
            if (count < 0) throw new InvalidOperationException(GetErrorMessage());
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
            {
                // 将非托管结构体读入托管结构体。
                var kv = Marshal.PtrToStructure<NativeKv>(buffer + i * size);
                var key = Marshal.PtrToStringAnsi(kv.Key) ?? string.Empty;
                var val = Marshal.PtrToStringAnsi(kv.Value) ?? string.Empty;
                // FFI 分配的字符串需要释放，避免内存泄漏。
                free_string(kv.Key);
                free_string(kv.Value);
                if (!string.IsNullOrEmpty(key)) result[key] = val;
            }
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // 构造 EasyTier 的 TOML 配置文本。
    // 含：实例名、实例 ID、网络标识（名称/密钥）、对端 URI、标志位（禁用 TUN/P2P/打洞）。
    private static (string toml,string instancename) BuildTomlConfig(SharedNode node, Guid? id = null)
    {
        var guid = (id ?? Guid.NewGuid()).ToString();
        var instanceName = string.IsNullOrWhiteSpace(node.Name) ? $"healthcheck-{guid}" : node.Name;
        instanceName = instanceName + node.Id;
        var sb = new StringBuilder();
        sb.AppendLine($"instance_name = \"{instanceName}\"");
        sb.AppendLine($"instance_id = \"{guid}\"");
        sb.AppendLine("[network_identity]");
        sb.AppendLine($"network_name = \"{node.NetworkName}\"\nnetwork_secret = \"{node.NetworkSecret}\"\n");
        sb.AppendLine("[[peer]]");
        sb.AppendLine($"uri = \"{node.Protocol}://{node.Host}:{node.Port}\"\n");
        sb.AppendLine("[flags]");
        sb.AppendLine("no_tun = true");
        sb.AppendLine("disable_p2p = true");
        sb.AppendLine("disable_udp_hole_punching = true");
        return (sb.ToString(), instanceName);
    }

    // 运行时已启动的实例句柄缓存（按 SharedNode.Id）。避免重复启动。
    static Dictionary<string,int> nodedic = new Dictionary<string, int>();

    // 对指定节点进行一次探测：
    // - 按需启动实例
    // - 抓取 FFI 信息并解析为健康状态快照
    public static (bool ok, string? version, int connCount, long latencyUs, string? error) Probe(SharedNode node, TimeSpan timeout)
    {
        try
        {
            var toml = BuildTomlConfig(node);
            if (!nodedic.ContainsKey(toml.instancename))
            {
                int r1 = parse_config(toml.toml);
                if (r1 < 0) return (false, null, 0, 0, GetErrorMessage());
                var r2 = run_network_instance(toml.toml);
                if (r2 < 0) return (false, null, 0, 0, GetErrorMessage());
                nodedic[toml.instancename] = r2;
            }
            var snapshot = ExtractMetrics(CollectAll(), node.NetworkName, $"{node.Protocol}://{node.Host}:{node.Port}");
            // 若解析到错误信息，直接返回（失败优先）。
            if (!string.IsNullOrEmpty(snapshot.error)) return snapshot;
            // 若存在连接且状态 ok，直接返回。
            if (snapshot.connCount > 0 && snapshot.ok) return snapshot;
            return snapshot;
        }
        catch (DllNotFoundException e) { return (false, null, 0, 0, $"FFI dll not found: {e.Message}"); }
        catch (Exception ex) { return (false, null, 0, 0, ex.Message); }
    }

    // 新增：探测后删除（关闭并清理实例）
    public static (bool ok, string? version, int connCount, long latencyUs, string? error) ProbeAndDispose(SharedNode node)
    {
        var instancename = "";
        try
        {
            var toml = BuildTomlConfig(node);
            instancename = toml.instancename;
            if (!nodedic.ContainsKey(toml.instancename))
            {
                int r1 = parse_config(toml.toml);
                if (r1 < 0) return (false, null, 0, 0, GetErrorMessage());
                var r2 = run_network_instance(toml.toml);
                if (r2 < 0) return (false, null, 0, 0, GetErrorMessage());
                nodedic[toml.instancename] = r2;
            }
            Thread.Sleep(1000 * 10);

            var snapshot = ExtractMetrics(CollectAll(), node.NetworkName, $"{node.Protocol}://{node.Host}:{node.Port}");
            // 若解析到错误信息，直接返回（失败优先）。
            if (!string.IsNullOrEmpty(snapshot.error)) return snapshot;
            // 若存在连接且状态 ok，直接返回。
            if (snapshot.connCount > 0 && snapshot.ok) return snapshot;
            return snapshot;
        }
        catch (DllNotFoundException e) { return (false, null, 0, 0, $"FFI dll not found: {e.Message}"); }
        catch (Exception ex) { return (false, null, 0, 0, ex.Message); }
        finally 
        {
            IntPtr namesPtr = IntPtr.Zero;
            nodedic.Remove(instancename);
            var instanceNames = nodedic.Keys.ToArray();
            IntPtr[] namePointers = null;
            try
            {
                if (instanceNames != null && instanceNames.Length > 0)
                {
                    namePointers = new IntPtr[instanceNames.Length];
                    for (int i = 0; i < instanceNames.Length; i++)
                    {
                        if (instanceNames[i] == null)
                            throw new ArgumentException($"instanceNames[{i}] is null");

                        namePointers[i] = Marshal.StringToHGlobalAnsi(instanceNames[i]);
                    }

                    namesPtr = Marshal.AllocHGlobal(IntPtr.Size * namePointers.Length);
                    Marshal.Copy(namePointers, 0, namesPtr, namePointers.Length);
                }
                else
                {
                    namesPtr = IntPtr.Zero;
                }
                if (namesPtr == IntPtr.Zero && nodedic.Count >0)
                    throw new Exception("");
                int ret = retain_network_instance(namesPtr, instanceNames?.Length ?? 0);

            }
            catch 
            {

            }
        }
    }


    // 从 FFI 返回的键值对中提取健康状态：
    // - 顶层快速字段（error_msg/running/version）
    // - 定位目标网络的 JSON（若无则回退扫描所有 JSON）
    // - 解析版本、错误、运行态、连接数与延迟；记录 peers/routes 用于零延迟规则
    // - 事件中匹配当前目标的 ConnectError：仅在无连接/路由且延迟为 0 时视为不健康（避免历史错误影响）
    // - 若无显式错误且延迟为 0 且 peers/routes 都为 0，则按“零延迟判定掉线”处理
    // - 错误文本统一进行 IP/域名掩码（Sanitize）
    private static (bool ok, string? version, int connCount, long latencyUs, string? error) ExtractMetrics(Dictionary<string, string> dict, string networkKey, string expectedTarget)
    {
        bool ok = true;              // 默认健康
        string? error = null;        // 错误文案（掩码后）
        string? version = null;      // 版本字符串（优先 my_node_info.version）
        int connCount = 0;           // 连接数（由 peers 或 peer_route_pairs 或网络聚合统计）
        long latencyUs = 0;          // 延迟（微秒），由 peers.stats.latency_us 或 routes.path_latency 推断
        bool sawConnectivityError = false; // 是否观察到显式连接错误（error_msg 或 ConnectError）
        int peersCount = 0;          // peers 数组长度（用于零延迟规则）
        int routesCount = 0;         // routes 数组长度（用于零延迟规则）

        // 读取顶层快速字段：通常为聚合后的键值，非 JSON。
        if (dict.TryGetValue("error_msg", out var topErr) && !IsNullOrEmptyOrLiteralNull(topErr)) { error = Sanitize(topErr); sawConnectivityError = true; }
        if (dict.TryGetValue("running", out var runningStr)) ok = ParseBoolLike(runningStr);
        if (dict.TryGetValue("version", out var topVer)) version = topVer;

        // 定位目标网络对应的 JSON blob（key 为 networkKey），没有就回退扫描所有 JSON 值。
        string? blob = dict.TryGetValue(networkKey, out var val) && LooksLikeJson(val) ? val : null;

        // 扫描事件：只在“当前无连接/无路由且延迟为 0”时将匹配到的 ConnectError 视为不健康。
        // 这样历史错误不会影响已经恢复的节点。
        void scanEventsForTarget(JsonElement root)
        {
            var targets = new[] { expectedTarget, expectedTarget.Replace("://", ":") };
            if (!root.TryGetProperty("events", out var eventsEl) || eventsEl.ValueKind != JsonValueKind.Array) return;
            foreach (var evt in eventsEl.EnumerateArray())
            {
                if (evt.ValueKind != JsonValueKind.String) continue; // 事件条目是字符串形式的 JSON
                var es = evt.GetString();
                if (string.IsNullOrWhiteSpace(es) || !LooksLikeJson(es)) continue;
                try
                {
                    using var evtDoc = JsonDocument.Parse(es);
                    var evRoot = evtDoc.RootElement;
                    if (!evRoot.TryGetProperty("event", out var evObj) || evObj.ValueKind != JsonValueKind.Object) continue;
                    foreach (var ev in evObj.EnumerateObject())
                    {
                        if (!ev.NameEquals("ConnectError")) continue;
                        bool targetMatch = false;
                        if (ev.Value.ValueKind == JsonValueKind.Array)
                        {
                            var arr = ev.Value.EnumerateArray().ToArray();
                            var targetRaw = arr.Length > 0 && arr[0].ValueKind == JsonValueKind.String ? arr[0].GetString() : null;
                            if (!string.IsNullOrEmpty(targetRaw) && targets.Any(t => string.Equals(t, targetRaw, StringComparison.OrdinalIgnoreCase))) targetMatch = true;
                        }
                        else
                        {
                            targetMatch = true; // 无具体目标同样视作匹配
                        }

                        // 只有在“未形成任何连接/路由且延迟为 0”时，才把 ConnectError 视为当前不健康。
                        if (targetMatch)
                        {
                            sawConnectivityError = true;
                            if (peersCount == 0 && routesCount == 0 && latencyUs == 0)
                            {
                                var targetMasked = Sanitize(ev.Value.ValueKind == JsonValueKind.Array ? ev.Value.EnumerateArray().FirstOrDefault().GetString() : null);
                                string? msgMasked = null;
                                if (ev.Value.ValueKind == JsonValueKind.Array)
                                {
                                    var arr = ev.Value.EnumerateArray().ToArray();
                                    if (arr.Length > 2 && arr[2].ValueKind == JsonValueKind.String) msgMasked = Sanitize(arr[2].GetString());
                                }
                                error ??= $"ConnectError: {targetMasked} {msgMasked}".Trim();
                                ok = false;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        // 解析单个网络 JSON 根：提取版本、错误、运行态、连接数、延迟，并更新 peers/routes 计数。
        void parseRoot(JsonElement root)
        {
            // 版本优先来源：my_node_info.version；其次 route.version；最后通用 version。
            if (version is null)
            {
                if (TryFindStringAtPath(root, new[] { "my_node_info", "version" }, out var ve)) version = ve;
                else if (TryFindStringRecursive(root, "route.version", out var vr)) version = vr?.Split('-').FirstOrDefault();
                else if (TryFindStringRecursive(root, "version", out var vgen)) version = vgen;
            }
            // 错误（掩码）
            if (error is null && TryFindStringRecursive(root, "error_msg", out var e) && !IsNullOrEmptyOrLiteralNull(e)) { error = Sanitize(e); sawConnectivityError = true; }
            // 运行态
            if (TryFindBoolRecursive(root, "running", out var run)) ok = run;
            // 连接数：优先 network_count 聚合；否则统计 peers 或 peer_route_pairs。
            var add = SumIntByNameRecursive(root, "network_count");
            if (add > 0) connCount += add; else { connCount += CountArrayByNameRecursive(root, "peers"); if (connCount == 0) connCount += CountArrayByNameRecursive(root, "peer_route_pairs"); }
            // 延迟：优先 peers.conns.stats.latency_us（微秒）；其次 routes.path_latency（毫秒）×1000。
            var maxLatency = MaxIntByNameRecursive(root, "latency_us");
            if (maxLatency > 0) latencyUs = maxLatency; else { var minPath = MinIntByNameRecursive(root, "path_latency"); if (minPath > 0) latencyUs = (long)minPath * 1000L; }
            // peers/routes 数量统计：用于“零延迟判定掉线”。
            if (root.TryGetProperty("peers", out var peersEl) && peersEl.ValueKind == JsonValueKind.Array) peersCount = peersEl.GetArrayLength();
            if (root.TryGetProperty("routes", out var routesEl) && routesEl.ValueKind == JsonValueKind.Array) routesCount = routesEl.GetArrayLength();
            // 事件扫描（见上）。
            scanEventsForTarget(root);
        }

        // 先解析目标网络 blob，否则回退解析所有 JSON 值。
        if (blob is not null) { try { using var doc = JsonDocument.Parse(blob); parseRoot(doc.RootElement); } catch { } }
        else
        {
            foreach (var s in dict.Values)
            { if (!LooksLikeJson(s)) continue; try { using var doc = JsonDocument.Parse(s); parseRoot(doc.RootElement); } catch { } }
        }

        // 零延迟判定：仅在无显式错误且 peers/routes 都为 0 时触发，不会覆盖已有的明确错误。
        if (!sawConnectivityError && latencyUs == 0 && peersCount == 0 && routesCount == 0)
        { ok = false; error ??= "latency is zero"; }

        // 任何错误文案都表示不健康。
        if (!string.IsNullOrEmpty(error)) ok = false;
        return (ok, version, connCount, latencyUs, error);
    }

    // 工具方法：判断字符串是否像 JSON（以 { 或 [ 开头）。
    private static bool LooksLikeJson(string s)
        => !string.IsNullOrWhiteSpace(s) && (s.TrimStart().StartsWith("{") || s.TrimStart().StartsWith("["));

    // 工具方法：是否为空或字面值 "null"。
    private static bool IsNullOrEmptyOrLiteralNull(string s)
        => string.IsNullOrWhiteSpace(s) || s.Trim().Equals("null", StringComparison.OrdinalIgnoreCase);

    // 工具方法：解析布尔样式字符串（true/1）。
    private static bool ParseBoolLike(string s)
        => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("1");

    // 工具方法：按路径获取字符串属性（逐层对象）。
    private static bool TryFindStringAtPath(JsonElement el, string[] path, out string? value)
    {
        value = null; var cur = el;
        foreach (var p in path) { if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(p, out cur)) return false; }
        if (cur.ValueKind == JsonValueKind.String) { value = cur.GetString(); return true; }
        return false;
    }

    // 工具方法：递归查找指定名称的字符串/数字并返回字符串表示。
    private static bool TryFindStringRecursive(JsonElement el, string name, out string? value)
    {
        value = null;
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    if (prop.NameEquals(name))
                    { if (prop.Value.ValueKind == JsonValueKind.String) { value = prop.Value.GetString(); return true; } if (prop.Value.ValueKind == JsonValueKind.Number) { value = prop.Value.ToString(); return true; } }
                    if (TryFindStringRecursive(prop.Value, name, out value)) return true;
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray()) if (TryFindStringRecursive(item, name, out value)) return true;
                break;
        }
        return false;
    }

    // 工具方法：递归查找指定名称的布尔值。
    private static bool TryFindBoolRecursive(JsonElement el, string name, out bool value)
    {
        value = false;
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    if (prop.NameEquals(name) && (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)) { value = prop.Value.GetBoolean(); return true; }
                    if (TryFindBoolRecursive(prop.Value, name, out value)) return true;
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray()) if (TryFindBoolRecursive(item, name, out value)) return true;
                break;
        }
        return false;
    }

    // 工具方法：递归求和指定名称的整数字段。
    private static int SumIntByNameRecursive(JsonElement el, string name)
    {
        var sum = 0;
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject()) { if (prop.NameEquals(name) && prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var v)) sum += v; else sum += SumIntByNameRecursive(prop.Value, name); }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray()) sum += SumIntByNameRecursive(item, name);
                break;
        }
        return sum;
    }

    // 工具方法：递归查找指定名称的数组长度（返回第一个命中的长度）。
    private static int CountArrayByNameRecursive(JsonElement el, string name)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    if (prop.NameEquals(name) && prop.Value.ValueKind == JsonValueKind.Array) return prop.Value.GetArrayLength();
                    var child = CountArrayByNameRecursive(prop.Value, name); if (child > 0) return child;
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray()) { var child = CountArrayByNameRecursive(item, name); if (child > 0) return child; }
                break;
        }
        return 0;
    }

    // 工具方法：递归查找指定名称的整数字段最大值。
    private static int MaxIntByNameRecursive(JsonElement el, string name)
    {
        var max = 0;
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject()) { if (prop.NameEquals(name) && prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var v)) { if (v > max) max = v; } var child = MaxIntByNameRecursive(prop.Value, name); if (child > max) max = child; }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray()) { var child = MaxIntByNameRecursive(item, name); if (child > max) max = child; }
                break;
        }
        return max;
    }

    // 工具方法：递归查找指定名称的整数字段最小值（返回 0 表示未命中）。
    private static int MinIntByNameRecursive(JsonElement el, string name)
    {
        int? min = null;
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject()) { if (prop.NameEquals(name) && prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var v)) { if (min is null || v < min) min = v; } var child = MinIntByNameRecursive(prop.Value, name); if (child > 0 && (min is null || child < min)) min = child; }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray()) { var child = MinIntByNameRecursive(item, name); if (child > 0 && (min is null || child < min)) min = child; }
                break;
        }
        return min ?? 0;
    }

    // 敏感信息掩码：错误文案中的 IP/域名统一做掩码，避免泄露具体地址。
    private static string Sanitize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var text = s;
        // IPv4 掩码：a.b.*.*
        text = Regex.Replace(text, @"\b(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})\b", m => $"{m.Groups[1].Value}.{m.Groups[2].Value}.*.*");
        // IPv6 掩码：保留前两段，其余通配
        text = Regex.Replace(text, @"\b([0-9a-fA-F]{1,4}:){2}([0-9a-fA-F]{1,4}:){0,6}[0-9a-fA-F]{1,4}\b", m => { var parts = m.Value.Split(':'); return parts.Length >= 2 ? parts[0] + ":" + parts[1] + ":*:*::*" : "*:*::*"; });
        // tcp://host:port 或 tcp:host:port 的 host 掩码（支持 IP 或域名）。
        text = Regex.Replace(text, @"\b(tcp(?:\:\/\/|:) )(\S+?)(:\d{1,5})\b", m => { var prefix = m.Groups[1].Value; var host = m.Groups[2].Value; var port = m.Groups[3].Value; return prefix + MaskHost(host) + port; });
        // 独立域名掩码：如 example.com / sub.example.com
        text = Regex.Replace(text, @"\b([a-zA-Z0-9-]+\.)+[a-zA-Z]{2,}\b", m => MaskDomain(m.Value));
        return text;
    }

    // 掩码 host：IP -> 屏蔽后两段；IPv6 -> 仅保留前两段；域名 -> 保留前缀+TLD 或 SLD+TLD
    private static string MaskHost(string host)
    {
        if (Regex.IsMatch(host, @"^\d{1,3}(?:\.\d{1,3}){3}$")) { var hp = host.Split('.'); return hp[0] + "." + hp[1] + ".*.*"; }
        if (host.Contains(":") && Regex.IsMatch(host, @"^[0-9a-fA-F:]+$")) { var parts = host.Split(':'); return parts.Length >= 2 ? parts[0] + ":" + parts[1] + ":*:*::*" : "*:*::*"; }
        return MaskDomain(host);
    }

    // 掩码域名：
    // - 二段域名（example.com）：保留 2 字符前缀 + TLD，如 ex***.com
    // - 多段域名（sub.example.com）：保留 SLD+TLD，子域以 ** 替代，如 **.example.com
    private static string MaskDomain(string domain)
    {
        var parts = domain.Split('.');
        if (parts.Length <= 2)
        {
            var left = parts[0]; var tld = parts.Length == 2 ? parts[1] : ""; var prefix = left.Length <= 2 ? left : left[..2];
            return string.IsNullOrEmpty(tld) ? prefix + "***" : prefix + "***." + tld;
        }
        else { var tld = parts[^1]; var sld = parts[^2]; return "**." + sld + "." + tld; }
    }
}
