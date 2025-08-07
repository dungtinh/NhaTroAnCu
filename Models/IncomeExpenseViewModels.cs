using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaTroAnCu.Models
{
    public class IncomeExpenseListViewModel
    {
        public List<IncomeExpenseItemViewModel> Items { get; set; }
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string CategoryFilter { get; set; }
        public string TypeFilter { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal Balance { get; set; }
    }

    public class IncomeExpenseItemViewModel
    {
        public int Id { get; set; }
        public string CategoryName { get; set; }
        public bool CategoryIsSystem { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; }
        public string ReferenceNumber { get; set; }
        public string RoomName { get; set; }
        public string ContractInfo { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateIncomeExpenseViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        public int CategoryId { get; set; }

        public int? ContractId { get; set; }
        public int? RoomId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số tiền")]
        [Range(1, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày giao dịch")]
        public DateTime TransactionDate { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(50)]
        public string ReferenceNumber { get; set; }
    }

    public class CategoryManagementViewModel
    {
        public List<IncomeExpenseCategory> Categories { get; set; }
        public int IncomeCount { get; set; }
        public int ExpenseCount { get; set; }
    }
}