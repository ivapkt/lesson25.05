using Microsoft.Data.Sqlite;

namespace ContactsDirectory;

public class Contact
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
}

internal class Program
{
    private static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "contacts.db");

    private const string ConnectionString = "Data Source=" + "contacts.db";

    private static readonly string FullConnectionString = $"Data Source={DbPath}";

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine($"Путь к базе данных: {DbPath}");
        Console.WriteLine(new string('=', 60));

        EnsureDatabase();

        Console.WriteLine("\n--- Все контакты ---");
        PrintContacts(GetAllContacts());

        Console.WriteLine("\n--- Контакты отдела IT ---");
        PrintContacts(GetContactsByDepartment("IT"));

        Console.Write("\nВведите Id для поиска контакта: ");
        string? input = Console.ReadLine();
        if (int.TryParse(input, out int id))
        {
            Console.WriteLine($"\n--- Контакт с Id = {id} ---");
            PrintContact(GetContactById(id));
        }
        else
        {
            Console.WriteLine("Введено некорректное число.");
        }

        Console.WriteLine("\n--- Контакты без e-mail ---");
        PrintContacts(GetContactsWithoutEmail());

        Console.WriteLine("\nГотово. Нажмите Enter для выхода.");
        Console.ReadLine();
    }

    private static void EnsureDatabase()
    {
        using var connection = new SqliteConnection(FullConnectionString);
        connection.Open();

        using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Contacts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NOT NULL,
                    Phone TEXT NULL,
                    Email TEXT NULL,
                    Department TEXT NULL
                );";
            createCmd.ExecuteNonQuery();
        }

        using (var countCmd = connection.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM Contacts;";
            long count = (long)(countCmd.ExecuteScalar() ?? 0L);

            if (count == 0)
            {
                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO Contacts (FullName, Phone, Email, Department) VALUES
                    ('Иван Петров',     '+7-900-111-22-33', 'ivan@mail.com',   'IT'),
                    ('Мария Орлова',     NULL,              'maria@mail.com',  'HR'),
                    ('Сергей Кузнецов', '+7-900-222-33-44', NULL,              'IT'),
                    ('Анна Соколова',   '+7-900-333-44-55', 'anna@mail.com',   'Sales'),
                    ('Дмитрий Волков',  '+7-900-444-55-66', 'dmitry@mail.com', NULL),
                    ('Елена Морозова',  '+7-900-555-66-77', 'elena@mail.com',  'HR'),
                    ('Павел Новиков',    NULL,              NULL,              'IT'),
                    ('Ольга Зайцева',   '+7-900-666-77-88', 'olga@mail.com',   'Sales');";
                insertCmd.ExecuteNonQuery();
            }
        }
    }

    private static List<Contact> GetAllContacts()
    {
        var list = new List<Contact>();

        using var connection = new SqliteConnection(FullConnectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, FullName, Phone, Email, Department FROM Contacts;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapContact(reader));
        }

        return list;
    }

    private static List<Contact> GetContactsByDepartment(string department)
    {
        var list = new List<Contact>();

        using var connection = new SqliteConnection(FullConnectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT Id, FullName, Phone, Email, Department
                            FROM Contacts
                            WHERE Department = $dep;";
        cmd.Parameters.AddWithValue("$dep", department);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapContact(reader));
        }

        return list;
    }

    private static Contact? GetContactById(int id)
    {
        using var connection = new SqliteConnection(FullConnectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT Id, FullName, Phone, Email, Department
                            FROM Contacts
                            WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return MapContact(reader);
        }
        return null;
    }

    private static List<Contact> GetContactsWithoutEmail()
    {
        var list = new List<Contact>();

        using var connection = new SqliteConnection(FullConnectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT Id, FullName, Phone, Email, Department
                            FROM Contacts
                            WHERE Email IS NULL;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapContact(reader));
        }

        return list;
    }

    private static Contact MapContact(SqliteDataReader reader)
    {
        return new Contact
        {
            Id = reader.GetInt32(0),
            FullName = reader.GetString(1),
            Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
            Email = reader.IsDBNull(3) ? null : reader.GetString(3),
            Department = reader.IsDBNull(4) ? null : reader.GetString(4)
        };
    }

    private static void PrintContacts(IEnumerable<Contact> contacts)
    {
        bool any = false;
        foreach (var c in contacts)
        {
            PrintContact(c);
            any = true;
        }
        if (!any)
        {
            Console.WriteLine("Записей не найдено.");
        }
    }

    private static void PrintContact(Contact? contact)
    {
        if (contact is null)
        {
            Console.WriteLine("Контакт не найден.");
            return;
        }

        string phone = contact.Phone ?? "<нет телефона>";
        string email = contact.Email ?? "<нет e-mail>";
        string dep = contact.Department ?? "<нет отдела>";

        Console.WriteLine($"{contact.Id} | {contact.FullName} | {phone} | {email} | {dep}");
    }
}
