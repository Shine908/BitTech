using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace BitTech
{
    // === МОДЕЛЬ ТОВАРА ===
    public class ProductItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public int Price { get; set; }
        public int OriginalPrice { get; set; }
        public bool HasDiscount { get; set; }

        public BitmapImage? Image { get; set; }
        public string Category { get; set; } = "";
        public string Producer { get; set; } = "";
        public string Description { get; set; } = "";
        public string Complectation { get; set; } = "";

        public static BitmapImage? BytesToImage(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            var img = new BitmapImage();
            try
            {
                using var ms = new MemoryStream(bytes);
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();
            }
            catch { return null; }
            return img;
        }
    }

    public partial class MainWindow : Window
    {
        private const string ConnStr = @"Data Source=HOME-PC;Initial Catalog=BitTech;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

        private readonly LoginWindow _login;
        private readonly ObservableCollection<ProductItem> _products = new();
        private bool _isAdmin;

        public MainWindow(LoginWindow login)
        {
            InitializeComponent();
            _login = login;

            _isAdmin = login.UserLogin.Equals("admin", StringComparison.OrdinalIgnoreCase);
            lvProducts.ItemsSource = _products;

            if (_login.IsGuest)
            {
                pnlControls.Visibility = Visibility.Collapsed;
                lblStatus.Text = "👁 Режим ГОСТЯ: только просмотр";
            }
            else
            {
                // --- ЛОГИКА ФОРМИРОВАНИЯ ФИО ---
                List<string> nameParts = new List<string>();

                if (!string.IsNullOrEmpty(_login.UserFamilia)) nameParts.Add(_login.UserFamilia);
                if (!string.IsNullOrEmpty(_login.UserImya)) nameParts.Add(_login.UserImya);
                if (!string.IsNullOrEmpty(_login.UserOtchestvo)) nameParts.Add(_login.UserOtchestvo);

                // Если ФИО нет вообще, используем логин
                string displayName = nameParts.Count > 0 ? string.Join(" ", nameParts) : _login.UserLogin;
                // --------------------------------

                lblStatus.Text = _isAdmin
                    ? $"🛡️ АДМИНИСТРАТОР: {displayName}"
                    : $"✅ Пользователь: {displayName}";

                btnManageProducts.Visibility = _isAdmin ? Visibility.Visible : Visibility.Collapsed;
                LoadCategories();
            }
            LoadProducts();
        }

        private void LoadCategories()
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                var da = new SqlDataAdapter("SELECT ID_Kategoria, Kategoria FROM Kategoria ORDER BY Kategoria", conn);
                var dt = new System.Data.DataTable(); da.Fill(dt);
                var row = dt.NewRow(); row["ID_Kategoria"] = 0; row["Kategoria"] = "-- Все категории --";
                dt.Rows.InsertAt(row, 0);
                cmbCategory.ItemsSource = dt.DefaultView;
                cmbCategory.DisplayMemberPath = "Kategoria";
                cmbCategory.SelectedValuePath = "ID_Kategoria";
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки категорий: " + ex.Message); }
        }

        private void LoadProducts(string search = "", int catId = 0)
        {
            string sql = @"SELECT ID_Tovar, Nazvanie, Kolichestvo, Cena, Izobrazhenie FROM Tovar WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(search)) sql += " AND Nazvanie LIKE @s";
            if (catId > 0) sql += " AND Kategoria_ID_Kategoria = @c";

            List<ProductItem> tempList = new List<ProductItem>();

            try
            {
                using var conn = new SqlConnection(ConnStr);
                var cmd = new SqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@s", "%" + search + "%");
                if (catId > 0) cmd.Parameters.AddWithValue("@c", catId);

                conn.Open();
                using var r = cmd.ExecuteReader();

                while (r.Read())
                {
                    int qty = r.GetInt32(2);
                    int price = r.GetInt32(3);
                    bool hasDiscount = (qty <= 5 && qty > 0);

                    tempList.Add(new ProductItem
                    {
                        Id = r.GetInt32(0),
                        Name = r.GetString(1),
                        Quantity = qty,
                        OriginalPrice = price,
                        Price = hasDiscount ? (int)Math.Round(price * 0.9) : price,
                        HasDiscount = hasDiscount,
                        Image = r.IsDBNull(4) ? null : ProductItem.BytesToImage((byte[])r[4])
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки товаров: " + ex.Message);
                return;
            }

            _products.Clear();
            foreach (var item in tempList) _products.Add(item);
        }

        private void TxtSearch_TextChanged(object s, TextChangedEventArgs e) => ApplyFilter();
        private void CmbCategory_SelectionChanged(object s, SelectionChangedEventArgs e) => ApplyFilter();

        private void ApplyFilter()
        {
            if (_login.IsGuest) return;
            int catId = cmbCategory.SelectedValue is DBNull or null ? 0 : Convert.ToInt32(cmbCategory.SelectedValue);
            LoadProducts(txtSearch.Text, catId);
        }

        private void LvProducts_MouseDoubleClick(object s, MouseButtonEventArgs e)
        {
            if (lvProducts.SelectedItem is ProductItem p)
            {
                new ProductDetailWindow(p.Id, !_login.IsGuest, _login.UserId, _isAdmin).ShowDialog();
                ApplyFilter();
            }
        }

        private void BtnCart_Click(object s, RoutedEventArgs e)
        {
            if (!_login.IsGuest) new CartWindow(_login.UserId).ShowDialog();
        }

        private void BtnManageProducts_Click(object s, RoutedEventArgs e)
        {
            if (_isAdmin)
            {
                new AdminPanelWindow().ShowDialog();
                ApplyFilter();
            }
        }

        private void BtnLogout_Click(object s, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы действительно хотите выйти?", "Выход",
                                         MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                new LoginWindow().Show();
                this.Close();
            }
        }
    }
}