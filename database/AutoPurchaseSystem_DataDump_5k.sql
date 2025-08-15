USE AUTO_PURCHASE_SYSTEM;
GO

-- =====================================
-- 1. T_CLIENTS (5000 records)
-- =====================================
WITH nums AS (
    SELECT TOP (5000) ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS n
    FROM master..spt_values a CROSS JOIN master..spt_values b
)
INSERT INTO T_CLIENTS (CLIENT_ID, MACHINE_ID, CREATED_AT)
SELECT NEWID(),
       'MACHINE_' + RIGHT('00000' + CAST(n AS NVARCHAR), 5),
       DATEADD(DAY, - (n % 365), GETDATE())
FROM nums;
GO

-- =====================================
-- 2. T_LICENSES (5000 records)
-- =====================================
WITH nums AS (
    SELECT TOP (5000) ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS n
    FROM master..spt_values a CROSS JOIN master..spt_values b
)
INSERT INTO T_LICENSES (LICENSE_ID, LICENSE_KEY, CREATED_AT, EXPIRED_AT, IS_ACTIVE)
SELECT NEWID(),
       'LICENSE_' + RIGHT('00000' + CAST(n AS NVARCHAR), 5),
       GETDATE(),
       DATEADD(DAY, 30 + n % 365, GETDATE()),
       1
FROM nums;
GO

-- =====================================
-- 3. T_LICENSE_ASSIGNMENTS (1:1 client-license)
-- =====================================
INSERT INTO T_LICENSE_ASSIGNMENTS (ASSIGNMENT_ID, LICENSE_ID, CLIENT_ID, IS_LOGGED_IN, LAST_LOGIN_AT, LAST_LOGOUT_AT, ASSIGNED_AT)
SELECT NEWID(), L.LICENSE_ID, C.CLIENT_ID, 0, NULL, NULL, GETDATE()
FROM (SELECT TOP 5000 LICENSE_ID, ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS rn FROM T_LICENSES) L
INNER JOIN (SELECT TOP 5000 CLIENT_ID, ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS rn FROM T_CLIENTS) C
ON L.rn = C.rn;
GO

-- =====================================
-- 4. T_PROXIES (100 records)
-- =====================================
WITH nums AS (
    SELECT TOP (100) ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS n
    FROM master..spt_values
)
INSERT INTO T_PROXIES (PROXY_ID, PROXY_ADDRESS, ACCOUNT_USERNAME, ACCOUNT_PASSWORD, CREATED_AT)
SELECT NEWID(),
       '192.168.' + CAST(n / 256 AS NVARCHAR) + '.' + CAST(n % 256 AS NVARCHAR) + ':8080',
       'user' + CAST(n AS NVARCHAR),
       'pass' + CAST(n AS NVARCHAR),
       GETDATE()
FROM nums;
GO

-- =====================================
-- 5. T_PURCHASE_SCHEDULES (5000 records)
-- =====================================
WITH nums AS (
    SELECT TOP (5000) ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS n
    FROM master..spt_values a CROSS JOIN master..spt_values b
)
INSERT INTO T_PURCHASE_SCHEDULES (SCHEDULE_ID, CLIENT_ID, PRODUCT_ID, QUANTITY, PURCHASE_TIME, COOKIE, PROXY_ID, STATUS, CREATED_AT)
SELECT NEWID(),
       C.CLIENT_ID,
       'PROD_' + RIGHT('00000' + CAST(n AS NVARCHAR), 5),
       1 + (n % 5),
       DATEADD(MINUTE, n % 1440, GETDATE()),
       'COOKIE_' + CAST(n AS NVARCHAR),
       P.PROXY_ID,
       'PENDING',
       GETDATE()
FROM nums
INNER JOIN (SELECT CLIENT_ID, ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS rn FROM T_CLIENTS) C
ON nums.n = C.rn
INNER JOIN (SELECT PROXY_ID, ROW_NUMBER() OVER(ORDER BY NEWID()) AS rn FROM T_PROXIES) P
ON ((nums.n - 1) % 100) + 1 = P.rn;
GO

-- =====================================
-- 6. T_PURCHASE_RESULTS (5000 records)
-- =====================================
WITH nums AS (
    SELECT TOP (5000) ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS n
    FROM master..spt_values a CROSS JOIN master..spt_values b
)
INSERT INTO T_PURCHASE_RESULTS (RESULT_ID, SCHEDULE_ID, IS_SUCCESS, RESPONSE_MESSAGE, CREATED_AT)
SELECT NEWID(),
       S.SCHEDULE_ID,
       CASE WHEN n % 2 = 0 THEN 1 ELSE 0 END,
       CASE WHEN n % 2 = 0 THEN 'SUCCESS' ELSE 'FAIL' END,
       GETDATE()
FROM nums
INNER JOIN (SELECT SCHEDULE_ID, ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS rn FROM T_PURCHASE_SCHEDULES) S
ON nums.n = S.rn;
GO

-- =====================================
-- 7. T_ADMIN_USERS (50 records)
-- =====================================
WITH nums AS (
    SELECT TOP (50) ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS n
    FROM master..spt_values
)
INSERT INTO T_ADMIN_USERS (ADMIN_ID, USERNAME, PASSWORD_HASH, CREATED_AT)
SELECT NEWID(),
       'ADMIN_' + RIGHT('00' + CAST(n AS NVARCHAR), 2),
       HASHBYTES('SHA2_256', 'password' + CAST(n AS NVARCHAR)),
       GETDATE()
FROM nums;
GO
