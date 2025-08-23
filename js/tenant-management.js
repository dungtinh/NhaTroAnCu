// tenant-management.js - Quản lý tenant cho Contract Create/Edit
var TenantManager = (function () {
    'use strict';

    var tenantCount = 0;
    var maxTenants = 4;

    // Template HTML cho tenant mới
    function getTenantTemplate(index) {
        return `
            <div class="tenant-card" data-tenant-index="${index}">
                <div class="tenant-card-header">
                    <span class="tenant-number">Người thuê ${index + 1}</span>
                    <button type="button" class="btn-remove-tenant btn btn-sm btn-danger" onclick="TenantManager.removeTenant(${index})">
                        <i class="fas fa-trash"></i> Xóa
                    </button>
                </div>

                <!-- Hidden fields -->
                <input type="hidden" name="Tenants[${index}].Id" value="0" />
                <input type="hidden" name="Tenants[${index}].TenantId" value="0" />

                <!-- Phần quét CCCD và upload ảnh -->
                <div class="scan-section" style="background: #f8f9fa; padding: 15px; border-radius: 5px; margin-bottom: 20px;">
                    <h5 style="color: #2c3e50; margin-bottom: 15px;">
                        <i class="fas fa-id-card"></i> Quét CCCD/CMND & Upload ảnh
                    </h5>

                    <div class="form-group">
                        <label>Upload ảnh CCCD/CMND để tự động điền thông tin</label>
                        <input type="file" class="cccd-file form-control" data-tenant-index="${index}" accept="image/*" />
                        <small class="form-text text-muted">
                            Hỗ trợ định dạng: JPG, PNG, GIF. Hệ thống sẽ tự động nhận diện và điền thông tin.
                        </small>
                    </div>

                    <button type="button" class="btn btn-primary btn-scan-cccd" data-tenant-index="${index}">
                        <i class="fas fa-qrcode"></i> Quét thông tin CCCD
                    </button>

                    <div class="scan-result hidden" data-tenant-index="${index}" style="margin-top: 15px;">
                        <!-- Kết quả quét sẽ hiển thị ở đây -->
                    </div>

                    <div class="form-group" style="margin-top: 15px;">
                        <label>Upload ảnh chân dung (sẽ lưu vào hồ sơ người thuê)</label>
                        <input type="file" name="TenantPhotos[${index}]" class="form-control tenant-photo-file" data-tenant-index="${index}" accept="image/*" />
                        <small class="form-text text-muted">
                            Ảnh này sẽ được lưu làm ảnh đại diện của người thuê
                        </small>
                    </div>
                </div>

                <!-- Thông tin người thuê -->
                <div class="form-row">
                    <div class="form-group">
                        <label>Họ và tên <span class="text-danger">*</span></label>
                        <input type="text" name="Tenants[${index}].FullName" class="form-control tenant-fullname" data-tenant-index="${index}" required />
                        <span class="field-validation-valid text-danger" data-valmsg-for="Tenants[${index}].FullName" data-valmsg-replace="true"></span>
                    </div>

                    <div class="form-group">
                        <label>Số CCCD/CMND <span class="text-danger">*</span></label>
                        <input type="text" name="Tenants[${index}].IdentityCard" class="form-control tenant-identity" data-tenant-index="${index}" required />
                        <span class="field-validation-valid text-danger" data-valmsg-for="Tenants[${index}].IdentityCard" data-valmsg-replace="true"></span>
                    </div>

                    <div class="form-group">
                        <label>Số điện thoại</label>
                        <input type="text" name="Tenants[${index}].PhoneNumber" class="form-control tenant-phone" data-tenant-index="${index}" />
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>Ngày sinh</label>
                        <input type="text" name="Tenants[${index}].BirthDate" class="form-control datetime tenant-birthdate" data-tenant-index="${index}" />
                    </div>

                    <div class="form-group">
                        <label>Giới tính</label>
                        <select name="Tenants[${index}].Gender" class="form-control tenant-gender" data-tenant-index="${index}">
                            <option value="">-- Chọn --</option>
                            <option value="Nam">Nam</option>
                            <option value="Nữ">Nữ</option>
                            <option value="Khác">Khác</option>
                        </select>
                    </div>

                    <div class="form-group">
                        <label>Dân tộc</label>
                        <input type="text" name="Tenants[${index}].Ethnicity" class="form-control tenant-ethnicity" data-tenant-index="${index}" />
                    </div>

                    <div class="form-group">
                        <label>Biển số xe</label>
                        <input type="text" name="Tenants[${index}].VehiclePlate" class="form-control tenant-vehicle" data-tenant-index="${index}" />
                    </div>
                </div>

                <div class="form-group">
                    <label>Địa chỉ thường trú</label>
                    <input type="text" name="Tenants[${index}].PermanentAddress" class="form-control tenant-address" data-tenant-index="${index}" />
                </div>
            </div>
        `;
    }

    // Khởi tạo
    function init() {
        // Đếm số tenant hiện có
        tenantCount = $('.tenant-card').length;

        // Cập nhật hiển thị nút xóa
        updateRemoveButtons();

        // Bind events
        bindEvents();
    }

    // Bind các events
    function bindEvents() {
        // Event cho nút quét CCCD (sử dụng delegation)
        $(document).off('click', '.btn-scan-cccd').on('click', '.btn-scan-cccd', function () {
            var index = $(this).data('tenant-index');
            scanCCCD(index);
        });

        // Event cho nút xóa tenant
        $(document).off('click', '.btn-remove-tenant').on('click', '.btn-remove-tenant', function () {
            var index = $(this).closest('.tenant-card').data('tenant-index');
            removeTenant(index);
        });
    }

    // Thêm tenant mới
    function addTenant() {
        if (tenantCount >= maxTenants) {
            Swal.fire({
                icon: 'warning',
                title: 'Đạt giới hạn',
                text: `Đã đạt giới hạn số người thuê (tối đa ${maxTenants} người/phòng)`,
                confirmButtonColor: '#ffc107'
            });
            return;
        }

        var newTenantHtml = getTenantTemplate(tenantCount);
        $('#tenantListContainer').append(newTenantHtml);

        tenantCount++;
        updateRemoveButtons();

        // Re-init datepicker cho fields mới
        initDatepickerForNewTenant(tenantCount - 1);

        // Re-init autonumeric cho money fields nếu có
        initAutonumericForNewTenant(tenantCount - 1);

        // Scroll đến tenant mới
        var newCard = $(`.tenant-card[data-tenant-index="${tenantCount - 1}"]`);
        $('html, body').animate({
            scrollTop: newCard.offset().top - 100
        }, 500);

        // Focus vào field đầu tiên
        newCard.find('.tenant-fullname').focus();
    }

    // Xóa tenant
    function removeTenant(index) {
        if (tenantCount <= 1) {
            Swal.fire({
                icon: 'warning',
                title: 'Không thể xóa',
                text: 'Phải có ít nhất 1 người thuê trong hợp đồng!',
                confirmButtonColor: '#ffc107'
            });
            return;
        }

        Swal.fire({
            title: 'Xác nhận xóa?',
            text: "Bạn có chắc muốn xóa người thuê này?",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#3085d6',
            cancelButtonColor: '#d33',
            confirmButtonText: 'Xóa',
            cancelButtonText: 'Hủy'
        }).then((result) => {
            if (result.isConfirmed) {
                $(`.tenant-card[data-tenant-index="${index}"]`).fadeOut(300, function () {
                    $(this).remove();
                    tenantCount--;
                    updateTenantIndexes();
                    updateRemoveButtons();
                });
            }
        });
    }

    // Cập nhật lại index sau khi xóa
    function updateTenantIndexes() {
        $('.tenant-card').each(function (newIndex) {
            var $card = $(this);
            var oldIndex = $card.data('tenant-index');

            // Cập nhật data-tenant-index
            $card.attr('data-tenant-index', newIndex);

            // Cập nhật số thứ tự hiển thị
            $card.find('.tenant-number').text('Người thuê ' + (newIndex + 1));

            // Cập nhật tất cả các input names
            $card.find('input, select, textarea').each(function () {
                var $input = $(this);
                var name = $input.attr('name');
                if (name) {
                    var newName = name.replace(/\[(\d+)\]/g, '[' + newIndex + ']');
                    $input.attr('name', newName);
                }

                // Cập nhật data-tenant-index
                if ($input.data('tenant-index') !== undefined) {
                    $input.attr('data-tenant-index', newIndex);
                }

                // Cập nhật validation message
                var valMsg = $input.siblings('.field-validation-valid');
                if (valMsg.length) {
                    var valMsgFor = valMsg.attr('data-valmsg-for');
                    if (valMsgFor) {
                        var newValMsgFor = valMsgFor.replace(/\[(\d+)\]/g, '[' + newIndex + ']');
                        valMsg.attr('data-valmsg-for', newValMsgFor);
                    }
                }
            });

            // Cập nhật buttons
            $card.find('.btn-scan-cccd').attr('data-tenant-index', newIndex);
            $card.find('.btn-remove-tenant').attr('onclick', 'TenantManager.removeTenant(' + newIndex + ')');
            $card.find('.scan-result').attr('data-tenant-index', newIndex);
        });
    }

    // Cập nhật hiển thị nút xóa
    function updateRemoveButtons() {
        if (tenantCount <= 1) {
            $('.btn-remove-tenant').hide();
        } else {
            $('.btn-remove-tenant').show();
        }
    }

    // Khởi tạo datepicker cho tenant mới
    function initDatepickerForNewTenant(index) {
        var $dateFields = $(`.tenant-card[data-tenant-index="${index}"] .datetime`);
        if ($dateFields.length && typeof $.fn.datepicker !== 'undefined') {
            $dateFields.datepicker({
                format: 'dd/mm/yyyy',
                autoclose: true,
                todayHighlight: true,
                language: 'vi'
            });
        }
    }

    // Khởi tạo autonumeric cho tenant mới
    function initAutonumericForNewTenant(index) {
        var $moneyFields = $(`.tenant-card[data-tenant-index="${index}"] .money`);
        if ($moneyFields.length && typeof AutoNumeric !== 'undefined') {
            $moneyFields.each(function () {
                new AutoNumeric(this, {
                    digitGroupSeparator: '.',
                    decimalCharacter: ',',
                    decimalPlaces: 0,
                    minimumValue: '0'
                });
            });
        }
    }

    // Hàm quét CCCD
    function scanCCCD(tenantIndex) {
        var fileInput = $(`.cccd-file[data-tenant-index="${tenantIndex}"]`)[0];

        if (!fileInput.files || !fileInput.files[0]) {
            Swal.fire({
                icon: 'warning',
                title: 'Chưa chọn file',
                text: 'Vui lòng chọn file ảnh CCCD!',
                confirmButtonColor: '#ffc107'
            });
            return;
        }

        var formData = new FormData();
        formData.append('image', fileInput.files[0]);

        // Copy ảnh CCCD sang input photo
        var tenantPhotoInput = $(`.tenant-photo-file[data-tenant-index="${tenantIndex}"]`)[0];
        if (tenantPhotoInput && fileInput.files[0]) {
            var dataTransfer = new DataTransfer();
            dataTransfer.items.add(fileInput.files[0]);
            tenantPhotoInput.files = dataTransfer.files;
        }

        var $btn = $(`.btn-scan-cccd[data-tenant-index="${tenantIndex}"]`);
        var $result = $(`.scan-result[data-tenant-index="${tenantIndex}"]`);

        // Hiển thị loading
        $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> Đang quét...');
        $result.removeClass('hidden success partial error').html('Đang xử lý ảnh...');

        // Gọi API quét CCCD
        $.ajax({
            url: window.scanCCCDUrl || '/FPTReader/ScanCCCD',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {
                $btn.prop('disabled', false).html('<i class="fas fa-qrcode"></i> Quét thông tin CCCD');

                if (response.success && response.data) {
                    // Điền thông tin vào form
                    fillTenantData(tenantIndex, response.data);

                    $result.addClass('success').html(
                        '<i class="fas fa-check-circle"></i> Quét thành công! Thông tin đã được điền vào form.'
                    );

                    // Hiển thị thông báo thành công
                    Swal.fire({
                        icon: 'success',
                        title: 'Quét thành công!',
                        text: 'Thông tin CCCD đã được điền vào form và ảnh đã được lưu.',
                        timer: 2000,
                        showConfirmButton: false
                    });
                } else {
                    $result.addClass('error').html(
                        '<i class="fas fa-exclamation-circle"></i> ' + (response.message || 'Không thể quét được thông tin từ ảnh')
                    );
                }
            },
            error: function (xhr, status, error) {
                $btn.prop('disabled', false).html('<i class="fas fa-qrcode"></i> Quét thông tin CCCD');
                $result.addClass('error').html(
                    '<i class="fas fa-exclamation-circle"></i> Có lỗi xảy ra: ' + error
                );

                Swal.fire({
                    icon: 'error',
                    title: 'Lỗi!',
                    text: 'Không thể kết nối đến server',
                    confirmButtonColor: '#d33'
                });
            }
        });
    }

    // Điền dữ liệu từ kết quả quét vào form
    function fillTenantData(index, data) {
        if (data.name) {
            $(`.tenant-fullname[data-tenant-index="${index}"]`).val(data.name);
        }
        if (data.id) {
            $(`.tenant-identity[data-tenant-index="${index}"]`).val(data.id);
        }
        if (data.dob) {
            $(`.tenant-birthdate[data-tenant-index="${index}"]`).val(data.dob);
        }
        if (data.sex) {
            $(`.tenant-gender[data-tenant-index="${index}"]`).val(data.sex);
        }
        if (data.home) {
            $(`.tenant-address[data-tenant-index="${index}"]`).val(data.home);
        }
        if (data.ethnicity) {
            $(`.tenant-ethnicity[data-tenant-index="${index}"]`).val(data.ethnicity);
        }
    }

    // Public API
    return {
        init: init,
        addTenant: addTenant,
        removeTenant: removeTenant,
        scanCCCD: scanCCCD
    };
})();

// Auto-init khi document ready
$(document).ready(function () {
    TenantManager.init();
});