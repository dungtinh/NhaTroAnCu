using System;
using System.Collections.Generic;
using System.Linq;
using NhaTroAnCu.Models;

namespace NhaTroAnCu.Services
{
    public class IncomeExpenseService
    {
        private NhaTroAnCuEntities db;

        public IncomeExpenseService(NhaTroAnCuEntities context)
        {
            db = context;
        }

        // ========== 1. GHI NHẬN THANH TOÁN TIỀN PHÒNG ==========
        // Không kiểm tra bill, không giới hạn số tiền
        public ServiceResult RecordRoomPayment(RoomPaymentViewModel model)
        {
            try
            {
                // Lấy category "Tiền phòng"
                var category = GetOrCreateCategory("Tiền phòng", "Income", true);

                // Tạo mô tả thanh toán
                string description = GenerateRoomPaymentDescription(model);

                // Tạo record thu tiền
                var income = new IncomeExpense
                {
                    CategoryId = category.Id,
                    ContractId = model.ContractId,
                    Amount = model.Amount,
                    TransactionDate = model.PaymentDate,
                    Description = description,
                    ReferenceNumber = GenerateReferenceNumber("PAY", model.ContractId),
                    CreatedBy = model.UserId,
                    CreatedAt = DateTime.Now
                };

                db.IncomeExpenses.Add(income);
                db.SaveChanges();

                return new ServiceResult
                {
                    Success = true,
                    Message = $"Đã ghi nhận thanh toán {model.Amount:N0}đ thành công",
                    Data = income.Id
                };
            }
            catch (Exception ex)
            {
                return new ServiceResult
                {
                    Success = false,
                    Message = "Lỗi: " + ex.Message
                };
            }
        }

        // ========== 2. GHI NHẬN THU CHI KHÁC ==========
        public ServiceResult RecordGeneralTransaction(GeneralTransactionViewModel model)
        {
            try
            {
                var category = db.IncomeExpenseCategories.Find(model.CategoryId);
                if (category == null)
                    return new ServiceResult { Success = false, Message = "Danh mục không tồn tại" };

                var transaction = new IncomeExpense
                {
                    CategoryId = model.CategoryId,
                    ContractId = model.ContractId, // Có thể null cho thu chi ngoài hợp đồng
                    Amount = model.Amount,
                    TransactionDate = model.TransactionDate,
                    Description = model.Description,
                    ReferenceNumber = string.IsNullOrEmpty(model.ReferenceNumber)
                        ? GenerateReferenceNumber(category.Type == "Income" ? "IN" : "EX", null)
                        : model.ReferenceNumber,
                    CreatedBy = model.UserId,
                    CreatedAt = DateTime.Now
                };

                db.IncomeExpenses.Add(transaction);
                db.SaveChanges();

                string typeText = category.Type == "Income" ? "thu" : "chi";
                return new ServiceResult
                {
                    Success = true,
                    Message = $"Đã ghi nhận {typeText} {model.Amount:N0}đ - {category.Name}",
                    Data = transaction.Id
                };
            }
            catch (Exception ex)
            {
                return new ServiceResult
                {
                    Success = false,
                    Message = "Lỗi: " + ex.Message
                };
            }
        }

        // ========== 3. LẤY THÔNG TIN THANH TOÁN CHO PHÒNG/HỢP ĐỒNG ==========
        public RoomPaymentSummary GetRoomPaymentSummary(int contractId, int month, int year)
        {
            var contract = db.Contracts
                .Include("ContractRooms.Room")
                .Include("Company")
                .FirstOrDefault(c => c.Id == contractId);

            if (contract == null)
                return null;

            // Lấy tất cả thanh toán tiền phòng trong tháng
            var roomPayments = db.IncomeExpenses
                .Where(ie => ie.ContractId == contractId
                    && ie.IncomeExpenseCategory.Name == "Thu tiền phòng"
                    && ie.TransactionDate.Month == month
                    && ie.TransactionDate.Year == year)
                .OrderBy(ie => ie.TransactionDate)
                .ToList();

            // Lấy bill của tháng (nếu có) - SỬA LẠI CHO ĐÚNG VỚI MODEL
            var bill = db.UtilityBills
                .FirstOrDefault(b => b.ContractId == contractId
                    && b.Month == month
                    && b.Year == year);

            // Tính toán
            decimal totalPaid = roomPayments.Sum(p => p.Amount);
            decimal billAmount = bill?.TotalAmount ?? 0;
            decimal balance = totalPaid - billAmount; // Dương = thừa, Âm = thiếu

            return new RoomPaymentSummary
            {
                ContractId = contractId,
                Month = month,
                Year = year,
                BillAmount = billAmount,
                TotalPaid = totalPaid,
                Balance = balance,
                PaymentCount = roomPayments.Count,
                Payments = roomPayments.Select(p => new PaymentItem
                {
                    Id = p.Id,
                    Date = p.TransactionDate,
                    Amount = p.Amount,
                    ReferenceNumber = p.ReferenceNumber,
                    Description = p.Description
                }).ToList(),
                HasBill = bill != null,
                BillId = bill?.Id,
                IsPaidInFull = totalPaid >= billAmount && billAmount > 0,
                IsOverpaid = totalPaid > billAmount && billAmount > 0
            };
        }

        // ========== 4. BÁO CÁO TỔNG HỢP THANH TOÁN VÀ PHIẾU BÁO ==========
        public PaymentBillReport GetPaymentBillReport(int contractId, int? month, int? year)
        {
            var contract = db.Contracts.Find(contractId);
            if (contract == null) return null;

            // Lấy tất cả bills
            var billsQuery = db.UtilityBills.Where(b => b.ContractId == contractId);
            if (month.HasValue && year.HasValue)
            {
                billsQuery = billsQuery.Where(b => b.Month == month.Value && b.Year == year.Value);
            }
            else if (year.HasValue)
            {
                billsQuery = billsQuery.Where(b => b.Year == year.Value);
            }

            var bills = billsQuery.OrderByDescending(b => b.Year).ThenByDescending(b => b.Month).ToList();

            var reportItems = new List<PaymentBillReportItem>();
            decimal cumulativeBalance = 0; // Số dư tích lũy

            foreach (var bill in bills)
            {
                // Lấy tất cả thanh toán trong tháng
                var payments = db.IncomeExpenses
                    .Where(ie => ie.ContractId == contractId
                        && ie.IncomeExpenseCategory.Name == "Thu tiền phòng"
                        && ie.TransactionDate.Month == bill.Month
                        && ie.TransactionDate.Year == bill.Year)
                    .OrderBy(ie => ie.TransactionDate)
                    .ToList();

                decimal monthlyPaid = payments.Sum(p => p.Amount);
                decimal monthlyBalance = monthlyPaid - bill.TotalAmount;
                cumulativeBalance += monthlyBalance;

                // Tính số điện nước sử dụng
                int electricityUsage = 0;
                int waterUsage = bill.WaterIndexEnd - bill.WaterIndexStart;

                // Tính điện từ ElectricityAmount (nếu có công thức tính ngược)
                // Giả sử giá điện trung bình 3000đ/kWh
                if (bill.ElectricityAmount > 0)
                {
                    electricityUsage = (int)(bill.ElectricityAmount / 3000);
                }

                reportItems.Add(new PaymentBillReportItem
                {
                    Month = bill.Month,
                    Year = bill.Year,
                    BillId = bill.Id,
                    BillAmount = bill.TotalAmount,
                    ElectricityUsage = electricityUsage,
                    WaterUsage = waterUsage,
                    TotalPaid = monthlyPaid,
                    PaymentCount = payments.Count,
                    MonthlyBalance = monthlyBalance,
                    CumulativeBalance = cumulativeBalance,
                    Status = GetPaymentStatus(monthlyBalance, bill.TotalAmount),
                    Payments = payments.Select(p => new PaymentDetail
                    {
                        Date = p.TransactionDate,
                        Amount = p.Amount,
                        Reference = p.ReferenceNumber
                    }).ToList()
                });
            }

            return new PaymentBillReport
            {
                ContractId = contractId,
                ContractType = contract.ContractType,
                Items = reportItems,
                TotalBilled = reportItems.Sum(i => i.BillAmount),
                TotalPaid = reportItems.Sum(i => i.TotalPaid),
                FinalBalance = cumulativeBalance
            };
        }

        // ========== HELPER METHODS ==========

        private IncomeExpenseCategory GetOrCreateCategory(string name, string type, bool isSystem)
        {
            var category = db.IncomeExpenseCategories
                .FirstOrDefault(c => c.Name == name && c.Type == type);

            if (category == null)
            {
                category = new IncomeExpenseCategory
                {
                    Name = name,
                    Type = type,
                    IsSystem = isSystem,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                db.IncomeExpenseCategories.Add(category);
                db.SaveChanges();
            }

            return category;
        }

        private string GenerateReferenceNumber(string prefix, int? contractId)
        {
            string reference = $"{prefix}-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}";
            if (contractId.HasValue)
                reference = $"{prefix}-C{contractId}-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}";
            return reference;
        }

        private string GenerateRoomPaymentDescription(RoomPaymentViewModel model)
        {
            var contract = db.Contracts
                .Include("ContractRooms.Room")
                .Include("Company")
                .Include("ContractTenants.Tenant")
                .FirstOrDefault(c => c.Id == model.ContractId);

            if (contract == null)
                return $"Thu tiền phòng - Tháng {model.Month}/{model.Year}";

            string description = "";

            if (contract.ContractType == "Individual")
            {
                var room = contract.ContractRooms.FirstOrDefault()?.Room;
                var tenant = contract.ContractTenants.FirstOrDefault()?.Tenant;

                description = $"Thu tiền phòng {room?.Name ?? "N/A"}";
                if (tenant != null)
                    description += $" - {tenant.FullName}";
            }
            else // Company
            {
                var roomCount = contract.ContractRooms.Count;
                var roomNames = string.Join(", ", contract.ContractRooms.Take(3).Select(cr => cr.Room.Name));
                if (roomCount > 3)
                    roomNames += $" và {roomCount - 3} phòng khác";

                description = $"Thu tiền {contract.Company.CompanyName} - {roomCount} phòng ({roomNames})";
            }

            description += $" - Tháng {model.Month}/{model.Year}";

            if (!string.IsNullOrEmpty(model.Note))
                description += $" - {model.Note}";

            return description;
        }

        private string GetPaymentStatus(decimal balance, decimal billAmount)
        {
            if (billAmount == 0) return "Chưa có phiếu";
            if (balance == 0) return "Đã thanh toán đủ";
            if (balance > 0) return $"Thừa {balance:N0}đ";
            return $"Thiếu {Math.Abs(balance):N0}đ";
        }
        public class StatisticsService
        {
            private NhaTroAnCuEntities db;

            public StatisticsService(NhaTroAnCuEntities context)
            {
                db = context;
            }

            // Lấy thống kê tổng quan
            public IncomeExpenseStatistics GetStatistics(StatisticsFilter filter)
            {
                var query = db.IncomeExpenses.AsQueryable();

                // Áp dụng filters
                if (filter.FromDate.HasValue)
                    query = query.Where(ie => ie.TransactionDate >= filter.FromDate.Value);

                if (filter.ToDate.HasValue)
                    query = query.Where(ie => ie.TransactionDate <= filter.ToDate.Value);

                if (filter.CategoryId.HasValue)
                    query = query.Where(ie => ie.CategoryId == filter.CategoryId.Value);

                if (!string.IsNullOrEmpty(filter.Type) && filter.Type != "All")
                    query = query.Where(ie => ie.IncomeExpenseCategory.Type == filter.Type);

                if (!string.IsNullOrEmpty(filter.ContractType) && filter.ContractType != "All")
                    query = query.Where(ie => ie.Contract != null && ie.Contract.ContractType == filter.ContractType);

                // Tính toán
                var incomeQuery = query.Where(ie => ie.IncomeExpenseCategory.Type == "Income");
                var expenseQuery = query.Where(ie => ie.IncomeExpenseCategory.Type == "Expense");

                var statistics = new IncomeExpenseStatistics
                {
                    TotalIncome = incomeQuery.Sum(ie => (decimal?)ie.Amount) ?? 0,
                    TotalExpense = expenseQuery.Sum(ie => (decimal?)ie.Amount) ?? 0,
                    Period = filter.GetPeriodDescription()
                };

                // Thu nhập theo loại hợp đồng
                statistics.IndividualContractIncome = incomeQuery
                    .Where(ie => ie.Contract != null && ie.Contract.ContractType == "Individual")
                    .Sum(ie => (decimal?)ie.Amount) ?? 0;

                statistics.CompanyContractIncome = incomeQuery
                    .Where(ie => ie.Contract != null && ie.Contract.ContractType == "Company")
                    .Sum(ie => (decimal?)ie.Amount) ?? 0;

                statistics.OtherIncome = incomeQuery
                    .Where(ie => ie.ContractId == null)
                    .Sum(ie => (decimal?)ie.Amount) ?? 0;

                statistics.Balance = statistics.TotalIncome - statistics.TotalExpense;

                // Thống kê theo danh mục
                statistics.CategoryStatistics = GetCategoryStatistics(query);

                // Thống kê theo tháng (nếu có filter thời gian)
                if (filter.FromDate.HasValue || filter.ToDate.HasValue)
                {
                    statistics.MonthlyStatistics = GetMonthlyStatistics(filter);
                }

                return statistics;
            }

            // Lấy thống kê theo danh mục
            private List<CategoryStatistic> GetCategoryStatistics(IQueryable<IncomeExpense> query)
            {
                var categoryStats = query
                    .GroupBy(ie => new {
                        ie.CategoryId,
                        ie.IncomeExpenseCategory.Name,
                        ie.IncomeExpenseCategory.Type
                    })
                    .Select(g => new CategoryStatistic
                    {
                        CategoryId = g.Key.CategoryId,
                        CategoryName = g.Key.Name,
                        Type = g.Key.Type,
                        TotalAmount = g.Sum(ie => ie.Amount),
                        TransactionCount = g.Count()
                    })
                    .ToList();

                // Tính phần trăm
                var totalIncome = categoryStats.Where(c => c.Type == "Income").Sum(c => c.TotalAmount);
                var totalExpense = categoryStats.Where(c => c.Type == "Expense").Sum(c => c.TotalAmount);

                foreach (var stat in categoryStats)
                {
                    var total = stat.Type == "Income" ? totalIncome : totalExpense;
                    stat.Percentage = total > 0 ? (stat.TotalAmount / total) * 100 : 0;
                }

                return categoryStats.OrderByDescending(c => c.TotalAmount).ToList();
            }

            // Lấy thống kê theo tháng
            private List<MonthlyStatistic> GetMonthlyStatistics(StatisticsFilter filter)
            {
                var startDate = filter.FromDate ?? DateTime.Now.AddMonths(-11);
                var endDate = filter.ToDate ?? DateTime.Now;

                var monthlyData = new List<MonthlyStatistic>();
                var currentDate = new DateTime(startDate.Year, startDate.Month, 1);

                while (currentDate <= endDate)
                {
                    var monthEnd = currentDate.AddMonths(1).AddDays(-1);

                    var monthQuery = db.IncomeExpenses
                        .Where(ie => ie.TransactionDate >= currentDate && ie.TransactionDate <= monthEnd);

                    var stat = new MonthlyStatistic
                    {
                        Month = currentDate.Month,
                        Year = currentDate.Year,
                        TotalIncome = monthQuery
                            .Where(ie => ie.IncomeExpenseCategory.Type == "Income")
                            .Sum(ie => (decimal?)ie.Amount) ?? 0,
                        TotalExpense = monthQuery
                            .Where(ie => ie.IncomeExpenseCategory.Type == "Expense")
                            .Sum(ie => (decimal?)ie.Amount) ?? 0
                    };

                    // Thu nhập theo loại hợp đồng
                    stat.IndividualIncome = monthQuery
                        .Where(ie => ie.IncomeExpenseCategory.Type == "Income"
                            && ie.Contract != null
                            && ie.Contract.ContractType == "Individual")
                        .Sum(ie => (decimal?)ie.Amount) ?? 0;

                    stat.CompanyIncome = monthQuery
                        .Where(ie => ie.IncomeExpenseCategory.Type == "Income"
                            && ie.Contract != null
                            && ie.Contract.ContractType == "Company")
                        .Sum(ie => (decimal?)ie.Amount) ?? 0;

                    stat.OtherIncome = monthQuery
                        .Where(ie => ie.IncomeExpenseCategory.Type == "Income"
                            && ie.ContractId == null)
                        .Sum(ie => (decimal?)ie.Amount) ?? 0;

                    monthlyData.Add(stat);
                    currentDate = currentDate.AddMonths(1);
                }

                // Tính % tăng trưởng
                for (int i = 1; i < monthlyData.Count; i++)
                {
                    var current = monthlyData[i];
                    var previous = monthlyData[i - 1];

                    current.IncomeGrowth = previous.TotalIncome > 0
                        ? ((current.TotalIncome - previous.TotalIncome) / previous.TotalIncome) * 100
                        : 0;

                    current.ExpenseGrowth = previous.TotalExpense > 0
                        ? ((current.TotalExpense - previous.TotalExpense) / previous.TotalExpense) * 100
                        : 0;
                }

                return monthlyData;
            }
        }
    }    
}