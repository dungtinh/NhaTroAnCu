using NhaTroAnCu.Models;
using NhaTroAnCu.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using System.Web.Hosting;
using System.Globalization;
using iTextSharp.text.pdf.draw;
using ClosedXML.Excel;

namespace NhaTroAnCu.Controllers
{
    public class TenantContractsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        public ActionResult Index(
            int page = 1,
            int pageSize = 50,
            string searchName = "",
            string searchCard = "",
            string searchAddress = "",
            string sortField = "ContractSignedDate",
            string sortDirection = "desc",
            string fromDate = null,
            string toDate = null)
        {
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            var query = from ct in db.ContractTenants
                        join t in db.Tenants on ct.TenantId equals t.Id
                        join c in db.Contracts on ct.ContractId equals c.Id
                        join r in db.Rooms on c.RoomId equals r.Id
                        select new
                        {
                            t.FullName,
                            t.IdentityCard,
                            t.Gender,
                            t.PermanentAddress,
                            t.PhoneNumber,
                            t.Photo, // Thêm trường ảnh
                            t.Ethnicity,         // Thêm mới
                            t.VehiclePlate,      // Thêm mới
                            RoomName = r.Name,
                            ContractSignedDate = c.StartDate,
                            MoveInDate = ct.Contract.MoveInDate
                        };

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(x => x.FullName.Contains(searchName));
            if (!string.IsNullOrEmpty(searchCard))
                query = query.Where(x => x.IdentityCard.Contains(searchCard));
            if (!string.IsNullOrEmpty(searchAddress))
                query = query.Where(x => x.PermanentAddress.Contains(searchAddress));

            if (from.HasValue)
                query = query.Where(x => x.ContractSignedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.ContractSignedDate <= to.Value);

            switch (sortField)
            {
                case "FullName":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.FullName) : query.OrderByDescending(x => x.FullName);
                    break;
                case "ContractSignedDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
                case "MoveInDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.MoveInDate) : query.OrderByDescending(x => x.MoveInDate);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
            }

            int totalItems = query.Count();

            // Lấy dữ liệu và thêm số thứ tự
            var items = query.Skip((page - 1) * pageSize).Take(pageSize)
                .ToList()
                .Select((x, index) => new TenantContractListItemViewModel
                {
                    OrderNumber = (page - 1) * pageSize + index + 1, // Thêm số thứ tự
                    FullName = x.FullName,
                    IdentityCard = x.IdentityCard,
                    Gender = x.Gender,
                    PermanentAddress = x.PermanentAddress,
                    PhoneNumber = x.PhoneNumber,
                    Photo = x.Photo,
                    RoomName = x.RoomName,
                    ContractSignedDate = x.ContractSignedDate,
                    MoveInDate = x.MoveInDate,
                    Ethnicity = x.Ethnicity,           // Thêm mới
                    VehiclePlate = x.VehiclePlate      // Thêm mới
                }).ToList();

            var model = new TenantContractListViewModel
            {
                Items = items,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                SearchName = searchName,
                SearchCard = searchCard,
                SearchAddress = searchAddress,
                SortField = sortField,
                SortDirection = sortDirection,
                FromDate = from,
                ToDate = to
            };

            return View(model);
        }



        public ActionResult ExportToPdf(
             string searchName = "",
             string searchCard = "",
             string searchAddress = "",
             string sortField = "ContractSignedDate",
             string sortDirection = "desc",
             string fromDate = null,
             string toDate = null)
        {
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            var query = from ct in db.ContractTenants
                        join t in db.Tenants on ct.TenantId equals t.Id
                        join c in db.Contracts on ct.ContractId equals c.Id
                        join r in db.Rooms on c.RoomId equals r.Id
                        select new
                        {
                            t.FullName,
                            t.IdentityCard,
                            t.Gender,
                            t.PermanentAddress,
                            t.PhoneNumber,
                            t.Photo,
                            t.Ethnicity,
                            t.VehiclePlate,
                            RoomName = r.Name,
                            ContractSignedDate = c.StartDate,
                            MoveInDate = ct.Contract.MoveInDate
                        };

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(x => x.FullName.Contains(searchName));
            if (!string.IsNullOrEmpty(searchCard))
                query = query.Where(x => x.IdentityCard.Contains(searchCard));
            if (!string.IsNullOrEmpty(searchAddress))
                query = query.Where(x => x.PermanentAddress.Contains(searchAddress));

            if (from.HasValue)
                query = query.Where(x => x.ContractSignedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.ContractSignedDate <= to.Value);

            switch (sortField)
            {
                case "FullName":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.FullName) : query.OrderByDescending(x => x.FullName);
                    break;
                case "ContractSignedDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
                case "MoveInDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.MoveInDate) : query.OrderByDescending(x => x.MoveInDate);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
            }

            var items = query.ToList();

            string fontPath = HostingEnvironment.MapPath("~/Fonts/times.ttf");
            BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

            var mainTitleFont = new Font(baseFont, 24, Font.BOLD, new BaseColor(0, 51, 102));
            var titleFont = new Font(baseFont, 18, Font.BOLD, new BaseColor(0, 51, 102));
            var subtitleFont = new Font(baseFont, 14, Font.NORMAL, BaseColor.GRAY);
            var headerFont = new Font(baseFont, 11, Font.BOLD, BaseColor.WHITE);
            var rowFont = new Font(baseFont, 10, Font.NORMAL);
            var footerFont = new Font(baseFont, 10, Font.ITALIC, BaseColor.GRAY);

            var detailTitleFont = new Font(baseFont, 20, Font.BOLD, new BaseColor(0, 51, 102));
            var detailLabelFont = new Font(baseFont, 12, Font.BOLD, new BaseColor(70, 70, 70));
            var detailValueFont = new Font(baseFont, 12, Font.NORMAL);
            var detailHeaderFont = new Font(baseFont, 14, Font.BOLD, BaseColor.WHITE);

            using (var stream = new MemoryStream())
            {
                Document pdfDoc = new Document(PageSize.A4, 20, 10, 56, 45);
                PdfWriter writer = PdfWriter.GetInstance(pdfDoc, stream);
                writer.PageEvent = new CustomPdfPageEventHelper();
                pdfDoc.Open();

                // TRANG BÌA
                pdfDoc.Add(new Paragraph(" "));
                pdfDoc.Add(new Paragraph(" "));

                var mainTitle = new Paragraph("NHÀ TRỌ AN CƯ", mainTitleFont);
                mainTitle.Alignment = Element.ALIGN_CENTER;
                pdfDoc.Add(mainTitle);

                var line = new LineSeparator(2f, 80f, new BaseColor(0, 51, 102), Element.ALIGN_CENTER, -2);
                pdfDoc.Add(new Chunk(line));
                pdfDoc.Add(new Paragraph(" "));

                var reportTitle = new Paragraph("DANH SÁCH NGƯỜI THUÊ PHÒNG", titleFont);
                reportTitle.Alignment = Element.ALIGN_CENTER;
                pdfDoc.Add(reportTitle);

                var exportDate = new Paragraph($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}", subtitleFont);
                exportDate.Alignment = Element.ALIGN_CENTER;
                pdfDoc.Add(exportDate);

                pdfDoc.Add(new Paragraph(" "));
                pdfDoc.Add(new Paragraph(" "));

                // PHẦN 1: BẢNG DANH SÁCH
                PdfPTable table = new PdfPTable(9);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 1f, 2.5f, 2.1f, 0.9f, 3f, 1.9f, 1.4f, 1.7f, 1.7f });
                table.SpacingBefore = 15f;
                table.SpacingAfter = 20f;

                string[] headers = { "TT", "Họ tên", "Số thẻ", "GT", "Địa chỉ", "SĐT", "Phòng", "Ngày ký", "Ngày vào" };
                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont));
                    cell.BackgroundColor = new BaseColor(0, 51, 102);
                    cell.HorizontalAlignment = PdfPCell.ALIGN_CENTER;
                    cell.VerticalAlignment = PdfPCell.ALIGN_MIDDLE;
                    cell.Padding = 8;
                    cell.BorderColor = BaseColor.WHITE;
                    table.AddCell(cell);
                }

                int orderNumber = 1;
                bool isAlternateRow = false;
                foreach (var item in items)
                {
                    var bgColor = isAlternateRow ? new BaseColor(240, 240, 240) : BaseColor.WHITE;

                    var sttCell = new PdfPCell(new Phrase(orderNumber.ToString(), rowFont))
                    {
                        HorizontalAlignment = PdfPCell.ALIGN_CENTER,
                        BackgroundColor = bgColor,
                        Padding = 5,
                        BorderColor = new BaseColor(200, 200, 200)
                    };
                    table.AddCell(sttCell);

                    AddTableCell(table, item.FullName, rowFont, bgColor);
                    AddTableCell(table, item.IdentityCard, rowFont, bgColor);
                    AddTableCell(table, item.Gender, rowFont, bgColor, PdfPCell.ALIGN_CENTER);
                    AddTableCell(table, item.PermanentAddress, rowFont, bgColor);
                    AddTableCell(table, item.PhoneNumber, rowFont, bgColor);
                    AddTableCell(table, item.RoomName, rowFont, bgColor, PdfPCell.ALIGN_CENTER);
                    AddTableCell(table, item.ContractSignedDate.ToString("dd/MM/yyyy"), rowFont, bgColor, PdfPCell.ALIGN_CENTER);
                    AddTableCell(table, item.MoveInDate.ToString("dd/MM/yyyy"), rowFont, bgColor, PdfPCell.ALIGN_CENTER);

                    orderNumber++;
                    isAlternateRow = !isAlternateRow;
                }

                pdfDoc.Add(table);

                pdfDoc.Add(new Paragraph(" "));
                var footerLine = new LineSeparator(1f, 100f, BaseColor.GRAY, Element.ALIGN_CENTER, -2);
                pdfDoc.Add(new Chunk(footerLine));

                var footerInfo = new Paragraph();
                footerInfo.Add(new Chunk("ĐỊA CHỈ: ", detailLabelFont));
                footerInfo.Add(new Chunk("Thôn Đình Ngọ (Ấp), Xã Hồng Phong, Huyện An Dương, Hải Phòng\n", detailValueFont));
                footerInfo.Add(new Chunk("CHỦ NHÀ TRỌ: ", detailLabelFont));
                footerInfo.Add(new Chunk("Tạ Ngọc Duy - CCCD: 034082002422\n", detailValueFont));
                footerInfo.Add(new Chunk("ĐIỆN THOẠI: ", detailLabelFont));
                footerInfo.Add(new Chunk("0975092833", detailValueFont));
                footerInfo.Alignment = Element.ALIGN_CENTER;
                pdfDoc.Add(footerInfo);

                // PHẦN 2: CHI TIẾT TỪNG NGƯỜI
                int tenantIndex = 1;
                foreach (var item in items)
                {
                    pdfDoc.NewPage();

                    // Header của trang chi tiết
                    var headerTable = new PdfPTable(1);
                    headerTable.WidthPercentage = 100;
                    var headerCell = new PdfPCell();
                    headerCell.BackgroundColor = new BaseColor(0, 51, 102);
                    headerCell.Border = Rectangle.NO_BORDER;
                    headerCell.Padding = 15;

                    var headerPara = new Paragraph("THÔNG TIN CHI TIẾT NGƯỜI THUÊ", new Font(baseFont, 18, Font.BOLD, BaseColor.WHITE));
                    headerPara.Alignment = Element.ALIGN_CENTER;
                    headerCell.AddElement(headerPara);

                    var tenantNumber = new Paragraph($"Người thuê số: {tenantIndex}/{items.Count}", new Font(baseFont, 12, Font.NORMAL, BaseColor.WHITE));
                    tenantNumber.Alignment = Element.ALIGN_CENTER;
                    headerCell.AddElement(tenantNumber);

                    headerTable.AddCell(headerCell);
                    pdfDoc.Add(headerTable);

                    pdfDoc.Add(new Paragraph(" "));

                    PdfPTable contentTable = new PdfPTable(1);
                    contentTable.WidthPercentage = 100;
                    contentTable.SpacingBefore = 20f;

                    // Ảnh (có thể 2 ảnh trên 1 dòng)
                    PdfPCell photoCell = new PdfPCell();
                    photoCell.Border = Rectangle.NO_BORDER;
                    photoCell.Padding = 10;
                    photoCell.HorizontalAlignment = PdfPCell.ALIGN_CENTER;

                    if (!string.IsNullOrEmpty(item.Photo))
                    {
                        var photos = item.Photo.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Take(2).ToArray();
                        PdfPTable photoRow = new PdfPTable(photos.Length);
                        photoRow.WidthPercentage = 100;
                        foreach (var p in photos)
                        {
                            PdfPCell imgCell = new PdfPCell();
                            imgCell.Border = Rectangle.NO_BORDER;
                            imgCell.HorizontalAlignment = PdfPCell.ALIGN_CENTER;
                            try
                            {
                                string imagePath = HostingEnvironment.MapPath(p.Trim());
                                if (System.IO.File.Exists(imagePath))
                                {
                                    iTextSharp.text.Image photo = iTextSharp.text.Image.GetInstance(imagePath);
                                    photo.ScaleToFit(200f, 150f);
                                    photo.Border = Rectangle.BOX;
                                    photo.BorderColor = new BaseColor(200, 200, 200);
                                    photo.BorderWidth = 1f;
                                    imgCell.AddElement(photo);
                                }
                                else
                                {
                                    AddNoPhotoPlaceholder(imgCell);
                                }
                            }
                            catch
                            {
                                AddNoPhotoPlaceholder(imgCell);
                            }
                            photoRow.AddCell(imgCell);
                        }
                        photoCell.AddElement(photoRow);
                    }
                    else
                    {
                        AddNoPhotoPlaceholder(photoCell);
                    }
                    contentTable.AddCell(photoCell);

                    // Thông tin chi tiết
                    PdfPCell infoCell = new PdfPCell();
                    infoCell.Border = Rectangle.NO_BORDER;
                    infoCell.Padding = 10;

                    var nameTitle = new Paragraph(item.FullName, new Font(baseFont, 18, Font.BOLD, new BaseColor(0, 51, 102)));
                    nameTitle.SpacingAfter = 15f;
                    infoCell.AddElement(nameTitle);

                    PdfPTable infoTable = new PdfPTable(2);
                    infoTable.WidthPercentage = 100;
                    infoTable.SetWidths(new float[] { 40f, 60f });

                    AddInfoRow(infoTable, "Số CMND/CCCD:", item.IdentityCard, detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Giới tính:", item.Gender, detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Dân tộc:", item.Ethnicity ?? "Kinh", detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Địa chỉ thường trú:", item.PermanentAddress, detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Số điện thoại:", item.PhoneNumber, detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Biển số xe:", item.VehiclePlate ?? "Không có", detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Phòng thuê:", item.RoomName, detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Ngày ký hợp đồng:", item.ContractSignedDate.ToString("dd/MM/yyyy"), detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Ngày vào ở:", item.MoveInDate.ToString("dd/MM/yyyy"), detailLabelFont, detailValueFont);

                    infoCell.AddElement(infoTable);
                    contentTable.AddCell(infoCell);

                    pdfDoc.Add(contentTable);

                    tenantIndex++;
                }

                pdfDoc.Close();

                byte[] pdfBytes = stream.ToArray();
                return File(pdfBytes, "application/pdf", "DanhSachNguoiThuePhong.pdf");
            }
        }

        public ActionResult ExportToWord(
            string searchName = "",
            string searchCard = "",
            string searchAddress = "",
            string sortField = "ContractSignedDate",
            string sortDirection = "desc",
            string fromDate = null,
            string toDate = null)
        {
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            var query = from ct in db.ContractTenants
                        join t in db.Tenants on ct.TenantId equals t.Id
                        join c in db.Contracts on ct.ContractId equals c.Id
                        join r in db.Rooms on c.RoomId equals r.Id
                        select new
                        {
                            FullName = t.FullName ?? "",
                            IdentityCard = t.IdentityCard ?? "",
                            Gender = t.Gender ?? "",
                            PermanentAddress = t.PermanentAddress ?? "",
                            PhoneNumber = t.PhoneNumber ?? "",
                            Photo = t.Photo,
                            Ethnicity = t.Ethnicity ?? "",
                            VehiclePlate = t.VehiclePlate ?? "",
                            RoomName = r.Name ?? "",
                            ContractSignedDate = c.StartDate,
                            MoveInDate = ct.Contract.MoveInDate
                        };

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(x => x.FullName.Contains(searchName));
            if (!string.IsNullOrEmpty(searchCard))
                query = query.Where(x => x.IdentityCard.Contains(searchCard));
            if (!string.IsNullOrEmpty(searchAddress))
                query = query.Where(x => x.PermanentAddress.Contains(searchAddress));

            if (from.HasValue)
                query = query.Where(x => x.ContractSignedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.ContractSignedDate <= to.Value);

            switch (sortField)
            {
                case "FullName":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.FullName) : query.OrderByDescending(x => x.FullName);
                    break;
                case "ContractSignedDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
                case "MoveInDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.MoveInDate) : query.OrderByDescending(x => x.MoveInDate);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
            }

            var items = query.ToList();

            var doc = new Aspose.Words.Document();
            var builder = new Aspose.Words.DocumentBuilder(doc);

            var pageSetup = builder.PageSetup;
            pageSetup.LeftMargin = Aspose.Words.ConvertUtil.MillimeterToPoint(10);
            pageSetup.RightMargin = Aspose.Words.ConvertUtil.MillimeterToPoint(10);
            pageSetup.Orientation = Aspose.Words.Orientation.Portrait;

            builder.Font.Name = "Times New Roman";
            builder.Font.Size = 12;

            // TRANG BÌA
            builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
            builder.Font.Size = 24;
            builder.Font.Bold = true;
            builder.Font.Color = System.Drawing.Color.FromArgb(0, 51, 102);
            builder.Writeln("NHÀ TRỌ AN CƯ");

            builder.Font.Size = 14;
            builder.Font.Bold = false;
            builder.Font.Color = System.Drawing.Color.Gray;
            builder.Writeln("Thôn Đình Ngọ (Ấp), Xã Hồng Phong, Huyện An Dương, Hải Phòng");
            builder.Writeln("");

            builder.Font.Size = 18;
            builder.Font.Bold = true;
            builder.Font.Color = System.Drawing.Color.FromArgb(0, 51, 102);
            builder.Writeln("DANH SÁCH NGƯỜI THUÊ PHÒNG");

            builder.Font.Size = 12;
            builder.Font.Bold = false;
            builder.Font.Color = System.Drawing.Color.Black;
            builder.Writeln($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");
            builder.Writeln("");

            builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Left;

            // BẢNG DANH SÁCH
            var table = builder.StartTable();

            string[] headers = { "STT", "Họ tên", "Số thẻ", "GT", "Địa chỉ", "SĐT", "Phòng", "Ngày ký", "Ngày vào" };
            foreach (var header in headers)
            {
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = System.Drawing.Color.FromArgb(0, 51, 102);
                builder.Font.Color = System.Drawing.Color.White;
                builder.Font.Bold = true;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                builder.Write(header);
            }
            builder.EndRow();

            int orderNumber = 1;
            foreach (var item in items)
            {
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.Font.Color = System.Drawing.Color.Black;
                builder.Font.Bold = false;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                builder.Write(orderNumber.ToString());

                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Left;
                builder.Write(item.FullName ?? "");

                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.Write(item.IdentityCard ?? "");

                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                builder.Write(item.Gender ?? "");

                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Left;
                builder.Write(item.PermanentAddress ?? "");

                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.Write(item.PhoneNumber ?? "");

                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                builder.Write(item.RoomName ?? "");

                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.Write(item.ContractSignedDate.ToString("dd/MM/yyyy"));

                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.Write(item.MoveInDate.ToString("dd/MM/yyyy"));

                builder.EndRow();
                orderNumber++;
            }

            builder.EndTable();

            builder.Writeln("");
            builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
            builder.Font.Bold = true;
            builder.Font.Size = 14;
            builder.Font.Color = System.Drawing.Color.FromArgb(0, 51, 102);
            builder.Writeln("NHÀ TRỌ AN CƯ");

            builder.Font.Bold = false;
            builder.Font.Size = 12;
            builder.Font.Color = System.Drawing.Color.Black;
            builder.Writeln("ĐỊA CHỈ: THÔN ĐÌNH NGỌ (ẤP), XÃ HỒNG PHONG, HUYỆN AN DƯƠNG, HẢI PHÒNG");
            builder.Writeln("CHỦ NHÀ TRỌ: TẠ NGỌC DUY - CCCD: 034082002422");
            builder.Writeln("ĐIỆN THOẠI: 0975092833");

            // PHẦN CHI TIẾT TỪNG NGƯỜI
            int tenantIndex = 1;
            foreach (var item in items)
            {
                builder.InsertBreak(Aspose.Words.BreakType.PageBreak);

                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                builder.Font.Size = 18;
                builder.Font.Bold = true;
                builder.Font.Color = System.Drawing.Color.FromArgb(0, 51, 102);
                builder.Writeln("THÔNG TIN CHI TIẾT NGƯỜI THUÊ");

                builder.Font.Size = 12;
                builder.Font.Bold = false;
                builder.Writeln($"Người thuê số: {tenantIndex}/{items.Count}");
                builder.Writeln("");

                builder.Font.Size = 16;
                builder.Font.Bold = true;
                builder.Font.Color = System.Drawing.Color.FromArgb(0, 51, 102);
                builder.Writeln(item.FullName ?? "");
                builder.Writeln("");

                // Ảnh CCCD/Hộ chiếu (2 ảnh trên 1 dòng)
                if (!string.IsNullOrEmpty(item.Photo))
                {
                    var photos = item.Photo.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Take(2).ToArray();
                    builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                    builder.Font.Size = 12;
                    builder.Font.Bold = true;
                    builder.Font.Color = System.Drawing.Color.Black;
                    builder.Writeln("Ảnh giấy tờ:");
                    builder.Writeln(" ");
                    foreach (var p in photos)
                    {
                        try
                        {
                            string imagePath = Server.MapPath(p.Trim());
                            if (System.IO.File.Exists(imagePath))
                            {
                                var shape = builder.InsertImage(imagePath);
                                shape.Width = 200;
                                shape.Height = 150;
                                shape.WrapType = Aspose.Words.Drawing.WrapType.Inline;
                                builder.Write("   ");
                            }
                        }
                        catch
                        {
                            // Bỏ qua nếu không tải được ảnh
                        }
                    }
                    builder.Writeln(" ");
                }
                builder.Writeln(" ");
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Left;
                builder.Font.Size = 12;
                builder.Font.Bold = false;
                builder.Font.Color = System.Drawing.Color.Black;

                var infoTable = builder.StartTable();

                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Số giấy tờ:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.IdentityCard ?? "");
                builder.EndRow();

                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Giới tính:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.Gender ?? "");
                builder.EndRow();

                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Dân tộc:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.Ethnicity ?? "Kinh");
                builder.EndRow();

                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Địa chỉ thường trú:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.PermanentAddress ?? "");
                builder.EndRow();

                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Số điện thoại:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.PhoneNumber ?? "");
                builder.EndRow();

                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Biển số xe:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.VehiclePlate ?? "Không có");
                builder.EndRow();

                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Phòng thuê:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.RoomName ?? "");
                builder.EndRow();

                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Ngày ký hợp đồng:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.ContractSignedDate.ToString("dd/MM/yyyy"));
                builder.EndRow();

                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Ngày vào ở:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.MoveInDate.ToString("dd/MM/yyyy"));
                builder.EndRow();

                builder.EndTable();
                tenantIndex++;
            }

            string fileName = $"DanhSachNguoiThuePhong_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            string tempPath = Server.MapPath("~/Uploads/Tenants/");
            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            string filePath = Path.Combine(tempPath, fileName);
            doc.Save(filePath);

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            System.IO.File.Delete(filePath);

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }

        // Helper methods
        private void AddTableCell(PdfPTable table, string text, Font font, BaseColor bgColor, int align = PdfPCell.ALIGN_LEFT)
        {
            var cell = new PdfPCell(new Phrase(text, font))
            {
                HorizontalAlignment = align,
                BackgroundColor = bgColor,
                Padding = 5,
                BorderColor = new BaseColor(200, 200, 200)
            };
            table.AddCell(cell);
        }

        private void AddInfoRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            var labelCell = new PdfPCell(new Phrase(label, labelFont))
            {
                Border = Rectangle.NO_BORDER,
                PaddingBottom = 8,
                PaddingRight = 5,
                HorizontalAlignment = PdfPCell.ALIGN_RIGHT
            };
            table.AddCell(labelCell);

            var valueCell = new PdfPCell(new Phrase(value, valueFont))
            {
                Border = Rectangle.NO_BORDER,
                PaddingBottom = 8,
                PaddingLeft = 5
            };
            table.AddCell(valueCell);
        }

        private void AddNoPhotoPlaceholder(PdfPCell cell)
        {
            var placeholderTable = new PdfPTable(1);
            placeholderTable.WidthPercentage = 100;

            var placeholderCell = new PdfPCell();
            placeholderCell.FixedHeight = 150f;
            placeholderCell.BackgroundColor = new BaseColor(245, 245, 245);
            placeholderCell.BorderColor = new BaseColor(200, 200, 200);
            placeholderCell.BorderWidth = 1f;
            placeholderCell.HorizontalAlignment = PdfPCell.ALIGN_CENTER;
            placeholderCell.VerticalAlignment = PdfPCell.ALIGN_MIDDLE;

            string fontPath = HostingEnvironment.MapPath("~/Fonts/times.ttf");
            var noPhotoText = new Paragraph("Không có ảnh", new Font(BaseFont.CreateFont(fontPath, BaseFont.CP1252, BaseFont.NOT_EMBEDDED), 14, Font.ITALIC, BaseColor.GRAY));
            placeholderCell.AddElement(noPhotoText);

            placeholderTable.AddCell(placeholderCell);
            cell.AddElement(placeholderTable);
        }



        public ActionResult ExportToExcel(
      string searchName = "",
      string searchCard = "",
      string searchAddress = "",
      string sortField = "ContractSignedDate",
      string sortDirection = "desc",
      string fromDate = null,
      string toDate = null)
        {
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            var query = from ct in db.ContractTenants
                        join t in db.Tenants on ct.TenantId equals t.Id
                        join c in db.Contracts on ct.ContractId equals c.Id
                        join r in db.Rooms on c.RoomId equals r.Id
                        select new
                        {
                            t.FullName,
                            t.BirthDate,
                            t.Gender,
                            t.IdentityCard,
                            t.Ethnicity,
                            t.VehiclePlate,
                            t.PermanentAddress,
                            ContractSignedDate = c.StartDate,
                            RoomName = r.Name
                        };

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(x => x.FullName.Contains(searchName));
            if (!string.IsNullOrEmpty(searchCard))
                query = query.Where(x => x.IdentityCard.Contains(searchCard));
            if (!string.IsNullOrEmpty(searchAddress))
                query = query.Where(x => x.PermanentAddress.Contains(searchAddress));
            if (from.HasValue)
                query = query.Where(x => x.ContractSignedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.ContractSignedDate <= to.Value);

            switch (sortField)
            {
                case "FullName":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.FullName) : query.OrderByDescending(x => x.FullName);
                    break;
                case "ContractSignedDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
            }

            var items = query.ToList();
            int tongSoPhong = db.Rooms.Count();

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Thông Tin Lưu Trú");

                // ===== HEADER THÔNG TIN NHÀ TRỌ =====
                ws.Range("A1:J1").Merge().Value = "CƠ SỞ NHÀ TRỌ: NHÀ TRỌ AN CƯ";
                ws.Range("A2:J2").Merge().Value = "ĐỊA CHỈ: THÔN ĐÌNH NGỌ (ẤP), XÃ HỒNG PHONG, HUYỆN AN DƯƠNG, HẢI PHÒNG";
                ws.Range("A3:J3").Merge().Value = "CHỦ NHÀ TRỌ: TẠ NGỌC DUY - CCCD: 034082002422";
                ws.Range("A4:J4").Merge().Value = "ĐIỆN THOẠI: 0975092833";
                ws.Range("A5:J5").Merge().Value = $"Tổng số phòng: {tongSoPhong}";
                ws.Range("A1:J5").Style.Font.Bold = true;
                ws.Range("A1:J5").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Range("A1:J5").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Range("A1:J5").Style.Fill.BackgroundColor = XLColor.WhiteSmoke;

                // ===== TIÊU ĐỀ BẢNG =====
                ws.Range("A7:J7").Merge().Value = "DANH SÁCH LƯU TRÚ";
                ws.Range("A7:J7").Style.Font.Bold = true;
                ws.Range("A7:J7").Style.Font.FontSize = 14;
                ws.Range("A7:J7").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range("A7:J7").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Row(7).Height = 25;

                // ===== HEADER CỘT =====
                ws.Cell(8, 1).Value = "STT";
                ws.Cell(8, 2).Value = "Họ và Tên";
                ws.Cell(8, 3).Value = "Năm sinh";
                ws.Cell(8, 4).Value = "Giới tính";
                ws.Cell(8, 5).Value = "SỐ CCCD";
                ws.Cell(8, 6).Value = "Dân tộc";
                ws.Cell(8, 7).Value = "Phương tiện";
                ws.Cell(8, 8).Value = "ĐKTT";
                ws.Cell(8, 9).Value = "NGÀY ĐK";
                ws.Cell(8, 10).Value = "P/Ở";
                ws.Range("A8:J8").Style.Font.Bold = true;
                ws.Range("A8:J8").Style.Fill.BackgroundColor = XLColor.LightBlue;
                ws.Range("A8:J8").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range("A8:J8").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range("A8:J8").Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                ws.Row(8).Height = 20;

                // ===== DATA =====
                int row = 9;
                int stt = 1;
                foreach (var item in items)
                {
                    ws.Cell(row, 1).Value = stt++;
                    ws.Cell(row, 2).Value = item.FullName;
                    ws.Cell(row, 3).Value = item.BirthDate?.ToString("yyyy");
                    ws.Cell(row, 4).Value = item.Gender;
                    ws.Cell(row, 5).Value = item.IdentityCard;
                    ws.Cell(row, 6).Value = item.Ethnicity;
                    ws.Cell(row, 7).Value = item.VehiclePlate;
                    //ws.Cell(row, 8).Value = item.PermanentAddress;
                    ws.Cell(row, 8).Value = WrapText(item.PermanentAddress, 20);
                    ws.Cell(row, 8).Style.Alignment.WrapText = true;
                    ws.Cell(row, 9).Value = item.ContractSignedDate.ToString("dd/MM/yyyy");
                    ws.Cell(row, 10).Value = item.RoomName;

                    // Căn trái cho Họ tên, ĐKTT, Phương tiện
                    ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                    ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                    ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    // Chữ thường (không in đậm)
                    ws.Range(row, 1, row, 10).Style.Font.Bold = false;

                    // Border và căn giữa mặc định
                    ws.Range(row, 1, row, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Range(row, 1, row, 10).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    ws.Range(row, 1, row, 10).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Range(row, 1, row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Dòng chẵn lẻ
                    if ((row - 9) % 2 == 1)
                        ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.White;
                    else
                        ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.LightGray;

                    row++;
                }

                ws.Columns().AdjustToContents();
                ws.SheetView.FreezeRows(8);

                // ===== PAGE SETUP =====
                ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
                ws.PageSetup.PagesWide = 1;
                ws.PageSetup.PagesTall = 1;
                ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "ThongTinLuuTru.xlsx");
                }
            }
        }
        string WrapText(string text, int maxLength = 20)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
                return text;

            var result = new List<string>();
            int start = 0;
            while (start < text.Length)
            {
                // Đoạn còn lại ngắn hơn maxLength
                if (text.Length - start <= maxLength)
                {
                    result.Add(text.Substring(start));
                    break;
                }
                // Tìm dấu cách gần nhất trước maxLength
                int end = start + maxLength;
                int spaceIndex = text.LastIndexOf(' ', end, maxLength);
                if (spaceIndex <= start) // Không có dấu cách, phải cắt cứng
                    spaceIndex = end;
                result.Add(text.Substring(start, spaceIndex - start).Trim());
                start = spaceIndex;
                // Bỏ qua dấu cách đầu dòng tiếp theo nếu có
                while (start < text.Length && text[start] == ' ')
                    start++;
            }
            return string.Join(Environment.NewLine, result);
        }
    }
}