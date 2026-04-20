-- ============================================================================
-- ISOLATED EMPLOYEE SLICE — 9 таблиц для шаблонного генератора
-- Порядок: справочники → lookup → main → m2m → deferred FK
-- ============================================================================

-- ============================================
-- 1. sys.tenant (базовая зависимость)
-- ============================================

CREATE TABLE sys.tenant
(
    id         serial PRIMARY KEY,
    name       varchar(255) NOT NULL,
    code       varchar(50)  NOT NULL UNIQUE,
    is_active  boolean      NOT NULL DEFAULT true,
    created_at timestamp with time zone NOT NULL DEFAULT now()
);

-- ============================================
-- 2. org.ref_legal_form (справочник — нужен для company)
-- ============================================

CREATE TABLE org.ref_legal_form
(
    id               serial PRIMARY KEY,
    tenant_id        integer NOT NULL REFERENCES sys.tenant,
    name             varchar(255) NOT NULL,
    description      text,
    name_ru          varchar(255),
    name_kg          varchar(255),
    name_en          varchar(255),
    description_ru   text,
    description_kg   text,
    description_en   text,
    code             varchar(50) NOT NULL,
    sort_order       integer NOT NULL DEFAULT 0,
    foreground_color varchar(30),
    background_color varchar(30),
    is_active        boolean NOT NULL DEFAULT true,
    created_at       timestamp with time zone NOT NULL DEFAULT now(),
    created_by       integer,
    updated_at       timestamp with time zone NOT NULL DEFAULT now(),
    updated_by       integer,
    UNIQUE (tenant_id, code)
);

CREATE INDEX idx_ref_legal_form_tenant ON org.ref_legal_form (tenant_id);

-- ============================================
-- 3. org.ref_employee_position (справочник — dropdown на форме employee)
-- ============================================

CREATE TABLE org.ref_employee_position
(
    id               serial PRIMARY KEY,
    tenant_id        integer NOT NULL REFERENCES sys.tenant,
    name             varchar(255) NOT NULL,
    description      text,
    name_ru          varchar(255),
    name_kg          varchar(255),
    name_en          varchar(255),
    description_ru   text,
    description_kg   text,
    description_en   text,
    code             varchar(50) NOT NULL,
    sort_order       integer NOT NULL DEFAULT 0,
    foreground_color varchar(30),
    background_color varchar(30),
    is_active        boolean NOT NULL DEFAULT true,
    created_at       timestamp with time zone NOT NULL DEFAULT now(),
    created_by       integer,
    updated_at       timestamp with time zone NOT NULL DEFAULT now(),
    updated_by       integer,
    UNIQUE (tenant_id, code)
);

CREATE INDEX idx_ref_emp_position_tenant ON org.ref_employee_position (tenant_id);

-- ============================================
-- 4. org.ref_specialization (справочник — используется через M2M)
-- ============================================

CREATE TABLE org.ref_specialization
(
    id               serial PRIMARY KEY,
    tenant_id        integer NOT NULL REFERENCES sys.tenant,
    name             varchar(255) NOT NULL,
    description      text,
    name_ru          varchar(255),
    name_kg          varchar(255),
    name_en          varchar(255),
    description_ru   text,
    description_kg   text,
    description_en   text,
    code             varchar(50) NOT NULL,
    sort_order       integer NOT NULL DEFAULT 0,
    foreground_color varchar(30),
    background_color varchar(30),
    is_active        boolean NOT NULL DEFAULT true,
    created_at       timestamp with time zone NOT NULL DEFAULT now(),
    created_by       integer,
    updated_at       timestamp with time zone NOT NULL DEFAULT now(),
    updated_by       integer,
    UNIQUE (tenant_id, code)
);

CREATE INDEX idx_ref_specialization_tenant ON org.ref_specialization (tenant_id);

-- ============================================
-- 5. auth.user_account (lookup — опциональная привязка к employee)
-- ============================================

CREATE TABLE auth.user_account
(
    id            serial PRIMARY KEY,
    tenant_id     integer NOT NULL REFERENCES sys.tenant,
    login         varchar(100) NOT NULL,
    password_hash varchar(255) NOT NULL,
    email         varchar(255),
    is_active     boolean NOT NULL DEFAULT true,
    last_login    timestamp with time zone,
    created_at    timestamp with time zone NOT NULL DEFAULT now(),
    created_by    integer,
    updated_at    timestamp with time zone NOT NULL DEFAULT now(),
    updated_by    integer,
    totp_secret   varchar(64),
    totp_enabled  boolean NOT NULL DEFAULT false,
    UNIQUE (tenant_id, login)
);

CREATE INDEX idx_user_account_tenant ON auth.user_account (tenant_id);
CREATE INDEX idx_user_account_active ON auth.user_account (tenant_id) WHERE (is_active = true);

-- ============================================
-- 6. org.company (parent — организация сотрудника)
-- ============================================

CREATE TABLE org.company
(
    id            serial PRIMARY KEY,
    tenant_id     integer NOT NULL REFERENCES sys.tenant,
    name          varchar(500) NOT NULL,
    short_name    varchar(100),
    legal_form_id integer NOT NULL REFERENCES org.ref_legal_form,
    address       text,
    inn           varchar(30),
    okpo          varchar(30),
    phone         varchar(50),
    email         varchar(255),
    logo_path     text,
    is_active     boolean NOT NULL DEFAULT true,
    created_at    timestamp with time zone NOT NULL DEFAULT now(),
    created_by    integer,
    updated_at    timestamp with time zone NOT NULL DEFAULT now(),
    updated_by    integer
);

CREATE INDEX idx_company_tenant ON org.company (tenant_id);
CREATE INDEX idx_company_legal_form ON org.company (legal_form_id);

-- ============================================
-- 7. org.department (parent — отдел, self-ref + deferred FK на employee)
-- ============================================

CREATE TABLE org.department
(
    id               serial PRIMARY KEY,
    tenant_id        integer NOT NULL REFERENCES sys.tenant,
    company_id       integer NOT NULL REFERENCES org.company,
    parent_id        integer REFERENCES org.department,
    name             varchar(255) NOT NULL,
    code             varchar(50) NOT NULL,
    head_employee_id integer,  -- FK добавляется после создания employee
    sort_order       integer NOT NULL DEFAULT 0,
    is_active        boolean NOT NULL DEFAULT true,
    created_at       timestamp with time zone NOT NULL DEFAULT now(),
    created_by       integer,
    updated_at       timestamp with time zone NOT NULL DEFAULT now(),
    updated_by       integer
);

CREATE INDEX idx_department_tenant ON org.department (tenant_id);
CREATE INDEX idx_department_company ON org.department (company_id);
CREATE INDEX idx_department_parent ON org.department (parent_id) WHERE (parent_id IS NOT NULL);
CREATE INDEX idx_department_head ON org.department (head_employee_id) WHERE (head_employee_id IS NOT NULL);

-- ============================================
-- 8. org.employee (MAIN — основная таблица)
-- ============================================

CREATE TABLE org.employee
(
    id              serial PRIMARY KEY,
    tenant_id       integer NOT NULL REFERENCES sys.tenant,
    company_id      integer NOT NULL REFERENCES org.company,
    department_id   integer REFERENCES org.department,
    position_id     integer NOT NULL REFERENCES org.ref_employee_position,
    user_account_id integer CONSTRAINT fk_employee_user_account REFERENCES auth.user_account,
    last_name       varchar(100) NOT NULL,
    first_name      varchar(100) NOT NULL,
    middle_name     varchar(100),
    phone           varchar(50),
    email           varchar(255),
    hire_date       date NOT NULL,
    dismiss_date    date,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamp with time zone NOT NULL DEFAULT now(),
    created_by      integer,
    updated_at      timestamp with time zone NOT NULL DEFAULT now(),
    updated_by      integer,
    photo_path      varchar(500)
);

CREATE INDEX idx_employee_tenant ON org.employee (tenant_id);
CREATE INDEX idx_employee_company ON org.employee (company_id);
CREATE INDEX idx_employee_department ON org.employee (department_id) WHERE (department_id IS NOT NULL);
CREATE INDEX idx_employee_position ON org.employee (position_id);
CREATE INDEX idx_employee_user_account ON org.employee (user_account_id) WHERE (user_account_id IS NOT NULL);
CREATE INDEX idx_employee_active ON org.employee (tenant_id) WHERE (is_active = true);

-- ============================================
-- 9. org.employee_specialization (M2M — таб «Специализации» на форме employee)
-- ============================================

CREATE TABLE org.employee_specialization
(
    id                  serial PRIMARY KEY,
    tenant_id           integer NOT NULL REFERENCES sys.tenant,
    employee_id         integer NOT NULL REFERENCES org.employee,
    specialization_id   integer NOT NULL REFERENCES org.ref_specialization,
    is_primary          boolean NOT NULL DEFAULT false,
    certificate_series  varchar(50),
    certificate_number  varchar(50),
    certificate_issued  date,
    certificate_expires date,
    created_at          timestamp with time zone NOT NULL DEFAULT now(),
    created_by          integer,
    updated_at          timestamp with time zone NOT NULL DEFAULT now(),
    updated_by          integer,
    UNIQUE (employee_id, specialization_id)
);

CREATE INDEX idx_emp_spec_tenant ON org.employee_specialization (tenant_id);
CREATE INDEX idx_emp_spec_employee ON org.employee_specialization (employee_id);
CREATE INDEX idx_emp_spec_spec ON org.employee_specialization (specialization_id);

-- ============================================
-- DEFERRED FK: department.head_employee_id → employee
-- (циклическая зависимость, добавляется после обеих таблиц)
-- ============================================

ALTER TABLE org.department
    ADD CONSTRAINT fk_department_head_employee
        FOREIGN KEY (head_employee_id) REFERENCES org.employee;
