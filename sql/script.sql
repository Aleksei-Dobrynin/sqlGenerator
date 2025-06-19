create table org_structure
(
    id           serial
        primary key,
    parent_id    integer,
    unique_id    text,
    name         text,
    version      text,
    is_active    boolean,
    date_start   timestamp,
    date_end     timestamp,
    remote_id    text,
    created_at   timestamp,
    updated_at   timestamp,
    created_by   integer,
    updated_by   integer,
    short_name   text,
    code         varchar,
    order_number integer
);

alter table org_structure
    owner to postgres;

create table workflow
(
    name       text,
    is_active  boolean,
    date_start timestamp,
    date_end   timestamp,
    created_at timestamp,
    updated_at timestamp,
    created_by integer,
    updated_by integer,
    id         serial
        primary key,
    name_kg    text
);

alter table workflow
    owner to postgres;

create table workflow_task_template
(
    id           integer default nextval('workflow_task_template_id_seq'::regclass) not null
        constraint "XPKШаблон_задачи_Workflow"
            primary key,
    workflow_id  integer
        constraint r_1729
            references workflow,
    name         text,
    "order"      integer,
    is_active    boolean,
    is_required  boolean,
    description  text,
    created_at   timestamp,
    updated_at   timestamp,
    created_by   integer,
    updated_by   integer,
    structure_id integer,
    type_id      integer,
    district_id  integer
);

alter table workflow_task_template
    owner to postgres;

