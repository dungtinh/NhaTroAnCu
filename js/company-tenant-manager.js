// company-tenant-manager.js
// Quản lý khách thuê cho phòng trong hợp đồng công ty
// Phù hợp với structure sử dụng ContractTenant

var CompanyTenantManager = {
    // Configuration
    config: {
        maxTenantsPerRoom: 4,
        contractId: null,
        isEditMode: false
    },

    // State
    currentRoom: null,
    roomTenants: {}, // {roomId: [tenants]}
    deletedContractTenantIds: [],

    // Initialize
    init: function (options) {
        $.extend(this.config, options);
        this.bindEvents();

        if (this.config.isEditMode && this.config.contractId) {
            this.loadExistingTenants();
        } else {
            this.initializeEmptyRooms();
        }
    },

    // Bind events
    bindEvents: function () {
        var self = this;

        // Form submit
        $('#addTenantForm').on('submit', function (e) {
            e.preventDefault();
            self.addNewTenant();
        });

        // Source room change
        $('#sourceRoomSelect').on('change', function () {
            self.loadSourceRoomTenants($(this).val());
        });

        // Modal close
        $('#manageTenantModal').on('hidden.bs.modal', function () {
            self.updateRoomDisplay();
        });
    },

    // Initialize empty rooms
    initializeEmptyRooms: function () {
        var self = this;
        $('.room-row').each(function () {
            var roomId = $(this).data('room-id');
            if (!self.roomTenants[roomId]) {
                self.roomTenants[roomId] = [];
            }
        });
    },

    // Load existing tenants from server
    loadExistingTenants: function () {
        var self = this;

        $.ajax({
            url: '/Contracts/GetCompanyTenants',
            type: 'GET',
            data: { contractId: self.config.contractId },
            success: function (response) {
                if (response.success && response.tenants) {
                    // Group tenants by room
                    response.tenants.forEach(function (tenant) {
                        if (!self.roomTenants[tenant.roomId]) {
                            self.roomTenants[tenant.roomId] = [];
                        }

                        self.roomTenants[tenant.roomId].push({
                            id: tenant.id, // ContractTenant.Id
                            tenantId: tenant.tenantId, // Tenant.Id
                            roomId: tenant.roomId,
                            name: tenant.fullName,
                            identity: tenant.identityCard,
                            phone: tenant.phoneNumber,
                            birthDate: tenant.birthDate,
                            gender: tenant.gender,
                            ethnicity: tenant.ethnicity,
                            address: tenant.permanentAddress,
                            vehicle: tenant.vehiclePlate,
                            photo: tenant.photo,
                            isNew: false
                        });
                    });

                    self.updateAllRoomDisplays();
                }
            }
        });
    },

    // Open modal
    openModal: function (roomId, roomName, roomIndex) {
        this.currentRoom = {
            id: roomId,
            name: roomName,
            index: roomIndex
        };

        $('#modalRoomId').val(roomId);
        $('#modalRoomIndex').val(roomIndex);
        $('#modalRoomName, #modalRoomNameInfo').text(roomName);

        // Get price
        var $row = $(`.room-row[data-room-id="${roomId}"]`);
        var priceText = $row.find('.room-price').val() || $row.find('.text-muted').text();
        $('#modalRoomPrice').text(priceText);

        this.loadCurrentTenants(roomId);
        this.loadOtherRooms(roomId);

        $('#manageTenantModal').modal('show');
    },

    // Load current tenants
    loadCurrentTenants: function (roomId) {
        var tenants = this.roomTenants[roomId] || [];
        var html = '';

        if (tenants.length === 0) {
            $('#noTenantsAlert').show();
            $('#currentTenantsList').hide();
        } else {
            $('#noTenantsAlert').hide();
            $('#currentTenantsList').show();

            tenants.forEach(function (tenant) {
                html += CompanyTenantManager.renderTenantCard(tenant);
            });

            $('#currentTenantsList').html(html);
        }

        this.updateTenantCount(tenants.length);
    },

    // Render tenant card
    renderTenantCard: function (tenant) {
        return `
            <div class="tenant-card-modal" data-tenant-id="${tenant.id || 'new_' + Date.now()}">
                <div class="card mb-3">
                    <div class="card-body">
                        <div class="row">
                            <div class="col-md-2 text-center">
                                <img src="${tenant.photo || '/images/default-avatar.png'}" 
                                     class="rounded-circle" 
                                     style="width: 80px; height: 80px; object-fit: cover;">
                            </div>
                            <div class="col-md-8">
                                <h6>${tenant.name} ${tenant.isNew ? '<span class="badge badge-success">Mới</span>' : ''}</h6>
                                <small class="text-muted">
                                    CCCD: ${tenant.identity} | 
                                    SĐT: ${tenant.phone || 'N/A'} |
                                    Ngày sinh: ${tenant.birthDate || 'N/A'}
                                </small>
                                <div class="text-muted small">
                                    ${tenant.address || ''}
                                </div>
                            </div>
                            <div class="col-md-2 text-right">
                                <button class="btn btn-sm btn-danger" 
                                        onclick="CompanyTenantManager.removeTenant('${tenant.id}', '${tenant.name}')">
                                    <i class="fas fa-trash"></i>
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    },

    // Update tenant count
    updateTenantCount: function (count) {
        $('#modalTenantCount').text(count + '/' + this.config.maxTenantsPerRoom);
        $('#currentTenantBadge').text(count);

        if (count >= this.config.maxTenantsPerRoom) {
            $('#modalRoomStatus').removeClass('badge-success').addClass('badge-danger').text('Đầy');
        } else {
            $('#modalRoomStatus').removeClass('badge-danger').addClass('badge-success').text('Còn chỗ');
        }
    },

    // Add new tenant
    addNewTenant: function () {
        var roomId = $('#modalRoomId').val();
        var tenants = this.roomTenants[roomId] || [];

        if (tenants.length >= this.config.maxTenantsPerRoom) {
            Swal.fire('Lỗi', 'Phòng đã đầy', 'warning');
            return;
        }

        var newTenant = {
            id: null, // Will be null for new tenants
            tenantId: null,
            roomId: roomId,
            name: $('#newTenantName').val(),
            identity: $('#newTenantIdentity').val(),
            phone: $('#newTenantPhone').val(),
            birthDate: $('#newTenantBirthDate').val(),
            gender: $('#newTenantGender').val(),
            ethnicity: $('#newTenantEthnicity').val(),
            address: $('#newTenantAddress').val(),
            vehicle: $('#newTenantVehicle').val(),
            isNew: true
        };

        // Validate
        if (!newTenant.name || !newTenant.identity) {
            Swal.fire('Lỗi', 'Vui lòng nhập họ tên và CCCD', 'error');
            return;
        }

        // Check duplicate
        var isDuplicate = false;
        Object.values(this.roomTenants).forEach(function (roomTenants) {
            roomTenants.forEach(function (t) {
                if (t.identity === newTenant.identity) {
                    isDuplicate = true;
                }
            });
        });

        if (isDuplicate) {
            Swal.fire('Lỗi', 'Số CCCD đã tồn tại', 'error');
            return;
        }

        // Add to room
        if (!this.roomTenants[roomId]) {
            this.roomTenants[roomId] = [];
        }
        this.roomTenants[roomId].push(newTenant);

        this.loadCurrentTenants(roomId);
        $('#addTenantForm')[0].reset();
        $('a[href="#currentTenants"]').tab('show');

        Swal.fire('Thành công', 'Đã thêm khách thuê', 'success');
    },

    // Remove tenant
    removeTenant: function (tenantId, tenantName) {
        var self = this;

        Swal.fire({
            title: 'Xác nhận xóa?',
            text: `Xóa khách thuê ${tenantName}?`,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Xóa',
            cancelButtonText: 'Hủy'
        }).then((result) => {
            if (result.isConfirmed) {
                var roomId = $('#modalRoomId').val();
                var tenants = self.roomTenants[roomId] || [];

                // Find tenant
                var tenant = tenants.find(t => t.id == tenantId);

                // If existing tenant, add to deleted list
                if (tenant && tenant.id && !tenant.isNew) {
                    self.deletedContractTenantIds.push(tenant.id);
                }

                // Remove from array
                self.roomTenants[roomId] = tenants.filter(t => t.id != tenantId);

                self.loadCurrentTenants(roomId);
                Swal.fire('Đã xóa', '', 'success');
            }
        });
    },

    // Load other rooms for transfer
    loadOtherRooms: function (currentRoomId) {
        var self = this;
        var options = '<option value="">-- Chọn phòng --</option>';

        $('.room-row').each(function () {
            var roomId = $(this).data('room-id');
            var roomName = $(this).find('strong').first().text();

            if (roomId != currentRoomId) {
                var count = self.roomTenants[roomId] ? self.roomTenants[roomId].length : 0;
                if (count > 0) {
                    options += `<option value="${roomId}">${roomName} (${count} người)</option>`;
                }
            }
        });

        $('#sourceRoomSelect').html(options);
    },

    // Load source room tenants
    loadSourceRoomTenants: function (sourceRoomId) {
        if (!sourceRoomId) {
            $('#sourceRoomTenants').hide();
            return;
        }

        var tenants = this.roomTenants[sourceRoomId] || [];
        if (tenants.length === 0) {
            $('#sourceRoomTenants').hide();
            return;
        }

        var html = '<div class="list-group">';
        tenants.forEach(function (tenant) {
            html += `
                <label class="list-group-item">
                    <input type="checkbox" class="transfer-tenant-checkbox" 
                           data-tenant='${JSON.stringify(tenant)}'>
                    <span class="ml-2">${tenant.name} - ${tenant.identity}</span>
                </label>
            `;
        });
        html += '</div>';

        $('#transferTenantsList').html(html);
        $('#sourceRoomTenants').show();
    },

    // Confirm transfer
    confirmTransfer: function () {
        var self = this;
        var targetRoomId = $('#modalRoomId').val();
        var sourceRoomId = $('#sourceRoomSelect').val();
        var selectedTenants = [];

        $('.transfer-tenant-checkbox:checked').each(function () {
            var tenant = JSON.parse($(this).attr('data-tenant'));
            selectedTenants.push(tenant);
        });

        if (selectedTenants.length === 0) {
            Swal.fire('Thông báo', 'Chọn khách thuê để chuyển', 'warning');
            return;
        }

        // Check capacity
        var currentCount = (this.roomTenants[targetRoomId] || []).length;
        if (currentCount + selectedTenants.length > this.config.maxTenantsPerRoom) {
            Swal.fire('Lỗi', 'Phòng không đủ chỗ', 'error');
            return;
        }

        // Transfer
        selectedTenants.forEach(function (tenant) {
            // Remove from source
            self.roomTenants[sourceRoomId] = self.roomTenants[sourceRoomId]
                .filter(t => t.identity !== tenant.identity);

            // Update room and add to target
            tenant.roomId = targetRoomId;
            if (!self.roomTenants[targetRoomId]) {
                self.roomTenants[targetRoomId] = [];
            }
            self.roomTenants[targetRoomId].push(tenant);
        });

        this.loadCurrentTenants(targetRoomId);
        this.loadOtherRooms(targetRoomId);
        $('#sourceRoomSelect').val('');
        $('#sourceRoomTenants').hide();

        Swal.fire('Thành công', `Đã chuyển ${selectedTenants.length} khách`, 'success');
    },

    // Update room display
    updateRoomDisplay: function () {
        if (!this.currentRoom) return;

        var roomId = this.currentRoom.id;
        var count = (this.roomTenants[roomId] || []).length;
        var $row = $(`.room-row[data-room-id="${roomId}"]`);

        $row.find('.tenant-count-badge').html(`<i class="fas fa-users"></i> ${count} người`);

        if (count > 0) {
            $row.find('.badge-warning').removeClass('badge-warning')
                .addClass('badge-success').text('Đang sử dụng');
        } else {
            $row.find('.badge-success').removeClass('badge-success')
                .addClass('badge-warning').text('Trống');
        }
    },

    // Update all displays
    updateAllRoomDisplays: function () {
        var self = this;
        $('.room-row').each(function () {
            var roomId = $(this).data('room-id');
            var count = (self.roomTenants[roomId] || []).length;

            $(this).find('.tenant-count-badge').html(
                `<i class="fas fa-users"></i> ${count} người`
            );

            if (count > 0) {
                $(this).find('.badge-warning').removeClass('badge-warning')
                    .addClass('badge-success').text('Đang sử dụng');
            }
        });
    },

    // Generate hidden inputs for form submission
    generateHiddenInputs: function () {
        var html = '';
        var index = 0;
        var self = this;

        // Generate for all tenants
        Object.keys(this.roomTenants).forEach(function (roomId) {
            var tenants = self.roomTenants[roomId];

            tenants.forEach(function (tenant) {
                html += `
                    <input type="hidden" name="CompanyTenants[${index}].Id" value="${tenant.id || ''}" />
                    <input type="hidden" name="CompanyTenants[${index}].TenantId" value="${tenant.tenantId || ''}" />
                    <input type="hidden" name="CompanyTenants[${index}].RoomId" value="${roomId}" />
                    <input type="hidden" name="CompanyTenants[${index}].FullName" value="${tenant.name}" />
                    <input type="hidden" name="CompanyTenants[${index}].IdentityCard" value="${tenant.identity}" />
                    <input type="hidden" name="CompanyTenants[${index}].PhoneNumber" value="${tenant.phone || ''}" />
                    <input type="hidden" name="CompanyTenants[${index}].BirthDate" value="${tenant.birthDate || ''}" />
                    <input type="hidden" name="CompanyTenants[${index}].Gender" value="${tenant.gender || ''}" />
                    <input type="hidden" name="CompanyTenants[${index}].Ethnicity" value="${tenant.ethnicity || ''}" />
                    <input type="hidden" name="CompanyTenants[${index}].PermanentAddress" value="${tenant.address || ''}" />
                    <input type="hidden" name="CompanyTenants[${index}].VehiclePlate" value="${tenant.vehicle || ''}" />
                    <input type="hidden" name="CompanyTenants[${index}].IsNew" value="${tenant.isNew || false}" />
                `;
                index++;
            });
        });

        // Add deleted IDs
        this.deletedContractTenantIds.forEach(function (id) {
            html += `<input type="hidden" name="DeletedContractTenantIds" value="${id}" />`;
        });

        $('#companyTenantsHiddenInputs').remove();
        $('<div id="companyTenantsHiddenInputs" style="display:none;">' + html + '</div>')
            .appendTo('form.contract-form');
    },

    // Save tenants
    saveRoomTenants: function () {
        this.generateHiddenInputs();
        $('#manageTenantModal').modal('hide');
        Swal.fire('Đã lưu', 'Thông tin khách thuê đã được cập nhật', 'success');
    }
};