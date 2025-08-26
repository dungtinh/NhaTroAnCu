using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Ajax.Utilities;
using Microsoft.AspNet.Identity;
using NhaTroAnCu.Helpers;
using NhaTroAnCu.Models;

namespace NhaTroAnCu.Controllers
{
    public class RoomsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        public ActionResult Index(int? day, int? month, int? year)
        {
            var now = DateTime.Now;
            int selectedMonth = month ?? now.Month;
            int selectedYear = year ?? now.Year;
            int selectedDay = day ?? DateTime.DaysInMonth(selectedYear, selectedMonth);

            ViewBag.SelectedDay = selectedDay;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;

            var filterDate = new DateTime(selectedYear, selectedMonth, selectedDay);

            var rooms = db.Rooms
                .Include(r => r.ContractRooms.Select(cr => cr.Contract))
                .Include(r => r.ContractTenants.Select(ct => ct.Tenant))
                .ToList();

            var roomViewModels = rooms.Select(room => GetRoomViewModel(room, filterDate, selectedMonth, selectedYear, now))
                                      .ToList();

            // Tính toán thống kê cho Summary Dashboard
            CalculateSummaryStatistics(roomViewModels, filterDate);

            return View(roomViewModels);
        }

        private void CalculateSummaryStatistics(List<RoomViewModel> roomViewModels, DateTime filterDate)
        {
            // Tổng số phòng
            int totalRooms = roomViewModels.Count;

            // Phòng đã thuê (có hợp đồng active)
            int rentedRooms = roomViewModels.Count(r =>
                r.Room.ContractRooms.Any(cr =>
                    cr.Contract.Status == "Active" &&
                    cr.Contract.StartDate <= filterDate &&
                    cr.Contract.EndDate >= filterDate));

            // Phòng còn trống
            int emptyRooms = totalRooms - rentedRooms;

            // Phòng sắp hết hạn (trong vòng 31 ngày)
            int nearExpiredRooms = roomViewModels.Count(r => r.IsContractNearingEnd);

            // Phòng đã hết hạn hợp đồng
            int expiredRooms = roomViewModels.Count(r => r.IsContractExpired);

            // Tính hiệu suất sử dụng phòng
            decimal occupancyRate = totalRooms > 0 ? (decimal)rentedRooms / totalRooms : 0;

            // Gán vào ViewBag để sử dụng trong View
            ViewBag.TotalRooms = totalRooms;
            ViewBag.RentedRooms = rentedRooms;
            ViewBag.EmptyRooms = emptyRooms;
            ViewBag.NearExpiredRooms = nearExpiredRooms;
            ViewBag.ExpiredRooms = expiredRooms;
            ViewBag.OccupancyRate = occupancyRate;
        }

        private RoomViewModel GetRoomViewModel(Room room, DateTime filterDate, int selectedMonth, int selectedYear, DateTime now)
        {
            string colorClass = "gray"; // Mặc định phòng trống
            bool isContractExpired = false;
            bool isContractNearingEnd = false;
            string tenantName = "";
            DateTime? contractEndDate = null;

            try
            {
                // Tìm hợp đồng active của phòng qua ContractRooms
                var activeContract = room.ContractRooms
                    .Where(cr => cr.Contract != null && cr.Contract.Status == "Active")
                    .Select(cr => cr.Contract)
                    .OrderByDescending(c => c.StartDate)
                    .FirstOrDefault();

                if (activeContract != null)
                {
                    contractEndDate = activeContract.EndDate;

                    // Kiểm tra hợp đồng còn hiệu lực không
                    if (activeContract.StartDate <= filterDate && activeContract.EndDate >= filterDate)
                    {
                        // Hợp đồng còn hiệu lực

                        // Kiểm tra sắp hết hạn (trong 31 ngày)
                        var daysUntilEnd = (activeContract.EndDate - filterDate).TotalDays;
                        if (daysUntilEnd <= 31)
                        {
                            isContractNearingEnd = true;
                        }

                        // Lấy tên khách thuê
                        var contractTenant = room.ContractTenants
                            .Where(ct => ct.ContractId == activeContract.Id && ct.Tenant != null)
                            .Select(ct => ct.Tenant)
                            .FirstOrDefault();

                        if (contractTenant != null)
                        {
                            tenantName = contractTenant.FullName;
                        }
                        else if (activeContract.Company != null)
                        {
                            tenantName = activeContract.Company.CompanyName;
                        }

                        // Kiểm tra tình trạng thanh toán
                        var bill = db.UtilityBills
                            .FirstOrDefault(b => b.ContractId == activeContract.Id
                                && b.Month == selectedMonth
                                && b.Year == selectedYear);

                        if (bill != null)
                        {
                            // Có bill cho tháng này
                            decimal mustPay = bill.TotalAmount;

                            // Sử dụng IncomeExpenses thay vì PaymentHistories
                            // Lấy category "Thu tiền phòng" để lọc các khoản thu tiền phòng
                            var roomPaymentCategory = db.IncomeExpenseCategories
                                .FirstOrDefault(c => c.Name == "Thu tiền phòng" && c.Type == "Income");

                            decimal paid = 0;

                            if (roomPaymentCategory != null)
                            {
                                // Tính tổng số tiền đã thanh toán cho tháng này
                                // Dựa vào TransactionDate hoặc có thể lưu Month/Year trong Description
                                paid = db.IncomeExpenses
                                    .Where(ie => ie.ContractId == activeContract.Id
                                        && ie.CategoryId == roomPaymentCategory.Id
                                        && ie.TransactionDate.Month == selectedMonth
                                        && ie.TransactionDate.Year == selectedYear)
                                    .Sum(ie => (decimal?)ie.Amount) ?? 0;
                            }

                            if (paid >= mustPay)
                            {
                                colorClass = "green"; // Đã thanh toán đủ
                            }
                            else if (paid > 0)
                            {
                                colorClass = "yellow"; // Thanh toán một phần  
                            }
                            else
                            {
                                colorClass = "orange"; // Chưa thanh toán
                            }
                        }
                        else
                        {
                            // Chưa có bill
                            colorClass = "blue"; // Đang thuê, chưa có bill
                        }
                    }
                    else if (activeContract.EndDate < filterDate)
                    {
                        // Hợp đồng đã hết hạn
                        isContractExpired = true;
                        colorClass = "red"; // Dùng "red" thay vì "expired"

                        // Lấy tên từ hợp đồng cũ
                        var contractTenant = room.ContractTenants
                            .Where(ct => ct.ContractId == activeContract.Id && ct.Tenant != null)
                            .Select(ct => ct.Tenant)
                            .FirstOrDefault();

                        if (contractTenant != null)
                        {
                            tenantName = contractTenant.FullName + " (Hết HĐ)";
                        }
                        else if (activeContract.Company != null)
                        {
                            tenantName = activeContract.Company.CompanyName + " (Hết HĐ)";
                        }
                    }
                }
                else
                {
                    // Không có hợp đồng
                    if (room.IsOccupied)
                    {
                        colorClass = "purple"; // Có người ở không hợp đồng
                        tenantName = "Có người ở";
                    }
                    else
                    {
                        colorClass = "gray"; // Phòng trống
                        tenantName = "";
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error nếu cần
                System.Diagnostics.Debug.WriteLine($"Error in GetRoomViewModel for room {room.Name}: {ex.Message}");

                // Trả về giá trị mặc định an toàn
                colorClass = "gray";
                tenantName = "Lỗi";
            }

            return new RoomViewModel
            {
                Room = room,
                ColorClass = colorClass,
                TenantName = tenantName,
                IsContractNearingEnd = isContractNearingEnd,
                IsContractExpired = isContractExpired,
                ContractEndDate = contractEndDate
            };
        }

        public ActionResult Details(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();

            // Tìm hợp đồng active cho phòng này
            var activeContract = db.Contracts
                .Include(c => c.ContractRooms)
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.ContractExtensionHistories)
                .Where(c => c.Status == "Active" && c.ContractRooms.Any(cr => cr.RoomId == id))
                .FirstOrDefault();

            var now = DateTime.Now;
            var currentMonth = now.Month;
            var currentYear = now.Year;

            SetupViewBagForDetails(room, activeContract, currentMonth, currentYear, now);

            return View(room);
        }

        private void SetupViewBagForDetails(Room room, Contract activeContract, int currentMonth, int currentYear, DateTime now)
        {
            ViewBag.ActiveContract = activeContract;
            ViewBag.CurrentMonth = currentMonth;
            ViewBag.CurrentYear = currentYear;

            if (activeContract != null)
            {
                // Lấy bill cho hợp đồng này
                var currentBill = db.UtilityBills.FirstOrDefault(b =>
                    b.Month == currentMonth &&
                    b.Year == currentYear &&
                    b.ContractId == activeContract.Id);

                ViewBag.Bill = currentBill;

                // Lấy chỉ số nước tháng trước
                var prevBill = new UtilityBillService(db).GetHighestWaterIndexEnd(room.Id);
                ViewBag.WaterPrev = prevBill;

                // Lấy tenants cho phòng này
                var roomTenants = db.ContractTenants
                    .Include(ct => ct.Tenant)
                    .Where(ct => ct.ContractId == activeContract.Id && ct.RoomId == room.Id)
                    .ToList();
                ViewBag.RoomTenants = roomTenants;
                var totalBill = db.UtilityBills
            .Where(b => b.ContractId == activeContract.Id)
            .Sum(b => (decimal?)b.TotalAmount) ?? 0;

                // Tổng đã thu
                var totalPaid = db.IncomeExpenses
                    .Where(ie => ie.ContractId == activeContract.Id
                        && ie.IncomeExpenseCategory.Name == "Thu tiền phòng")
                    .Sum(ie => (decimal?)ie.Amount) ?? 0;

                // Số tiền thiếu
                var amountDue = totalBill - totalPaid;

                ViewBag.AmountDue = amountDue > 0 ? amountDue : 0;
                ViewBag.RoomId = room.Id;

                // Lấy tất cả bills của hợp đồng
                var bills = db.UtilityBills
                    .Where(b => b.ContractId == activeContract.Id)
                    .OrderByDescending(b => b.Year)
                    .ThenByDescending(b => b.Month)
                    .ToList();

                // Lấy tất cả thanh toán tiền phòng của hợp đồng
                var payments = db.IncomeExpenses
                    .Where(ie => ie.ContractId == activeContract.Id
                        && ie.IncomeExpenseCategory.Name == "Thu tiền phòng")
                    .OrderByDescending(ie => ie.TransactionDate)
                    .ToList();

                // Tạo danh sách theo tháng
                var paymentHistory = new List<MonthlyPaymentHistory>();
                decimal cumulativeDeposit = 0; // Số dư tích lũy (cọc)

                foreach (var bill in bills)
                {
                    // Lấy các thanh toán trong tháng này
                    var monthPayments = payments
                        .Where(p => p.TransactionDate.Month == bill.Month
                            && p.TransactionDate.Year == bill.Year)
                        .OrderBy(p => p.TransactionDate)
                        .ToList();

                    decimal totalPaidInMonth = monthPayments.Sum(p => p.Amount);
                    decimal monthBalance = totalPaidInMonth - bill.TotalAmount;
                    cumulativeDeposit += monthBalance;

                    paymentHistory.Add(new MonthlyPaymentHistory
                    {
                        Month = bill.Month,
                        Year = bill.Year,
                        Bill = bill,
                        Payments = monthPayments,
                        TotalBilled = bill.TotalAmount,
                        TotalPaid = totalPaidInMonth,
                        MonthBalance = monthBalance,
                        CumulativeDeposit = cumulativeDeposit
                    });
                }

                // Kiểm tra các tháng có thanh toán nhưng chưa có bill
                var monthsWithPaymentOnly = payments
                    .GroupBy(p => new { p.TransactionDate.Month, p.TransactionDate.Year })
                    .Where(g => !bills.Any(b => b.Month == g.Key.Month && b.Year == g.Key.Year))
                    .Select(g => new MonthlyPaymentHistory
                    {
                        Month = g.Key.Month,
                        Year = g.Key.Year,
                        Bill = null,
                        Payments = g.OrderBy(p => p.TransactionDate).ToList(),
                        TotalBilled = 0,
                        TotalPaid = g.Sum(p => p.Amount),
                        MonthBalance = g.Sum(p => p.Amount),
                        CumulativeDeposit = cumulativeDeposit + g.Sum(p => p.Amount)
                    })
                    .ToList();

                paymentHistory.AddRange(monthsWithPaymentOnly);
                paymentHistory = paymentHistory.OrderByDescending(h => h.Year).ThenByDescending(h => h.Month).ToList();

                ViewBag.PaymentHistory = paymentHistory;
            }
            else
            {
                ViewBag.Bill = null;
                ViewBag.WaterPrev = 0;
                ViewBag.ExtraCharge = 0;
                ViewBag.Discount = 0;
                ViewBag.RoomTenants = null;
                ViewBag.AmountDue = 0;
                ViewBag.RoomId = room.Id;
                ViewBag.PaymentHistory = null;
            }
        }

        [HttpPost]
        public ActionResult ToggleOccupied(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();

            room.IsOccupied = !room.IsOccupied;
            db.SaveChanges();

            return Json(new { success = true, status = room.IsOccupied });
        }

        // GET: Rooms/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Rooms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Room room)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(room.Name))
                    room.Name = $"P{room.Area}{room.Floor}{room.RoomNumber}";

                db.Rooms.Add(room);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(room);
        }

        // GET: Rooms/Edit/5
        public ActionResult Edit(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();
            return View(room);
        }

        // POST: Rooms/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Room room)
        {
            if (ModelState.IsValid)
            {
                db.Entry(room).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(room);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db?.Dispose();
            }
            base.Dispose(disposing);
        }
        // GET: /Rooms/ManageEmployees/5
        public ActionResult ManageTenants(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();

            // Tìm contract active của phòng
            var contractRoom = db.ContractRooms
                .Include(cr => cr.Contract.Company)
                .FirstOrDefault(cr => cr.RoomId == id
                    && cr.Contract.Status == "Active");

            if (contractRoom == null)
            {
                TempData["Error"] = "Phòng này chưa có hợp đồng active";
                return RedirectToAction("Details", new { id });
            }

            // Lấy danh sách người thuê hiện tại trong phòng
            var currentTenants = db.ContractTenants
                .Include(ct => ct.Tenant)
                .Where(ct => ct.ContractId == contractRoom.ContractId && ct.RoomId == id)
                .Select(ct => new TenantViewModel
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
                    CreatedAt = ct.CreatedAt
                })
                .ToList();

            var model = new RoomTenantManagementViewModel
            {
                RoomId = id,
                RoomName = room.Name,
                ContractId = contractRoom.ContractId,
                CompanyName = contractRoom.Contract.Company?.CompanyName,
                ContractType = contractRoom.Contract.ContractType ?? "Individual",
                ContractStartDate = contractRoom.Contract.StartDate,
                ContractEndDate = contractRoom.Contract.EndDate,
                CurrentTenants = currentTenants
            };

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddTenant(int roomId, TenantInputModel model, HttpPostedFileBase photoFile)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin";
                return RedirectToAction("ManageTenants", new { id = roomId });
            }

            var contractRoom = db.ContractRooms
                .Include(cr => cr.Contract)
                .FirstOrDefault(cr => cr.RoomId == roomId && cr.Contract.Status == "Active");

            if (contractRoom == null)
            {
                TempData["Error"] = "Không tìm thấy hợp đồng active cho phòng này";
                return RedirectToAction("Details", new { id = roomId });
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var existingTenant = db.Tenants
                        .FirstOrDefault(t => t.IdentityCard == model.IdentityCard);

                    Tenant tenant;
                    if (existingTenant != null)
                    {
                        tenant = existingTenant;
                        // Cập nhật thông tin
                        tenant.FullName = model.FullName;
                        tenant.PhoneNumber = model.PhoneNumber;
                        tenant.BirthDate = model.BirthDate;
                        tenant.Gender = model.Gender;
                        tenant.PermanentAddress = model.PermanentAddress;
                        tenant.Ethnicity = model.Ethnicity;
                        tenant.VehiclePlate = model.VehiclePlate;

                        if (contractRoom.Contract.CompanyId.HasValue)
                        {
                            tenant.CompanyId = contractRoom.Contract.CompanyId;
                        }
                    }
                    else
                    {
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
                            CompanyId = contractRoom.Contract.CompanyId
                        };

                        // Sử dụng TenantPhotoHelper để xử lý upload ảnh CCCD
                        if (photoFile != null && photoFile.ContentLength > 0)
                        {
                            try
                            {
                                tenant.Photo = TenantPhotoHelper.SaveTenantPhoto(photoFile, tenant.IdentityCard);
                            }
                            catch (InvalidOperationException ex)
                            {
                                TempData["Error"] = ex.Message;
                                return RedirectToAction("ManageTenants", new { id = roomId });
                            }
                        }

                        db.Tenants.Add(tenant);
                    }

                    db.SaveChanges();

                    // Thêm vào ContractTenants
                    var contractTenant = new ContractTenant
                    {
                        ContractId = contractRoom.ContractId,
                        TenantId = tenant.Id,
                        RoomId = roomId,
                        CreatedAt = DateTime.Now
                    };
                    db.ContractTenants.Add(contractTenant);
                    db.SaveChanges();

                    transaction.Commit();
                    TempData["Success"] = $"Đã thêm {tenant.FullName} vào phòng";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                }
            }

            return RedirectToAction("ManageTenants", new { id = roomId });
        }

        // POST: /Rooms/RemoveTenant
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveTenant(int roomId, int tenantId)
        {
            var contractTenant = db.ContractTenants
                .Include(ct => ct.Tenant)
                .FirstOrDefault(ct => ct.RoomId == roomId && ct.TenantId == tenantId);

            if (contractTenant != null)
            {
                var tenantName = contractTenant.Tenant.FullName;
                db.ContractTenants.Remove(contractTenant);
                db.SaveChanges();

                TempData["Success"] = $"Đã xóa {tenantName} khỏi phòng";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy người thuê trong phòng này";
            }

            return RedirectToAction("ManageTenants", new { id = roomId });
        }

        // GET: /Rooms/TenantReport
        // Báo cáo danh sách người đang thuê trọ
        public ActionResult TenantReport(string contractType = "All", int? companyId = null)
        {
            var query = db.ContractTenants
                .Include(ct => ct.Contract)
                .Include(ct => ct.Tenant)
                .Include(ct => ct.Room)
                .Where(ct => ct.Contract.Status == "Active");

            // Filter by contract type
            if (contractType == "Company")
            {
                query = query.Where(ct => ct.Contract.ContractType == "Company");
                if (companyId.HasValue)
                {
                    query = query.Where(ct => ct.Contract.CompanyId == companyId);
                }
            }
            else if (contractType == "Individual")
            {
                query = query.Where(ct => ct.Contract.ContractType == "Individual");
            }

            var tenants = query
                .Select(ct => new
                {
                    ct.Tenant.FullName,
                    ct.Tenant.IdentityCard,
                    ct.Tenant.PhoneNumber,
                    ct.Tenant.BirthDate,
                    ct.Tenant.Gender,
                    ct.Tenant.VehiclePlate,
                    RoomName = ct.Room.Name,
                    ContractType = ct.Contract.ContractType,
                    CompanyName = ct.Contract.Company.CompanyName,
                    MoveInDate = ct.Contract.MoveInDate,
                    ContractEndDate = ct.Contract.EndDate
                })
                .ToList();

            ViewBag.ContractType = contractType;
            ViewBag.CompanyId = companyId;

            return View(tenants);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RecordRoomPayment(int contractId, int roomId, decimal amount, DateTime paymentDate, string note)
        {
            try
            {
                // Lấy category "Tiền phòng"
                var category = db.IncomeExpenseCategories
                    .FirstOrDefault(c => c.Name == "Thu tiền phòng" && c.Type == "Income");

                var income = new IncomeExpense
                {
                    CategoryId = category.Id,
                    ContractId = contractId,
                    Amount = amount,
                    TransactionDate = paymentDate,
                    Description = $"Thu tiền phòng - {note}",
                    ReferenceNumber = $"THU-{DateTime.Now:yyyyMMddHHmmss}",
                    CreatedBy = User.Identity.GetUserId(),
                    CreatedAt = DateTime.Now
                };

                db.IncomeExpenses.Add(income);
                db.SaveChanges();

                var pdfBytes = GeneratePaymentReceipt(income.Id);

                // Lưu file PDF vào thư mục tạm
                string fileName = $"HoaDon_{income.ReferenceNumber}.pdf";
                string tempPath = Server.MapPath($"~/App_Data/Receipts/{fileName}");

                // Tạo thư mục nếu chưa có
                string directory = Path.GetDirectoryName(tempPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                System.IO.File.WriteAllBytes(tempPath, pdfBytes);

                return Json(new
                {
                    success = true,
                    receiptUrl = Url.Action("DownloadReceipt", new { fileName = fileName })
                });
            }
            catch
            {
                return Json(new { success = false });
            }
        }
        public ActionResult DownloadReceipt(string fileName)
        {
            string path = Server.MapPath($"~/App_Data/Receipts/{fileName}");
            if (System.IO.File.Exists(path))
            {
                byte[] fileBytes = System.IO.File.ReadAllBytes(path);
                return File(fileBytes, "application/pdf", fileName);
            }
            return HttpNotFound();
        }
        private byte[] GeneratePaymentReceipt(int incomeExpenseId)
        {
            // Lấy thông tin thanh toán vừa lưu
            var payment = db.IncomeExpenses
                .Include(ie => ie.Contract)
                .Include(ie => ie.Contract.ContractRooms.Select(cr => cr.Room))
                .Include(ie => ie.Contract.ContractTenants.Select(ct => ct.Tenant))
                .Include(ie => ie.Contract.Company)
                .FirstOrDefault(ie => ie.Id == incomeExpenseId);

            if (payment == null)
                throw new Exception("Không tìm thấy thông tin thanh toán");

            var contract = payment.Contract;
            var room = contract.ContractRooms.FirstOrDefault()?.Room;

            // Lấy bill của tháng hiện tại
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            var currentBill = db.UtilityBills
                .FirstOrDefault(b => b.ContractId == contract.Id
                    && b.Month == currentMonth
                    && b.Year == currentYear);

            // Tính toán số dư
            var totalBilled = db.UtilityBills
                .Where(b => b.ContractId == contract.Id)
                .Sum(b => (decimal?)b.TotalAmount) ?? 0;

            var totalPaid = db.IncomeExpenses
                .Where(ie => ie.ContractId == contract.Id
                    && ie.IncomeExpenseCategory.Name == "Thu tiền phòng")
                .Sum(ie => (decimal?)ie.Amount) ?? 0;

            var balance = totalPaid - totalBilled;

            // Tạo PDF
            using (var memoryStream = new MemoryStream())
            {
                // Khởi tạo document - Chỉ định đầy đủ namespace
                var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 60, 60);
                var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, memoryStream);

                document.Open();

                // Font tiếng Việt
                string fontPath = Server.MapPath("~/fonts/times.ttf");
                iTextSharp.text.pdf.BaseFont bf = iTextSharp.text.pdf.BaseFont.CreateFont(
                    fontPath,
                    iTextSharp.text.pdf.BaseFont.IDENTITY_H,
                    iTextSharp.text.pdf.BaseFont.EMBEDDED);

                iTextSharp.text.Font titleFont = new iTextSharp.text.Font(bf, 18, iTextSharp.text.Font.BOLD);
                iTextSharp.text.Font headerFont = new iTextSharp.text.Font(bf, 14, iTextSharp.text.Font.BOLD);
                iTextSharp.text.Font normalFont = new iTextSharp.text.Font(bf, 12, iTextSharp.text.Font.NORMAL);
                iTextSharp.text.Font boldFont = new iTextSharp.text.Font(bf, 12, iTextSharp.text.Font.BOLD);
                iTextSharp.text.Font smallFont = new iTextSharp.text.Font(bf, 10, iTextSharp.text.Font.NORMAL);

                // HEADER - Tiêu đề
                var titlePara = new iTextSharp.text.Paragraph("HÓA ĐƠN THU TIỀN PHÒNG TRỌ", titleFont);
                titlePara.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                document.Add(titlePara);

                document.Add(new iTextSharp.text.Paragraph($"Số: {payment.ReferenceNumber}", normalFont));
                document.Add(new iTextSharp.text.Paragraph($"Ngày: {payment.TransactionDate:dd/MM/yyyy}", normalFont));
                document.Add(new iTextSharp.text.Paragraph(" "));

                // THÔNG TIN HỢP ĐỒNG
                document.Add(new iTextSharp.text.Paragraph("I. THÔNG TIN HỢP ĐỒNG", headerFont));
                document.Add(new iTextSharp.text.Paragraph($"Mã hợp đồng: #{contract.Id}", normalFont));
                document.Add(new iTextSharp.text.Paragraph($"Phòng: {room?.Name ?? "N/A"}", normalFont));

                if (contract.ContractType == "Individual")
                {
                    var tenant = contract.ContractTenants.FirstOrDefault()?.Tenant;
                    document.Add(new iTextSharp.text.Paragraph($"Khách thuê: {tenant?.FullName ?? "N/A"}", normalFont));
                    document.Add(new iTextSharp.text.Paragraph($"CMND/CCCD: {tenant?.IdentityCard ?? "N/A"}", normalFont));
                    document.Add(new iTextSharp.text.Paragraph($"Điện thoại: {tenant?.PhoneNumber ?? "N/A"}", normalFont));
                }
                else
                {
                    var company = contract.Company;
                    document.Add(new iTextSharp.text.Paragraph($"Công ty: {company?.CompanyName ?? "N/A"}", normalFont));
                    document.Add(new iTextSharp.text.Paragraph($"Mã số thuế: {company?.TaxCode ?? "N/A"}", normalFont));
                }

                document.Add(new iTextSharp.text.Paragraph(" "));

                // THÔNG TIN PHIẾU BÁO THÁNG NÀY
                if (currentBill != null)
                {
                    document.Add(new iTextSharp.text.Paragraph($"II. PHIẾU BÁO THÁNG {currentMonth}/{currentYear}", headerFont));

                    var table = new iTextSharp.text.pdf.PdfPTable(2);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 60, 40 });

                    AddTableRow(table, "Tiền phòng:", currentBill.RentAmount.ToString("N0") + "đ", normalFont);
                    AddTableRow(table, "Tiền điện:", currentBill.ElectricityAmount.ToString("N0") + "đ", normalFont);
                    AddTableRow(table, "Tiền nước:", currentBill.WaterAmount.ToString("N0") + "đ", normalFont);

                    if (currentBill.ExtraCharge > 0)
                        AddTableRow(table, "Phụ thu:", currentBill.ExtraCharge.ToString("N0") + "đ", normalFont);
                    if (currentBill.Discount > 0)
                        AddTableRow(table, "Giảm giá:", currentBill.Discount.ToString("N0") + "đ", normalFont);

                    AddTableRow(table, "TỔNG CỘNG:", currentBill.TotalAmount.ToString("N0") + "đ", boldFont);

                    document.Add(table);
                    document.Add(new iTextSharp.text.Paragraph(" "));
                }

                // THÔNG TIN THANH TOÁN
                document.Add(new iTextSharp.text.Paragraph("III. THÔNG TIN THANH TOÁN", headerFont));
                document.Add(new iTextSharp.text.Paragraph($"Số tiền thu: {payment.Amount.ToString("N0")}đ", boldFont));
                document.Add(new iTextSharp.text.Paragraph($"Bằng chữ: {NumberToText(payment.Amount)}", normalFont));
                document.Add(new iTextSharp.text.Paragraph($"Ghi chú: {payment.Description}", normalFont));
                document.Add(new iTextSharp.text.Paragraph(" "));

                // TÌNH TRẠNG CÔNG NỢ
                document.Add(new iTextSharp.text.Paragraph("IV. TIỀN CỌC", headerFont));
                if (balance >= 0)
                {
                    document.Add(new iTextSharp.text.Paragraph($"Tiền thừa (đặt cọc): {balance.ToString("N0")}đ", boldFont));
                }
                else
                {
                    document.Add(new iTextSharp.text.Paragraph($"Âm cọc: {Math.Abs(balance).ToString("N0")}đ", boldFont));
                }

                document.Add(new iTextSharp.text.Paragraph(" "));

                // LƯU Ý
                document.Add(new iTextSharp.text.Paragraph("V. LƯU Ý QUAN TRỌNG", headerFont));
                var nextMonth = DateTime.Now.AddMonths(1).Month;
                var nextYear = DateTime.Now.AddMonths(1).Year;

                document.Add(new iTextSharp.text.Paragraph($"• Hạn đóng tiền tháng tới: 10/{nextMonth:00}/{nextYear}", normalFont));
                document.Add(new iTextSharp.text.Paragraph($"• Đề nghị chụp đồng hồ nước vào ngày 09/{nextMonth:00}/{nextYear} để tính phí nước", normalFont));
                document.Add(new iTextSharp.text.Paragraph("• Điện được tính theo công tơ điện tử", normalFont));
                document.Add(new iTextSharp.text.Paragraph("• Vui lòng giữ hóa đơn này để đối chiếu khi cần", normalFont));
                document.Add(new iTextSharp.text.Paragraph(" "));

                // CHỮ KÝ
                document.Add(new iTextSharp.text.Paragraph(" "));
                document.Add(new iTextSharp.text.Paragraph(" "));

                var signTable = new iTextSharp.text.pdf.PdfPTable(2);
                signTable.WidthPercentage = 100;
                signTable.DefaultCell.Border = 0;
                signTable.SetWidths(new float[] { 50, 50 });

                var cellLeft = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("Người nộp tiền", normalFont));
                cellLeft.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                cellLeft.Border = 0;
                signTable.AddCell(cellLeft);

                var cellRight = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("Người thu tiền", normalFont));
                cellRight.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                cellRight.Border = 0;
                signTable.AddCell(cellRight);

                signTable.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(" ", normalFont)) { Border = 0, FixedHeight = 60 });
                signTable.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(" ", normalFont)) { Border = 0, FixedHeight = 60 });

                document.Add(signTable);

                document.Close();
                writer.Close();

                return memoryStream.ToArray();
            }
        }
        private string NumberToText(decimal number)
        {
            // Hàm chuyển số thành chữ tiếng Việt
            // Code đơn giản:
            return $"{number:N0} đồng";
            // Có thể thêm logic chuyển đổi phức tạp hơn nếu cần
        }
        // Helper functions
        private void AddTableRow(iTextSharp.text.pdf.PdfPTable table, string label, string value, iTextSharp.text.Font font)
        {
            table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(label, font)) { Border = 0, PaddingBottom = 5 });
            table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(value, font)) { Border = 0, PaddingBottom = 5, HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT });
        }

        // Thêm vào RoomsController.cs

        public ActionResult History(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();

            var viewModel = new RoomHistoryViewModel
            {
                Room = room,
                Contracts = GetRoomContracts(id),
                Tenants = GetRoomTenants(id),
                Payments = GetRoomPayments(id),
                CurrentContract = GetCurrentContract(id),
                TotalContracts = 0,
                TotalTenants = 0,
                TotalRevenue = 0,
                TotalMonthsRented = 0,
                OccupancyRate = 0,
                AverageMonthlyRevenue = 0,
                AverageContractDuration = 0,
                TotalDebt = 0,
                PaymentCompleteRate = 0,
                AveragePaymentDelay = 0
            };

            // Calculate statistics
            CalculateRoomStatistics(viewModel, id);

            return View(viewModel);
        }

        private List<ContractHistoryItem> GetRoomContracts(int roomId)
        {
            var contracts = db.ContractRooms
                .Where(cr => cr.RoomId == roomId)
                .Select(cr => cr.Contract)
                .OrderByDescending(c => c.StartDate)
                .ToList();

            var result = new List<ContractHistoryItem>();

            foreach (var contract in contracts)
            {
                var item = new ContractHistoryItem
                {
                    Id = contract.Id,
                    StartDate = contract.StartDate,
                    EndDate = contract.EndDate,
                    Status = contract.Status,
                    MonthlyRent = contract.PriceAgreed,
                    ContractType = contract.ContractType,
                    Tenants = new List<TenantInfo>()
                };

                // Get tenants for this contract
                var tenants = db.ContractTenants
                    .Where(ct => ct.ContractId == contract.Id && ct.RoomId == roomId)
                    .Select(ct => ct.Tenant)
                    .ToList();

                foreach (var tenant in tenants)
                {
                    item.Tenants.Add(new TenantInfo
                    {
                        Id = tenant.Id,
                        FullName = tenant.FullName,
                        IdentityCard = tenant.IdentityCard
                    });
                }

                // Calculate payment statistics
                var bills = db.UtilityBills
                    .Where(b => b.ContractId == contract.Id)
                    .ToList();

                var payments = db.IncomeExpenses
                    .Where(ie => ie.ContractId == contract.Id &&
                                ie.IncomeExpenseCategory.Name == "Thu tiền phòng")
                    .ToList();

                item.TotalPaid = payments.Sum(p => p.Amount);
                item.TotalDebt = bills.Sum(b => b.TotalAmount) - item.TotalPaid;
                item.PaymentRate = bills.Any() ?
                    (int)((item.TotalPaid / bills.Sum(b => b.TotalAmount)) * 100) : 100;

                result.Add(item);
            }

            return result;
        }

        private List<TenantHistoryItem> GetRoomTenants(int roomId)
        {
            var tenants = db.ContractTenants
                .Where(ct => ct.RoomId == roomId)
                .Select(ct => new
                {
                    Tenant = ct.Tenant,
                    Contract = ct.Contract
                })
                .ToList();

            var result = new List<TenantHistoryItem>();

            foreach (var item in tenants)
            {
                var tenantHistory = new TenantHistoryItem
                {
                    Id = item.Tenant.Id,
                    FullName = item.Tenant.FullName,
                    IdentityCard = item.Tenant.IdentityCard,
                    PhoneNumber = item.Tenant.PhoneNumber,
                    Gender = item.Tenant.Gender,
                    MoveInDate = item.Contract.StartDate,
                    MoveOutDate = item.Contract.Status == "Ended" ?
                        (DateTime?)item.Contract.EndDate : null,
                    IsCurrent = item.Contract.Status == "Active"
                };

                result.Add(tenantHistory);
            }

            return result.DistinctBy(t => t.Id).ToList();
        }

        private List<PaymentHistoryItem> GetRoomPayments(int roomId)
        {
            var contracts = db.ContractRooms
                .Where(cr => cr.RoomId == roomId)
                .Select(cr => cr.ContractId)
                .ToList();

            var result = new List<PaymentHistoryItem>();

            foreach (var contractId in contracts)
            {
                var bills = db.UtilityBills
                    .Where(b => b.ContractId == contractId)
                    .ToList();

                foreach (var bill in bills)
                {
                    var payments = db.IncomeExpenses
                        .Where(ie => ie.ContractId == contractId &&
                                    ie.TransactionDate.Month == bill.Month &&
                                    ie.TransactionDate.Year == bill.Year &&
                                    ie.IncomeExpenseCategory.Name == "Thu tiền phòng")
                        .ToList();

                    var paidAmount = payments.Sum(p => p.Amount);
                    var remaining = bill.TotalAmount - paidAmount;

                    result.Add(new PaymentHistoryItem
                    {
                        ContractId = contractId,
                        Month = bill.Month,
                        Year = bill.Year,
                        BillAmount = bill.TotalAmount,
                        PaidAmount = paidAmount,
                        Remaining = remaining,
                        Status = remaining <= 0 ? "Paid" :
                                paidAmount > 0 ? "Partial" : "Unpaid",
                        PaymentDate = payments.FirstOrDefault()?.TransactionDate
                    });
                }
            }

            return result;
        }

        private ContractHistoryItem GetCurrentContract(int roomId)
        {
            var contract = db.ContractRooms
                .Where(cr => cr.RoomId == roomId)
                .Select(cr => cr.Contract)
                .FirstOrDefault(c => c.Status == "Active");

            if (contract == null) return null;

            var tenants = db.ContractTenants
                .Where(ct => ct.ContractId == contract.Id && ct.RoomId == roomId)
                .Select(ct => ct.Tenant.FullName)
                .ToList();

            return new ContractHistoryItem
            {
                Id = contract.Id,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                Status = contract.Status,
                MonthlyRent = contract.PriceAgreed,
                TenantNames = string.Join(", ", tenants)
            };
        }

        private void CalculateRoomStatistics(RoomHistoryViewModel model, int roomId)
        {
            // Total contracts
            model.TotalContracts = model.Contracts.Count;

            // Total tenants
            model.TotalTenants = model.Tenants.Count;

            // Total revenue
            model.TotalRevenue = model.Payments.Sum(p => p.PaidAmount);

            // Total months rented
            foreach (var contract in model.Contracts)
            {
                var months = ((contract.EndDate - contract.StartDate).TotalDays / 30);
                model.TotalMonthsRented += (int)months;
            }

            // Occupancy rate (last 12 months)
            var oneYearAgo = DateTime.Now.AddYears(-1);
            var totalDays = (DateTime.Now - oneYearAgo).TotalDays;
            var occupiedDays = 0.0;

            foreach (var contract in model.Contracts)
            {
                var start = contract.StartDate < oneYearAgo ? oneYearAgo : contract.StartDate;
                var end = contract.EndDate > DateTime.Now ? DateTime.Now : contract.EndDate;

                if (start < end)
                {
                    occupiedDays += (end - start).TotalDays;
                }
            }

            model.OccupancyRate = (int)((occupiedDays / totalDays) * 100);

            // Average monthly revenue
            if (model.TotalMonthsRented > 0)
            {
                model.AverageMonthlyRevenue = model.TotalRevenue / model.TotalMonthsRented;
            }

            // Average contract duration
            if (model.Contracts.Any())
            {
                model.AverageContractDuration = model.TotalMonthsRented / model.Contracts.Count;
            }

            // Total debt
            model.TotalDebt = model.Payments.Sum(p => p.Remaining);

            // Payment complete rate
            var totalBilled = model.Payments.Sum(p => p.BillAmount);
            if (totalBilled > 0)
            {
                model.PaymentCompleteRate = (int)((model.TotalRevenue / totalBilled) * 100);
            }

            // Average payment delay (simplified)
            model.AveragePaymentDelay = 5; // This would need more complex calculation
        }

        

       

    }
}