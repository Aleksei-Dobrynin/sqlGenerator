create table employee
(
    id          serial
        primary key,
    last_name   text,
    first_name  text,
    second_name text,
    pin         text,
    remote_id   text,
    user_id     text,
    created_at  timestamp,
    updated_at  timestamp,
    created_by  integer,
    updated_by  integer,
    telegram    text,
    email       text,
    guid        text
);

alter table employee
    owner to postgres;

create table employee_contact
(
    id                 serial
        primary key,
    value              text,
    employee_id        integer,
    allow_notification boolean,
    type_id            integer,
    created_at         timestamp,
    created_by         integer,
    updated_at         timestamp,
    updated_by         integer
);

alter table employee_contact
    owner to postgres;


create table employee_in_structure
(
    id           serial
        primary key,
    employee_id  integer   not null
        references employee,
    date_start   timestamp not null,
    date_end     timestamp,
    created_at   timestamp,
    updated_at   timestamp,
    created_by   integer,
    updated_by   integer,
    structure_id integer   not null
        references org_structure,
    post_id      integer,
    is_temporary boolean,
    district_id  integer
);

alter table employee_in_structure
    owner to postgres;
