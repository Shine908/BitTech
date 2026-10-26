using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Windows;

namespace BitTech
{
    public partial class CartWindow : Window
    {
        private const string ConnStr = @"Data Source=HOME-PC;Initial Catalog=BitTech;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";
        private readonly int _userId;
        private int _orderId;
        private DataTable _dtCart = new();

        public CartWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
            LoadCart();
        }

        private void LoadCart()
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                var findCmd = new SqlCommand(
                    "SELECT ID_Zakaz FROM Zakaz WHERE User_ID_Users = @uid AND AdresDostavki IS NULL", conn);
                findCmd.Parameters.AddWithValue("@uid", _userId);
                object? res = findCmd.ExecuteScalar();

                if (res == null)
                {
                    dgCart.ItemsSource = null;
                    lblTotal.Text = "Корзина пуста";
                    _orderId = 0;
                    return;
                }

                _orderId = Convert.ToInt32(res);
                string sql = @"SELECT tp.ID_TovarnayaPoziciya, t.Nazvanie, tp.Kolichestvo, tp.CenaTovara, 
                                      (tp.Kolichestvo * tp.CenaTovara) as Summa
                               FROM TovarnayaPoziciya tp
                               JOIN Tovar t ON tp.Tovar_ID_Tovar = t.ID_Tovar
                               WHERE tp.Zakaz_ID_Zakaz = @oid";

                var da = new SqlDataAdapter(sql, conn);
                da.SelectCommand.Parameters.AddWithValue("@oid", _orderId);
                _dtCart = new DataTable();
                da.Fill(_dtCart);
                dgCart.ItemsSource = _dtCart.DefaultView;

                int total = 0;
                foreach (DataRow row in _dtCart.Rows) total += Convert.ToInt32(row["Summa"]);
                lblTotal.Text = "Итого: " + total.ToString("N0") + " ₽";

                var addrCmd = new SqlCommand("SELECT AdresDostavki FROM Zakaz WHERE ID_Zakaz = @id", conn);
                addrCmd.Parameters.AddWithValue("@id", _orderId);
                object? addr = addrCmd.ExecuteScalar();
                if (addr != DBNull.Value) txtAddress.Text = addr.ToString();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки корзины: " + ex.Message); }
        }

       
        private void BtnDelete_Click(object s, RoutedEventArgs e)
        {
            if (dgCart.SelectedItem is DataRowView row &&
                MessageBox.Show("Удалить товар?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    int posId = Convert.ToInt32(row["ID_TovarnayaPoziciya"]);
                    using var conn = new SqlConnection(ConnStr);
                    conn.Open();
                    var cmd = new SqlCommand("DELETE FROM TovarnayaPoziciya WHERE ID_TovarnayaPoziciya = @id", conn);
                    cmd.Parameters.AddWithValue("@id", posId);
                    cmd.ExecuteNonQuery();
                    LoadCart();
                }
                catch (Exception ex) { MessageBox.Show("Ошибка удаления: " + ex.Message); }
            }
        }

        private void BtnOrder_Click(object s, RoutedEventArgs e)
        {
            if (_dtCart.Rows.Count == 0) { MessageBox.Show("Корзина пуста!"); return; }
            if (string.IsNullOrWhiteSpace(txtAddress.Text))
            { MessageBox.Show("Укажите адрес доставки!"); txtAddress.Focus(); return; }

            try
            {
                int total = 0;
                foreach (DataRow row in _dtCart.Rows) total += Convert.ToInt32(row["Summa"]);

                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                var cmd = new SqlCommand(
                    "UPDATE Zakaz SET AdresDostavki = @addr, ItogovayaCena = @total WHERE ID_Zakaz = @id", conn);
                cmd.Parameters.AddWithValue("@addr", txtAddress.Text.Trim());
                cmd.Parameters.AddWithValue("@total", total);
                cmd.Parameters.AddWithValue("@id", _orderId);
                cmd.ExecuteNonQuery();

                MessageBox.Show($"Заказ оформлен!\nАдрес: {txtAddress.Text.Trim()}\nСумма: {total:N0} ₽");
                this.Close();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка оформления: " + ex.Message); }
        }
    }
}