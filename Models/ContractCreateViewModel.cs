public class ContractCreateViewModel
{
    // ... các thuộc tính khác ...

    public decimal DepositAmount { get; set; }

    // Thu cọc ngay khi tạo hợp đồng
    public bool CollectDepositNow { get; set; }
    public decimal? ActualDepositCollected { get; set; }
}