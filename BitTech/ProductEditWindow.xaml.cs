using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32; // Для OpenFileDialog

namespace BitTech
{
    public partial class ProductEditWindow : Window
    {
        private const string ConnStr = @"Data Source=HOME-PC;Initial Catalog=BitTech;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

        private readonly int _productId; // 0 = добавление, >0 = редактирование
        private byte[]? _imageBytes;     // Байты выбранного изображения

        public ProductEditWindow(int productId = 0)
        {
            InitializeComponent();
            _productId = productId;

            // Меняем заголовок в зависимости от режима
            lblTitle.Text = _productId == 0 ? "Добавление нового товара" : "Редактирование товара";

            LoadDictionaries();
            if (_productId > 0) LoadProductData();
        }

        // Загрузка категорий и производителей в ComboBox
        private void LoadDictionaries()
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                // Категории
                var catCmd = new SqlCommand("SELECT ID_Kategoria, Kategoria FROM Kategoria ORDER BY Kategoria", conn);
                var catDa = new SqlDataAdapter(catCmd);
                var catDt = new DataTable(); catDa.Fill(catDt);
                cmbCategory.ItemsSource = catDt.DefaultView;
                cmbCategory.DisplayMemberPath = "Kategoria";
                cmbCategory.SelectedValuePath = "ID_Kategoria";

                // Производители
                var prodCmd = new SqlCommand("SELECT ID_Proizvoditel, Proizvoditel FROM Proizvoditel ORDER BY Proizvoditel", conn);
                var prodDa = new SqlDataAdapter(prodCmd);
                var prodDt = new DataTable(); prodDa.Fill(prodDt);
                cmbProducer.ItemsSource = prodDt.DefaultView;
                cmbProducer.DisplayMemberPath = "Proizvoditel";
                cmbProducer.SelectedValuePath = "ID_Proizvoditel";
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки справочников: " + ex.Message); }
        }

        // Загрузка данных существующего товара
        private void LoadProductData()
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                var cmd = new SqlCommand("SELECT * FROM Tovar WHERE ID_Tovar = @id", conn);
                cmd.Parameters.AddWithValue("@id", _productId);

                conn.Open();
                using var r = cmd.ExecuteReader();

                if (r.Read())
                {
                    txtName.Text = r["Nazvanie"].ToString();
                    cmbCategory.SelectedValue = r["Kategoria_ID_Kategoria"];
                    cmbProducer.SelectedValue = r["Proizvoditel_ID_Proizvoditel"];
                    txtPrice.Text = r["Cena"].ToString();
                    txtQty.Text = r["Kolichestvo"].ToString();
                    txtDesc.Text = r["Opisanie"].ToString();
                    txtComplectation.Text = r["Komplectacia"].ToString();

                    // Если есть картинка в БД, показываем превью
                    if (!r.IsDBNull(r.GetOrdinal("Izobrazhenie")) && r["Izobrazhenie"] is byte[] bytes && bytes.Length > 0)
                    {
                        _imageBytes = bytes;
                        imgPreview.Source = ProductItem.BytesToImage(bytes);
                        imgPreview.Visibility = Visibility.Visible;
                        txtImagePath.Text = "[Загружено из базы данных]";
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки товара: " + ex.Message); }
        }

        // Выбор файла изображения
        private void BtnSelectImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Выберите изображение товара"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _imageBytes = File.ReadAllBytes(dialog.FileName);
                    txtImagePath.Text = dialog.SafeFileName;

                    // Показываем превью
                    imgPreview.Source = ProductItem.BytesToImage(_imageBytes);
                    imgPreview.Visibility = Visibility.Visible;
                }
                catch (Exception ex) { MessageBox.Show("Ошибка чтения файла: " + ex.Message); }
            }
        }

        // Разрешаем ввод только цифр в поля цены и количества
        private void TxtNumeric_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        // Сохранение (Добавление или Обновление)
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(txtName.Text)) { MessageBox.Show("Укажите название!"); return; }
            if (cmbCategory.SelectedValue is null or DBNull) { MessageBox.Show("Выберите категорию!"); return; }
            if (cmbProducer.SelectedValue is null or DBNull) { MessageBox.Show("Выберите производителя!"); return; }
            if (!int.TryParse(txtPrice.Text, out int price) || price <= 0) { MessageBox.Show("Укажите корректную цену!"); return; }
            if (!int.TryParse(txtQty.Text, out int qty) || qty < 0) { MessageBox.Show("Укажите корректное количество!"); return; }

            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                if (_productId == 0)
                {
                    // ДОБАВЛЕНИЕ НОВОГО ТОВАРА
                    var cmd = new SqlCommand(@"INSERT INTO Tovar 
                        (Nazvanie, Kategoria_ID_Kategoria, Proizvoditel_ID_Proizvoditel, Cena, Kolichestvo, Opisanie, Komplectacia, Izobrazhenie) 
                        VALUES (@n, @cat, @prod, @price, @qty, @desc, @compl, @img)", conn);

                    cmd.Parameters.AddWithValue("@n", txtName.Text.Trim());
                    cmd.Parameters.AddWithValue("@cat", Convert.ToInt32(cmbCategory.SelectedValue));
                    cmd.Parameters.AddWithValue("@prod", Convert.ToInt32(cmbProducer.SelectedValue));
                    cmd.Parameters.AddWithValue("@price", price);
                    cmd.Parameters.AddWithValue("@qty", qty);
                    cmd.Parameters.AddWithValue("@desc", txtDesc.Text.Trim());
                    cmd.Parameters.AddWithValue("@compl", txtComplectation.Text.Trim());
                    cmd.Parameters.AddWithValue("@img", (_imageBytes ?? (object)DBNull.Value));

                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Товар успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // ОБНОВЛЕНИЕ СУЩЕСТВУЮЩЕГО ТОВАРА
                    var cmd = new SqlCommand(@"UPDATE Tovar SET 
                        Nazvanie = @n, Kategoria_ID_Kategoria = @cat, Proizvoditel_ID_Proizvoditel = @prod, 
                        Cena = @price, Kolichestvo = @qty, Opisanie = @desc, Komplectacia = @compl, Izobrazhenie = @img 
                        WHERE ID_Tovar = @id", conn);

                    cmd.Parameters.AddWithValue("@id", _productId);
                    cmd.Parameters.AddWithValue("@n", txtName.Text.Trim());
                    cmd.Parameters.AddWithValue("@cat", Convert.ToInt32(cmbCategory.SelectedValue));
                    cmd.Parameters.AddWithValue("@prod", Convert.ToInt32(cmbProducer.SelectedValue));
                    cmd.Parameters.AddWithValue("@price", price);
                    cmd.Parameters.AddWithValue("@qty", qty);
                    cmd.Parameters.AddWithValue("@desc", txtDesc.Text.Trim());
                    cmd.Parameters.AddWithValue("@compl", txtComplectation.Text.Trim());

                    // Если пользователь не выбрал новую картинку, оставляем старую (NULL в параметре не перезапишет поле)
                    // Но если хотим разрешить удаление картинки, нужна отдельная логика. 
                    // Сейчас: если _imageBytes null, значит пользователь не трогал картинку -> передаем DBNull, чтобы не стереть старую? 
                    // Нет, UPDATE перезапишет на NULL. Нужно проверить: если мы в режиме редактирования и байты null, не обновляем поле картинки.

                    if (_imageBytes != null)
                        cmd.Parameters.AddWithValue("@img", _imageBytes);
                    else
                        // Хак: чтобы не стереть старую картинку, если новую не выбирали, 
                        // лучше сделать два запроса или проверить в коде. 
                        // Для простоты курсовой: если картинку не меняли, просто не трогаем это поле в UPDATE.
                        // Поэтому перепишем SQL динамически или используем подзапрос.
                        // Простой вариант: прочитаем старую картинку и передадим её обратно, если новая не выбрана.
                        cmd.Parameters.AddWithValue("@img", DBNull.Value); // ВНИМАНИЕ: это сотрет картинку!

                    // ИСПРАВЛЕНИЕ ДЛЯ КАРТИНКИ ПРИ РЕДАКТИРОВАНИИ:
                    // Если _imageBytes == null, значит пользователь не загружал новое фото.
                    // Мы должны сохранить старое. Самый простой способ - убрать поле Izobrazhenie из UPDATE, 
                    // но тогда нужен динамический SQL. 
                    // Давай сделаем проще: если байтов нет, прочитаем текущие из БД и передадим их обратно.
                    if (_imageBytes == null && _productId > 0)
                    {
                        var getImgCmd = new SqlCommand("SELECT Izobrazhenie FROM Tovar WHERE ID_Tovar = @id", conn);
                        getImgCmd.Parameters.AddWithValue("@id", _productId);
                        object? oldImg = getImgCmd.ExecuteScalar();
                        cmd.Parameters["@img"].Value = oldImg ?? DBNull.Value;
                    }

                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Товар успешно обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                this.DialogResult = true; // Сообщаем родительскому окну об успехе
                this.Close();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка сохранения: " + ex.Message); }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}