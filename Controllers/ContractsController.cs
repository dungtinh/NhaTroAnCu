using Microsoft.Owin.BuilderProperties;
using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Web;
using System.Web.Mvc;
using Xceed.Document.NET;
using Xceed.Pdf;
using Xceed.Words.NET;

namespace NhaTroAnCu.Controllers
{
    public class ContractsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: /Contracts/Create?roomId=5
        public ActionResult Create(int roomId)
        {
            var room = db.Rooms.Find(roomId);
            ViewBag.RoomId = new SelectList(db.Rooms.Where(r => !r.IsOccupied), "Id", "Name");
            return View(new ContractCreateViewModel()
            {
                MoveInDate = DateTime.Now,
                PriceAgreed = room.DefaultPrice,
                DepositAmount = room.DefaultPrice,
                ElectricityPrice = 3500,
                WaterPrice = 15000,
            });
        }


        // POST: /Contracts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ContractCreateViewModel vm, IEnumerable<HttpPostedFileBase> TenantPhotos)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.RoomId = new SelectList(db.Rooms.Where(r => !r.IsOccupied), "Id", "Name");
                return View(vm);
            }

            var contract = new Contract
            {
                RoomId = vm.RoomId,
                MoveInDate = vm.MoveInDate,
                StartDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day),
                EndDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day).AddMonths(vm.Months),
                PriceAgreed = vm.PriceAgreed,
                DepositAmount = vm.DepositAmount,
                Note = vm.Note,
                Status = "Active",
                ElectricityPrice = vm.ElectricityPrice,
                WaterPrice = vm.WaterPrice
            };

            db.Contracts.Add(contract);
            db.SaveChanges();

            var photoList = TenantPhotos?.ToList() ?? new List<HttpPostedFileBase>();

            for (int i = 0; i < vm.Tenants.Count; i++)
            {
                string photoPaths = null;
                var tenantPhotos = new List<string>();

                // Xử lý upload nhiều ảnh cho mỗi tenant
                // Lấy các file tương ứng với tenant hiện tại
                var files = photoList.Skip(i * 10).Take(10).Where(f => f != null && f.ContentLength > 0).ToList();

                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                    string ext = Path.GetExtension(file.FileName);
                    string uniqueName = fileName + "_" + Guid.NewGuid().ToString("N") + ext;
                    string serverPath = Server.MapPath("~/Uploads/TenantPhotos/");
                    if (!Directory.Exists(serverPath)) Directory.CreateDirectory(serverPath);
                    string savePath = Path.Combine(serverPath, uniqueName);
                    file.SaveAs(savePath);
                    tenantPhotos.Add("/Uploads/TenantPhotos/" + uniqueName);
                }

                // Lưu các đường dẫn ảnh, phân cách bằng dấu chấm phẩy
                if (tenantPhotos.Any())
                {
                    photoPaths = string.Join(";", tenantPhotos);
                }

                var t = vm.Tenants[i];
                var tenant = new Tenant
                {
                    FullName = t.FullName,
                    IdentityCard = t.IdentityCard,
                    PhoneNumber = t.PhoneNumber,
                    BirthDate = t.BirthDate,
                    Gender = t.Gender,
                    PermanentAddress = t.PermanentAddress,
                    Photo = photoPaths,
                    Ethnicity = t.Ethnicity,
                    VehiclePlate = t.VehiclePlate
                };
                db.Tenants.Add(tenant);
                db.SaveChanges();

                db.ContractTenants.Add(new ContractTenant
                {
                    ContractId = contract.Id,
                    TenantId = tenant.Id,
                    CreatedAt = DateTime.Now
                });
            }

            // Đánh dấu phòng đã có người ở
            var room = db.Rooms.Find(vm.RoomId);
            if (room != null)
            {
                room.IsOccupied = true;
            }

            db.SaveChanges();
            return RedirectToAction("Index", "Rooms");
        }

        // GET: /Contracts/End/5
        public ActionResult End(int id)
        {
            var contract = db.Contracts.Find(id);
            if (contract == null) return HttpNotFound();

            contract.Status = "Ended";
            contract.EndDate = DateTime.Now;

            var room = db.Rooms.Find(contract.RoomId);
            if (room != null) room.IsOccupied = false;

            db.SaveChanges();
            return RedirectToAction("index", "Rooms", new { id = contract.RoomId });
        }
        public ActionResult ExportContract(int id)
        {
            var contract = db.Contracts
     .Include("Room")
     .Include("ContractTenants.Tenant") // ✅ dùng string thay vì lambda
     .FirstOrDefault(c => c.Id == id);

            if (contract == null) return HttpNotFound();

            string path = Server.MapPath("~/App_Data/Contracts");

            Aspose.Words.Document doc = new Aspose.Words.Document();
            doc.RemoveAllChildren();
            var dict = new Dictionary<string, string>();
            string pathTemp = Server.MapPath("~/App_Data/templates/CT01.docx");
            var doctemp = new Aspose.Words.Document(pathTemp);
            Aspose.Words.DocumentBuilder builder = new Aspose.Words.DocumentBuilder(doctemp);

            // Hợp đồng
            dict = new Dictionary<string, string>();
            pathTemp = Server.MapPath("~/App_Data/templates/HOPDONG.docx");
            doctemp = new Aspose.Words.Document(pathTemp);
            dict.Add("ngay", contract.StartDate.ToString("dd"));
            dict.Add("thang", contract.StartDate.ToString("MM"));
            dict.Add("nam", contract.StartDate.ToString("yyyy"));

            builder = new Aspose.Words.DocumentBuilder(doctemp);
            builder.MoveToMergeField("benthue");
            foreach (var khach in contract.ContractTenants)
            {
                string hoTen = khach.Tenant.FullName;
                // Kiểm tra chưa đủ 18 tuổi
                if (khach.Tenant.BirthDate != null)
                {
                    var age = (contract.StartDate - khach.Tenant.BirthDate.Value).TotalDays / 365.25;
                    if (age < 18)
                    {
                        hoTen += " (chưa đủ 18 tuổi)";
                    }
                }
                // kiểm tra nếu là dòng cuối thì builder.Write
                if (khach != contract.ContractTenants.Last())
                    builder.Writeln("Họ và tên: " + hoTen + "; CC/CCCD: " + khach.Tenant.IdentityCard + "; Đt: " + khach.Tenant.PhoneNumber + ";");
                else
                    builder.Write("Họ và tên: " + hoTen + "; CC/CCCD: " + khach.Tenant.IdentityCard + "; Đt: " + khach.Tenant.PhoneNumber + ".");
            }
            dict.Add("sophong", contract.Room.Name);
            doctemp.MailMerge.Execute(dict.Keys.ToArray(), dict.Values.ToArray());

            if (doctemp.PageCount % 2 != 0)
            {
                builder.MoveToDocumentEnd();
                builder.InsertBreak(Aspose.Words.BreakType.PageBreak);
            }
            doc.AppendDocument(doctemp, Aspose.Words.ImportFormatMode.KeepSourceFormatting);

            // I, TỜ KHAI CT01
            foreach (var khach in contract.ContractTenants)
            {
                dict = new Dictionary<string, string>();
                pathTemp = Server.MapPath("~/App_Data/templates/CT01.docx");
                doctemp = new Aspose.Words.Document(pathTemp);
                dict.Add("hoten", khach.Tenant.FullName);
                dict.Add("sodienthoai", khach.Tenant.PhoneNumber);
                dict.Add("ngaysinh", khach.Tenant.BirthDate?.ToString("dd/MM/yyyy"));

                GenSoDinhDanh(khach.Tenant.IdentityCard, dict);
                doctemp.MailMerge.Execute(dict.Keys.ToArray(), dict.Values.ToArray());
                builder = new Aspose.Words.DocumentBuilder(doctemp);
                if (doctemp.PageCount % 2 != 0)
                {
                    builder.MoveToDocumentEnd();
                    builder.InsertBreak(Aspose.Words.BreakType.PageBreak);
                }
                doc.AppendDocument(doctemp, Aspose.Words.ImportFormatMode.KeepSourceFormatting);
            }
            // Tài sản            
            pathTemp = Server.MapPath("~/App_Data/templates/TAISAN.docx");
            doctemp = new Aspose.Words.Document(pathTemp);

            builder = new Aspose.Words.DocumentBuilder(doctemp);
            builder.MoveToMergeField("benthue");
            foreach (var khach in contract.ContractTenants)
            {
                string hoTen = khach.Tenant.FullName;
                // Kiểm tra chưa đủ 18 tuổi
                if (khach.Tenant.BirthDate != null)
                {
                    var age = (contract.StartDate - khach.Tenant.BirthDate.Value).TotalDays / 365.25;
                    if (age < 18)
                    {
                        hoTen += " (chưa đủ 18 tuổi)";
                    }
                }
                // kiểm tra nếu là dòng cuối thì builder.Write
                if (khach != contract.ContractTenants.Last())
                    builder.Writeln("Họ và tên: " + hoTen + "; CC/CCCD: " + khach.Tenant.IdentityCard + "; Đt: " + khach.Tenant.PhoneNumber + ";");
                else
                    builder.Write("Họ và tên: " + hoTen + "; CC/CCCD: " + khach.Tenant.IdentityCard + "; Đt: " + khach.Tenant.PhoneNumber + ".");
            }
            doctemp.MailMerge.Execute(dict.Keys.ToArray(), dict.Values.ToArray());
            if (doctemp.PageCount % 2 != 0)
            {
                builder.MoveToDocumentEnd();
                builder.InsertBreak(Aspose.Words.BreakType.PageBreak);
            }
            doc.AppendDocument(doctemp, Aspose.Words.ImportFormatMode.KeepSourceFormatting);


            string webpart = string.Empty;
            builder = new Aspose.Words.DocumentBuilder(doc);
            foreach (var khach in contract.ContractTenants)
            {
                webpart += khach.Tenant.FullName + "_";
                // --- Chèn ảnh khách thuê vào docx ---
                if (!string.IsNullOrEmpty(khach.Tenant.Photo))
                {
                    // Đường dẫn vật lý
                    string photoPath = Server.MapPath(khach.Tenant.Photo);
                    if (System.IO.File.Exists(photoPath))
                    {
                        // Chèn ảnh vào vị trí mong muốn, ví dụ dưới cùng trang
                        builder.MoveToDocumentEnd();
                        builder.Writeln(); // Xuống dòng
                        builder.InsertImage(photoPath);
                    }
                }
                // --- Kết thúc chèn ảnh ---
                if (doc.PageCount % 2 != 0)
                {
                    builder.MoveToDocumentEnd();
                    builder.InsertBreak(Aspose.Words.BreakType.PageBreak);
                }
            }
            webpart += "_phong_" + contract.Room.Name + "_c" + id + ".docx";
            foreach (Aspose.Words.Section section in doc.Sections)
            {
                section.ClearHeadersFooters();
            }
            path += webpart;
            doc.Save(path);
            return File(path, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", webpart);
        }

        public ActionResult ExportHistoryToPdf(
            int id,
            string searchName = "",
            string searchCard = "",
            string searchAddress = "",
            string sortField = "StartDate",
            string sortDirection = "desc",
            string fromDate = null,
            string toDate = null
        )
        {
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            var query = db.Contracts
                .Where(c => c.RoomId == id)
                .Select(c => new
                {
                    RoomName = c.Room.Name,
                    Tenants = c.ContractTenants.Select(ct => ct.Tenant.FullName),
                    IdentityCards = c.ContractTenants.Select(ct => ct.Tenant.IdentityCard),
                    Genders = c.ContractTenants.Select(ct => ct.Tenant.Gender),
                    Addresses = c.ContractTenants.Select(ct => ct.Tenant.PermanentAddress),
                    PhoneNumbers = c.ContractTenants.Select(ct => ct.Tenant.PhoneNumber),
                    c.StartDate,
                    c.EndDate,
                    c.Note
                });

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(x => x.Tenants.Any(n => n.Contains(searchName)));
            if (!string.IsNullOrEmpty(searchCard))
                query = query.Where(x => x.IdentityCards.Any(ic => ic.Contains(searchCard)));
            if (!string.IsNullOrEmpty(searchAddress))
                query = query.Where(x => x.Addresses.Any(addr => addr.Contains(searchAddress)));
            if (from.HasValue)
                query = query.Where(x => x.StartDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.StartDate <= to.Value);

            switch (sortField)
            {
                case "RoomName":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.RoomName) : query.OrderByDescending(x => x.RoomName);
                    break;
                case "StartDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.StartDate) : query.OrderByDescending(x => x.StartDate);
                    break;
                case "EndDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.EndDate) : query.OrderByDescending(x => x.EndDate);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(x => x.StartDate) : query.OrderByDescending(x => x.StartDate);
                    break;
            }

            var items = query.ToList();

            string fontPath = Server.MapPath("~/Fonts/times.ttf");
            var baseFont = iTextSharp.text.pdf.BaseFont.CreateFont(fontPath, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED);

            var titleFont = new iTextSharp.text.Font(baseFont, 16, iTextSharp.text.Font.BOLD);
            var headerFont = new iTextSharp.text.Font(baseFont, 12, iTextSharp.text.Font.BOLD);
            var rowFont = new iTextSharp.text.Font(baseFont, 11, iTextSharp.text.Font.NORMAL);

            using (var stream = new MemoryStream())
            {
                var pdfDoc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 10, 10, 20, 20);
                iTextSharp.text.pdf.PdfWriter.GetInstance(pdfDoc, stream);
                pdfDoc.Open();

                pdfDoc.Add(new iTextSharp.text.Paragraph("LỊCH SỬ THUÊ PHÒNG", titleFont));
                pdfDoc.Add(new iTextSharp.text.Paragraph($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}", rowFont));
                pdfDoc.Add(new iTextSharp.text.Paragraph(" "));

                var table = new iTextSharp.text.pdf.PdfPTable(9);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 2.2f, 2.8f, 2.2f, 1.2f, 3f, 2f, 2f, 2f, 3f });

                string[] headers = { "Phòng", "Người thuê", "Số thẻ", "Giới tính", "Địa chỉ", "SĐT", "Ngày thuê", "Ngày kết thúc", "Ghi chú" };
                foreach (var header in headers)
                {
                    var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(header, headerFont));
                    cell.BackgroundColor = new iTextSharp.text.BaseColor(220, 220, 220);
                    cell.HorizontalAlignment = iTextSharp.text.pdf.PdfPCell.ALIGN_CENTER;
                    table.AddCell(cell);
                }

                foreach (var item in items)
                {
                    table.AddCell(new iTextSharp.text.Phrase(item.RoomName, rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(string.Join(", ", item.Tenants), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(string.Join(", ", item.IdentityCards), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(string.Join(", ", item.Genders), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(string.Join(", ", item.Addresses), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(string.Join(", ", item.PhoneNumbers), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(item.StartDate.ToString("dd/MM/yyyy"), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(item.EndDate.ToString("dd/MM/yyyy") ?? "-", rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(item.Note, rowFont));
                }

                pdfDoc.Add(table);
                pdfDoc.Add(new iTextSharp.text.Paragraph(" "));
                pdfDoc.Add(new iTextSharp.text.Paragraph("NHÀ TRỌ AN CƯ", titleFont));
                pdfDoc.Add(new iTextSharp.text.Paragraph("ĐỊA CHỈ: THÔN ĐÌNH NGỌ (ẤP), XÃ HỒNG PHONG, HUYỆN AN DƯƠNG, HẢI PHÒNG", rowFont));
                pdfDoc.Add(new iTextSharp.text.Paragraph("CHỦ NHÀ TRỌ: TẠ NGỌC DUY - CCCD: 034082002422", rowFont));
                pdfDoc.Add(new iTextSharp.text.Paragraph("ĐIỆN THOẠI: 0975092833", rowFont));
                pdfDoc.Close();

                byte[] pdfBytes = stream.ToArray();
                return File(pdfBytes, "application/pdf", "LichSuThuePhong.pdf");
            }
        }
        void GenSoDinhDanh(string sdd, Dictionary<string, string> dict)
        {
            char s1 = ' ', s2 = ' ', s3 = ' ', s4 = ' ', s5 = ' ',
                s6 = ' ', s7 = ' ', s8 = ' ', s9 = ' ', s10 = ' ', s11 = ' ', s12 = ' ';
            if (!string.IsNullOrEmpty(sdd))
            {
                var chars = sdd.ToCharArray();
                if (chars.Length >= 12)
                {
                    s1 = chars[0];
                    s2 = chars[1];
                    s3 = chars[2];
                    s4 = chars[3];
                    s5 = chars[4];
                    s6 = chars[5];
                    s7 = chars[6];
                    s8 = chars[7];
                    s9 = chars[8];
                    s10 = chars[9];
                    s11 = chars[10];
                    s12 = chars[11];
                }
            }
            dict.Add("s1", s1.ToString());
            dict.Add("s2", s2.ToString());
            dict.Add("s3", s3.ToString());
            dict.Add("s4", s4.ToString());
            dict.Add("s5", s5.ToString());
            dict.Add("s6", s6.ToString());
            dict.Add("s7", s7.ToString());
            dict.Add("s8", s8.ToString());
            dict.Add("s9", s9.ToString());
            dict.Add("s10", s10.ToString());
            dict.Add("s11", s11.ToString());
            dict.Add("s12", s12.ToString());
        }
        [HttpPost]
        public ActionResult UploadContractScan(int contractId, HttpPostedFileBase scanFile)
        {
            var contract = db.Contracts.Find(contractId);
            if (contract == null) return HttpNotFound();

            if (scanFile != null && scanFile.ContentLength > 0)
            {
                string fileName = Path.GetFileNameWithoutExtension(scanFile.FileName);
                string ext = Path.GetExtension(scanFile.FileName);
                string uniqueName = $"contractscan_{contractId}_{DateTime.Now.Ticks}{ext}";
                string folder = Server.MapPath("~/Uploads/ContractScans/");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string filePath = Path.Combine(folder, uniqueName);
                scanFile.SaveAs(filePath);
                contract.ContractScanFilePath = "/Uploads/ContractScans/" + uniqueName;
                db.SaveChanges();
            }
            // Redirect về lại trang chi tiết phòng đang xem
            return RedirectToAction("Details", "Rooms", new { id = contract.RoomId });
        }

        public ActionResult Edit(int id)
        {
            var contract = db.Contracts.Include("ContractTenants.Tenant").FirstOrDefault(c => c.Id == id);
            if (contract == null) return HttpNotFound();

            var vm = new ContractEditViewModel
            {
                Id = contract.Id,
                RoomId = contract.RoomId,
                MoveInDate = contract.MoveInDate,
                Months = ((contract.EndDate.Year - contract.StartDate.Year) * 12 + contract.EndDate.Month - contract.StartDate.Month),
                PriceAgreed = contract.PriceAgreed,
                DepositAmount = contract.DepositAmount,
                ElectricityPrice = contract.ElectricityPrice,
                WaterPrice = contract.WaterPrice,
                Tenants = contract.ContractTenants.Select(ct => new TenantEditModel
                {
                    Id = ct.TenantId,
                    FullName = ct.Tenant.FullName,
                    IdentityCard = ct.Tenant.IdentityCard,
                    PhoneNumber = ct.Tenant.PhoneNumber,
                    BirthDate = ct.Tenant.BirthDate,
                    Gender = ct.Tenant.Gender,
                    PermanentAddress = ct.Tenant.PermanentAddress,
                    Photo = ct.Tenant.Photo,
                    Ethnicity = ct.Tenant.Ethnicity,
                    VehiclePlate = ct.Tenant.VehiclePlate
                }).ToList()
            };

            ViewBag.RoomList = new SelectList(db.Rooms, "Id", "Name", vm.RoomId);
            return View(vm);
        }

        // POST: Contracts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ContractEditViewModel vm, IEnumerable<HttpPostedFileBase> TenantPhotos)
        {
            var contract = db.Contracts.Include("ContractTenants.Tenant").FirstOrDefault(c => c.Id == vm.Id);
            if (contract == null)
                return HttpNotFound();

            // Cập nhật thông tin hợp đồng
            contract.MoveInDate = vm.MoveInDate;
            contract.StartDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day);
            contract.EndDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day).AddMonths(vm.Months);
            contract.PriceAgreed = vm.PriceAgreed;
            contract.DepositAmount = vm.DepositAmount;
            contract.ElectricityPrice = vm.ElectricityPrice;
            contract.WaterPrice = vm.WaterPrice;

            // Mapping tenant (thêm, xóa, cập nhật)
            var oldTenantIds = contract.ContractTenants.Select(ct => ct.TenantId).ToList();
            var newTenantIds = vm.Tenants.Where(t => t.Id > 0).Select(t => t.Id).ToList();

            // XÓA tenant đã bị loại khỏi hợp đồng
            foreach (var ct in contract.ContractTenants.Where(ct => !newTenantIds.Contains(ct.TenantId)).ToList())
            {
                var tenant = db.Tenants.Find(ct.TenantId);

                // Xóa các file ảnh cũ nếu có
                if (tenant != null && !string.IsNullOrEmpty(tenant.Photo))
                {
                    var oldPhotos = tenant.Photo.Split(';');
                    foreach (var oldPhoto in oldPhotos.Where(p => !string.IsNullOrWhiteSpace(p)))
                    {
                        try
                        {
                            var oldPath = Server.MapPath(oldPhoto);
                            if (System.IO.File.Exists(oldPath))
                            {
                                System.IO.File.Delete(oldPath);
                            }
                        }
                        catch { }
                    }
                }

                db.ContractTenants.Remove(ct);
                if (tenant != null)
                {
                    db.Tenants.Remove(tenant);
                }
            }

            var photoList = TenantPhotos?.ToList() ?? new List<HttpPostedFileBase>();
            int globalPhotoIndex = 0; // Index để theo dõi vị trí trong danh sách ảnh tổng

            // CẬP NHẬT tenant cũ và THÊM tenant mới
            foreach (var t in vm.Tenants)
            {
                string photoPaths = null;
                var tenantPhotos = new List<string>();

                // Lấy các file tương ứng với tenant hiện tại
                // Giả sử mỗi tenant có thể có tối đa 10 ảnh
                var currentTenantFiles = new List<HttpPostedFileBase>();
                for (int j = 0; j < 10 && globalPhotoIndex < photoList.Count; j++)
                {
                    var file = photoList[globalPhotoIndex];
                    if (file != null && file.ContentLength > 0)
                    {
                        currentTenantFiles.Add(file);
                    }
                    globalPhotoIndex++;
                }

                // Xử lý upload các ảnh mới
                foreach (var file in currentTenantFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                    string ext = Path.GetExtension(file.FileName);
                    string uniqueName = fileName + "_" + Guid.NewGuid().ToString("N") + ext;
                    string serverPath = Server.MapPath("~/Uploads/TenantPhotos/");
                    if (!Directory.Exists(serverPath)) Directory.CreateDirectory(serverPath);
                    string savePath = Path.Combine(serverPath, uniqueName);
                    file.SaveAs(savePath);
                    tenantPhotos.Add("/Uploads/TenantPhotos/" + uniqueName);
                }

                if (t.Id > 0)
                {
                    // Update tenant cũ
                    var tenant = db.Tenants.Find(t.Id);
                    if (tenant != null)
                    {
                        tenant.FullName = t.FullName;
                        tenant.IdentityCard = t.IdentityCard;
                        tenant.PhoneNumber = t.PhoneNumber;
                        tenant.BirthDate = t.BirthDate;
                        tenant.Gender = t.Gender;
                        tenant.PermanentAddress = t.PermanentAddress;
                        tenant.Ethnicity = t.Ethnicity;
                        tenant.VehiclePlate = t.VehiclePlate;

                        // Nếu có ảnh mới thì xóa ảnh cũ và cập nhật
                        if (tenantPhotos.Any())
                        {
                            // Xóa ảnh cũ
                            if (!string.IsNullOrEmpty(tenant.Photo))
                            {
                                var oldPhotos = tenant.Photo.Split(';');
                                foreach (var oldPhoto in oldPhotos.Where(p => !string.IsNullOrWhiteSpace(p)))
                                {
                                    try
                                    {
                                        var oldPath = Server.MapPath(oldPhoto);
                                        if (System.IO.File.Exists(oldPath))
                                        {
                                            System.IO.File.Delete(oldPath);
                                        }
                                    }
                                    catch { }
                                }
                            }

                            // Cập nhật ảnh mới
                            tenant.Photo = string.Join(";", tenantPhotos);
                        }
                        // Nếu không có ảnh mới, giữ nguyên ảnh cũ
                    }
                }
                else
                {
                    // Thêm tenant mới
                    var tenant = new Tenant
                    {
                        FullName = t.FullName,
                        IdentityCard = t.IdentityCard,
                        PhoneNumber = t.PhoneNumber,
                        BirthDate = t.BirthDate,
                        Gender = t.Gender,
                        PermanentAddress = t.PermanentAddress,
                        Photo = tenantPhotos.Any() ? string.Join(";", tenantPhotos) : null,
                        Ethnicity = t.Ethnicity,
                        VehiclePlate = t.VehiclePlate
                    };
                    db.Tenants.Add(tenant);
                    db.SaveChanges();

                    db.ContractTenants.Add(new ContractTenant
                    {
                        ContractId = contract.Id,
                        TenantId = tenant.Id,
                        CreatedAt = DateTime.Now
                    });
                }
            }

            db.SaveChanges();
            return RedirectToAction("Details", "Rooms", new { id = contract.RoomId });
        }
        [HttpPost]
        public ActionResult ExtendContract(int contractId, int extendMonths, string note)
        {
            var contract = db.Contracts.Find(contractId);
            if (contract == null || contract.Status != "Active")
                return HttpNotFound();

            if (extendMonths <= 0)
                return Json(new { success = false, message = "Số tháng gia hạn phải lớn hơn 0." });

            DateTime? oldEndDate = contract.EndDate;
            if (oldEndDate.HasValue)
                contract.EndDate = oldEndDate.Value.AddMonths(extendMonths);
            else
                contract.EndDate = DateTime.Now.AddMonths(extendMonths);

            // Lưu lịch sử gia hạn
            var log = new ContractExtensionHistory
            {
                ContractId = contract.Id,
                ExtendedAt = DateTime.Now,
                OldEndDate = oldEndDate,
                NewEndDate = contract.EndDate,
                ExtendMonths = extendMonths,
                Note = note
            };
            db.ContractExtensionHistories.Add(log);

            db.SaveChanges();
            return Json(new { success = true, newEndDate = contract.EndDate.ToString("dd/MM/yyyy") });
        }

        public ActionResult History(
                int id, int page = 1, int pageSize = 20,
                string searchName = "", string searchCard = "", string searchAddress = "",
                string sortField = "StartDate", string sortDirection = "desc",
                string fromDate = null, string toDate = null)
        {
            ViewBag.RoomId = id;
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            var query = db.Contracts
                .Where(c => c.RoomId == id)
                .Select(c => new
                {
                    RoomName = c.Room.Name,
                    Tenants = c.ContractTenants.Select(ct => ct.Tenant.FullName),
                    IdentityCards = c.ContractTenants.Select(ct => ct.Tenant.IdentityCard),
                    Genders = c.ContractTenants.Select(ct => ct.Tenant.Gender),
                    Addresses = c.ContractTenants.Select(ct => ct.Tenant.PermanentAddress),
                    PhoneNumbers = c.ContractTenants.Select(ct => ct.Tenant.PhoneNumber),
                    c.StartDate,
                    c.EndDate,
                    c.Note
                });

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(x => x.Tenants.Any(n => n.Contains(searchName)));
            if (!string.IsNullOrEmpty(searchCard))
                query = query.Where(x => x.IdentityCards.Any(ic => ic.Contains(searchCard)));
            if (!string.IsNullOrEmpty(searchAddress))
                query = query.Where(x => x.Addresses.Any(addr => addr.Contains(searchAddress)));
            if (from.HasValue)
                query = query.Where(x => x.StartDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.StartDate <= to.Value);

            switch (sortField)
            {
                case "RoomName":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.RoomName) : query.OrderByDescending(x => x.RoomName);
                    break;
                case "StartDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.StartDate) : query.OrderByDescending(x => x.StartDate);
                    break;
                case "EndDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.EndDate) : query.OrderByDescending(x => x.EndDate);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(x => x.StartDate) : query.OrderByDescending(x => x.StartDate);
                    break;
            }

            int totalItems = query.Count();

            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList()
                .Select(x => new NhaTroAnCu.Models.ContractHistoryItemViewModel
                {
                    RoomName = x.RoomName,
                    TenantNames = string.Join(", ", x.Tenants),
                    IdentityCards = string.Join(", ", x.IdentityCards),
                    Genders = string.Join(", ", x.Genders),
                    Addresses = string.Join(", ", x.Addresses),
                    PhoneNumbers = string.Join(", ", x.PhoneNumbers),
                    StartDate = x.StartDate,
                    EndDate = x.EndDate, // giữ nullable
                    Note = x.Note
                }).ToList();

            var model = new NhaTroAnCu.Models.ContractHistoryViewModel
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
                ToDate = to,
                RoomId = id
            };

            return View(model);
        }
    }

}