$(document).ready(function () {
    // Initialize datepicker cho các trường datetime
    if ($.fn.datepicker && $.datepicker) {
        // Set defaults cho tiếng Việt
        $.datepicker.regional['vi'] = {
            closeText: 'Đóng',
            prevText: '&#x3C;',
            nextText: '&#x3E;',
            currentText: 'Hôm nay',
            monthNames: ['Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6',
                'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12'],
            monthNamesShort: ['Th1', 'Th2', 'Th3', 'Th4', 'Th5', 'Th6',
                'Th7', 'Th8', 'Th9', 'Th10', 'Th11', 'Th12'],
            dayNames: ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'],
            dayNamesShort: ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'],
            dayNamesMin: ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'],
            weekHeader: 'Tu',
            dateFormat: 'dd/mm/yy',
            firstDay: 1,
            isRTL: false,
            showMonthAfterYear: false,
            yearSuffix: ''
        };
        $.datepicker.setDefaults($.datepicker.regional['vi']);

        // Initialize datepicker
        $('.datetime').each(function () {
            $(this).datepicker({
                dateFormat: 'dd/mm/yy',
                changeMonth: true,
                changeYear: true,
                yearRange: '2020:2030'
            });
        });
    }
});
