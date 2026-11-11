using System;
using System.Windows.Forms;
using QuanLyKhachSan.Views;

namespace QuanLyKhachSan
{
    internal static class Program
    {
        /// <summary>
        /// Điểm vào ứng dụng: đăng nhập → form chính; "Đăng xuất" quay lại màn hình đăng nhập.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            // --khoi-tao: chỉ tạo/cập nhật database rồi thoát (dùng cho bộ cài đặt và kiểm thử tự động).
            if (Array.Exists(args, a => string.Equals(a, "--khoi-tao", StringComparison.OrdinalIgnoreCase)))
                return FrmKhoiDong.ChayKhongGiaoDien();

            ApplicationConfiguration.Initialize();

            // Máy mới: tự tạo database từ script nhúng trong exe, không cần chạy script bằng tay.
            if (!FrmKhoiDong.ChuanBi())
                return 1;

            while (true)
            {
                FrmMain chinh;
                using (var frmDangNhap = new FrmDangNhap())
                {
                    if (frmDangNhap.ShowDialog() != DialogResult.OK)
                        return 0;
                    chinh = new FrmMain(frmDangNhap.NguoiDungDangNhap);
                }

                using (chinh)
                {
                    Application.Run(chinh);
                    if (!chinh.DangXuat)
                        return 0;
                }
            }
        }
    }
}
