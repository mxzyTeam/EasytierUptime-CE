let serverData = {};
let trendChart = null;
let latestUpdateTime = '加载中...';

// 页面加载后获取节点 ID 并请求数据
window.onload = function () {
    const nodeId = getNodeId();
    fetchNodeDetail(nodeId);
    bindRefreshEvent();
};

// 1. 获取 URL 中的节点 ID
function getNodeId() {
    const urlParams = new URLSearchParams(window.location.search);
    return urlParams.get('id') || 1; // 默认 ID=1
}

// 2. 请求节点详情数据
function fetchNodeDetail(nodeId) {
    const container = document.getElementById('detail-container');

    fetch(`api/node.php?id=${nodeId}`)
        .then(res => {
            if (!res.ok) throw new Error('网络请求失败');
            return res.json();
        })
        .then(data => {
            if (data.success && data.data) {
                serverData = data.data;
                latestUpdateTime = data.latest_update;
                // 渲染详情页面
                renderNodeDetail();
            } else {
                container.innerHTML = `
                            <div class="loading-container" style="color: #e53e3e;">
                                <i class="fas fa-exclamation-circle"></i>
                                <div>未找到该节点数据</div>
                            </div>
                        `;
            }
        })
        .catch(error => {
            container.innerHTML = `
                        <div class="loading-container" style="color: #e53e3e;">
                            <i class="fas fa-exclamation-circle"></i>
                            <div>加载失败：${error.message}</div>
                        </div>
                    `;
        });
}

// 3. 渲染节点详情
function renderNodeDetail() {
    const {
        name, server_id, protocol, version, created_at, updated_at,
        is_active, is_approved, ring_granularity, tags, allow_relay, host, port,
        qq_number, wechat, mail, current_connections, max_connections,
        usage_percentage, last_response_time, health_percentage_24h,
        health_record_total_counter_ring, current_health_status,
        description
    } = serverData;

    // 计算时间粒度（秒转分钟）
    const granularityText = `${ring_granularity}秒（${ring_granularity / 60}分钟）`;
    // 联系方式
    const contactText = qq_number || wechat || mail || '无';
    // 主机地址/端口
    const hostPortText = `${host || '-'}` / `${port || '-'}`;
    // 描述信息
    const descriptionText = description || '暂无描述';

    // 渲染页面结构
    const container = document.getElementById('detail-container');
    container.innerHTML = `
                <!-- 状态标签 -->
                <div class="card" style="grid-column: 1 / 4;">
                    <div class="status-tags">
                        <div class="status-tag ${current_health_status === 'healthy' ? 'tag-success' : 'tag-danger'}">
                            <i class="fas ${current_health_status === 'healthy' ? 'fa-check-circle' : 'fa-exclamation-circle'}"></i> 
                            健康状态：${current_health_status === 'healthy' ? '正常' : '异常'}
                        </div>
                        <div class="status-tag ${usage_percentage > 100 ? 'tag-danger' : 'tag-warning'}">
                            <i class="fas fa-exclamation-circle"></i> 连接状态：${usage_percentage > 100 ? '过载' : '正常'}（${current_connections}/${max_connections}）
                        </div>
                        <div class="status-tag ${last_response_time > 5000000 ? 'tag-danger' : 'tag-success'}">
                            <i class="fas ${last_response_time > 5000000 ? 'fa-clock' : 'fa-tachometer-alt'}"></i> 
                            响应状态：${last_response_time > 5000000 ? '缓慢' : '正常'}（${formatResponseTime(last_response_time)}）
                        </div>
                        <div class="status-tag tag-info">
                            <i class="fas fa-calendar-alt"></i> 最后更新：${updated_at}
                        </div>
                        <div class="status-tag ${is_approved ? 'tag-success' : 'tag-warning'}">
                            <i class="fas fa-verified"></i> 审核状态：${is_approved ? '已通过' : '未通过'}
                        </div>
                    </div>
                </div>

                <!-- 核心指标 -->
                <div class="card metrics-card">
                    <div class="metric-item ${usage_percentage > 100 ? 'warning' : ''}">
                        <div class="metric-label">
                            <i class="fas fa-percentage"></i> 连接使用率
                        </div>
                        <div class="metric-value ${usage_percentage > 100 ? 'warning' : ''}">${usage_percentage.toFixed(2)}<span class="metric-unit">%</span></div>
                    </div>
                    <div class="metric-item ${last_response_time > 5000000 ? 'warning' : ''}" id="response-time-item">
                        <div class="metric-label">
                            <i class="fas fa-tachometer-alt"></i> 最后响应时间
                        </div>
                        <div class="metric-value ${last_response_time > 5000000 ? 'warning' : ''}">
                            ${formatResponseTime(last_response_time).split(' ')[0]}
                            <span class="metric-unit">${formatResponseTime(last_response_time).split(' ')[1]}</span>
                        </div>
                    </div>
                    <div class="metric-item">
                        <div class="metric-label">
                            <i class="fas fa-heart"></i> 24小时健康率
                        </div>
                        <div class="metric-value">${health_percentage_24h.toFixed(2)}<span class="metric-unit">%</span></div>
                    </div>
                    <div class="metric-item ${usage_percentage > 100 ? 'warning' : ''}">
                        <div class="metric-label">
                            <i class="fas fa-users"></i> 当前连接百分比/最大连接百分比
                        </div>
                        <div class="metric-value ${usage_percentage > 100 ? 'warning' : ''}">${current_connections}% / ${max_connections}%</div>
                    </div>
                </div>

                <!-- 统计卡片 -->
                <div class="card stats-card">
                    <div class="stat-item">
                        <div class="stat-label">健康检查总次数</div>
                        <div class="stat-value" id="total-checks">0</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-label">单次最高检查次数</div>
                        <div class="stat-value" id="max-checks">0</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-label">单次平均检查次数</div>
                        <div class="stat-value" id="avg-checks">0</div>
                    </div>
                </div>

                <!-- 趋势图 -->
                <div class="card">
                    <div class="card-title">
                        <div class="card-title-left">
                            <i></i>健康检查次数趋势（${ring_granularity / 60}分钟/次）
                        </div>
                        <div class="card-actions">
                            <select id="time-range">
                                <option value="10">显示前10时段</option>
                                <option value="20" selected>显示前20时段</option>
                                <option value="all">显示全部时段</option>
                            </select>
                        </div>
                    </div>
                    <div id="trend-chart"></div>
                </div>

                <!-- 连接饼图 -->
                <div class="card">
                    <div class="card-title">
                        <div class="card-title-left">
                            <i></i>连接数占比
                        </div>
                    </div>
                    <div id="connection-chart"></div>
                </div>

                <!-- 健康占比图 -->
                <div class="card">
                    <div class="card-title">
                        <div class="card-title-left">
                            <i></i>24小时健康检查统计
                        </div>
                    </div>
                    <div id="health-rate-chart"></div>
                </div>

                <!-- 基础信息 -->
                <div class="card info-card">
                    <div class="info-item">
                        <div class="info-label">节点ID</div>
                        <div class="info-value">${server_id}</div>
                    </div>
                    <div class="info-item">
                        <div class="info-label">协议/版本</div>
                        <div class="info-value">${protocol} / ${version}</div>
                    </div>
                    <div class="info-item">
                        <div class="info-label">创建时间</div>
                        <div class="info-value">${created_at}</div>
                    </div>
                    <div class="info-item">
                        <div class="info-label">时间粒度</div>
                        <div class="info-value">${granularityText}</div>
                    </div>
                    <div class="info-item">
                        <div class="info-label">标签</div>
                        <div class="info-value">${tags.join('、')}</div>
                    </div>
                    <div class="info-item">
                        <div class="info-label">是否允许转发</div>
                        <div class="info-value">${allow_relay === null || allow_relay === undefined ? '不确定' : allow_relay ? '是' : '否'}</div>
                    </div>
                    <div class="info-item">
                        <div class="info-label">主机地址/端口</div>
                        <div class="info-value">${(host && port) ? `${host}:${port}` : '无'}</div>
                    </div>
                    <div class="info-item">
                        <div class="info-label">联系方式</div>
                        <div class="info-value">${contactText}</div>
                    </div>
                    <div class="info-item">
                        <div class="info-label">描述</div>
                        <div class="info-value">${descriptionText}</div>
                    </div>
                </div>
            `;

    document.getElementById('detail-update-time').textContent = latestUpdateTime;

    // 更新页面标题
    document.getElementById('node-name').textContent = `节点详细信息：${name}`;

    // 初始化图表和统计数据
    calculateHealthStats();
    initTrendChart('20');
    initConnectionChart();
    initHealthRateChart();
    bindTimeRangeEvent();
}

// 4. 格式化响应时间（ms/s 切换）
function formatResponseTime(time) {
    // 确保 time 是数字（容错）
    time = Number(time) || 0;
    const msTime = time / 1000;
    if (msTime >= 1000) {
        // 大于等于1秒，显示为 秒.毫秒（保留1位小数）
        return `${(msTime / 1000).toFixed(1)} s`;
    } else {
        // 小于1秒，显示为毫秒
        return `${msTime.toFixed(1)} ms`;
    }
}

// 5. 计算健康统计数据
function calculateHealthStats() {
    const healthData = serverData.health_record_total_counter_ring || [];
    const totalChecks = healthData.reduce((sum, val) => sum + val, 0);
    const maxChecks = Math.max(...healthData, 0);
    const avgChecks = healthData.length > 0 ? (totalChecks / healthData.length).toFixed(1) : '0';

    document.getElementById('total-checks').textContent = totalChecks;
    document.getElementById('max-checks').textContent = maxChecks;
    document.getElementById('avg-checks').textContent = avgChecks;
}

// 6. 初始化趋势图
function initTrendChart(range = '20') {
    const healthData = serverData.health_record_total_counter_ring || [];
    let displayData, xAxisData;

    switch (range) {
        case '10':
            displayData = healthData.slice(0, 10);
            xAxisData = displayData.map((_, i) => `第${i + 1}时段`);
            break;
        case 'all':
            displayData = healthData;
            xAxisData = displayData.map((_, i) => `第${i + 1}时段`);
            break;
        default:
            displayData = healthData.slice(0, 20);
            xAxisData = displayData.map((_, i) => `第${i + 1}时段`);
    }

    if (trendChart) trendChart.dispose();
    trendChart = echarts.init(document.getElementById('trend-chart'));

    trendChart.setOption({
        tooltip: { trigger: 'axis', formatter: '{b}: {c} 次' },
        grid: { left: '10%', right: '5%', bottom: '15%', top: '10%' },
        xAxis: { type: 'category', data: xAxisData, axisLabel: { rotate: 30, fontSize: 11 } },
        yAxis: { type: 'value', name: '检查次数', nameTextStyle: { fontSize: 11 }, min: Math.min(...displayData) - 20 },
        series: [{
            name: '健康检查次数',
            type: 'line',
            data: displayData,
            smooth: true,
            lineStyle: { width: 2, color: '#409eff' },
            itemStyle: { color: '#409eff', borderWidth: 2, borderColor: '#fff' },
            areaStyle: {
                color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
                    { offset: 0, color: 'rgba(64, 158, 255, 0.3)' },
                    { offset: 1, color: 'rgba(64, 158, 255, 0.05)' }
                ])
            },
            markLine: { data: [{ type: 'average', name: '平均值' }], lineStyle: { color: '#ff7d00' }, label: { fontSize: 10 } }
        }]
    });
}

// 7. 初始化连接饼图
function initConnectionChart() {
    const { current_connections, max_connections } = serverData;
    const connectionChart = echarts.init(document.getElementById('connection-chart'));

    connectionChart.setOption({
        tooltip: { trigger: 'item', formatter: '{b}: {c}% (占图{d}%)' },
        legend: { top: 'bottom', textStyle: { fontSize: 11 } },
        series: [{
            name: '连接数',
            type: 'pie',
            radius: ['40%', '70%'],
            center: ['50%', '40%'],
            data: [
                { value: current_connections, name: '当前连接', itemStyle: { color: '#409eff' } },
                { value: Math.max(0, max_connections - current_connections), name: '剩余连接', itemStyle: { color: '#f5f5f5' } },
                { value: Math.max(0, current_connections - max_connections), name: '超出连接', itemStyle: { color: '#ff4d4f' } }
            ],
            label: { show: false },
            emphasis: { label: { show: true, fontSize: 14, fontWeight: 'bold' } },
            labelLine: { show: false }
        }]
    });
}

// 8. 初始化健康占比图
function initHealthRateChart() {
    const healthData = serverData.health_record_total_counter_ring || [];
    const healthyData = serverData.health_record_healthy_counter_ring || [];

    // 总检查次数（求和，容错非数字）
    const totalChecks = healthData.reduce((sum, val) => sum + (Number(val) || 0), 0);
    // 成功检查次数（求和，容错非数字）
    const successChecks = healthyData.reduce((sum, val) => sum + (Number(val) || 0), 0);
    // 失败次数（总次数 - 成功次数，避免负数）
    const failChecks = Math.max(0, totalChecks - successChecks);
    const healthRate = serverData.health_percentage_24h || ((totalChecks > 0 ? successChecks / totalChecks * 100 : 0).toFixed(2));

    const healthRateChart = echarts.init(document.getElementById('health-rate-chart'));
    healthRateChart.setOption({
        tooltip: { trigger: 'item', formatter: '{b}: {c} 次 ({d}%)' },
        legend: { top: 'bottom', textStyle: { fontSize: 11 } },
        series: [{
            name: '健康检查结果',
            type: 'pie',
            radius: ['40%', '70%'],
            center: ['50%', '40%'],
            data: [
                { value: successChecks, name: '成功', itemStyle: { color: '#48bb78' } },
                { value: failChecks, name: '失败', itemStyle: { color: '#ff4d4f' } }
            ],
            label: {
                show: true,
                position: 'center',
                formatter: `${healthData.length}个时段\n${healthRate}%健康`,
                fontSize: 14,
                fontWeight: 'bold',
                color: '#333'
            },
            labelLine: { show: false }
        }]
    });
}

// 9. 绑定时间范围切换事件
function bindTimeRangeEvent() {
    document.getElementById('time-range').addEventListener('change', (e) => {
        initTrendChart(e.target.value);
    });
}

// 10. 绑定刷新事件
function bindRefreshEvent() {
    document.getElementById('refresh-btn').addEventListener('click', () => {
        const btn = document.getElementById('refresh-btn');
        btn.disabled = true;
        btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> 刷新中...';
        document.getElementById('detail-update-time').textContent = '刷新中...';

        const nodeId = getNodeId();
        fetchNodeDetail(nodeId).then(() => {
            btn.disabled = false;
            btn.innerHTML = '<i class="fas fa-sync-alt"></i> 刷新数据';
            alert('数据刷新成功！');
            // 刷新后自动更新为最新的 monitor_tables.update_time
        });
    });
}

// 窗口大小变化时调整图表
window.addEventListener('resize', () => {
    if (trendChart) trendChart.resize();
    echarts.init(document.getElementById('connection-chart')).resize();
    echarts.init(document.getElementById('health-rate-chart')).resize();
});
