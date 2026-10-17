using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>Xuất Excel bằng ClosedXML và in hợp đồng Word bằng OpenXML (thay Aspose/FlexCel trả phí của bản cũ).</summary>
public class OfficeDocuments(IWebHostEnvironment env, Microsoft.Extensions.Options.IOptions<LandlordOptions> landlord)
{
    public const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public byte[] ContractList(List<Contract> contracts)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Danh sách hợp đồng");
        string[] headers = ["Mã HĐ", "Loại HĐ", "Phòng", "Khách thuê/Công ty", "Ngày bắt đầu", "Ngày kết thúc", "Giá thuê", "Điện", "Nước", "Trạng thái", "Ghi chú"];
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        var header = ws.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;
        header.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        int row = 2;
        var soon = DateTime.Today.AddDays(30);
        foreach (var c in contracts)
        {
            ws.Cell(row, 1).Value = c.Id;
            ws.Cell(row, 2).Value = ContractType.Display(c.ContractType);
            ws.Cell(row, 3).Value = string.Join(", ", c.ContractRooms.Select(cr => cr.Room.Name));
            ws.Cell(row, 4).Value = c.IsCompany ? c.Company?.CompanyName ?? "" : string.Join(", ", c.ContractTenants.Select(ct => ct.Tenant.FullName));
            ws.Cell(row, 5).Value = c.StartDate.ToString("dd/MM/yyyy");
            ws.Cell(row, 6).Value = c.EndDate.ToString("dd/MM/yyyy");
            ws.Cell(row, 7).Value = c.PriceAgreed;
            ws.Cell(row, 8).Value = c.ElectricityPrice;
            ws.Cell(row, 9).Value = c.WaterPrice;
            for (int col = 7; col <= 9; col++) ws.Cell(row, col).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 10).Value = ContractStatus.Display(c.Status);
            ws.Cell(row, 11).Value = c.Note;

            var range = ws.Range(row, 1, row, headers.Length);
            if (c.Status == ContractStatus.Expired) range.Style.Fill.BackgroundColor = XLColor.LightCoral;
            else if (c.Status == ContractStatus.Ended) range.Style.Fill.BackgroundColor = XLColor.LightGray;
            else if (c.EndDate <= soon) range.Style.Fill.BackgroundColor = XLColor.LightYellow;
            row++;
        }
        ws.Columns().AdjustToContents();

        var st = wb.Worksheets.Add("Thống kê");
        st.Cell(1, 1).Value = "THỐNG KÊ HỢP ĐỒNG";
        st.Cell(1, 1).Style.Font.Bold = true;
        st.Cell(1, 1).Style.Font.FontSize = 16;
        int r = 3;
        void Stat(string label, XLCellValue value, string? format = null)
        {
            st.Cell(r, 1).Value = label;
            st.Cell(r, 2).Value = value;
            if (format != null) st.Cell(r, 2).Style.NumberFormat.Format = format;
            r++;
        }
        void Title(string text) { r++; st.Cell(r, 1).Value = text; st.Cell(r, 1).Style.Font.Bold = true; r++; }

        Stat("Tổng số hợp đồng:", contracts.Count);
        Stat("Đang hiệu lực:", contracts.Count(c => c.Status == ContractStatus.Active));
        Stat("Đã hết hạn:", contracts.Count(c => c.Status == ContractStatus.Expired));
        Stat("Đã kết thúc:", contracts.Count(c => c.Status == ContractStatus.Ended));
        Stat("Sắp hết hạn (30 ngày):", contracts.Count(c => c.Status == ContractStatus.Active && c.EndDate <= soon));
        Title("THỐNG KÊ THEO LOẠI");
        Stat("Hợp đồng cá nhân:", contracts.Count(c => c.ContractType == ContractType.Individual));
        Stat("Hợp đồng công ty:", contracts.Count(c => c.ContractType == ContractType.Company));
        Title("GIÁ TRỊ HỢP ĐỒNG");
        Stat("Tổng giá trị đang active:", contracts.Where(c => c.Status == ContractStatus.Active).Sum(c => c.PriceAgreed), "#,##0 \"VNĐ\"");
        st.Columns().AdjustToContents();

        return Save(wb);
    }

    /// <summary>Sổ đăng ký lưu trú: sheet người Việt Nam và sheet khách nước ngoài, gộp ô số phòng như bản cũ.</summary>
    public byte[] TenantRegister(List<TenantRow> all)
    {
        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = "Times New Roman";
        var vn = all.Where(t => !Nationality.IsForeign(t.Ethnicity)).ToList();
        var foreign = all.Where(t => Nationality.IsForeign(t.Ethnicity)).ToList();

        var ws = wb.Worksheets.Add("Việt Nam");
        WriteRegisterSheet(ws, vn,
            $"DANH SÁCH ĐĂNG KÝ Ở TẠI {landlord.Value.Name.ToUpper(Fmt.Vi)}{(string.IsNullOrWhiteSpace(landlord.Value.RegisterArea) ? "" : ", " + landlord.Value.RegisterArea.ToUpper(Fmt.Vi))}",
            ["STT", "Số phòng", "Họ và Tên", "Năm sinh", "Số điện thoại", "Giới tính", "Số giấy tờ", "Dân tộc", "Địa chỉ thường trú", "Biển số xe", "Ghi chú"],
            [6, 10, 22, 11, 14, 8, 15, 9, 48, 12, 12],
            t => [t.RoomName, t.TenantName, Fmt.Date(t.BirthDate), t.PhoneNumber ?? "", t.Gender ?? "", t.IdentityCard, t.Ethnicity ?? "Kinh", t.PermanentAddress ?? "", t.VehiclePlate ?? "", ""],
            "Tổng số người", "Tổng số phòng", "DANH SÁCH NGƯỜI VIỆT NAM");

        var wf = wb.Worksheets.Add("Nước ngoài");
        if (foreign.Count > 0)
        {
            WriteRegisterSheet(wf, foreign,
                $"DANH SÁCH KHÁCH NƯỚC NGOÀI LƯU TRÚ TẠI {landlord.Value.Name.ToUpper(Fmt.Vi)}",
                ["STT", "Số phòng", "Họ và Tên", "Năm sinh", "Giới tính", "Số giấy tờ", "Quốc tịch", "Địa chỉ"],
                [6, 10, 22, 11, 8, 15, 10, 48],
                t => [t.RoomName, t.TenantName, Fmt.Date(t.BirthDate), t.Gender ?? "", t.IdentityCard, Nationality.Of(t.Ethnicity), t.PermanentAddress ?? ""],
                "Tổng số khách nước ngoài", "Số phòng có khách nước ngoài", "DANH SÁCH KHÁCH NƯỚC NGOÀI");
        }
        else
        {
            wf.Cell(1, 1).Value = "Không có khách nước ngoài";
            wf.Cell(1, 1).Style.Font.Bold = true;
        }

        ws.SetTabActive();
        return Save(wb);
    }

    private void WriteRegisterSheet(IXLWorksheet ws, List<TenantRow> rows, string title, string[] headers, double[] widths,
        Func<TenantRow, string[]> values, string totalPeopleLabel, string totalRoomsLabel, string pageHeader)
    {
        int cols = headers.Length;
        var titleRange = ws.Range(1, 1, 2, cols).Merge();
        titleRange.Value = title;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        titleRange.Style.Alignment.WrapText = true;

        var dateRange = ws.Range(3, 1, 3, cols).Merge();
        dateRange.Value = $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
        dateRange.Style.Font.Italic = true;

        for (int i = 0; i < cols; i++) ws.Cell(4, i + 1).Value = headers[i];
        var head = ws.Range(4, 1, 4, cols);
        head.Style.Font.Bold = true;
        head.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        head.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        head.Style.Alignment.WrapText = true;
        head.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        head.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        int row = 5, stt = 1;
        var roomRows = new Dictionary<string, List<int>>();
        foreach (var t in rows)
        {
            if (!roomRows.TryGetValue(t.RoomName, out var list)) roomRows[t.RoomName] = list = [];
            list.Add(row);
            ws.Cell(row, 1).Value = stt++;
            var v = values(t);
            for (int i = 0; i < v.Length; i++) ws.Cell(row, i + 2).Value = v[i];
            row++;
        }
        if (rows.Count > 0)
        {
            var data = ws.Range(5, 1, row - 1, cols);
            data.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            data.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            data.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            data.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            data.Style.Alignment.WrapText = true;
        }
        foreach (var g in roomRows.Values.Where(g => g.Count > 1))
        {
            // Chỉ gộp khi các dòng cùng phòng liền nhau (danh sách đã sắp theo phòng)
            if (g.Max() - g.Min() + 1 == g.Count) ws.Range(g.Min(), 2, g.Max(), 2).Merge();
        }

        row++;
        var total1 = ws.Range(row, 1, row, 3).Merge();
        total1.Value = $"{totalPeopleLabel}: {rows.Count}";
        total1.Style.Font.Bold = true;
        row++;
        var total2 = ws.Range(row, 1, row, 3).Merge();
        total2.Value = $"{totalRoomsLabel}: {roomRows.Count}";
        total2.Style.Font.Bold = true;

        for (int i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.Header.Center.AddText(pageHeader).SetBold();
        ws.PageSetup.Footer.Left.AddText("Ngày in: ").SetFontSize(10);
        ws.PageSetup.Footer.Left.AddText(XLHFPredefinedText.Date).SetFontSize(10);
        ws.PageSetup.Footer.Center.AddText(XLHFPredefinedText.PageNumber);
        ws.PageSetup.Footer.Center.AddText("/");
        ws.PageSetup.Footer.Center.AddText(XLHFPredefinedText.NumberOfPages);
        ws.PageSetup.Footer.Right.AddText(landlord.Value.Name).SetFontSize(10);
        ws.PageSetup.Margins.SetLeft(0.7).SetRight(0.7).SetTop(0.7).SetBottom(0.7).SetHeader(0.3).SetFooter(0.3);
        ws.SheetView.FreezeRows(4);
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ===================== HỢP ĐỒNG WORD =====================

    public string ContractTemplatePath => Path.Combine(env.ContentRootPath, "Templates", "HOPDONG.docx");

    /// <summary>Trộn dữ liệu hợp đồng vào mẫu Templates/HOPDONG.docx (các trường MERGEFIELD).</summary>
    public byte[] PrintContract(Contract contract)
    {
        if (!File.Exists(ContractTemplatePath)) throw new InvalidOperationException("Không tìm thấy file mẫu hợp đồng (Templates/HOPDONG.docx).");
        var data = ContractMergeData(contract);

        using var ms = new MemoryStream();
        using (var fs = File.OpenRead(ContractTemplatePath)) fs.CopyTo(ms);
        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var parts = new List<OpenXmlPartRootElement?> { doc.MainDocumentPart?.Document };
            if (doc.MainDocumentPart != null)
            {
                parts.AddRange(doc.MainDocumentPart.HeaderParts.Select(h => (OpenXmlPartRootElement)h.Header));
                parts.AddRange(doc.MainDocumentPart.FooterParts.Select(f => (OpenXmlPartRootElement)f.Footer));
            }
            foreach (var root in parts.OfType<OpenXmlPartRootElement>())
            {
                MergeFields(root, data);
                root.Save();
            }
        }
        return ms.ToArray();
    }

    public Dictionary<string, string> ContractMergeData(Contract contract)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ngay"] = contract.StartDate.Day.ToString(),
            ["thang"] = contract.StartDate.Month.ToString(),
            ["nam"] = contract.StartDate.Year.ToString(),
            ["sophong"] = string.Join(", ", contract.ContractRooms.Select(cr => cr.Room.Name)),
            ["giathue"] = Fmt.Money(contract.PriceAgreed),
            ["giathue_text"] = VietnameseNumber.ToWords(contract.PriceAgreed),
            ["giadien"] = Fmt.Money(contract.ElectricityPrice),
            ["gianuoc"] = Fmt.Money(contract.WaterPrice),
            ["tiencoc"] = Fmt.Money(contract.PriceAgreed * 2),
            ["tiencoc_text"] = VietnameseNumber.ToWords(contract.PriceAgreed * 2),
            ["ngaybatdau"] = contract.StartDate.ToString("dd/MM/yyyy"),
            ["ngayketthuc"] = contract.EndDate.ToString("dd/MM/yyyy"),
            ["sothang"] = Fmt.MonthsBetween(contract.StartDate, contract.EndDate).ToString(),
            ["ngaynhanphong"] = contract.MoveInDate.ToString("dd/MM/yyyy"),
            ["ghichu"] = contract.Note ?? "",
            ["dientich"] = "16 m²"
        };

        var sb = new StringBuilder();
        if (contract.IsCompany && contract.Company != null)
        {
            var c = contract.Company;
            sb.AppendLine($"Công ty: {c.CompanyName}");
            sb.AppendLine($"Mã số thuế: {c.TaxCode}");
            sb.AppendLine($"Địa chỉ: {c.Address}");
            sb.AppendLine($"Điện thoại: {c.Phone}");
            if (!string.IsNullOrEmpty(c.Representative))
            {
                sb.AppendLine($"Người đại diện: {c.Representative}");
                if (!string.IsNullOrEmpty(c.RepresentativePhone)) sb.AppendLine($"Số điện thoại người đại diện: {c.RepresentativePhone}");
            }
        }
        else
        {
            var tenants = contract.ContractTenants.OrderBy(ct => ct.CreatedAt).Select(ct => ct.Tenant).ToList();
            if (tenants.Count > 0)
            {
                var main = tenants[0];
                sb.AppendLine($"Họ và tên: {main.FullName}");
                sb.AppendLine($"CMND/CCCD: {main.IdentityCard}");
                sb.AppendLine($"Điện thoại: {main.PhoneNumber}");
                sb.AppendLine($"Địa chỉ thường trú: {main.PermanentAddress}");
                if (main.BirthDate.HasValue) sb.AppendLine($"Ngày sinh: {main.BirthDate:dd/MM/yyyy}");
                if (tenants.Count > 1)
                {
                    sb.AppendLine();
                    sb.AppendLine("Cùng với những người sau:");
                    for (int i = 1; i < tenants.Count; i++) sb.AppendLine($"{i}. {tenants[i].FullName} - CCCD: {tenants[i].IdentityCard}");
                }
            }
        }
        d["benthue"] = sb.ToString().TrimEnd();
        return d;
    }

    /// <summary>Thay các trường MERGEFIELD (dạng fldSimple và dạng fldChar) bằng giá trị, giữ định dạng của run kết quả.</summary>
    private static void MergeFields(OpenXmlElement root, Dictionary<string, string> data)
    {
        foreach (var simple in root.Descendants<SimpleField>().ToList())
        {
            var name = FieldName(simple.Instruction?.Value);
            if (name == null || !data.TryGetValue(name, out var value)) continue;
            var props = simple.Descendants<RunProperties>().FirstOrDefault()?.CloneNode(true) as RunProperties;
            var run = BuildRun(value, props);
            simple.Parent?.ReplaceChild(run, simple);
        }

        // Dạng phức: begin - instrText - separate - kết quả - end, có thể trải trên nhiều run
        static bool IsChar(Run r, FieldCharValues type) => r.GetFirstChild<FieldChar>()?.FieldCharType?.Value == type;

        foreach (var paragraph in root.Descendants<Paragraph>().ToList())
        {
            int searchFrom = 0;
            while (true)
            {
                var runs = paragraph.Elements<Run>().ToList();
                if (searchFrom >= runs.Count) break;
                int begin = runs.FindIndex(searchFrom, r => IsChar(r, FieldCharValues.Begin));
                if (begin < 0) break;
                int end = runs.FindIndex(begin, r => IsChar(r, FieldCharValues.End));
                if (end < 0) break;
                int sep = runs.FindIndex(begin, end - begin, r => IsChar(r, FieldCharValues.Separate));

                var instr = string.Concat(runs.Skip(begin).Take((sep >= 0 ? sep : end) - begin).SelectMany(r => r.Elements<FieldCode>()).Select(fc => fc.Text));
                var name = FieldName(instr);
                if (name == null || !data.TryGetValue(name, out var value))
                {
                    searchFrom = end + 1; // không phải trường cần trộn, bỏ qua
                    continue;
                }

                var resultRun = sep >= 0 ? runs.Skip(sep + 1).Take(end - sep - 1).FirstOrDefault(r => r.GetFirstChild<Text>() != null) : null;
                var props = (resultRun ?? runs[begin]).RunProperties?.CloneNode(true) as RunProperties;
                runs[begin].InsertBeforeSelf(BuildRun(value, props));
                for (int i = begin; i <= end; i++) runs[i].Remove();
                searchFrom = begin + 1;
            }
        }
    }

    private static string? FieldName(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return null;
        var parts = instruction.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !parts[0].Equals("MERGEFIELD", StringComparison.OrdinalIgnoreCase)) return null;
        return parts[1].Trim('"');
    }

    private static Run BuildRun(string value, RunProperties? props)
    {
        var run = new Run();
        if (props != null) run.Append(props);
        var lines = value.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) run.Append(new Break());
            run.Append(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        return run;
    }
}
