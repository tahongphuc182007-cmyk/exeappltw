using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyKhachSan.Repositories;

namespace QuanLyKhachSan.Views
{
    /// <summary>
    /// Màn hình chờ lúc mở ứng dụng: tự tạo/cập nhật database (xem <see cref="KhoiTaoCsdl"/>) trước khi đăng nhập.
    /// </summary>
    public class FrmKhoiDong : Form
    {
        private readonly Label lblTrangThai = new Label();
        private Exception _loi;

        public FrmKhoiDong()
        {
            Text = "Autumn Hotel";
            ClientSize = new Size(380, 110);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            ControlBox = false;
            ShowInTaskbar = true;
            Padding = new Padding(16);

            lblTrangThai.Text = "Đang chuẩn bị dữ liệu...";
            lblTrangThai.Dock = DockStyle.Top;
            lblTrangThai.Height = 40;
            lblTrangThai.TextAlign = ContentAlignment.MiddleLeft;

            var thanhChay = new ProgressBar { Dock = DockStyle.Top, Style = ProgressBarStyle.Marquee, Height = 18 };

            Controls.Add(thanhChay);
            Controls.Add(lblTrangThai);
            GiaoDien.ApDung(this);
            Shown += async (s, e) => await ChayAsync();
        }

        private async Task ChayAsync()
        {
            try
            {
                bool vuaTao = await Task.Run(() => KhoiTaoCsdl.DamBaoSanSang(Database.ChuoiKetNoi));
                if (vuaTao) lblTrangThai.Text = "Đã tạo dữ liệu.";
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                _loi = ex;
                DialogResult = DialogResult.Abort;
            }
        }

        /// <summary>Hiện màn hình chờ; trả về false (sau khi báo lỗi) nếu không chuẩn bị được database.</summary>
        public static bool ChuanBi()
        {
            using (var f = new FrmKhoiDong())
            {
                if (f.ShowDialog() == DialogResult.OK) return true;
                MessageBox.Show(MoTaLoi(f._loi), "Không mở được dữ liệu",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>Chạy không giao diện (tham số --khoi-tao): dùng cho bộ cài đặt và kiểm thử tự động.</summary>
        public static int ChayKhongGiaoDien()
        {
            try
            {
                KhoiTaoCsdl.DamBaoSanSang(Database.ChuoiKetNoi);
                return 0;
            }
            catch (Exception ex)
            {
                GhiNhatKy(ex);
                return 1;
            }
        }

        private static string MoTaLoi(Exception ex)
        {
            string duongDanLog = GhiNhatKy(ex);
            string goiY;
            try
            {
                goiY = KhoiTaoCsdl.LaLocalDb(Database.ChuoiKetNoi)
                    ? "Máy này có thể chưa cài SQL Server Express LocalDB. Hãy chạy lại bộ cài đặt Autumn Hotel "
                      + "(bộ cài sẽ tự cài LocalDB), hoặc cài SqlLocalDB.msi từ trang của Microsoft."
                    : "Kiểm tra SQL Server đang chạy và chuỗi kết nối ConnectionStrings:KhachSan trong appsettings.json.";
            }
            catch (Exception)
            {
                goiY = "Kiểm tra tệp appsettings.json bên cạnh QuanLyKhachSan.exe.";
            }

            return "Không chuẩn bị được cơ sở dữ liệu.\n\n" + goiY
                + "\n\nChi tiết: " + (ex == null ? "(không rõ)" : ex.Message)
                + (duongDanLog == null ? string.Empty : "\n\nNhật ký: " + duongDanLog);
        }

        private static string GhiNhatKy(Exception ex)
        {
            try
            {
                string thuMuc = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuanLyKhachSan");
                Directory.CreateDirectory(thuMuc);
                string tep = Path.Combine(thuMuc, "khoi-tao.log");
                File.AppendAllText(tep, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine
                    + (ex == null ? "(không rõ)" : ex.ToString()) + Environment.NewLine + Environment.NewLine);
                return tep;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
