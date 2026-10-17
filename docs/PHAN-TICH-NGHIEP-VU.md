# Phân tích nghiệp vụ Nhà trọ An Cư (bản 2)

Bản MVC cũ chỉ là nháp. Bản này thiết kế lại dữ liệu và màn hình theo yêu cầu ngày 17/10/2026.
Những chỗ yêu cầu chưa nói rõ được ghi là **Giả định**; đổi được trong phần Cấu hình hoặc sửa code nếu khác ý.

## 1. Phòng trọ

- 2 dãy **A** và **B**, mỗi dãy 4 tầng (tầng trệt là tầng 0), mỗi tầng 8 phòng: tổng **64 phòng**.
- Mã phòng `{Dãy}{Tầng}.{Số}`: A0.1 … A0.8 (tầng trệt), A1.1 … A1.8, A2.1 … A2.8, A3.1 … A3.8; dãy B tương tự.
- Danh mục đồ đạc: Tủ, Giường, Điều hòa, Quạt, Bàn ghế, Đệm (thêm/bớt được).
- Mỗi dãy có **cấu hình đồ đạc mặc định** (loại và số lượng). Phòng mới lấy theo mặc định của dãy, sau đó chỉnh riêng từng phòng.
- Mỗi phòng có **giá thuê tháng** và **chỉ số công tơ điện hiện tại** (số tháng trước).
  **Giả định**: giá mặc định của mỗi dãy là 2.500.000đ, chỉnh trong Cấu hình.
- Cấu hình chung: giá điện **3.500đ/số**, nước **80.000đ/người/tháng**, ngày thu tiền **mồng 10**.

## 2. Khách thuê

### Nhận phòng (khách vào)
- Chọn phòng đang trống, nhập ngày vào, giá thuê (lấy từ phòng, sửa được), tiền cọc (mặc định = 1 tháng tiền phòng),
  số điện đầu (mặc định = số điện khi khách trước trả phòng), danh sách người ở (quét CCCD, nhiều người/phòng).
- **Tiền thu khi vào** gồm 2 khoản tách riêng:
  - **Tiền cọc** → ghi vào **tài khoản cá nhân**.
  - **Tiền phòng kỳ đầu + tiền nước kỳ đầu** → ghi vào **tài khoản hộ kinh doanh**.
- **Cách tính kỳ đầu** (theo "+/− số tiền còn phải đóng trong tháng"): kỳ đầu tính từ ngày vào đến mồng 10 tháng sau.
  Tiền phòng kỳ đầu = giá tháng + (10 − ngày vào) × giá tháng / 30. Vào trước mồng 10 thì cộng thêm, vào sau thì trừ bớt.
  Ví dụ giá 2.500.000đ, vào ngày 17: 2.500.000 − 7 × 83.333 = 1.916.667đ, đóng đến mồng 10 tháng sau.
  Tiền nước kỳ đầu tính theo cùng tỷ lệ. Tiền điện tính ở kỳ thu tiếp theo theo số công tơ.
- In **hợp đồng** từ mẫu Word, khách ký, tải **bản scan** lên phần mềm.
- Mỗi lần thu tiền có thể tải **chứng từ** (ảnh chuyển khoản, phiếu thu), không bắt buộc.

### Thu tiền hằng tháng
- Ngày 10 hằng tháng lập phiếu cho các phòng đang thuê: nhập số điện mới, phần mềm tính:
  tiền phòng (kỳ 10 → 10 tháng sau) + tiền điện ((số mới − số cũ) × 3.500) + tiền nước (số người × 80.000) ± khoản khác.
- Thu tiền vào tài khoản hộ kinh doanh, thu một phần được, theo dõi còn nợ.

### Trả phòng (khách ra)
- Chọn phòng, nhập ngày trả, **số điện cuối**, các khoản **phạt** (bẩn tường, vỡ kính…), **ảnh hiện trạng**, ghi chú.
- **Kiểm đồ đạc**: nhập số lượng thực tế từng món; phần mềm **cảnh báo** món nào khác cấu hình của phòng hoặc mặc định của dãy.
- **Quyết toán**: tiền cọc − tiền điện chưa thu − tiền phạt − các phiếu còn nợ = số trả lại khách
  (âm thì khách phải nộp thêm). Nhập số tiền thực trả/thực thu.
  **Giả định**: không hoàn lại tiền phòng đã đóng trước cho những ngày chưa ở (nhập ở "khoản khác" nếu muốn hoàn).
- Ghi sổ: tiền cọc trả lại → **chi từ tài khoản cá nhân**. Phần cọc bị khấu trừ (điện, phạt, nợ) là doanh thu nên được
  **chuyển nội bộ** từ tài khoản cá nhân sang tài khoản hộ kinh doanh, để báo cáo hộ kinh doanh đủ doanh thu.
- Chốt: hợp đồng kết thúc, phòng chuyển **trống**, số điện cuối thành số đầu cho khách sau.

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
- Báo cáo theo tháng và theo loại: tiền phòng, điện, nước, cọc, trả cọc, phạt, sửa chữa, mua sắm, quảng cáo, khác.

## 6. Quảng cáo
- Trang **giới thiệu công khai** (không cần đăng nhập): ảnh nhà trọ, bản đồ **định vị**, mô tả, số điện thoại,
  danh sách phòng đang trống kèm giá và đồ đạc.
- Quản lý ảnh, vị trí bản đồ, nội dung trong phần Quảng cáo; có **mã QR** dẫn tới trang giới thiệu để in lên phướn/băng rôn.
- Chi phí in phướn, quảng cáo ghi vào sổ chi loại "Quảng cáo".

## 7. Người dùng
- **Quản trị** (chủ nhà): toàn quyền, cấu hình, quản lý nhân viên, sửa/xóa chứng từ tiền.
- **Nhân viên**: nhận/trả phòng, thu tiền, ghi sửa chữa, mua sắm; không sửa/xóa chứng từ tiền, không đổi cấu hình.
- Tài khoản đầu tiên đăng ký là Quản trị; nhân viên do Quản trị tạo.
