-- v0001_CreateProject.sql
-- 立项基表 + 迁移版本表（幂等，可重复执行）

CREATE TABLE IF NOT EXISTS SchemaVersion (
    Version   INTEGER PRIMARY KEY,
    AppliedAt TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS Project (
    Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    Name               TEXT    NOT NULL,
    Type               INTEGER NOT NULL,
    Status             INTEGER NOT NULL,
    CreatedAt          TEXT    NOT NULL,
    UpdatedAt          TEXT    NOT NULL,
    TechStack          TEXT    NOT NULL DEFAULT '',
    Summary            TEXT    NOT NULL DEFAULT '',
    SubType            TEXT    NOT NULL DEFAULT '',
    FolderPath         TEXT    NOT NULL DEFAULT '',
    ScheduleStart      TEXT,
    ScheduleEnd        TEXT,
    DevPlan            TEXT    NOT NULL DEFAULT '',
    Constraints        TEXT    NOT NULL DEFAULT '',
    Goals              TEXT    NOT NULL DEFAULT '',
    AcceptanceCriteria TEXT    NOT NULL DEFAULT '',
    ProgressPercent    REAL    NOT NULL DEFAULT 0
);
