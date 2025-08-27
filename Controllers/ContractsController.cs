using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Aspose.Words;
using ClosedXML.Excel;
using Microsoft.AspNet.Identity;
using NhaTroAnCu.Helpers;
using NhaTroAnCu.Models;
using PagedList;
using Contract = NhaTroAnCu.Models.Contract;

namespace NhaTroAnCu.Controllers
{
    public class ContractsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: Contracts/Extend/5
        [HttpGet]
        public ActionResult Extend(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .Include(c => c.ContractExtensionHistories)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            // Kiểm tra contract đã kết thúc chưa
            if (contract.Status == "Ended")
            {
                TempData["Error"] = "Không thể gia hạn hợp đồng đã kết thúc.";
                return RedirectToAction("Details", new { id = id });
            }

            ViewBag.ContractId = id;
            ViewBag.CurrentEndDate = contract.EndDate;

            return View(contract);
        }

        // POST: Contracts/Extend/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Extend(int id, DateTime newEndDate, string extendNote)
        {
            var contract = db.Contracts
                .Include(c => c.ContractExtensionHistories)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            // Validation
            if (newEndDate <= contract.EndDate)
            {
                TempData["Error"] = "Ngày gia hạn mới phải sau ngày kết thúc hiện tại.";
                return View(contract);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Lưu lịch sử gia hạn
                    var extensionHistory = new ContractExtensionHistory
                    {
                        ContractId = contract.Id,
                        OldEndDate = contract.EndDate,
                        NewEndDate = newEndDate,
                        Note = extendNote,
                        ExtendedAt = DateTime.Now,
                    };

                    db.ContractExtensionHistories.Add(extensionHistory);

                    // Cập nhật ngày kết thúc mới cho hợp đồng
                    contract.EndDate = newEndDate;

                    // Thêm ghi chú vào Note của contract
                    var noteEntry = $"\n[{DateTime.Now:dd/MM/yyyy HH:mm}] Gia hạn từ {contract.EndDate:dd/MM/yyyy} đến {newEndDate:dd/MM/yyyy}";
                    if (!string.IsNullOrEmpty(extendNote))
                    {
                        noteEntry += $" - {extendNote}";
                    }
                    contract.Note = (contract.Note ?? "") + noteEntry;

                    // Lưu thay đổi
                    db.SaveChanges();

                    // Commit transaction
                    transaction.Commit();

                    TempData["Success"] = $"Đã gia hạn hợp đồng đến ngày {newEndDate:dd/MM/yyyy} thành công.";

                    return RedirectToAction("Details", "Contracts", new { id = contract.Id });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Có lỗi xảy ra khi gia hạn hợp đồng. Vui lòng thử lại.";
                    return View(contract);
                }
            }
        }

        // Action hỗ trợ: Lấy lịch sử gia hạn
        [HttpGet]
        public JsonResult GetExtensionHistory(int id)
        {
            var history = db.ContractExtensionHistories
                .Where(h => h.ContractId == id)
                .OrderByDescending(h => h.ExtendedAt)
                .ToList();

            return Json(history, JsonRequestBehavior.AllowGet);
        }


        // GET: Contracts/End/5
        [HttpGet]
        public ActionResult End(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.IncomeExpenses.Select(ci => ci.IncomeExpenseCategory))
                .Include(c => c.Company)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            // Kiểm tra contract đã kết thúc chưa
            if (contract.Status == "Ended")
            {
                TempData["Error"] = "Hợp đồng này đã được kết thúc trước đó.";
                return RedirectToAction("Details", new { id = id });
            }

            ViewBag.ContractId = id;
            return View(contract);
        }

        // POST: Contracts/End/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult End(int id, DateTime? endDate, decimal? refundAmount, string refundNote)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Cập nhật trạng thái hợp đồng
                    contract.Status = "Ended";
                    contract.EndDate = endDate ?? DateTime.Now;
                    // Cập nhật trạng thái tất cả các phòng trong hợp đồng
                    foreach (var contractRoom in contract.ContractRooms)
                    {
                        contractRoom.Room.IsOccupied = false;
                    }

                    // Ghi nhận trả cọc nếu có
                    if (refundAmount.HasValue && refundAmount.Value > 0)
                    {
                        // Tìm hoặc tạo category "Trả tiền cọc"
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
                                CreatedAt = DateTime.Now,
                            };
                            db.IncomeExpenseCategories.Add(expenseCategory);
                            db.SaveChanges();
                        }

                        // Tạo phiếu chi trả cọc
                        var incomeExpense = new IncomeExpense
                        {
                            CategoryId = expenseCategory.Id,
                            ContractId = id,
                            Amount = refundAmount.Value,
                            TransactionDate = endDate?.Date ?? DateTime.Now.Date,
                            Description = !string.IsNullOrEmpty(refundNote)
                                ? refundNote
                                : $"Trả tiền cọc khi kết thúc hợp đồng {contract.Id}",
                            ReferenceNumber = $"REFUND-{contract.Id}-{DateTime.Now:yyyyMMddHHmmss}",
                            CreatedBy = User.Identity.GetUserId(),
                            CreatedAt = DateTime.Now
                        };

                        db.IncomeExpenses.Add(incomeExpense);
                    }

                    // Lưu tất cả thay đổi
                    db.SaveChanges();

                    // Commit transaction
                    transaction.Commit();

                    TempData["Success"] = $"Đã kết thúc hợp đồng {contract.Id} thành công.";

                    // Redirect về phòng đầu tiên trong hợp đồng hoặc danh sách hợp đồng

                    return RedirectToAction("Details", "Contracts", new { id = contract.Id });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    // Log error nếu có hệ thống logging
                    // Logger.Error($"Error ending contract {id}: {ex.Message}", ex);

                    TempData["Error"] = "Có lỗi xảy ra khi kết thúc hợp đồng. Vui lòng thử lại.";
                    return RedirectToAction("Details", new { id = id });
                }
            }
        }

        // GET: Contracts
        public ActionResult Index(string searchTerm, string contractType, string status,
    DateTime? fromDate, DateTime? toDate, int? companyId,
    string sortOrder, int? page, int? pageSize)
        {
            // Query cơ bản
            var contracts = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                contracts = contracts.Where(c =>
                    c.Id.ToString().Contains(searchTerm) ||
                    c.ContractRooms.Any(cr => cr.Room.Name.Contains(searchTerm)) ||
                    c.ContractTenants.Any(ct => ct.Tenant.FullName.Contains(searchTerm) ||
                                               ct.Tenant.IdentityCard.Contains(searchTerm)) ||
                    (c.Company != null && c.Company.CompanyName.Contains(searchTerm))
                );
            }

            // Lọc theo loại hợp đồng
            if (!string.IsNullOrEmpty(contractType))
            {
                contracts = contracts.Where(c => c.ContractType == contractType);
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "NearExpiry")
                {
                    var thirtyDaysFromNow = DateTime.Now.AddDays(30);
                    contracts = contracts.Where(c => c.Status == "Active" &&
                                                    c.EndDate <= thirtyDaysFromNow);
                }
                else
                {
                    contracts = contracts.Where(c => c.Status == status);
                }
            }

            // Lọc theo ngày
            if (fromDate.HasValue)
            {
                contracts = contracts.Where(c => c.StartDate >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                contracts = contracts.Where(c => c.EndDate <= toDate.Value);
            }

            // Lọc theo công ty
            if (companyId.HasValue)
            {
                contracts = contracts.Where(c => c.CompanyId == companyId.Value);
            }

            // Cập nhật trạng thái hợp đồng hết hạn
            UpdateExpiredContracts();

            // Sắp xếp
            switch (sortOrder)
            {
                case "id_desc":
                    contracts = contracts.OrderByDescending(c => c.Id);
                    break;
                case "Date":
                    contracts = contracts.OrderBy(c => c.StartDate);
                    break;
                case "date_desc":
                    contracts = contracts.OrderByDescending(c => c.StartDate);
                    break;
                case "Status":
                    contracts = contracts.OrderBy(c => c.Status);
                    break;
                case "status_desc":
                    contracts = contracts.OrderByDescending(c => c.Status);
                    break;
                case "EndDate":
                    contracts = contracts.OrderBy(c => c.EndDate);
                    break;
                case "enddate_desc":
                    contracts = contracts.OrderByDescending(c => c.EndDate);
                    break;
                default:
                    contracts = contracts.OrderByDescending(c => c.Id);
                    break;
            }

            // Phân trang
            int currentPageSize = pageSize ?? 10;
            int pageNumber = page ?? 1;

            // Tạo ViewModel
            var viewModel = new ContractListViewModel
            {
                Contracts = contracts.ToPagedList(pageNumber, currentPageSize),
                SearchTerm = searchTerm,
                ContractType = contractType,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                CompanyId = companyId,
                SortOrder = sortOrder,
                PageSize = pageSize,

                // Sort parameters
                IdSortParm = String.IsNullOrEmpty(sortOrder) ? "id_desc" : "",
                DateSortParm = sortOrder == "Date" ? "date_desc" : "Date",
                StatusSortParm = sortOrder == "Status" ? "status_desc" : "Status",
                EndDateSortParm = sortOrder == "EndDate" ? "enddate_desc" : "EndDate",

                // Companies dropdown
                Companies = new SelectList(
                    db.Companies.OrderBy(c => c.CompanyName),
                    "Id",
                    "CompanyName",
                    companyId
                )
            };

            // Calculate statistics
            var contractsList = contracts.ToList();
            viewModel.TotalContracts = contractsList.Count;
            viewModel.ActiveContracts = contractsList.Count(c => c.Status == "Active");
            viewModel.ExpiredContracts = contractsList.Count(c => c.Status == "Expired");
            viewModel.EndedContracts = contractsList.Count(c => c.Status == "Ended");

            var expiryDateThreshold = DateTime.Now.AddDays(30);
            viewModel.NearExpiryContracts = contractsList.Count(c =>
                c.Status == "Active" && c.EndDate <= expiryDateThreshold);

            viewModel.TotalActiveValue = contractsList
                .Where(c => c.Status == "Active")
                .Sum(c => c.PriceAgreed);

            viewModel.TotalRoomsRented = contractsList
                .Where(c => c.Status == "Active")
                .SelectMany(c => c.ContractRooms)
                .Select(cr => cr.RoomId)
                .Distinct()
                .Count();

            viewModel.TotalTenants = contractsList
                .Where(c => c.Status == "Active")
                .SelectMany(c => c.ContractTenants)
                .Select(ct => ct.TenantId)
                .Distinct()
                .Count();

            return View(viewModel);
        }

        // Helper method: Cập nhật hợp đồng hết hạn
        private void UpdateExpiredContracts()
        {
            var expiredContracts = db.Contracts
                .Where(c => c.Status == "Active" && c.EndDate < DateTime.Now)
                .ToList();

            foreach (var contract in expiredContracts)
            {
                contract.Status = "Expired";
            }

            if (expiredContracts.Any())
            {
                db.SaveChanges();
            }
        }

        // Helper method: Chuẩn bị dữ liệu cho ViewBag
        private void PrepareViewBagData(string searchTerm, string contractType, string status,
            DateTime? fromDate, DateTime? toDate, int? companyId, int? pageSize)
        {
            // Maintain filter state
            ViewBag.CurrentFilter = searchTerm;
            ViewBag.ContractType = contractType;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.CompanyId = companyId;
            ViewBag.PageSize = pageSize;

            // Dropdown lists
            ViewBag.ContractTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Tất cả --" },
                new SelectListItem { Value = "Individual", Text = "Cá nhân/Hộ gia đình" },
                new SelectListItem { Value = "Company", Text = "Công ty" }
            };

            ViewBag.Statuses = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Tất cả --" },
                new SelectListItem { Value = "Active", Text = "Đang hiệu lực" },
                new SelectListItem { Value = "Expired", Text = "Đã hết hạn" },
                new SelectListItem { Value = "Ended", Text = "Đã kết thúc" },
                new SelectListItem { Value = "NearExpiry", Text = "Sắp hết hạn (30 ngày)" }
            };

            ViewBag.Companies = new SelectList(
                db.Companies.OrderBy(c => c.CompanyName),
                "Id",
                "CompanyName",
                companyId
            );

            ViewBag.PageSizes = new List<SelectListItem>
            {
                new SelectListItem { Value = "10", Text = "10" },
                new SelectListItem { Value = "25", Text = "25" },
                new SelectListItem { Value = "50", Text = "50" },
                new SelectListItem { Value = "100", Text = "100" }
            };
        }

        // Helper method: Tính toán thống kê
        private void CalculateStatistics(IQueryable<Contract> contracts)
        {
            var contractsList = contracts.ToList();

            ViewBag.TotalContracts = contractsList.Count;
            ViewBag.ActiveContracts = contractsList.Count(c => c.Status == "Active");
            ViewBag.ExpiredContracts = contractsList.Count(c => c.Status == "Expired");
            ViewBag.EndedContracts = contractsList.Count(c => c.Status == "Ended");

            // Sắp hết hạn trong 30 ngày
            var thirtyDaysFromNow = DateTime.Now.AddDays(30);
            ViewBag.NearExpiryContracts = contractsList.Count(c =>
                c.Status == "Active" && c.EndDate <= thirtyDaysFromNow);

            // Tổng giá trị hợp đồng đang active
            ViewBag.TotalActiveValue = contractsList
                .Where(c => c.Status == "Active")
                .Sum(c => c.PriceAgreed);

            // Tổng số phòng đang cho thuê
            ViewBag.TotalRoomsRented = contractsList
                .Where(c => c.Status == "Active")
                .SelectMany(c => c.ContractRooms)
                .Select(cr => cr.RoomId)
                .Distinct()
                .Count();

            // Tổng số khách thuê
            ViewBag.TotalTenants = contractsList
                .Where(c => c.Status == "Active")
                .SelectMany(c => c.ContractTenants)
                .Select(ct => ct.TenantId)
                .Distinct()
                .Count();
        }

        // GET: Contracts/Export - Xuất Excel
        public ActionResult Export(string searchTerm, string contractType, string status,
            DateTime? fromDate, DateTime? toDate, int? companyId)
        {
            // Sử dụng cùng logic filter như Index
            var contracts = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .AsQueryable();

            // Apply filters (giống như trong Index)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                contracts = contracts.Where(c =>
                    c.Id.ToString().Contains(searchTerm) ||
                    c.ContractRooms.Any(cr => cr.Room.Name.Contains(searchTerm)) ||
                    c.ContractTenants.Any(ct => ct.Tenant.FullName.Contains(searchTerm) ||
                                               ct.Tenant.IdentityCard.Contains(searchTerm)) ||
                    (c.Company != null && c.Company.CompanyName.Contains(searchTerm))
                );
            }

            if (!string.IsNullOrEmpty(contractType))
                contracts = contracts.Where(c => c.ContractType == contractType);

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "NearExpiry")
                {
                    var thirtyDaysFromNow = DateTime.Now.AddDays(30);
                    contracts = contracts.Where(c => c.Status == "Active" &&
                                                    c.EndDate <= thirtyDaysFromNow);
                }
                else
                {
                    contracts = contracts.Where(c => c.Status == status);
                }
            }

            if (fromDate.HasValue)
                contracts = contracts.Where(c => c.StartDate >= fromDate.Value);

            if (toDate.HasValue)
                contracts = contracts.Where(c => c.EndDate <= toDate.Value);

            if (companyId.HasValue)
                contracts = contracts.Where(c => c.CompanyId == companyId.Value);

            // Tạo Excel file
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Danh sách hợp đồng");

                // Headers
                worksheet.Cell(1, 1).Value = "Mã HĐ";
                worksheet.Cell(1, 2).Value = "Loại HĐ";
                worksheet.Cell(1, 3).Value = "Phòng";
                worksheet.Cell(1, 4).Value = "Khách thuê/Công ty";
                worksheet.Cell(1, 5).Value = "Ngày bắt đầu";
                worksheet.Cell(1, 6).Value = "Ngày kết thúc";
                worksheet.Cell(1, 7).Value = "Giá thuê";
                worksheet.Cell(1, 8).Value = "Điện";
                worksheet.Cell(1, 9).Value = "Nước";
                worksheet.Cell(1, 10).Value = "Trạng thái";
                worksheet.Cell(1, 11).Value = "Ghi chú";

                // Format header row
                var headerRange = worksheet.Range(1, 1, 1, 11);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                // Data rows
                var row = 2;
                foreach (var contract in contracts.OrderByDescending(c => c.Id))
                {
                    worksheet.Cell(row, 1).Value = contract.Id;
                    worksheet.Cell(row, 2).Value = contract.ContractType == "Company" ? "Công ty" : "Cá nhân";

                    // Danh sách phòng
                    var rooms = string.Join(", ", contract.ContractRooms.Select(cr => cr.Room.Name));
                    worksheet.Cell(row, 3).Value = rooms;

                    // Tên khách thuê hoặc công ty
                    string customerName = "";
                    if (contract.ContractType == "Company")
                    {
                        customerName = contract.Company?.CompanyName ?? "";
                    }
                    else
                    {
                        var tenants = contract.ContractTenants.Select(ct => ct.Tenant.FullName).ToList();
                        customerName = string.Join(", ", tenants);
                    }
                    worksheet.Cell(row, 4).Value = customerName;

                    worksheet.Cell(row, 5).Value = contract.StartDate.ToString("dd/MM/yyyy");
                    worksheet.Cell(row, 6).Value = contract.EndDate.ToString("dd/MM/yyyy");
                    worksheet.Cell(row, 7).Value = contract.PriceAgreed;
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0";

                    worksheet.Cell(row, 8).Value = contract.ElectricityPrice;
                    worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0";

                    worksheet.Cell(row, 9).Value = contract.WaterPrice;
                    worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0";

                    worksheet.Cell(row, 10).Value = GetStatusDisplay(contract.Status);
                    worksheet.Cell(row, 11).Value = contract.Note;

                    // Highlight theo trạng thái
                    if (contract.Status == "Expired")
                    {
                        worksheet.Range(row, 1, row, 11).Style.Fill.BackgroundColor = XLColor.LightCoral;
                    }
                    else if (contract.Status == "Ended")
                    {
                        worksheet.Range(row, 1, row, 11).Style.Fill.BackgroundColor = XLColor.LightGray;
                    }
                    else if (contract.EndDate <= DateTime.Now.AddDays(30))
                    {
                        worksheet.Range(row, 1, row, 11).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    }

                    row++;
                }

                // Auto fit columns
                worksheet.Columns().AdjustToContents();

                // Thêm sheet thống kê
                var statsSheet = workbook.Worksheets.Add("Thống kê");
                AddStatisticsSheet(statsSheet, contracts.ToList());

                // Save to memory stream
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    var fileName = $"DanhSachHopDong_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                    return File(content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        fileName);
                }
            }
        }

        // Helper method: Thêm sheet thống kê
        private void AddStatisticsSheet(IXLWorksheet sheet, List<Contract> contracts)
        {
            sheet.Cell(1, 1).Value = "THỐNG KÊ HỢP ĐỒNG";
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 16;

            int row = 3;
            sheet.Cell(row, 1).Value = "Tổng số hợp đồng:";
            sheet.Cell(row, 2).Value = contracts.Count;
            row++;

            sheet.Cell(row, 1).Value = "Đang hiệu lực:";
            sheet.Cell(row, 2).Value = contracts.Count(c => c.Status == "Active");
            row++;

            sheet.Cell(row, 1).Value = "Đã hết hạn:";
            sheet.Cell(row, 2).Value = contracts.Count(c => c.Status == "Expired");
            row++;

            sheet.Cell(row, 1).Value = "Đã kết thúc:";
            sheet.Cell(row, 2).Value = contracts.Count(c => c.Status == "Ended");
            row++;

            var thirtyDaysFromNow = DateTime.Now.AddDays(30);
            sheet.Cell(row, 1).Value = "Sắp hết hạn (30 ngày):";
            sheet.Cell(row, 2).Value = contracts.Count(c =>
                c.Status == "Active" && c.EndDate <= thirtyDaysFromNow);
            row += 2;

            sheet.Cell(row, 1).Value = "THỐNG KÊ THEO LOẠI";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            row++;

            sheet.Cell(row, 1).Value = "Hợp đồng cá nhân:";
            sheet.Cell(row, 2).Value = contracts.Count(c => c.ContractType == "Individual");
            row++;

            sheet.Cell(row, 1).Value = "Hợp đồng công ty:";
            sheet.Cell(row, 2).Value = contracts.Count(c => c.ContractType == "Company");
            row += 2;

            sheet.Cell(row, 1).Value = "GIÁ TRỊ HỢP ĐỒNG";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            row++;

            var activeContracts = contracts.Where(c => c.Status == "Active").ToList();
            sheet.Cell(row, 1).Value = "Tổng giá trị đang active:";
            sheet.Cell(row, 2).Value = activeContracts.Sum(c => c.PriceAgreed);
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0 VNĐ";

            sheet.Columns().AdjustToContents();
        }

        // Helper method: Lấy text hiển thị cho status
        private string GetStatusDisplay(string status)
        {
            switch (status)
            {
                case "Active": return "Đang hiệu lực";
                case "Expired": return "Đã hết hạn";
                case "Ended": return "Đã kết thúc";
                default: return status;
            }
        }

        // GET: Contracts/BatchUpdate - Cập nhật hàng loạt
        [HttpPost]
        public ActionResult BatchUpdate(int[] contractIds, string action)
        {
            if (contractIds == null || contractIds.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một hợp đồng.";
                return RedirectToAction("Index");
            }

            try
            {
                var contracts = db.Contracts.Where(c => contractIds.Contains(c.Id)).ToList();

                switch (action)
                {
                    case "extend":
                        // Chuyển đến trang gia hạn hàng loạt
                        return RedirectToAction("BatchExtend", new { ids = string.Join(",", contractIds) });

                    case "end":
                        // Kết thúc hàng loạt
                        foreach (var contract in contracts.Where(c => c.Status == "Active"))
                        {
                            contract.Status = "Ended";
                            contract.EndDate = DateTime.Now;
                        }
                        db.SaveChanges();
                        TempData["Success"] = $"Đã kết thúc {contracts.Count} hợp đồng.";
                        break;

                    case "delete":
                        // Xóa hàng loạt (cần xác nhận)
                        return View("ConfirmBatchDelete", contracts);

                    default:
                        TempData["Error"] = "Hành động không hợp lệ.";
                        break;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";
            }

            return RedirectToAction("Index");
        }


        // GET: Contracts/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            return View(contract);
        }

        // GET: Contracts/Create
        public ActionResult Create(int? roomId)
        {
            var today = DateTime.Now;
            var model = new ContractCreateViewModel
            {
                MoveInDate = new DateTime(today.Year, today.Month, 10),
                StartDate = DateTime.Now,
                ElectricityPrice = 3500,
                WaterPrice = 15000,
                Months = 12,
                ContractType = roomId.HasValue ? "Individual" : null,
                SelectedRooms = new List<RoomSelectionModel>()
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

            LoadCreateViewData();
            return View(model);
        }

        // POST: Contracts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ContractCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errorList = new List<string>();

                foreach (var modelError in ModelState)
                {
                    string fieldName = modelError.Key;
                    var errors = modelError.Value.Errors;

                    if (errors.Count > 0)
                    {
                        foreach (var error in errors)
                        {
                            string errorMsg = $"Field: [{fieldName}] - Error: {error.ErrorMessage}";
                            errorList.Add(errorMsg);

                            // Debug output
                            System.Diagnostics.Debug.WriteLine(errorMsg);
                        }
                    }
                }
                LoadCreateViewData();
                return View(model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var contract = new Contract
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
                        contract = CreateCompanyContract(model, contract);
                    }
                    else
                    {
                        contract = CreateIndividualContract(model, contract);
                    }

                    transaction.Commit();
                    TempData["Success"] = "Đã tạo hợp đồng thành công!";
                    return RedirectToAction("Details", new { id = contract.Id });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                    LoadCreateViewData();
                    return View(model);
                }
            }
        }

        // GET: Contracts/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

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

                var occupiedRoomIds = db.ContractRooms
                    .Where(cr => cr.Contract.Status == "Active" && cr.ContractId != id)
                    .Select(cr => cr.RoomId)
                    .ToList();

                var availableRooms = db.Rooms
                    .Where(r => !occupiedRoomIds.Contains(r.Id))
                    .ToList();

                model.SelectedRooms = availableRooms.Select(r => new RoomSelectionModel
                {
                    RoomId = r.Id,
                    RoomName = r.Name,
                    DefaultPrice = r.DefaultPrice,
                    AgreedPrice = contract.ContractRooms.FirstOrDefault(cr => cr.RoomId == r.Id)?.PriceAgreed ?? r.DefaultPrice,
                    IsSelected = contract.ContractRooms.Any(cr => cr.RoomId == r.Id),
                    Notes = contract.ContractRooms.FirstOrDefault(cr => cr.RoomId == r.Id)?.Notes
                }).ToList();

                if (contract.Company != null)
                {
                    ViewBag.CompanyTaxCode = contract.Company.TaxCode;
                }
            }
            else
            {
                var contractRoom = contract.ContractRooms.FirstOrDefault();
                if (contractRoom != null)
                {
                    model.RoomId = contractRoom.RoomId;
                    model.PriceAgreed = contractRoom.PriceAgreed;

                    model.SelectedRooms = contract.ContractRooms.Select(r => new RoomSelectionModel
                    {
                        RoomId = r.RoomId,
                        RoomName = r.Room.Name,
                        DefaultPrice = r.Room.DefaultPrice,
                        AgreedPrice = contract.ContractRooms.FirstOrDefault()?.PriceAgreed ?? r.Room.DefaultPrice,
                        IsSelected = contract.ContractRooms.Any(cr => cr.RoomId == r.Room.Id),
                        Notes = contract.ContractRooms.FirstOrDefault(cr => cr.RoomId == r.Room.Id)?.Notes
                    }).ToList();
                }

                model.Tenants = contract.ContractTenants.Select(ct => new TenantViewModel
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
                }).ToList();
            }

            LoadEditViewData(model.Id);
            return View(model);
        }

        // POST: Contracts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ContractEditViewModel model, HttpPostedFileBase ContractScanFile)
        {
            if (!ModelState.IsValid)
            {
                LoadEditViewData(model.Id);
                return View(model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var contract = db.Contracts
                        .Include(c => c.ContractRooms)
                        .Include(c => c.ContractTenants)
                        .FirstOrDefault(c => c.Id == model.Id);

                    if (contract == null)
                    {
                        return HttpNotFound();
                    }

                    // Update basic info
                    contract.StartDate = model.StartDate;
                    contract.EndDate = model.EndDate;
                    contract.MoveInDate = model.MoveInDate;
                    contract.ElectricityPrice = model.ElectricityPrice;
                    contract.WaterPrice = model.WaterPrice;
                    contract.Note = model.Note;
                    contract.Status = model.Status;

                    // Process contract scan file
                    if (ContractScanFile != null && ContractScanFile.ContentLength > 0)
                    {
                        ProcessContractScanFile(contract, ContractScanFile);
                    }

                    // Process based on type
                    if (contract.ContractType == "Individual")
                    {
                        UpdateIndividualContract(contract, model);
                    }
                    else if (contract.ContractType == "Company")
                    {
                        UpdateCompanyContract(contract, model);
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    TempData["Success"] = "Đã cập nhật hợp đồng thành công!";
                    return RedirectToAction("Details", new { id = contract.Id });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", $"Có lỗi xảy ra: {ex.Message}");
                    LoadEditViewData(model.Id);
                    return View(model);
                }
            }
        }

        // DELETE: Contracts/Delete/5
        [HttpPost]
        public ActionResult Delete(int id)
        {
            try
            {
                var contract = db.Contracts
                    .Include(c => c.ContractRooms)
                    .Include(c => c.ContractTenants)
                    .FirstOrDefault(c => c.Id == id);

                if (contract == null)
                {
                    return Json(new { success = false, message = "Hợp đồng không tồn tại" });
                }

                // Update room status
                foreach (var cr in contract.ContractRooms)
                {
                    var room = db.Rooms.Find(cr.RoomId);
                    if (room != null)
                    {
                        room.IsOccupied = false;
                    }
                }

                // Remove contract tenants
                db.ContractTenants.RemoveRange(contract.ContractTenants);

                // Remove contract rooms
                db.ContractRooms.RemoveRange(contract.ContractRooms);

                // Remove contract
                db.Contracts.Remove(contract);

                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa hợp đồng thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #region Helper Methods

        private Contract CreateIndividualContract(ContractCreateViewModel model, Contract contract)
        {
            if (!model.SingleRoomId.HasValue)
            {
                throw new InvalidOperationException("Vui lòng chọn phòng");
            }

            var room = db.Rooms.Find(model.SingleRoomId.Value);
            if (room == null || room.IsOccupied)
            {
                throw new InvalidOperationException("Phòng không tồn tại hoặc đã được thuê");
            }

            contract.PriceAgreed = model.SingleRoomPrice ?? room.DefaultPrice;
            db.Contracts.Add(contract);
            db.SaveChanges();

            var contractRoom = new ContractRoom
            {
                ContractId = contract.Id,
                RoomId = room.Id,
                PriceAgreed = contract.PriceAgreed,
                Notes = model.Note
            };
            db.ContractRooms.Add(contractRoom);
            db.SaveChanges();

            room.IsOccupied = true;
            db.SaveChanges();

            return contract;
        }

        private Contract CreateCompanyContract(ContractCreateViewModel model, Contract contract)
        {
            // Process company
            if (model.Company != null && !string.IsNullOrEmpty(model.Company.TaxCode))
            {
                var existingCompany = db.Companies
                    .FirstOrDefault(c => c.TaxCode == model.Company.TaxCode);

                Company company;
                if (existingCompany != null)
                {
                    company = existingCompany;
                    UpdateCompanyInfo(company, model.Company);
                }
                else
                {
                    company = CreateNewCompany(model.Company);
                    db.Companies.Add(company);
                }

                db.SaveChanges();
                contract.CompanyId = company.Id;
            }

            db.Contracts.Add(contract);
            db.SaveChanges();

            // Process rooms
            decimal totalPrice = 0;
            if (model.SelectedRooms != null)
            {
                foreach (var selectedRoom in model.SelectedRooms)
                {
                    if (!selectedRoom.IsSelected) continue;

                    var room = db.Rooms.Find(selectedRoom.RoomId);
                    if (room != null)
                    {
                        var contractRoom = new ContractRoom
                        {
                            ContractId = contract.Id,
                            RoomId = selectedRoom.RoomId,
                            PriceAgreed = selectedRoom.AgreedPrice,
                            Notes = selectedRoom.Notes
                        };
                        db.ContractRooms.Add(contractRoom);
                        room.IsOccupied = true;
                        totalPrice += selectedRoom.AgreedPrice;
                    }
                }
            }

            contract.PriceAgreed = totalPrice;
            db.SaveChanges();

            return contract;
        }

        private void UpdateIndividualContract(Contract contract, ContractEditViewModel model)
        {
            if (model.RoomId.HasValue && model.PriceAgreed.HasValue)
            {
                var contractRoom = contract.ContractRooms.FirstOrDefault();
                if (contractRoom != null)
                {
                    if (contractRoom.RoomId != model.RoomId.Value)
                    {
                        var oldRoom = db.Rooms.Find(contractRoom.RoomId);
                        if (oldRoom != null) oldRoom.IsOccupied = false;

                        var contractTenants = db.ContractTenants
                            .Where(ct => ct.ContractId == contract.Id)
                            .ToList();

                        foreach (var ct in contractTenants)
                        {
                            ct.RoomId = model.RoomId.Value;
                        }

                        contractRoom.RoomId = model.RoomId.Value;

                        var newRoom = db.Rooms.Find(model.RoomId.Value);
                        if (newRoom != null && contract.Status == "Active")
                        {
                            newRoom.IsOccupied = true;
                        }
                    }

                    contractRoom.PriceAgreed = model.PriceAgreed.Value;
                    contractRoom.Notes = model.SelectedRooms[0].Notes;
                }
                contract.PriceAgreed = model.PriceAgreed.Value;
            }

            // Process tenants using helper
            TenantContractHelper.ProcessIndividualContractTenants(
                db, contract, model.Tenants, Request, true
            );
        }

        private void UpdateCompanyContract(Contract contract, ContractEditViewModel model)
        {
            if (model.SelectedRooms != null)
            {
                var currentRoomIds = contract.ContractRooms.Select(cr => cr.RoomId).ToList();
                var newRoomIds = model.SelectedRooms.Where(x => x.IsSelected).Select(r => r.RoomId).ToList();
                // Remove rooms
                var roomsToRemove = contract.ContractRooms
                    .Where(cr => !newRoomIds.Contains(cr.RoomId))
                    .ToList();

                foreach (var contractRoom in roomsToRemove)
                {
                    RemoveRoomFromContract(contract.Id, contractRoom.RoomId);
                }

                // Add new rooms
                var roomsToAdd = newRoomIds
                    .Where(id => !currentRoomIds.Contains(id))
                    .ToList();

                foreach (var roomId in roomsToAdd)
                {
                    AddRoomToContract(contract.Id, roomId, model.SelectedRooms);
                }

                // Update existing rooms
                foreach (var roomModel in model.SelectedRooms)
                {
                    var contractRoom = contract.ContractRooms
                        .FirstOrDefault(cr => cr.RoomId == roomModel.RoomId);

                    if (contractRoom != null)
                    {
                        contractRoom.PriceAgreed = roomModel.AgreedPrice;
                        contractRoom.Notes = roomModel.Notes;
                    }
                }

                // Update total price
                contract.PriceAgreed = contract.ContractRooms.Sum(cr => cr.PriceAgreed);
            }
        }

        private Company CreateNewCompany(Company model)
        {
            return new Company
            {
                CompanyName = model.CompanyName,
                TaxCode = model.TaxCode,
                Address = model.Address,
                Phone = model.Phone,
                Email = model.Email,
                Representative = model.Representative,
                RepresentativePhone = model.RepresentativePhone,
                RepresentativeEmail = model.RepresentativeEmail,
                BankAccount = model.BankAccount,
                BankName = model.BankName
            };
        }

        private void UpdateCompanyInfo(Company company, Company model)
        {
            company.CompanyName = model.CompanyName;
            company.Address = model.Address;
            company.Phone = model.Phone;
            company.Email = model.Email;
            company.Representative = model.Representative;
            company.RepresentativePhone = model.RepresentativePhone;
            company.RepresentativeEmail = model.RepresentativeEmail;
            company.BankAccount = model.BankAccount;
            company.BankName = model.BankName;
        }

        private void ProcessContractScanFile(Contract contract, HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return;

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("File phải là PDF, JPG hoặc PNG");
            }

            if (!string.IsNullOrEmpty(contract.ContractScanFilePath))
            {
                var oldPath = Server.MapPath(contract.ContractScanFilePath);
                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }

            var fileName = $"Contract_{contract.Id}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
            var uploadDir = Server.MapPath("~/Uploads/ContractScans");

            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var filePath = Path.Combine(uploadDir, fileName);
            file.SaveAs(filePath);
            contract.ContractScanFilePath = $"/Uploads/ContractScans/{fileName}";
        }

        private void AddRoomToContract(int contractId, int roomId, List<RoomSelectionModel> roomModels)
        {
            var room = db.Rooms.Find(roomId);
            if (room == null) return;

            var isOccupied = db.ContractRooms
                .Any(cr => cr.RoomId == roomId &&
                          cr.Contract.Status == "Active" &&
                          cr.ContractId != contractId);

            if (isOccupied) return;

            var roomModel = roomModels.FirstOrDefault(r => r.RoomId == roomId);
            var agreedPrice = roomModel?.AgreedPrice ?? room.DefaultPrice;

            var contractRoom = new ContractRoom
            {
                ContractId = contractId,
                RoomId = roomId,
                PriceAgreed = agreedPrice,
                Notes = roomModel?.Notes
            };
            db.ContractRooms.Add(contractRoom);

            room.IsOccupied = true;
        }

        private void RemoveRoomFromContract(int contractId, int roomId)
        {
            var contractRoom = db.ContractRooms
                .FirstOrDefault(cr => cr.ContractId == contractId && cr.RoomId == roomId);

            if (contractRoom == null) return;

            // Remove tenants in room
            var tenantsInRoom = db.ContractTenants
                .Where(ct => ct.ContractId == contractId && ct.RoomId == roomId)
                .ToList();

            foreach (var ct in tenantsInRoom)
            {
                db.ContractTenants.Remove(ct);

                var hasOtherContracts = db.ContractTenants
                    .Any(c => c.TenantId == ct.TenantId && c.ContractId != contractId);

                if (!hasOtherContracts)
                {
                    var tenant = db.Tenants.Find(ct.TenantId);
                    if (tenant != null)
                    {
                        if (!string.IsNullOrEmpty(tenant.Photo))
                        {
                            TenantPhotoHelper.DeleteTenantPhoto(tenant.Photo);
                        }
                        db.Tenants.Remove(tenant);
                    }
                }
            }

            var room = db.Rooms.Find(roomId);
            if (room != null)
            {
                room.IsOccupied = false;
            }

            db.ContractRooms.Remove(contractRoom);
        }

        private void LoadCreateViewData()
        {
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
                .ToList();

            ViewBag.AvailableRooms = availableRooms;
        }

        private void LoadEditViewData(int contractId)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms)
                .FirstOrDefault(c => c.Id == contractId);

            if (contract != null)
            {
                if (contract.ContractType == "Individual")
                {
                    var currentRoomId = contract.ContractRooms.FirstOrDefault()?.RoomId;
                    var occupiedRoomIds = db.ContractRooms
                        .Where(cr => cr.Contract.Status == "Active" && cr.ContractId != contractId)
                        .Select(cr => cr.RoomId)
                        .ToList();

                    var availableRooms = db.Rooms
                        .Where(r => !occupiedRoomIds.Contains(r.Id) || r.Id == currentRoomId)
                        .ToList();

                    ViewBag.AvailableRooms = availableRooms.Select(r => new SelectListItem
                    {
                        Value = r.Id.ToString(),
                        Text = r.Name + " - " + r.DefaultPrice.ToString("N0") + " VNĐ",
                        Selected = r.Id == currentRoomId
                    }).ToList();
                }
            }
        }

        #endregion

        #region AJAX Actions

        [HttpGet]
        public JsonResult GetAvailableRoomsForCompany(int contractId)
        {
            try
            {
                var currentContract = db.Contracts
                    .Include(c => c.ContractRooms)
                    .FirstOrDefault(c => c.Id == contractId);

                if (currentContract == null || currentContract.ContractType != "Company")
                {
                    return Json(new { success = false, message = "Invalid contract" },
                        JsonRequestBehavior.AllowGet);
                }

                var occupiedRoomIds = db.ContractRooms
                    .Where(cr => cr.Contract.Status == "Active" && cr.ContractId != contractId)
                    .Select(cr => cr.RoomId)
                    .ToList();

                var currentRoomIds = currentContract.ContractRooms
                    .Select(cr => cr.RoomId)
                    .ToList();

                var allRooms = db.Rooms.ToList();

                var availableRooms = allRooms
                    .Where(r => !occupiedRoomIds.Contains(r.Id) || currentRoomIds.Contains(r.Id))
                    .Select(r => new
                    {
                        Id = r.Id,
                        Name = r.Name ?? $"Phòng {r.Id}",
                        DefaultPrice = r.DefaultPrice,
                        FormattedPrice = r.DefaultPrice.ToString("N0") + " VNĐ",
                        Area = r.Area,
                        Floor = r.Floor,
                        IsCurrentlySelected = currentRoomIds.Contains(r.Id)
                    })
                    .OrderBy(r => r.Name)
                    .ToList();

                return Json(new { success = true, rooms = availableRooms },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message },
                    JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db?.Dispose();
            }
            base.Dispose(disposing);
        }



        ////////////PRINT CONTRACT
        public ActionResult Print(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            try
            {
                // Đường dẫn đến file template
                string templatePath = Server.MapPath("~/App_Data/Templates/HOPDONG.docx");

                if (!System.IO.File.Exists(templatePath))
                {
                    TempData["Error"] = "Không tìm thấy file mẫu hợp đồng!";
                    return RedirectToAction("Details", new { id });
                }

                // Load document template
                Document doc = new Document(templatePath);

                // Chuẩn bị dữ liệu để merge
                var mergeData = PrepareContractData(contract);

                // Thực hiện mail merge
                doc.MailMerge.Execute(mergeData.Keys.ToArray(), mergeData.Values.ToArray());


                // Tạo tên file output
                string fileName = $"HopDong_{contract.Id:D6}_{DateTime.Now:yyyyMMdd}.docx";

                // Lưu document vào memory stream
                using (MemoryStream stream = new MemoryStream())
                {
                    // Lưu dưới dạng DOCX
                    doc.Save(stream, SaveFormat.Docx);

                    // Trả về file để download
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi tạo hợp đồng: " + ex.Message;
                return RedirectToAction("Details", new { id });
            }
        }

        // Helper method để chuẩn bị dữ liệu merge
        private Dictionary<string, string> PrepareContractData(Contract contract)
        {
            var data = new Dictionary<string, string>();

            // Thông tin ngày tháng
            data["ngay"] = contract.StartDate.Day.ToString();
            data["thang"] = contract.StartDate.Month.ToString();
            data["nam"] = contract.StartDate.Year.ToString();

            // Thông tin phòng
            var rooms = contract.ContractRooms.Select(cr => cr.Room).ToList();
            if (rooms.Any())
            {
                data["sophong"] = string.Join(", ", rooms.Select(r => r.Name));
            }
            else
            {
                data["sophong"] = "";
                data["dientich"] = "16 m²";
            }

            // Thông tin bên thuê
            if (contract.ContractType == "Company" && contract.Company != null)
            {
                // Hợp đồng công ty
                var company = contract.Company;
                data["benthue"] = BuildCompanyInfo(company);
            }
            else
            {
                // Hợp đồng cá nhân
                var tenants = contract.ContractTenants
                    .OrderBy(ct => ct.CreatedAt)
                    .Select(ct => ct.Tenant)
                    .ToList();

                data["benthue"] = BuildTenantInfo(tenants);
            }

            // Thông tin giá thuê
            data["giathue"] = FormatCurrency(contract.PriceAgreed);
            data["giathue_text"] = NumberToText(contract.PriceAgreed);

            // Thông tin điện nước
            data["giadien"] = FormatNumber(contract.ElectricityPrice);
            data["gianuoc"] = FormatNumber(contract.WaterPrice);

            // Tiền cọc (thường là 2 tháng)
            decimal depositAmount = contract.PriceAgreed * 2;
            data["tiencoc"] = FormatCurrency(depositAmount);
            data["tiencoc_text"] = NumberToText(depositAmount);

            // Thời hạn hợp đồng
            data["ngaybatdau"] = contract.StartDate.ToString("dd/MM/yyyy");
            data["ngayketthuc"] = contract.EndDate.ToString("dd/MM/yyyy");

            // Tính số tháng
            int months = ((contract.EndDate.Year - contract.StartDate.Year) * 12)
                + contract.EndDate.Month - contract.StartDate.Month;
            data["sothang"] = months.ToString();

            // Ngày nhận phòng
            data["ngaynhanphong"] = contract.MoveInDate.ToString("dd/MM/yyyy");

            // Ghi chú
            data["ghichu"] = contract.Note ?? "";

            return data;
        }

        // Helper method xây dựng thông tin công ty
        private string BuildCompanyInfo(Company company)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Công ty: {company.CompanyName}");
            sb.AppendLine($"Mã số thuế: {company.TaxCode}");
            sb.AppendLine($"Địa chỉ: {company.Address}");
            sb.AppendLine($"Điện thoại: {company.Phone}");

            if (!string.IsNullOrEmpty(company.Representative))
            {
                sb.AppendLine($"Người đại diện: {company.Representative}");
                if (!string.IsNullOrEmpty(company.RepresentativePhone))
                    sb.AppendLine($"Số điện thoại người đại diện: {company.RepresentativePhone}");
            }

            return sb.ToString();
        }

        // Helper method xây dựng thông tin người thuê
        private string BuildTenantInfo(List<Tenant> tenants)
        {
            if (!tenants.Any())
                return "";

            var sb = new StringBuilder();

            // Người đại diện (người đầu tiên)
            var mainTenant = tenants.First();
            sb.AppendLine($"Họ và tên: {mainTenant.FullName}");
            sb.AppendLine($"CMND/CCCD: {mainTenant.IdentityCard}");
            sb.AppendLine($"Điện thoại: {mainTenant.PhoneNumber}");
            sb.AppendLine($"Địa chỉ thường trú: {mainTenant.PermanentAddress}");

            if (mainTenant.BirthDate.HasValue)
            {
                sb.AppendLine($"Ngày sinh: {mainTenant.BirthDate.Value:dd/MM/yyyy}");
            }

            // Nếu có nhiều người
            if (tenants.Count > 1)
            {
                sb.AppendLine("");
                sb.AppendLine("Cùng với những người sau:");

                for (int i = 1; i < tenants.Count; i++)
                {
                    var tenant = tenants[i];
                    sb.AppendLine($"{i}. {tenant.FullName} - CCCD: {tenant.IdentityCard}");
                }
            }

            return sb.ToString();
        }

        // Format tiền tệ
        private string FormatCurrency(decimal amount)
        {
            return amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        }

        // Format số
        private string FormatNumber(decimal number)
        {
            return number.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        }

        // Chuyển số thành chữ tiếng Việt
        private string NumberToText(decimal number)
        {
            if (number == 0)
                return "không đồng";

            string[] ones = { "", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
            string[] tens = { "", "", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };

            // Simplified implementation - cần thư viện chuyển đổi số sang chữ tiếng Việt đầy đủ
            // Ví dụ: 2000000 -> "hai triệu đồng"

            long amount = (long)number;

            if (amount < 1000)
                return ConvertHundreds(amount, ones, tens) + " đồng";

            if (amount < 1000000)
                return ConvertThousands(amount, ones, tens) + " đồng";

            if (amount < 1000000000)
                return ConvertMillions(amount, ones, tens) + " đồng";

            return amount.ToString("N0") + " đồng";
        }

        // Helper methods cho chuyển đổi số
        private string ConvertHundreds(long number, string[] ones, string[] tens)
        {
            // Implementation đơn giản
            return number.ToString();
        }

        private string ConvertThousands(long number, string[] ones, string[] tens)
        {
            long thousands = number / 1000;
            long remainder = number % 1000;

            string result = ConvertHundreds(thousands, ones, tens) + " nghìn";
            if (remainder > 0)
                result += " " + ConvertHundreds(remainder, ones, tens);

            return result;
        }

        private string ConvertMillions(long number, string[] ones, string[] tens)
        {
            long millions = number / 1000000;
            long remainder = number % 1000000;

            string result = ConvertHundreds(millions, ones, tens) + " triệu";
            if (remainder > 0)
            {
                if (remainder >= 1000)
                    result += " " + ConvertThousands(remainder, ones, tens);
                else
                    result += " " + ConvertHundreds(remainder, ones, tens);
            }

            return result;
        }
        /// END\\//////////
    }
}