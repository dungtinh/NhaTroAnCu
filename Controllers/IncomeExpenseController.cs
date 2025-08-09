using NhaTroAnCu.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using System.Diagnostics.Contracts;

namespace NhaTroAnCu.Controllers
{
    public class IncomeExpenseController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: IncomeExpense
        public ActionResult Index(
            int page = 1,
            int pageSize = 20,
            string fromDate = null,
            string toDate = null,
            string categoryFilter = null,
            string typeFilter = null)
        {
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", null);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", null);

            var query = db.IncomeExpenses
                .Include(i => i.IncomeExpenseCategory)
                .Include(i => i.Room)
                .Include(i => i.Contract)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(x => x.TransactionDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.TransactionDate <= to.Value);
            if (!string.IsNullOrEmpty(categoryFilter))
                query = query.Where(x => x.CategoryId.ToString() == categoryFilter);
            if (!string.IsNullOrEmpty(typeFilter))
                query = query.Where(x => x.IncomeExpenseCategory.Type == typeFilter);

            // Tính tổng
            var totalIncome = query.Where(x => x.IncomeExpenseCategory.Type == "Income").Sum(x => (decimal?)x.Amount) ?? 0;
            var totalExpense = query.Where(x => x.IncomeExpenseCategory.Type == "Expense").Sum(x => (decimal?)x.Amount) ?? 0;

            var totalItems = query.Count();

            var items = query
                .OrderByDescending(x => x.TransactionDate)
                .ThenByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new IncomeExpenseItemViewModel
                {
                    Id = x.Id,
                    CategoryName = x.IncomeExpenseCategory.Name,
                    CategoryIsSystem = x.IncomeExpenseCategory.IsSystem,
                    Type = x.IncomeExpenseCategory.Type,
                    Amount = x.Amount,
                    TransactionDate = x.TransactionDate,
                    Description = x.Description,
                    ReferenceNumber = x.ReferenceNumber,
                    RoomName = x.Room != null ? x.Room.Name : "",
                    ContractInfo = x.Contract != null ?
                        "HĐ #" + x.Contract.Id + " - Phòng " + x.Contract.Room.Name : "",
                    CreatedAt = x.CreatedAt
                }).ToList();

            var model = new IncomeExpenseListViewModel
            {
                Items = items,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                FromDate = from,
                ToDate = to,
                CategoryFilter = categoryFilter,
                TypeFilter = typeFilter,
                TotalIncome = totalIncome,
                TotalExpense = totalExpense,
                Balance = totalIncome - totalExpense
            };

            // Load categories for filter
            ViewBag.Categories = db.IncomeExpenseCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Type)
                .ThenBy(c => c.Name)
                .ToList();

            return View(model);
        }

        // GET: IncomeExpense/Create
        public ActionResult Create()
        {
            var model = new CreateIncomeExpenseViewModel
            {
                TransactionDate = DateTime.Now
            };

            LoadViewBagData();
            return View(model);
        }

        // POST: IncomeExpense/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateIncomeExpenseViewModel model)
        {
            if (ModelState.IsValid)
            {
                var incomeExpense = new IncomeExpense
                {
                    CategoryId = model.CategoryId,
                    ContractId = model.ContractId,
                    RoomId = model.RoomId,
                    Amount = model.Amount,
                    TransactionDate = model.TransactionDate,
                    Description = model.Description,
                    ReferenceNumber = model.ReferenceNumber,
                    CreatedBy = User.Identity.GetUserId(),
                    CreatedAt = DateTime.Now
                };

                db.IncomeExpenses.Add(incomeExpense);
                db.SaveChanges();

                TempData["Success"] = "Đã thêm giao dịch thành công!";
                return RedirectToAction("Index");
            }

            LoadViewBagData();
            return View(model);
        }

        // GET: IncomeExpense/Edit/5
        public ActionResult Edit(int id)
        {
            var incomeExpense = db.IncomeExpenses.Find(id);
            if (incomeExpense == null)
                return HttpNotFound();

            var model = new CreateIncomeExpenseViewModel
            {
                CategoryId = incomeExpense.CategoryId,
                ContractId = incomeExpense.ContractId,
                RoomId = incomeExpense.RoomId,
                Amount = incomeExpense.Amount,
                TransactionDate = incomeExpense.TransactionDate,
                Description = incomeExpense.Description,
                ReferenceNumber = incomeExpense.ReferenceNumber
            };

            LoadViewBagData();
            return View(model);
        }

        // POST: IncomeExpense/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, CreateIncomeExpenseViewModel model)
        {
            if (ModelState.IsValid)
            {
                var incomeExpense = db.IncomeExpenses.Find(id);
                if (incomeExpense == null)
                    return HttpNotFound();

                incomeExpense.CategoryId = model.CategoryId;
                incomeExpense.ContractId = model.ContractId;
                incomeExpense.RoomId = model.RoomId;
                incomeExpense.Amount = model.Amount;
                incomeExpense.TransactionDate = model.TransactionDate;
                incomeExpense.Description = model.Description;
                incomeExpense.ReferenceNumber = model.ReferenceNumber;
                incomeExpense.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                TempData["Success"] = "Đã cập nhật giao dịch thành công!";
                return RedirectToAction("Index");
            }

            LoadViewBagData();
            return View(model);
        }

        // POST: IncomeExpense/Delete/5
        [HttpPost]
        public ActionResult Delete(int id)
        {
            var incomeExpense = db.IncomeExpenses.Find(id);
            if (incomeExpense == null)
                return Json(new { success = false, message = "Không tìm thấy giao dịch" });

            db.IncomeExpenses.Remove(incomeExpense);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã xóa giao dịch thành công" });
        }

        // GET: IncomeExpense/Categories
        public ActionResult Categories()
        {
            var categories = db.IncomeExpenseCategories
                .OrderBy(c => c.Type)
                .ThenBy(c => c.Name)
                .ToList();

            var model = new CategoryManagementViewModel
            {
                Categories = categories,
                IncomeCount = categories.Count(c => c.Type == "Income"),
                ExpenseCount = categories.Count(c => c.Type == "Expense")
            };

            return View(model);
        }

        // POST: IncomeExpense/CreateCategory
        [HttpPost]
        public ActionResult CreateCategory(string name, string type)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Json(new { success = false, message = "Tên danh mục không được để trống" });

            if (type != "Income" && type != "Expense")
                return Json(new { success = false, message = "Loại danh mục không hợp lệ" });

            var category = new IncomeExpenseCategory
            {
                Name = name.Trim(),
                Type = type,
                IsSystem = false,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            db.IncomeExpenseCategories.Add(category);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã thêm danh mục thành công" });
        }

        // POST: IncomeExpense/UpdateCategory
        [HttpPost]
        public ActionResult UpdateCategory(int id, string name)
        {
            var category = db.IncomeExpenseCategories.Find(id);
            if (category == null)
                return Json(new { success = false, message = "Không tìm thấy danh mục" });

            if (category.IsSystem)
                return Json(new { success = false, message = "Không thể sửa danh mục hệ thống" });

            category.Name = name.Trim();
            category.UpdatedAt = DateTime.Now;
            db.SaveChanges();

            return Json(new { success = true, message = "Đã cập nhật danh mục thành công" });
        }

        // POST: IncomeExpense/DeleteCategory
        [HttpPost]
        public ActionResult DeleteCategory(int id)
        {
            var category = db.IncomeExpenseCategories.Find(id);
            if (category == null)
                return Json(new { success = false, message = "Không tìm thấy danh mục" });

            if (category.IsSystem)
                return Json(new { success = false, message = "Không thể xóa danh mục hệ thống" });

            // Kiểm tra có giao dịch nào sử dụng không
            var hasTransactions = db.IncomeExpenses.Any(i => i.CategoryId == id);
            if (hasTransactions)
            {
                // Soft delete
                category.IsActive = false;
                category.UpdatedAt = DateTime.Now;
                db.SaveChanges();
                return Json(new { success = true, message = "Đã ẩn danh mục (có giao dịch liên quan)" });
            }
            else
            {
                // Hard delete
                db.IncomeExpenseCategories.Remove(category);
                db.SaveChanges();
                return Json(new { success = true, message = "Đã xóa danh mục thành công" });
            }
        }

        // Helper method
        private void LoadViewBagData()
        {
            ViewBag.Categories = new SelectList(
                db.IncomeExpenseCategories.Where(c => c.IsActive && !c.IsSystem).OrderBy(c => c.Type).ThenBy(c => c.Name),
                "Id", "Name");

            ViewBag.Rooms = new SelectList(
                db.Rooms.OrderBy(r => r.Name),
                "Id", "Name");
        }
        // Auto record payment from PaymentHistory
        public void RecordRoomPayment(int paymentHistoryId)
        {
            var payment = db.PaymentHistories
                .Include(p => p.Contract)
                .FirstOrDefault(p => p.Id == paymentHistoryId);

            if (payment != null)
            {
                var incomeCategory = db.IncomeExpenseCategories
                    .FirstOrDefault(c => c.Name == "Thu tiền phòng" && c.IsSystem);

                if (incomeCategory != null)
                {
                    var incomeExpense = new IncomeExpense
                    {
                        CategoryId = incomeCategory.Id,
                        ContractId = payment.ContractId,
                        RoomId = payment.RoomId,
                        Amount = payment.TotalAmount,
                        TransactionDate = payment.PaidDate.Date,
                        Description = $"Thu tiền phòng tháng {payment.Month}/{payment.Year}",
                        ReferenceNumber = $"PAYMENT-{payment.Id}",
                        CreatedBy = "System",
                        CreatedAt = DateTime.Now
                    };

                    db.IncomeExpenses.Add(incomeExpense);
                    db.SaveChanges();
                }
            }
        }

        // Record deposit refund when ending contract
        public void RecordDepositRefund(int contractId, decimal amount, string note)
        {
            var contract = db.Contracts.Find(contractId);
            if (contract != null)
            {
                var expenseCategory = db.IncomeExpenseCategories
                    .FirstOrDefault(c => c.Name == "Trả tiền cọc" && c.IsSystem);

                if (expenseCategory != null)
                {
                    var incomeExpense = new IncomeExpense
                    {
                        CategoryId = expenseCategory.Id,
                        ContractId = contractId,
                        RoomId = contract.RoomId,
                        Amount = amount,
                        TransactionDate = DateTime.Now.Date,
                        Description = note ?? $"Trả tiền cọc khi kết thúc hợp đồng",
                        ReferenceNumber = $"DEPOSIT-{contractId}",
                        CreatedBy = User.Identity.GetUserId(),
                        CreatedAt = DateTime.Now
                    };

                    db.IncomeExpenses.Add(incomeExpense);
                    db.SaveChanges();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        // GET: IncomeExpense/GetContractsForRoom
        public ActionResult GetContractsForRoom(int roomId)
        {
            var contracts = db.Contracts
                .Where(c => c.RoomId == roomId && c.Status == "Active")
                .Select(c => new
                {
                    c.Id,
                    Display = "HĐ #" + c.Id + " - " + c.StartDate.Day + "/" + c.StartDate.Month + "/" + c.StartDate.Year
                })
                .ToList();

            return Json(contracts, JsonRequestBehavior.AllowGet);
        }

        // GET: IncomeExpense/Report
        public ActionResult Report(int? month, int? year)
        {
            var currentMonth = month ?? DateTime.Now.Month;
            var currentYear = year ?? DateTime.Now.Year;

            var startDate = new DateTime(currentYear, currentMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var transactions = db.IncomeExpenses
                .Include(i => i.IncomeExpenseCategory)
                .Where(i => i.TransactionDate >= startDate && i.TransactionDate <= endDate)
                .OrderBy(i => i.TransactionDate)
                .ToList();

            var income = transactions.Where(t => t.IncomeExpenseCategory.Type == "Income").Sum(t => t.Amount);
            var expense = transactions.Where(t => t.IncomeExpenseCategory.Type == "Expense").Sum(t => t.Amount);

            ViewBag.Month = currentMonth;
            ViewBag.Year = currentYear;
            ViewBag.TotalIncome = income;
            ViewBag.TotalExpense = expense;
            ViewBag.Balance = income - expense;
            ViewBag.Transactions = transactions;

            // Controller: Trước khi trả về View
            var expenseChartData = db.IncomeExpenses
                .Where(x => x.IncomeExpenseCategory.Type == "Expense" && x.TransactionDate.Month == currentMonth && x.TransactionDate.Year == currentYear)
                .GroupBy(x => x.IncomeExpenseCategory.Name)
                .Select(g => new { Category = g.Key, TotalAmount = g.Sum(x => x.Amount) })
                .ToList();

            ViewBag.ExpenseChartData = expenseChartData;

            return View();
        }
        [HttpPost]
        public ActionResult CreateDeposit(int contractId, decimal amount, string description = null)
        {
            try
            {
                var contract = db.Contracts.Find(contractId);
                if (contract == null)
                    return Json(new { success = false, message = "Không tìm thấy hợp đồng" });

                if (contract.DepositCollected)
                    return Json(new { success = false, message = "Tiền cọc đã được thu trước đó" });

                // Get deposit category
                var depositCategory = db.IncomeExpenseCategories
                    .FirstOrDefault(c => c.Name == "Thu tiền cọc" && c.IsSystem);

                if (depositCategory == null)
                {
                    // Create if not exists
                    depositCategory = new IncomeExpenseCategory
                    {
                        Name = "Thu tiền cọc",
                        Type = "Income",
                        IsSystem = true,
                        CreatedAt = DateTime.Now
                    };
                    db.IncomeExpenseCategories.Add(depositCategory);
                    db.SaveChanges();
                }

                // Create income record
                var income = new IncomeExpense
                {
                    CategoryId = depositCategory.Id,
                    Amount = amount,
                    Description = description ?? $"Thu tiền cọc phòng {contract.Room.Name}",
                    TransactionDate = DateTime.Now,
                    RoomId = contract.RoomId,
                    ContractId = contractId,
                    CreatedBy = User.Identity.Name ?? "System",
                    CreatedAt = DateTime.Now
                };

                db.IncomeExpenses.Add(income);
                db.SaveChanges();

                // Update contract
                contract.DepositCollected = true;
                contract.DepositCollectedDate = DateTime.Now;
                contract.DepositIncomeId = income.Id;
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Đã thu tiền cọc {amount:N0}đ thành công!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}