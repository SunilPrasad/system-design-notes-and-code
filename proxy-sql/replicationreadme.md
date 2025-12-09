Step-by-Step Guide: Setting Up MySQL Master-Replica Replication with Docker on Windows

Replicating MySQL databases is essential for scaling reads, ensuring high availability, and providing backups. This guide walks you through setting up one primary (write) and one replica (read) MySQL instance using Docker on Windows — all tested and working.

Prerequisites

Docker Desktop installed on Windows

Basic familiarity with Docker and command line

docker-compose available

Step 1: Create Project Structure

Create a folder for your project, e.g., mysql-replication. Inside it:

mysql-replication/
├─ docker-compose.yml
├─ primary/
│   └─ mysql-primary.cnf
└─ replica/
    └─ mysql-replica.cnf

1.1 Primary MySQL Config (mysql-primary.cnf)
[mysqld]
server-id=1
log_bin=mysql-bin
binlog_do_db=appdb

1.2 Replica MySQL Config (mysql-replica.cnf)
[mysqld]
server-id=2
relay_log=relay-bin
read_only=ON


Note: Windows cannot set Linux-style file permissions, so we copy configs into Docker images instead of mounting them.

Step 2: Create Dockerfiles
2.1 Primary Dockerfile (primary/Dockerfile)
FROM mysql:8.0
COPY mysql-primary.cnf /etc/mysql/conf.d/

2.2 Replica Dockerfile (replica/Dockerfile)
FROM mysql:8.0
COPY mysql-replica.cnf /etc/mysql/conf.d/

Step 3: Create docker-compose.yml
version: "3.8"

services:
  mysql-primary:
    build: ./primary
    container_name: mysql-primary
    environment:
      MYSQL_ROOT_PASSWORD: rootpassword
      MYSQL_DATABASE: appdb
    networks:
      - proxynet

  mysql-replica:
    build: ./replica
    container_name: mysql-replica
    environment:
      MYSQL_ROOT_PASSWORD: rootpassword
      MYSQL_DATABASE: appdb
    networks:
      - proxynet

  proxysql:
    image: proxysql/proxysql:2.5.5
    container_name: proxysql
    ports:
      - "6032:6032"
      - "6033:6033"
    networks:
      - proxynet
    depends_on:
      - mysql-primary
      - mysql-replica

networks:
  proxynet:
    driver: bridge

Step 4: Start Containers
docker compose build
docker compose up -d


The primary and replica containers will start.

ProxySQL is optional for routing reads/writes.

Step 5: Create Replication User on Primary

Enter the primary MySQL shell:

docker exec -it mysql-primary mysql -uroot -p


Create a replication user with mysql_native_password (required on Windows):

DROP USER IF EXISTS 'repl'@'%';
CREATE USER 'repl'@'%' IDENTIFIED WITH mysql_native_password BY 'replpass';
GRANT REPLICATION SLAVE ON *.* TO 'repl'@'%';
FLUSH PRIVILEGES;


Why mysql_native_password? MySQL 8 defaults to caching_sha2_password, which often fails in Docker/Windows replication.

Step 6: Get Master Status

On primary MySQL shell:

SHOW MASTER STATUS;


Note the File and Position values (e.g., mysql-bin.000001 and 157).

These are needed to point the replica to the correct starting point.

Step 7: Configure Replica

Enter the replica MySQL shell:

docker exec -it mysql-replica mysql -uroot -p


Stop any existing replication:

STOP REPLICA;
RESET REPLICA ALL;


Configure replication source:

CHANGE REPLICATION SOURCE TO
  SOURCE_HOST='mysql-primary',
  SOURCE_USER='repl',
  SOURCE_PASSWORD='replpass',
  SOURCE_LOG_FILE='mysql-bin.000001',  -- from Step 6
  SOURCE_LOG_POS=157;                  -- from Step 6
START REPLICA;


Verify:

SHOW REPLICA STATUS\G


Replica_IO_Running: Yes

Replica_SQL_Running: Yes

Last_IO_Error and Last_SQL_Error should be empty.

Step 8: Test Replication

On primary, create a table with timestamp:

USE appdb;

CREATE TABLE IF NOT EXISTS test_replication (
  id INT AUTO_INCREMENT PRIMARY KEY,
  note VARCHAR(255),
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO test_replication(note) VALUES ('Hello replica!');


On replica, check data:

SELECT * FROM test_replication;


Output:

id	note	created_at
1	Hello replica!	2025-12-09 12:40:15

✅ Timestamp and ID are replicated exactly as on the primary.

Step 9: Verify Primary vs Replica

Primary:

SHOW VARIABLES LIKE 'read_only';


Replica:

SHOW VARIABLES LIKE 'read_only';


Primary: OFF (writes allowed)

Replica: ON (read-only)

Step 10: Optional – Insert Current Timestamp
INSERT INTO test_replication(note) VALUES ('new row');


The created_at column automatically stores the current timestamp.

Replica will replicate this exact value — it does not generate its own NOW().

✅ Conclusion

You now have:

A primary MySQL for writes

A replica MySQL for reads

Working master-replica replication on Windows + Docker

Automatic replication of IDs and timestamps

This setup is ideal for:

Scaling reads

Backup and reporting

Safe testing of replication concepts

Next Steps

Add ProxySQL rules to route writes → primary, reads → replica

Enable GTID-based replication for easier failover

Add more replicas for higher availability