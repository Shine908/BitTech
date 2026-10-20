using Microsoft.Data.SqlClient;
using System;
using System.Windows;

namespace BitTech
{
    public partial class LoginWindow : Window
    {
        private const string ConnStr = @"Data Source=HOME-PC;Initial Catalog=BitTech;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

        // Свойства пользователя
        public int UserId { get; private set; }
        public bool IsGuest { get; private set; }
        public string UserLogin { get; private set; } = "";

        // Новые свойства для ФИО
        public string UserFamilia { get; private set; } = "";
        public string UserImya { get; private set; } = "";
        public string UserOtchestvo { get; private set; } = "";

        public LoginWindow() => InitializeComponent();

        private void TxtLogin_GotFocus(object s, RoutedEventArgs e)
        { if (txtLogin.Text == "Введите логин...") txtLogin.Text = ""; }

        private void TxtLogin_LostFocus(object s, RoutedEventArgs e)
        { if (string.IsNullOrWhiteSpace(txtLogin.Text)) txtLogin.Text = "Введите логин..."; }

        private void BtnGuest_Click(object s, RoutedEventArgs e)
        {
            IsGuest = true;
            UserId = 0;
            UserLogin = "Гость";
            UserFamilia = ""; UserImya = ""; UserOtchestvo = "";
            OpenMain();
        }

        private void BtnLogin_Click(object s, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            if (login == "Введите логин..." || string.IsNullOrEmpty(login))
            { lblError.Text = "Пожалуйста, введите логин!"; return; }

            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                // ЗАПРОС ТЕПЕРЬ ЗАБИРАЕТ ФИО
                var cmd = new SqlCommand("SELECT ID_Users, Familia, Imya, Otchestvo FROM Users WHERE Login = @login", conn);
                cmd.Parameters.AddWithValue("@login", login);

                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    UserId = reader.GetInt32(0);
                    UserLogin = login;
                    IsGuest = false;

                    // Сохраняем ФИО (если в базе NULL, пишем пустую строку)
                    UserFamilia = reader.IsDBNull(1) ? "" : reader.GetString(1).Trim();
                    UserImya = reader.IsDBNull(2) ? "" : reader.GetString(2).Trim();
                    UserOtchestvo = reader.IsDBNull(3) ? "" : reader.GetString(3).Trim();

                    OpenMain();
                }
                else
                {
                    lblError.Text = "Пользователь не найден в базе.";
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка БД: " + ex.Message); }
        }

        private void BtnRegister_Click(object s, RoutedEventArgs e)
        {
            var regWindow = new RegisterWindow();
            regWindow.ShowDialog();
        }

        private void OpenMain()
        {
            new MainWindow(this).Show();
            this.Close();
        }
    }
}