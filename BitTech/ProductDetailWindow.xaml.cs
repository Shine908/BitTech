using Microsoft.Data.SqlClient;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace BitTech
{
    public partial class ProductDetailWindow : Window
    {
        private const string ConnStr = @"Data Source=HOME-PC;Initial Catalog=BitTech;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

        private readonly int _productId;
        private readonly bool _canBuy;
        private readonly int _userId;
        private readonly bool _isAdmin;

        public ProductDetailWindow(int productId, bool canBuy, int userId, bool isAdmin = false)
        {
            InitializeComponent();
            _productId = productId;
            _canBuy = canBuy;
            _userId = userId;
            _isAdmin = isAdmin;

            pnlBuy.Visibility = canBuy ? Visibility.Visible : Visibility.Collapsed;
            LoadData();
        }

        private void LoadData()
        {
            string sql = @"SELECT t.*, k.Kategoria, p.Proizvoditel FROM Tovar t
                           JOIN Kategoria k ON t.Kategoria_ID_Kategoria = k.ID_Kategoria
                           JOIN Proizvoditel p ON t.Proizvoditel_ID_Proizvoditel = p.ID_Proizvoditel
                           WHERE t.ID_Tovar = @id";
            try
            {
                using var conn = new SqlConnection(ConnStr);
                var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", _productId);
                conn.Open();
                using var r = cmd.ExecuteReader();

                if (r.Read())
                {
                    lblName.Text = r["Nazvanie"].ToString();
                    lblCategory.Text = "Категория: " + r["Kategoria"];
                    lblProducer.Text = "Производитель: " + r["Proizvoditel"];

                    int originalPrice = Convert.ToInt32(r["Cena"]);
                    int quantity = Convert.ToInt32(r["Kolichestvo"]);

                    // ЛОГИКА СКИДКИ
                    bool hasDiscount = (quantity <= 5 && quantity > 0);

                    if (hasDiscount)
                    {
                        int newPrice = (int)Math.Round(originalPrice * 0.9);

                        // Показываем старую цену зачеркнутой
                        lblOldPrice.Text = originalPrice.ToString("N0") + " ₽";
                        lblOldPrice.Visibility = Visibility.Visible;

                        // Показываем новую цену
                        lblPrice.Text = newPrice.ToString("N0") + " ₽";
                        lblPrice.Foreground = System.Windows.Media.Brushes.Red; // Можно сделать красной или зеленой
                    }
                    else
                    {
                        // Обычная цена без скидки
                        lblOldPrice.Visibility = Visibility.Collapsed;
                        lblPrice.Text = originalPrice.ToString("N0") + " ₽";
                        lblPrice.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 125, 50)); // Зеленый цвет
                    }

                    lblStock.Text = "В наличии: " + quantity + " шт.";
                    lblDesc.Text = r["Opisanie"].ToString();
                    lblComplectation.Text = "Комплектация: " + r["Komplectacia"].ToString();

                    if (!r.IsDBNull(4) && r["Izobrazhenie"] is byte[] bytes)
                        picProduct.Source = ProductItem.BytesToImage(bytes);
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки: " + ex.Message); }
        }

        private void TxtQty_PreviewTextInput(object s, TextCompositionEventArgs e)
        { e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$"); }

        private void BtnAddToCart_Click(object s, RoutedEventArgs e)
        {
            if (!int.TryParse(txtQty.Text, out int qty) || qty <= 0)
            { MessageBox.Show("Укажите корректное количество!"); return; }

            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                // Получаем или создаем черновик заказа
                var findCmd = new SqlCommand(
                    "SELECT ID_Zakaz FROM Zakaz WHERE User_ID_Users = @uid AND AdresDostavki IS NULL", conn);
                findCmd.Parameters.AddWithValue("@uid", _userId);
                object? res = findCmd.ExecuteScalar();

                int orderId;
                if (res != null) orderId = Convert.ToInt32(res);
                else
                {
                    var createCmd = new SqlCommand(
                        "INSERT INTO Zakaz (Data, User_ID_Users, ItogovayaCena, AdresDostavki) VALUES (GETDATE(), @uid, 0, NULL); SELECT SCOPE_IDENTITY();", conn);
                    createCmd.Parameters.AddWithValue("@uid", _userId);
                    orderId = Convert.ToInt32(createCmd.ExecuteScalar());
                }

                // Проверяем наличие позиции в корзине
                var checkCmd = new SqlCommand(
                    "SELECT ID_TovarnayaPoziciya, Kolichestvo FROM TovarnayaPoziciya WHERE Zakaz_ID_Zakaz = @oid AND Tovar_ID_Tovar = @pid", conn);
                checkCmd.Parameters.AddWithValue("@oid", orderId);
                checkCmd.Parameters.AddWithValue("@pid", _productId);

                using var r = checkCmd.ExecuteReader();
                if (r.Read())
                {
                    int posId = Convert.ToInt32(r["ID_TovarnayaPoziciya"]);
                    r.Close();
                    var upd = new SqlCommand("UPDATE TovarnayaPoziciya SET Kolichestvo = Kolichestvo + @q WHERE ID_TovarnayaPoziciya = @pid", conn);
                    upd.Parameters.AddWithValue("@q", qty);
                    upd.Parameters.AddWithValue("@pid", posId);
                    upd.ExecuteNonQuery();
                }
                else
                {
                    r.Close();
                    // ВАЖНО: В корзину добавляем ТЕКУЩУЮ цену (со скидкой или без)
                    // Но лучше хранить базовую цену, а скидку считать при оформлении. 
                    // Для простоты курсовой запишем ту цену, которую видит пользователь сейчас.

                    // Получаем актуальную цену из БД (базовую)
                    var priceCmd = new SqlCommand("SELECT Cena FROM Tovar WHERE ID_Tovar = @id", conn);
                    priceCmd.Parameters.AddWithValue("@id", _productId);
                    int basePrice = Convert.ToInt32(priceCmd.ExecuteScalar());

                    // Если есть скидка, применяем её к цене в корзине
                    int finalPrice = (Convert.ToInt32(lblStock.Text.Split(' ')[2]) <= 5)
                                     ? (int)Math.Round(basePrice * 0.9)
                                     : basePrice;

                    var ins = new SqlCommand(
                        "INSERT INTO TovarnayaPoziciya (Tovar_ID_Tovar, Kolichestvo, CenaTovara, Zakaz_ID_Zakaz) VALUES (@pid, @q, @pr, @oid)", conn);
                    ins.Parameters.AddWithValue("@pid", _productId);
                    ins.Parameters.AddWithValue("@q", qty);
                    ins.Parameters.AddWithValue("@pr", finalPrice); // Записываем цену со скидкой
                    ins.Parameters.AddWithValue("@oid", orderId);
                    ins.ExecuteNonQuery();
                }

                MessageBox.Show("Товар добавлен в корзину!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }
    }
}