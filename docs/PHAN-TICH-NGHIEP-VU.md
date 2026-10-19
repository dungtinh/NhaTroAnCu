# Phân tích nghiệp vụ Nhà trọ An Cư (bản 2)

Bản MVC cũ chỉ là nháp. Bản này thiết kế lại dữ liệu và màn hình theo yêu cầu ngày 17/10/2026.
Những chỗ yêu cầu chưa nói rõ được ghi là **Giả định**; đổi được trong phần Cấu hình hoặc sửa code nếu khác ý.

## 1. Phòng trọ

- 2 dãy **A** và **B**, mỗi dãy 4 tầng (tầng trệt là tầng 0), mỗi tầng 8 phòng: tổng **64 phòng**.
- Mã phòng `{Dãy}{Tầng}.{Số}`: A0.1 … A0.8 (tầng trệt), A1.1 … A1.8, A2.1 … A2.8, A3.1 … A3.8; dãy B tương tự.
- Danh mục đồ đạc: Tủ, Giường, Điều hòa, Quạt, Bàn ghế, Đệm (thêm/bớt được).
- Mỗi dãy có **cấu hình đồ đạc mặc định** (loại và số lượng). Phòng mới lấy theo mặc định của dãy, sau đó chỉnh riêng từng phòng.
- Mỗi phòng có **giá thuê tháng**. **Giả định**: giá mặc định của mỗi dãy là 2.500.000đ, chỉnh trong Cấu hình.
- **Điện**: công tơ điện tử do chủ nhà quản lý tập trung, không gắn công tơ từng phòng cho khách xem. Phần mềm **không lưu
  chỉ số đầu/cuối kỳ**, mỗi kỳ chỉ nhập **số điện đã dùng (kWh)** của từng phòng; khách đồng ý không cần xem công tơ.
- Cấu hình chung: giá điện **3.500đ/số**, nước **80.000đ/người/tháng** (cố định, không theo ngày), ngày thu tiền **mồng 10**,
  **tiền cọc mặc định 1.000.000đ** cho mọi khách (sửa được từng hợp đồng).

## 2. Khách thuê

### Nhận phòng (khách vào)
- Màn hình 3 bước (cập nhật 19/10/2026): **Phòng** → **Khách** → **Tiền & xác nhận**, mỗi bước một màn hình ngắn, thanh nút
  "Tiếp"/"Nhận phòng" dính đáy trên điện thoại.
  - Bước Phòng: chọn phòng trống, ngày vào, giá thuê (lấy từ phòng, sửa được và cập nhật lại giá phòng), tiền cọc (mặc định
    1.000.000đ theo Cấu hình), **kiểm và sửa số lượng đồ đạc thực tế** (lưu vào cấu hình phòng), tối đa số người, ghi chú.
  - Bước Khách: nhiều người/phòng; mỗi người **chụp CCCD 2 mặt hoặc chọn ảnh có sẵn**, **ảnh chân dung** (chụp bằng camera trước
    hoặc chọn ảnh), quét CCCD bằng AI để tự điền. **Không nhập số điện.**
  - Bước Tiền: xem lại phòng/khách, bảng tiền thu khi vào, công thức kỳ chốt đầu, nộp đủ hay nộp thiếu, chứng từ.
- **Tiền thu khi vào** gồm 2 khoản tách riêng:
  - **Tiền cọc** 1.000.000đ → ghi vào **tài khoản cá nhân**.
  - **Đủ 1 tháng tiền phòng** (trả trước) → ghi vào **tài khoản hộ kinh doanh**. Không thu nước, điện lúc vào.
  - Ví dụ giá 2.000.000đ: khách nộp 2.000.000 + 1.000.000 = **3.000.000đ**.
- Tháng tiền phòng trả trước này được tính cho **kỳ tròn tháng đầu tiên** tính từ ngày chốt đầu (xem bên dưới).
- In **hợp đồng** từ mẫu Word, khách ký, tải **bản scan** lên phần mềm.
- Mỗi lần thu tiền có thể tải **chứng từ** (ảnh chuyển khoản, phiếu thu), không bắt buộc.

### Thu tiền hằng tháng (ngày chốt mồng 10)
- **Kỳ chốt đầu** của một khách là **mồng 10 gần nhất sau ngày vào** (cập nhật 19/10/2026 theo yêu cầu):
  vào trước mồng 10 → chốt ngay mồng 10 tháng đó (vào 01/09 → chốt 10/09); vào sau mồng 10 → mồng 10 tháng sau (vào 20/09 → chốt 10/10).
  Phiếu kỳ chốt đầu gồm:
  - **Tiền phòng từ ngày vào đến ngày chốt** (đoạn lẻ; tháng trả trước lúc vào bao kỳ tròn tháng ngay sau ngày chốt đầu).
    Tính theo tháng 30 ngày: vào trước mồng 10 số ngày = 10 − ngày vào; vào sau mồng 10 số ngày = 30 + 10 − ngày vào;
    tiền = giá tháng × số ngày / 30, làm tròn nghìn. Phiếu và màn hình ghi rõ **số ngày × giá ÷ 30**.
    Giá 2.000.000: vào 01/09 → 9 ngày → 600.000đ, thu ngày 10/09. Vào 20/09 → 20 ngày → 1.333.000đ, thu ngày 10/10.
    Vào đúng mồng 10: thu như bình thường, không thêm bớt gì: khi vào nộp 1 tháng + cọc (tháng trả trước tính từ ngày vào),
    từ mồng 10 tháng sau thu đủ tháng + điện + nước (xác nhận của chủ nhà 19/10/2026).
  - **Tiền điện** = số điện đã dùng (nhập) × 3.500.
  - **Tiền nước** = số người × 80.000 (đủ tháng, không chia ngày).
- **Các kỳ sau**: tiền phòng **đủ tháng** (2.000.000) + điện theo số đã dùng + nước cố định ± khoản khác.
  Phiếu kỳ 10/M tính tiền phòng cho 10/M → 10/(M+1), tức khách luôn **đóng trước một tháng**.
  Ví dụ vào 01/09: nộp 3.000.000 khi vào (2.000.000 cho 10/09 → 10/10 + cọc); 10/09 nộp 600.000 + điện + nước; 10/10 nộp 2.000.000 + điện + nước.
  Vào 20/09: nộp 3.000.000 khi vào (2.000.000 cho 10/10 → 10/11 + cọc); 10/10 nộp 1.333.000 + điện + nước; 10/11 nộp 2.000.000 + điện + nước.
- Màn hình **Thu tiền tháng** liệt kê **tất cả phòng đang có khách**, thứ tự: **chưa thu trước, đã thu sau**; trong mỗi nhóm
  theo **dãy A → B, tầng thấp → cao, số phòng bé → to**. Mỗi dòng chỉ cần **nhập số điện đã dùng**; tiền phòng, nước, điện tự tính
  theo cấu hình. Có nút **Lập phiếu / Lưu**, **Thu tiền** (kèm chứng từ), **In phiếu thu**; trạng thái **Đã thu / Chưa thu**.
- Phòng nhận trong chính tháng đó chưa đến kỳ chốt đầu nên không hiện (có thể bật "Hiện cả phòng không cần lập").
- **Ghi số điện** là bước riêng (trang Ghi số điện, cập nhật 20/10/2026): bảng mọi phòng cần thu kỳ này theo dãy, tầng;
  gõ số điện đã dùng từng phòng (Enter xuống phòng sau) hoặc **nhập từ file Excel** (tải file mẫu, điền cột "Số điện (kWh)").
  Lưu thì lập/cập nhật phiếu tháng. Màn hình Thu tiền tháng chỉ còn thu tiền, sửa phiếu, in phiếu.
- **Tiền điện, nước tổng** của cả dãy trọ (hóa đơn điện lực, nước sạch) nhập mỗi kỳ ở trang Ghi số điện: tổng số điện (kWh),
  tiền điện, số nước (m³), tiền nước, ngày trả, tài khoản chi (mặc định hộ kinh doanh), chứng từ. Lưu thì ghi 2 khoản chi
  "Tiền điện", "Tiền nước" vào sổ thu chi; có bảng so sánh số điện, tiền điện, nước thu của khách với số trả tổng.
- Thu tiền vào tài khoản hộ kinh doanh, thu một phần được, theo dõi còn nợ.

### Trả phòng (khách ra)
- Chọn phòng, nhập ngày trả, **số điện đã dùng** từ kỳ chốt trước, các khoản **phạt** (bẩn tường, vỡ kính…), **ảnh hiện trạng**, ghi chú.
- **Kiểm đồ đạc**: nhập số lượng thực tế từng món; phần mềm **cảnh báo** món nào khác cấu hình của phòng hoặc mặc định của dãy.
- **Tiền phòng và tiền nước tính đủ tháng**: kỳ chốt nào đã tới ngày thu mà chưa lập phiếu thì tính **trọn tháng** tiền phòng
  và tiền nước vào phiếu trả phòng (không chia ngày); kỳ đã lập phiếu thì chỉ lấy phần còn nợ. Không hoàn tiền phòng đã đóng
  trước cho những ngày chưa ở (muốn hoàn thì nhập âm ở "khoản khác").
- **Chỉ trả lại cọc khi khách ở đủ 3 tháng trở lên** (Cấu hình "Ở đủ mấy tháng mới trả lại cọc", mặc định 3; cập nhật 19/10/2026).
  Ở ít hơn thì **cọc không hoàn trả**: ghi là doanh thu hộ kinh doanh (chuyển nội bộ từ TK cá nhân), các khoản điện, phạt, nợ khách vẫn
  phải nộp thêm. Chủ nhà vẫn sửa tay được số trả cọc nếu muốn châm chước.
- **Quyết toán**: tiền cọc (0 nếu không hoàn trả) − tiền điện − tiền phạt − tiền phòng/nước kỳ chưa lập − các phiếu còn nợ − khoản khác = số trả lại khách
  (âm thì khách phải nộp thêm). Nhập số tiền thực trả/thực thu.
- Ghi sổ: tiền cọc trả lại → **chi từ tài khoản cá nhân**. Phần cọc bị khấu trừ (điện, phạt, nợ) là doanh thu nên được
  **chuyển nội bộ** từ tài khoản cá nhân sang tài khoản hộ kinh doanh, để báo cáo hộ kinh doanh đủ doanh thu.
- Chốt: hợp đồng kết thúc, phòng chuyển **trống**.

## 3. Sửa chữa
- Chọn **phòng** hoặc **khu vực chung** (ghi rõ vị trí), nội dung sửa, người/thợ thực hiện, số tiền.
- **Bắt buộc** có ảnh (trước và/hoặc sau khi sửa) và **chứng từ chuyển khoản**.
- Tiền sửa **trừ thẳng vào tài khoản cá nhân** (đổi được tài khoản nếu cần).
- Chống gian lận: nhân viên chỉ thêm mới, không sửa/xóa được; mọi phiếu ghi người tạo và thời gian; chủ nhà (Quản trị)
  mới sửa/xóa được. Màn hình lịch sử sửa chữa theo phòng giúp phát hiện sửa lặp lại.

## 4. Mua sắm
- Nội dung, số tiền, tài khoản chi (mặc định tài khoản cá nhân), **ảnh chứng từ bắt buộc**, gắn với phòng nếu có.

## 5. Tài khoản và báo cáo
- 2 tài khoản: **Cá nhân** và **Hộ kinh doanh**, mỗi tài khoản có **số dư đầu kỳ** và ngày đầu kỳ.
- Mọi khoản thu/chi ghi vào một tài khoản; số dư hiện tại = đầu kỳ + thu − chi. Chuyển tiền giữa 2 tài khoản được ghi 2 dòng.
- Báo cáo theo **tháng, quý, năm**: doanh thu, chi phí, lãi (không tính chuyển nội bộ và cọc giữ hộ), bảng và biểu đồ từng tháng,
  theo loại (tiền phòng, điện, nước, cọc, trả cọc, phạt, sửa chữa, mua sắm, quảng cáo, khác), điện nước tính cho khách so với trả tổng,
  số dư từng tài khoản đầu/cuối kỳ, doanh thu theo phòng.

## 6. Quảng cáo
- Trang **giới thiệu công khai** (không cần đăng nhập): ảnh nhà trọ, bản đồ **định vị**, mô tả, số điện thoại,
  danh sách phòng đang trống kèm giá và đồ đạc.
- Quản lý ảnh, vị trí bản đồ, nội dung trong phần Quảng cáo; có **mã QR** dẫn tới trang giới thiệu để in lên phướn/băng rôn.
- Chi phí in phướn, quảng cáo ghi vào sổ chi loại "Quảng cáo".

## 7. Người dùng
- **Quản trị** (chủ nhà): toàn quyền, cấu hình, quản lý nhân viên, sửa/xóa chứng từ tiền.
- **Nhân viên**: nhận/trả phòng, thu tiền, ghi sửa chữa, mua sắm; không sửa/xóa chứng từ tiền, không đổi cấu hình.
- Tài khoản đầu tiên đăng ký là Quản trị; nhân viên do Quản trị tạo.

## Rà soát luồng vào/ra (18/10/2026, cập nhật 19/10/2026 theo cách thu mới)

Các kiểm tra phần mềm tự chặn hoặc cảnh báo:

- **Nhận phòng**: không nhận vào phòng đang có khách hoặc đang sửa; khách đang ở phòng khác phải trả phòng cũ trước;
  ngày vào không quá 31 ngày tới hoặc 2 năm trước; tiền cọc, tiền phòng đã thu không được lớn hơn số phải thu.
- **Thu tiền**: phải nhập số điện đã dùng (0 cũng được) mới lập được phiếu; không thu quá số còn phải thu của phiếu
  (khách đưa thừa thì ghi ở Thu chi > Thu khác); phiếu đã đủ thì không thu thêm.
- **Trả phòng**: ngày trả không trước ngày vào và không quá 7 ngày tới; cọc dùng để quyết toán là **cọc đã thu thật**
  (không phải số ghi trên hợp đồng); trả cọc không quá số đang giữ; kỳ đã đến ngày thu mà chưa lập phiếu được **tính đủ tháng**
  ngay trong phiếu trả phòng (không còn cảnh báo bỏ sót).
- **Giới hạn đã biết**: nợ còn lại của hợp đồng đã trả phòng (khi cố tình trả phòng mà khách chưa nộp đủ) không hiện trên sơ đồ,
  chỉ xem được ở trang hợp đồng/phòng. Không hoàn tiền phòng đã thu khi khách đi giữa kỳ.
