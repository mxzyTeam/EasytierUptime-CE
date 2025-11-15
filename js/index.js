let currentPage = 1;
let currentSearchKey = ''; // 存储当前搜索关键词

window.onload = function () {
    fetchNodeList(currentPage, currentSearchKey); // 初始加载无搜索关键词
    bindSearchEvent();
};

// 1. 请求节点列表（新增 searchKey 参数）
function fetchNodeList(page, searchKey) {
    const tbody = document.getElementById('node-list-body');
    tbody.innerHTML = `<tr><td colspan="5" class="loading"><i class="fas fa-spinner"></i> 加载中...</td></tr>`;

    // 关键：传递搜索关键词到后端
    const url = new URL(`api/get-node-list.php`, window.location.origin);
    url.searchParams.set('page', page);
    url.searchParams.set('per_page', 20);
    if (searchKey) url.searchParams.set('search', searchKey); // 拼接搜索参数

    fetch(url.toString())
        .then(res => {
            if (!res.ok) throw new Error('网络请求失败');
            return res.json();
        })
        .then(data => {
            if (data.success) {
                if (data.data.length > 0) {
                    renderNodeList(data.data);
                    renderPagination(data.pagination);
                    document.getElementById('data-update-time').textContent = data.latest_update;
                } else {
                    tbody.innerHTML = `<tr><td colspan="5" class="loading">未找到匹配的节点</td></tr>`;
                    document.getElementById('pagination').innerHTML = '';
                    document.getElementById('data-update-time').textContent = data.latest_update || '无数据';
                }
            } else {
                tbody.innerHTML = `<tr><td colspan="5" class="loading" style="color: #e53e3e;">${data.error}</td></tr>`;
                document.getElementById('data-update-time').textContent = '加载失败';
            }
        })
        .catch(error => {
            tbody.innerHTML = `<tr><td colspan="5" class="loading" style="color: #e53e3e;">加载失败：${error.message}</td></tr>`;
            document.getElementById('data-update-time').textContent = '加载失败';
        });
}

// 2. 渲染列表、分页、绑定事件（保持不变，删除原doSearch函数的本地过滤逻辑）
function renderNodeList(nodes) {
    const tbody = document.getElementById('node-list-body');
    tbody.innerHTML = '';

    nodes.forEach(node => {
        const tagsHtml = node.tags.map(tag => `<span class="node-tag">${tag}</span>`).join('');
        const statusClass = node.is_active ? 'status-online' : 'status-offline';
        const statusIcon = node.is_active ? 'fa-check-circle' : 'fa-times-circle';
        const statusText = node.is_active ? '在线' : '离线';

        let loadClass = 'load-low';
        if (node.load_level === 'high') loadClass = 'load-high';
        else if (node.load_level === 'medium') loadClass = 'load-medium';
        else if (node.load_level === 'none') loadClass = '';

        const tr = document.createElement('tr');
        tr.onclick = () => {
            window.location.href = `node-detail.html?id=${node.server_id}`;
        };
        tr.innerHTML = `
            <td>
                <div style="display: flex; align-items: center;">
                    <i class="fas fa-server" style="color: #409eff; margin-right: 8px;"></i>
                    ${node.name}
                </div>
            </td>
            <td>
                <div class="status-tag ${statusClass}">
                    <i class="fas ${statusIcon}"></i> ${statusText}
                </div>
            </td>
            <td>
                <div class="load-tag ${loadClass}">${node.load_text}</div>
            </td>
            <td>
                <div class="tag-group">${tagsHtml}</div>
            </td>
            <td>${node.created_at}</td>
        `;
        tbody.appendChild(tr);
    });
}

function renderPagination(pagination) {
    const paginationEl = document.getElementById('pagination');
    paginationEl.innerHTML = `
        <button class="pagination-btn" onclick="fetchNodeList(1, currentSearchKey)" ${pagination.page === 1 ? 'disabled' : ''}>
            <i class="fas fa-angle-double-left"></i> 首页
        </button>
        <button class="pagination-btn" onclick="fetchNodeList(${pagination.page - 1}, currentSearchKey)" ${pagination.page === 1 ? 'disabled' : ''}>
            <i class="fas fa-angle-left"></i> 上一页
        </button>
        <span class="pagination-info">
            第 ${pagination.page}/${pagination.total_pages} 页（共 ${pagination.total} 个节点）
        </span>
        <button class="pagination-btn" onclick="fetchNodeList(${pagination.page + 1}, currentSearchKey)" ${pagination.page === pagination.total_pages ? 'disabled' : ''}>
            下一页 <i class="fas fa-angle-right"></i>
        </button>
        <button class="pagination-btn" onclick="fetchNodeList(${pagination.total_pages}, currentSearchKey)" ${pagination.page === pagination.total_pages ? 'disabled' : ''}>
            末页 <i class="fas fa-angle-double-right"></i>
        </button>
    `;
}

// 3. 绑定搜索事件（传递关键词到后端）
function bindSearchEvent() {
    const searchInput = document.querySelector('.search-input');
    const searchBtn = document.querySelector('.search-btn');

    searchBtn.addEventListener('click', () => {
        currentSearchKey = searchInput.value.trim();
        fetchNodeList(1, currentSearchKey); // 搜索时默认跳转到第1页
    });

    searchInput.addEventListener('keyup', e => {
        if (e.key === 'Enter') {
            currentSearchKey = searchInput.value.trim();
            fetchNodeList(1, currentSearchKey);
        }
    });
}