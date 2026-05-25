using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Data.Sqlite;

namespace ContactsManager
{
    // Модель контакта - описывает одну строку таблицы Contacts
    public class Contact
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Department { get; set; }
    }

    // Модель записи журнала - одна строка таблицы AuditLog
    public class AuditLogEntry
    {
        public int Id { get; set; }
        public int ContactId { get; set; }
        public string ActionName { get; set; } = "";
        public string? Details { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    internal class Program
    {
        // Путь к файлу БД рядом с приложением
        private static readonly string DbPath =
            Path.Combine(AppContext.BaseDirectory, "contacts.db");

        // Единая строка подключения
        private static readonly string ConnectionString =
            $"Data Source={DbPath}";

        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("Путь к БД: " + DbPath);

            EnsureDatabase();

            Console.WriteLine("\n=== Исходный список контактов ===");
            PrintContacts(GetAllContacts());

            // Добавление контакта
            Console.WriteLine("\n=== Добавление нового контакта ===");
            var newContact = new Contact
            {
                FullName = "Иванов Иван",
                Phone = "+7-900-111-22-33",
                Email = "ivanov@mail.ru",
                Department = "ИТ"
            };
            int added = AddContact(newContact);
            Console.WriteLine($"Добавлено строк: {added}");

            // Изменение контакта (берём первого из списка)
            Console.WriteLine("\n=== Изменение контакта Id=1 ===");
            var updated = new Contact
            {
                Id = 1,
                FullName = "Петров Пётр Изменённый",
                Phone = "+7-000-000-00-00",
                Email = "new@mail.ru",
                Department = "Бухгалтерия"
            };
            int updRows = UpdateContact(updated);
            Console.WriteLine(updRows > 0
                ? "Контакт изменён."
                : "Контакт с таким Id не найден.");

            // Поиск
            Console.WriteLine("\n=== Поиск по слову 'ИТ' ===");
            PrintContacts(SearchContacts("ИТ"));

            // Удаление
            Console.WriteLine("\n=== Удаление контакта Id=2 ===");
            int delRows = DeleteContact(2);
            Console.WriteLine(delRows > 0
                ? "Контакт удалён."
                : "Контакт с таким Id не найден.");

            // Успешная транзакция
            Console.WriteLine("\n=== Успешная транзакция (COMMIT) ===");
            RunSuccessfulTransaction(new Contact
            {
                FullName = "Сидорова Анна",
                Phone = "+7-911-222-33-44",
                Email = "sidorova@mail.ru",
                Department = "Кадры"
            });

            // Откат транзакции
            Console.WriteLine("\n=== Транзакция с ошибкой (ROLLBACK) ===");
            RunRollbackTransaction(new Contact
            {
                FullName = "Тестовый Откатный",
                Phone = "00000",
                Email = "rollback@mail.ru",
                Department = "Тест"
            });

            // Итоги
            Console.WriteLine("\n=== Итоговый список контактов ===");
            PrintContacts(GetAllContacts());

            Console.WriteLine("\n=== Итоговый журнал AuditLog ===");
            PrintAuditLog(GetAuditLog());
        }

        // Создание таблиц и тестовых данных
        static void EnsureDatabase()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string createContacts = @"
                CREATE TABLE IF NOT EXISTS Contacts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NOT NULL,
                    Phone TEXT NULL,
                    Email TEXT NULL,
                    Department TEXT NULL
                );";

            string createAudit = @"
                CREATE TABLE IF NOT EXISTS AuditLog (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ContactId INTEGER NOT NULL,
                    ActionName TEXT NOT NULL,
                    Details TEXT NULL,
                    CreatedAt TEXT NOT NULL
                );";

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = createContacts + createAudit;
                cmd.ExecuteNonQuery();
            }

            // Проверяем, есть ли уже данные
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Contacts;";
            long count = (long)checkCmd.ExecuteScalar()!;

            if (count == 0)
            {
                var seed = new List<Contact>
                {
                    new() { FullName="Петров Пётр", Phone="111", Email="p@m.ru", Department="ИТ" },
                    new() { FullName="Смирнова Ольга", Phone="222", Email="o@m.ru", Department="Бухгалтерия" },
                    new() { FullName="Кузнецов Олег", Phone="333", Email="k@m.ru", Department="Кадры" },
                    new() { FullName="Орлова Мария", Phone=null,  Email="m@m.ru", Department="ИТ" },        // без телефона
                    new() { FullName="Беляев Сергей", Phone="555", Email=null,    Department="Кадры" },     // без email
                    new() { FullName="Зайцева Нина",  Phone="666", Email="n@m.ru", Department=null },       // без отдела
                    new() { FullName="Морозов Илья",  Phone="777", Email="i@m.ru", Department="ИТ" },
                    new() { FullName="Лебедев Антон", Phone="888", Email="a@m.ru", Department="Бухгалтерия" }
                };

                foreach (var c in seed) AddContact(c);
            }
        }

        // Получить все контакты
        static List<Contact> GetAllContacts()
        {
            var list = new List<Contact>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, FullName, Phone, Email, Department FROM Contacts;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Contact
                {
                    Id = reader.GetInt32(0),
                    FullName = reader.GetString(1),
                    Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Department = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }
            return list;
        }

        // Поиск по FullName и Department
        static List<Contact> SearchContacts(string searchText)
        {
            var list = new List<Contact>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT Id, FullName, Phone, Email, Department
                                FROM Contacts
                                WHERE FullName LIKE @p OR Department LIKE @p;";
            cmd.Parameters.AddWithValue("@p", "%" + searchText + "%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Contact
                {
                    Id = reader.GetInt32(0),
                    FullName = reader.GetString(1),
                    Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Department = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }
            return list;
        }

        // Добавление
        static int AddContact(Contact c)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO Contacts (FullName, Phone, Email, Department)
                                VALUES (@n, @p, @e, @d);";
            cmd.Parameters.AddWithValue("@n", c.FullName);
            cmd.Parameters.AddWithValue("@p", (object?)c.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", (object?)c.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@d", (object?)c.Department ?? DBNull.Value);

            return cmd.ExecuteNonQuery();
        }

        // Изменение
        static int UpdateContact(Contact c)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"UPDATE Contacts
                                SET FullName=@n, Phone=@p, Email=@e, Department=@d
                                WHERE Id=@id;";
            cmd.Parameters.AddWithValue("@n", c.FullName);
            cmd.Parameters.AddWithValue("@p", (object?)c.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", (object?)c.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@d", (object?)c.Department ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", c.Id);

            return cmd.ExecuteNonQuery();
        }

        // Удаление
        static int DeleteContact(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Contacts WHERE Id=@id;";
            cmd.Parameters.AddWithValue("@id", id);

            return cmd.ExecuteNonQuery();
        }

        // Чтение журнала
        static List<AuditLogEntry> GetAuditLog()
        {
            var list = new List<AuditLogEntry>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, ContactId, ActionName, Details, CreatedAt FROM AuditLog;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AuditLogEntry
                {
                    Id = reader.GetInt32(0),
                    ContactId = reader.GetInt32(1),
                    ActionName = reader.GetString(2),
                    Details = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CreatedAt = reader.GetString(4)
                });
            }
            return list;
        }

        // Успешная транзакция
        static void RunSuccessfulTransaction(Contact c)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var tx = connection.BeginTransaction();

            try
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = tx;
                insert.CommandText = @"INSERT INTO Contacts (FullName, Phone, Email, Department)
                                       VALUES (@n,@p,@e,@d);
                                       SELECT last_insert_rowid();";
                insert.Parameters.AddWithValue("@n", c.FullName);
                insert.Parameters.AddWithValue("@p", (object?)c.Phone ?? DBNull.Value);
                insert.Parameters.AddWithValue("@e", (object?)c.Email ?? DBNull.Value);
                insert.Parameters.AddWithValue("@d", (object?)c.Department ?? DBNull.Value);
                long newId = (long)insert.ExecuteScalar()!;

                using var log = connection.CreateCommand();
                log.Transaction = tx;
                log.CommandText = @"INSERT INTO AuditLog (ContactId, ActionName, Details, CreatedAt)
                                    VALUES (@cid,@a,@det,@dt);";
                log.Parameters.AddWithValue("@cid", newId);
                log.Parameters.AddWithValue("@a", "CREATE");
                log.Parameters.AddWithValue("@det", "Добавлен контакт " + c.FullName);
                log.Parameters.AddWithValue("@dt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                log.ExecuteNonQuery();

                tx.Commit();
                Console.WriteLine("Транзакция выполнена (COMMIT). Новый Id = " + newId);
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Console.WriteLine("Ошибка, выполнен ROLLBACK: " + ex.Message);
            }
        }

        // Транзакция с откатом
        static void RunRollbackTransaction(Contact c)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var tx = connection.BeginTransaction();

            try
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = tx;
                insert.CommandText = @"INSERT INTO Contacts (FullName, Phone, Email, Department)
                                       VALUES (@n,@p,@e,@d);";
                insert.Parameters.AddWithValue("@n", c.FullName);
                insert.Parameters.AddWithValue("@p", (object?)c.Phone ?? DBNull.Value);
                insert.Parameters.AddWithValue("@e", (object?)c.Email ?? DBNull.Value);
                insert.Parameters.AddWithValue("@d", (object?)c.Department ?? DBNull.Value);
                insert.ExecuteNonQuery();

                // Искусственная ошибка
                throw new Exception("Искусственная ошибка для проверки ROLLBACK");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Console.WriteLine("ROLLBACK выполнен. Причина: " + ex.Message);
            }
        }

        // Вывод контактов
        static void PrintContacts(List<Contact> list)
        {
            Console.WriteLine($"{"Id",-4}{"ФИО",-30}{"Телефон",-20}{"Email",-20}{"Отдел",-15}");
            foreach (var c in list)
            {
                Console.WriteLine(
                    $"{c.Id,-4}{c.FullName,-30}{c.Phone ?? "—",-20}{c.Email ?? "—",-20}{c.Department ?? "—",-15}");
            }
        }

        // Вывод журнала
        static void PrintAuditLog(List<AuditLogEntry> list)
        {
            Console.WriteLine($"{"Id",-4}{"CId",-5}{"Действие",-10}{"Детали",-40}{"Время",-20}");
            foreach (var a in list)
            {
                Console.WriteLine(
                    $"{a.Id,-4}{a.ContactId,-5}{a.ActionName,-10}{a.Details ?? "",-40}{a.CreatedAt,-20}");
            }
        }
    }
}
