using System;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace DepartmentsDataTableApp
{
    internal class Program
    {
        // Путь к файлу БД формируется рядом с исполняемым файлом приложения
        private static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "departments.db");

        // Строка подключения хранится в одном месте
        private static readonly string ConnectionString = $"Data Source={DbPath};Version=3;";

        // SELECT-запрос для адаптера. Включает первичный ключ Id — это обязательное условие для работы CommandBuilder
        private const string SelectSql = "SELECT Id, Name FROM Departments ORDER BY Id";

        static void Main()
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine($"Файл базы данных: {DbPath}");
                Console.WriteLine(new string('=', 60));

                // 1) Гарантируем существование таблицы и стартовых данных
                EnsureDatabase();

                // 2) Открываем соединение и работаем через DataTable
                using var connection = new SQLiteConnection(ConnectionString);
                connection.Open();

                using var adapter = CreateAdapter(connection);
                DataTable table = LoadDepartments(adapter);

                ShowTable(table, "Исходное содержимое таблицы (после Fill)");

                // 3) Локальные изменения в памяти (отсоединённая модель)
                RenameDepartment(table, id: 1, newName: "Отдел кадров (обновлённый)");
                AddDepartment(table, "Новый отдел маркетинга");
                DeleteDepartment(table, id: 3);

                ShowTable(table, "Состояние ДО сохранения (Modified / Added / Deleted)");

                // 4) Сохранение изменений через CommandBuilder
                int affected = SaveChanges(adapter, table);
                Console.WriteLine($"\nСохранено записей: {affected}");

                // 5) Повторная загрузка для проверки результата
                DataTable reloaded = ReloadDepartments();
                ShowTable(reloaded, "Итоговое содержимое таблицы (после перезагрузки)");

                // 6) Краткий вывод по теме
                PrintConclusion();
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[ОШИБКА] " + ex.Message);
            }

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        // Создание БД и стартовых данных
        private static void EnsureDatabase()
        {
            using var connection = new SQLiteConnection(ConnectionString);
            connection.Open();

            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Departments (
                    Id   INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE
                );";
            createCmd.ExecuteNonQuery();

            // Проверяем, пуста ли таблица — чтобы не плодить дубли при повторных запусках
            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Departments";
            long count = (long)countCmd.ExecuteScalar();

            if (count == 0)
            {
                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO Departments (Name) VALUES
                        ('Отдел кадров'),
                        ('Бухгалтерия'),
                        ('IT-отдел'),
                        ('Производственный отдел'),
                        ('Юридический отдел');";
                insertCmd.ExecuteNonQuery();
                Console.WriteLine("Стартовые данные добавлены.");
            }
        }

        // Настройка адаптера
        private static SQLiteDataAdapter CreateAdapter(SQLiteConnection connection)
        {
            var adapter = new SQLiteDataAdapter(SelectSql, connection);
            // Чтобы DataTable знал о первичном ключе — нужно для Rows.Find и CommandBuilder
            adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;
            return adapter;
        }

        // Загрузка данных в DataTable
        private static DataTable LoadDepartments(SQLiteDataAdapter adapter)
        {
            var table = new DataTable("Departments");
            adapter.Fill(table);
            return table;
        }

        // Вывод таблицы и состояний строк
        private static void ShowTable(DataTable table, string title)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {title} ---");
            Console.WriteLine($"{"Id",-5} {"Name",-40} {"RowState"}");

            foreach (DataRow row in table.Rows)
            {
                // Для удалённых строк current-версия недоступна — берём Original
                if (row.RowState == DataRowState.Deleted)
                {
                    int id = (int)(long)row["Id", DataRowVersion.Original];
                    string name = (string)row["Name", DataRowVersion.Original];
                    Console.WriteLine($"{id,-5} {name,-40} {row.RowState}");
                }
                else
                {
                    Console.WriteLine($"{row["Id"],-5} {row["Name"],-40} {row.RowState}");
                }
            }
        }

        // Изменение существующей строки
        private static void RenameDepartment(DataTable table, int id, string newName)
        {
            DataRow? row = table.Rows.Find(id);
            if (row != null)
                row["Name"] = newName;
        }

        // Добавление новой строки
        private static void AddDepartment(DataTable table, string name)
        {
            DataRow newRow = table.NewRow();
            newRow["Name"] = name;
            table.Rows.Add(newRow);
        }

        // Пометка строки на удаление
        private static void DeleteDepartment(DataTable table, int id)
        {
            DataRow? row = table.Rows.Find(id);
            row?.Delete();
        }

        // Сохранение изменений
        private static int SaveChanges(SQLiteDataAdapter adapter, DataTable table)
        {
            using var builder = new SQLiteCommandBuilder(adapter);
            // CommandBuilder автоматически сгенерирует INSERT, UPDATE, DELETE
            return adapter.Update(table);
        }

        // Повторная загрузка таблицы из БД
        private static DataTable ReloadDepartments()
        {
            using var connection = new SQLiteConnection(ConnectionString);
            connection.Open();
            using var adapter = CreateAdapter(connection);
            return LoadDepartments(adapter);
        }

        // Краткий вывод
        private static void PrintConclusion()
        {
            Console.WriteLine("\n--- Вывод ---");
            Console.WriteLine("Подход DataTable + CommandBuilder отлично подходит для одиночных");
            Console.WriteLine("простых таблиц без сложных связей — справочников, словарей, настроек.");
            Console.WriteLine("Для проектов со множеством связанных сущностей и бизнес-логикой");
            Console.WriteLine("удобнее переходить к Entity Framework Core.");
        }
    }
}
