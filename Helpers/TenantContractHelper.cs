using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using NhaTroAnCu.Models;

namespace NhaTroAnCu.Helpers
{
    /// <summary>
    /// Helper class để quản lý Tenant trong Contract (Create/Edit)
    /// </summary>
    public static class TenantContractHelper
    {
        /// <summary>
        /// Xử lý thêm/cập nhật tenant cho hợp đồng Individual (overload cho Create với List<Tenant>)
        /// </summary>
        public static void ProcessIndividualContractTenants(
            NhaTroAnCuEntities db,
            Contract contract,
            List<Tenant> tenantModels,
            HttpRequestBase request,
            bool isEdit = false)
        {
            // Convert List<Tenant> sang List<TenantViewModel>
            var tenantViewModels = tenantModels?.Select(t => new TenantViewModel
            {
                TenantId = t.Id,
                FullName = t.FullName,
                IdentityCard = t.IdentityCard,
                PhoneNumber = t.PhoneNumber,
                BirthDate = t.BirthDate,
                Gender = t.Gender,
                PermanentAddress = t.PermanentAddress,
                Ethnicity = t.Ethnicity,
                VehiclePlate = t.VehiclePlate,
                Photo = t.Photo
            }).ToList();

            // Gọi method chính
            ProcessIndividualContractTenants(db, contract, tenantViewModels, request, isEdit);
        }

        /// <summary>
        /// Xử lý thêm/cập nhật tenant cho hợp đồng Individual (method chính với List<TenantViewModel>)
        /// </summary>
        public static void ProcessIndividualContractTenants(
            NhaTroAnCuEntities db,
            Contract contract,
            List<TenantViewModel> tenantModels,
            HttpRequestBase request,
            bool isEdit = false)
        {
            if (tenantModels == null || !tenantModels.Any())
            {
                if (!isEdit)
                {
                    throw new InvalidOperationException("Hợp đồng cá nhân phải có ít nhất 1 người thuê");
                }
                return;
            }

            // Lấy RoomId từ contract
            var roomId = contract.ContractRooms.FirstOrDefault()?.RoomId;
            if (!roomId.HasValue)
            {
                throw new InvalidOperationException("Không tìm thấy phòng cho hợp đồng");
            }

            // Nếu là Edit, xử lý xóa tenant trước
            if (isEdit)
            {
                RemoveDeletedTenants(db, contract, tenantModels);
            }

            // Xử lý thêm/cập nhật tenant
            foreach (var tenantModel in tenantModels)
            {
                if (string.IsNullOrEmpty(tenantModel.FullName) ||
                    string.IsNullOrEmpty(tenantModel.IdentityCard))
                {
                    continue;
                }

                var tenant = ProcessSingleTenant(
                    db,
                    tenantModel,
                    contract.CompanyId,
                    request,
                    tenantModels.IndexOf(tenantModel)
                );

                if (tenant != null)
                {
                    // Đảm bảo tenant được lưu
                    db.SaveChanges();

                    // Thêm vào ContractTenant nếu chưa có
                    EnsureContractTenantExists(db, contract.Id, tenant.Id, roomId.Value);
                }
            }

            // Validate số lượng tenant
            ValidateTenantCount(db, contract.Id, isEdit);
        }

        /// <summary>
        /// Xử lý thêm/cập nhật tenant cho hợp đồng Company
        /// </summary>
        public static void ProcessCompanyContractTenants(
            NhaTroAnCuEntities db,
            Contract contract,
            List<TenantViewModel> tenantModels,
            HttpRequestBase request)
        {
            if (tenantModels == null || !tenantModels.Any())
            {
                return;
            }

            foreach (var tenantModel in tenantModels)
            {
                if (string.IsNullOrEmpty(tenantModel.FullName) ||
                    string.IsNullOrEmpty(tenantModel.IdentityCard))
                {
                    continue;
                }

                // Với Company, cần xác định RoomId cho tenant
                // TenantViewModel.Id có thể được dùng làm RoomId
                if (!tenantModel.Id.HasValue)
                {
                    continue;
                }

                var roomId = tenantModel.Id.Value;

                // Kiểm tra room thuộc contract
                if (!contract.ContractRooms.Any(cr => cr.RoomId == roomId))
                {
                    continue;
                }

                // Kiểm tra giới hạn 4 người/phòng
                var currentCount = db.ContractTenants
                    .Count(ct => ct.ContractId == contract.Id && ct.RoomId == roomId);

                if (currentCount >= 4)
                {
                    continue;
                }

                var tenant = ProcessSingleTenant(
                    db,
                    tenantModel,
                    contract.CompanyId,
                    request,
                    tenantModels.IndexOf(tenantModel)
                );

                if (tenant != null)
                {
                    // Đảm bảo tenant được lưu
                    db.SaveChanges();

                    // Thêm vào ContractTenant nếu chưa có
                    EnsureContractTenantExists(db, contract.Id, tenant.Id, roomId);
                }
            }
        }

        /// <summary>
        /// Xử lý một tenant từ Tenant model (cho Create)
        /// </summary>
        private static Tenant ProcessSingleTenantFromModel(
            NhaTroAnCuEntities db,
            Tenant model,
            int? companyId,
            HttpRequestBase request,
            int tenantIndex)
        {
            Tenant tenant = null;

            // Tìm tenant existing theo IdentityCard
            if (!string.IsNullOrEmpty(model.IdentityCard))
            {
                tenant = db.Tenants.FirstOrDefault(t => t.IdentityCard == model.IdentityCard);
            }

            if (tenant != null)
            {
                // Cập nhật thông tin tenant existing
                tenant.FullName = model.FullName;
                tenant.PhoneNumber = model.PhoneNumber;
                tenant.BirthDate = model.BirthDate;
                tenant.Gender = model.Gender;
                tenant.PermanentAddress = model.PermanentAddress;
                tenant.Ethnicity = model.Ethnicity;
                tenant.VehiclePlate = model.VehiclePlate;

                if (companyId.HasValue)
                {
                    tenant.CompanyId = companyId;
                }
            }
            else
            {
                // Tạo tenant mới
                tenant = new Tenant
                {
                    FullName = model.FullName,
                    IdentityCard = model.IdentityCard,
                    PhoneNumber = model.PhoneNumber,
                    BirthDate = model.BirthDate,
                    Gender = model.Gender,
                    PermanentAddress = model.PermanentAddress,
                    Ethnicity = model.Ethnicity,
                    VehiclePlate = model.VehiclePlate,
                    CompanyId = companyId
                };
                db.Tenants.Add(tenant);
            }

            // Xử lý upload ảnh
            ProcessTenantPhoto(tenant, request, tenantIndex);

            return tenant;
        }
        private static Tenant ProcessSingleTenant(
            NhaTroAnCuEntities db,
            TenantViewModel model,
            int? companyId,
            HttpRequestBase request,
            int tenantIndex)
        {
            Tenant tenant = null;

            // Tìm tenant existing
            if (model.TenantId > 0)
            {
                tenant = db.Tenants.Find(model.TenantId);
            }
            else if (!string.IsNullOrEmpty(model.IdentityCard))
            {
                tenant = db.Tenants.FirstOrDefault(t => t.IdentityCard == model.IdentityCard);
            }

            if (tenant != null)
            {
                // Cập nhật thông tin tenant existing
                UpdateTenantInfo(tenant, model, companyId);
            }
            else
            {
                // Tạo tenant mới
                tenant = CreateNewTenant(model, companyId);
                db.Tenants.Add(tenant);
            }

            // Xử lý upload ảnh
            ProcessTenantPhoto(tenant, request, tenantIndex);

            return tenant;
        }

        /// <summary>
        /// Cập nhật thông tin tenant
        /// </summary>
        private static void UpdateTenantInfo(Tenant tenant, TenantViewModel model, int? companyId)
        {
            tenant.FullName = model.FullName;
            tenant.PhoneNumber = model.PhoneNumber;
            tenant.BirthDate = model.BirthDate;
            tenant.Gender = model.Gender;
            tenant.PermanentAddress = model.PermanentAddress;
            tenant.Ethnicity = model.Ethnicity;
            tenant.VehiclePlate = model.VehiclePlate;

            // Cập nhật CompanyId nếu cần
            if (companyId.HasValue)
            {
                tenant.CompanyId = companyId;
            }
        }

        /// <summary>
        /// Tạo tenant mới
        /// </summary>
        private static Tenant CreateNewTenant(TenantViewModel model, int? companyId)
        {
            return new Tenant
            {
                FullName = model.FullName,
                IdentityCard = model.IdentityCard,
                PhoneNumber = model.PhoneNumber,
                BirthDate = model.BirthDate,
                Gender = model.Gender,
                PermanentAddress = model.PermanentAddress,
                Ethnicity = model.Ethnicity,
                VehiclePlate = model.VehiclePlate,
                CompanyId = companyId
            };
        }

        /// <summary>
        /// Xử lý upload ảnh tenant
        /// </summary>
        private static void ProcessTenantPhoto(Tenant tenant, HttpRequestBase request, int tenantIndex)
        {
            if (tenant == null || request == null || request.Files == null)
            {
                return;
            }

            var photoKey = $"TenantPhotos[{tenantIndex}]";
            var photoFile = request.Files[photoKey];

            if (photoFile != null && photoFile.ContentLength > 0)
            {
                try
                {
                    // Xóa ảnh cũ nếu có
                    if (!string.IsNullOrEmpty(tenant.Photo))
                    {
                        TenantPhotoHelper.DeleteTenantPhoto(tenant.Photo);
                    }

                    // Lưu ảnh mới
                    tenant.Photo = TenantPhotoHelper.SaveTenantPhoto(photoFile, tenant.IdentityCard);
                }
                catch (Exception ex)
                {
                    // Log error nhưng không throw để không break process
                    System.Diagnostics.Debug.WriteLine($"Error uploading photo for {tenant.FullName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Đảm bảo ContractTenant tồn tại
        /// </summary>
        private static void EnsureContractTenantExists(
            NhaTroAnCuEntities db,
            int contractId,
            int tenantId,
            int roomId)
        {
            var exists = db.ContractTenants
                .Any(ct => ct.ContractId == contractId &&
                          ct.TenantId == tenantId &&
                          ct.RoomId == roomId);

            if (!exists)
            {
                var contractTenant = new ContractTenant
                {
                    ContractId = contractId,
                    TenantId = tenantId,
                    RoomId = roomId,
                    CreatedAt = DateTime.Now
                };
                db.ContractTenants.Add(contractTenant);
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Xóa những tenant đã bị remove từ UI (chỉ cho Edit)
        /// </summary>
        private static void RemoveDeletedTenants(
            NhaTroAnCuEntities db,
            Contract contract,
            List<TenantViewModel> currentTenants)
        {
            // Lấy danh sách tenant IDs còn lại từ UI
            var submittedTenantIds = currentTenants
                .Where(t => t.TenantId > 0)
                .Select(t => t.TenantId)
                .ToList();

            // Lấy danh sách ContractTenant hiện tại trong DB
            var existingContractTenants = db.ContractTenants
                .Where(ct => ct.ContractId == contract.Id)
                .ToList();

            // Tìm những tenant bị xóa
            var tenantsToRemove = existingContractTenants
                .Where(ct => !submittedTenantIds.Contains(ct.TenantId))
                .ToList();

            foreach (var contractTenant in tenantsToRemove)
            {
                // Xóa quan hệ ContractTenant
                db.ContractTenants.Remove(contractTenant);

                // Lấy tenant từ database
                var tenant = db.Tenants.Find(contractTenant.TenantId);

                if (tenant != null)
                {
                    // Kiểm tra tenant có hợp đồng khác không
                    var hasOtherContracts = db.ContractTenants
                        .Any(ct => ct.TenantId == tenant.Id &&
                                  ct.ContractId != contract.Id);

                    // Nếu không có hợp đồng khác, xóa tenant và ảnh
                    if (!hasOtherContracts)
                    {
                        // Xóa ảnh nếu có
                        if (!string.IsNullOrEmpty(tenant.Photo))
                        {
                            TenantPhotoHelper.DeleteTenantPhoto(tenant.Photo);
                        }

                        // Xóa tenant
                        db.Tenants.Remove(tenant);
                    }
                }
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Validate số lượng tenant trong contract
        /// </summary>
        private static void ValidateTenantCount(
            NhaTroAnCuEntities db,
            int contractId,
            bool isEdit)
        {
            var tenantCount = db.ContractTenants.Count(ct => ct.ContractId == contractId);

            if (tenantCount == 0)
            {
                throw new InvalidOperationException(
                    isEdit
                        ? "Sau khi chỉnh sửa, hợp đồng phải có ít nhất 1 người thuê!"
                        : "Hợp đồng phải có ít nhất 1 người thuê!"
                );
            }
        }

        /// <summary>
        /// Thêm một tenant vào Company contract (dùng cho AJAX)
        /// </summary>
        public static Tenant AddCompanyTenant(
            NhaTroAnCuEntities db,
            int contractId,
            int roomId,
            TenantViewModel model,
            HttpPostedFileBase photoFile = null)
        {
            // Validate contract
            var contract = db.Contracts
                .Include(c => c.ContractRooms)
                .FirstOrDefault(c => c.Id == contractId && c.ContractType == "Company");

            if (contract == null)
            {
                throw new InvalidOperationException("Hợp đồng không tồn tại hoặc không phải loại Company");
            }

            // Validate room
            if (!contract.ContractRooms.Any(cr => cr.RoomId == roomId))
            {
                throw new InvalidOperationException("Phòng không thuộc hợp đồng này");
            }

            // Check room capacity
            var currentCount = db.ContractTenants
                .Count(ct => ct.ContractId == contractId && ct.RoomId == roomId);

            if (currentCount >= 4)
            {
                throw new InvalidOperationException("Phòng đã đạt giới hạn 4 người");
            }

            // Process tenant
            var existingTenant = db.Tenants
                .FirstOrDefault(t => t.IdentityCard == model.IdentityCard);

            Tenant tenant;
            if (existingTenant != null)
            {
                tenant = existingTenant;
                UpdateTenantInfo(tenant, model, contract.CompanyId);
            }
            else
            {
                tenant = CreateNewTenant(model, contract.CompanyId);

                // Xử lý upload ảnh nếu có
                if (photoFile != null && photoFile.ContentLength > 0)
                {
                    tenant.Photo = TenantPhotoHelper.SaveTenantPhoto(photoFile, tenant.IdentityCard);
                }

                db.Tenants.Add(tenant);
            }

            db.SaveChanges();

            // Thêm ContractTenant
            EnsureContractTenantExists(db, contractId, tenant.Id, roomId);

            return tenant;
        }

        /// <summary>
        /// Xóa một tenant khỏi contract (dùng cho AJAX)
        /// </summary>
        public static bool RemoveContractTenant(
            NhaTroAnCuEntities db,
            int contractId,
            int tenantId,
            int? roomId = null)
        {
            // Tìm ContractTenant
            var query = db.ContractTenants
                .Where(ct => ct.ContractId == contractId && ct.TenantId == tenantId);

            if (roomId.HasValue)
            {
                query = query.Where(ct => ct.RoomId == roomId.Value);
            }

            var contractTenant = query.FirstOrDefault();

            if (contractTenant == null)
            {
                return false;
            }

            // Kiểm tra không phải tenant cuối cùng của contract
            var remainingCount = db.ContractTenants
                .Count(ct => ct.ContractId == contractId && ct.Id != contractTenant.Id);

            if (remainingCount == 0)
            {
                throw new InvalidOperationException(
                    "Không thể xóa người thuê cuối cùng trong hợp đồng. Vui lòng kết thúc hợp đồng nếu cần."
                );
            }

            // Xóa ContractTenant
            db.ContractTenants.Remove(contractTenant);

            // Kiểm tra và xóa Tenant nếu không có hợp đồng khác
            var hasOtherContracts = db.ContractTenants
                .Any(ct => ct.TenantId == tenantId && ct.ContractId != contractId);

            if (!hasOtherContracts)
            {
                var tenant = db.Tenants.Find(tenantId);
                if (tenant != null)
                {
                    // Xóa ảnh
                    if (!string.IsNullOrEmpty(tenant.Photo))
                    {
                        TenantPhotoHelper.DeleteTenantPhoto(tenant.Photo);
                    }

                    db.Tenants.Remove(tenant);
                }
            }

            db.SaveChanges();
            return true;
        }

        /// <summary>
        /// Lấy danh sách tenant của một contract
        /// </summary>
        public static List<TenantViewModel> GetContractTenants(
            NhaTroAnCuEntities db,
            int contractId,
            int? roomId = null)
        {
            var query = db.ContractTenants
                .Include(ct => ct.Tenant)
                .Where(ct => ct.ContractId == contractId);

            if (roomId.HasValue)
            {
                query = query.Where(ct => ct.RoomId == roomId.Value);
            }

            return query.Select(ct => new TenantViewModel
            {
                Id = ct.Id,
                TenantId = ct.TenantId,
                FullName = ct.Tenant.FullName,
                IdentityCard = ct.Tenant.IdentityCard,
                PhoneNumber = ct.Tenant.PhoneNumber,
                BirthDate = ct.Tenant.BirthDate,
                Gender = ct.Tenant.Gender,
                PermanentAddress = ct.Tenant.PermanentAddress,
                Ethnicity = ct.Tenant.Ethnicity,
                VehiclePlate = ct.Tenant.VehiclePlate,
                Photo = ct.Tenant.Photo,
                JoinDate = ct.CreatedAt
            }).ToList();
        }
    }
}