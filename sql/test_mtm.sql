-- Тестовая схема для проверки CRUD + mtm
-- employee (main) -> employee_contact (mtm) -> contact_type (dictionary)

CREATE TABLE contact_type (
    id serial PRIMARY KEY,
    name varchar(255) NOT NULL,
    code varchar(50) NOT NULL,
    description varchar(500),
    created_at timestamp,
    updated_at timestamp,
    created_by int,
    updated_by int
);

CREATE TABLE employee (
    id serial PRIMARY KEY,
    name varchar(255) NOT NULL,
    last_name varchar(255),
    birth_date date,
    created_at timestamp,
    updated_at timestamp,
    created_by int,
    updated_by int
);

CREATE TABLE employee_contact (
    id serial PRIMARY KEY,
    value varchar(255) NOT NULL,
    employee_id int NOT NULL REFERENCES employee(id),
    type_id int NOT NULL REFERENCES contact_type(id),
    allow_notification boolean DEFAULT false,
    created_at timestamp,
    updated_at timestamp,
    created_by int,
    updated_by int
);
