using Microsoft.AspNet.Identity;
using NhaTroAnCu.Helpers;
using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
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

        // GET: /Contracts/Create
        public ActionResult Create(int? roomId)
        {
            var model = new ContractCreateViewModel
            {
                MoveInDate = DateTime.Now,
                StartDate = DateTime.Now,
                ElectricityPrice = 3500,
                WaterPrice = 15000,
                Months = 12,
                ContractType = roomId.HasValue ? "Individual" : null
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

            // Load danh sách phòng trống
            var availableRooms = db.Rooms
                .Where(r => !r.IsOccupied)
                .Select(r => new RoomSelectionModel
                {
                    RoomId = r.Id,
                    RoomName = r.Name,
                    DefaultPrice = r.DefaultPrice,
                    AgreedPrice = r.DefaultPrice
                })
                .ToList();

            ViewBag.AvailableRooms = availableRooms;

            return View(model);
        }

        // POST: /Contracts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ContractCreateViewModel model)
        {
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
            // 1. Tạo hoặc cập nhật thông tin công ty
            Company company;
            var existingCompany = db.Companies
                .FirstOrDefault(c => c.TaxCode == model.Company.TaxCode);

            if (existingCompany != null)
            {
                company = existingCompany;
                // Cập nhật thông tin công ty
                company.CompanyName = model.Company.CompanyName;
                company.Address = model.Company.Address;
                company.Representative = model.Company.Representative;
                company.RepresentativePhone = model.Company.RepresentativePhone;
                company.Email = model.Company.Email;
                company.BankAccount = model.Company.BankAccount;
                company.BankName = model.Company.BankName;
                company.UpdatedAt = DateTime.Now;
            }
            else
            {
                company = new Company
                {
                    CompanyName = model.Company.CompanyName,
                    TaxCode = model.Company.TaxCode,
                    Address = model.Company.Address,
                    Phone = model.Company.Phone,
                    Email = model.Company.Email,
                    Representative = model.Company.Representative,
                    RepresentativePhone = model.Company.RepresentativePhone,
                    RepresentativeEmail = model.Company.RepresentativeEmail,
                    BankAccount = model.Company.BankAccount,
                    BankName = model.Company.BankName,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };
                db.Companies.Add(company);
            }

            db.SaveChanges();
            contract.CompanyId = company.Id;

            // 2. Lưu contract
            contract.PriceAgreed = 0; // Sẽ tính tổng sau
            db.Contracts.Add(contract);
            db.SaveChanges();

            // 3. Tạo ContractRooms cho các phòng được chọn
            decimal totalPrice = 0;
            if (model.SelectedRooms != null)
            {
                foreach (var roomSelection in model.SelectedRooms.Where(r => r.IsSelected))
                {
                    var contractRoom = new ContractRoom
                    {
                        ContractId = contract.Id,
                        RoomId = roomSelection.RoomId,
                        PriceAgreed = roomSelection.AgreedPrice,
                        Notes = roomSelection.Notes
                    };
                    db.ContractRooms.Add(contractRoom);
                    totalPrice += roomSelection.AgreedPrice;

                    // Cập nhật trạng thái phòng
                    var room = db.Rooms.Find(roomSelection.RoomId);
                    if (room != null)
                    {
                        room.IsOccupied = true;
                    }
                }
            }

            // Cập nhật tổng giá trị hợp đồng
            contract.PriceAgreed = totalPrice;

            // 4. Ghi nhận tiền cọc nếu có
            if (model.DepositAmount > 0)
            {
                RecordDeposit(contract.Id, model.DepositAmount, company.CompanyName);
            }

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

            // Ghi nhận tiền cọc nếu có
            if (model.DepositAmount > 0)
            {
                var firstTenant = db.ContractTenants
                    .Include(ct => ct.Tenant)
                    .FirstOrDefault(ct => ct.ContractId == contract.Id);

                var depositorName = firstTenant?.Tenant?.FullName ?? "Khách thuê";
                RecordDeposit(contract.Id, model.DepositAmount, depositorName);
            }

            // 7. Cập nhật trạng thái phòng
            room.IsOccupied = true;

            // 8. Ghi nhận tiền cọc nếu có
            if (model.DepositAmount > 0)
            {
                var tenantName = model.Tenants.FirstOrDefault()?.FullName ?? "Khách hàng";
                RecordDeposit(contract.Id, model.DepositAmount, tenantName);
            }

            db.SaveChanges();
            return contract;
        }

        // Helper method để ghi nhận tiền cọc
        private void RecordDeposit(int contractId, decimal amount, string customerName)
        {
            var incomeCategory = db.IncomeExpenseCategories
                .FirstOrDefault(c => c.Name == "Thu tiền cọc" && c.IsSystem);

            if (incomeCategory == null)
            {
                // Tạo category nếu chưa có
                incomeCategory = new IncomeExpenseCategory
                {
                    Name = "Thu tiền cọc",
                    Type = "Income",
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                db.IncomeExpenseCategories.Add(incomeCategory);
                db.SaveChanges();
            }

            var incomeExpense = new IncomeExpense
            {
                CategoryId = incomeCategory.Id,
                ContractId = contractId,
                Amount = amount,
                TransactionDate = DateTime.Now.Date,
                Description = $"Thu tiền cọc từ {customerName} - HĐ #{contractId}",
                ReferenceNumber = $"DEPOSIT-{contractId}",
                CreatedBy = User.Identity.GetUserId(),
                CreatedAt = DateTime.Now
            };

            db.IncomeExpenses.Add(incomeExpense);
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
    }
}