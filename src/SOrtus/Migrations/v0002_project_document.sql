-- v0002_project_document.sql
-- 编程详情页 V2：立项版本号列 + 项目文档表（PRD / 迭代规划 / 其他）

ALTER TABLE Project ADD COLUMN Version TEXT NOT NULL DEFAULT '';

CREATE TABLE IF NOT EXISTS ProjectDocument (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    ProjectId      INTEGER NOT NULL,
    Category       TEXT    NOT NULL,
    Title          TEXT    NOT NULL DEFAULT '',
    SourceFileName TEXT    NOT NULL DEFAULT '',
    Format         TEXT    NOT NULL DEFAULT 'md',
    Content        TEXT    NOT NULL DEFAULT '',
    SortOrder      INTEGER NOT NULL DEFAULT 0,
    CreatedAt      TEXT    NOT NULL DEFAULT '',
    UpdatedAt      TEXT    NOT NULL DEFAULT '',
    FOREIGN KEY (ProjectId) REFERENCES Project (Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_ProjectDocument_Project ON ProjectDocument (ProjectId, Category, SortOrder);

CREATE UNIQUE INDEX IF NOT EXISTS UX_ProjectDocument_Slot ON ProjectDocument (ProjectId, Category) WHERE Category = 'Prd' OR Category = 'IterationPlan';
