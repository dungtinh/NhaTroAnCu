$(document).ready(function () {
    // Destroy existing datepicker instances
    $('.datetime').each(function () {
        if ($(this).data('datepicker')) {
            $(this).datepicker('destroy');
        }
    });

    // Initialize Bootstrap Datepicker với format đúng
    $('.datetime').datepicker({
        format: "dd/mm/yyyy",  // Bootstrap Datepicker dùng 'format' và 'yyyy'
        autoclose: true,
        todayHighlight: true,
        todayBtn: "linked",
        clearBtn: true,
        language: "vi",
        weekStart: 1,
        forceParse: false,
        orientation: "bottom auto",
        templates: {
            leftArrow: '<i class="fas fa-chevron-left"></i>',
            rightArrow: '<i class="fas fa-chevron-right"></i>'
        }
    });    

    // Xử lý khi user chọn ngày
    $('.datetime').on('changeDate', function (e) {
        // e.date là Date object
        // e.format() sẽ format theo format đã set
        var formattedDate = e.format();
        console.log('Selected date:', formattedDate);

        // Đảm bảo value được set đúng
        $(this).val(formattedDate);
    });

    // Fix cho trường hợp user nhập tay
    $('.datetime').on('blur', function () {
        var val = $(this).val();
        if (val && val.indexOf('/') > -1) {
            var parts = val.split('/');
            if (parts.length === 3) {
                // Validate và format lại nếu cần
                var day = parseInt(parts[0]);
                var month = parseInt(parts[1]);
                var year = parseInt(parts[2]);

                if (day > 0 && day <= 31 && month > 0 && month <= 12 && year > 1900) {
                    // Valid date
                    var formattedDate = ('0' + day).slice(-2) + '/' +
                        ('0' + month).slice(-2) + '/' + year;
                    $(this).val(formattedDate);
                } else {
                    // Invalid date, reset to today
                    $(this).datepicker('setDate', new Date());
                }
            }
        }
    });

    // Debug logging
    console.log('Bootstrap Datepicker initialized with format: dd/mm/yyyy');
    $('.datetime').each(function () {
        console.log('Input:', $(this).attr('name'), 'Value:', $(this).val());
    });
});

// Helper function để convert date format
function convertDateFormat(dateStr, fromFormat, toFormat) {
    if (!dateStr) return '';

    var parts = dateStr.split('/');
    if (parts.length !== 3) return dateStr;

    if (fromFormat === 'mm/dd/yyyy' && toFormat === 'dd/mm/yyyy') {
        return parts[1] + '/' + parts[0] + '/' + parts[2];
    } else if (fromFormat === 'dd/mm/yyyy' && toFormat === 'mm/dd/yyyy') {
        return parts[1] + '/' + parts[0] + '/' + parts[2];
    }

    return dateStr;
}

// Function để validate và fix date format trước khi submit
function validateDateFormats() {
    var isValid = true;

    $('.datetime').each(function () {
        var val = $(this).val();
        var name = $(this).attr('name');

        if (val) {
            // Check format dd/mm/yyyy
            var regex = /^(0[1-9]|[12][0-9]|3[01])\/(0[1-9]|1[012])\/\d{4}$/;
            if (!regex.test(val)) {
                console.error('Invalid date format for ' + name + ': ' + val);

                // Try to fix
                var parts = val.split('/');
                if (parts.length === 3) {
                    var day = ('0' + parseInt(parts[0])).slice(-2);
                    var month = ('0' + parseInt(parts[1])).slice(-2);
                    var year = parts[2];

                    var fixed = day + '/' + month + '/' + year;
                    $(this).val(fixed);
                    console.log('Fixed to:', fixed);
                } else {
                    isValid = false;
                    alert('Ngày ' + name + ' không đúng định dạng dd/mm/yyyy');
                }
            }
        }
    });

    return isValid;
}

// Attach to form submit
$('form').on('submit', function (e) {
    if (!validateDateFormats()) {
        e.preventDefault();
        return false;
    }

    // Log final values
    console.log('Submitting with dates:');
    $('.datetime').each(function () {
        console.log($(this).attr('name') + ':', $(this).val());
    });
});
(function ($) {
    $.fn.datepicker.dates['vi'] = {
        days: ["Chủ nhật", "Thứ hai", "Thứ ba", "Thứ tư", "Thứ năm", "Thứ sáu", "Thứ bảy"],
        daysShort: ["CN", "T2", "T3", "T4", "T5", "T6", "T7"],
        daysMin: ["CN", "T2", "T3", "T4", "T5", "T6", "T7"],
        months: ["Tháng 1", "Tháng 2", "Tháng 3", "Tháng 4", "Tháng 5", "Tháng 6",
            "Tháng 7", "Tháng 8", "Tháng 9", "Tháng 10", "Tháng 11", "Tháng 12"],
        monthsShort: ["Th1", "Th2", "Th3", "Th4", "Th5", "Th6",
            "Th7", "Th8", "Th9", "Th10", "Th11", "Th12"],
        today: "Hôm nay",
        clear: "Xóa",
        format: "dd/mm/yyyy",
        titleFormat: "MM yyyy",
        weekStart: 1
    };
}(jQuery));