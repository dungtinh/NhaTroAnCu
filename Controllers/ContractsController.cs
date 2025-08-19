using Microsoft.AspNet.Identity;
using NhaTroAnCu.Helpers;
using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace NhaTroAnCu.Controllers
{
    public class ContractsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: /Contracts
        public ActionResult Index()
        {
            var contracts = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.Company)
                .OrderByDescending(c => c.StartDate)
                .ToList();

            return View(contracts);
        }

        public ActionResult Create(int? roomId)
        {
            var model = new ContractCreateViewModel
            {
                MoveInDate = DateTime.Now,
                StartDate = DateTime.Now,
                ElectricityPrice = 3500,
                WaterPrice = 15000,
                Months = 12,
                ContractType = roomId.HasValue ? "Individual" : null,
                SelectedRooms = new List<RoomSelectionModel>() // Khởi tạo list
            };

            if (roomId.HasValue)
            {
                var room = db.Rooms.Find(roomId.Value);
                if (room != null)
                {
                    model.SingleRoomId = roomId;
                    model.SingleRoomPrice = room.DefaultPrice;
                }
            }

            // Load danh sách phòng trống - Đảm bảo là List
            var availableRooms = db.Rooms
                .Where(r => !r.IsOccupied)
                .Select(r => new RoomSelectionModel
                {
                    RoomId = r.Id,
                    RoomName = r.Name,
                    DefaultPrice = r.DefaultPrice,
                    AgreedPrice = r.DefaultPrice,
                    IsSelected = false
                })
                .ToList(); // Chắc chắn convert to List

            ViewBag.AvailableRooms = availableRooms;

            return View(model);
        }

        // POST: /Contracts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ContractCreateViewModel model)
        {
            if (Request.Form["StartDate"] != null)
            {
                var startDateStr = Request.Form["StartDate"];
                var parsedDate = DateTimeHelper.ParseDate(startDateStr);
                if (parsedDate.HasValue)
                    model.StartDate = parsedDate.Value;
            }

            if (Request.Form["MoveInDate"] != null)
            {
                var moveInDateStr = Request.Form["MoveInDate"];
                var parsedDate = DateTimeHelper.ParseDate(moveInDateStr);
                if (parsedDate.HasValue)
                    model.MoveInDate = parsedDate.Value;
            }
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    Contract contract = new Contract
                    {
                        ContractType = model.ContractType,
                        StartDate = model.StartDate,
                        EndDate = model.StartDate.AddMonths(model.Months),
                        MoveInDate = model.MoveInDate,
                        ElectricityPrice = model.ElectricityPrice,
                        WaterPrice = model.WaterPrice,
                        Note = model.Note,
                        Status = "Active"
                    };

                    if (model.ContractType == "Company")
                    {
                        // Xử lý hợp đồng công ty
                        contract = CreateCompanyContract(model, contract);
                    }
                    else
                    {
                        // Xử lý hợp đồng cá nhân
                        contract = CreateIndividualContract(model, contract);
                    }

                    transaction.Commit();
                    return RedirectToAction("Details", new { id = contract.Id });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);

                    // Reload data for view
                    ViewBag.AvailableRooms = db.Rooms
                        .Where(r => !r.IsOccupied)
                        .Select(r => new RoomSelectionModel
                        {
                            RoomId = r.Id,
                            RoomName = r.Name,
                            DefaultPrice = r.DefaultPrice,
                            AgreedPrice = r.DefaultPrice
                        })
                        .ToList();

                    return View(model);
                }
            }
        }

        // Xử lý tạo hợp đồng công ty
        private Contract CreateCompanyContract(ContractCreateViewModel model, Contract contract)
        {
            // Debug logging
            System.Diagnostics.Debug.WriteLine($"SelectedRooms count: {model.SelectedRooms?.Count ?? 0}");

            // 1. Tạo hoặc cập nhật thông tin công ty
            Company company;
            var existingCompany = db.Companies
                .FirstOrDefault(c => c.TaxCode == model.Company.TaxCode);

            if (existingCompany != null)
            {
                company = existingCompany;
                // Update company info
                company.CompanyName = model.Company.CompanyName;
                company.Address = model.Company.Address;
                company.Representative = model.Company.Representative;
                company.RepresentativePhone = model.Company.RepresentativePhone;
                company.Email = model.Company.Email;
                company.UpdatedAt = DateTime.Now;
            }
            else
            {
                company = new Company
                {
                    CompanyName = model.Company.CompanyName,
                    TaxCode = model.Company.TaxCode,
                    Address = model.Company.Address,
                    Email = model.Company.Email,
                    Representative = model.Company.Representative,
                    RepresentativePhone = model.Company.RepresentativePhone,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };
                db.Companies.Add(company);
            }

            db.SaveChanges();
            contract.CompanyId = company.Id;

            // 2. Lưu contract
            contract.PriceAgreed = 0;
            db.Contracts.Add(contract);
            db.SaveChanges();

            // 3. Tạo ContractRooms cho các phòng được chọn
            decimal totalPrice = 0;
            int roomCount = 0;

            if (model.SelectedRooms != null && model.SelectedRooms.Any())
            {
                foreach (var roomSelection in model.SelectedRooms.Where(r => r.IsSelected))
                {
                    // Debug log
                    System.Diagnostics.Debug.WriteLine($"Processing room: {roomSelection.RoomId}, Price: {roomSelection.AgreedPrice}");

                    // Kiểm tra phòng tồn tại
                    var room = db.Rooms.Find(roomSelection.RoomId);
                    if (room == null)
                    {
                        throw new Exception($"Phòng với ID {roomSelection.RoomId} không tồn tại");
                    }

                    // Kiểm tra phòng còn trống
                    if (room.IsOccupied)
                    {
                        throw new Exception($"Phòng {room.Name} đã được thuê");
                    }

                    var contractRoom = new ContractRoom
                    {
                        ContractId = contract.Id,
                        RoomId = roomSelection.RoomId,
                        PriceAgreed = roomSelection.AgreedPrice > 0 ? roomSelection.AgreedPrice : room.DefaultPrice,
                        Notes = roomSelection.Notes
                    };

                    db.ContractRooms.Add(contractRoom);
                    totalPrice += contractRoom.PriceAgreed;
                    roomCount++;

                    // Cập nhật trạng thái phòng
                    room.IsOccupied = true;
                }
            }

            // Kiểm tra có phòng nào được chọn không
            if (roomCount == 0)
            {
                throw new Exception("Vui lòng chọn ít nhất một phòng cho hợp đồng công ty");
            }

            // Cập nhật tổng giá trị hợp đồng
            contract.PriceAgreed = totalPrice;

            db.SaveChanges();
            return contract;
        }

        // Xử lý tạo hợp đồng cá nhân
        private Contract CreateIndividualContract(ContractCreateViewModel model, Contract contract)
        {
            // 1. Kiểm tra phòng
            if (!model.SingleRoomId.HasValue)
            {
                throw new Exception("Vui lòng chọn phòng cho hợp đồng cá nhân");
            }

            var room = db.Rooms.Find(model.SingleRoomId.Value);
            if (room == null)
            {
                throw new Exception("Phòng không tồn tại");
            }

            // 2. Set giá cho contract
            contract.PriceAgreed = model.SingleRoomPrice ?? room.DefaultPrice;

            // 3. Lưu contract
            db.Contracts.Add(contract);
            db.SaveChanges();

            // 4. Tạo ContractRoom
            var contractRoom = new ContractRoom
            {
                ContractId = contract.Id,
                RoomId = room.Id,
                PriceAgreed = contract.PriceAgreed,
                Notes = model.Note
            };
            db.ContractRooms.Add(contractRoom);

            // 5. Tạo Tenant nếu có thông tin
            // Xử lý danh sách người thuê
            if (model.Tenants != null && model.Tenants.Any())
            {
                foreach (var tenantModel in model.Tenants)
                {
                    if (string.IsNullOrEmpty(tenantModel.FullName) || string.IsNullOrEmpty(tenantModel.IdentityCard))
                    {
                        continue;
                    }

                    var existingTenant = db.Tenants
                        .FirstOrDefault(t => t.IdentityCard == tenantModel.IdentityCard);

                    Tenant tenant;
                    if (existingTenant != null)
                    {
                        tenant = existingTenant;
                        tenant.FullName = tenantModel.FullName;
                        tenant.PhoneNumber = tenantModel.PhoneNumber;
                        tenant.BirthDate = tenantModel.BirthDate;
                        tenant.Gender = tenantModel.Gender;
                        tenant.PermanentAddress = tenantModel.PermanentAddress;
                        tenant.Ethnicity = tenantModel.Ethnicity;
                        tenant.VehiclePlate = tenantModel.VehiclePlate;
                    }
                    else
                    {
                        tenant = new Tenant
                        {
                            FullName = tenantModel.FullName,
                            IdentityCard = tenantModel.IdentityCard,
                            PhoneNumber = tenantModel.PhoneNumber,
                            BirthDate = tenantModel.BirthDate,
                            Gender = tenantModel.Gender,
                            PermanentAddress = tenantModel.PermanentAddress,
                            Ethnicity = tenantModel.Ethnicity,
                            VehiclePlate = tenantModel.VehiclePlate,
                            CompanyId = null
                        };

                        // Sử dụng TenantPhotoHelper để xử lý upload ảnh
                        var tenantIndex = model.Tenants.IndexOf(tenantModel);
                        var photoKey = $"TenantPhotos[{tenantIndex}]";

                        if (Request.Files[photoKey] != null)
                        {
                            var photoFile = Request.Files[photoKey];
                            try
                            {
                                tenant.Photo = TenantPhotoHelper.SaveTenantPhoto(photoFile, tenant.IdentityCard);
                            }
                            catch (Exception ex)
                            {
                                // Log error hoặc thêm vào ModelState
                                ModelState.AddModelError("", $"Lỗi upload ảnh cho {tenant.FullName}: {ex.Message}");
                            }
                        }

                        db.Tenants.Add(tenant);
                    }

                    db.SaveChanges();

                    var contractTenant = new ContractTenant
                    {
                        ContractId = contract.Id,
                        TenantId = tenant.Id,
                        RoomId = room.Id,
                        CreatedAt = DateTime.Now
                    };
                    db.ContractTenants.Add(contractTenant);
                }
            }

            // Cập nhật trạng thái phòng
            room.IsOccupied = true;
            db.SaveChanges();

            // 7. Cập nhật trạng thái phòng
            room.IsOccupied = true;

            db.SaveChanges();
            return contract;
        }


        // GET: /Contracts/Details/5
        public ActionResult Details(int id)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .Include(c => c.ContractExtensionHistories)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null) return HttpNotFound();

            return View(contract);
        }

        // GET: /Contracts/End/5
        public ActionResult End(int id)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null) return HttpNotFound();

            ViewBag.Contract = contract;
            return View();
        }

        // POST: /Contracts/EndConfirm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EndConfirm(int id, decimal? refundAmount, string refundNote)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == id);

            if (contract == null) return HttpNotFound();

            contract.Status = "Ended";
            contract.EndDate = DateTime.Now;

            // Cập nhật tất cả các phòng trong hợp đồng
            foreach (var contractRoom in contract.ContractRooms)
            {
                contractRoom.Room.IsOccupied = false;
            }

            // Ghi nhận trả cọc nếu có
            if (refundAmount.HasValue && refundAmount.Value > 0)
            {
                var expenseCategory = db.IncomeExpenseCategories
                    .FirstOrDefault(c => c.Name == "Trả tiền cọc" && c.IsSystem);

                if (expenseCategory == null)
                {
                    expenseCategory = new IncomeExpenseCategory
                    {
                        Name = "Trả tiền cọc",
                        Type = "Expense",
                        IsSystem = true,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    db.IncomeExpenseCategories.Add(expenseCategory);
                    db.SaveChanges();
                }

                var incomeExpense = new IncomeExpense
                {
                    CategoryId = expenseCategory.Id,
                    ContractId = id,
                    Amount = refundAmount.Value,
                    TransactionDate = DateTime.Now.Date,
                    Description = refundNote ?? $"Trả tiền cọc khi kết thúc hợp đồng",
                    ReferenceNumber = $"REFUND-{id}",
                    CreatedBy = User.Identity.GetUserId(),
                    CreatedAt = DateTime.Now
                };

                db.IncomeExpenses.Add(incomeExpense);
            }

            db.SaveChanges();

            // Redirect về phòng đầu tiên trong hợp đồng
            var firstRoomId = contract.ContractRooms.FirstOrDefault()?.RoomId ?? 0;
            return RedirectToAction("Details", "Rooms", new { id = firstRoomId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db?.Dispose();
            }
            base.Dispose(disposing);
        }
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Contract contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            // Create ViewModel
            var model = new ContractEditViewModel
            {
                Id = contract.Id,
                ContractType = contract.ContractType,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                MoveInDate = contract.MoveInDate,
                ElectricityPrice = contract.ElectricityPrice,
                WaterPrice = contract.WaterPrice,
                Note = contract.Note,
                Status = contract.Status,
                ContractScanFilePath = contract.ContractScanFilePath,
                CompanyId = contract.CompanyId,
                CompanyName = contract.Company?.CompanyName
            };

            if (contract.ContractType == "Company")
            {
                // Load rooms for company contract
                model.SelectedRooms = contract.ContractRooms.Select(cr => new RoomSelectionModel
                {
                    RoomId = cr.RoomId,
                    RoomName = cr.Room.Name,
                    DefaultPrice = cr.Room.DefaultPrice,
                    AgreedPrice = cr.PriceAgreed
                }).ToList();
            }
            else // Individual contract
            {
                // Get room and price for individual contract
                var contractRoom = contract.ContractRooms.FirstOrDefault();
                if (contractRoom != null)
                {
                    model.RoomId = contractRoom.RoomId;
                    model.PriceAgreed = contractRoom.PriceAgreed;
                }

                // Load tenants
                model.Tenants = contract.ContractTenants.Select(ct => new TenantViewModel
                {
                    Id = ct.TenantId,
                    FullName = ct.Tenant.FullName,
                    IdentityCard = ct.Tenant.IdentityCard,
                    PhoneNumber = ct.Tenant.PhoneNumber,
                    BirthDate = ct.Tenant.BirthDate,
                    Gender = ct.Tenant.Gender,
                    Ethnicity = ct.Tenant.Ethnicity,
                    PermanentAddress = ct.Tenant.PermanentAddress,
                    VehiclePlate = ct.Tenant.VehiclePlate
                }).ToList();

                // Load available rooms for dropdown
                var occupiedRoomIds = db.ContractRooms
                    .Where(cr => cr.Contract.Status == "Active" && cr.ContractId != id)
                    .Select(cr => cr.RoomId)
                    .ToList();

                ViewBag.AvailableRooms = db.Rooms
                    .Where(r => !occupiedRoomIds.Contains(r.Id) || r.Id == model.RoomId)
                    .Select(r => new SelectListItem
                    {
                        Value = r.Id.ToString(),
                        Text = r.Name + " - " + r.DefaultPrice.ToString("N0") + " VNĐ"
                    })
                    .ToList();
            }

            return View(model);
        }

        // POST: Contracts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ContractEditViewModel model, HttpPostedFileBase ContractScanFile)
        {
            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        // Get existing contract
                        var contract = db.Contracts
                            .Include(c => c.ContractRooms)
                            .Include(c => c.ContractTenants)
                            .FirstOrDefault(c => c.Id == model.Id);

                        if (contract == null)
                        {
                            return HttpNotFound();
                        }

                        // Update basic contract information
                        contract.StartDate = model.StartDate;
                        contract.EndDate = model.EndDate;
                        contract.MoveInDate = model.MoveInDate;
                        contract.ElectricityPrice = model.ElectricityPrice;
                        contract.WaterPrice = model.WaterPrice;
                        contract.Note = model.Note;
                        contract.Status = model.Status;

                        // Handle file upload
                        if (ContractScanFile != null && ContractScanFile.ContentLength > 0)
                        {
                            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                            var extension = Path.GetExtension(ContractScanFile.FileName).ToLower();

                            if (!allowedExtensions.Contains(extension))
                            {
                                ModelState.AddModelError("", "Chỉ chấp nhận file PDF, JPG hoặc PNG");
                                transaction.Rollback();
                                return View(model);
                            }

                            if (ContractScanFile.ContentLength > 5 * 1024 * 1024) // 5MB
                            {
                                ModelState.AddModelError("", "File không được vượt quá 5MB");
                                transaction.Rollback();
                                return View(model);
                            }

                            // Delete old file if exists
                            if (!string.IsNullOrEmpty(contract.ContractScanFilePath))
                            {
                                var oldFilePath = Server.MapPath(contract.ContractScanFilePath);
                                if (System.IO.File.Exists(oldFilePath))
                                {
                                    System.IO.File.Delete(oldFilePath);
                                }
                            }

                            // Save new file
                            var fileName = $"Contract_{model.Id}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                            var uploadPath = Server.MapPath("~/Uploads/Contracts");

                            if (!Directory.Exists(uploadPath))
                            {
                                Directory.CreateDirectory(uploadPath);
                            }

                            var filePath = Path.Combine(uploadPath, fileName);
                            ContractScanFile.SaveAs(filePath);
                            contract.ContractScanFilePath = $"/Uploads/Contracts/{fileName}";
                        }

                        if (contract.ContractType == "Company")
                        {
                            // Update rooms for company contract
                            UpdateCompanyContractRooms(contract, model);
                        }
                        else // Individual contract
                        {
                            // Update room for individual contract
                            UpdateIndividualContractRoom(contract, model);

                            // Update tenants
                            UpdateContractTenants(contract, model);
                        }

                        db.SaveChanges();
                        transaction.Commit();

                        TempData["Success"] = "Cập nhật hợp đồng thành công!";
                        return RedirectToAction("Details", new { id = contract.Id });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                    }
                }
            }

            // Reload data for view if validation fails
            if (model.ContractType == "Individual")
            {
                var occupiedRoomIds = db.ContractRooms
                    .Where(cr => cr.Contract.Status == "Active" && cr.ContractId != model.Id)
                    .Select(cr => cr.RoomId)
                    .ToList();

                ViewBag.AvailableRooms = db.Rooms
                    .Where(r => !occupiedRoomIds.Contains(r.Id) || r.Id == model.RoomId)
                    .Select(r => new SelectListItem
                    {
                        Value = r.Id.ToString(),
                        Text = r.Name + " - " + r.DefaultPrice.ToString("N0") + " VNĐ"
                    })
                    .ToList();
            }

            return View(model);
        }

        // Helper method to update rooms for company contract
        private void UpdateCompanyContractRooms(Contract contract, ContractEditViewModel model)
        {
            // Remove old contract rooms
            db.ContractRooms.RemoveRange(contract.ContractRooms);

            // Add new contract rooms
            if (model.SelectedRooms != null && model.SelectedRooms.Any())
            {
                foreach (var room in model.SelectedRooms)
                {
                    var contractRoom = new ContractRoom
                    {
                        ContractId = contract.Id,
                        RoomId = room.RoomId,
                        PriceAgreed = room.AgreedPrice,
                        Notes = ""
                    };
                    db.ContractRooms.Add(contractRoom);

                    // Update room status
                    var roomEntity = db.Rooms.Find(room.RoomId);
                    if (roomEntity != null)
                    {
                        roomEntity.IsOccupied = contract.Status == "Active";
                    }
                }

                // Calculate total price for company contract
                contract.PriceAgreed = model.SelectedRooms.Sum(r => r.AgreedPrice);
            }
        }

        // Helper method to update room for individual contract
        private void UpdateIndividualContractRoom(Contract contract, ContractEditViewModel model)
        {
            // Remove old contract room
            db.ContractRooms.RemoveRange(contract.ContractRooms);

            if (model.RoomId.HasValue && model.PriceAgreed.HasValue)
            {
                // Add new contract room
                var contractRoom = new ContractRoom
                {
                    ContractId = contract.Id,
                    RoomId = model.RoomId.Value,
                    PriceAgreed = model.PriceAgreed.Value,
                    Notes = ""
                };
                db.ContractRooms.Add(contractRoom);

                // Update room status
                var room = db.Rooms.Find(model.RoomId.Value);
                if (room != null)
                {
                    room.IsOccupied = contract.Status == "Active";
                }

                // Update contract price
                contract.PriceAgreed = model.PriceAgreed.Value;
            }
        }

        // Helper method to update tenants
        private void UpdateContractTenants(Contract contract, ContractEditViewModel model)
        {
            // Get existing tenant IDs
            var existingTenantIds = contract.ContractTenants.Select(ct => ct.TenantId).ToList();
            var updatedTenantIds = model.Tenants.Where(t => t.Id.HasValue).Select(t => t.Id.Value).ToList();

            // Remove deleted contract-tenant relationships
            var toRemove = contract.ContractTenants
                .Where(ct => !updatedTenantIds.Contains(ct.TenantId))
                .ToList();

            foreach (var ct in toRemove)
            {
                db.ContractTenants.Remove(ct);

                // Also remove the tenant if not associated with other contracts
                var tenant = db.Tenants.Find(ct.TenantId);
                if (tenant != null)
                {
                    var hasOtherContracts = db.ContractTenants
                        .Any(c => c.TenantId == tenant.Id && c.ContractId != contract.Id);

                    if (!hasOtherContracts)
                    {
                        db.Tenants.Remove(tenant);
                    }
                }
            }

            // Update existing and add new tenants
            foreach (var tenantModel in model.Tenants)
            {
                Tenant tenant;

                if (tenantModel.Id.HasValue)
                {
                    // Update existing tenant
                    tenant = db.Tenants.Find(tenantModel.Id.Value);
                    if (tenant != null)
                    {
                        tenant.FullName = tenantModel.FullName;
                        tenant.IdentityCard = tenantModel.IdentityCard;
                        tenant.PhoneNumber = tenantModel.PhoneNumber;
                        tenant.BirthDate = tenantModel.BirthDate;
                        tenant.Gender = tenantModel.Gender;
                        tenant.Ethnicity = tenantModel.Ethnicity;
                        tenant.PermanentAddress = tenantModel.PermanentAddress;
                        tenant.VehiclePlate = tenantModel.VehiclePlate;
                    }
                }
                else
                {
                    // Create new tenant
                    tenant = new Tenant
                    {
                        FullName = tenantModel.FullName,
                        IdentityCard = tenantModel.IdentityCard,
                        PhoneNumber = tenantModel.PhoneNumber,
                        BirthDate = tenantModel.BirthDate,
                        Gender = tenantModel.Gender,
                        Ethnicity = tenantModel.Ethnicity,
                        PermanentAddress = tenantModel.PermanentAddress,
                        VehiclePlate = tenantModel.VehiclePlate
                    };
                    db.Tenants.Add(tenant);
                    db.SaveChanges(); // Save to get the ID

                    // Create contract-tenant relationship
                    var contractTenant = new ContractTenant
                    {
                        ContractId = contract.Id,
                        TenantId = tenant.Id,
                        RoomId = model.RoomId.Value
                    };
                    db.ContractTenants.Add(contractTenant);
                }
            }
        }

        // AJAX method to get available rooms
        public JsonResult GetAvailableRooms(int? excludeContractId = null)
        {
            var occupiedRoomIds = db.ContractRooms
                .Where(cr => cr.Contract.Status == "Active" && cr.ContractId != excludeContractId)
                .Select(cr => cr.RoomId)
                .ToList();

            var availableRooms = db.Rooms
                .Where(r => !occupiedRoomIds.Contains(r.Id))
                .Select(r => new
                {
                    Id = r.Id,
                    Name = r.Name,
                    DefaultPrice = r.DefaultPrice
                })
                .ToList();

            return Json(availableRooms, JsonRequestBehavior.AllowGet);
        }
    }
}