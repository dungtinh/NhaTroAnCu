window.ShowSuccess = function (message, callback) {
    Swal.fire({
        icon: 'success',
        title: 'Thành công!',
        text: message,
        confirmButtonColor: '#28a745'
    }).then((result) => {
        if (callback && typeof callback === 'function') {
            callback();
        }
    });
};

window.ShowError = function (message) {
    Swal.fire({
        icon: 'error',
        title: 'Lỗi!',
        text: message,
        confirmButtonColor: '#d33'
    });
};

window.ShowWarning = function (message) {
    Swal.fire({
        icon: 'warning',
        title: 'Cảnh báo!',
        text: message,
        confirmButtonColor: '#ffc107'
    });
};

window.ShowInfo = function (message) {
    Swal.fire({
        icon: 'info',
        title: 'Thông tin',
        text: message,
        confirmButtonColor: '#3085d6'
    });
};

window.ConfirmDelete = function (title, text, deleteCallback) {
    Swal.fire({
        title: title || 'Xác nhận xóa?',
        text: text || 'Bạn không thể hoàn tác sau khi xóa!',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#3085d6',
        confirmButtonText: 'Xóa',
        cancelButtonText: 'Hủy'
    }).then((result) => {
        if (result.isConfirmed && deleteCallback) {
            deleteCallback();
        }
    });
};

window.ConfirmAction = function (title, text, confirmCallback) {
    Swal.fire({
        title: title,
        text: text,
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#3085d6',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Xác nhận',
        cancelButtonText: 'Hủy'
    }).then((result) => {
        if (result.isConfirmed && confirmCallback) {
            confirmCallback();
        }
    });
};
