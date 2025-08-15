# Auto Purchase System Database Setup

This repository contains the SQL scripts to create and populate the **Auto Purchase System** database for client management, license management, purchase schedules, and results tracking.

---

## **Files Description**

### 1. `AutoPurchaseSystem_CreateTables.sql`
- **Purpose:** Creates all database tables with proper relationships, primary keys, and foreign keys.
- **Features:**
  - Uses **DROP TABLE IF EXISTS** to allow rerunning the script safely.
  - All table names and columns follow **UPPERCASE convention** and `T_TABLE_NAME`, `COLUMN_NAME` style.
  - Tables included:
    - `T_CLIENTS`
    - `T_LICENSES`
    - `T_LICENSE_ASSIGNMENTS`
    - `T_PROXIES`
    - `T_PURCHASE_SCHEDULES`
    - `T_PURCHASE_RESULTS`
    - `T_ADMIN_USERS`

---

### 2. `AutoPurchaseSystem_StoredProcedures.sql`
- **Purpose:** Creates all **stored procedures** for client pre-login, sending schedules, checking purchase results, and admin license management.
- **Features:**
  - Uses `CREATE OR ALTER PROCEDURE` for all SPs.
  - Stored procedures included:
    - `SP_CLIENT_PRELOGIN`
    - `SP_CLIENT_SEND_SCHEDULE`
    - `SP_GET_PURCHASE_STATUS`
    - `SP_ADMIN_ADD_LICENSE`
    - `SP_ADMIN_UPDATE_LICENSE`
    - `SP_ADMIN_DELETE_LICENSE`
    - `SP_ADMIN_LIST_LICENSES`

---

### 3. `AutoPurchaseSystem_DataDump_5k.sql`
- **Purpose:** Populates tables with **sample data** for testing.
- **Features:**
  - Generates 5,000 records for main tables (`T_CLIENTS`, `T_LICENSES`, `T_LICENSE_ASSIGNMENTS`, `T_PURCHASE_SCHEDULES`, `T_PURCHASE_RESULTS`).
  - Generates 100 proxies in `T_PROXIES` and 50 admin users in `T_ADMIN_USERS`.
  - Maintains **foreign key relationships** between tables.
  - Provides realistic data for client, license, schedule, proxy, and purchase results.
  - Randomizes purchase times and status messages for realistic testing.

---

## **Execution Order**

1. **Create Tables**
   ```sql
   EXECUTE AutoPurchaseSystem_CreateTables.sql

2. **Create SPs**
   ```sql
   EXECUTE AutoPurchaseSystem_StoredProcedures.sql

3. **Data Dump**
   ```sql
   EXECUTE AutoPurchaseSystem_DataDump_5k.sql

