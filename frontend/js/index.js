let currentPage = 1;
let currentSearchKey = '';

window.onload = function () {
    fetchNodeList(currentPage, currentSearchKey);
    bindSearchEvent();
};

function fetchNodeList(page, searchKey) {
    const tbody = document.getElementById('node-list-body');
    tbody.innerHTML = '<tr><td colspan="5" class="loading"><i class="fas fa-spinner"></i> 加载中...</td></tr>';

    const url = new URL(`api/nodes`, window.location.origin);
    url.searchParams.set('page', page);
    url.searchParams.set('per_page', 20);
    if (searchKey) url.searchParams.set('search', searchKey);

    fetch(url)
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                renderNodeList(data.data);
                renderPagination(data.pagination);
                document.getElementById('data-update-time').textContent = data.latest_update;
            } else {
                showError(data.error);
            }
        })
        .catch(error => showError(`加载失败：${error.message}`));
}

function renderNodeList(nodes) {
    const tbody = document.getElementById('node-list-body');
    tbody.innerHTML = nodes.map(node => `
        <tr onclick="window.location.href='node-detail.html?id=${node.server_id}'">
            <td>
                <i class="fas fa-server" style="color: #6c5ce7; margin-right: 8px;"></i>
                ${node.name}
            </td>
            <td>
                <div class="status-tag ${node.is_active ? 'status-online' : 'status-offline'}">
                    <i class="fas ${node.is_active ? 'fa-check-circle' : 'fa-times-circle'}"></i> 
                    ${node.is_active ? '在线' : '离线'}
                </div>
            </td>
            <td>
                <div class="load-tag ${getNodeLoadClass(node.load_level)}">${node.load_text}</div>
            </td>
            <td>
                <div class="tag-group">${node.tags.map(tag => `<span class="node-tag">${tag}</span>`).join('')}</div>
            </td>
            <td>${node.created_at}</td>
        </tr>
    `).join('');
}

function renderPagination(pagination) {
    const paginationEl = document.getElementById('pagination');
    paginationEl.innerHTML = `
        <button onclick="fetchNodeList(1, currentSearchKey)" ${pagination.page === 1 ? 'disabled' : ''}>
            <i class="fas fa-angle-double-left"></i> 首页
        </button>
        <button onclick="fetchNodeList(${pagination.page - 1}, currentSearchKey)" ${pagination.page === 1 ? 'disabled' : ''}>
            <i class="fas fa-angle-left"></i> 上一页
        </button>
        <span class="pagination-info">
            第 ${pagination.page}/${pagination.total_pages} 页（共 ${pagination.total} 个节点）
        </span>
        <button onclick="fetchNodeList(${pagination.page + 1}, currentSearchKey)" ${pagination.page === pagination.total_pages ? 'disabled' : ''}>
            下一页 <i class="fas fa-angle-right"></i>
        </button>
        <button onclick="fetchNodeList(${pagination.total_pages}, currentSearchKey)" ${pagination.page === pagination.total_pages ? 'disabled' : ''}>
            末页 <i class="fas fa-angle-double-right"></i>
        </button>
    `;
}

function bindSearchEvent() {
    const searchInput = document.querySelector('.search-input');
    const searchBtn = document.querySelector('.search-btn');

    searchBtn.addEventListener('click', () => {
        currentSearchKey = searchInput.value.trim();
        fetchNodeList(1, currentSearchKey);
    });

    searchInput.addEventListener('keyup', (e) => {
        if (e.key === 'Enter') {
            currentSearchKey = searchInput.value.trim();
            fetchNodeList(1, currentSearchKey);
        }
    });
}

function showError(message) {
    const tbody = document.getElementById('node-list-body');
    tbody.innerHTML = `<tr><td colspan="5" style="color: #e53e3e; text-align: center;">${message}</td></tr>`;
    document.getElementById('pagination').innerHTML = '';
}

function getNodeLoadClass(level) {
    switch(level) {
        case 'high': return 'load-high';
        case 'medium': return 'load-medium';
        default: return 'load-low';
    }
}
