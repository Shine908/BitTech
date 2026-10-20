using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace BitTech
{
    public partial class AdminPanelWindow : Window
    {
        // Твоя строка подключения
        private const string ConnStr = @"Data Source=HOME-PC;Initial Catalog=BitTech;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

        private readonly ObservableCollection<ProductItem> _products = new();

        public AdminPanelWindow()
        {
            InitializeComponent();
            dgProducts.ItemsSource = _products;
            LoadProducts();
        }

        // Загрузка списка товаров в таблицу
        private void LoadProducts()
        {
            string sql = @"SELECT t.ID_Tovar, t.Nazvanie, k.Kategoria, p.Proizvoditel, t.Kolichestvo, t.Cena 
                           FROM Tovar t
                           JOIN Kategoria k ON t.Kategoria_ID_Kategoria = k.ID_Kategoria
                           JOIN Proizvoditel p ON t.Proizvoditel_ID_Proizvoditel = p.ID_Proizvoditel
                           ORDER BY t.ID_Tovar";

            _products.Clear();
            try
            {
                using var conn = new SqlConnection(ConnStr);
                var cmd = new SqlCommand(sql, conn);
                conn.Open();
                using var r = cmd.ExecuteReader();

                while (r.Read())
                {
                    _products.Add(new ProductItem
                    {
                        Id = r.GetInt32(0),
                        Name = r.GetString(1),
                        Category = r.GetString(2),
                        Producer = r.GetString(3),
                        Quantity = r.GetInt32(4),
                        OriginalPrice = r.GetInt32(5)
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки списка товаров: " + ex.Message);
            }
        }

        // Кнопка "Добавить"
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Передаем 0, чтобы открылось окно в режиме добавления
            var editWindow = new ProductEditWindow(0);

            // ShowDialog возвращает true, если DialogResult = true (успешное сохранение)
            if (editWindow.ShowDialog() == true)
            {
                LoadProducts(); // Обновляем таблицу после добавления
            }
        }

        // Кнопка "Редактировать"
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgProducts.SelectedItem is ProductItem selectedProduct)
            {
                // Передаем ID товара для загрузки данных
                var editWindow = new ProductEditWindow(selectedProduct.Id);

                if (editWindow.ShowDialog() == true)
                {
                    LoadProducts(); // Обновляем таблицу после редактирования
                }
            }
            else
            {
                MessageBox.Show("Выберите товар из списка для редактирования!");
            }
        }

        // Кнопка "Удалить"
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgProducts.SelectedItem is ProductItem selectedProduct)
            {
                var result = MessageBox.Show(
                    $"Вы действительно хотите удалить товар '{selectedProduct.Name}'?\nЭто действие нельзя отменить.",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var conn = new SqlConnection(ConnStr);
                        conn.Open();

                        // Удаляем товар по ID
                        var cmd = new SqlCommand("DELETE FROM Tovar WHERE ID_Tovar = @id", conn);
                        cmd.Parameters.AddWithValue("@id", selectedProduct.Id);
                        cmd.ExecuteNonQuery();

                        MessageBox.Show("Товар успешно удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadProducts(); // Обновляем таблицу
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка при удалении: " + ex.Message);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите товар из списка для удаления!");
            }
        }

        // ОБРАБОТЧИК ДВОЙНОГО КЛИКА (Исправление ошибки CS1061)
        private void DgProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // При двойном клике вызываем ту же логику, что и кнопка "Редактировать"
            BtnEdit_Click(sender, e);
        }
    }
}