# SQLite Recovery Checklist for Windows

## Summary

This note documents a successful recovery of a malformed SQLite database using `sqlite3.exe`, plus a practical checklist for future incidents.

### Results from this recovery

- Database involved: `localhistory.db`
- Related SQLite WAL files were present:
  - `localhistory.db-wal`
  - `localhistory.db-shm`
- Recovery command used:

```powershell
sqlite3.exe localhistory.db ".recover" | sqlite3.exe localhistory_recovered.db
```

- Validation result:

```powershell
sqlite3.exe localhistory_recovered.db "PRAGMA integrity_check;"
```

Output:

```text
ok
```

- The recovered database opened successfully in the application.
- Interactive `sqlite3.exe` looked like it was hanging only when run from **PowerShell ISE**.
- Running the same command in **regular PowerShell** worked immediately.

> Conclusion: the recovery succeeded, and the later interactive issue was a shell/terminal issue, not a database issue.

---

## What the extra SQLite files mean

When SQLite uses **WAL mode**, these files are normal:

| File | Meaning | Keep it? |
|---|---|---|
| `*.db` | Main database file | Yes |
| `*.db-wal` | Write-ahead log containing recent committed changes not yet checkpointed into the main DB | Yes |
| `*.db-shm` | Shared memory helper file for WAL coordination | Usually yes; can be recreated |

> Do not assume `-wal` and `-shm` mean corruption. They are often normal.

---

## Safe recovery checklist

### 1. Close the application

Before doing anything:

- close the app that uses the database
- make sure no background process is still holding the file open

### 2. Back up all related files

Copy all SQLite-related files before testing anything:

- `database.db`
- `database.db-wal`
- `database.db-shm`

Work only on the copies.

### 3. Run an integrity check first

```powershell
sqlite3.exe database.db "PRAGMA integrity_check;"
```

If the output is:

```text
ok
```

then the database structure is valid.

If the output mentions corruption or `database disk image is malformed`, continue with recovery.

### 4. Try recovery into a new database

Use a **new** output file. Do not try to repair the original in place.

```powershell
sqlite3.exe database.db ".recover" | sqlite3.exe database_recovered.db
```

If needed, try the variant below:

```powershell
sqlite3.exe database.db ".recover --ignore-freelist" | sqlite3.exe database_recovered.db
```

### 5. Validate the recovered database

```powershell
sqlite3.exe database_recovered.db "PRAGMA integrity_check;"
```

Expected output:

```text
ok
```

### 6. Inspect the recovered contents

Check that important objects and data exist.

```powershell
sqlite3.exe database_recovered.db ".tables"
sqlite3.exe database_recovered.db ".schema"
sqlite3.exe database_recovered.db "SELECT name FROM sqlite_schema WHERE type='table' ORDER BY name;"
```

If you know important table names, also compare row counts:

```powershell
sqlite3.exe database_recovered.db "SELECT COUNT(*) FROM ImportantTable;"
```

### 7. Test in the application

If the recovered database passes integrity checks:

- open it in the application
- verify that the expected data is visible
- confirm key features work normally

### 8. Replace carefully

Only after validation:

1. keep the original files as backups
2. rename the damaged original files
3. copy the recovered DB into place as the active `*.db`
4. do **not** keep the old `-wal` and `-shm` beside the recovered database
5. let SQLite recreate fresh WAL files if needed

---

## Useful SQLite commands

### Integrity check

```powershell
sqlite3.exe database.db "PRAGMA integrity_check;"
```

### Quick schema/table listing

```powershell
sqlite3.exe database.db ".tables"
sqlite3.exe database.db ".schema"
```

### Read-only open

Useful when testing without write activity:

```powershell
sqlite3.exe -readonly database.db
```

### Backup a valid database

```powershell
sqlite3.exe database.db ".backup database_backup.db"
```

### Checkpoint WAL

```powershell
sqlite3.exe database.db "PRAGMA wal_checkpoint(FULL);"
```

### Compact the recovered database

```powershell
sqlite3.exe database_recovered.db "VACUUM;"
```

---

## Terminal guidance on Windows

### Use these for interactive `sqlite3.exe`

- **PowerShell**
- **Command Prompt (`cmd.exe`)**
- **Windows Terminal**

### Avoid this for interactive SQLite work

- **PowerShell ISE**

PowerShell ISE can make console programs appear to hang or become unresponsive even when the program itself is fine.

### Symptom seen in this case

- one-shot commands such as `".tables"` worked
- starting an interactive SQLite session appeared to hang
- regular PowerShell solved the problem immediately

> If `sqlite3.exe` behaves strangely only in ISE, switch shells before assuming the database is damaged.

---

## PATH note

If `sqlite3.exe` works in one shell but not another, the cause is often environment inheritance.

Examples:

- updating `PATH` in one running PowerShell session does not automatically update already-open `cmd.exe` windows
- after changing `PATH`, open a **new** shell window
- or use the full path to `sqlite3.exe`

Example:

```powershell
C:\path\to\sqlite3.exe database.db
```

---

## Recommended standard procedure

For future incidents, use this order:

1. close the application
2. copy `*.db`, `*.db-wal`, and `*.db-shm`
3. run `PRAGMA integrity_check;`
4. if malformed, run `.recover` into a **new** DB
5. run `PRAGMA integrity_check;` on the recovered DB
6. inspect tables/schema
7. test in the application
8. replace only after confirming the recovered DB is good
9. use regular PowerShell, `cmd.exe`, or Windows Terminal for interactive work

---

## One-line recovery reference

```powershell
sqlite3.exe database.db ".recover" | sqlite3.exe database_recovered.db
sqlite3.exe database_recovered.db "PRAGMA integrity_check;"
```

If the second command returns `ok`, continue with data validation and application testing.
