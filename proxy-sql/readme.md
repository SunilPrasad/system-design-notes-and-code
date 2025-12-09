# Tutorial: Master Database Routing with ProxySQL (in 15 Minutes)

Have you ever wondered how massive applications handle millions of users? They don't just use one database server; they use many. But how does the application know which database to talk to?

Enter **ProxySQL**.

Think of ProxySQL as a traffic cop. It stands between your application and your databases.

  * It sends **Write** commands (saving data) to your Primary database.
  * It sends **Read** commands (viewing data) to your Replica databases.

This guide will show you how to build this exact architecture from scratch using **Docker**, **MySQL**, and **C\#**.

-----

### **Prerequisites**

Before we start, make sure you have these installed:

1.  **Docker Desktop:** To run our databases.
2.  **.NET SDK:** To run our C\# code.

-----

### **Step 1: Build the Infrastructure**

We need three "servers": one Primary MySQL, one Replica MySQL, and the ProxySQL server itself. Instead of buying hardware, we will use a `docker-compose.yml` file.

**Action:** Create a folder named `ProxySqlLab` and create a file inside called `docker-compose.yml`. Paste this code:

```yaml
version: '3.8'

services:
  # 1. The Writer (Primary)
  mysql-primary:
    image: mysql:8.0
    hostname: mysql-primary
    environment:
      MYSQL_ROOT_PASSWORD: root
      MYSQL_DATABASE: appdb
    command: --default-authentication-plugin=mysql_native_password
    networks:
      - proxynet

  # 2. The Reader (Replica)
  mysql-replica:
    image: mysql:8.0
    hostname: mysql-replica
    environment:
      MYSQL_ROOT_PASSWORD: root
      MYSQL_DATABASE: appdb
    command: --default-authentication-plugin=mysql_native_password
    networks:
      - proxynet

  # 3. The Traffic Cop (ProxySQL)
  proxysql:
    image: proxysql/proxysql:2.5.5
    container_name: proxysql
    ports:
      - "6032:6032" # Admin Interface (Config)
      - "6033:6033" # Traffic Interface (App connects here)
    networks:
      - proxynet
    depends_on:
      - mysql-primary
      - mysql-replica

networks:
  proxynet:
```

**Launch it:** Open your terminal in that folder and run:

```bash
docker-compose up -d
```

-----

### **Step 2: Label Your Databases**

To prove that ProxySQL is working, we need a way to tell our databases apart. We will create a table called `server_id` on both, but we will put different data in them.

**1. Setup the Primary (Writer)**
Run this in your terminal:

```bash
docker exec -it mysql-primary-1 mysql -u root -proot
```

*Paste this SQL:*

```sql
-- Create our app user
CREATE USER 'app_user'@'%' IDENTIFIED BY 'secret';
GRANT ALL PRIVILEGES ON appdb.* TO 'app_user'@'%';
FLUSH PRIVILEGES;

-- Tag this server
USE appdb;
CREATE TABLE server_id (name VARCHAR(50));
INSERT INTO server_id VALUES ('I AM THE PRIMARY (WRITER)');
EXIT;
```

**2. Setup the Replica (Reader)**
Run this in your terminal:

```bash
docker exec -it mysql-replica-1 mysql -u root -proot
```

*Paste this SQL:*

```sql
CREATE USER 'app_user'@'%' IDENTIFIED BY 'secret';
GRANT ALL PRIVILEGES ON appdb.* TO 'app_user'@'%';
FLUSH PRIVILEGES;

USE appdb;
CREATE TABLE server_id (name VARCHAR(50));
INSERT INTO server_id VALUES ('I AM THE REPLICA (READER)');
EXIT;
```

-----

### **Step 3: Configure the Proxy (The "Brain")**

Now for the fun part. We need to teach ProxySQL how to route traffic. We do this by connecting to its **Admin Interface** on port `6032`.

**Connect to Proxy Admin:**

```bash
docker exec -it proxysql mysql -u admin -padmin -h 127.0.0.1 -P 6032
```

**Run these 3 Blocks of SQL:**

**A. Create Hostgroups (Buckets of servers)**
We will define `HG10` for Writers and `HG20` for Readers.

```sql
-- Add Primary to Writer Group (10) AND Reader Group (20)
INSERT INTO mysql_servers (hostgroup_id, hostname, port) VALUES (10, 'mysql-primary', 3306);
INSERT INTO mysql_servers (hostgroup_id, hostname, port) VALUES (20, 'mysql-primary', 3306);

-- Add Replica to Reader Group (20) Only
INSERT INTO mysql_servers (hostgroup_id, hostname, port) VALUES (20, 'mysql-replica', 3306);

LOAD MYSQL SERVERS TO RUNTIME;
SAVE MYSQL SERVERS TO DISK;
```

**B. Add the User**
Allow `app_user` to connect through the proxy.

```sql
INSERT INTO mysql_users (username, password, default_hostgroup) VALUES ('app_user', 'secret', 10);

LOAD MYSQL USERS TO RUNTIME;
SAVE MYSQL USERS TO DISK;
```

**C. Define the Rules (The Magic)**
This tells ProxySQL: *"If the query starts with SELECT, send it to HG20. Otherwise, send it to HG10."*

```sql
INSERT INTO mysql_query_rules (rule_id, active, match_digest, destination_hostgroup, apply)
VALUES (1, 1, '^SELECT', 20, 1);

LOAD MYSQL QUERY RULES TO RUNTIME;
SAVE MYSQL QUERY RULES TO DISK;
EXIT;
```

-----

### **Step 4: The Application Test (C\#)**

Now we write a tiny C\# app. The most important thing to notice is that the app **only connects to port 6033**. It has no idea that two different databases exist behind the scenes.

1.  **Create the project:**

    ```bash
    dotnet new console -n ProxyTest
    cd ProxyTest
    dotnet add package MySql.Data
    ```

2.  **Paste this into `Program.cs`:**

<!-- end list -->

```csharp
using System;
using MySql.Data.MySqlClient;

class Program
{
    // Note: We connect to Port 6033 (Proxy), not 3306 (DB)
    static string connStr = "Server=localhost;Port=6033;Database=appdb;Uid=app_user;Pwd=secret;";

    static void Main(string[] args)
    {
        Console.WriteLine("--- Connecting to ProxySQL ---");

        // 1. Send a WRITE command (Should go to Primary)
        Console.WriteLine("\nSending INSERT command...");
        RunQuery("INSERT INTO server_id (name) VALUES ('New Data')");

        // 2. Send a READ command (Should go to Replica)
        Console.WriteLine("\nSending SELECT command...");
        string result = RunScalar("SELECT name FROM server_id LIMIT 1");
        
        Console.WriteLine($"\nProxySQL routed our query to: {result}");
    }

    static void RunQuery(string sql)
    {
        using (var conn = new MySqlConnection(connStr))
        {
            conn.Open();
            new MySqlCommand(sql, conn).ExecuteNonQuery();
        }
    }

    static string RunScalar(string sql)
    {
        using (var conn = new MySqlConnection(connStr))
        {
            conn.Open();
            return new MySqlCommand(sql, conn).ExecuteScalar().ToString();
        }
    }
}
```

-----

### **Step 5: The "Aha\!" Moment**

Run your application:

```bash
dotnet run
```

**You should see this output:**

> Sending INSERT command...
>
> Sending SELECT command...
>
> ProxySQL routed our query to: **I AM THE REPLICA (READER)**

**Success\!**
You sent a query to one port (`6033`). ProxySQL looked at the SQL, saw it was a `SELECT`, and redirected it to the Replica container transparently.

### **Summary**

You have just successfully:

1.  Deployed a complex database architecture using Docker.
2.  Configured ProxySQL Query Rules.
3.  Routed traffic intelligently without changing application logic.

**Next Step:**
Would you like me to create a "Part 2" section for this blog post that covers **Monitoring**, showing how to view the live traffic stats inside ProxySQL?