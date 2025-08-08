public class ContractEditViewModel
{
    // ... các thuộc tính khác ...

    public decimal DepositAmount { get; set; }

    // Thu cọc ngay khi chỉnh sửa hợp đồng
    public bool CollectDepositNow { get; set; }
    public decimal? ActualDepositCollected { get; set; }
}