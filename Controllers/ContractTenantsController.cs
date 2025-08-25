using NhaTroAnCu.Helpers;
using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace NhaTroAnCu.Controllers
{
    public class ContractTenantsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        public ActionResult TenantManager(int contractId, int roomId)
        {
            // Validate contract and room exist
            var contract = db.Contracts
                .Include(c => c.Company)
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == contractId);

            if (contract == null)
            {
                TempData["Error"] = "Không tìm thấy hợp đồng!";
                return RedirectToAction("Index", "Contracts");
            }

            // Check if room belongs to this contract
            var contractRoom = contract.ContractRooms
                .FirstOrDefault(cr => cr.RoomId == roomId);

            if (contractRoom == null)
            {
                TempData["Error"] = "Phòng không thuộc hợp đồng này!";
                return RedirectToAction("Details", "Contracts", new { id = contractId });
            }

            // Get existing tenants for this room and contract
            var contractTenants = db.ContractTenants
                .Include(ct => ct.Tenant)
                .Where(ct => ct.ContractId == contractId && ct.RoomId == roomId)
                .ToList();

            var model = new TenantManagerViewModel
            {
                ContractId = contractId,
                RoomId = roomId,
                ContractCode = $"HD-{contract.Id:D6}",
                RoomName = contractRoom.Room.Name,
                CompanyName = contract.Company?.CompanyName ?? "Cá nhân",
                CompanyId = contract.CompanyId,
                ContractTenants = contractTenants.Select(ct => new TenantViewModel
                {
                    Id = ct.Id,
                    TenantId = ct.TenantId,
                    FullName = ct.Tenant.FullName,
                    IdentityCard = ct.Tenant.IdentityCard,
                    PhoneNumber = ct.Tenant.PhoneNumber,
                    Gender = ct.Tenant.Gender,
                    BirthDate = ct.Tenant.BirthDate,
                    PermanentAddress = ct.Tenant.PermanentAddress,
                    Ethnicity = ct.Tenant.Ethnicity,
                    VehiclePlate = ct.Tenant.VehiclePlate,
                    Photo = ct.Tenant.Photo,
                    CreatedAt = ct.CreatedAt
                }).ToList()
            };

            return View(model);
        }

        // POST: TenantContracts/SaveTenants
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveTenants(TenantManagerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadViewData(model);
                return View("TenantManager", model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var contract = db.Contracts
                        .Include(c => c.ContractRooms)
                        .FirstOrDefault(c => c.Id == model.ContractId);

                    if (contract == null)
                    {
                        throw new InvalidOperationException("Không tìm thấy hợp đồng");
                    }

                    // Validate room belongs to contract
                    if (!contract.ContractRooms.Any(cr => cr.RoomId == model.RoomId))
                    {
                        throw new InvalidOperationException("Phòng không thuộc hợp đồng này");
                    }

                    // Convert Tenants to TenantViewModel for processing
                    var tenantViewModels = model.Tenants?.Select(t => new TenantViewModel
                    {
                        TenantId = t.Id,
                        FullName = t.FullName,
                        IdentityCard = t.IdentityCard,
                        PhoneNumber = t.PhoneNumber,
                        BirthDate = t.BirthDate,
                        Gender = t.Gender,
                        PermanentAddress = t.PermanentAddress,
                        Ethnicity = t.Ethnicity,
                        VehiclePlate = t.VehiclePlate,
                        Photo = t.Photo
                    }).ToList();

                    // Process tenants based on contract type
                    if (contract.ContractType == "Company")
                    {
                        // Với hợp đồng công ty, truyền roomId cụ thể
                        TenantContractHelper.ProcessCompanyContractTenants(
                            db,
                            contract,
                            model.RoomId,
                            tenantViewModels,
                            Request,
                            true // isEdit mode
                        );
                    }
                    else
                    {
                        // Với hợp đồng cá nhân, không cần roomId vì chỉ có 1 phòng
                        TenantContractHelper.ProcessIndividualContractTenants(
                            db,
                            contract,
                            tenantViewModels,
                            Request,
                            true // isEdit mode
                        );
                    }

                    transaction.Commit();
                    TempData["Success"] = "Đã cập nhật thông tin người thuê thành công!";
                    return RedirectToAction("Details", "Rooms", new { id = model.RoomId });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                    LoadViewData(model);
                    return View("TenantManager", model);
                }
            }
        }

        private void LoadViewData(TenantManagerViewModel model)
        {
            // Reload contract and room info for display
            var contract = db.Contracts
                .Include(c => c.Company)
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == model.ContractId);

            if (contract != null)
            {
                var room = contract.ContractRooms
                    .FirstOrDefault(cr => cr.RoomId == model.RoomId)?.Room;

                if (room != null)
                {
                    model.ContractCode = $"HD-{contract.Id:D6}";
                    model.RoomName = room.Name;
                    model.CompanyName = contract.Company?.CompanyName ?? "Cá nhân";
                    model.CompanyId = contract.CompanyId;
                }
            }
        }

        // POST: TenantContracts/RemoveTenant
        [HttpPost]
        public ActionResult RemoveTenant(int contractTenantId)
        {
            try
            {
                var contractTenant = db.ContractTenants.Find(contractTenantId);
                if (contractTenant == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người thuê" });
                }

                db.ContractTenants.Remove(contractTenant);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa người thuê" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: TenantContracts/GetTenants
        public ActionResult GetTenants(int contractId, int roomId)
        {
            var contractTenants = db.ContractTenants
                .Include(ct => ct.Tenant)
                .Where(ct => ct.ContractId == contractId && ct.RoomId == roomId)
                .Select(ct => new
                {
                    id = ct.Id,
                    tenantId = ct.TenantId,
                    contractTenantId = ct.Id,
                    fullName = ct.Tenant.FullName,
                    identityCard = ct.Tenant.IdentityCard,
                    phoneNumber = ct.Tenant.PhoneNumber,
                    gender = ct.Tenant.Gender,
                    birthDate = ct.Tenant.BirthDate,
                    permanentAddress = ct.Tenant.PermanentAddress,
                    ethnicity = ct.Tenant.Ethnicity,
                    vehiclePlate = ct.Tenant.VehiclePlate,
                    photo = ct.Tenant.Photo,
                    createdAt = ct.CreatedAt
                })
                .ToList();

            return Json(contractTenants, JsonRequestBehavior.AllowGet);
        }

        // GET: TenantContracts/GetRoomContractInfo
        public ActionResult GetRoomContractInfo(int contractId, int roomId)
        {
            var contract = db.Contracts
                .Include(c => c.Company)
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == contractId);

            if (contract == null)
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }

            var room = contract.ContractRooms
                .FirstOrDefault(cr => cr.RoomId == roomId)?.Room;

            if (room == null)
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                roomName = room.Name,
                contractCode = $"HD-{contract.Id:D6}",
                companyName = contract.Company?.CompanyName ?? "Cá nhân"
            }, JsonRequestBehavior.AllowGet);
        }

        private void UpdateTenantInfo(Tenant tenant, TenantDataViewModel data)
        {
            tenant.FullName = data.FullName;
            tenant.IdentityCard = data.IdentityCard;
            tenant.PhoneNumber = data.PhoneNumber;
            tenant.Gender = data.Gender;
            tenant.BirthDate = data.BirthDate;
            tenant.PermanentAddress = data.PermanentAddress;
            tenant.Ethnicity = data.Ethnicity;
            tenant.VehiclePlate = data.VehiclePlate;

            // Handle photo if provided (base64 or file path)
            if (!string.IsNullOrEmpty(data.Photo))
            {
                tenant.Photo = data.Photo;
            }
        }


        // GET: ContractTenants
        public ActionResult Index(string searchName, string searchCard, string searchRoom,
             string filterStatus, string filterCompany, string filterContractType)
        {
            var query = db.ContractTenants
                .Include(ct => ct.Tenant)
                .Include(ct => ct.Room)
                .Include(ct => ct.Contract)
                .Include(ct => ct.Contract.Company)
                .AsQueryable();

            // Lọc theo trạng thái hợp đồng
            if (!string.IsNullOrEmpty(filterStatus))
            {
                query = query.Where(ct => ct.Contract.Status == filterStatus);
            }
            else
            {
                // Mặc định chỉ hiển thị hợp đồng Active
                query = query.Where(ct => ct.Contract.Status == "Active");
            }

            // Lọc theo loại hợp đồng
            if (!string.IsNullOrEmpty(filterContractType))
            {
                query = query.Where(ct => ct.Contract.ContractType == filterContractType);
            }

            // Lọc theo công ty (cho hợp đồng công ty)
            if (!string.IsNullOrEmpty(filterCompany))
            {
                int companyId = int.Parse(filterCompany);
                query = query.Where(ct => ct.Contract.CompanyId == companyId);
            }

            // Tìm kiếm
            if (!string.IsNullOrEmpty(searchName))
            {
                query = query.Where(ct => ct.Tenant.FullName.Contains(searchName));
            }
            if (!string.IsNullOrEmpty(searchCard))
            {
                query = query.Where(ct => ct.Tenant.IdentityCard.Contains(searchCard));
            }
            if (!string.IsNullOrEmpty(searchRoom))
            {
                query = query.Where(ct => ct.Room.Name.Contains(searchRoom));
            }

            var result = query
                .OrderBy(ct => ct.Room.Name)
                .ThenBy(ct => ct.Tenant.FullName)
                .Select(ct => new TenantReportViewModel
                {
                    Id = ct.Id,
                    TenantId = ct.TenantId,
                    TenantName = ct.Tenant.FullName,
                    IdentityCard = ct.Tenant.IdentityCard,
                    PhoneNumber = ct.Tenant.PhoneNumber,
                    BirthDate = ct.Tenant.BirthDate,
                    Gender = ct.Tenant.Gender,
                    Ethnicity = ct.Tenant.Ethnicity,
                    PermanentAddress = ct.Tenant.PermanentAddress,
                    VehiclePlate = ct.Tenant.VehiclePlate,
                    Photo = ct.Tenant.Photo,

                    RoomId = ct.RoomId,
                    RoomName = ct.Room.Name,

                    ContractId = ct.ContractId,
                    ContractType = ct.Contract.ContractType,
                    ContractStatus = ct.Contract.Status,
                    StartDate = ct.Contract.StartDate,
                    EndDate = ct.Contract.EndDate,
                    MoveInDate = ct.Contract.MoveInDate,

                    CompanyId = ct.Contract.CompanyId,
                    CompanyName = ct.Contract.Company != null ? ct.Contract.Company.CompanyName : null,

                })
                .ToList();

            // Chuẩn bị dữ liệu cho filters
            ViewBag.Companies = db.Companies
                .OrderBy(c => c.CompanyName)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.CompanyName
                })
                .ToList();

            ViewBag.ContractTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Individual", Text = "Cá nhân/Hộ gia đình" },
                new SelectListItem { Value = "Company", Text = "Công ty" }
            };

            ViewBag.Statuses = new List<SelectListItem>
            {
                new SelectListItem { Value = "Active", Text = "Đang ở" },
                new SelectListItem { Value = "Expired", Text = "Hết hạn" },
                new SelectListItem { Value = "Terminated", Text = "Đã kết thúc" }
            };

            // Truyền các giá trị filter để maintain state
            ViewBag.SearchName = searchName;
            ViewBag.SearchCard = searchCard;
            ViewBag.SearchRoom = searchRoom;
            ViewBag.FilterStatus = filterStatus;
            ViewBag.FilterCompany = filterCompany;
            ViewBag.FilterContractType = filterContractType;

            // Thống kê
            ViewBag.TotalTenants = result.Count;
            ViewBag.TotalRooms = result.Select(r => r.RoomId).Distinct().Count();
            ViewBag.TotalCompanies = result.Where(r => r.CompanyId.HasValue)
                .Select(r => r.CompanyId).Distinct().Count();

            return View(result);
        }

        // GET: ContractTenants/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            if (contractTenant == null)
            {
                return HttpNotFound();
            }
            return View(contractTenant);
        }

        // GET: ContractTenants/Create
        public ActionResult Create()
        {
            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status");
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name");
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName");
            return View();
        }

        // POST: ContractTenants/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,RoomId,TenantId,ContractId,CreatedAt")] ContractTenant contractTenant)
        {
            if (ModelState.IsValid)
            {
                db.ContractTenants.Add(contractTenant);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status", contractTenant.ContractId);
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name", contractTenant.RoomId);
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName", contractTenant.TenantId);
            return View(contractTenant);
        }

        // GET: ContractTenants/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            if (contractTenant == null)
            {
                return HttpNotFound();
            }
            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status", contractTenant.ContractId);
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name", contractTenant.RoomId);
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName", contractTenant.TenantId);
            return View(contractTenant);
        }

        // POST: ContractTenants/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,RoomId,TenantId,ContractId,CreatedAt")] ContractTenant contractTenant)
        {
            if (ModelState.IsValid)
            {
                db.Entry(contractTenant).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status", contractTenant.ContractId);
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name", contractTenant.RoomId);
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName", contractTenant.TenantId);
            return View(contractTenant);
        }

        // GET: ContractTenants/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            if (contractTenant == null)
            {
                return HttpNotFound();
            }
            return View(contractTenant);
        }

        // POST: ContractTenants/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            db.ContractTenants.Remove(contractTenant);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        // GET: ContractTenants/Export - Xuất báo cáo Excel
        // GET: ContractTenants/Export - Xuất báo cáo PDF
        public ActionResult Export(string searchName, string searchCard, string searchRoom,
            string filterStatus, string filterCompany, string filterContractType)
        {
            try
            {
                // Build query giống như trong Index
                var query = from ct in db.ContractTenants
                            join t in db.Tenants on ct.TenantId equals t.Id
                            join r in db.Rooms on ct.RoomId equals r.Id
                            join c in db.Contracts on ct.ContractId equals c.Id
                            join cr in db.ContractRooms on new { ct.ContractId, ct.RoomId }
                                equals new { cr.ContractId, cr.RoomId }
                            select new TenantReportViewModel
                            {
                                Id = ct.Id,
                                TenantId = t.Id,
                                TenantName = t.FullName,
                                IdentityCard = t.IdentityCard,
                                PhoneNumber = t.PhoneNumber,
                                BirthDate = t.BirthDate,
                                Gender = t.Gender,
                                Ethnicity = t.Ethnicity,
                                PermanentAddress = t.PermanentAddress,
                                VehiclePlate = t.VehiclePlate,
                                Photo = t.Photo,
                                RoomId = r.Id,
                                RoomName = r.Name,
                                ContractId = c.Id,
                                ContractType = c.ContractType,
                                ContractStatus = c.Status,
                                MoveInDate = c.MoveInDate,
                                CompanyId = c.CompanyId,
                                CompanyName = c.Company.CompanyName
                            };

                // Apply filters - nếu filterStatus rỗng thì mặc định lấy Active
                if (string.IsNullOrEmpty(filterStatus))
                {
                    query = query.Where(x => x.ContractStatus == "Active");
                }
                else
                {
                    query = query.Where(x => x.ContractStatus == filterStatus);
                }

                // Apply other filters
                if (!string.IsNullOrEmpty(searchName))
                {
                    query = query.Where(x => x.TenantName.Contains(searchName));
                }

                if (!string.IsNullOrEmpty(searchCard))
                {
                    query = query.Where(x => x.IdentityCard.Contains(searchCard));
                }

                if (!string.IsNullOrEmpty(searchRoom))
                {
                    query = query.Where(x => x.RoomName.Contains(searchRoom));
                }

                if (!string.IsNullOrEmpty(filterContractType))
                {
                    query = query.Where(x => x.ContractType == filterContractType);
                }

                if (!string.IsNullOrEmpty(filterCompany))
                {
                    int companyId = int.Parse(filterCompany);
                    query = query.Where(x => x.CompanyId == companyId);
                }

                var data = query.OrderBy(x => x.RoomName).ThenBy(x => x.TenantName).ToList();

                // Tạo PDF document sử dụng iTextSharp
                using (var memoryStream = new MemoryStream())
                {
                    // Khởi tạo document
                    var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4.Rotate(), 25, 25, 30, 30);
                    var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, memoryStream);

                    // Thêm metadata
                    document.AddAuthor("Nhà Trọ An Cư");
                    document.AddCreator("Hệ thống quản lý nhà trọ");
                    document.AddSubject("Danh sách khách thuê");
                    document.AddTitle("Báo cáo danh sách khách thuê");

                    document.Open();

                    // Font tiếng Việt
                    string fontPath = Server.MapPath("~/Fonts/times.ttf");
                    var baseFont = iTextSharp.text.pdf.BaseFont.CreateFont(fontPath,
                        iTextSharp.text.pdf.BaseFont.IDENTITY_H,
                        iTextSharp.text.pdf.BaseFont.EMBEDDED);

                    var titleFont = new iTextSharp.text.Font(baseFont, 18, iTextSharp.text.Font.BOLD);
                    var headerFont = new iTextSharp.text.Font(baseFont, 11, iTextSharp.text.Font.BOLD);
                    var normalFont = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.NORMAL);
                    var boldFont = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.BOLD);
                    var smallFont = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL);

                    // Title
                    var title = new iTextSharp.text.Paragraph("DANH SÁCH KHÁCH THUÊ", titleFont);
                    title.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                    title.SpacingAfter = 10f;
                    document.Add(title);

                    // Thông tin xuất
                    var exportInfo = new iTextSharp.text.Paragraph($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}", normalFont);
                    exportInfo.Alignment = iTextSharp.text.Element.ALIGN_RIGHT;
                    exportInfo.SpacingAfter = 5f;
                    document.Add(exportInfo);

                    if (!string.IsNullOrEmpty(filterStatus))
                    {
                        var statusText = filterStatus == "Active" ? "Đang ở" :
                                        filterStatus == "Expired" ? "Hết hạn" : "Đã kết thúc";
                        var statusInfo = new iTextSharp.text.Paragraph($"Trạng thái: {statusText}", normalFont);
                        statusInfo.Alignment = iTextSharp.text.Element.ALIGN_RIGHT;
                        statusInfo.SpacingAfter = 10f;
                        document.Add(statusInfo);
                    }

                    // Tạo bảng
                    var table = new iTextSharp.text.pdf.PdfPTable(12);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 3f, 12f, 10f, 10f, 8f, 5f, 8f, 8f, 12f, 8f, 8f, 8f });
                    table.SpacingBefore = 10f;

                    // Header row
                    AddTableHeader(table, "STT", headerFont);
                    AddTableHeader(table, "Họ tên", headerFont);
                    AddTableHeader(table, "CCCD", headerFont);
                    AddTableHeader(table, "Điện thoại", headerFont);
                    AddTableHeader(table, "Ngày sinh", headerFont);
                    AddTableHeader(table, "Giới tính", headerFont);
                    AddTableHeader(table, "Phòng", headerFont);
                    AddTableHeader(table, "Loại HĐ", headerFont);
                    AddTableHeader(table, "Công ty", headerFont);
                    AddTableHeader(table, "Trạng thái", headerFont);
                    AddTableHeader(table, "Ngày vào", headerFont);
                    AddTableHeader(table, "Chính", headerFont);

                    // Data rows
                    int stt = 1;
                    foreach (var item in data)
                    {
                        AddTableCell(table, stt++.ToString(), smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.TenantName, smallFont, iTextSharp.text.Element.ALIGN_LEFT);
                        AddTableCell(table, item.IdentityCard, smallFont, iTextSharp.text.Element.ALIGN_LEFT);
                        AddTableCell(table, item.PhoneNumber, smallFont, iTextSharp.text.Element.ALIGN_LEFT);
                        AddTableCell(table, item.BirthDate?.ToString("dd/MM/yyyy") ?? "", smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.Gender ?? "", smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.RoomName, smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.ContractType == "Individual" ? "Cá nhân" : "Công ty", smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.CompanyName ?? "-", smallFont, iTextSharp.text.Element.ALIGN_LEFT);

                        string status = item.ContractStatus == "Active" ? "Đang ở" :
                                       item.ContractStatus == "Expired" ? "Hết hạn" : "Kết thúc";
                        AddTableCell(table, status, smallFont, iTextSharp.text.Element.ALIGN_CENTER);

                        AddTableCell(table, item.MoveInDate?.ToString("dd/MM/yyyy") ?? "", smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.IsPrimary ? "✓" : "", smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                    }

                    document.Add(table);

                    // Đường kẻ
                    document.Add(new iTextSharp.text.Paragraph(" "));
                    var line = new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, iTextSharp.text.BaseColor.GRAY,
                        iTextSharp.text.Element.ALIGN_CENTER, -2);
                    document.Add(new iTextSharp.text.Chunk(line));
                    document.Add(new iTextSharp.text.Paragraph(" "));

                    // Footer với thống kê
                    var footer = new iTextSharp.text.Paragraph("THỐNG KÊ:", boldFont);
                    footer.SpacingAfter = 5f;
                    document.Add(footer);

                    var stats = new iTextSharp.text.Paragraph();
                    stats.Add(new iTextSharp.text.Chunk($"- Tổng số người: {data.Count}\n", normalFont));
                    stats.Add(new iTextSharp.text.Chunk($"- Số phòng: {data.Select(x => x.RoomId).Distinct().Count()}\n", normalFont));

                    if (data.Any(x => x.ContractType == "Company"))
                    {
                        stats.Add(new iTextSharp.text.Chunk($"- Số công ty: {data.Where(x => x.CompanyId.HasValue).Select(x => x.CompanyId).Distinct().Count()}", normalFont));
                    }
                    document.Add(stats);

                    document.Close();
                    writer.Close();

                    // Trả về file PDF
                    byte[] bytes = memoryStream.ToArray();
                    var fileName = $"DanhSachKhachThue_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    return File(bytes, "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi xuất file: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // Helper methods cho bảng PDF
        private void AddTableHeader(iTextSharp.text.pdf.PdfPTable table, string text, iTextSharp.text.Font font)
        {
            var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(text, font));
            cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
            cell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
            cell.BackgroundColor = new iTextSharp.text.BaseColor(230, 230, 230);
            cell.Padding = 5;
            table.AddCell(cell);
        }

        private void AddTableCell(iTextSharp.text.pdf.PdfPTable table, string text, iTextSharp.text.Font font, int alignment)
        {
            var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(text, font));
            cell.HorizontalAlignment = alignment;
            cell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
            cell.Padding = 4;
            table.AddCell(cell);
        }
    }
}
