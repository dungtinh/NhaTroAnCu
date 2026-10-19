// Trên điện thoại, bảng có tiêu đề cột (thead) được xếp thành thẻ: mỗi ô hiện kèm tên cột.
// Gắn data-label cho từng ô theo tiêu đề cột; chạy lại mỗi khi Blazor vẽ lại trang.
(function () {
    function label(table) {
        var heads = table.tHead ? Array.prototype.map.call(table.tHead.rows[0].cells, function (c) { return c.innerText.trim(); }) : null;
        if (!heads || heads.length === 0) return;
        table.setAttribute('data-stack', '');
        Array.prototype.forEach.call(table.tBodies, function (body) {
            Array.prototype.forEach.call(body.rows, function (row) {
                if (row.cells.length === 1) { row.setAttribute('data-full', ''); return; }
                Array.prototype.forEach.call(row.cells, function (cell, i) {
                    var h = heads[i] || '';
                    if (cell.getAttribute('data-label') !== h) cell.setAttribute('data-label', h);
                    if (!cell.innerText.trim() && !cell.querySelector('input,select,button,a,img')) cell.setAttribute('data-empty', '');
                    else cell.removeAttribute('data-empty');
                });
            });
        });
    }
    var pending = false;
    function run() {
        pending = false;
        document.querySelectorAll('table.nh-table').forEach(label);
    }
    function schedule() { if (!pending) { pending = true; requestAnimationFrame(run); } }
    new MutationObserver(schedule).observe(document.documentElement, { childList: true, subtree: true, characterData: true });
    document.addEventListener('DOMContentLoaded', run);
})();

// Bảng "Thêm" trên điện thoại: bấm link thì đóng bảng (để Blazor tự chuyển trang).
document.addEventListener('click', function (e) {
    var link = e.target.closest && e.target.closest('.offcanvas a[href]');
    if (!link || !window.bootstrap) return;
    var panel = link.closest('.offcanvas');
    var inst = window.bootstrap.Offcanvas.getInstance(panel);
    if (inst) inst.hide();
});

// Chat nội bộ
window.nhChat = {
    scrollBottom: function (id) { var el = document.getElementById(id); if (el) el.scrollTop = el.scrollHeight; },
    clear: function (el) { if (el) { el.value = ''; el.style.height = ''; } }
};

// Cài ứng dụng (PWA): giữ sự kiện cài để trang /app bấm nút là cài được.
window.nhInstall = { prompt: null };
window.addEventListener('beforeinstallprompt', function (e) { e.preventDefault(); window.nhInstall.prompt = e; document.dispatchEvent(new Event('nh-can-install')); });
window.nhInstall.canInstall = function () { return !!window.nhInstall.prompt; };
window.nhInstall.isStandalone = function () { return window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true; };
window.nhInstall.isIOS = function () { return /iphone|ipad|ipod/i.test(navigator.userAgent); };
window.nhInstall.show = async function () {
    var p = window.nhInstall.prompt; if (!p) return 'none';
    p.prompt(); var r = await p.userChoice; window.nhInstall.prompt = null; return r.outcome;
};

// Ghi số điện: Enter ở ô số điện thì chuyển xuống ô của phòng kế tiếp.
document.addEventListener('keydown', function (e) {
    if (e.key !== 'Enter' || !e.target.classList || !e.target.classList.contains('nh-kwh')) return;
    e.preventDefault();
    var all = Array.prototype.slice.call(document.querySelectorAll('input.nh-kwh'));
    var next = all[all.indexOf(e.target) + (e.shiftKey ? -1 : 1)];
    if (next) { next.focus(); next.select(); }
});

// Xem ảnh và phiếu PDF ngay trong khung trên trang. Link mở tab mới (target=_blank) trên app cài ở màn hình chính iPhone
// không có nút quay lại nên người dùng bị kẹt; ở đây chặn các link ảnh/PDF và hiện lớp phủ có nút đóng, nút quay lại cũng đóng được.
(function () {
    var box, opened = false;
    function ensure() {
        if (box) return;
        box = document.createElement('div');
        box.className = 'nh-lightbox';
        box.setAttribute('role', 'dialog');
        box.innerHTML = '<div class="nh-lb-bar"><span class="nh-lb-title"></span><a class="nh-lb-dl btn btn-sm btn-outline-light" download><i class="fas fa-download"></i> Tải về</a><button type="button" class="nh-lb-close" aria-label="Đóng">×</button></div><div class="nh-lb-body"></div>';
        document.body.appendChild(box);
        box.addEventListener('click', function (e) {
            if (e.target === box || e.target.classList.contains('nh-lb-body') || e.target.closest('.nh-lb-close')) close();
        });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && opened) close(); });
        window.addEventListener('popstate', function () { if (opened) hide(); });
    }
    function open(href, title, isPdf) {
        ensure();
        var body = box.querySelector('.nh-lb-body');
        body.innerHTML = '';
        var el = document.createElement(isPdf ? 'iframe' : 'img');
        el.src = href;
        if (isPdf) el.title = title || 'PDF'; else el.alt = title || '';
        body.appendChild(el);
        box.querySelector('.nh-lb-title').textContent = title || '';
        var dl = box.querySelector('.nh-lb-dl');
        dl.href = href;
        dl.style.display = isPdf ? '' : 'none';
        box.classList.add('show');
        document.body.classList.add('nh-lb-open');
        opened = true;
        try { history.pushState({ nhLightbox: true }, ''); } catch (err) { }
    }
    function hide() {
        if (!box) return;
        box.classList.remove('show');
        document.body.classList.remove('nh-lb-open');
        box.querySelector('.nh-lb-body').innerHTML = '';
        opened = false;
    }
    function close() {
        if (history.state && history.state.nhLightbox) history.back(); else hide();
    }
    document.addEventListener('click', function (e) {
        var a = e.target.closest && e.target.closest('a[href]');
        if (!a || a.closest('.nh-lightbox') || e.ctrlKey || e.metaKey || e.shiftKey) return;
        var href = a.getAttribute('href') || '';
        var isImg = /\.(png|jpe?g|gif|webp|bmp)(\?|$)/i.test(href) || (a.target === '_blank' && !!a.querySelector('img'));
        var isPdf = /\.pdf(\?|$)/i.test(href) || /^\/?files\/(bills|checkout)\//i.test(href);
        if (!isImg && !isPdf) return;
        e.preventDefault();
        e.stopPropagation();
        var img = a.querySelector('img');
        open(a.href, a.getAttribute('title') || (img && img.alt) || '', isPdf && !isImg);
    }, true);
})();
