$(function () {
    // Hàm tính lại số nước đã dùng và tiền nước khi nhập chỉ số nước
    function updateWaterUsedAndMoney() {
        var start = parseInt($('#waterPrev').val()) || 0;
        var end = parseInt($('#waterCurrent').val()) || 0;
        var price = getMoney('#waterPrice') || 0;
        var used = end - start;
        if (used < 0) used = 0;
        $('#waterUsed').val(used);

        var waterMoney = used * price;
        $('#waterMoney').val(waterMoney);
        if (AutoNumeric.getAutoNumericElement('#waterMoney')) {
            AutoNumeric.getAutoNumericElement('#waterMoney').set(waterMoney);
        }
        updateTotalMoney();
    }

    function getMoney(selector) {
        var anElement = AutoNumeric.getAutoNumericElement(selector);
        return anElement ? anElement.getNumber() : parseInt($(selector).val()) || 0;
    }

    function updateTotalMoney() {
        var electric = getMoney('#electricMoney');
        var water = getMoney('#waterMoney');
        var rent = getMoney('#rentMoney');
        var extra = getMoney('#extraCharge');
        var discount = getMoney('#discount');
        var total = electric + water + rent + extra - discount;
        if (total < 0) total = 0;
        $('#totalMoney').val(total);
        if (AutoNumeric.getAutoNumericElement('#totalMoney')) {
            AutoNumeric.getAutoNumericElement('#totalMoney').set(total);
        }
    }

    $('#waterCurrent, #waterPrice').on('input change', updateWaterUsedAndMoney);
    $('#electricMoney, #waterMoney, #rentMoney, #extraCharge, #discount').on('input change', updateTotalMoney);

    updateWaterUsedAndMoney();
    updateTotalMoney();
});