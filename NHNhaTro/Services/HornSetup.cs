using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>
/// Nhập hợp đồng Công ty TNHH Horn (Việt Nam) vào dữ liệu thật (chạy một lần: NHNhaTro.exe --setup-horn [file scan...]).
/// Theo hợp đồng 01/HĐTN-Horn ký 10/09/2025 (20 phòng, đã hết hạn) và phụ lục 03.2026/Horn-TND (B3.1–B3.5, 10/09/2026 → 09/09/2027):
/// tạo hợp đồng công ty, 2 thời hạn, ghi quý 1 của phụ lục đã thu ngoài phần mềm (nộp 20/09–05/10/2026), gắn 5 phòng B3.1–B3.5,
/// ghi phần cọc còn thiếu (6.600.000/phòng, đã ghi 1.000.000 lúc nhập khách cũ) vào TK cá nhân (chủ nhà xác nhận 22/10/2026),
/// đính kèm bản scan hợp đồng, phụ lục. Đã có hợp đồng Horn thì không làm gì.
/// </summary>
public static class HornSetup
{
    private const string User = "nhap-hd-horn";
    private static readonly string[] PhuLucRooms = ["B3.1", "B3.2", "B3.3", "B3.4", "B3.5"];

    public static async Task<string> RunAsync(IServiceProvider sp, IEnumerable<string> scanFiles)
    {
        var dbFactory = sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var companies = sp.GetRequiredService<CompanyService>();
        var files = sp.GetRequiredService<FileStorage>();
        var log = new List<string>();

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            if (await db.CompanyContracts.AnyAsync(c => c.ShortName == "Horn"))
                throw new InvalidOperationException("Đã có hợp đồng công ty Horn, không nhập lại.");
        }
        var scans = scanFiles.ToList();
        foreach (var f in scans)
            if (!File.Exists(f)) throw new InvalidOperationException($"Không thấy file scan {f}");

        List<int> leaseIds;
        int personalAccount;
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            personalAccount = (await db.Settings.AsNoTracking().FirstAsync()).PersonalAccountId;
            var leases = await db.Leases.AsNoTracking().Where(l => l.Status == LeaseStatus.Active && PhuLucRooms.Contains(l.Room.Code))
                .Select(l => new { l.Id, l.Room.Code, l.CompanyContractId }).ToListAsync();
            var missing = PhuLucRooms.Except(leases.Select(l => l.Code)).ToList();
            if (missing.Count > 0) throw new InvalidOperationException($"Phòng {string.Join(", ", missing)} không có khách đang thuê: kiểm tra lại trước khi nhập.");
            if (leases.Any(l => l.CompanyContractId != null)) throw new InvalidOperationException("Có phòng B3.x đã gắn hợp đồng công ty khác.");
            leaseIds = leases.Select(l => l.Id).ToList();
        }

        // 1) Hợp đồng công ty
        var contractId = await companies.SaveContractAsync(new CompanyContract
        {
            CompanyName = "Công ty TNHH Horn (Việt Nam)", ShortName = "Horn", TaxCode = "0201822112",
            Address = "Nhà xưởng tiêu chuẩn số 1, 2, 6 và 8, lô đất CN1 khu công nghiệp An Dương, phường An Phong, thành phố Hải Phòng",
            Representative = "Yu Penghui", RepresentativeTitle = "Tổng giám đốc", Phone = "0225.2299.518",
            BankAccount = "111002659668 Vietinbank - CN Kiến An, Hải Phòng",
            ContractNo = "01/HĐTN-Horn", SignedDate = new DateTime(2025, 9, 10),
            RentMonthsPerPeriod = 3, WaterPerRoom = 100_000, MaxOccupantsPerRoom = 2, IsActive = true,
            Note = "Tiền phòng trả trước theo quý (chuyển khoản), điện trả hằng tháng, nước 100.000đ/phòng/tháng (chủ nhà 22/10/2026). "
                 + "Tối đa 2 người/phòng. Quá 7 ngày kể từ ngày 25 tháng đầu quý chưa nộp thì bên cho thuê được đơn phương chấm dứt. "
                 + "Giảm phòng/chấm dứt báo trước 1 tháng; năm đầu bên nào chấm dứt phạt 1 tháng (bên thuê: 1 tháng cọc của phòng bỏ)."
        }, User);
        log.Add("Đã tạo hợp đồng công ty Horn (Công ty TNHH Horn (Việt Nam), HĐ 01/HĐTN-Horn ký 10/09/2025).");

        // 2) Hợp đồng gốc 20 phòng (đã hết hạn): các kỳ tự ghi đã thu ngoài phần mềm vì đã qua hạn nộp
        await companies.AddTermAsync(contractId, new TermForm
        {
            DocumentNo = "01/HĐTN-Horn", SignedDate = new DateTime(2025, 9, 10), FromDate = new DateTime(2025, 9, 10), ToDate = new DateTime(2026, 9, 9),
            RentPerRoom = 2_200_000, DepositPerRoom = 6_600_000,
            RoomCodes = "B1.1, B1.2, B1.7, B1.8, B2.1, B2.2, B2.3, B2.4, B2.5, B2.6, B2.7, B2.8, B3.1, B3.2, B3.3, B3.4, B3.5, B3.6, B3.7, B3.8",
            Note = "Hợp đồng gốc 20 phòng, 44.000.000đ/tháng, cọc 132.000.000đ. Đã hết hạn, thu ngoài phần mềm.", ApplyToLeases = false
        });

        // 3) Phụ lục 03.2026: 5 phòng B3.1–B3.5
        await companies.AddTermAsync(contractId, new TermForm
        {
            DocumentNo = "PL 03.2026/Horn-TND", FromDate = new DateTime(2026, 9, 10), ToDate = new DateTime(2027, 9, 9),
            RentPerRoom = 2_200_000, DepositPerRoom = 6_600_000, RoomCodes = string.Join(", ", PhuLucRooms),
            Note = "Phụ lục giảm còn 5 phòng, 11.000.000đ/tháng, cọc 33.000.000đ.", ApplyToLeases = false
        });

        // 4) Cửa sổ nộp theo văn bản: quý 1 của hợp đồng gốc nộp 20–30/09/2025, quý 1 của phụ lục nộp 20/09–05/10/2026 (đã thu ngoài phần mềm)
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            var periods = await db.ContractPeriods.Include(p => p.Term).Where(p => p.Term.CompanyContractId == contractId && p.Number == 1).ToListAsync();
            foreach (var p in periods)
            {
                if (p.Term.FromDate.Year == 2025) { p.PayFrom = new DateTime(2025, 9, 20); p.PayTo = new DateTime(2025, 9, 30); }
                else { p.PayFrom = new DateTime(2026, 9, 20); p.PayTo = new DateTime(2026, 10, 5); }
                p.SettledOutside = true;
                p.Note = "Đã thu trước khi dùng phần mềm";
            }
            await db.SaveChangesAsync();
            var settled = await db.ContractPeriods.CountAsync(p => p.Term.CompanyContractId == contractId && p.SettledOutside);
            log.Add($"Đã thêm 2 thời hạn: 01/HĐTN-Horn (10/09/2025 → 09/09/2026) và PL 03.2026/Horn-TND (10/09/2026 → 09/09/2027); {settled} kỳ ghi đã thu ngoài phần mềm.");
        }

        // 5) Gắn 5 phòng: giá 2.200.000, cọc 6.600.000, tối đa 2 người theo phụ lục
        await companies.AttachLeasesAsync(contractId, leaseIds);
        log.Add($"Đã gắn {leaseIds.Count} phòng {string.Join(", ", PhuLucRooms)}: giá 2.200.000đ, cọc 6.600.000đ/phòng, tối đa 2 người.");

        // 6) Cọc còn thiếu vào TK cá nhân, cùng ngày với cọc đã ghi lúc nhập khách cũ
        DateTime depositDate;
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            depositDate = await db.Transactions.Where(t => t.LeaseId != null && leaseIds.Contains(t.LeaseId.Value) && t.Category == TxCategory.Deposit)
                .Select(t => (DateTime?)t.Date).MinAsync() ?? DateTime.Today;
        }
        try
        {
            var added = await companies.CollectDepositsAsync(contractId, personalAccount, depositDate, [], User);
            log.Add($"Đã ghi thêm cọc {Fmt.Vnd(added)} vào TK cá nhân (ngày {depositDate:dd/MM/yyyy}); tổng cọc Horn 33.000.000đ.");
        }
        catch (InvalidOperationException ex) { log.Add("Cọc: " + ex.Message); }

        // 7) Bản scan hợp đồng, phụ lục
        int n = 0;
        foreach (var path in scans)
        {
            await using var fs = File.OpenRead(path);
            var name = Path.GetFileName(path);
            var web = await files.SaveAsync(fs, name, "Contracts");
            await using var db = await dbFactory.CreateDbContextAsync();
            AttachmentService.Attach(db, AttachmentOwner.Company, contractId, [new PendingFile(web, name, AttachmentKind.ContractScan)], User);
            await db.SaveChangesAsync();
            n++;
        }
        if (n > 0) log.Add($"Đã đính kèm {n} bản scan: {string.Join(", ", scans.Select(Path.GetFileName))}.");

        // 8) Kết quả: kỳ tiền phòng sẽ lập phiếu
        var details = await companies.GetAsync(contractId, DateTime.Today.Month, DateTime.Today.Year);
        foreach (var p in details!.Periods.Where(p => !p.Period.SettledOutside))
            log.Add($"Kỳ {p.Period.Number} {p.Term.DocumentNo}: {p.Period.From:dd/MM/yyyy} → {p.Period.To.AddDays(-1):dd/MM/yyyy}, nộp {p.Period.PayFrom:dd/MM} – {p.Period.PayTo:dd/MM/yyyy}, dự tính {Fmt.Vnd(p.Total)}{(p.Generated ? " (đã lập phiếu)" : "")}.");
        foreach (var w in details.Warnings) log.Add("Cảnh báo: " + w);
        return string.Join(Environment.NewLine, log);
    }
}
