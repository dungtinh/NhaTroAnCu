using System;
using System.Collections.Generic;
using System.Linq;

namespace NhaTroAnCu.Models
{
    // ========== THỐNG KÊ THU CHI TỔNG QUAN ==========
    public class IncomeExpenseStatistics
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal Balance { get; set; }

        // Phân loại thu nhập
        public decimal IndividualContractIncome { get; set; }  // Thu từ HĐ cá nhân
        public decimal CompanyContractIncome { get; set; }     // Thu từ HĐ công ty
        public decimal OtherIncome { get; set; }              // Thu khác (không thuộc HĐ)

        // Thống kê theo danh mục
        public List<CategoryStatistic> CategoryStatistics { get; set; }

        // Thống kê theo thời gian
        public List<MonthlyStatistic> MonthlyStatistics { get; set; }

        public string Period { get; set; } // Mô tả khoảng thời gian thống kê

        // Tính toán tỷ lệ
        public decimal IncomeExpenseRatio => TotalExpense > 0 ? TotalIncome / TotalExpense : 0;
        public decimal ProfitMargin => TotalIncome > 0 ? (Balance / TotalIncome) * 100 : 0;
    }

    // ========== THỐNG KÊ THEO DANH MỤC ==========
    public class CategoryStatistic
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string Type { get; set; } // Income/Expense
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public decimal Percentage { get; set; } // % so với tổng thu/chi

        // Icon và màu sắc cho UI
        public string GetIcon()
        {
            switch (CategoryName)
            {
                case "Thu tiền phòng": return "fas fa-home";
                case "Tiền điện chung": return "fas fa-bolt";
                case "Tiền nước chung": return "fas fa-tint";
                case "Thu tiền cọc": return "fas fa-shield-alt";
                case "Thuê bảo vệ": return "fas fa-user-shield";
                case "Rác thải": return "fas fa-trash";
                case "Sửa chữa": return "fas fa-tools";
                default: return "fas fa-dollar-sign";
            }
        }

        public string GetColor()
        {
            if (Type == "Income")
            {
                switch (CategoryName)
                {
                    case "Tiền phòng": return "#27ae60";
                    case "Tiền điện": return "#f39c12";
                    case "Tiền nước": return "#3498db";
                    case "Tiền cọc": return "#9b59b6";
                    default: return "#2ecc71";
                }
            }
            else
            {
                switch (CategoryName)
                {
                    case "Bảo vệ": return "#e74c3c";
                    case "Rác thải": return "#95a5a6";
                    case "Sửa chữa": return "#e67e22";
                    case "Internet": return "#34495e";
                    default: return "#c0392b";
                }
            }
        }
    }

    // ========== THỐNG KÊ THEO THÁNG ==========
    public class MonthlyStatistic
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal Balance => TotalIncome - TotalExpense;

        // Chi tiết theo loại hợp đồng
        public decimal IndividualIncome { get; set; }
        public decimal CompanyIncome { get; set; }
        public decimal OtherIncome { get; set; }

        // So sánh với tháng trước
        public decimal IncomeGrowth { get; set; } // % tăng/giảm
        public decimal ExpenseGrowth { get; set; }

        public string MonthYearDisplay => $"{Month:00}/{Year}";
    }

    // ========== FILTER CHO THỐNG KÊ ==========
    public class StatisticsFilter
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? CategoryId { get; set; }
        public string Type { get; set; } // Income/Expense/All
        public string ContractType { get; set; } // Individual/Company/All
        public int? ContractId { get; set; }

        public string GetPeriodDescription()
        {
            if (FromDate.HasValue && ToDate.HasValue)
                return $"Từ {FromDate.Value:dd/MM/yyyy} đến {ToDate.Value:dd/MM/yyyy}";
            if (FromDate.HasValue)
                return $"Từ {FromDate.Value:dd/MM/yyyy}";
            if (ToDate.HasValue)
                return $"Đến {ToDate.Value:dd/MM/yyyy}";
            return "Tất cả thời gian";
        }
    }

    // ========== DASHBOARD TỔNG QUAN ==========
    public class IncomeExpenseDashboard
    {
        // Thống kê tháng hiện tại
        public MonthlyStatistic CurrentMonth { get; set; }

        // So sánh với tháng trước
        public MonthlyStatistic LastMonth { get; set; }

        // Top danh mục thu/chi
        public List<CategoryStatistic> TopIncomeCategories { get; set; }
        public List<CategoryStatistic> TopExpenseCategories { get; set; }

        // Phòng chưa thanh toán
        public List<UnpaidRoom> UnpaidRooms { get; set; }

        // Biểu đồ 12 tháng gần nhất
        public List<MonthlyStatistic> YearlyTrend { get; set; }

        // Cảnh báo
        public List<FinancialAlert> Alerts { get; set; }
    }

    // ========== PHÒNG CHƯA THANH TOÁN ==========
    public class UnpaidRoom
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public string TenantName { get; set; }
        public string ContractType { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal AmountDue { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Balance => AmountDue - AmountPaid;
        public int DaysOverdue { get; set; }
    }

    // ========== CẢNH BÁO TÀI CHÍNH ==========
    public class FinancialAlert
    {
        public string Type { get; set; } // Warning, Danger, Info
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }

        public string GetAlertClass()
        {
            switch (Type)
            {
                case "Warning": return "alert-warning";
                case "Danger": return "alert-danger";
                case "Info": return "alert-info";
                default: return "alert-secondary";
            }
        }

        public string GetIcon()
        {
            switch (Type)
            {
                case "Warning": return "fas fa-exclamation-triangle";
                case "Danger": return "fas fa-times-circle";
                case "Info": return "fas fa-info-circle";
                default: return "fas fa-bell";
            }
        }
    }

    // ========== BÁO CÁO XUẤT FILE ==========
    public class ExportReportViewModel
    {
        public string ReportType { get; set; } // Monthly, Yearly, Custom
        public int? Month { get; set; }
        public int? Year { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Format { get; set; } // Excel, PDF
        public bool IncludeDetails { get; set; }
        public bool GroupByCategory { get; set; }
        public bool GroupByContract { get; set; }
    }
    // ========== VIEW MODELS ==========

    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    public class RoomPaymentViewModel
    {
        public int ContractId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string Note { get; set; }
        public string UserId { get; set; }
    }

    public class GeneralTransactionViewModel
    {
        public int CategoryId { get; set; }
        public int? ContractId { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; }
        public string ReferenceNumber { get; set; }
        public string UserId { get; set; }
    }

    public class RoomPaymentSummary
    {
        public int ContractId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal BillAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Balance { get; set; } // Dương = thừa, Âm = thiếu
        public int PaymentCount { get; set; }
        public List<PaymentItem> Payments { get; set; }
        public bool HasBill { get; set; }
        public int? BillId { get; set; }
        public bool IsPaidInFull { get; set; }
        public bool IsOverpaid { get; set; }
    }

    public class PaymentItem
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string ReferenceNumber { get; set; }
        public string Description { get; set; }
    }

    public class PaymentBillReport
    {
        public int ContractId { get; set; }
        public string ContractType { get; set; }
        public List<PaymentBillReportItem> Items { get; set; }
        public decimal TotalBilled { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal FinalBalance { get; set; }
    }

    public class PaymentBillReportItem
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public int BillId { get; set; }
        public decimal BillAmount { get; set; }
        public int ElectricityUsage { get; set; }
        public int WaterUsage { get; set; }
        public decimal TotalPaid { get; set; }
        public int PaymentCount { get; set; }
        public decimal MonthlyBalance { get; set; }
        public decimal CumulativeBalance { get; set; }
        public string Status { get; set; }
        public List<PaymentDetail> Payments { get; set; }
    }

    public class PaymentDetail
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; }
    }
    public class MonthlyPaymentHistory
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public UtilityBill Bill { get; set; }
        public List<IncomeExpense> Payments { get; set; }
        public decimal TotalBilled { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal MonthBalance { get; set; }
        public decimal CumulativeDeposit { get; set; }
    }
}