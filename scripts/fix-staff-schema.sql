-- One-time repair: EnsureCreated left AdminUsers + ContactSubmissions
-- without Staff or __EFMigrationsHistory, so MigrateAsync cannot apply.
-- Run against jamago_db (pgAdmin or psql) while SSH tunnel is up.

CREATE TABLE IF NOT EXISTS "Staff" (
  "Id" uuid NOT NULL,
  "FullName" character varying(150) NOT NULL,
  "Role" character varying(120) NOT NULL,
  "Responsibility" character varying(1000) NOT NULL,
  "Department" character varying(120) NULL,
  "DisplayOrder" integer NOT NULL,
  "IsActive" boolean NOT NULL,
  "CreatedAt" timestamp with time zone NOT NULL,
  "UpdatedAt" timestamp with time zone NULL,
  CONSTRAINT "PK_Staff" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_Staff_DisplayOrder"
ON "Staff" ("DisplayOrder");

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
  "MigrationId" character varying(150) NOT NULL,
  "ProductVersion" character varying(32) NOT NULL,
  CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260716094755_InitialCreate', '10.0.9')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO "Staff"
  ("Id", "FullName", "Role", "Responsibility", "Department", "DisplayOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT * FROM (VALUES
  (gen_random_uuid(), 'Operations Lead', 'Field Operations',
   'Oversees manned guarding, patrol routes, and on-site incident response.',
   'Operations', 1, true, NOW(), NULL::timestamptz),
  (gen_random_uuid(), 'Client Success Manager', 'Account Management',
   'Main point of contact for reporting, scheduling, and service planning.',
   'Client Services', 2, true, NOW(), NULL::timestamptz),
  (gen_random_uuid(), 'Technical Supervisor', 'Systems & Monitoring',
   'Leads CCTV, access control, and alarm monitoring across client sites.',
   'Technical', 3, true, NOW(), NULL::timestamptz)
) AS v("Id", "FullName", "Role", "Responsibility", "Department", "DisplayOrder", "IsActive", "CreatedAt", "UpdatedAt")
WHERE NOT EXISTS (SELECT 1 FROM "Staff" LIMIT 1);
