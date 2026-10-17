using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>
/// Nạp dữ liệu nghiệm thu (chạy: NHNhaTro.exe --seed-demo) và xóa dữ liệu nghiệp vụ (NHNhaTro.exe --clear-data).
/// Dữ liệu đi qua đúng các nghiệp vụ của ứng dụng (nhận phòng, lập phiếu, thu tiền, trả phòng, sửa chữa...),
/// nên số dư, sơ đồ phòng và báo cáo khớp như khi nhập tay.
/// </summary>
public static class DemoDataSeeder
{
    private const string User = "nghiem-thu";
    private const string Mark = "Dữ liệu nghiệm thu";

    public static async Task<string> SeedAsync(IServiceProvider sp)
    {
        var dbFactory = sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var setup = sp.GetRequiredService<SetupService>();
        var rooms = sp.GetRequiredService<RoomService>();
        var leases = sp.GetRequiredService<LeaseService>();
        var billing = sp.GetRequiredService<BillingService>();
        var accounts = sp.GetRequiredService<AccountService>();
        var expenses = sp.GetRequiredService<ExpenseService>();
        var attachments = sp.GetRequiredService<AttachmentService>();

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            if (await db.Leases.AnyAsync() || await db.Transactions.AnyAsync() || await db.Repairs.AnyAsync())
                throw new InvalidOperationException("Database đã có dữ liệu nghiệp vụ. Chạy --clear-data trước nếu muốn nạp lại.");
        }

        async Task<PendingFile> Img(string name, string kind, (byte R, byte G, byte B) color, bool isPublic = false)
        {
            using var ms = new MemoryStream(Png(480, 320, color));
            return await attachments.UploadAsync(ms, name + ".png", kind, isPublic);
        }
        var gray = ((byte)225, (byte)225, (byte)225);

        // ---------- Cấu hình ----------
        var s = await setup.GetSettingsAsync();
        s.AdTitle = "Phòng trọ mới, đủ đồ - An Phong, Hải Phòng";
        s.AdDescription = "64 phòng khép kín tại Thôn Đình Ngọ, phường An Phong. Có tủ, giường, điều hòa, quạt, bàn ghế, đệm.\nĐiện 3.500đ/số, nước 80.000đ/người/tháng. Cọc 1 tháng. Chỗ để xe rộng, camera an ninh.";
        await setup.SaveSettingsAsync(s);
        s = await setup.GetSettingsAsync();

        foreach (var a in await setup.GetAccountsAsync())
        {
            bool personal = a.Kind == AccountKind.Personal;
            a.OpeningBalance = personal ? 50_000_000 : 20_000_000;
            a.OpeningDate = new DateTime(2026, 9, 1);
            a.BankName = personal ? "Vietcombank" : "BIDV";
            a.AccountNumber = personal ? "0011000123456" : "12510000999888";
            a.AccountHolder = personal ? "TA NGOC DUY" : "HO KINH DOANH NHA TRO AN CU";
            await setup.SaveAccountAsync(a);
        }

        if (!(await setup.GetFurnitureItemsAsync(true)).Any(i => i.Name == "Tủ lạnh")) await setup.AddFurnitureItemAsync("Tủ lạnh");
        var items = await setup.GetFurnitureItemsAsync(true);
        int Item(string name) => items.First(i => i.Name == name).Id;
        var blockB = (await setup.GetBlocksAsync()).First(b => b.Code == "B");
        var bFurniture = blockB.DefaultFurniture.ToDictionary(f => f.FurnitureItemId, f => f.Quantity);
        bFurniture[Item("Tủ lạnh")] = 1;
        await setup.SaveBlockAsync(blockB.Id, 2_700_000, bFurniture, applyToEmptyRooms: true, applyRentToEmptyRooms: true);

        var all = await rooms.ListRoomsAsync();
        Room R(string code) => all.First(r => r.Code == code);

        // Số điện hiện tại các phòng sẽ cho thuê; A3.1 đang sửa
        foreach (var (code, meter) in new[] { ("A0.1", 1000), ("A0.2", 2000), ("A0.3", 800), ("A0.5", 300), ("A1.3", 1500), ("A2.4", 640), ("B0.1", 500), ("B1.2", 1200), ("B3.8", 90) })
        {
            var r = R(code);
            var fur = (await rooms.GetDetailsAsync(code))!.Furniture.Where(f => f.Quantity > 0).ToDictionary(f => f.ItemId, f => f.Quantity);
            await rooms.UpdateRoomAsync(r.Id, r.Rent, meter, r.MaxOccupants, null, false, fur);
        }
        {
            var r = R("A3.1");
            var fur = (await rooms.GetDetailsAsync("A3.1"))!.Furniture.Where(f => f.Quantity > 0).ToDictionary(f => f.ItemId, f => f.Quantity);
            await rooms.UpdateRoomAsync(r.Id, r.Rent, 0, r.MaxOccupants, "Đang sơn lại, chống thấm trần", true, fur);
        }
        // A0.5 có thêm 1 quạt so với mặc định (để thấy cảnh báo đồ đạc khác mặc định)
        {
            var r = R("A0.5");
            var fur = (await rooms.GetDetailsAsync("A0.5"))!.Furniture.Where(f => f.Quantity > 0).ToDictionary(f => f.ItemId, f => f.Quantity);
            fur[Item("Quạt")] = 2;
            await rooms.UpdateRoomAsync(r.Id, r.Rent, 300, r.MaxOccupants, null, false, fur);
        }

        // ---------- Nhận phòng ----------
        int seq = 0;
        TenantInput T(string name, string card, string? phone, string gender, int birthYear, string address, string nationality = "Việt Nam", string ethnicity = "Kinh", string? plate = null) =>
            new() { FullName = name, IdentityCard = card, Phone = phone, Gender = gender, BirthDate = new DateTime(birthYear, 1 + (++seq % 12), 1 + seq % 27), PermanentAddress = address, Nationality = nationality, Ethnicity = ethnicity, VehiclePlate = plate };

        async Task<int> CheckIn(string code, DateTime date, List<TenantInput> people, bool payDeposit = true, bool payFirst = true, decimal? depositPaid = null)
        {
            var room = (await rooms.ListRoomsAsync()).First(r => r.Code == code);
            foreach (var p in people)
            {
                p.Photos.Add((await Img("cccd-" + p.IdentityCard + "-truoc", "TenantPhotos", (40, 90, 190))).Path);
                p.Photos.Add((await Img("cccd-" + p.IdentityCard + "-sau", "TenantPhotos", (60, 150, 90))).Path);
            }
            var first = LeaseService.CalculateFirstPeriod(room.Rent, date, s.BillingDay, s.WaterPricePerPerson, people.Count);
            var form = new CheckInForm
            {
                RoomId = room.Id, MoveInDate = date, MonthlyRent = room.Rent, Deposit = room.Rent, StartMeterReading = room.MeterReading, Note = Mark,
                Tenants = people, PaymentDate = date,
                DepositPaid = depositPaid ?? (payDeposit ? room.Rent : 0), FirstPaymentPaid = payFirst ? first.Total : 0
            };
            if (form.DepositPaid > 0) form.DepositReceipts.Add(await Img("ck-coc-" + code, "Receipts", gray));
            if (form.FirstPaymentPaid > 0) form.PaymentReceipts.Add(await Img("ck-ky-dau-" + code, "Receipts", gray));
            var id = await leases.CheckInAsync(form, User);
            await leases.SetContractScanAsync(id, await Img("hop-dong-" + code, "Contracts", (250, 250, 250)), User);
            return id;
        }

        var l03 = await CheckIn("A0.3", new DateTime(2026, 8, 15), [T("Đỗ Văn Hùng", "031089000301", "0912000301", "Nam", 1989, "Kiến An, Hải Phòng", plate: "15B1-234.56")]);
        var l01 = await CheckIn("A0.1", new DateTime(2026, 9, 12), [T("Trần Văn An", "031095000101", "0901000101", "Nam", 1995, "Số 5 Lạch Tray, Ngô Quyền, Hải Phòng", plate: "15A-123.45"),
            T("Lê Thị Bình", "031097000102", "0901000102", "Nữ", 1997, "An Hòa, An Dương, Hải Phòng")]);
        var l02 = await CheckIn("A0.2", new DateTime(2026, 9, 1), [T("Vũ Thị Lan", "031099000201", "0902000201", "Nữ", 1999, "Thủy Nguyên, Hải Phòng")]);
        var l05 = await CheckIn("A0.5", new DateTime(2026, 9, 20), [T("Bùi Văn Nam", "034096000501", "0905000501", "Nam", 1996, "Vũ Thư, Thái Bình"),
            T("Bùi Văn Bắc", "034098000502", "0905000502", "Nam", 1998, "Vũ Thư, Thái Bình"), T("Ngô Thị Thu", "034000000503", "0905000503", "Nữ", 2000, "Kiến Xương, Thái Bình")]);
        var l13 = await CheckIn("A1.3", new DateTime(2026, 10, 3), [T("Phạm Quốc Cường", "031094001301", "0913001301", "Nam", 1994, "Lê Chân, Hải Phòng")]);
        var l24 = await CheckIn("A2.4", new DateTime(2026, 10, 15), [T("Zhang Wei", "E12345678", "0977002401", "Nam", 1990, "Quảng Tây, Trung Quốc", "Trung Quốc", "Hán"),
            T("Li Na", "E87654321", "0977002402", "Nữ", 1992, "Quảng Tây, Trung Quốc", "Trung Quốc", "Hán")], payFirst: false, depositPaid: 1_000_000);
        var lB01 = await CheckIn("B0.1", new DateTime(2026, 9, 5), [T("Hoàng Minh Đức", "031088100101", "0912100101", "Nam", 1988, "Hồng Bàng, Hải Phòng"),
            T("Nguyễn Thị Hoa", "031090100102", null, "Nữ", 1990, "Hồng Bàng, Hải Phòng"), T("Hoàng Minh Khôi", "031012100103", null, "Nam", 2012, "Hồng Bàng, Hải Phòng")]);
        var lB12 = await CheckIn("B1.2", new DateTime(2026, 9, 25), [T("Đặng Thu Trang", "031001101201", "0931101201", "Nữ", 2001, "An Lão, Hải Phòng"),
            T("Mai Phương", "031002101202", "0931101202", "Nữ", 2002, "Tiên Lãng, Hải Phòng")]);
        var lB38 = await CheckIn("B3.8", new DateTime(2026, 10, 10), [T("Lý Văn Tâm", "036093103801", "0938103801", "Nam", 1993, "Nam Trực, Nam Định")]);

        // Nguyễn Thị Hoa chuyển đi 01/10
        {
            var l = (await leases.GetLeaseAsync(lB01))!;
            var keep = l.Tenants.Where(t => t.Tenant.FullName != "Nguyễn Thị Hoa").OrderByDescending(t => t.IsRepresentative).Select(t => TenantInput.From(t.Tenant)).ToList();
            await leases.SaveTenantsAsync(lB01, keep, new DateTime(2026, 10, 1));
        }

        // ---------- Phiếu tháng và thu tiền ----------
        async Task Bill(int month, int year, string code, int meterEnd, decimal other, string? note, decimal? pay, DateTime payDate)
        {
            var sheet = await billing.GetSheetAsync(month, year);
            var row = sheet.Rows.First(r => r.RoomCode == code);
            row.MeterEnd = meterEnd;
            row.OtherAmount = other;
            row.Note = note;
            var billId = await billing.SaveBillAsync(month, year, row, User);
            if (pay is decimal amount && amount > 0)
            {
                var bill = (await billing.GetBillAsync(billId))!;
                var form = new PaymentForm { BillId = billId, Amount = amount == decimal.MaxValue ? bill.TotalAmount : amount, Date = payDate, AccountId = s.BusinessAccountId, Note = "Chuyển khoản" };
                form.Receipts.Add(await Img("ck-thang-" + code + "-" + month, "Receipts", gray));
                await billing.RecordPaymentAsync(form, User);
            }
        }
        const decimal Full = decimal.MaxValue;
        await Bill(9, 2026, "A0.3", 905, 0, null, Full, new DateTime(2026, 9, 11));
        await Bill(10, 2026, "A0.1", 1135, 50_000, "Phí rác", Full, new DateTime(2026, 10, 11));
        await Bill(10, 2026, "A0.2", 2180, 0, null, 1_500_000, new DateTime(2026, 10, 12));
        await Bill(10, 2026, "B0.1", 620, 50_000, "Phí rác", Full, new DateTime(2026, 10, 10));
        await Bill(10, 2026, "B1.2", 1260, 0, null, null, DateTime.Today);
        // A0.5 chưa lập phiếu tháng 10 (hiện màu xanh trên sơ đồ)

        // ---------- Trả phòng A0.3 ----------
        {
            var details = (await rooms.GetDetailsAsync("A0.3"))!;
            var form = new CheckoutForm
            {
                LeaseId = l03, EndDate = new DateTime(2026, 10, 12), EndMeterReading = 1010,
                Penalties = [new() { Description = "Tường bẩn", Amount = 300_000 }, new() { Description = "Vỡ kính cửa sổ", Amount = 150_000 }],
                FurnitureActual = details.Furniture.ToDictionary(f => f.ItemId, f => f.Quantity),
                Note = "Tường bẩn gần giường, kính cửa sổ nứt. Các đồ đạc khác đủ."
            };
            form.Photos.Add(await Img("hien-trang-A0.3-1", "Checkout", (180, 120, 60)));
            form.Photos.Add(await Img("hien-trang-A0.3-2", "Checkout", (200, 40, 40)));
            var preview = await leases.PreviewCheckoutAsync(form);
            form.DepositRefund = Math.Max(0, preview.Balance);
            form.Collected = Math.Max(0, -preview.Balance);
            if (form.DepositRefund > 0) form.RefundReceipts.Add(await Img("ck-tra-coc-A0.3", "Receipts", gray));
            await leases.CheckoutAsync(form, User);
        }

        // ---------- Sửa chữa ----------
        async Task Repair(DateTime date, string? code, string? location, string? item, string desc, string? by, decimal amount)
        {
            var f = new RepairForm
            {
                Date = date, RoomId = code == null ? null : R(code).Id, Location = location, FurnitureItemId = item == null ? null : Item(item),
                Description = desc, PerformedBy = by, Amount = amount, AccountId = s.PersonalAccountId
            };
            f.BeforePhotos.Add(await Img("truoc-sua", "Repairs", (200, 40, 40)));
            f.AfterPhotos.Add(await Img("sau-sua", "Repairs", (40, 180, 80)));
            if (amount > 0) f.Receipts.Add(await Img("ck-sua", "Receipts", gray));
            await expenses.CreateRepairAsync(f, User);
        }
        await Repair(new DateTime(2026, 10, 2), "B1.2", null, "Điều hòa", "Nạp gas điều hòa", "Thợ điện lạnh Minh", 450_000);
        await Repair(new DateTime(2026, 10, 14), "A0.3", null, null, "Sơn lại tường, thay kính cửa sổ sau khi khách trả phòng", "Anh Toàn thợ sơn", 1_200_000);
        await Repair(new DateTime(2026, 10, 15), null, "Cầu thang dãy A", null, "Thay 4 bóng đèn cầu thang", "Tự làm", 240_000);
        await Repair(new DateTime(2026, 10, 16), "B1.2", null, "Điều hòa", "Điều hòa chảy nước, thay ống thoát", "Thợ điện lạnh Minh", 350_000);
        await Repair(new DateTime(2026, 10, 16), null, "Bể nước mái dãy B", null, "Súc rửa bể nước (không mất tiền)", "Tự làm", 0);

        // ---------- Mua sắm, quảng cáo ----------
        async Task Buy(DateTime date, string desc, string? supplier, string? code, decimal amount, bool ad)
        {
            var f = new PurchaseForm { Date = date, Description = desc, Supplier = supplier, RoomId = code == null ? null : R(code).Id, Amount = amount, AccountId = s.PersonalAccountId, IsAdvertising = ad };
            f.Receipts.Add(await Img("hoa-don", "Receipts", (250, 250, 200)));
            await expenses.CreatePurchaseAsync(f, User);
        }
        await Buy(new DateTime(2026, 9, 18), "Mua 2 quạt treo tường", "Điện máy Xanh An Dương", "A0.5", 760_000, false);
        await Buy(new DateTime(2026, 10, 5), "Bóng đèn LED 10 cái để thay thế", "Cửa hàng điện Hưng Thịnh", null, 350_000, false);
        await Buy(new DateTime(2026, 9, 3), "In 2 băng rôn cho thuê phòng", "In ấn Phương Nam", null, 300_000, true);
        await Buy(new DateTime(2026, 10, 1), "Chạy quảng cáo Facebook 2 tuần", "Facebook", null, 500_000, true);

        // ---------- Thu chi khác ----------
        async Task Manual(int accountId, DateTime date, bool income, decimal amount, string category, string desc, string? code = null)
        {
            var f = new ManualTransactionForm { AccountId = accountId, Date = date, IsIncome = income, Amount = amount, Category = category, Description = desc, RoomId = code == null ? null : R(code).Id };
            await accounts.AddManualAsync(f, [await Img("chung-tu", "Receipts", gray)], User);
        }
        await Manual(s.BusinessAccountId, new DateTime(2026, 10, 10), true, 300_000, TxCategory.OtherIncome, "Phí gửi xe tháng 10");
        await Manual(s.BusinessAccountId, new DateTime(2026, 10, 8), false, 4_200_000, TxCategory.Electricity, "Nộp tiền điện EVN tháng 9");
        await Manual(s.BusinessAccountId, new DateTime(2026, 10, 9), false, 900_000, TxCategory.Water, "Nộp tiền nước sạch tháng 9");
        await accounts.TransferAsync(s.BusinessAccountId, s.PersonalAccountId, 5_000_000, new DateTime(2026, 10, 15), "Rút tiền phòng về TK cá nhân", [], User);

        // ---------- Ảnh quảng cáo ----------
        foreach (var (name, color) in new[] { ("phong-mau-1", ((byte)90, (byte)170, (byte)230)), ("phong-mau-2", ((byte)240, (byte)180, (byte)80)), ("mat-tien", ((byte)120, (byte)200, (byte)140)) })
        {
            var f = await Img(name, "Gallery", color, isPublic: true);
            await attachments.AddAsync(AttachmentOwner.Site, 1, [f with { Kind = AttachmentKind.Gallery }], User);
        }

        return "Đã nạp dữ liệu nghiệm thu: 9 hợp đồng (1 đã trả phòng), phiếu tháng 9-10, 5 phiếu sửa chữa, 4 khoản mua sắm/quảng cáo, thu chi khác, ảnh quảng cáo.";
    }

    /// <summary>Xóa toàn bộ dữ liệu nghiệp vụ (khách, hợp đồng, phiếu, thu chi, sửa chữa, mua sắm, file đính kèm). Giữ cấu hình, phòng, tài khoản tiền, người dùng.</summary>
    public static async Task<string> ClearAsync(IServiceProvider sp)
    {
        var dbFactory = sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var files = sp.GetRequiredService<FileStorage>();
        await using var db = await dbFactory.CreateDbContextAsync();
        var paths = await db.Attachments.Where(a => a.EntityType != AttachmentOwner.Site).Select(a => a.Path).ToListAsync();
        paths.AddRange((await db.Tenants.Select(t => t.Photos).ToListAsync()).Where(p => p != null).SelectMany(p => p!.Split(';', StringSplitOptions.RemoveEmptyEntries)));

        await using var tx = await db.Database.BeginTransactionAsync();
        int n = 0;
        n += await db.Attachments.Where(a => a.EntityType != AttachmentOwner.Site).ExecuteDeleteAsync();
        n += await db.Transactions.ExecuteDeleteAsync();
        n += await db.Repairs.ExecuteDeleteAsync();
        n += await db.Purchases.ExecuteDeleteAsync();
        n += await db.LeasePenalties.ExecuteDeleteAsync();
        n += await db.Bills.ExecuteDeleteAsync();
        n += await db.LeaseTenants.ExecuteDeleteAsync();
        n += await db.Leases.ExecuteDeleteAsync();
        n += await db.Tenants.ExecuteDeleteAsync();
        await db.Rooms.ExecuteUpdateAsync(r => r.SetProperty(x => x.Status, RoomStatus.Empty).SetProperty(x => x.MeterReading, 0).SetProperty(x => x.Note, (string?)null));
        await tx.CommitAsync();
        foreach (var p in paths.Distinct()) files.Delete(p);
        return $"Đã xóa {n} dòng dữ liệu nghiệp vụ và {paths.Distinct().Count()} file. Phòng về trạng thái trống, số điện 0. Cấu hình, tài khoản tiền, ảnh quảng cáo và người dùng được giữ.";
    }

    /// <summary>Tạo ảnh PNG một màu (ảnh minh họa cho dữ liệu thử).</summary>
    private static byte[] Png(int w, int h, (byte R, byte G, byte B) c)
    {
        var raw = new byte[(w * 3 + 1) * h];
        for (int y = 0; y < h; y++)
        {
            int row = y * (w * 3 + 1);
            for (int x = 0; x < w; x++)
            {
                bool border = x < 6 || y < 6 || x >= w - 6 || y >= h - 6;
                raw[row + 1 + x * 3] = border ? (byte)60 : c.R;
                raw[row + 2 + x * 3] = border ? (byte)60 : c.G;
                raw[row + 3 + x * 3] = border ? (byte)60 : c.B;
            }
        }
        using var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, true)) zs.Write(raw);
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        void Chunk(string type, byte[] data)
        {
            var t = System.Text.Encoding.ASCII.GetBytes(type);
            ms.Write(BE(data.Length));
            ms.Write(t);
            ms.Write(data);
            ms.Write(BE((int)Crc(t.Concat(data).ToArray())));
        }
        var ihdr = new byte[13];
        BE(w).CopyTo(ihdr, 0); BE(h).CopyTo(ihdr, 4);
        ihdr[8] = 8; ihdr[9] = 2;
        Chunk("IHDR", ihdr);
        Chunk("IDAT", z.ToArray());
        Chunk("IEND", []);
        return ms.ToArray();
    }

    private static byte[] BE(int v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];

    private static uint Crc(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
