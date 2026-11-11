using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace QuanLyKhachSan.Repositories
{
    /// <summary>
    /// Tự tạo hoặc cập nhật database khi ứng dụng khởi động, để máy mới chỉ cần cài đặt rồi chạy.
    /// Hai script 01_CreateDatabase.sql và 02_InsertSampleData.sql được nhúng vào exe (EmbeddedResource)
    /// và đều chạy lại được, nên chỉ cần chạy khi database chưa có hoặc script đã đổi so với lần chạy trước.
    /// Với LocalDB, tệp .mdf/.ldf nằm ở %LocalAppData%\QuanLyKhachSan\Data.
    /// </summary>
    public static class KhoiTaoCsdl
    {
        public const string TenCsdl = "QuanLyKhachSan";
        private const string TenThuocTinhPhienBan = "PhienBanKichBan";
        private static readonly string[] KichBan = { "01_CreateDatabase.sql", "02_InsertSampleData.sql" };

        /// <summary>Thư mục chứa tệp dữ liệu khi dùng LocalDB.</summary>
        public static string ThuMucDuLieu
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "QuanLyKhachSan", "Data");
            }
        }

        public static bool LaLocalDb(string chuoiKetNoi)
        {
            var b = new SqlConnectionStringBuilder(chuoiKetNoi);
            return (b.DataSource ?? string.Empty).TrimStart().StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Bảo đảm database tồn tại và đúng phiên bản script. Ném ngoại lệ nếu không kết nối được SQL Server.
        /// Trả về true nếu vừa tạo mới hoặc cập nhật cấu trúc.
        /// </summary>
        public static bool DamBaoSanSang(string chuoiKetNoi)
        {
            var b = new SqlConnectionStringBuilder(chuoiKetNoi);
            string tenCsdl = string.IsNullOrWhiteSpace(b.InitialCatalog) ? TenCsdl : b.InitialCatalog;
            if (!string.Equals(tenCsdl, TenCsdl, StringComparison.OrdinalIgnoreCase))
                return false; // Script ghi cứng tên QuanLyKhachSan; cấu hình tên khác thì người dùng tự quản lý.

            b.InitialCatalog = "master";
            if (b.ConnectTimeout < 60) b.ConnectTimeout = 60; // LocalDB khởi động lần đầu có thể mất vài chục giây.

            List<string> noiDung = new List<string>();
            foreach (string ten in KichBan) noiDung.Add(DocKichBan(ten));
            string phienBan = BamPhienBan(noiDung);

            using (var conn = new SqlConnection(b.ConnectionString))
            {
                conn.Open();

                if (Scalar(conn, "SELECT DB_ID(@ten)", new SqlParameter("@ten", tenCsdl)) == null)
                {
                    if (LaLocalDb(chuoiKetNoi)) TaoCsdlLocalDb(conn, tenCsdl);
                    // Server khác: script 01 tự CREATE DATABASE ở thư mục mặc định của instance.
                }
                else
                {
                    conn.ChangeDatabase(tenCsdl);
                    object daCo = Scalar(conn,
                        "SELECT CAST(value AS nvarchar(200)) FROM sys.extended_properties WHERE class = 0 AND name = @ten",
                        new SqlParameter("@ten", TenThuocTinhPhienBan));
                    bool coBang = Scalar(conn, "SELECT OBJECT_ID(N'dbo.NguoiDung', N'U')") != null;
                    if (coBang && string.Equals(daCo as string, phienBan, StringComparison.Ordinal))
                        return false; // Đã sẵn sàng, không cần làm gì.
                    conn.ChangeDatabase("master");
                }

                foreach (string kichBan in noiDung) ChayKichBan(conn, kichBan);

                conn.ChangeDatabase(tenCsdl);
                GhiPhienBan(conn, phienBan);
                return true;
            }
        }

        /// <summary>Tạo database với tệp ở thư mục của người dùng; gắn lại nếu tệp .mdf đã có từ trước.</summary>
        private static void TaoCsdlLocalDb(SqlConnection conn, string tenCsdl)
        {
            Directory.CreateDirectory(ThuMucDuLieu);
            string mdf = Path.Combine(ThuMucDuLieu, tenCsdl + ".mdf");
            string ldf = Path.Combine(ThuMucDuLieu, tenCsdl + "_log.ldf");
            string ten = "[" + tenCsdl.Replace("]", "]]") + "]";

            string sql;
            if (File.Exists(mdf))
            {
                sql = File.Exists(ldf)
                    ? "CREATE DATABASE " + ten + " ON (FILENAME = " + ChuoiSql(mdf) + "), (FILENAME = " + ChuoiSql(ldf) + ") FOR ATTACH;"
                    : "CREATE DATABASE " + ten + " ON (FILENAME = " + ChuoiSql(mdf) + ") FOR ATTACH_REBUILD_LOG;";
            }
            else
            {
                sql = "CREATE DATABASE " + ten
                    + " ON PRIMARY (NAME = " + ChuoiSql(tenCsdl) + ", FILENAME = " + ChuoiSql(mdf) + ")"
                    + " LOG ON (NAME = " + ChuoiSql(tenCsdl + "_log") + ", FILENAME = " + ChuoiSql(ldf) + ");";
            }

            using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 })
                cmd.ExecuteNonQuery();
        }

        /// <summary>Chạy một script theo từng batch, tách tại các dòng chỉ có GO (giống sqlcmd).</summary>
        private static void ChayKichBan(SqlConnection conn, string kichBan)
        {
            foreach (string batch in TachBatch(kichBan))
            {
                using (var cmd = new SqlCommand(batch, conn) { CommandTimeout = 300 })
                    cmd.ExecuteNonQuery();
            }
        }

        public static IEnumerable<string> TachBatch(string kichBan)
        {
            string[] phan = Regex.Split(kichBan, @"^[ \t]*GO[ \t]*(?:--[^\r\n]*)?\r?$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            foreach (string p in phan)
            {
                if (!string.IsNullOrWhiteSpace(p)) yield return p;
            }
        }

        private static void GhiPhienBan(SqlConnection conn, string phienBan)
        {
            const string sql = @"
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND name = @ten)
    EXEC sys.sp_updateextendedproperty @name = @ten, @value = @giaTri;
ELSE
    EXEC sys.sp_addextendedproperty @name = @ten, @value = @giaTri;";
            try
            {
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(new SqlParameter("@ten", TenThuocTinhPhienBan));
                    cmd.Parameters.Add(new SqlParameter("@giaTri", phienBan));
                    cmd.ExecuteNonQuery();
                }
            }
            catch (SqlException)
            {
                // Tài khoản không có quyền ALTER DATABASE: lần sau chỉ chạy lại script (vẫn an toàn).
            }
        }

        private static string DocKichBan(string ten)
        {
            Assembly asm = typeof(KhoiTaoCsdl).Assembly;
            string tenTaiNguyen = null;
            foreach (string r in asm.GetManifestResourceNames())
            {
                if (r.EndsWith("." + ten, StringComparison.OrdinalIgnoreCase)) { tenTaiNguyen = r; break; }
            }
            if (tenTaiNguyen == null)
                throw new InvalidOperationException("Không tìm thấy script nhúng " + ten + " trong ứng dụng.");

            using (Stream s = asm.GetManifestResourceStream(tenTaiNguyen))
            using (var reader = new StreamReader(s, Encoding.UTF8, true))
                return reader.ReadToEnd();
        }

        private static string BamPhienBan(IEnumerable<string> noiDung)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] du = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n--\n", noiDung)));
                return Convert.ToHexString(du).Substring(0, 32);
            }
        }

        private static object Scalar(SqlConnection conn, string sql, params SqlParameter[] thamSo)
        {
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (thamSo != null) cmd.Parameters.AddRange(thamSo);
                object kq = cmd.ExecuteScalar();
                return kq == DBNull.Value ? null : kq;
            }
        }

        private static string ChuoiSql(string s)
        {
            return "N'" + s.Replace("'", "''") + "'";
        }
    }
}
