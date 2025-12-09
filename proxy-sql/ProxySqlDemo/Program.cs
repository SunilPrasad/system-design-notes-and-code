using System;
using MySql.Data.MySqlClient;

class Program
{
    // Connect to ProxySQL on port 6033, NOT directly to MySQL (3306)
    static string connString = "Server=localhost;Port=6033;Database=appdb;Uid=app_user;Pwd=app_password;";

    static void Main(string[] args)
    {
        Console.WriteLine("--- Testing ProxySQL Read/Write Split ---");

        // Test 1: Write Operation (Should go to Primary)
        Console.WriteLine("\n[1] Executing INSERT (Write)...");
        ExecuteQuery("INSERT INTO server_id (name) VALUES ('New_Data_From_App')");

        // Test 2: Read Operation (Should go to Replica)
        Console.WriteLine("\n[2] Executing SELECT (Read)...");
        string serverName = ExecuteScalar("SELECT name FROM server_id LIMIT 1");
        Console.WriteLine($"   Response came from: {serverName}");

        if (serverName == "REPLICA_SERVER")
            Console.WriteLine("   SUCCESS! Proxy routed READ to Replica.");
        else
            Console.WriteLine("   NOTE: Proxy routed READ to Primary (Fallback or Config issue).");
    }

    static void ExecuteQuery(string sql)
    {
        using (var conn = new MySqlConnection(connString))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.ExecuteNonQuery();
                Console.WriteLine("   Write executed successfully.");
            }
        }
    }

    static string ExecuteScalar(string sql)
    {
        using (var conn = new MySqlConnection(connString))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                return cmd.ExecuteScalar()?.ToString();
            }
        }
    }
}