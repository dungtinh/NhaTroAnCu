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
