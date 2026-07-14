-- Run in pgAdmin as postgres (or any superuser).
-- Set PASSWORD to match GOLDEN-RULES.txt / appsettings.Development.json (never commit real secrets).

DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'jamago_admin') THEN
    CREATE ROLE jamago_admin LOGIN PASSWORD 'CHANGE_ME';
  ELSE
    ALTER ROLE jamago_admin WITH LOGIN PASSWORD 'CHANGE_ME';
  END IF;
END
$$;

-- If jamago_db does not exist yet, run:
-- CREATE DATABASE jamago_db OWNER jamago_admin;
