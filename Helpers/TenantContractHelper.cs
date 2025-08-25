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
    /// Updated: Hỗ trợ roomId parameter cho hợp đồng công ty
    /// </summary>
    public static class TenantContractHelper
    {
        /// <summary>
        /// Xử lý thêm/cập nhật tenant cho hợp đồng công ty với roomId cụ thể
        /// </summary>
        public static void ProcessCompanyContractTenants(
            NhaTroAnCuEntities db,
            Contract contract,
            int roomId,
            List<TenantViewModel> tenantModels,
            HttpRequestBase request,
            bool isEdit = false)
        {
            // Validate room belongs to contract
            if (!contract.ContractRooms.Any(cr => cr.RoomId == roomId))
            {
                throw new InvalidOperationException($"Phòng {roomId} không thuộc hợp đồng {contract.Id}");
            }

            ProcessTenants(db, contract, roomId, tenantModels, request, isEdit);
        }

        /// <summary>
        /// Xử lý thêm/cập nhật tenant cho hợp đồng Individual (1 phòng)
        /// </summary>
        public static void ProcessIndividualContractTenants(
            NhaTroAnCuEntities db,
            Contract contract,
            List<TenantViewModel> tenantModels,
            HttpRequestBase request,
            bool isEdit = false)
        {
            // Với hợp đồng cá nhân, chỉ có 1 phòng
            var roomId = contract.ContractRooms.FirstOrDefault()?.RoomId;
            if (!roomId.HasValue)
            {
                throw new InvalidOperationException("Không tìm thấy phòng cho hợp đồng cá nhân");
            }

            ProcessTenants(db, contract, roomId.Value, tenantModels, request, isEdit);
        }

        /// <summary>
        /// Xử lý thêm/cập nhật tenant cho hợp đồng Individual với List<Tenant>
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

            ProcessIndividualContractTenants(db, contract, tenantViewModels, request, isEdit);
        }

        /// <summary>
        /// Core method xử lý tenants với roomId cụ thể
        /// </summary>
        private static void ProcessTenants(
            NhaTroAnCuEntities db,
            Contract contract,
            int roomId,
            List<TenantViewModel> tenantModels,
            HttpRequestBase request,
            bool isEdit = false)
        {
            if (tenantModels == null || !tenantModels.Any())
            {
                if (!isEdit)
                {
                    throw new InvalidOperationException("Vui lòng nhập ít nhất một người thuê");
                }
                return;
            }

            // Nếu là edit mode, xóa các ContractTenant cũ của room này
            if (isEdit)
            {
                var existingContractTenants = db.ContractTenants
                    .Where(ct => ct.ContractId == contract.Id && ct.RoomId == roomId)
                    .ToList();

                db.ContractTenants.RemoveRange(existingContractTenants);
                db.SaveChanges();
            }

            // Process từng tenant
            foreach (var tenantModel in tenantModels)
            {
                if (string.IsNullOrWhiteSpace(tenantModel.FullName) ||
                    string.IsNullOrWhiteSpace(tenantModel.IdentityCard))
                {
                    continue; // Skip empty tenants
                }

                Tenant tenant;

                // Kiểm tra tenant đã tồn tại chưa (theo CCCD)
                tenant = db.Tenants.FirstOrDefault(t => t.IdentityCard == tenantModel.IdentityCard);

                if (tenant != null)
                {
                    // Update thông tin tenant hiện có
                    UpdateTenantInfo(tenant, tenantModel, contract.CompanyId, request);
                }
                else
                {
                    // Tạo tenant mới
                    tenant = CreateNewTenant(tenantModel, contract.CompanyId, request);
                    db.Tenants.Add(tenant);
                }

                db.SaveChanges();

                // Tạo ContractTenant record
                var contractTenant = new ContractTenant
                {
                    ContractId = contract.Id,
                    RoomId = roomId,
                    TenantId = tenant.Id,
                    CreatedAt = DateTime.Now
                };

                db.ContractTenants.Add(contractTenant);
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Update thông tin tenant
        /// </summary>
        private static void UpdateTenantInfo(
            Tenant tenant,
            TenantViewModel model,
            int? companyId,
            HttpRequestBase request)
        {
            tenant.FullName = model.FullName;
            tenant.IdentityCard = model.IdentityCard;
            tenant.PhoneNumber = model.PhoneNumber;
            tenant.BirthDate = model.BirthDate;
            tenant.Gender = model.Gender;
            tenant.PermanentAddress = model.PermanentAddress;
            tenant.Ethnicity = model.Ethnicity;
            tenant.VehiclePlate = model.VehiclePlate;

            // Update CompanyId nếu cần
            if (companyId.HasValue)
            {
                tenant.CompanyId = companyId;
            }

            // Handle photo upload if provided
            if (request?.Files != null && request.Files.Count > 0)
            {
                var photoPath = HandlePhotoUpload(request, tenant.IdentityCard);
                if (!string.IsNullOrEmpty(photoPath))
                {
                    tenant.Photo = photoPath;
                }
            }
        }

        /// <summary>
        /// Tạo tenant mới
        /// </summary>
        private static Tenant CreateNewTenant(
            TenantViewModel model,
            int? companyId,
            HttpRequestBase request)
        {
            var tenant = new Tenant
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

            // Handle photo upload
            if (request?.Files != null && request.Files.Count > 0)
            {
                var photoPath = HandlePhotoUpload(request, tenant.IdentityCard);
                if (!string.IsNullOrEmpty(photoPath))
                {
                    tenant.Photo = photoPath;
                }
            }

            return tenant;
        }

        /// <summary>
        /// Xử lý upload ảnh
        /// </summary>
        private static string HandlePhotoUpload(HttpRequestBase request, string identityCard)
        {
            try
            {
                foreach (string fileName in request.Files)
                {
                    HttpPostedFileBase file = request.Files[fileName];
                    if (file != null && file.ContentLength > 0)
                    {
                        // Generate unique filename
                        var extension = System.IO.Path.GetExtension(file.FileName);
                        var newFileName = $"tenant_{identityCard}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                        var uploadPath = "~/Uploads/Tenants/";
                        var physicalPath = HttpContext.Current.Server.MapPath(uploadPath);

                        // Create directory if not exists
                        if (!System.IO.Directory.Exists(physicalPath))
                        {
                            System.IO.Directory.CreateDirectory(physicalPath);
                        }

                        var fullPath = System.IO.Path.Combine(physicalPath, newFileName);
                        file.SaveAs(fullPath);

                        return uploadPath + newFileName;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw
                System.Diagnostics.Debug.WriteLine($"Error uploading photo: {ex.Message}");
            }

            return null;
        }
    }

    /// <summary>
    /// ViewModel for Tenant data
    /// </summary>
   
}