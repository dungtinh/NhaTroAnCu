using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

public class TransactionFilter
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? CategoryId { get; set; }
    public string? Type { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record TransactionRow(int Id, string CategoryName, bool CategoryIsSystem, string Type, decimal Amount, DateTime TransactionDate,
    string? Description, string? ReferenceNumber, DateTime CreatedAt, int? ContractId, string? ContractInfo);

public record TransactionList(List<TransactionRow> Items, int TotalItems, decimal TotalIncome, decimal TotalExpense)
{
    public decimal Balance => TotalIncome - TotalExpense;
}

public class TransactionForm
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public int? ContractId { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Today;
    public string? Description { get; set; }
    public string? ReferenceNumber { get; set; }
}

public record ContractOption(int Id, string Display);

public record MonthlyReport(int Month, int Year, decimal TotalIncome, decimal TotalExpense, List<IncomeExpense> Transactions,
    List<(string Category, decimal Amount)> ExpenseByCategory, List<(string Category, decimal Amount)> IncomeByCategory)
{
    public decimal Balance => TotalIncome - TotalExpense;
}

public class IncomeExpenseService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<TransactionList> ListAsync(TransactionFilter f)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.IncomeExpenses.AsNoTracking().AsQueryable();
        if (f.FromDate.HasValue) q = q.Where(x => x.TransactionDate >= f.FromDate.Value.Date);
        if (f.ToDate.HasValue) q = q.Where(x => x.TransactionDate <= f.ToDate.Value.Date);
        if (f.CategoryId.HasValue) q = q.Where(x => x.CategoryId == f.CategoryId.Value);
        if (!string.IsNullOrEmpty(f.Type)) q = q.Where(x => x.Category.Type == f.Type);

        decimal income = await q.Where(x => x.Category.Type == CategoryType.Income).SumAsync(x => (decimal?)x.Amount) ?? 0;
        decimal expense = await q.Where(x => x.Category.Type == CategoryType.Expense).SumAsync(x => (decimal?)x.Amount) ?? 0;
        int total = await q.CountAsync();

        int pageSize = Math.Clamp(f.PageSize, 5, 200);
        var raw = await q.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.CreatedAt)
            .Skip((Math.Max(1, f.Page) - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, CategoryName = x.Category.Name, x.Category.IsSystem, x.Category.Type, x.Amount, x.TransactionDate, x.Description,
                x.ReferenceNumber, x.CreatedAt, x.ContractId,
                ContractType = x.Contract != null ? x.Contract.ContractType : null,
                CompanyName = x.Contract != null && x.Contract.Company != null ? x.Contract.Company.CompanyName : null,
                Rooms = x.Contract != null ? x.Contract.ContractRooms.Select(cr => cr.Room.Name).ToList() : new List<string>()
            })
            .ToListAsync();

        var items = raw.Select(x => new TransactionRow(x.Id, x.CategoryName, x.IsSystem, x.Type, x.Amount, x.TransactionDate, x.Description,
            x.ReferenceNumber, x.CreatedAt, x.ContractId,
            x.ContractId == null ? null
                : x.ContractType == ContractType.Company && x.CompanyName != null ? $"{x.CompanyName} - {string.Join(", ", x.Rooms)}"
                : string.Join(", ", x.Rooms))).ToList();

        return new TransactionList(items, total, income, expense);
    }

    public async Task<List<IncomeExpenseCategory>> ListCategoriesAsync(bool onlySelectable = false)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.IncomeExpenseCategories.AsNoTracking();
        if (onlySelectable) q = q.Where(c => c.IsActive && !c.IsSystem);
        return await q.OrderBy(c => c.Type).ThenBy(c => c.Name).ToListAsync();
    }

    public async Task<List<ContractOption>> ListActiveContractsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var raw = await db.Contracts.AsNoTracking()
            .Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Expired)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, Company = c.Company != null ? c.Company.CompanyName : null, Rooms = c.ContractRooms.Select(cr => cr.Room.Name).ToList() })
            .ToListAsync();
        return raw.Select(c => new ContractOption(c.Id, $"HĐ #{c.Id} - Phòng: {string.Join(", ", c.Rooms)}{(c.Company != null ? " - " + c.Company : "")}")).ToList();
    }

    public async Task<TransactionForm?> GetFormAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var x = await db.IncomeExpenses.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        return x == null ? null : new TransactionForm
        {
            Id = x.Id, CategoryId = x.CategoryId, ContractId = x.ContractId, Amount = x.Amount, TransactionDate = x.TransactionDate,
            Description = x.Description, ReferenceNumber = x.ReferenceNumber
        };
    }

    public async Task SaveAsync(TransactionForm form, string? userId)
    {
        if (form.CategoryId <= 0) throw new InvalidOperationException("Vui lòng chọn danh mục.");
        if (form.Amount <= 0) throw new InvalidOperationException("Số tiền phải lớn hơn 0.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var category = await db.IncomeExpenseCategories.FindAsync(form.CategoryId) ?? throw new InvalidOperationException("Danh mục không tồn tại.");

        IncomeExpense entity;
        if (form.Id == 0)
        {
            entity = new IncomeExpense { CreatedBy = userId, CreatedAt = DateTime.Now };
            db.IncomeExpenses.Add(entity);
            if (string.IsNullOrWhiteSpace(form.ReferenceNumber))
                form.ReferenceNumber = $"{(category.Type == CategoryType.Income ? "IN" : "EX")}-{DateTime.Now:yyyyMMdd-HHmmss}";
        }
        else
        {
            entity = await db.IncomeExpenses.FindAsync(form.Id) ?? throw new InvalidOperationException("Không tìm thấy giao dịch.");
            entity.UpdatedAt = DateTime.Now;
        }

        entity.CategoryId = form.CategoryId;
        entity.ContractId = form.ContractId;
        entity.Amount = form.Amount;
        entity.TransactionDate = form.TransactionDate.Date;
        entity.Description = form.Description;
        entity.ReferenceNumber = form.ReferenceNumber;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var x = await db.IncomeExpenses.FindAsync(id) ?? throw new InvalidOperationException("Không tìm thấy giao dịch.");
        db.IncomeExpenses.Remove(x);
        await db.SaveChangesAsync();
    }

    public async Task CreateCategoryAsync(string name, string type)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Tên danh mục không được để trống.");
        if (type is not (CategoryType.Income or CategoryType.Expense)) throw new InvalidOperationException("Loại danh mục không hợp lệ.");
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.IncomeExpenseCategories.AnyAsync(c => c.Name == name.Trim() && c.Type == type))
            throw new InvalidOperationException("Danh mục này đã tồn tại.");
        db.IncomeExpenseCategories.Add(new IncomeExpenseCategory { Name = name.Trim(), Type = type, IsSystem = false, IsActive = true, CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();
    }

    public async Task UpdateCategoryAsync(int id, string name, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Tên danh mục không được để trống.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var c = await db.IncomeExpenseCategories.FindAsync(id) ?? throw new InvalidOperationException("Không tìm thấy danh mục.");
        if (c.IsSystem) throw new InvalidOperationException("Không thể sửa danh mục hệ thống.");
        c.Name = name.Trim();
        c.IsActive = isActive;
        c.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    /// <summary>Xóa danh mục; nếu đã có giao dịch thì chỉ ẩn. Trả về thông báo kết quả.</summary>
    public async Task<string> DeleteCategoryAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var c = await db.IncomeExpenseCategories.FindAsync(id) ?? throw new InvalidOperationException("Không tìm thấy danh mục.");
        if (c.IsSystem) throw new InvalidOperationException("Không thể xóa danh mục hệ thống.");
        if (await db.IncomeExpenses.AnyAsync(i => i.CategoryId == id))
        {
            c.IsActive = false;
            c.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();
            return "Đã ẩn danh mục (có giao dịch liên quan).";
        }
        db.IncomeExpenseCategories.Remove(c);
        await db.SaveChangesAsync();
        return "Đã xóa danh mục.";
    }

    public async Task<MonthlyReport> GetMonthlyReportAsync(int month, int year)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        var tx = await db.IncomeExpenses.AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.Contract).ThenInclude(c => c!.ContractRooms).ThenInclude(cr => cr.Room)
            .Where(i => i.TransactionDate >= start && i.TransactionDate < end)
            .OrderBy(i => i.TransactionDate).ThenBy(i => i.Id)
            .AsSplitQuery()
            .ToListAsync();

        List<(string, decimal)> ByCategory(string type) => tx.Where(t => t.Category.Type == type)
            .GroupBy(t => t.Category.Name).Select(g => (g.Key, g.Sum(t => t.Amount))).OrderByDescending(x => x.Item2).ToList();

        return new MonthlyReport(month, year,
            tx.Where(t => t.Category.Type == CategoryType.Income).Sum(t => t.Amount),
            tx.Where(t => t.Category.Type == CategoryType.Expense).Sum(t => t.Amount),
            tx, ByCategory(CategoryType.Expense), ByCategory(CategoryType.Income));
    }
}
