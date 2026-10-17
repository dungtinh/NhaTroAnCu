using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>Cấu hình chung, dãy phòng, danh mục đồ đạc, tài khoản tiền và dữ liệu khởi tạo.</summary>
public class SetupService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public static readonly string[] DefaultFurniture = ["Tủ", "Giường", "Điều hòa", "Quạt", "Bàn ghế", "Đệm"];

    /// <summary>Tạo dữ liệu ban đầu nếu chưa có: cấu hình, 2 tài khoản, đồ đạc, dãy A/B và 64 phòng.</summary>
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (!await db.Accounts.AnyAsync())
        {
            db.Accounts.AddRange(
                new MoneyAccount { Name = "Tài khoản cá nhân", Kind = AccountKind.Personal, OpeningDate = DateTime.Today },
                new MoneyAccount { Name = "Tài khoản hộ kinh doanh", Kind = AccountKind.Business, OpeningDate = DateTime.Today });
            await db.SaveChangesAsync();
        }

        if (!await db.Settings.AnyAsync())
        {
            var accounts = await db.Accounts.ToListAsync();
            db.Settings.Add(new AppSetting
            {
                Id = 1,
                PersonalAccountId = accounts.First(a => a.Kind == AccountKind.Personal).Id,
                BusinessAccountId = accounts.First(a => a.Kind == AccountKind.Business).Id,
                Address = "Thôn Đình Ngọ, phường An Phong, thành phố Hải Phòng",
                RegisterArea = "Tổ dân phố Đình Ngọ, phường An Phong",
                Owner = "Tạ Ngọc Duy",
                Phone = "0975092833",
                AdTitle = "Nhà trọ An Cư - phòng sạch, đầy đủ đồ, giá hợp lý",
                AdDescription = "64 phòng tại Thôn Đình Ngọ, An Phong, Hải Phòng. Phòng có tủ, giường, điều hòa, quạt, bàn ghế, đệm. Điện 3.500đ/số, nước 80.000đ/người/tháng."
            });
            await db.SaveChangesAsync();
        }

        if (!await db.FurnitureItems.AnyAsync())
        {
            for (int i = 0; i < DefaultFurniture.Length; i++)
                db.FurnitureItems.Add(new FurnitureItem { Name = DefaultFurniture[i], SortOrder = i + 1 });
            await db.SaveChangesAsync();
        }

        if (!await db.Blocks.AnyAsync())
        {
            var items = await db.FurnitureItems.ToListAsync();
            foreach (var code in new[] { "A", "B" })
            {
                var block = new Block { Code = code, Name = "Dãy " + code, Floors = 4, RoomsPerFloor = 8, DefaultRent = 2_500_000 };
                foreach (var item in items) block.DefaultFurniture.Add(new BlockFurniture { FurnitureItemId = item.Id, Quantity = 1 });
                db.Blocks.Add(block);
            }
            await db.SaveChangesAsync();
        }

        if (!await db.Rooms.AnyAsync())
        {
            var blocks = await db.Blocks.Include(b => b.DefaultFurniture).ToListAsync();
            foreach (var block in blocks)
                for (int floor = 0; floor < block.Floors; floor++)
                    for (int n = 1; n <= block.RoomsPerFloor; n++)
                    {
                        var room = new Room { BlockId = block.Id, Floor = floor, Number = n, Code = $"{block.Code}{floor}.{n}", Rent = block.DefaultRent };
                        foreach (var d in block.DefaultFurniture) room.Furniture.Add(new RoomFurniture { FurnitureItemId = d.FurnitureItemId, Quantity = d.Quantity });
                        db.Rooms.Add(room);
                    }
            await db.SaveChangesAsync();
        }
    }

    public async Task<AppSetting> GetSettingsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Settings.AsNoTracking().FirstAsync();
    }

    public async Task SaveSettingsAsync(AppSetting input)
    {
        if (input.ElectricityPrice < 0 || input.WaterPricePerPerson < 0) throw new InvalidOperationException("Giá điện, nước không được âm.");
        if (input.BillingDay is < 1 or > 28) throw new InvalidOperationException("Ngày thu tiền phải từ 1 đến 28.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Settings.FirstAsync();
        db.Entry(s).CurrentValues.SetValues(input);
        s.Id = 1;
        await db.SaveChangesAsync();
    }

    public async Task<List<Block>> GetBlocksAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Blocks.AsNoTracking().Include(b => b.DefaultFurniture).ThenInclude(f => f.FurnitureItem).OrderBy(b => b.Code).ToListAsync();
    }

    public async Task<List<FurnitureItem>> GetFurnitureItemsAsync(bool includeInactive = false)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FurnitureItems.AsNoTracking().Where(i => includeInactive || i.IsActive).OrderBy(i => i.SortOrder).ThenBy(i => i.Name).ToListAsync();
    }

    public async Task AddFurnitureItemAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Nhập tên đồ đạc.");
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.FurnitureItems.AnyAsync(i => i.Name == name.Trim())) throw new InvalidOperationException("Đồ đạc này đã có.");
        var max = await db.FurnitureItems.MaxAsync(i => (int?)i.SortOrder) ?? 0;
        db.FurnitureItems.Add(new FurnitureItem { Name = name.Trim(), SortOrder = max + 1 });
        await db.SaveChangesAsync();
    }

    public async Task SetFurnitureItemActiveAsync(int id, bool active)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.FurnitureItems.FindAsync(id) ?? throw new InvalidOperationException("Không tìm thấy.");
        item.IsActive = active;
        await db.SaveChangesAsync();
    }

    /// <summary>Lưu giá mặc định và đồ đạc mặc định của dãy; tùy chọn áp dụng cho mọi phòng đang trống của dãy.</summary>
    public async Task<int> SaveBlockAsync(int blockId, decimal defaultRent, Dictionary<int, int> furniture, bool applyToEmptyRooms, bool applyRentToEmptyRooms)
    {
        if (defaultRent < 0) throw new InvalidOperationException("Giá thuê không được âm.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var block = await db.Blocks.Include(b => b.DefaultFurniture).FirstAsync(b => b.Id == blockId);
        block.DefaultRent = defaultRent;
        SyncFurniture(block.DefaultFurniture, furniture, (itemId, qty) => new BlockFurniture { BlockId = blockId, FurnitureItemId = itemId, Quantity = qty }, x => x.FurnitureItemId, (x, q) => x.Quantity = q, db);

        int changed = 0;
        if (applyToEmptyRooms || applyRentToEmptyRooms)
        {
            var rooms = await db.Rooms.Include(r => r.Furniture).Where(r => r.BlockId == blockId && r.Status == RoomStatus.Empty).ToListAsync();
            foreach (var room in rooms)
            {
                if (applyRentToEmptyRooms) room.Rent = defaultRent;
                if (applyToEmptyRooms)
                    SyncFurniture(room.Furniture, furniture, (itemId, qty) => new RoomFurniture { RoomId = room.Id, FurnitureItemId = itemId, Quantity = qty }, x => x.FurnitureItemId, (x, q) => x.Quantity = q, db);
                changed++;
            }
        }
        await db.SaveChangesAsync();
        return changed;
    }

    /// <summary>Đồng bộ danh sách (món, số lượng): số lượng 0 thì bỏ món đó.</summary>
    public static void SyncFurniture<T>(List<T> current, Dictionary<int, int> target, Func<int, int, T> create, Func<T, int> itemId, Action<T, int> setQty, ApplicationDbContext db) where T : class
    {
        foreach (var x in current.ToList())
        {
            var id = itemId(x);
            if (!target.TryGetValue(id, out var qty) || qty <= 0)
            {
                current.Remove(x);
                db.Remove(x);
            }
            else setQty(x, qty);
        }
        foreach (var (id, qty) in target.Where(t => t.Value > 0))
            if (!current.Any(x => itemId(x) == id)) current.Add(create(id, qty));
    }

    public async Task<List<MoneyAccount>> GetAccountsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Accounts.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
    }

    public async Task SaveAccountAsync(MoneyAccount input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw new InvalidOperationException("Nhập tên tài khoản.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var a = await db.Accounts.FindAsync(input.Id) ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        a.Name = input.Name.Trim();
        a.OpeningBalance = input.OpeningBalance;
        a.OpeningDate = input.OpeningDate.Date;
        a.BankName = input.BankName;
        a.AccountNumber = input.AccountNumber;
        a.AccountHolder = input.AccountHolder;
        await db.SaveChangesAsync();
    }

    public async Task<string?> GetFptApiKeyAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FptReaderApis.Where(x => x.Status).Select(x => x.ApiKey).FirstOrDefaultAsync();
    }

    public async Task SaveFptApiKeyAsync(string? apiKey, string? email)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.FptReaderApis.RemoveRange(await db.FptReaderApis.ToListAsync());
        if (!string.IsNullOrWhiteSpace(apiKey))
            db.FptReaderApis.Add(new FptReaderApi { ApiKey = apiKey.Trim(), Email = string.IsNullOrWhiteSpace(email) ? "-" : email.Trim(), Status = true });
        await db.SaveChangesAsync();
    }
}
