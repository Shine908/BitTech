using Microsoft.Data.SqlClient;
using System;
using System.Windows;
using System.Windows.Media;

namespace BitTech
{
    public partial class RegisterWindow : Window
    {
        // Твоя строка подключения
        private const string ConnStr = @"Data Source=HOME-PC;Initial Catalog=BitTech;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

        public RegisterWindow() => InitializeComponent();

        // Обработчик кнопки "Назад"
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // Просто закрываем окно регистрации, пользователь вернется на вход
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string login = txtNewLogin.Text.Trim();
            string familia = txtFamilia.Text.Trim();
            string imya = txtImya.Text.Trim();
            string otchestvo = txtOtchestvo.Text.Trim();

            // Сброс сообщения
            lblMessage.Text = "";

            if (string.IsNullOrEmpty(login))
            {
                lblMessage.Text = "Логин обязателен для заполнения!";
                lblMessage.Foreground = Brushes.Red;
                return;
            }

            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                // Проверка на уникальность логина
                var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Login = @login", conn);
                checkCmd.Parameters.AddWithValue("@login", login);
                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                {
                    lblMessage.Text = "Этот логин уже занят! Придумайте другой.";
                    lblMessage.Foreground = Brushes.Red;
                    return;
                }

                // Вставка нового пользователя
                var insertCmd = new SqlCommand(
                    "INSERT INTO Users (Familia, Imya, Otchestvo, Login) VALUES (@f, @i, @o, @l)", conn);

                // Если поля пустые, записываем NULL, чтобы не хранить пустые строки
                insertCmd.Parameters.AddWithValue("@f", string.IsNullOrEmpty(familia) ? DBNull.Value : (object)familia);
                insertCmd.Parameters.AddWithValue("@i", string.IsNullOrEmpty(imya) ? DBNull.Value : (object)imya);
                insertCmd.Parameters.AddWithValue("@o", string.IsNullOrEmpty(otchestvo) ? DBNull.Value : (object)otchestvo);
                insertCmd.Parameters.AddWithValue("@l", login);

                insertCmd.ExecuteNonQuery();

                lblMessage.Text = "✅ Успешная регистрация!\nТеперь вы можете войти.";
                lblMessage.Foreground = Brushes.Green;

                // Очистка полей для удобства
                txtNewLogin.Clear();
                txtFamilia.Clear();
                txtImya.Clear();
                txtOtchestvo.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при регистрации: " + ex.Message);
            }
        }
    }
}